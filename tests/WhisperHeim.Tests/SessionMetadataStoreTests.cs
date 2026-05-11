using System.IO;
using WhisperHeim.Services.Recording;

namespace WhisperHeim.Tests;

/// <summary>
/// Covers <see cref="SessionMetadataStore"/>'s three-tier ownership resolution:
/// session.json wins, then trailing directory-name suffix, then null for
/// un-stamped legacy directories. Drives the multi-machine gating in
/// <c>TranscriptStorageService.ListPendingSessions</c>.
/// </summary>
public class SessionMetadataStoreTests : IDisposable
{
    private readonly string _testRoot;

    public SessionMetadataStoreTests()
    {
        _testRoot = Path.Combine(
            Path.GetTempPath(),
            "WhisperHeimTests",
            "sessionmeta_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
                Directory.Delete(_testRoot, recursive: true);
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void WriteThenRead_RoundTripsAllFields()
    {
        var sessionDir = Path.Combine(_testRoot, "20260511_120000_desktop");
        Directory.CreateDirectory(sessionDir);

        var original = new SessionMetadata
        {
            SessionId = "20260511_120000_abc",
            MachineId = "desktop",
            StartedAt = new DateTimeOffset(2026, 5, 11, 12, 0, 0, TimeSpan.Zero),
            SchemaVersion = 1,
        };

        SessionMetadataStore.Write(sessionDir, original);
        var roundTripped = SessionMetadataStore.TryRead(sessionDir);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.SessionId, roundTripped!.SessionId);
        Assert.Equal(original.MachineId, roundTripped.MachineId);
        Assert.Equal(original.StartedAt, roundTripped.StartedAt);
        Assert.Equal(original.SchemaVersion, roundTripped.SchemaVersion);
    }

    [Fact]
    public void TryRead_ReturnsNull_WhenFileMissing()
    {
        var sessionDir = Path.Combine(_testRoot, "no_metadata");
        Directory.CreateDirectory(sessionDir);

        Assert.Null(SessionMetadataStore.TryRead(sessionDir));
    }

    [Fact]
    public void ResolveOriginMachineId_PrefersSessionJsonOverDirectoryName()
    {
        // Directory name says "laptop" but session.json says "desktop":
        // session.json must win, since it survives directory renames.
        var sessionDir = Path.Combine(_testRoot, "20260511_120000_laptop");
        Directory.CreateDirectory(sessionDir);
        SessionMetadataStore.Write(sessionDir, new SessionMetadata
        {
            SessionId = "20260511_120000_abc",
            MachineId = "desktop",
            StartedAt = DateTimeOffset.UtcNow,
        });

        Assert.Equal("desktop", SessionMetadataStore.ResolveOriginMachineId(sessionDir));
    }

    [Fact]
    public void ResolveOriginMachineId_FallsBackToDirectoryNameSuffix()
    {
        var sessionDir = Path.Combine(_testRoot, "20260511_120000_desktop");
        Directory.CreateDirectory(sessionDir);
        // No session.json written

        Assert.Equal("desktop", SessionMetadataStore.ResolveOriginMachineId(sessionDir));
    }

    [Fact]
    public void ResolveOriginMachineId_HandlesCollisionSuffix()
    {
        // {timestamp}_{machineId}_{n} pattern from CallRecordingService when
        // the same-second name already exists in the synced dir.
        var sessionDir = Path.Combine(_testRoot, "20260511_120000_desktop_3");
        Directory.CreateDirectory(sessionDir);

        Assert.Equal("desktop", SessionMetadataStore.ResolveOriginMachineId(sessionDir));
    }

    [Fact]
    public void ResolveOriginMachineId_ReturnsNullForLegacyUnstampedDirectory()
    {
        // Pre-task-105 directory: just {timestamp}, no machineId suffix.
        var sessionDir = Path.Combine(_testRoot, "20260511_120000");
        Directory.CreateDirectory(sessionDir);

        Assert.Null(SessionMetadataStore.ResolveOriginMachineId(sessionDir));
    }

    [Fact]
    public void ResolveOriginMachineId_HandlesRecoveredPrefix()
    {
        // Recovered orphans get a "recovered_" prefix from
        // RecordingFileStager.SweepOrphans — the suffix parser must strip it.
        var sessionDir = Path.Combine(_testRoot, "recovered_20260511_120000_desktop");
        Directory.CreateDirectory(sessionDir);

        Assert.Equal("desktop", SessionMetadataStore.ResolveOriginMachineId(sessionDir));
    }

    [Fact]
    public void TryParseMachineIdFromDirectoryName_RejectsNonTimestampPrefix()
    {
        // Random directory names that don't look like our session pattern
        // must not be falsely parsed.
        Assert.Null(SessionMetadataStore.TryParseMachineIdFromDirectoryName("not_a_session"));
        Assert.Null(SessionMetadataStore.TryParseMachineIdFromDirectoryName(""));
        Assert.Null(SessionMetadataStore.TryParseMachineIdFromDirectoryName("a_b_c"));
    }
}
