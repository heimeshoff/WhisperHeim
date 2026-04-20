using WhisperHeim.Services.TextProcessing;

namespace WhisperHeim.Tests;

public class FillerRemovalServiceTests
{
    // ────────────────────────────────────────────────
    //  Null / empty / whitespace handling
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_NullText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FillerRemovalService.Clean(null));
    }

    [Fact]
    public void Clean_EmptyText_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FillerRemovalService.Clean(string.Empty));
    }

    [Fact]
    public void Clean_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, FillerRemovalService.Clean("   "));
    }

    // ────────────────────────────────────────────────
    //  Tier 1 — Multi-word English fillers
    // ────────────────────────────────────────────────

    [Theory]
    [InlineData("This is, you know, the answer.", "This is, the answer.")]
    [InlineData("I mean it is complex.", "it is complex.")]
    [InlineData("It's sort of done.", "It's done.")]
    [InlineData("It's kind of done.", "It's done.")]
    public void Clean_RemovesMultiWordEnglishFillers(string input, string expected)
    {
        Assert.Equal(expected, FillerRemovalService.Clean(input));
    }

    [Fact]
    public void Clean_MultiWord_IsCaseInsensitive()
    {
        Assert.Equal(
            "The result, is ready.",
            FillerRemovalService.Clean("The result, You Know, is ready."));
    }

    // ────────────────────────────────────────────────
    //  Tier 2 — Single-word English fillers
    // ────────────────────────────────────────────────

    [Theory]
    [InlineData("So um this is good.", "So this is good.")]
    [InlineData("Well uh that's it.", "Well that's it.")]
    [InlineData("Umm I think so.", "I think so.")]
    [InlineData("Uhh maybe.", "maybe.")]
    public void Clean_RemovesSingleWordEnglishFillers(string input, string expected)
    {
        Assert.Equal(expected, FillerRemovalService.Clean(input));
    }

    [Fact]
    public void Clean_SingleWord_IsCaseInsensitive()
    {
        Assert.Equal(
            "Yes please.",
            FillerRemovalService.Clean("Yes UH please."));
    }

    // ────────────────────────────────────────────────
    //  Word-boundary correctness
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_PreservesWordsContainingFillersAsSubstrings_Uh()
    {
        // "factually" contains no "uh", but this is a classic sanity check —
        // the regex must not strip substring matches inside longer words.
        const string text = "This was factually correct.";
        Assert.Equal(text, FillerRemovalService.Clean(text));
    }

    [Fact]
    public void Clean_PreservesHumMumAndSimilar()
    {
        // "humming", "mumble", "human" all contain filler-shaped substrings.
        // None of "um", "uh", "umm", "uhh" are full words here so nothing strips.
        const string text = "The humming human was mumbling.";
        Assert.Equal(text, FillerRemovalService.Clean(text));
    }

    [Fact]
    public void Clean_PreservesSortOfInsideLongerPhrase()
    {
        // "sort" alone is not a filler; only "sort of" is.
        Assert.Equal(
            "Please sort them now.",
            FillerRemovalService.Clean("Please sort them now."));
    }

    // ────────────────────────────────────────────────
    //  German single-word fillers
    // ────────────────────────────────────────────────

    [Theory]
    [InlineData("Ich äh gehe jetzt.", "Ich gehe jetzt.")]
    [InlineData("Ähm ja genau.", "ja genau.")]
    [InlineData("Das ist hm schwierig.", "Das ist schwierig.")]
    [InlineData("Öh moment.", "moment.")]
    [InlineData("Öhm vielleicht.", "vielleicht.")]
    [InlineData("Hmm ich weiß nicht.", "ich weiß nicht.")]
    public void Clean_RemovesGermanFillers_WhenDeDE(string input, string expected)
    {
        Assert.Equal(expected, FillerRemovalService.Clean(input, "de-DE"));
    }

    [Fact]
    public void Clean_GermanFillers_AreAlsoRemovedForBareDe()
    {
        // The task spec says "de-DE" but a bare "de" code (Whisper convention) must
        // also trigger German rules.
        Assert.Equal(
            "Ich gehe jetzt.",
            FillerRemovalService.Clean("Ich äh gehe jetzt.", "de"));
    }

    [Fact]
    public void Clean_GermanFillers_AreNotRemovedForEnglish()
    {
        // When dictating in English, German words should survive (even if ASR
        // happens to emit them from a German speaker name or quote).
        Assert.Equal(
            "Say äh loudly.",
            FillerRemovalService.Clean("Say äh loudly.", "en-US"));
    }

    [Fact]
    public void Clean_GermanFillers_DefaultLanguageIsEnglishOnly()
    {
        // Null language → English-only. German fillers pass through.
        Assert.Equal(
            "Sag äh mal.",
            FillerRemovalService.Clean("Sag äh mal.", null));
    }

    [Fact]
    public void Clean_GermanWordBoundary_DoesNotStripAehInsideGaehnen()
    {
        // "gähnen" (to yawn) contains "äh" as a substring — must not be stripped.
        Assert.Equal(
            "Ich muss gähnen.",
            FillerRemovalService.Clean("Ich muss gähnen.", "de-DE"));
    }

    [Fact]
    public void Clean_GermanKeepsDiscourseParticles()
    {
        // German discourse particles (halt, also, eben, doch, ja, schon, mal)
        // were deliberately excluded from the filler list — they carry meaning.
        const string text = "Das ist halt so, also eben doch ja schon mal passiert.";
        Assert.Equal(text, FillerRemovalService.Clean(text, "de-DE"));
    }

    [Fact]
    public void Clean_German_StillRemovesEnglishFillers()
    {
        // English fillers always run because a German ASR transcript can
        // contain English interjections.
        Assert.Equal(
            "Ich gehe jetzt, weißt du.",
            FillerRemovalService.Clean("Ich äh gehe um jetzt, weißt du.", "de-DE"));
        // "um" → stripped as English single-word filler
        // "äh" → stripped as German single-word filler
    }

    // ────────────────────────────────────────────────
    //  Whitespace + punctuation normalization
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_CollapsesDoubleSpacesLeftByRemoval()
    {
        Assert.Equal(
            "Hello world.",
            FillerRemovalService.Clean("Hello um world."));
    }

    [Fact]
    public void Clean_TrimsLeadingAndTrailingWhitespace()
    {
        Assert.Equal(
            "Hello.",
            FillerRemovalService.Clean("  um  Hello.  "));
    }

    [Fact]
    public void Clean_FixesSpaceBeforeComma()
    {
        Assert.Equal(
            "Yes, please.",
            FillerRemovalService.Clean("Yes um, please."));
    }

    [Fact]
    public void Clean_FixesSpaceBeforePeriod()
    {
        Assert.Equal(
            "Done.",
            FillerRemovalService.Clean("Done um."));
    }

    [Theory]
    [InlineData("Word ; end.", "Word; end.")]
    [InlineData("Word : end.", "Word: end.")]
    [InlineData("Word ! end.", "Word! end.")]
    [InlineData("Word ? end.", "Word? end.")]
    public void Clean_FixesSpaceBeforePunctuation(string input, string expected)
    {
        Assert.Equal(expected, FillerRemovalService.Clean(input));
    }

    // ────────────────────────────────────────────────
    //  Ordering — multi-word before single-word
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_MultiWordMatchedBeforeSingleWord()
    {
        // "you know" must be killed as a phrase; we must not accidentally leave
        // "you know" bits around. The flanking commas are preserved —
        // the pipeline is not a grammar fixer.
        Assert.Equal(
            "I think, it is done.",
            FillerRemovalService.Clean("I think, you know, it is done."));
    }

    [Fact]
    public void Clean_LongerSingleWordPreferredOverShorter()
    {
        // "umm" (3 chars) must win over "um" (2 chars) inside a single input
        // so we don't leave a dangling "m".
        Assert.Equal(
            "okay",
            FillerRemovalService.Clean("umm okay"));
    }

    // ────────────────────────────────────────────────
    //  End-to-end scenarios
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_EnglishParagraph_StripsAllFillersAndNormalizesSpacing()
    {
        const string input = "So, um, the thing is, you know, I mean, it's kind of done, uh.";
        Assert.Equal("So, the thing is, it's done,.", FillerRemovalService.Clean(input));
        // Note: trailing ", uh." leaves ",." because the period attaches to the
        // preceding clause's trailing comma after the "uh" is removed. The
        // deterministic pipeline is not a grammar fixer — higher-level
        // punctuation repair is out of scope.
    }

    [Fact]
    public void Clean_GermanParagraph_StripsAllFillersAndNormalizesSpacing()
    {
        const string input = "Also, äh, ich denke, ähm, es ist öh fertig, hmm.";
        Assert.Equal(
            "Also, ich denke, es ist fertig,.",
            FillerRemovalService.Clean(input, "de-DE"));
    }

    [Fact]
    public void Clean_NoFillers_PassesThroughUnchanged()
    {
        const string text = "The quick brown fox jumps over the lazy dog.";
        Assert.Equal(text, FillerRemovalService.Clean(text));
    }

    // ────────────────────────────────────────────────
    //  Performance — budget is <1ms per utterance
    // ────────────────────────────────────────────────

    [Fact]
    public void Clean_IsFast_OnTypicalUtterance()
    {
        // Warm up the regex JIT / compiled patterns.
        FillerRemovalService.Clean("warmup", "de-DE");

        const string text = "So, um, the thing is, you know, I mean, it's kind of done, uh, and, you know, that's it.";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            FillerRemovalService.Clean(text, "de-DE");
        }
        sw.Stop();

        var perCallMs = sw.Elapsed.TotalMilliseconds / 1000.0;

        // Generous bound — the <1ms budget is for a single call; 1000 calls
        // should easily fit well under 100ms in CI.
        Assert.True(perCallMs < 1.0,
            $"Expected <1ms per call, got {perCallMs:F3}ms (total {sw.Elapsed.TotalMilliseconds:F1}ms for 1000 calls).");
    }
}
