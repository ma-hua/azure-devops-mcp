namespace AzureMcp.Server.Utils;

public enum DiffLineKind { Context, Added, Removed }

/// <summary>A single line within a diff hunk, with explicit semantic kind and 1-based line numbers.</summary>
public sealed record DiffLine(DiffLineKind Kind, string Content, int? OldLineNumber, int? NewLineNumber);

/// <summary>A contiguous changed region, including surrounding context lines.</summary>
public sealed record DiffHunk(int OldStart, int OldCount, int NewStart, int NewCount, IReadOnlyList<DiffLine> Lines);

/// <summary>
/// Full structured diff result. <see cref="RawPatch"/> contains a standard unified-diff string built
/// from <see cref="Hunks"/>. When <see cref="IsTruncated"/> is true the response is paginated at hunk
/// boundaries; use <see cref="TotalHunkCount"/> and a cursor to fetch remaining hunks.
/// </summary>
public sealed record DiffResult(
    string RawPatch,
    IReadOnlyList<DiffHunk> Hunks,
    bool IsTruncated,
    int TotalHunkCount);

public static class UnifiedDiff
{
    /// <summary>
    /// Builds a structured diff. When <paramref name="hunkOffset"/> is &gt; 0 the first N hunks are
    /// skipped (cursor-based pagination). Truncation always occurs at hunk boundaries so no hunk is
    /// ever split mid-way; at least one hunk is always returned even if it exceeds <paramref name="maxChars"/>.
    /// <paramref name="maxLines"/> caps each side's line count; if exceeded an <see cref="InvalidOperationException"/>
    /// is thrown so callers can surface a structured error instead of hanging on an O(n×m) LCS computation.
    /// </summary>
    public static DiffResult Build(
        string beforeText,
        string afterText,
        int contextLines,
        int maxChars,
        int hunkOffset = 0,
        int maxLines = 2000)
    {
        var before = SplitLines(beforeText);
        var after = SplitLines(afterText);

        if (before.Length > maxLines || after.Length > maxLines)
            throw new InvalidOperationException(
                $"File is too large for in-process diff ({before.Length} × {after.Length} lines; limit is {maxLines} per side). " +
                $"Use get_pr_file_content to retrieve specific line ranges instead.");

        var lcs = BuildLcs(before, after);

        var allHunks = BuildAllHunks(before, after, lcs, contextLines);
        var totalHunkCount = allHunks.Count;

        var window = hunkOffset > 0
            ? allHunks.Skip(hunkOffset).ToList()
            : (IReadOnlyList<DiffHunk>)allHunks;

        // Select complete hunks that fit within maxChars; always include at least one.
        const string header = "--- before\n+++ after\n";
        var charBudget = maxChars - header.Length;
        var returned = new List<DiffHunk>();
        var usedChars = 0;

        foreach (var hunk in window)
        {
            var hunkLen = EstimateHunkLength(hunk);
            if (returned.Count > 0 && usedChars + hunkLen > charBudget)
                break;
            returned.Add(hunk);
            usedChars += hunkLen;
        }

        var isTruncated = returned.Count < window.Count;

        var sb = new System.Text.StringBuilder(header);
        foreach (var hunk in returned)
            AppendHunkText(sb, hunk);

        return new DiffResult(sb.ToString(), returned, isTruncated, totalHunkCount);
    }

    private static List<DiffHunk> BuildAllHunks(string[] before, string[] after, int[,] lcs, int contextLines)
    {
        var hunks = new List<DiffHunk>();
        var i = 0;
        var j = 0;

        while (i < before.Length || j < after.Length)
        {
            if (i < before.Length && j < after.Length && before[i] == after[j])
            {
                i++;
                j++;
                continue;
            }

            var startI = Math.Max(0, i - contextLines);
            var startJ = Math.Max(0, j - contextLines);
            var endI = i;
            var endJ = j;

            while (endI < before.Length || endJ < after.Length)
            {
                if (endI < before.Length && endJ < after.Length && lcs[endI + 1, endJ + 1] == lcs[endI, endJ])
                    break;

                if (endI < before.Length && (endJ == after.Length || lcs[endI + 1, endJ] >= lcs[endI, endJ + 1]))
                    endI++;
                else if (endJ < after.Length)
                    endJ++;
            }

            var ctxEndI = Math.Min(before.Length, endI + contextLines);
            var ctxEndJ = Math.Min(after.Length, endJ + contextLines);
            var oldCount = ctxEndI - startI;
            var newCount = ctxEndJ - startJ;
            var lines = new List<DiffLine>();

            // Leading context: lines [startI, i) in before correspond to [startJ, j) in after.
            for (var x = 0; x < i - startI; x++)
                lines.Add(new DiffLine(DiffLineKind.Context, before[startI + x], startI + x + 1, startJ + x + 1));

            // Diff region: walk bi (before index) and aj (after index) using the LCS table.
            var bi = i;
            var aj = j;
            while (bi < endI || aj < endJ)
            {
                if (bi < endI && aj < endJ && before[bi] == after[aj])
                {
                    lines.Add(new DiffLine(DiffLineKind.Context, before[bi], bi + 1, aj + 1));
                    bi++;
                    aj++;
                }
                else if (bi < endI && (aj == endJ || lcs[bi + 1, aj] >= lcs[bi, aj + 1]))
                {
                    lines.Add(new DiffLine(DiffLineKind.Removed, before[bi], bi + 1, null));
                    bi++;
                }
                else if (aj < endJ)
                {
                    lines.Add(new DiffLine(DiffLineKind.Added, after[aj], null, aj + 1));
                    aj++;
                }
            }

            // Trailing context: lines [endI, ctxEndI) in before correspond to [endJ, ctxEndJ) in after.
            for (var x = 0; x < ctxEndI - endI; x++)
                lines.Add(new DiffLine(DiffLineKind.Context, before[endI + x], endI + x + 1, endJ + x + 1));

            hunks.Add(new DiffHunk(startI + 1, oldCount, startJ + 1, newCount, lines));
            i = ctxEndI;
            j = ctxEndJ;
        }

        return hunks;
    }

    private static int EstimateHunkLength(DiffHunk hunk)
    {
        // "@@ -N,N +N,N @@\n" is at most ~40 chars; each line is prefix + content + newline.
        var len = 40;
        foreach (var line in hunk.Lines)
            len += 2 + line.Content.Length;
        return len;
    }

    private static void AppendHunkText(System.Text.StringBuilder sb, DiffHunk hunk)
    {
        sb.AppendLine($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");
        foreach (var line in hunk.Lines)
        {
            var prefix = line.Kind switch
            {
                DiffLineKind.Added => '+',
                DiffLineKind.Removed => '-',
                _ => ' '
            };
            sb.Append(prefix);
            sb.AppendLine(line.Content);
        }
    }

    private static string[] SplitLines(string text)
    {
        return (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static int[,] BuildLcs(string[] left, string[] right)
    {
        var matrix = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--)
        {
            for (var j = right.Length - 1; j >= 0; j--)
            {
                matrix[i, j] = left[i] == right[j]
                    ? matrix[i + 1, j + 1] + 1
                    : Math.Max(matrix[i + 1, j], matrix[i, j + 1]);
            }
        }

        return matrix;
    }
}
