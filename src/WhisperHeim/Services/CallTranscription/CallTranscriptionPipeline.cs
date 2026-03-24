using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using WhisperHeim.Services.Diarization;
using WhisperHeim.Services.Recording;
using WhisperHeim.Services.Transcription;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Orchestrates the end-to-end call transcription pipeline:
/// 1. Load audio from WAV files
/// 2. Diarize using dual-stream (mic + loopback) for speaker attribution
/// 3. Transcribe each speaker segment with Parakeet TDT
/// 4. Assemble into a structured transcript with timestamps and speaker labels
/// 5. Persist to %APPDATA%/WhisperHeim/transcripts/
///
/// Supports progress reporting and cancellation throughout.
/// </summary>
public sealed class CallTranscriptionPipeline : ICallTranscriptionPipeline
{
    private const int ExpectedSampleRate = 16000;

    /// <summary>
    /// Weight of each pipeline stage in the overall progress calculation.
    /// Loading=5%, Diarizing=30%, Transcribing=55%, Assembling=5%, Saving=5%.
    /// </summary>
    private static readonly (PipelineStage Stage, double Weight)[] StageWeights =
    [
        (PipelineStage.LoadingAudio, 0.05),
        (PipelineStage.Diarizing, 0.30),
        (PipelineStage.Transcribing, 0.55),
        (PipelineStage.Assembling, 0.05),
        (PipelineStage.Saving, 0.05),
    ];

    private readonly ISpeakerDiarizationService _diarization;
    private readonly ITranscriptionService _transcription;
    private readonly ITranscriptStorageService _storage;

    public CallTranscriptionPipeline(
        ISpeakerDiarizationService diarization,
        ITranscriptionService transcription,
        ITranscriptStorageService storage)
    {
        _diarization = diarization;
        _transcription = transcription;
        _storage = storage;
    }

    /// <inheritdoc />
    public async Task<CallTranscript> ProcessAsync(
        CallRecordingSession session,
        IReadOnlyList<string>? remoteSpeakerNames = null,
        string? localSpeakerName = null,
        IProgress<TranscriptionPipelineProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (!_diarization.IsLoaded)
            _diarization.LoadModels();

        if (!_transcription.IsLoaded)
            throw new InvalidOperationException(
                "Transcription model is not loaded. Call LoadModel() first.");

        if (session.EndTimestamp is null)
            throw new InvalidOperationException(
                "Cannot process a recording session that has not ended.");

        var guid = Guid.NewGuid().ToString("N")[..8];
        var transcriptId = $"{session.StartTimestamp:yyyyMMdd_HHmmss}_{guid}";

        // ── Stage 1 & 2: Diarize each stream by reading chunks from disk ─
        // Never load the full audio into memory. Each chunk is read from the
        // WAV file, diarized with a fresh native diarizer, then discarded.
        ReportProgress(progress, PipelineStage.LoadingAudio, 0, "Analyzing audio files...");

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Mic WAV: {0} (exists={1})",
            session.MicWavFilePath, File.Exists(session.MicWavFilePath));
        Trace.TraceInformation(
            "[CallTranscriptionPipeline] System WAV: {0} (exists={1})",
            session.SystemWavFilePath, File.Exists(session.SystemWavFilePath));

        bool micExists = File.Exists(session.MicWavFilePath);
        bool systemExists = File.Exists(session.SystemWavFilePath) &&
            !string.Equals(session.MicWavFilePath, session.SystemWavFilePath, StringComparison.OrdinalIgnoreCase);

