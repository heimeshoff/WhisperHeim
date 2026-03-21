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

        // ── Stage 1: Load audio ─────────────────────────────────────────
        ReportProgress(progress, PipelineStage.LoadingAudio, 0, "Loading audio files...");

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Mic WAV: {0} (exists={1})",
            session.MicWavFilePath, File.Exists(session.MicWavFilePath));
        Trace.TraceInformation(
            "[CallTranscriptionPipeline] System WAV: {0} (exists={1})",
            session.SystemWavFilePath, File.Exists(session.SystemWavFilePath));

        var micSamples = await Task.Run(
            () => LoadWavSamples(session.MicWavFilePath), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ReportProgress(progress, PipelineStage.LoadingAudio, 50, "Loading system audio...");

        var systemSamples = await Task.Run(
            () => LoadWavSamples(session.SystemWavFilePath), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        ReportProgress(progress, PipelineStage.LoadingAudio, 100, "Audio loaded.");

        Trace.TraceInformation(
            "[CallTranscriptionPipeline] Loaded audio: mic={0:F1}s, system={1:F1}s",
            (double)micSamples.Length / ExpectedSampleRate,
            (double)systemSamples.Length / ExpectedSampleRate);

        // ── Stage 2: Diarize ────────────────────────────────────────────
        ReportProgress(progress, PipelineStage.Diarizing, 0, "Identifying speakers...");

        var diarizationProgress = new Progress<DiarizationProgress>(dp =>
        {
            ReportProgress(progress, PipelineStage.Diarizing, dp.Percent,
                $"Diarizing... {dp.Percent:F0}%");
        });

        IReadOnlyList<AttributedDiarizationSegment> diarizedSegments;

        if (micSamples.Length > 0 && systemSamples.Length > 0)
        {
            // Dual-stream diarization for best speaker attribution
            diarizedSegments = await _diarization.DiarizeDualStreamAsync(
                micSamples, systemSamples, diarizationProgress, cancellationToken);
        }
        else
        {
            // Fallback to single-stream if one stream is empty
            var samples = micSamples.Length > 0 ? micSamples : systemSamples;
            var result = await _diarization.DiarizeAsync(
                samples, numSpeakers: -1, diarizationProgress, cancellationToken);

            diarizedSegments = result.Segments
                .Select(s => new AttributedDiarizationSegment(
                    s.SpeakerId, s.StartTime, s.EndTime,
                    micSamples.Length > 0 ? SpeakerSource.Microphone : SpeakerSource.Loopback))
                .ToList();
        }

        ReportProgress(progress, PipelineStage.Diarizing, 100,
            $"Diarization complete: {diarizedSegments.Count} segments found.");

        // Log each diarization segment for diagnostics
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
        ReportProgress(progress, PipelineStage.Transcribing, 0, "Transcribing segments...");

        // Combine mic and system audio into a single mixed stream for extracting segments.
        // We use the longer stream's length as the reference.
        var combinedSamples = MixAudioStreams(micSamples, systemSamples);

        var transcriptSegments = new List<TranscriptSegment>();
        int totalSegments = diarizedSegments.Count;

        for (int i = 0; i < totalSegments; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var seg = diarizedSegments[i];
            double segPercent = totalSegments > 0
                ? (double)i / totalSegments * 100.0
                : 0;

            var speakerLabel = GetSpeakerLabel(seg);
            ReportProgress(progress, PipelineStage.Transcribing, segPercent,
                $"Transcribing segment {i + 1}/{totalSegments} ({speakerLabel})...");

            // Extract the audio segment based on which source it came from
            float[] segmentSamples = seg.Source switch
            {
                SpeakerSource.Microphone => ExtractSegment(micSamples, seg.StartTime, seg.EndTime),
                SpeakerSource.Loopback => ExtractSegment(systemSamples, seg.StartTime, seg.EndTime),
                _ => ExtractSegment(combinedSamples, seg.StartTime, seg.EndTime),
            };

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
            var audioFileName = Path.GetFileNameWithoutExtension(filePath) + ".wav";
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

            return ReadAllSamples(resampler);
        }

        return ReadAllSamples(sampleProvider);
    }

    /// <summary>
    /// Reads all samples from a sample provider into a float array.
    /// </summary>
    private static float[] ReadAllSamples(ISampleProvider provider)
    {
        var buffer = new float[ExpectedSampleRate]; // 1 second buffer
        var allSamples = new List<float>();

        int samplesRead;
        while ((samplesRead = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
                allSamples.Add(buffer[i]);
        }

        return allSamples.ToArray();
    }

    /// <summary>
    /// Extracts a time-range of samples from an audio array.
    /// </summary>
    private static float[] ExtractSegment(float[] samples, TimeSpan startTime, TimeSpan endTime)
    {
        int startSample = (int)(startTime.TotalSeconds * ExpectedSampleRate);
        int endSample = (int)(endTime.TotalSeconds * ExpectedSampleRate);

        startSample = Math.Max(0, Math.Min(startSample, samples.Length));
        endSample = Math.Max(startSample, Math.Min(endSample, samples.Length));

        int length = endSample - startSample;
        if (length <= 0)
            return [];

        var segment = new float[length];
        Array.Copy(samples, startSample, segment, 0, length);
        return segment;
    }

    /// <summary>
    /// Mixes two audio streams by averaging overlapping samples.
    /// Used as fallback when source attribution is unknown.
    /// </summary>
    private static float[] MixAudioStreams(float[] stream1, float[] stream2)
    {
        int maxLength = Math.Max(stream1.Length, stream2.Length);
        if (maxLength == 0)
            return [];

        var mixed = new float[maxLength];
        for (int i = 0; i < maxLength; i++)
        {
            float s1 = i < stream1.Length ? stream1[i] : 0f;
            float s2 = i < stream2.Length ? stream2[i] : 0f;
            mixed[i] = (s1 + s2) * 0.5f;
        }

        return mixed;
    }

    /// <summary>
    /// Generates a human-readable speaker label from an attributed diarization segment.
    /// Mic source = "You", loopback source = "Speaker N".
    /// </summary>
    private static string GetSpeakerLabel(AttributedDiarizationSegment segment)
    {
        return segment.Source switch
        {
            SpeakerSource.Microphone => "You",
            SpeakerSource.Loopback => segment.SpeakerId == 1
                ? "Other"
                : $"Speaker {segment.SpeakerId}",
            _ => $"Speaker {segment.SpeakerId}",
        };
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
