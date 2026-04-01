using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Persists call transcripts as JSON files in per-session folders under recordings/.
/// Each session gets its own folder: recordings/YYYYMMDD_HHmmss/ containing
/// transcript.json and any associated WAV files.
/// </summary>
public sealed class TranscriptStorageService : ITranscriptStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DataPathService _dataPathService;

    public TranscriptStorageService(DataPathService dataPathService)
    {
        _dataPathService = dataPathService;
    }

    /// <inheritdoc />
    public string TranscriptsDirectory => _dataPathService.RecordingsPath;

    /// <summary>
    /// Creates a new session directory for recording and returns its path.
    /// Format: recordings/YYYYMMDD_HHmmss/
    /// </summary>
    public string CreateSessionDirectory(DateTimeOffset startTimestamp)
    {
        var sessionName = startTimestamp.LocalDateTime.ToString("yyyyMMdd_HHmmss");
        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, sessionName);

        // Avoid collision by appending a suffix
        if (Directory.Exists(sessionDir))
        {
            var suffix = 1;
            string candidate;
            do
            {
                candidate = $"{sessionDir}_{suffix}";
                suffix++;
            } while (Directory.Exists(candidate));
            sessionDir = candidate;
        }

        Directory.CreateDirectory(sessionDir);
        return sessionDir;
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        CallTranscript transcript,
        CancellationToken cancellationToken = default)
    {
        // Determine the session directory
        var sessionName = transcript.RecordingStartedUtc.LocalDateTime.ToString("yyyyMMdd_HHmmss");
        var sessionDir = Path.Combine(_dataPathService.RecordingsPath, sessionName);

        // If session dir doesn't exist yet, create it (may already exist from recording)
        Directory.CreateDirectory(sessionDir);

        var filePath = Path.Combine(sessionDir, "transcript.json");

        // Avoid overwriting: append a suffix if the file already exists in a different session
        if (File.Exists(filePath))
        {
            var suffix = 1;
            string candidateDir;
            do
            {
                candidateDir = Path.Combine(
                    _dataPathService.RecordingsPath, $"{sessionName}_{suffix}");
                suffix++;
            } while (Directory.Exists(candidateDir));

            sessionDir = candidateDir;
            Directory.CreateDirectory(sessionDir);
            filePath = Path.Combine(sessionDir, "transcript.json");
        }

        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        transcript.FilePath = filePath;

        Trace.TraceInformation(
            "[TranscriptStorageService] Saved transcript to {0} ({1} segments)",
            filePath, transcript.Segments.Count);

        return filePath;
    }

    /// <inheritdoc />
    public async Task<CallTranscript?> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var transcript = JsonSerializer.Deserialize<CallTranscript>(json, JsonOptions);

        if (transcript is not null)
        {
            transcript.FilePath = filePath;

            // Backward compatibility: generate a default name for old transcripts without one
            if (string.IsNullOrEmpty(transcript.Name))
            {
                transcript.Name = $"Call {transcript.RecordingStartedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";
            }
        }

        return transcript;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        CallTranscript transcript,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(transcript.FilePath))
            throw new InvalidOperationException("Cannot update a transcript without a file path.");

        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(transcript.FilePath, json, cancellationToken);

        Trace.TraceInformation(
            "[TranscriptStorageService] Updated transcript at {0}", transcript.FilePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListTranscriptFiles()
    {
        var recordingsDir = _dataPathService.RecordingsPath;
        if (!Directory.Exists(recordingsDir))
            return Array.Empty<string>();

        // Look for transcript.json inside each session subdirectory
        var files = new List<string>();
        foreach (var sessionDir in Directory.GetDirectories(recordingsDir))
        {
            var transcriptFile = Path.Combine(sessionDir, "transcript.json");
            if (File.Exists(transcriptFile))
            {
                files.Add(transcriptFile);
            }
            else if (!Directory.EnumerateFileSystemEntries(sessionDir).Any())
            {
                // Clean up empty session folders (e.g. left behind after deletion on synced drives)
                try
                {
                    Directory.Delete(sessionDir);
                    Trace.TraceInformation(
                        "[TranscriptStorageService] Cleaned up empty session directory: {0}", sessionDir);
                }
                catch (IOException) { }
            }
        }

        // Also support old-style flat transcript files during migration
        files.AddRange(Directory.GetFiles(recordingsDir, "transcript_*.json"));

        return files.OrderByDescending(f => f).ToArray();
    }

    /// <summary>
    /// Audio file extensions recognized as valid session content (WAV + import formats).
    /// </summary>
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".ogg", ".mp3", ".m4a"
    };

    /// <summary>
    /// Returns session directories that contain audio files but no transcript.json.
    /// Each entry is the full path to the session directory.
    /// </summary>
    public IReadOnlyList<string> ListPendingSessions()
    {
        var recordingsDir = _dataPathService.RecordingsPath;
        if (!Directory.Exists(recordingsDir))
            return Array.Empty<string>();

        var pending = new List<string>();
        foreach (var sessionDir in Directory.GetDirectories(recordingsDir))
        {
            var hasTranscript = File.Exists(Path.Combine(sessionDir, "transcript.json"));
            if (hasTranscript)
                continue;

            var hasAudio = Directory.GetFiles(sessionDir)
                .Any(f => AudioExtensions.Contains(Path.GetExtension(f)));
            if (hasAudio)
                pending.Add(sessionDir);
        }

        return pending.OrderByDescending(d => d).ToArray();
    }

    /// <summary>
    /// Deletes an entire recording session directory (transcript + WAV files).
    /// </summary>
    public void DeleteSession(string transcriptFilePath)
    {
        var sessionDir = Path.GetDirectoryName(transcriptFilePath);
        if (sessionDir is null)
            return;

        // Only delete the entire directory if it's a per-session folder
        // (i.e., it's a direct child of the recordings directory)
        var parentDir = Path.GetDirectoryName(sessionDir);
        if (parentDir is not null &&
            string.Equals(Path.GetFullPath(parentDir),
                Path.GetFullPath(_dataPathService.RecordingsPath),
                StringComparison.OrdinalIgnoreCase))
        {
            Directory.Delete(sessionDir, recursive: true);
            // On synced drives the folder may linger; remove the now-empty shell
            if (Directory.Exists(sessionDir) &&
                !Directory.EnumerateFileSystemEntries(sessionDir).Any())
            {
                Directory.Delete(sessionDir);
            }
            Trace.TraceInformation(
                "[TranscriptStorageService] Deleted session directory: {0}", sessionDir);
        }
        else
        {
            // Fallback: just delete the transcript file
            if (File.Exists(transcriptFilePath))
                File.Delete(transcriptFilePath);
        }
    }
}
