namespace AzureMcp.Server.Utils;

public static class UnifiedDiff
{
    public static string Build(string beforeText, string afterText, int contextLines, int maxChars, out bool truncated)
    {
        var before = SplitLines(beforeText);
        var after = SplitLines(afterText);
        var lcs = BuildLcs(before, after);
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("--- before");
        sb.AppendLine("+++ after");

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
                {
                    break;
                }

                if (endI < before.Length && (endJ == after.Length || lcs[endI + 1, endJ] >= lcs[endI, endJ + 1]))
                {
                    endI++;
                }
                else if (endJ < after.Length)
                {
                    endJ++;
                }
            }

            var ctxEndI = Math.Min(before.Length, endI + contextLines);
            var ctxEndJ = Math.Min(after.Length, endJ + contextLines);
            sb.AppendLine($"@@ -{startI + 1},{Math.Max(1, ctxEndI - startI)} +{startJ + 1},{Math.Max(1, ctxEndJ - startJ)} @@");

            for (var x = startI; x < i; x++)
            {
                sb.AppendLine($" {before[x]}");
            }

            var bi = i;
            var aj = j;
            while (bi < endI || aj < endJ)
            {
                if (bi < endI && aj < endJ && before[bi] == after[aj])
                {
                    sb.AppendLine($" {before[bi]}");
                    bi++;
                    aj++;
                    continue;
                }

                if (bi < endI && (aj == endJ || lcs[bi + 1, aj] >= lcs[bi, aj + 1]))
                {
                    sb.AppendLine($"-{before[bi]}");
                    bi++;
                }
                else if (aj < endJ)
                {
                    sb.AppendLine($"+{after[aj]}");
                    aj++;
                }
            }

            for (var x = endI; x < ctxEndI; x++)
            {
                sb.AppendLine($" {before[x]}");
            }

            i = ctxEndI;
            j = ctxEndJ;
        }

        var full = sb.ToString();
        if (full.Length <= maxChars)
        {
            truncated = false;
            return full;
        }

        truncated = true;
        return full[..maxChars] + "\n... [truncated]";
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
