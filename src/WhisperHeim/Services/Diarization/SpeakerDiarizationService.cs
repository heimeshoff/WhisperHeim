using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using SherpaOnnx;
using WhisperHeim.Services.Models;

namespace WhisperHeim.Services.Diarization;

/// <summary>
/// Speaker diarization service using sherpa-onnx with pyannote segmentation 3.0
/// and 3D-Speaker ERes2Net embedding extraction.
///
/// Processes audio on a background thread to avoid blocking the UI.
/// Supports both single-stream (pure diarization) and dual-stream
/// (mic + loopback attribution) modes.
/// </summary>
public sealed class SpeakerDiarizationService : ISpeakerDiarizationService
{
    /// <summary>
    /// Default clustering threshold when the number of speakers is unknown.
    /// Lower values produce more speakers; higher values merge more aggressively.
    /// 0.5 is the sherpa-onnx default.
    /// </summary>
    private const float DefaultClusteringThreshold = 0.5f;

    /// <summary>
    /// Sample rate expected by the diarization pipeline (16 kHz).
    /// </summary>
    private const int ExpectedSampleRate = 16000;

    private OfflineSpeakerDiarization? _diarizer;
    private readonly object _lock = new();
    private bool _disposed;

    /// <inheritdoc />
    public bool IsLoaded => _diarizer is not null;

