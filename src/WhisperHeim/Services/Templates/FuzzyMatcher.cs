namespace WhisperHeim.Services.Templates;

/// <summary>
/// Provides fuzzy string matching for matching spoken template names
/// against stored template names. Uses Levenshtein distance normalized
/// by the longer string length.
/// </summary>
public static class FuzzyMatcher
{
    /// <summary>
    /// Finds the best matching template name from the candidates.
    /// Returns null if no candidate scores above the threshold.
    /// </summary>
    /// <param name="spoken">The spoken/transcribed text to match.</param>
    /// <param name="candidates">Available template names.</param>
    /// <param name="threshold">Minimum similarity score (0.0 to 1.0). Default 0.4.</param>
    /// <returns>The best matching candidate name, or null if none matched.</returns>
    public static string? FindBestMatch(string spoken, IEnumerable<string> candidates, double threshold = 0.4)
    {
        if (string.IsNullOrWhiteSpace(spoken))
            return null;

        var normalizedSpoken = Normalize(spoken);
        string? bestMatch = null;
        double bestScore = 0;

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var normalizedCandidate = Normalize(candidate);

            // Try exact containment first (highest priority)
            if (normalizedSpoken.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
                normalizedCandidate.Contains(normalizedSpoken, StringComparison.OrdinalIgnoreCase))
            {
                double containScore = 0.95;
                if (containScore > bestScore)
                {
                    bestScore = containScore;
                    bestMatch = candidate;
                }
                continue;
            }

            // Compute similarity via Levenshtein distance
            var score = ComputeSimilarity(normalizedSpoken, normalizedCandidate);
            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestScore >= threshold ? bestMatch : null;
    }

    /// <summary>
    /// Computes a similarity score between 0.0 and 1.0 for two strings.
    /// 1.0 = identical, 0.0 = completely different.
    /// </summary>
    public static double ComputeSimilarity(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        var maxLen = Math.Max(a.Length, b.Length);
        if (maxLen == 0)
            return 1.0;

        var distance = LevenshteinDistance(a.ToLowerInvariant(), b.ToLowerInvariant());
        return 1.0 - (double)distance / maxLen;
    }

    private static string Normalize(string input)
    {
        // Strip everything except letters and digits, then lowercase.
        // This makes matching ignore case, spaces, punctuation, and hyphens.
        var chars = input.Where(char.IsLetterOrDigit).ToArray();
        return new string(chars).ToLowerInvariant();
    }

    /// <summary>
    /// Computes the Levenshtein edit distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        // Use single-row optimization
        var previousRow = new int[m + 1];
        var currentRow = new int[m + 1];

        for (var j = 0; j <= m; j++)
            previousRow[j] = j;

        for (var i = 1; i <= n; i++)
        {
            currentRow[0] = i;

            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            // Swap rows
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[m];
    }
}
