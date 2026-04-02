using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NAudio.Wave;
using WhisperHeim.Services.FileTranscription;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.Streams;

/// <summary>
/// Progress information for a batch of stream transcriptions.
/// </summary>
public sealed record StreamBatchProgress(
    int CurrentIndex,
    int TotalCount,
    string CurrentTitle,
    string Status);

/// <summary>
/// Orchestrates the transcription of video URLs (YouTube, Instagram).
/// Strategy: try to pull existing captions first; if unavailable, download audio
/// and run through the local Parakeet ASR pipeline.
/// </summary>
public sealed class StreamTranscriptionService
{
    private readonly ITranscriptionService _transcriptionService;
    private readonly StreamStorageService _storageService;

    public StreamTranscriptionService(
        ITranscriptionService transcriptionService,
        StreamStorageService storageService)
    {
        _transcriptionService = transcriptionService;
        _storageService = storageService;
    }

    /// <summary>
    /// Determines the platform for a given URL.
    /// </summary>
    public static StreamPlatform DetectPlatform(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return StreamPlatform.Unknown;

        var lower = url.Trim().ToLowerInvariant();

        if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
            return StreamPlatform.YouTube;

        if (lower.Contains("instagram.com"))
            return StreamPlatform.Instagram;

        return StreamPlatform.Unknown;
    }

    /// <summary>
    /// Transcribes a batch of URLs with per-link progress reporting.
    /// </summary>
    public async Task<IReadOnlyList<StreamTranscript>> TranscribeBatchAsync(
        IReadOnlyList<string> urls,
        IProgress<StreamBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<StreamTranscript>();

        for (int i = 0; i < urls.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = urls[i].Trim();
            if (string.IsNullOrWhiteSpace(url))
                continue;

            progress?.Report(new StreamBatchProgress(
                i + 1, urls.Count, url, "Starting..."));

            try
            {
                var transcript = await TranscribeSingleAsync(url, i + 1, urls.Count, progress, cancellationToken);
                results.Add(transcript);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Trace.TraceError(
                    "[StreamTranscriptionService] Failed to transcribe '{0}': {1}", url, ex.Message);

                // Create an error entry so the user sees what failed
                var errorTranscript = new StreamTranscript
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SourceUrl = url,
                    Title = $"[Error] {url}",
                    TranscriptText = $"Failed to transcribe: {ex.Message}",
                    DateTranscribedUtc = DateTimeOffset.UtcNow,
                    TranscriptionMethod = "error"
                };
                await _storageService.SaveAsync(errorTranscript, cancellationToken);
                results.Add(errorTranscript);
            }
        }

