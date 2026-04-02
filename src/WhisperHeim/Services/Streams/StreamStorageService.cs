using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Streams;

/// <summary>
/// Persists stream transcripts as JSON files under streams/.
/// Each transcript is stored as {id}.json in the streams directory.
/// </summary>
public sealed class StreamStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly DataPathService _dataPathService;

    public StreamStorageService(DataPathService dataPathService)
    {
        _dataPathService = dataPathService;
    }

    /// <summary>Root directory for stream transcripts.</summary>
    public string StreamsDirectory => _dataPathService.StreamsPath;

    /// <summary>
    /// Saves a stream transcript to disk.
    /// </summary>
    public async Task<string> SaveAsync(
        StreamTranscript transcript,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(StreamsDirectory);

        var filePath = Path.Combine(StreamsDirectory, $"{transcript.Id}.json");
        var json = JsonSerializer.Serialize(transcript, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        transcript.FilePath = filePath;

        Trace.TraceInformation(
            "[StreamStorageService] Saved stream transcript to {0}", filePath);

        return filePath;
    }

    /// <summary>
    /// Loads a stream transcript from a JSON file.
    /// </summary>
    public async Task<StreamTranscript?> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var transcript = JsonSerializer.Deserialize<StreamTranscript>(json, JsonOptions);

        if (transcript is not null)
        {
            transcript.FilePath = filePath;
        }

        return transcript;
    }

    /// <summary>
    /// Lists all saved stream transcript JSON file paths, newest first.
    /// </summary>
    public IReadOnlyList<string> ListTranscriptFiles()
    {
        if (!Directory.Exists(StreamsDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(StreamsDirectory, "*.json")
            .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
            .ToArray();
    }

    /// <summary>
    /// Loads all saved stream transcripts, sorted by date transcribed (newest first).
    /// </summary>
    public async Task<IReadOnlyList<StreamTranscript>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var files = ListTranscriptFiles();
        var transcripts = new List<StreamTranscript>();

        foreach (var file in files)
        {
            var transcript = await LoadAsync(file, cancellationToken);
            if (transcript is not null)
                transcripts.Add(transcript);
        }

        return transcripts
            .OrderByDescending(t => t.DateTranscribedUtc)
            .ToArray();
    }

    /// <summary>
    /// Deletes a stream transcript from disk.
    /// </summary>
    public void Delete(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Trace.TraceInformation(
                "[StreamStorageService] Deleted stream transcript: {0}", filePath);
        }
    }
}