        double micDurationSeconds = micExists ? GetWavDuration(session.MicWavFilePath) : 0;
        double systemDurationSeconds = systemExists ? GetWavDuration(session.SystemWavFilePath) : 0;

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Audio durations: mic={0:F1}s, system={1:F1}s",
            micDurationSeconds, systemDurationSeconds);

        // Resolve local speaker label
        var localSpeakerLabel = string.IsNullOrWhiteSpace(localSpeakerName) ? "You" : localSpeakerName;

        // Determine numSpeakers for loopback from remote speaker name list
        int loopbackNumSpeakers = remoteSpeakerNames is { Count: > 0 }
            ? remoteSpeakerNames.Count
            : -1; // auto-detect

        // -- Mic stream: skip diarization, single segment for local user --
        DiarizationResult? micDiarization = null;
        if (micExists && micDurationSeconds > 0.5)
        {
            ReportProgress(progress, PipelineStage.Diarizing, 0, "Processing microphone (single speaker)...");

            // The mic always has exactly one speaker (the local user).
            // Skip expensive diarization and create a single segment spanning the full audio.
            var micSegment = new DiarizationSegment(
                SpeakerId: 0,
                StartTime: TimeSpan.Zero,
                EndTime: TimeSpan.FromSeconds(micDurationSeconds));
            micDiarization = new DiarizationResult(
                [micSegment], SpeakerCount: 1,
                AudioDuration: TimeSpan.FromSeconds(micDurationSeconds),
                ProcessingDuration: TimeSpan.Zero);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Mic: skipped diarization, single segment ({0:F1}s)",
                micDurationSeconds);
        }

        // -- Diarize system stream --
        DiarizationResult? systemDiarization = null;
        if (systemExists && systemDurationSeconds > 0.5)
        {
            ReportProgress(progress, PipelineStage.Diarizing, 50, "Diarizing system audio...");

            systemDiarization = await DiarizeFromFileAsync(
                session.SystemWavFilePath, numSpeakers: loopbackNumSpeakers,
                dp =>
                {
                    ReportProgress(progress, PipelineStage.Diarizing, 50 + dp.Percent * 0.5,
                        $"Diarizing system — chunk {dp.ProcessedChunks}/{dp.TotalChunks}");
                },
                cancellationToken);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] System diarization: {0} segments",
                systemDiarization.Segments.Count);
        }

        // -- Build attributed segments --
        var diarizedSegments = new List<AttributedDiarizationSegment>();

        if (micDiarization is not null)
        {
            foreach (var seg in micDiarization.Segments)
                diarizedSegments.Add(new AttributedDiarizationSegment(
                    SpeakerId: 0, seg.StartTime, seg.EndTime, SpeakerSource.Microphone));
        }

        if (systemDiarization is not null)
        {
            foreach (var seg in systemDiarization.Segments)
                diarizedSegments.Add(new AttributedDiarizationSegment(
                    SpeakerId: seg.SpeakerId + 1, seg.StartTime, seg.EndTime, SpeakerSource.Loopback));
        }

        // Fallback: if neither stream had data, nothing to do
        if (diarizedSegments.Count == 0 && micDiarization is null && systemDiarization is null)
        {
            Trace.TraceWarning("[CallTranscriptionPipeline] No audio data to process.");
        }

        diarizedSegments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        ReportProgress(progress, PipelineStage.Diarizing, 100,
            $"Diarization complete: {diarizedSegments.Count} segments found.");

        for (int i = 0; i < diarizedSegments.Count; i++)
        {
            var ds = diarizedSegments[i];
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Diarization segment {0}: speaker={1}, source={2}, " +
                "start={3:F2}s, end={4:F2}s, duration={5:F2}s",
                i, ds.SpeakerId, ds.Source,
                ds.StartTime.TotalSeconds, ds.EndTime.TotalSeconds,
                (ds.EndTime - ds.StartTime).TotalSeconds);
        }

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Diarization complete: {0} segments",
            diarizedSegments.Count);

        // ── Stage 3: Transcribe each segment ────────────────────────────
        // Extract audio segments directly from WAV files on disk to avoid
        // holding the full audio in memory.
        ReportProgress(progress, PipelineStage.Transcribing, 0, "Transcribing segments...");

        var transcriptSegments = new List<TranscriptSegment>();
        int totalSegments = diarizedSegments.Count;

        for (int i = 0; i < totalSegments; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var seg = diarizedSegments[i];
            double segPercent = totalSegments > 0
                ? (double)i / totalSegments * 100.0
                : 0;

            var speakerLabel = GetSpeakerLabel(seg, localSpeakerLabel, remoteSpeakerNames);
            ReportProgress(progress, PipelineStage.Transcribing, segPercent,
                $"Transcribing segment {i + 1}/{totalSegments} ({speakerLabel})...");

            // Read only the segment's time range from the WAV file
            var wavPath = seg.Source == SpeakerSource.Microphone
                ? session.MicWavFilePath
                : session.SystemWavFilePath;

            float[] segmentSamples = await Task.Run(
                () => LoadWavSegment(wavPath, seg.StartTime, seg.EndTime), cancellationToken);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Segment {0}: {1} samples ({2:F2}s) from {3}",
                i, segmentSamples.Length,
                (double)segmentSamples.Length / ExpectedSampleRate,
                seg.Source);

            if (segmentSamples.Length == 0)
                continue;

            var result = await _transcription.TranscribeAsync(
                segmentSamples, ExpectedSampleRate, cancellationToken);

            var text = result.Text.Trim();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Segment {0} transcribed: \"{1}\"", i, text);

            if (string.IsNullOrWhiteSpace(text))
                continue;

            transcriptSegments.Add(new TranscriptSegment
            {
                Speaker = speakerLabel,
                StartTime = seg.StartTime,
                EndTime = seg.EndTime,
                Text = text,
                IsLocalSpeaker = seg.Source == SpeakerSource.Microphone,
            });
        }

        ReportProgress(progress, PipelineStage.Transcribing, 100,
            $"Transcription complete: {transcriptSegments.Count} segments with text.");

        // ── Stage 4: Assemble ───────────────────────────────────────────
        ReportProgress(progress, PipelineStage.Assembling, 0, "Assembling transcript...");

        // Merge consecutive segments from the same speaker
        var mergedSegments = MergeConsecutiveSpeakerSegments(transcriptSegments);

        var transcript = new CallTranscript
        {
            Id = transcriptId,
            Name = $"Call {session.StartTimestamp.LocalDateTime:yyyy-MM-dd HH:mm}",
            RecordingStartedUtc = session.StartTimestamp,
            RecordingEndedUtc = session.EndTimestamp.Value,
            Segments = mergedSegments,
            RemoteSpeakerNames = remoteSpeakerNames?.ToList() ?? new List<string>(),
        };

        ReportProgress(progress, PipelineStage.Assembling, 100, "Transcript assembled.");

        // ── Stage 5: Save ───────────────────────────────────────────────
        ReportProgress(progress, PipelineStage.Saving, 0, "Saving transcript...");

        // Save the transcript first to get the file path, then preserve audio alongside it
        var filePath = await _storage.SaveAsync(transcript, cancellationToken);

        // Preserve audio: mix mic + loopback into a single WAV for playback
        ReportProgress(progress, PipelineStage.Saving, 30, "Preserving audio file...");
        try
        {
            var audioFileName = "recording.wav";
            var audioFilePath = Path.Combine(
                Path.GetDirectoryName(filePath)!, audioFileName);

            await Task.Run(() =>
                MixAndSaveWav(session.MicWavFilePath, session.SystemWavFilePath, audioFilePath),
                cancellationToken);

            // Store the relative path in the transcript
            transcript.AudioFilePath = audioFileName;
            await _storage.UpdateAsync(transcript, cancellationToken);

            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Preserved audio to {0}", audioFilePath);
        }
        catch (Exception ex)
        {
            // Audio preservation is non-critical — log and continue
            Trace.TraceWarning(
                "[CallTranscriptionPipeline] Failed to preserve audio: {0}", ex.Message);
        }

        ReportProgress(progress, PipelineStage.Saving, 100, $"Transcript saved to {filePath}");

        sw.Stop();

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Pipeline complete in {0:F1}s — " +
            "{1} segments, saved to {2}",
            sw.Elapsed.TotalSeconds, transcript.Segments.Count, filePath);

        ReportProgress(progress, PipelineStage.Completed, 100, "Processing complete.");

        return transcript;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a WAV file and returns its samples as 16kHz mono float32.
    /// </summary>
    private static float[] LoadWavSamples(string wavFilePath)
    {
        if (!File.Exists(wavFilePath))
        {
            Trace.TraceWarning(
                "[CallTranscriptionPipeline] WAV file not found: {0}", wavFilePath);
            return [];
        }

        using var reader = new AudioFileReader(wavFilePath);

        // Estimate output sample count to pre-allocate (avoids List doubling OOM on long recordings)
        long estimatedSamples = (long)(reader.TotalTime.TotalSeconds * ExpectedSampleRate) + ExpectedSampleRate;

        // If the file is already 16kHz mono float, read directly
        // Otherwise NAudio's AudioFileReader handles conversion to float
        var sampleProvider = reader.ToSampleProvider();

        // Resample if needed
        if (reader.WaveFormat.SampleRate != ExpectedSampleRate ||
            reader.WaveFormat.Channels != 1)
        {
            // Use WdlResamplingSampleProvider for high-quality resampling
            var mono = reader.WaveFormat.Channels > 1
                ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider)
                : sampleProvider;

            var resampler = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                (ISampleProvider)mono, ExpectedSampleRate);

            return ReadAllSamples(resampler, estimatedSamples);
        }

        return ReadAllSamples(sampleProvider, estimatedSamples);
    }

    /// <summary>
    /// Reads all samples from a sample provider into a float array.
    /// Pre-allocates based on expected duration to avoid repeated List doubling.
    /// </summary>
    private static float[] ReadAllSamples(ISampleProvider provider, long estimatedSampleCount = 0)
    {
        var buffer = new float[ExpectedSampleRate]; // 1 second read buffer

        // Pre-allocate to avoid repeated array doubling for long recordings
        int capacity = estimatedSampleCount > 0
            ? (int)Math.Min(estimatedSampleCount, int.MaxValue)
            : ExpectedSampleRate * 60; // default 1 minute
        var allSamples = new List<float>(capacity);

        int samplesRead;
        while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            allSamples.AddRange(buffer.AsSpan(0, samplesRead).ToArray());
        }

        return allSamples.ToArray();
    }

    /// <summary>
    /// Runs diarization for a chunk in a separate child process.
    /// If the child crashes (native access violation), returns null instead of killing the app.
    /// </summary>
    private static async Task<DiarizationResult?> DiarizeChunkOutOfProcessAsync(
        float[] samples,
        int numSpeakers,
        CancellationToken cancellationToken)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"wh_diarize_{Guid.NewGuid():N}.raw");

        try
        {
            // Write raw float bytes to temp file
            var bytes = new byte[samples.Length * 4];
            Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
            await File.WriteAllBytesAsync(tempFile, bytes, cancellationToken);

            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Cannot determine process path.");

            var segModel = Models.ModelManagerService.PyannoteSegmentationModelPath;
            var embModel = Models.ModelManagerService.SpeakerEmbeddingModelPath;

            using var proc = new System.Diagnostics.Process();
            proc.StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--diarize-worker --samples \"{tempFile}\" " +
                            $"--segmentation \"{segModel}\" " +
                            $"--embedding \"{embModel}\" " +
                            $"--num-speakers {numSpeakers}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            proc.Start();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = proc.StandardError.ReadToEndAsync(cancellationToken);

            // Timeout: 2 minutes per chunk should be more than enough
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(2));

            try
            {
                await proc.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout — kill the child
                try { proc.Kill(entireProcessTree: true); } catch { }
                Trace.TraceWarning("[CallTranscriptionPipeline] Diarization child process timed out.");
                return null;
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (proc.ExitCode != 0)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Diarization child process exited with code {0}. stderr: {1}",
                    proc.ExitCode, stderr.Length > 500 ? stderr[..500] : stderr);
                return null;
            }

            // Parse JSON output
            var dtos = System.Text.Json.JsonSerializer.Deserialize<
                Diarization.DiarizationWorker.DiarizationSegmentDto[]>(stdout);

            if (dtos is null)
                return new DiarizationResult([], 0, TimeSpan.Zero, TimeSpan.Zero);

            var segments = new List<Diarization.DiarizationSegment>();
            var speakerIds = new HashSet<int>();

            foreach (var dto in dtos)
            {
                var start = TimeSpan.FromSeconds(dto.Start);
                var end = TimeSpan.FromSeconds(dto.End);
                if (end <= start) continue;
                segments.Add(new Diarization.DiarizationSegment(dto.Speaker, start, end));
                speakerIds.Add(dto.Speaker);
            }

            return new DiarizationResult(
                segments, speakerIds.Count,
                TimeSpan.FromSeconds((double)samples.Length / ExpectedSampleRate),
                TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Trace.TraceError(
                "[CallTranscriptionPipeline] Out-of-process diarization error: {0}", ex.Message);
            return null;
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    /// <summary>
    /// Gets the total duration of a WAV file without loading it into memory.
    /// </summary>
    private static double GetWavDuration(string wavFilePath)
    {
        using var reader = new AudioFileReader(wavFilePath);
        return reader.TotalTime.TotalSeconds;
    }

    /// <summary>
    /// Diarizes a WAV file by reading it in chunks from disk.
    /// Never holds more than one chunk (~120s) in memory at a time.
    /// Each chunk gets a fresh native diarizer to prevent memory accumulation.
    /// </summary>
    private async Task<DiarizationResult> DiarizeFromFileAsync(
        string wavFilePath,
        int numSpeakers,
        Action<DiarizationProgress> onProgress,
        CancellationToken cancellationToken)
    {
        const int chunkSeconds = 120;
        const int overlapSeconds = 10;

        double totalSeconds = GetWavDuration(wavFilePath);

        // For short files (under 2 minutes), load and diarize in one chunk
        if (totalSeconds <= 120)
        {
            var samples = await Task.Run(() => LoadWavSamples(wavFilePath), cancellationToken);
            var result = await DiarizeChunkOutOfProcessAsync(samples, numSpeakers, cancellationToken);
            return result ?? new DiarizationResult([], 0, TimeSpan.FromSeconds(totalSeconds), TimeSpan.Zero);
        }

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Diarizing {0:F0}s from file in {1}s chunks",
            totalSeconds, chunkSeconds);

        var sw = Stopwatch.StartNew();
        var allSegments = new List<DiarizationSegment>();
        var speakerIds = new HashSet<int>();

        int stepSeconds = chunkSeconds - overlapSeconds;
        int totalChunks = (int)Math.Ceiling((totalSeconds - overlapSeconds) / stepSeconds);
        int chunkIndex = 0;

        for (double offsetSec = 0; offsetSec < totalSeconds; offsetSec += stepSeconds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double lengthSec = Math.Min(chunkSeconds, totalSeconds - offsetSec);
            if (lengthSec < 1.0)
                break;

            var proc = System.Diagnostics.Process.GetCurrentProcess();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Diarizing chunk {0}/{1} (offset={2:F1}s, length={3:F1}s) " +
                "[memory: managed={4:F0}MB, working={5:F0}MB, private={6:F0}MB]",
                chunkIndex + 1, totalChunks, offsetSec, lengthSec,
                GC.GetTotalMemory(false) / 1048576.0,
                proc.WorkingSet64 / 1048576.0,
                proc.PrivateMemorySize64 / 1048576.0);

            // Read just this chunk from the WAV file
            var chunkSamples = await Task.Run(
                () => LoadWavSegment(wavFilePath, TimeSpan.FromSeconds(offsetSec),
                    TimeSpan.FromSeconds(offsetSec + lengthSec)),
                cancellationToken);

            if (chunkSamples.Length < ExpectedSampleRate)
            {
                chunkIndex++;
                continue;
            }

            // Run diarization in a child process so native crashes (access violations
            // in sherpa-onnx) don't kill the main application. If the child crashes,
            // we skip the chunk and continue with the rest of the recording.
            var chunkResult = await DiarizeChunkOutOfProcessAsync(
                chunkSamples, numSpeakers, cancellationToken);

            if (chunkResult is null)
            {
                Trace.TraceWarning(
                    "[CallTranscriptionPipeline] Diarization failed for chunk {0}/{1} " +
                    "(offset={2:F1}s) — skipping.",
                    chunkIndex + 1, totalChunks, offsetSec);
                chunkIndex++;
                continue;
            }

            // Release chunk and nudge GC to reclaim managed memory between chunks
            chunkSamples = null;
            GC.Collect(0, GCCollectionMode.Default, blocking: false);

            proc.Refresh();
            Trace.TraceInformation(
                "[CallTranscriptionPipeline] Chunk {0}/{1} done — {2} segments " +
                "[memory: managed={3:F0}MB, working={4:F0}MB, private={5:F0}MB]",
                chunkIndex + 1, totalChunks, chunkResult.Segments.Count,
                GC.GetTotalMemory(false) / 1048576.0,
                proc.WorkingSet64 / 1048576.0,
                proc.PrivateMemorySize64 / 1048576.0);

            // Report progress
            onProgress(new DiarizationProgress
            {
                ProcessedChunks = chunkIndex + 1,
                TotalChunks = totalChunks,
            });

            // Offset segments to absolute timeline, deduplicate overlap zone
            double keepFrom = offsetSec == 0 ? 0 : offsetSec + overlapSeconds / 2.0;
            double keepTo = (offsetSec + stepSeconds >= totalSeconds)
                ? double.MaxValue
                : offsetSec + stepSeconds + overlapSeconds / 2.0;

            foreach (var seg in chunkResult.Segments)
            {
                var absoluteStart = seg.StartTime + TimeSpan.FromSeconds(offsetSec);
                var absoluteEnd = seg.EndTime + TimeSpan.FromSeconds(offsetSec);
                double midpoint = (absoluteStart.TotalSeconds + absoluteEnd.TotalSeconds) / 2.0;

                if (midpoint >= keepFrom && midpoint < keepTo)
                {
                    allSegments.Add(new DiarizationSegment(
                        seg.SpeakerId, absoluteStart, absoluteEnd));
                    speakerIds.Add(seg.SpeakerId);
                }
            }

            chunkIndex++;
        }

        sw.Stop();
        allSegments.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] File diarization complete: {0:F1}s in {1:F0}ms — {2} speakers, {3} segments ({4} chunks)",
            totalSeconds, sw.Elapsed.TotalMilliseconds, speakerIds.Count, allSegments.Count, chunkIndex);

        return new DiarizationResult(
            allSegments, speakerIds.Count,
            TimeSpan.FromSeconds(totalSeconds), sw.Elapsed);
    }

    /// <summary>
    /// Loads a time range from a WAV file, resampling to 16kHz mono.
    /// Only reads the necessary portion from disk — does not load the full file.
    /// </summary>
    private static float[] LoadWavSegment(string wavFilePath, TimeSpan startTime, TimeSpan endTime)
    {
        if (!File.Exists(wavFilePath))
            return [];

        using var reader = new AudioFileReader(wavFilePath);

        // Seek to start position (AudioFileReader works in bytes for the underlying stream)
        if (startTime > TimeSpan.Zero && startTime < reader.TotalTime)
        {
            reader.CurrentTime = startTime;
        }

        var sampleProvider = reader.ToSampleProvider();

        // Resample/downmix if needed
        ISampleProvider provider;
        if (reader.WaveFormat.SampleRate != ExpectedSampleRate || reader.WaveFormat.Channels != 1)
        {
            var mono = reader.WaveFormat.Channels > 1
                ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sampleProvider)
                : sampleProvider;
            provider = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                (ISampleProvider)mono, ExpectedSampleRate);
        }
        else
        {
            provider = sampleProvider;
        }

        var duration = endTime - startTime;
        int expectedSamples = (int)(duration.TotalSeconds * ExpectedSampleRate) + ExpectedSampleRate;
        var result = new List<float>(expectedSamples);
        var buffer = new float[ExpectedSampleRate]; // 1 second read buffer
        int maxSamples = (int)(duration.TotalSeconds * ExpectedSampleRate) + 1;

        int totalRead = 0;
        int samplesRead;
        while (totalRead < maxSamples &&
               (samplesRead = provider.Read(buffer, 0, Math.Min(buffer.Length, maxSamples - totalRead))) > 0)
        {
            result.AddRange(buffer.AsSpan(0, samplesRead).ToArray());
            totalRead += samplesRead;
        }

        return result.ToArray();
    }

    /// <summary>
    /// Generates a human-readable speaker label from an attributed diarization segment.
    /// Mic source uses the configured local speaker name (default "You").
    /// Loopback source uses remote speaker names if available, otherwise "Speaker N".
    /// </summary>
    private static string GetSpeakerLabel(
        AttributedDiarizationSegment segment,
        string localSpeakerLabel,
        IReadOnlyList<string>? remoteSpeakerNames)
    {
        if (segment.Source == SpeakerSource.Microphone)
            return localSpeakerLabel;

        // Loopback speakers: SpeakerId is 1-based (0 is reserved for mic).
        // Map to remote speaker names list (0-indexed).
        int remoteIndex = segment.SpeakerId - 1;
        if (remoteSpeakerNames is { Count: > 0 } && remoteIndex >= 0 && remoteIndex < remoteSpeakerNames.Count)
        {
            var name = remoteSpeakerNames[remoteIndex];
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return segment.SpeakerId == 1
            ? "Other"
            : $"Speaker {segment.SpeakerId}";
    }

    /// <summary>
    /// Merges consecutive transcript segments from the same speaker into a single segment.
    /// This produces a cleaner transcript by combining fragments.
    /// </summary>
    private static IReadOnlyList<TranscriptSegment> MergeConsecutiveSpeakerSegments(
        List<TranscriptSegment> segments)
    {
        if (segments.Count == 0)
            return [];

        var merged = new List<TranscriptSegment>();
        var current = segments[0];

        for (int i = 1; i < segments.Count; i++)
        {
            var next = segments[i];

            // Merge if same speaker and gap is less than 2 seconds
            if (next.Speaker == current.Speaker &&
                (next.StartTime - current.EndTime).TotalSeconds < 2.0)
            {
                current = new TranscriptSegment
                {
                    Speaker = current.Speaker,
                    StartTime = current.StartTime,
                    EndTime = next.EndTime,
                    Text = current.Text + " " + next.Text,
                    IsLocalSpeaker = current.IsLocalSpeaker,
                };
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }

        merged.Add(current);
        return merged;
    }

    /// <summary>
    /// Mixes mic and system WAV files into a single stereo WAV file for playback.
    /// Left channel = mic, right channel = system audio.
    /// Falls back to copying whichever file exists if only one is available.
    /// </summary>
    private static void MixAndSaveWav(string micWavPath, string systemWavPath, string outputPath)
    {
        var micExists = File.Exists(micWavPath);
        var sysExists = File.Exists(systemWavPath);

        if (!micExists && !sysExists)
        {
            Trace.TraceWarning("[CallTranscriptionPipeline] No audio files to preserve.");
            return;
        }

        // If only one file exists, just copy it
        if (!micExists || !sysExists)
        {
            var source = micExists ? micWavPath : systemWavPath;
            File.Copy(source, outputPath, overwrite: true);
            return;
        }

        // Mix both into a mono WAV (averaged) at the original sample rate of the mic file
        using var micReader = new AudioFileReader(micWavPath);
        using var sysReader = new AudioFileReader(systemWavPath);

        var sampleRate = micReader.WaveFormat.SampleRate;
        var micProvider = micReader.ToSampleProvider();
        var sysProvider = sysReader.ToSampleProvider();

        // Resample system audio to match mic sample rate if different
        ISampleProvider sysSamples = sysProvider;
        if (sysReader.WaveFormat.SampleRate != sampleRate)
        {
            sysSamples = new NAudio.Wave.SampleProviders.WdlResamplingSampleProvider(
                sysProvider, sampleRate);
        }

        // Convert to mono if needed
        ISampleProvider micMono = micReader.WaveFormat.Channels > 1
            ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(micProvider)
            : micProvider;

        ISampleProvider sysMono = sysReader.WaveFormat.Channels > 1
            ? new NAudio.Wave.SampleProviders.StereoToMonoSampleProvider(sysSamples)
            : sysSamples;

        // Write mixed mono WAV
        var outFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        using var writer = new WaveFileWriter(outputPath, outFormat);

        var bufferSize = sampleRate; // 1 second at a time
        var micBuf = new float[bufferSize];
        var sysBuf = new float[bufferSize];

        while (true)
        {
            var micRead = micMono.Read(micBuf, 0, bufferSize);
            var sysRead = sysMono.Read(sysBuf, 0, bufferSize);
            var maxRead = Math.Max(micRead, sysRead);

            if (maxRead == 0)
                break;

            for (int i = 0; i < maxRead; i++)
            {
                float m = i < micRead ? micBuf[i] : 0f;
                float s = i < sysRead ? sysBuf[i] : 0f;
                writer.WriteSample((m + s) * 0.5f);
            }
        }
    }

    /// <summary>
    /// Reports pipeline progress with overall percentage calculation.
    /// </summary>
    private static void ReportProgress(
        IProgress<TranscriptionPipelineProgress>? progress,
        PipelineStage stage,
        double stagePercent,
        string description)
    {
        if (progress is null)
            return;

        double overallPercent = CalculateOverallPercent(stage, stagePercent);

        progress.Report(new TranscriptionPipelineProgress
        {
            Stage = stage,
            StagePercent = stagePercent,
            OverallPercent = overallPercent,
            Description = description,
        });
    }

    /// <summary>
    /// Calculates the overall pipeline progress percentage based on the current stage
    /// and the progress within that stage.
    /// </summary>
    private static double CalculateOverallPercent(PipelineStage stage, double stagePercent)
    {
        if (stage == PipelineStage.Completed)
            return 100.0;

        double cumulativeBefore = 0;
        double currentWeight = 0;

        foreach (var (s, weight) in StageWeights)
        {
            if (s == stage)
            {
                currentWeight = weight;
                break;
            }
            cumulativeBefore += weight;
        }

        return Math.Min(100.0,
            (cumulativeBefore + currentWeight * (stagePercent / 100.0)) * 100.0);
    }
}
