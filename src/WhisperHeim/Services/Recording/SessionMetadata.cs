using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhisperHeim.Services.Recording;

/// <summary>
/// Per-session metadata persisted as <c>session.json</c> inside each recording
/// directory. Identifies the origin machine of a recording so multi-machine
/// deployments sharing a cloud-synced <c>DataPath</c> can coordinate
/// transcription ownership: the recording machine is the one that auto-transcribes.
/// Other machines see the recording appear via sync and skip it in the
/// pending-sessions UI unless the user explicitly takes over.
/// </summary>
public sealed class SessionMetadata
{
    /// <summary>
    /// Unique session id (typically <c>{yyyyMMdd_HHmmss}_{guid}</c>), used for
    /// staging directory naming and as a stable handle even if the synced
    /// session directory is renamed later.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = "";

    /// <summary>
    /// Origin machine identifier — the machine that produced the recording.
    /// Compared against <see cref="Settings.DataPathService.MachineId"/> to
    /// decide whether this machine should auto-transcribe.
    /// </summary>
    [JsonPropertyName("machineId")]
    public string MachineId { get; set; } = "";

    /// <summary>UTC timestamp when the recording started.</summary>
    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Schema version for future migrations.</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;
}

/// <summary>
/// Static helpers for reading and writing <see cref="SessionMetadata"/>
/// and for resolving the origin machine of a recording directory.
/// </summary>
public static class SessionMetadataStore
{
    /// <summary>Standard filename for per-session metadata.</summary>
    public const string FileName = "session.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Writes <paramref name="metadata"/> as <c>session.json</c> into
    /// <paramref name="sessionDir"/>. Best-effort: errors are logged but
    /// never thrown, since the directory-name suffix is a sufficient fallback
    /// for the gating logic.
    /// </summary>
    public static void Write(string sessionDir, SessionMetadata metadata)
    {
        try
        {
            Directory.CreateDirectory(sessionDir);
            var path = Path.Combine(sessionDir, FileName);
            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[SessionMetadataStore] Failed to write session.json to {0}: {1}",
                sessionDir, ex.Message);
        }
    }

    /// <summary>
    /// Reads <c>session.json</c> from <paramref name="sessionDir"/>, returning
    /// <c>null</c> if it's missing or malformed.
    /// </summary>
    public static SessionMetadata? TryRead(string sessionDir)
    {
        try
        {
            var path = Path.Combine(sessionDir, FileName);
            if (!File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SessionMetadata>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[SessionMetadataStore] Failed to read session.json from {0}: {1}",
                sessionDir, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves the origin <c>machineId</c> for a recording directory using a
    /// three-tier fallback:
    /// <list type="number">
    /// <item><description>Prefer <c>session.json</c>'s <c>machineId</c> when present.</description></item>
    /// <item><description>Fall back to parsing the trailing <c>_{machineId}</c> suffix from the directory name
    /// (<c>{yyyyMMdd_HHmmss}_{machineId}</c> or <c>{yyyyMMdd_HHmmss}_{machineId}_{n}</c>).</description></item>
    /// <item><description>Return <c>null</c> for ancient un-stamped directories (created before this task
    /// shipped) — callers treat these as "owned by this machine" for backward compatibility.</description></item>
    /// </list>
    /// </summary>
    public static string? ResolveOriginMachineId(string sessionDir)
    {
        var fromJson = TryRead(sessionDir);
        if (fromJson is not null && !string.IsNullOrWhiteSpace(fromJson.MachineId))
            return fromJson.MachineId;

        var dirName = Path.GetFileName(sessionDir);
        return TryParseMachineIdFromDirectoryName(dirName);
    }

    /// <summary>
    /// Parses the trailing <c>_{machineId}</c> token from a session directory name
    /// formatted as <c>{yyyyMMdd_HHmmss}_{machineId}</c> or
    /// <c>{yyyyMMdd_HHmmss}_{machineId}_{n}</c>. Returns <c>null</c> if the name
    /// doesn't carry a recognizable machineId suffix (i.e. legacy / un-stamped).
    /// </summary>
    public static string? TryParseMachineIdFromDirectoryName(string dirName)
    {
        if (string.IsNullOrWhiteSpace(dirName))
            return null;

        // Recovered orphans are prefixed by RecordingFileStager.SweepOrphans;
        // strip that before parsing so the suffix logic still finds the machine id.
        if (dirName.StartsWith(RecordingFileStager.RecoveredPrefix, StringComparison.Ordinal))
            dirName = dirName.Substring(RecordingFileStager.RecoveredPrefix.Length);

        var parts = dirName.Split('_');
        // Minimum shape we know is timestamped: {yyyyMMdd}_{HHmmss}_{machineId}
        if (parts.Length < 3)
            return null;

        if (!IsTimestampPair(parts[0], parts[1]))
            return null;

        // parts[2..] is "{machineId}[_{n}]"; the trailing numeric suffix (collision counter)
        // is short and integer-only — strip it so we don't return "desktop_1".
        var candidate = parts[2];
        if (parts.Length >= 4 && !int.TryParse(parts[^1], out _))
        {
            // The machineId itself contained an underscore (we sanitise to [A-Za-z0-9-]
            // so this shouldn't happen with the current generator, but stay defensive).
            candidate = string.Join('_', parts[2..]);
        }

        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    private static bool IsTimestampPair(string datePart, string timePart)
    {
        if (datePart.Length != 8 || timePart.Length != 6)
            return false;
        for (int i = 0; i < datePart.Length; i++)
            if (!char.IsDigit(datePart[i])) return false;
        for (int i = 0; i < timePart.Length; i++)
            if (!char.IsDigit(timePart[i])) return false;
        return true;
    }
}
