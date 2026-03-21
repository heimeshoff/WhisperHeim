using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Persists call transcripts as JSON files in %APPDATA%/WhisperHeim/transcripts/.
/// Uses date-based naming: transcript_YYYYMMDD_HHmmss.json.
/// </summary>
public sealed class TranscriptStorageService : ITranscriptStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _transcriptsDirectory;

    public TranscriptStorageService()
    {
        _transcriptsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "transcripts");
    }

    /// <inheritdoc />
    public string TranscriptsDirectory => _transcriptsDirectory;

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        CallTranscript transcript,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_transcriptsDirectory);

        var fileName = $"transcript_{transcript.RecordingStartedUtc:yyyyMMdd_HHmmss}.json";
        var filePath = Path.Combine(_transcriptsDirectory, fileName);

        // Avoid overwriting: append a suffix if the file already exists
        if (File.Exists(filePath))
        {
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var suffix = 1;
            do
            {
                filePath = Path.Combine(_transcriptsDirectory, $"{baseName}_{suffix}.json");
                suffix++;
            } while (File.Exists(filePath));
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
        if (!Directory.Exists(_transcriptsDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_transcriptsDirectory, "transcript_*.json")
            .OrderByDescending(f => f)
            .ToArray();
    }
}