        return results;
    }

    private async Task<StreamTranscript> TranscribeSingleAsync(
        string url,
        int currentIndex,
        int totalCount,
        IProgress<StreamBatchProgress>? progress,
        CancellationToken cancellationToken)
    {
        var platform = DetectPlatform(url);

        progress?.Report(new StreamBatchProgress(
            currentIndex, totalCount, url, "Fetching metadata..."));

        // Try captions first (fast path)
        var (title, duration, captionText) = platform switch
        {
            StreamPlatform.YouTube => await TryGetYouTubeCaptionsAsync(url, cancellationToken),
            StreamPlatform.Instagram => await TryGetInstagramCaptionsAsync(url, cancellationToken),
            _ => await TryGetYouTubeCaptionsAsync(url, cancellationToken) // default to yt-dlp which supports many sites
        };

        if (!string.IsNullOrWhiteSpace(captionText))
        {
            progress?.Report(new StreamBatchProgress(
                currentIndex, totalCount, title ?? url, "Captions found!"));

            var transcript = new StreamTranscript
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceUrl = url,
                Title = title ?? url,
                TranscriptText = captionText.Trim(),
                Duration = duration,
                DateTranscribedUtc = DateTimeOffset.UtcNow,
                TranscriptionMethod = "captions"
            };

            await _storageService.SaveAsync(transcript, cancellationToken);
            return transcript;
        }

        // Fallback: download audio and transcribe with Parakeet
        progress?.Report(new StreamBatchProgress(
            currentIndex, totalCount, title ?? url, "Downloading audio..."));

        var audioPath = await DownloadAudioAsync(url, platform, cancellationToken);

        try
        {
            progress?.Report(new StreamBatchProgress(
                currentIndex, totalCount, title ?? url, "Transcribing with Parakeet..."));

            // Get metadata if we don't have title yet
            if (string.IsNullOrEmpty(title))
            {
                title = Path.GetFileNameWithoutExtension(audioPath);
            }

            var transcribedText = await TranscribeAudioFileAsync(audioPath, cancellationToken);

            // Get audio duration from the file
            if (duration == TimeSpan.Zero)
            {
                duration = GetAudioDuration(audioPath);
            }

            var transcript = new StreamTranscript
            {
                Id = Guid.NewGuid().ToString("N"),
                SourceUrl = url,
                Title = title ?? url,
                TranscriptText = transcribedText.Trim(),
                Duration = duration,
                DateTranscribedUtc = DateTimeOffset.UtcNow,
                TranscriptionMethod = "parakeet"
            };

            await _storageService.SaveAsync(transcript, cancellationToken);
            return transcript;
        }
        finally
        {
            // Clean up temporary audio file
            CleanupTempFile(audioPath);
        }
    }

    // ── YouTube: captions via yt-dlp ──────────────────────────────────

    private static async Task<(string? Title, TimeSpan Duration, string? CaptionText)>
        TryGetYouTubeCaptionsAsync(string url, CancellationToken cancellationToken)
    {
        string? title = null;
        TimeSpan duration = TimeSpan.Zero;

        try
        {
            // First get metadata (title + duration)
            var metaJson = await RunProcessAsync(
                "yt-dlp",
                $"--dump-json --no-download \"{url}\"",
                cancellationToken,
                timeoutMs: 30_000);

            if (!string.IsNullOrWhiteSpace(metaJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(metaJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("title", out var titleProp))
                        title = titleProp.GetString();

                    if (root.TryGetProperty("duration", out var durProp) &&
                        durProp.TryGetDouble(out var durationSecs))
                        duration = TimeSpan.FromSeconds(durationSecs);
                }
                catch (JsonException ex)
                {
                    Trace.TraceWarning(
                        "[StreamTranscriptionService] Failed to parse yt-dlp metadata JSON: {0}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] yt-dlp metadata fetch failed: {0}", ex.Message);
        }

        // Try to get subtitles/captions
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"whisperheim_subs_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                // Try auto-subs first (most YouTube videos have auto-generated subs)
                var subArgs = $"--write-auto-sub --write-sub --sub-lang en --sub-format vtt " +
                              $"--skip-download --convert-subs srt " +
                              $"-o \"{Path.Combine(tempDir, "%(id)s.%(ext)s")}\" \"{url}\"";

                await RunProcessAsync("yt-dlp", subArgs, cancellationToken, timeoutMs: 30_000);

                // Find the downloaded subtitle file
                var srtFiles = Directory.GetFiles(tempDir, "*.srt");
                var vttFiles = Directory.GetFiles(tempDir, "*.vtt");
                var subFile = srtFiles.FirstOrDefault() ?? vttFiles.FirstOrDefault();

                if (subFile is not null && File.Exists(subFile))
                {
                    var rawSubs = await File.ReadAllTextAsync(subFile, cancellationToken);
                    var cleanedText = CleanSubtitleText(rawSubs);

                    if (!string.IsNullOrWhiteSpace(cleanedText))
                    {
                        Trace.TraceInformation(
                            "[StreamTranscriptionService] Got captions for '{0}' ({1} chars)",
                            url, cleanedText.Length);
                        return (title, duration, cleanedText);
                    }
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] Caption extraction failed for '{0}': {1}", url, ex.Message);
        }

        return (title, duration, null);
    }

    // ── Instagram: captions via gallery-dl ────────────────────────────

    private static async Task<(string? Title, TimeSpan Duration, string? CaptionText)>
        TryGetInstagramCaptionsAsync(string url, CancellationToken cancellationToken)
    {
        string? title = null;
        string? captionText = null;

        try
        {
            // gallery-dl can extract metadata including captions
            var metaOutput = await RunProcessAsync(
                "gallery-dl",
                $"--dump-json \"{url}\"",
                cancellationToken,
                timeoutMs: 30_000);

            if (!string.IsNullOrWhiteSpace(metaOutput))
            {
                // gallery-dl outputs one JSON array per line; parse the first
                foreach (var line in metaOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line.Trim());
                        var root = doc.RootElement;

                        // Instagram posts have a "description" or "caption" field
                        if (root.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var element in root.EnumerateArray())
                            {
                                if (element.ValueKind != JsonValueKind.Object) continue;

                                if (element.TryGetProperty("description", out var desc) && desc.GetString() is string d && d.Length > 0)
                                    title = d.Length > 100 ? d[..100] + "..." : d;

                                // Instagram doesn't typically provide subtitle files,
                                // so we won't find caption text through gallery-dl
                            }
                        }
                        else if (root.ValueKind == JsonValueKind.Object)
                        {
                            if (root.TryGetProperty("description", out var desc) && desc.GetString() is string d && d.Length > 0)
                                title = d.Length > 100 ? d[..100] + "..." : d;
                        }
                    }
                    catch (JsonException) { /* skip malformed lines */ }
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] gallery-dl metadata fetch failed: {0}", ex.Message);
        }

        // Instagram reels rarely have captions/subtitles, so return null to fall through to audio
        return (title, TimeSpan.Zero, captionText);
    }

    // ── Audio download ────────────────────────────────────────────────

    private static async Task<string> DownloadAudioAsync(
        string url,
        StreamPlatform platform,
        CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"whisperheim_audio_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            if (platform == StreamPlatform.Instagram)
            {
                return await DownloadInstagramAudioAsync(url, tempDir, cancellationToken);
            }

            // Default: use yt-dlp (works for YouTube and many other sites)
            return await DownloadYouTubeAudioAsync(url, tempDir, cancellationToken);
        }
        catch
        {
            // Clean up temp dir on failure
            try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
            throw;
        }
    }

    private static async Task<string> DownloadYouTubeAudioAsync(
        string url,
        string tempDir,
        CancellationToken cancellationToken)
    {
        var outputTemplate = Path.Combine(tempDir, "audio.%(ext)s");
        var args = $"-x --audio-format wav --audio-quality 0 -o \"{outputTemplate}\" \"{url}\"";

        await RunProcessAsync("yt-dlp", args, cancellationToken, timeoutMs: 120_000);

        // Find the downloaded file
        var audioFile = Directory.GetFiles(tempDir)
            .FirstOrDefault(f => !f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        if (audioFile is null || !File.Exists(audioFile))
            throw new InvalidOperationException($"yt-dlp did not produce an audio file for '{url}'");

        return audioFile;
    }

    private static async Task<string> DownloadInstagramAudioAsync(
        string url,
        string tempDir,
        CancellationToken cancellationToken)
    {
        // Try gallery-dl first to download the video
        try
        {
            var args = $"-d \"{tempDir}\" \"{url}\"";
            await RunProcessAsync("gallery-dl", args, cancellationToken, timeoutMs: 60_000);

            // gallery-dl downloads to subdirectories; find any video/audio file
            var mediaFile = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".mp4" or ".mp3" or ".m4a" or ".wav" or ".webm" or ".ogg";
                });

            if (mediaFile is not null)
            {
                // If it's a video file, extract audio with ffmpeg
                var ext = Path.GetExtension(mediaFile).ToLowerInvariant();
                if (ext is ".mp4" or ".webm")
                {
                    var wavPath = Path.Combine(tempDir, "extracted_audio.wav");
                    await RunProcessAsync("ffmpeg",
                        $"-i \"{mediaFile}\" -vn -ar 16000 -ac 1 -f wav \"{wavPath}\"",
                        cancellationToken, timeoutMs: 60_000);

                    if (File.Exists(wavPath))
                        return wavPath;
                }

                return mediaFile;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] gallery-dl download failed, trying yt-dlp: {0}", ex.Message);
        }

        // Fallback: yt-dlp also supports Instagram
        return await DownloadYouTubeAudioAsync(url, tempDir, cancellationToken);
    }

    // ── Parakeet transcription ────────────────────────────────────────

    private async Task<string> TranscribeAudioFileAsync(
        string audioPath,
        CancellationToken cancellationToken)
    {
        if (!_transcriptionService.IsLoaded)
            _transcriptionService.LoadModel();

        // Decode to 16kHz mono float32
        var (samples, sampleRate) = await Task.Run(
            () => AudioFileDecoder.Decode(audioPath, cancellationToken), cancellationToken);

        if (samples.Length == 0)
            return "";

        // Chunk at silence boundaries for long files
        var chunks = await Task.Run(
            () => SilenceChunker.ChunkAtSilence(samples, sampleRate), cancellationToken);

        var textBuilder = new StringBuilder();

        for (int i = 0; i < chunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _transcriptionService.TranscribeAsync(
                chunks[i], sampleRate, cancellationToken);

            if (!string.IsNullOrWhiteSpace(result.Text))
            {
                if (textBuilder.Length > 0)
                    textBuilder.Append(' ');
                textBuilder.Append(result.Text);
            }
        }

        return textBuilder.ToString();
    }

    // ── Subtitle text cleaning ────────────────────────────────────────

    private static string CleanSubtitleText(string rawSubtitles)
    {
        // Remove SRT formatting (sequence numbers, timestamps, HTML tags)
        var lines = rawSubtitles.Split('\n');
        var textLines = new List<string>();
        var seenLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Skip empty lines, sequence numbers, and timestamp lines
            if (string.IsNullOrWhiteSpace(trimmed))
                continue;
            if (int.TryParse(trimmed, out _))
                continue;
            if (Regex.IsMatch(trimmed, @"^\d{2}:\d{2}:\d{2}"))
                continue;
            if (trimmed.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
                continue;
            if (trimmed.StartsWith("Kind:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (trimmed.StartsWith("Language:", StringComparison.OrdinalIgnoreCase))
                continue;

            // Strip HTML tags (e.g., <font>, <b>)
            trimmed = Regex.Replace(trimmed, @"<[^>]+>", "");
            trimmed = trimmed.Trim();

            if (!string.IsNullOrWhiteSpace(trimmed) && seenLines.Add(trimmed))
            {
                textLines.Add(trimmed);
            }
        }

        return string.Join(" ", textLines);
    }

    // ── Audio duration helper ─────────────────────────────────────────

    private static TimeSpan GetAudioDuration(string audioPath)
    {
        try
        {
            using var reader = new AudioFileReader(audioPath);
            return reader.TotalTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    // ── Temp file cleanup ─────────────────────────────────────────────

    private static void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var dir = Path.GetDirectoryName(path);
                File.Delete(path);

                // Also try to clean up the temp directory
                if (dir is not null && Directory.Exists(dir) &&
                    !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
                else if (dir is not null && Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] Failed to clean up temp file '{0}': {1}",
                path, ex.Message);
        }
    }

    // ── Process runner ────────────────────────────────────────────────

    private static async Task<string> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        int timeoutMs = 60_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {fileName}");

        using var reg = cancellationToken.Register(() =>
        {
            try { process.Kill(); } catch { /* ignore */ }
        });

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        var completed = process.WaitForExit(timeoutMs);
        cancellationToken.ThrowIfCancellationRequested();

        if (!completed)
        {
            try { process.Kill(); } catch { /* ignore */ }
            throw new TimeoutException($"{fileName} timed out after {timeoutMs}ms");
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            Trace.TraceWarning(
                "[StreamTranscriptionService] {0} exited with code {1}: {2}",
                fileName, process.ExitCode, error);
        }

        return output;
    }
}

/// <summary>
/// Supported stream platforms.
/// </summary>
public enum StreamPlatform
{
    Unknown,
    YouTube,
    Instagram
}
