using System.Diagnostics;

namespace WhisperHeim.Services.FileTranscription;

/// <summary>
/// Splits audio samples into chunks at silence boundaries.
/// Used to break long audio files into manageable segments for transcription.
/// </summary>
internal static class SilenceChunker
{
    /// <summary>
    /// Maximum chunk duration in seconds. Chunks longer than this will be
    /// forcibly split even if no silence boundary is found.
    /// </summary>
    private const double MaxChunkSeconds = 30.0;

    /// <summary>
    /// Minimum chunk duration in seconds. Very short chunks are merged with adjacent ones.
    /// </summary>
    private const double MinChunkSeconds = 1.0;

    /// <summary>
    /// Duration of the sliding window for RMS calculation (in seconds).
    /// </summary>
    private const double RmsWindowSeconds = 0.05; // 50ms

    /// <summary>
    /// Minimum silence duration to consider as a split point (in seconds).
    /// </summary>
    private const double MinSilenceDurationSeconds = 0.3; // 300ms

    /// <summary>
    /// RMS threshold below which audio is considered silence.
    /// </summary>
    private const float SilenceRmsThreshold = 0.01f;

    /// <summary>
    /// Splits audio samples into chunks at silence boundaries.
    /// If the audio is shorter than MaxChunkSeconds, returns a single chunk.
    /// </summary>
    /// <param name="samples">Float32 PCM samples.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <returns>List of sample arrays, one per chunk.</returns>
    public static List<float[]> ChunkAtSilence(float[] samples, int sampleRate)
    {
        double totalDuration = (double)samples.Length / sampleRate;

        // No need to chunk short audio
        if (totalDuration <= MaxChunkSeconds)
        {
            return [samples];
        }

        var silenceRegions = FindSilenceRegions(samples, sampleRate);
        var splitPoints = SelectSplitPoints(silenceRegions, samples.Length, sampleRate);

        if (splitPoints.Count == 0)
        {
            // No good silence boundaries found; force-split at MaxChunkSeconds intervals
            return ForceSplit(samples, sampleRate);
        }

        var chunks = SplitAtPoints(samples, splitPoints);

        // Merge any tiny chunks with their neighbors
        chunks = MergeSmallChunks(chunks, sampleRate);

        Trace.TraceInformation(
            "[SilenceChunker] Split {0:F1}s audio into {1} chunks",
            totalDuration, chunks.Count);

        return chunks;
    }

    /// <summary>
    /// Finds regions of silence in the audio using a sliding RMS window.
    /// </summary>
    private static List<(int Start, int End)> FindSilenceRegions(float[] samples, int sampleRate)
    {
        int windowSize = Math.Max(1, (int)(RmsWindowSeconds * sampleRate));
        int minSilenceSamples = (int)(MinSilenceDurationSeconds * sampleRate);

        var regions = new List<(int Start, int End)>();
        int silenceStart = -1;

        for (int i = 0; i <= samples.Length - windowSize; i += windowSize / 2) // 50% overlap
        {
            float rms = CalculateRms(samples, i, windowSize);

            if (rms < SilenceRmsThreshold)
            {
                if (silenceStart < 0)
                    silenceStart = i;
            }
            else
            {
                if (silenceStart >= 0)
                {
                    int silenceEnd = i;
                    if (silenceEnd - silenceStart >= minSilenceSamples)
                    {
                        regions.Add((silenceStart, silenceEnd));
                    }
                    silenceStart = -1;
                }
            }
        }

        // Handle trailing silence
        if (silenceStart >= 0 && samples.Length - silenceStart >= minSilenceSamples)
        {
            regions.Add((silenceStart, samples.Length));
        }

        return regions;
    }

    /// <summary>
    /// Selects the best split points from silence regions, targeting chunks of MaxChunkSeconds.
    /// </summary>
    private static List<int> SelectSplitPoints(
        List<(int Start, int End)> silenceRegions, int totalSamples, int sampleRate)
    {
        int maxChunkSamples = (int)(MaxChunkSeconds * sampleRate);
        var splitPoints = new List<int>();
        int lastSplit = 0;

        foreach (var (start, end) in silenceRegions)
        {
            int midpoint = (start + end) / 2;
            int chunkLength = midpoint - lastSplit;

            if (chunkLength >= maxChunkSamples)
            {
                splitPoints.Add(midpoint);
                lastSplit = midpoint;
            }
        }

        return splitPoints;
    }

    /// <summary>
    /// Force-splits audio into chunks of MaxChunkSeconds when no silence boundaries are found.
    /// </summary>
    private static List<float[]> ForceSplit(float[] samples, int sampleRate)
    {
        int chunkSize = (int)(MaxChunkSeconds * sampleRate);
        var chunks = new List<float[]>();

        for (int i = 0; i < samples.Length; i += chunkSize)
        {
            int remaining = Math.Min(chunkSize, samples.Length - i);
            float[] chunk = new float[remaining];
            Array.Copy(samples, i, chunk, 0, remaining);
            chunks.Add(chunk);
        }

        return chunks;
    }

    /// <summary>
    /// Splits samples at the given split points.
    /// </summary>
    private static List<float[]> SplitAtPoints(float[] samples, List<int> splitPoints)
    {
        var chunks = new List<float[]>();
        int lastPoint = 0;

        foreach (int point in splitPoints)
        {
            int length = point - lastPoint;
            if (length > 0)
            {
                float[] chunk = new float[length];
                Array.Copy(samples, lastPoint, chunk, 0, length);
                chunks.Add(chunk);
            }
            lastPoint = point;
        }

        // Final chunk
        int remainingLength = samples.Length - lastPoint;
        if (remainingLength > 0)
        {
            float[] finalChunk = new float[remainingLength];
            Array.Copy(samples, lastPoint, finalChunk, 0, remainingLength);
            chunks.Add(finalChunk);
        }

        return chunks;
    }

    /// <summary>
    /// Merges chunks shorter than MinChunkSeconds with adjacent chunks.
    /// </summary>
    private static List<float[]> MergeSmallChunks(List<float[]> chunks, int sampleRate)
    {
        int minChunkSamples = (int)(MinChunkSeconds * sampleRate);

        if (chunks.Count <= 1)
            return chunks;

        var result = new List<float[]> { chunks[0] };

        for (int i = 1; i < chunks.Count; i++)
        {
            if (chunks[i].Length < minChunkSamples)
            {
                // Merge with previous chunk
                float[] prev = result[^1];
                float[] merged = new float[prev.Length + chunks[i].Length];
                Array.Copy(prev, 0, merged, 0, prev.Length);
                Array.Copy(chunks[i], 0, merged, prev.Length, chunks[i].Length);
                result[^1] = merged;
            }
            else
            {
                result.Add(chunks[i]);
            }
        }

        return result;
    }

    private static float CalculateRms(float[] samples, int offset, int count)
    {
        double sumSquares = 0;
        int end = Math.Min(offset + count, samples.Length);
        int actualCount = end - offset;

        for (int i = offset; i < end; i++)
        {
            sumSquares += samples[i] * (double)samples[i];
        }

        return (float)Math.Sqrt(sumSquares / actualCount);
    }
}