    /// <inheritdoc />
    public void LoadModels()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_diarizer is not null)
            return;

        var segmentationPath = ModelManagerService.PyannoteSegmentationModelPath;
        var embeddingPath = ModelManagerService.SpeakerEmbeddingModelPath;

        ValidateModelFile(segmentationPath, "pyannote segmentation");
        ValidateModelFile(embeddingPath, "speaker embedding");

        int numThreads = Math.Min(Environment.ProcessorCount, 4);

        var config = new OfflineSpeakerDiarizationConfig();
        config.Segmentation.Pyannote.Model = segmentationPath;
        config.Segmentation.NumThreads = numThreads;
        config.Segmentation.Debug = 0;
        config.Segmentation.Provider = "cpu";

        config.Embedding.Model = embeddingPath;
        config.Embedding.NumThreads = numThreads;
        config.Embedding.Debug = 0;
        config.Embedding.Provider = "cpu";

        config.Clustering.NumClusters = -1; // Auto-detect by default
        config.Clustering.Threshold = DefaultClusteringThreshold;

        config.MinDurationOn = 0.3f;  // Minimum speech duration (seconds)
        config.MinDurationOff = 0.5f; // Minimum silence duration (seconds)

        try
        {
            _diarizer = new OfflineSpeakerDiarization(config);

            int sampleRate = _diarizer.SampleRate;
            if (sampleRate != ExpectedSampleRate)
            {
                Trace.TraceWarning(
                    "[SpeakerDiarizationService] Unexpected sample rate from diarizer: {0} (expected {1})",
                    sampleRate, ExpectedSampleRate);
            }

            Trace.TraceInformation(
                "[SpeakerDiarizationService] Models loaded (segmentation={0}, embedding={1}, threads={2})",
                Path.GetFileName(segmentationPath),
                Path.GetFileName(embeddingPath),
                numThreads);
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[SpeakerDiarizationService] Failed to load models: {0}", ex.Message);
            throw new InvalidOperationException(
                $"Failed to initialize speaker diarization: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<DiarizationResult> DiarizeAsync(
        float[] samples,
        int numSpeakers = -1,
        IProgress<DiarizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_diarizer is null)
            throw new InvalidOperationException(
                "Models are not loaded. Call LoadModels() before diarizing.");

        if (samples.Length == 0)
        {
            return new DiarizationResult(
                Array.Empty<DiarizationSegment>(),
                SpeakerCount: 0,
                AudioDuration: TimeSpan.Zero,
                ProcessingDuration: TimeSpan.Zero);
        }

        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / ExpectedSampleRate);

        var result = await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ProcessDiarization(samples, numSpeakers, progress, cancellationToken);
        }, cancellationToken);

        Trace.TraceInformation(
            "[SpeakerDiarizationService] Diarized {0:F1}s audio in {1:F0}ms — {2} speakers, {3} segments",
            audioDuration.TotalSeconds,
            result.ProcessingDuration.TotalMilliseconds,
            result.SpeakerCount,
            result.Segments.Count);

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttributedDiarizationSegment>> DiarizeDualStreamAsync(
        float[] micSamples,
        float[] loopbackSamples,
        IProgress<DiarizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_diarizer is null)
            throw new InvalidOperationException(
                "Models are not loaded. Call LoadModels() before diarizing.");

        // Step 1: Diarize each stream sequentially.
        // They share a native lock anyway, and sequential execution avoids
        // unobserved task exceptions if one fails.
        // Mic stream should have primarily one speaker (the local user).
        // Loopback stream has remote speakers.
        var micResult = await DiarizeAsync(micSamples, numSpeakers: 1, cancellationToken: cancellationToken);
        var loopbackResult = await DiarizeAsync(loopbackSamples, numSpeakers: -1, cancellationToken: cancellationToken);

        // Step 2: Build attributed segments
        var attributed = new List<AttributedDiarizationSegment>();

        // Mic segments are the local user
        foreach (var seg in micResult.Segments)
        {
            attributed.Add(new AttributedDiarizationSegment(
                SpeakerId: 0, // Local user is always speaker 0
                StartTime: seg.StartTime,
                EndTime: seg.EndTime,
                Source: SpeakerSource.Microphone));
        }

        // Loopback segments are remote speakers, offset speaker IDs to avoid collision
        foreach (var seg in loopbackResult.Segments)
        {
            attributed.Add(new AttributedDiarizationSegment(
                SpeakerId: seg.SpeakerId + 1, // Offset: speaker 0 is reserved for mic user
                StartTime: seg.StartTime,
                EndTime: seg.EndTime,
                Source: SpeakerSource.Loopback));
        }

        // Sort by start time
        attributed.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        Trace.TraceInformation(
            "[SpeakerDiarizationService] Dual-stream diarization: mic={0} segments, loopback={1} segments, total={2}",
            micResult.Segments.Count,
            loopbackResult.Segments.Count,
            attributed.Count);

        return attributed;
    }

    /// <summary>
    /// Core diarization logic. Runs on a background thread.
    /// Thread-safe via locking on the native diarizer handle.
    /// </summary>
    private DiarizationResult ProcessDiarization(
        float[] samples,
        int numSpeakers,
        IProgress<DiarizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var audioDuration = TimeSpan.FromSeconds((double)samples.Length / ExpectedSampleRate);

        OfflineSpeakerDiarizationSegment[] rawSegments;

        lock (_lock)
        {
            // Update clustering config if numSpeakers is specified
            if (numSpeakers > 0)
            {
                var config = new OfflineSpeakerDiarizationConfig();
                config.Segmentation.Pyannote.Model = ModelManagerService.PyannoteSegmentationModelPath;
                config.Segmentation.NumThreads = Math.Min(Environment.ProcessorCount, 4);
                config.Segmentation.Provider = "cpu";
                config.Embedding.Model = ModelManagerService.SpeakerEmbeddingModelPath;
                config.Embedding.NumThreads = Math.Min(Environment.ProcessorCount, 4);
                config.Embedding.Provider = "cpu";
                config.Clustering.NumClusters = numSpeakers;
                config.Clustering.Threshold = DefaultClusteringThreshold;
                config.MinDurationOn = 0.3f;
                config.MinDurationOff = 0.5f;
                _diarizer!.SetConfig(config);
            }

            if (progress != null)
            {
                // Use the callback variant for progress reporting
                OfflineSpeakerDiarizationProgressCallback callback =
                    (numProcessedChunks, numTotalChunks, _) =>
                    {
                        progress.Report(new DiarizationProgress
                        {
                            ProcessedChunks = numProcessedChunks,
                            TotalChunks = numTotalChunks,
                        });

                        // Return 0 to continue, non-zero to abort
                        return cancellationToken.IsCancellationRequested ? 1 : 0;
                    };

                rawSegments = _diarizer!.ProcessWithCallback(samples, callback, IntPtr.Zero);
            }
            else
            {
                rawSegments = _diarizer!.Process(samples);
            }

            // Reset to auto-detect for next call if we changed it
            if (numSpeakers > 0)
            {
                var resetConfig = new OfflineSpeakerDiarizationConfig();
                resetConfig.Segmentation.Pyannote.Model = ModelManagerService.PyannoteSegmentationModelPath;
                resetConfig.Segmentation.NumThreads = Math.Min(Environment.ProcessorCount, 4);
                resetConfig.Segmentation.Provider = "cpu";
                resetConfig.Embedding.Model = ModelManagerService.SpeakerEmbeddingModelPath;
                resetConfig.Embedding.NumThreads = Math.Min(Environment.ProcessorCount, 4);
                resetConfig.Embedding.Provider = "cpu";
                resetConfig.Clustering.NumClusters = -1;
                resetConfig.Clustering.Threshold = DefaultClusteringThreshold;
                resetConfig.MinDurationOn = 0.3f;
                resetConfig.MinDurationOff = 0.5f;
                _diarizer.SetConfig(resetConfig);
            }
        }

        sw.Stop();

        cancellationToken.ThrowIfCancellationRequested();

        // Convert sherpa-onnx segments to our domain model
        var segments = new List<DiarizationSegment>(rawSegments.Length);
        var speakerIds = new HashSet<int>();

        foreach (var raw in rawSegments)
        {
            var startTime = TimeSpan.FromSeconds(raw.Start);
            var endTime = TimeSpan.FromSeconds(raw.End);

            // Skip degenerate segments
            if (endTime <= startTime)
                continue;

            segments.Add(new DiarizationSegment(
                SpeakerId: raw.Speaker,
                StartTime: startTime,
                EndTime: endTime));

            speakerIds.Add(raw.Speaker);
        }

        return new DiarizationResult(
            Segments: segments,
            SpeakerCount: speakerIds.Count,
            AudioDuration: audioDuration,
            ProcessingDuration: sw.Elapsed);
    }

    private static void ValidateModelFile(string path, string name)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Model file not found: {name} at '{path}'. " +
                "Ensure models have been downloaded via the Model Manager.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _diarizer?.Dispose();
        }
        catch (Exception ex)
        {
            Trace.TraceError(
                "[SpeakerDiarizationService] Error disposing diarizer: {0}", ex.Message);
        }

        _diarizer = null;
    }
}
