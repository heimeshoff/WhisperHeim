using WhisperHeim.Services.CallTranscription;

namespace WhisperHeim.Tests;

public class SpeakerNameEditingTests
{
    private static CallTranscript CreateTestTranscript()
    {
        return new CallTranscript
        {
            Id = "test-1",
            Name = "Test Call",
            RecordingStartedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            RecordingEndedUtc = DateTimeOffset.UtcNow,
            Segments =
            [
                new TranscriptSegment { Speaker = "You", StartTime = TimeSpan.Zero, EndTime = TimeSpan.FromSeconds(5), Text = "Hello", IsLocalSpeaker = true },
                new TranscriptSegment { Speaker = "Other", StartTime = TimeSpan.FromSeconds(5), EndTime = TimeSpan.FromSeconds(10), Text = "Hi there", IsLocalSpeaker = false },
                new TranscriptSegment { Speaker = "You", StartTime = TimeSpan.FromSeconds(10), EndTime = TimeSpan.FromSeconds(15), Text = "How are you?", IsLocalSpeaker = true },
                new TranscriptSegment { Speaker = "Other", StartTime = TimeSpan.FromSeconds(15), EndTime = TimeSpan.FromSeconds(20), Text = "I'm fine", IsLocalSpeaker = false },
            ]
        };
    }

    [Fact]
    public void GetDisplaySpeaker_WithoutMappings_ReturnsOriginalLabel()
    {
        var transcript = CreateTestTranscript();
        var segment = transcript.Segments[0];

        Assert.Equal("You", transcript.GetDisplaySpeaker(segment));
    }

    [Fact]
    public void GetDisplaySpeaker_WithGlobalMapping_ReturnsMappedName()
    {
        var transcript = CreateTestTranscript();
        transcript.SpeakerNameMap["Other"] = "Alice";

        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[1]));
        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[3]));
    }

    [Fact]
    public void GetDisplaySpeaker_PerSegmentOverride_TakesPriority()
    {
        var transcript = CreateTestTranscript();
        transcript.SpeakerNameMap["Other"] = "Alice";
        transcript.Segments[3].SpeakerOverride = "Bob";

        // Segment 1 uses global mapping
        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[1]));
        // Segment 3 uses per-segment override
        Assert.Equal("Bob", transcript.GetDisplaySpeaker(transcript.Segments[3]));
    }

    [Fact]
    public void RenameSpeakerGlobally_UpdatesSpeakerNameMap()
    {
        var transcript = CreateTestTranscript();

        transcript.RenameSpeakerGlobally("Other", "Alice");

        Assert.Equal("Alice", transcript.SpeakerNameMap["Other"]);
        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[1]));
        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[3]));
    }

    [Fact]
    public void RenameSpeakerGlobally_ClearsRedundantPerSegmentOverrides()
    {
        var transcript = CreateTestTranscript();
        // Set a per-segment override to "Alice" first
        transcript.Segments[1].SpeakerOverride = "Alice";

        // Now do a global rename to "Alice" -- the override becomes redundant
        transcript.RenameSpeakerGlobally("Other", "Alice");

        Assert.Null(transcript.Segments[1].SpeakerOverride);
        Assert.Equal("Alice", transcript.GetDisplaySpeaker(transcript.Segments[1]));
    }

    [Fact]
    public void RenameSpeakerGlobally_PreservesNonMatchingOverrides()
    {
        var transcript = CreateTestTranscript();
        transcript.Segments[3].SpeakerOverride = "Bob";

        transcript.RenameSpeakerGlobally("Other", "Alice");

        // "Bob" override should remain because it's different from "Alice"
        Assert.Equal("Bob", transcript.Segments[3].SpeakerOverride);
        Assert.Equal("Bob", transcript.GetDisplaySpeaker(transcript.Segments[3]));
    }

    [Fact]
    public void RenameSpeakerGlobally_DoesNotAffectOtherSpeakers()
    {
        var transcript = CreateTestTranscript();

        transcript.RenameSpeakerGlobally("Other", "Alice");

        // "You" segments should be unaffected
        Assert.Equal("You", transcript.GetDisplaySpeaker(transcript.Segments[0]));
        Assert.Equal("You", transcript.GetDisplaySpeaker(transcript.Segments[2]));
    }

    [Fact]
    public void RenameSpeakerGlobally_EmptyName_DoesNothing()
    {
        var transcript = CreateTestTranscript();

        transcript.RenameSpeakerGlobally("Other", "");

        Assert.Empty(transcript.SpeakerNameMap);
    }

    [Fact]
    public void RenameSpeakerGlobally_SameName_DoesNothing()
    {
        var transcript = CreateTestTranscript();

        transcript.RenameSpeakerGlobally("Other", "Other");

        Assert.Empty(transcript.SpeakerNameMap);
    }

    [Fact]
    public void ExistingTranscripts_WithoutSpeakerNameMap_LoadCorrectly()
    {
        // Simulates loading an old transcript without speaker name map
        var transcript = CreateTestTranscript();

        // SpeakerNameMap should default to empty, not null
        Assert.NotNull(transcript.SpeakerNameMap);
        Assert.Empty(transcript.SpeakerNameMap);

        // All segments should still resolve to original names
        Assert.Equal("You", transcript.GetDisplaySpeaker(transcript.Segments[0]));
        Assert.Equal("Other", transcript.GetDisplaySpeaker(transcript.Segments[1]));
    }

    [Fact]
    public void SpeakerOverride_DefaultsToNull()
    {
        var segment = new TranscriptSegment
        {
            Speaker = "You",
            StartTime = TimeSpan.Zero,
            EndTime = TimeSpan.FromSeconds(5),
            Text = "Hello",
            IsLocalSpeaker = true
        };

        Assert.Null(segment.SpeakerOverride);
    }
}
