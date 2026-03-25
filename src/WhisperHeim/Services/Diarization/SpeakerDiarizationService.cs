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
    /// 0.80 reduces over-segmentation compared to the sherpa-onnx default of 0.5.
    /// When NumClusters is positive, this threshold is ignored entirely.
    /// </summary>
    internal const float DefaultClusteringThreshold = 0.80f;

    /// <summary>
    /// Sample rate expected by the diarization pipeline (16 kHz).
    /// </summary>
    private const int ExpectedSampleRate = 16000;

    /// <summary>
    /// Maximum audio chunk duration for diarization (in seconds).
    /// Audio longer than this is split into overlapping chunks to avoid native OOM.
    /// 5 minutes keeps peak native memory under ~400 MB.
    /// </summary>
    /// <summary>
    /// Callers that need chunking (e.g. CallTranscriptionPipeline) should handle it
    /// themselves and pass pre-chunked audio. This threshold is a safety net for
    /// direct callers passing unexpectedly long audio.
    /// </summary>
    private const int MaxChunkSeconds = 300;

    /// <summary>
    /// Overlap between adjacent chunks (in seconds).
    /// Ensures speaker segments at chunk boundaries are not truncated.
    /// </summary>
    private const int ChunkOverlapSeconds = 10;

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

        _diarizer = CreateDiarizer();

        Trace.TraceInformation(
            "[SpeakerDiarizationService] Models loaded (segmentation={0}, embedding={1}, threads={2})",
            Path.GetFileName(ModelManagerService.PyannoteSegmentationModelPath),
            Path.GetFileName(ModelManagerService.SpeakerEmbeddingModelPath),
            Math.Min(Environment.ProcessorCount, 4));
    }

    private static OfflineSpeakerDiarizationConfig CreateConfig(int numSpeakers = -1)
    {
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

        config.Clustering.NumClusters = numSpeakers;
        config.Clustering.Threshold = DefaultClusteringThreshold;

        config.MinDurationOn = 0.3f;
        config.MinDurationOff = 0.5f;

        return config;
    }

    private static OfflineSpeakerDiarization CreateDiarizer(int numSpeakers = -1)
    {
        var config = CreateConfig(numSpeakers);

        try
        {
            var diarizer = new OfflineSpeakerDiarization(config);

            int sampleRate = diarizer.SampleRate;
            if (sampleRate != ExpectedSampleRate)
            {
                Trace.TraceWarning(
                    "[SpeakerDiarizationService] Unexpected sample rate from diarizer: {0} (expected {1})",
                    sampleRate, ExpectedSampleRate);
            }

            return diarizer;
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
        var totalSeconds = audioDuration.TotalSeconds;

        // For short audio, process directly
        if (totalSeconds <= MaxChunkSeconds)
        {
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

        // For long audio, process in overlapping chunks to avoid native OOM.
        // Each chunk gets a fresh native diarizer instance that is disposed after use,
        // ensuring native memory is fully released between chunks.
        Trace.TraceInformation(
            "[SpeakerDiarizationService] Audio is {0:F0}s — splitting into {1}s chunks with {2}s overlap",
            totalSeconds, MaxChunkSeconds, ChunkOverlapSeconds);

        var sw = Stopwatch.StartNew();
        var allSegments = new List<DiarizationSegment>();
        var speakerIds = new HashSet<int>();

        int chunkSamples = MaxChunkSeconds * ExpectedSampleRate;
        int overlapSamples = ChunkOverlapSeconds * ExpectedSampleRate;
        int stepSamples = chunkSamples - overlapSamples;

        int totalChunks = (int)Math.Ceiling((double)(samples.Length - overlapSamples) / stepSamples);
        int chunkIndex = 0;

        for (int offset = 0; offset < samples.Length; offset += stepSamples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int length = Math.Min(chunkSamples, samples.Length - offset);
            if (length < ExpectedSampleRate) // Skip chunks shorter than 1 second
                break;

            var chunk = new float[length];
            Array.Copy(samples, offset, chunk, 0, length);

            double chunkOffsetSeconds = (double)offset / ExpectedSampleRate;

            Trace.TraceInformation(
                "[SpeakerDiarizationService] Processing chunk {0}/{1} (offset={2:F1}s, length={3:F1}s)",
                chunkIndex + 1, totalChunks, chunkOffsetSeconds, (double)length / ExpectedSampleRate);

            // Report overall progress across chunks
            int ci = chunkIndex; // capture for closure
            IProgress<DiarizationProgress>? chunkProgress = progress is not null
                ? new Progress<DiarizationProgress>(dp =>
                {
                    var overallPercent = ((double)ci / totalChunks +
                        dp.Percent / 100.0 / totalChunks) * 100.0;
                    progress.Report(new DiarizationProgress
                    {
                        ProcessedChunks = (int)overallPercent,
                        TotalChunks = 100,
                    });
                })
                : null;

            var chunkResult = await Task.Run(() =>
                ProcessDiarization(chunk, numSpeakers, chunkProgress, cancellationToken),
                cancellationToken);

            // Offset segment times back to the original audio timeline.
            // For overlapping regions, only keep segments whose midpoint falls
            // within this chunk's non-overlapping zone.
            double keepFrom = offset == 0 ? 0 : chunkOffsetSeconds + ChunkOverlapSeconds / 2.0;
            double keepTo = (offset + stepSamples >= samples.Length)
                ? double.MaxValue
                : chunkOffsetSeconds + (double)stepSamples + ChunkOverlapSeconds / 2.0;

            foreach (var seg in chunkResult.Segments)
            {
                var absoluteStart = seg.StartTime + TimeSpan.FromSeconds(chunkOffsetSeconds);
                var absoluteEnd = seg.EndTime + TimeSpan.FromSeconds(chunkOffsetSeconds);
                double midpoint = (absoluteStart.TotalSeconds + absoluteEnd.TotalSeconds) / 2.0;

                if (midpoint >= keepFrom && midpoint < keepTo)
                {
                    allSegments.Add(new DiarizationSegment(
                        SpeakerId: seg.SpeakerId,
                        StartTime: absoluteStart,
                        EndTime: absoluteEnd));
                    speakerIds.Add(seg.SpeakerId);
                }
            }

            chunkIndex++;
        }

        sw.Stop();

        // Sort by start time
        allSegments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        var finalResult = new DiarizationResult(
            Segments: allSegments,
            SpeakerCount: speakerIds.Count,
            AudioDuration: audioDuration,
            ProcessingDuration: sw.Elapsed);

        Trace.TraceInformation(
            "[SpeakerDiarizationService] Chunked diarization of {0:F1}s audio in {1:F0}ms — {2} speakers, {3} segments ({4} chunks)",
            audioDuration.TotalSeconds,
            sw.Elapsed.TotalMilliseconds,
            speakerIds.Count,
            allSegments.Count,
            chunkIndex);

        return finalResult;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AttributedDiarizationSegment>> DiarizeDualStreamAsync(
        float[] micSamples,
        float[] loopbackSamples,
        int loopbackNumSpeakers = -1,
        IProgress<DiarizationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_diarizer is null)
            throw new InvalidOperationException(
                "Models are not loaded. Call LoadModels() before diarizing.");

        // Mic stream: never diarize. The mic always has a single known speaker
        // (the local user). All mic audio is attributed to speaker 0.
        var micSegments = new List<AttributedDiarizationSegment>();
        if (micSamples.Length > 0)
        {
            var micDuration = TimeSpan.FromSeconds((double)micSamples.Length / ExpectedSampleRate);
            micSegments.Add(new AttributedDiarizationSegment(
                SpeakerId: 0,
                StartTime: TimeSpan.Zero,
                EndTime: micDuration,
                Source: SpeakerSource.Microphone));
        }

        // Loopback stream: diarize with constrained speaker count when provided.
        // When NumClusters is positive, the clustering threshold is ignored entirely,
        // which is the key fix for over-segmentation.
        var loopbackResult = await DiarizeAsync(
            loopbackSamples, numSpeakers: loopbackNumSpeakers,
            cancellationToken: cancellationToken);

        // Build attributed segments
        var attributed = new List<AttributedDiarizationSegment>(micSegments);

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
            "[SpeakerDiarizationService] Dual-stream diarization: mic={0} segments (VAD-only), loopback={1} segments (numSpeakers={2}), total={3}",
            micSegments.Count,
            loopbackResult.Segments.Count,
            loopbackNumSpeakers,
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

        // Pin the samples array so the GC cannot move it while native code
        // is processing (the sherpa-onnx P/Invoke runs for seconds at a time,
        // and GC compaction on other threads can invalidate the pointer).
        var pinHandle = System.Runtime.InteropServices.GCHandle.Alloc(
            samples, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
        lock (_lock)
        {
            // Update clustering config if numSpeakers is specified
            if (numSpeakers > 0)
            {
                _diarizer!.SetConfig(CreateConfig(numSpeakers));
            }

            if (progress != null)
            {
                // Use the callback variant for progress reporting.
                // Pin the delegate so the GC cannot collect it during native execution.
                OfflineSpeakerDiarizationProgressCallback callback =
                    (numProcessedChunks, numTotalChunks, _) =>
                    {
                        progress.Report(new DiarizationProgress
                        {
                            ProcessedChunks = numProcessedChunks,
                            TotalChunks = numTotalChunks,
                        });

                        return cancellationToken.IsCancellationRequested ? 1 : 0;
                    };

                // Prevent GC from collecting the delegate during native call
                GC.KeepAlive(callback);
                rawSegments = _diarizer!.ProcessWithCallback(samples, callback, IntPtr.Zero);
            }
            else
            {
                rawSegments = _diarizer!.Process(samples);
            }

            // Reset to auto-detect for next call if we changed it
            if (numSpeakers > 0)
            {
                _diarizer.SetConfig(CreateConfig(-1));
            }
        }
        }
        finally
        {
            pinHandle.Free();
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
