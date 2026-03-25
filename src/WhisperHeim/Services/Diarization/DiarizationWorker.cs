using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using SherpaOnnx;

namespace WhisperHeim.Services.Diarization;

/// <summary>
/// Headless diarization worker that runs in a child process.
/// Reads raw float samples from a temp file, runs sherpa-onnx diarization,
/// and writes JSON results to stdout. If the native code crashes, only the
/// child process dies — the parent survives and skips the chunk.
/// </summary>
internal static class DiarizationWorker
{
    /// <summary>
    /// Entry point for the --diarize-worker mode. Bypasses all WPF.
    /// Args: --samples &lt;path&gt; --segmentation &lt;path&gt; --embedding &lt;path&gt; --num-speakers &lt;n&gt;
    /// </summary>
    public static void Run(string[] args)
    {
        try
        {
            string? samplesPath = null, segPath = null, embPath = null;
            int numSpeakers = -1;
            float threshold = SpeakerDiarizationService.DefaultClusteringThreshold;

            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "--samples": samplesPath = args[++i]; break;
                    case "--segmentation": segPath = args[++i]; break;
                    case "--embedding": embPath = args[++i]; break;
                    case "--num-speakers": numSpeakers = int.Parse(args[++i]); break;
                    case "--threshold": threshold = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                }
            }

            if (samplesPath is null || segPath is null || embPath is null)
            {
                Console.Error.WriteLine("Missing required arguments.");
                Environment.Exit(2);
                return;
            }

            // Read raw float samples from temp file
            var bytes = File.ReadAllBytes(samplesPath);
            var samples = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);

            // Create diarizer
            var config = new OfflineSpeakerDiarizationConfig();
            config.Segmentation.Pyannote.Model = segPath;
            config.Segmentation.NumThreads = Math.Min(Environment.ProcessorCount, 4);
            config.Segmentation.Provider = "cpu";
            config.Embedding.Model = embPath;
            config.Embedding.NumThreads = Math.Min(Environment.ProcessorCount, 4);
            config.Embedding.Provider = "cpu";
            config.Clustering.NumClusters = numSpeakers;
            config.Clustering.Threshold = threshold;
            config.MinDurationOn = 0.3f;
            config.MinDurationOff = 0.5f;

            using var diarizer = new OfflineSpeakerDiarization(config);
            var rawSegments = diarizer.Process(samples);

            // Write JSON to stdout
            var output = rawSegments.Select(s => new DiarizationSegmentDto
            {
                Speaker = s.Speaker,
                Start = s.Start,
                End = s.End,
            }).ToArray();

            Console.Write(JsonSerializer.Serialize(output));
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            Environment.Exit(1);
        }
    }

    public sealed class DiarizationSegmentDto
    {
        public int Speaker { get; set; }
        public float Start { get; set; }
        public float End { get; set; }
    }
}
