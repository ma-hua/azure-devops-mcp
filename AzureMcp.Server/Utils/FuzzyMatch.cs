namespace AzureMcp.Server.Utils;

public static class FuzzyMatch
{
    public static int Score(string query, string candidate)
    {
        if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(candidate))
        {
            return 0;
        }

        var q = query.Trim().ToLowerInvariant();
        var c = candidate.Trim().ToLowerInvariant();

        if (q == c)
        {
            return 120;
        }

        if (c.StartsWith(q, StringComparison.Ordinal))
        {
            return 90;
        }

        if (c.Contains(q, StringComparison.Ordinal))
        {
            return 70;
        }

        var distance = LevenshteinDistance(q, c);
        var maxLength = Math.Max(q.Length, c.Length);
        if (maxLength == 0)
        {
            return 0;
        }

        var normalized = (int)Math.Round((1.0 - (double)distance / maxLength) * 60);
        return Math.Max(0, normalized);
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var rows = left.Length + 1;
        var cols = right.Length + 1;
        var matrix = new int[rows, cols];

        for (var i = 0; i < rows; i++)
        {
            matrix[i, 0] = i;
        }

        for (var j = 0; j < cols; j++)
        {
            matrix[0, j] = j;
        }

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[rows - 1, cols - 1];
    }
}
