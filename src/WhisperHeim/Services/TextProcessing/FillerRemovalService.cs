using System.Text.RegularExpressions;

namespace WhisperHeim.Services.TextProcessing;

/// <summary>
/// Deterministic clean-text pipeline: strips verbal filler words from dictation
/// transcriptions before they reach the focused window.
///
/// Mirrors MacParakeet's shipped behavior (TextProcessingPipeline.swift): two
/// unconditional tiers — multi-word fillers first (so longer phrases are matched
/// before their single-word substrings), then single-word fillers — followed by
/// whitespace and punctuation-spacing normalization.
///
/// Runs synchronously; budget &lt;1ms per utterance.
/// </summary>
/// <remarks>
/// Word lists are hardcoded for v1. A future "custom words" task will layer
/// user-editable overrides on top of this service.
///
/// German word list is applied only when the dictation language is
/// <c>de-DE</c> (or bare <c>de</c>). Multi-word German fillers are intentionally
/// excluded because German discourse particles (<c>halt</c>, <c>also</c>,
/// <c>eben</c>, <c>doch</c>, <c>ja</c>, <c>schon</c>, <c>mal</c>) carry meaning.
/// </remarks>
public static class FillerRemovalService
{
    // Tier 1 — Multi-word English fillers (checked first so "you know" is
    // removed before any single-word pass; no multi-word German fillers in v1).
    private static readonly string[] EnglishMultiWord =
    {
        "you know",
        "I mean",
        "sort of",
        "kind of",
    };

    // Tier 2 — Single-word English fillers.
    private static readonly string[] EnglishSingleWord =
    {
        "um",
        "uh",
        "umm",
        "uhh",
    };

    // Tier 2 — Single-word German fillers. Applied only for de-DE.
    private static readonly string[] GermanSingleWord =
    {
        "äh",
        "ähm",
        "hm",
        "hmm",
        "öh",
        "öhm",
    };

    // Pre-compiled regexes. Word-boundary-anchored (`\b…\b`), case-insensitive.
    // Word lists are sorted by descending length so that, for example, "umm"
    // is matched before "um" inside a single alternation (regex engines match
    // left-to-right greedily within a group).
    private static readonly Regex EnglishMultiWordRegex =
        BuildRegex(EnglishMultiWord);

    private static readonly Regex EnglishSingleWordRegex =
        BuildRegex(EnglishSingleWord);

    private static readonly Regex GermanSingleWordRegex =
        BuildRegex(GermanSingleWord);

    // Collapse runs of 2+ whitespace characters into a single space.
    private static readonly Regex MultipleSpacesRegex =
        new(@"\s{2,}", RegexOptions.Compiled);

    // Fix " ," / " ." / " ;" etc. → no space before punctuation.
    // Covers the ASCII punctuation that can appear in dictation output.
    private static readonly Regex SpaceBeforePunctuationRegex =
        new(@"\s+([,.;:!?])", RegexOptions.Compiled);

    // Collapse repeated comma / semicolon runs left behind after a filler that
    // was flanked by punctuation (e.g. "Yes, um, please" → "Yes, , please" → "Yes, please").
    // Periods are intentionally excluded: "..." (ellipsis) is meaningful.
    private static readonly Regex RepeatedCommaRegex =
        new(@"([,;])(\s*\1)+", RegexOptions.Compiled);

    /// <summary>
    /// Strips filler words from <paramref name="text"/> and normalizes
    /// whitespace and punctuation spacing.
    /// </summary>
    /// <param name="text">Raw ASR output. May be null or empty.</param>
    /// <param name="language">
    /// Dictation language code, e.g. <c>en-US</c>, <c>de-DE</c>, <c>en</c>,
    /// <c>de</c>. Case-insensitive. Null or empty is treated as English.
    /// When the language starts with <c>de</c>, the German filler list is
    /// applied in addition to English (English always runs because ASR may
    /// emit English interjections in a German context).
    /// </param>
    /// <returns>
    /// The cleaned text. Returns <paramref name="text"/> unchanged (or empty)
    /// when input is null/empty.
    /// </returns>
    public static string Clean(string? text, string? language = null)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        // Tier 1: multi-word English fillers first so "you know" is killed
        // before we'd otherwise have to worry about single-word overlap.
        var result = EnglishMultiWordRegex.Replace(text, string.Empty);

        // Tier 2: single-word English fillers.
        result = EnglishSingleWordRegex.Replace(result, string.Empty);

        // Tier 2 (conditional): single-word German fillers.
        if (IsGerman(language))
        {
            result = GermanSingleWordRegex.Replace(result, string.Empty);
        }

        // Whitespace + punctuation normalization.
        // Order matters: first drop spaces before punctuation (so "Yes , please"
        // becomes "Yes, please"), then collapse runs of duplicate commas /
        // semicolons left behind by removed fillers ("Yes,, please" → "Yes, please"),
        // then collapse whitespace runs, then trim.
        result = SpaceBeforePunctuationRegex.Replace(result, "$1");
        result = RepeatedCommaRegex.Replace(result, "$1");
        result = MultipleSpacesRegex.Replace(result, " ");
        result = result.Trim();

        return result;
    }

    /// <summary>
    /// Returns true if <paramref name="language"/> is a German locale code
    /// (<c>de</c>, <c>de-DE</c>, <c>de-AT</c>, etc.). Case-insensitive.
    /// </summary>
    private static bool IsGerman(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return false;

        // Match "de" or anything starting with "de-" / "de_"
        if (language.Equals("de", StringComparison.OrdinalIgnoreCase))
            return true;

        return language.StartsWith("de-", StringComparison.OrdinalIgnoreCase)
            || language.StartsWith("de_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a case-insensitive, word-boundary-anchored alternation regex
    /// from a list of literal filler phrases. Phrases are sorted by descending
    /// length so the regex engine prefers longer matches first (e.g. "umm"
    /// before "um" within the alternation).
    /// </summary>
    private static Regex BuildRegex(string[] words)
    {
        // Longest-first so e.g. "umm" wins over "um" when both start at the
        // same offset. Regex.Escape handles any future additions that contain
        // regex metacharacters; spaces inside a phrase (e.g. "you know") are
        // preserved literally.
        var ordered = words
            .OrderByDescending(w => w.Length)
            .Select(Regex.Escape);

        var pattern = @"\b(?:" + string.Join("|", ordered) + @")\b";
        return new Regex(pattern,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
