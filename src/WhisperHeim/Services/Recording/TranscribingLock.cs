using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Optional advisory lock file written into a recording session directory
/// when one machine takes over transcription of another machine's recording
/// (the "Transcribe here" action on the Transcripts page).
/// </summary>
/// <remarks>
/// This is intentionally <strong>advisory</strong>, not a distributed lock —
/// Drive / OneDrive / Dropbox are eventually-consistent and offer no atomic
/// claim primitive. The actual correctness guarantee for the multi-machine
/// flow comes from origin-based gating in
/// <c>TranscriptStorageService.ListPendingSessions</c>; the lock is just a
/// UI hint so the origin machine (if it ever comes back online during
/// takeover) can see that someone else is already working on it. Treat
/// failures to read or write the lock as non-fatal.
/// </remarks>
public sealed class TranscribingLock
{
    /// <summary>Standard filename for the advisory lock.</summary>
    public const string FileName = "transcribing.lock";

    /// <summary>Identifier of the machine that wrote the lock.</summary>
    [JsonPropertyName("machineId")]
    public string MachineId { get; set; } = "";

    /// <summary>UTC timestamp when the lock was written.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Best-effort write of <c>transcribing.lock</c> into <paramref name="sessionDir"/>.
    /// Logs and swallows errors.
    /// </summary>
    public static void Write(string sessionDir, string machineId)
    {
        try
        {
            var lockPath = Path.Combine(sessionDir, FileName);
            var content = new TranscribingLock
            {
                MachineId = machineId,
                StartedAt = DateTimeOffset.UtcNow,
            };
            var json = JsonSerializer.Serialize(content, JsonOptions);
            File.WriteAllText(lockPath, json);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscribingLock] Failed to write lock to {0}: {1}", sessionDir, ex.Message);
        }
    }

    /// <summary>
    /// Best-effort delete of <c>transcribing.lock</c> from <paramref name="sessionDir"/>.
    /// Safe to call even if the file does not exist.
    /// </summary>
    public static void Remove(string sessionDir)
    {
        try
        {
            var lockPath = Path.Combine(sessionDir, FileName);
            if (File.Exists(lockPath))
                File.Delete(lockPath);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscribingLock] Failed to remove lock at {0}: {1}", sessionDir, ex.Message);
        }
    }

    /// <summary>
    /// Returns the lock contents if a lock file exists in <paramref name="sessionDir"/>,
    /// or <c>null</c> if no lock is present or the file is unreadable.
    /// </summary>
    public static TranscribingLock? TryRead(string sessionDir)
    {
        try
        {
            var lockPath = Path.Combine(sessionDir, FileName);
            if (!File.Exists(lockPath))
                return null;
            var json = File.ReadAllText(lockPath);
            return JsonSerializer.Deserialize<TranscribingLock>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[TranscribingLock] Failed to read lock at {0}: {1}", sessionDir, ex.Message);
            return null;
        }
    }
}
