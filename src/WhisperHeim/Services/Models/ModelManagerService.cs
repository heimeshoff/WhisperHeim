using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using WhisperHeim.Services.Settings;

namespace WhisperHeim.Services.Models;

/// <summary>
/// Manages downloading, verifying, and locating AI model files.
/// Models are stored locally (not synced) in the models/ subdirectory.
/// </summary>
public sealed class ModelManagerService
{
    private static string ModelsRoot =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim",
            "models");

    /// <summary>
    /// Initializes the models root path from the data path service.
    /// Models stay local (not synced) so they use the local root.
    /// </summary>
    public static void Initialize(DataPathService dataPathService)
    {
        ModelsRoot = dataPathService.ModelsPath;
    }

    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };

    /// <summary>Parakeet TDT 0.6B v3 int8 model for multilingual speech recognition via sherpa-onnx.</summary>
    public static readonly ModelDefinition ParakeetTdt06B = new(
        Name: "Parakeet TDT 0.6B v3",
        Description: "NVIDIA Parakeet TDT 0.6B v3 (int8) — 25 European languages with auto-detection",
        SubDirectory: "parakeet-tdt-0.6b",
        Files: new[]
        {
            new ModelFileDefinition(
                "encoder.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/encoder.int8.onnx",
                ExpectedSizeBytes: 652_000_000), // ~622 MB
            new ModelFileDefinition(
                "decoder.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/decoder.int8.onnx",
                ExpectedSizeBytes: 12_600_000), // ~12 MB
            new ModelFileDefinition(
                "joiner.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/joiner.int8.onnx",
                ExpectedSizeBytes: 6_400_000), // ~6.1 MB
            new ModelFileDefinition(
                "tokens.txt",
                "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/tokens.txt",
                ExpectedSizeBytes: 9_600), // ~9.4 KB
        },
        ProjectUrl: "https://huggingface.co/nvidia/parakeet-tdt-0.6b-v2");

    /// <summary>Silero VAD ONNX model for voice activity detection.</summary>
    public static readonly ModelDefinition SileroVad = new(
        Name: "Silero VAD",
        Description: "Silero Voice Activity Detection (~2 MB)",
        SubDirectory: "silero-vad",
        Files: new[]
        {
            new ModelFileDefinition(
                "silero_vad.onnx",
                "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx",
                ExpectedSizeBytes: 2_300_000), // ~2 MB
        },
        ProjectUrl: "https://github.com/snakers4/silero-vad");

    /// <summary>Pyannote segmentation 3.0 ONNX model for speaker diarization.</summary>
    public static readonly ModelDefinition PyannoteSegmentation = new(
        Name: "Pyannote Segmentation 3.0",
        Description: "Pyannote speaker segmentation 3.0 (int8) — speaker diarization (~1.5 MB)",
        SubDirectory: "pyannote-segmentation-3.0",
        Files: new[]
        {
            new ModelFileDefinition(
                "model.int8.onnx",
                "https://huggingface.co/csukuangfj/sherpa-onnx-pyannote-segmentation-3-0/resolve/main/model.int8.onnx",
                ExpectedSizeBytes: 1_540_000), // ~1.5 MB
        },
        ProjectUrl: "https://github.com/pyannote/pyannote-audio");

    /// <summary>3D-Speaker ERes2Net speaker embedding model for diarization clustering.</summary>
    public static readonly ModelDefinition SpeakerEmbedding = new(
        Name: "3D-Speaker ERes2Net Embedding",
        Description: "3D-Speaker ERes2Net base speaker embedding extractor (~38 MB)",
        SubDirectory: "speaker-embedding",
        Files: new[]
        {
            new ModelFileDefinition(
                "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx",
                "https://github.com/k2-fsa/sherpa-onnx/releases/download/speaker-recongition-models/3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx",
                ExpectedSizeBytes: 39_593_761), // ~38 MB
        },
        ProjectUrl: "https://github.com/modelscope/3D-Speaker");

    /// <summary>Kyutai Pocket TTS full-precision (FP32) ONNX model for higher quality voice cloning.</summary>
    public static readonly ModelDefinition PocketTtsFp32 = new(
        Name: "Pocket TTS (FP32)",
        Description: "Kyutai Pocket TTS FP32 — higher quality English text-to-speech with voice cloning (~475 MB)",
        SubDirectory: "pocket-tts-fp32",
        Files: new[]
        {
            new ModelFileDefinition(
                "lm_flow.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/lm_flow.onnx",
                ExpectedSizeBytes: 41_000_000), // ~39 MB
            new ModelFileDefinition(
                "lm_main.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/lm_main.onnx",
                ExpectedSizeBytes: 318_000_000), // ~303 MB
            new ModelFileDefinition(
                "encoder.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/encoder.onnx",
                ExpectedSizeBytes: 76_200_000), // ~73 MB
            new ModelFileDefinition(
                "decoder.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/decoder.onnx",
                ExpectedSizeBytes: 43_500_000), // ~42 MB
            new ModelFileDefinition(
                "text_conditioner.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/text_conditioner.onnx",
                ExpectedSizeBytes: 17_200_000), // ~16 MB
            new ModelFileDefinition(
                "vocab.json",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/vocab.json",
                ExpectedSizeBytes: 72_000), // ~70 KB
            new ModelFileDefinition(
                "token_scores.json",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/token_scores.json",
                ExpectedSizeBytes: 130_000), // ~124 KB
            new ModelFileDefinition(
                "test_wavs/bria.wav",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/test_wavs/bria.wav",
                ExpectedSizeBytes: 2_250_000), // ~2.15 MB
            new ModelFileDefinition(
                "test_wavs/loona.wav",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-2026-01-26/resolve/main/test_wavs/loona.wav",
                ExpectedSizeBytes: 53_000), // ~50 KB
        },
        ProjectUrl: "https://github.com/kyutai-labs/moshi");

    /// <summary>Kyutai Pocket TTS int8 ONNX model for text-to-speech via sherpa-onnx.</summary>
    public static readonly ModelDefinition PocketTtsInt8 = new(
        Name: "Pocket TTS (int8)",
        Description: "Kyutai Pocket TTS int8 — English text-to-speech with voice cloning (~200 MB)",
        SubDirectory: "pocket-tts-int8",
        Files: new[]
        {
            new ModelFileDefinition(
                "lm_flow.int8.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/lm_flow.int8.onnx",
                ExpectedSizeBytes: 10_440_000), // ~10 MB
            new ModelFileDefinition(
                "lm_main.int8.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/lm_main.int8.onnx",
                ExpectedSizeBytes: 80_000_000), // ~76 MB
            new ModelFileDefinition(
                "encoder.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/encoder.onnx",
                ExpectedSizeBytes: 76_200_000), // ~73 MB
            new ModelFileDefinition(
                "decoder.int8.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/decoder.int8.onnx",
                ExpectedSizeBytes: 23_800_000), // ~23 MB
            new ModelFileDefinition(
                "text_conditioner.onnx",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/text_conditioner.onnx",
                ExpectedSizeBytes: 17_200_000), // ~16 MB
            new ModelFileDefinition(
                "vocab.json",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/vocab.json",
                ExpectedSizeBytes: 72_000), // ~70 KB
            new ModelFileDefinition(
                "token_scores.json",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/token_scores.json",
                ExpectedSizeBytes: 130_000), // ~124 KB
            new ModelFileDefinition(
                "test_wavs/bria.wav",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/test_wavs/bria.wav",
                ExpectedSizeBytes: 2_250_000), // ~2.15 MB
            new ModelFileDefinition(
                "test_wavs/loona.wav",
                "https://huggingface.co/csukuangfj2/sherpa-onnx-pocket-tts-int8-2026-01-26/resolve/main/test_wavs/loona.wav",
                ExpectedSizeBytes: 53_000), // ~50 KB
        },
        ProjectUrl: "https://github.com/kyutai-labs/moshi");

    /// <summary>
    /// All known model definitions.
    /// </summary>
    public static IReadOnlyList<ModelDefinition> KnownModels { get; } =
        [ParakeetTdt06B, SileroVad, PyannoteSegmentation, SpeakerEmbedding, PocketTtsFp32, PocketTtsInt8];

    /// <summary>
    /// Returns the directory where a given model's files are stored.
    /// </summary>
    public static string GetModelDirectory(ModelDefinition model) =>
        Path.Combine(ModelsRoot, model.SubDirectory);

    /// <summary>
    /// Returns the full path to a specific model file.
    /// </summary>
    public static string GetModelFilePath(ModelDefinition model, string fileName) =>
        Path.Combine(ModelsRoot, model.SubDirectory, fileName);

    /// <summary>
    /// Convenience: returns the Silero VAD model path.
    /// </summary>
    public static string SileroVadModelPath =>
        GetModelFilePath(SileroVad, "silero_vad.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet encoder path.
    /// </summary>
    public static string ParakeetEncoderPath =>
        GetModelFilePath(ParakeetTdt06B, "encoder.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet decoder path.
    /// </summary>
    public static string ParakeetDecoderPath =>
        GetModelFilePath(ParakeetTdt06B, "decoder.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet joiner path.
    /// </summary>
    public static string ParakeetJoinerPath =>
        GetModelFilePath(ParakeetTdt06B, "joiner.int8.onnx");

    /// <summary>
    /// Convenience: returns the Parakeet tokens path.
    /// </summary>
    public static string ParakeetTokensPath =>
        GetModelFilePath(ParakeetTdt06B, "tokens.txt");

    /// <summary>
    /// Convenience: returns the pyannote segmentation model path.
    /// </summary>
    public static string PyannoteSegmentationModelPath =>
        GetModelFilePath(PyannoteSegmentation, "model.int8.onnx");

    /// <summary>
    /// Convenience: returns the speaker embedding model path.
    /// </summary>
    public static string SpeakerEmbeddingModelPath =>
        GetModelFilePath(SpeakerEmbedding, "3dspeaker_speech_eres2net_base_sv_zh-cn_3dspeaker_16k.onnx");

    /// <summary>
    /// Convenience: returns the Pocket TTS model directory.
    /// </summary>
    /// <summary>
    /// Returns the active Pocket TTS model definition: FP32 if available, otherwise int8.
    /// </summary>
    public static ModelDefinition ActivePocketTtsModel
    {
        get
        {
            var fp32Dir = GetModelDirectory(PocketTtsFp32);
            if (File.Exists(Path.Combine(fp32Dir, "lm_main.onnx")))
                return PocketTtsFp32;
            return PocketTtsInt8;
        }
    }

    public static string PocketTtsModelDirectory =>
        GetModelDirectory(ActivePocketTtsModel);

    /// <summary>
    /// Checks the status of all known models.
    /// </summary>
    public IReadOnlyList<ModelStatusInfo> CheckAllModels()
    {
        return KnownModels.Select(CheckModel).ToList();
    }

    /// <summary>
    /// Checks the status of a single model.
    /// </summary>
    public ModelStatusInfo CheckModel(ModelDefinition model)
    {
        var dir = GetModelDirectory(model);
        long downloaded = 0;
        int presentCount = 0;

        foreach (var file in model.Files)
        {
            var path = Path.Combine(dir, file.FileName);
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                downloaded += info.Length;
                presentCount++;
            }
        }

        ModelStatus status;
        if (presentCount == 0)
            status = ModelStatus.Missing;
        else if (presentCount < model.Files.Count)
            status = ModelStatus.Incomplete;
        else
            status = ModelStatus.Ready;

        return new ModelStatusInfo(model, status, dir, downloaded);
    }

    /// <summary>
    /// Returns true if all known models are ready (downloaded and verified).
    /// </summary>
    public bool AreAllModelsReady()
    {
        return KnownModels.All(m => CheckModel(m).Status == ModelStatus.Ready);
    }

    /// <summary>
    /// Returns a list of models that need downloading.
    /// </summary>
    public IReadOnlyList<ModelDefinition> GetMissingModels()
    {
        return KnownModels
            .Where(m => CheckModel(m).Status != ModelStatus.Ready)
            .ToList();
    }

    /// <summary>
    /// Downloads all files for a model, reporting progress.
    /// Skips files that already exist with the expected size.
    /// </summary>
    public async Task DownloadModelAsync(
        ModelDefinition model,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var dir = GetModelDirectory(model);
        Directory.CreateDirectory(dir);

        long totalDownloadedSoFar = 0;

        for (int i = 0; i < model.Files.Count; i++)
        {
            var file = model.Files[i];
            var filePath = Path.Combine(dir, file.FileName);

            // Ensure parent directory exists (for files in subdirectories like test_wavs/bria.wav)
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // Skip if file already exists with correct size
            if (File.Exists(filePath))
            {
                var existingSize = new FileInfo(filePath).Length;
                if (IsFileSizeAcceptable(existingSize, file.ExpectedSizeBytes))
                {
                    totalDownloadedSoFar += existingSize;
                    continue;
                }

                // File exists but wrong size -- re-download
                File.Delete(filePath);
            }

            await DownloadFileAsync(
                file, filePath, model, i, totalDownloadedSoFar, progress, cancellationToken);

            if (File.Exists(filePath))
            {
                totalDownloadedSoFar += new FileInfo(filePath).Length;
            }
        }
    }

    /// <summary>
    /// Downloads all missing models.
    /// </summary>
    public async Task DownloadAllMissingModelsAsync(
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var missing = GetMissingModels();

        foreach (var model in missing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await DownloadModelAsync(model, progress, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes all files for a model.
    /// </summary>
    public void DeleteModel(ModelDefinition model)
    {
        var dir = GetModelDirectory(model);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private async Task DownloadFileAsync(
        ModelFileDefinition file,
        string filePath,
        ModelDefinition model,
        int fileIndex,
        long previousFilesTotal,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tempPath = filePath + ".tmp";

        try
        {
            using var response = await SharedHttpClient.GetAsync(
                file.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? file.ExpectedSizeBytes;

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long bytesDownloaded = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                bytesDownloaded += bytesRead;

                progress?.Report(new ModelDownloadProgress
                {
                    ModelName = model.Name,
                    CurrentFileName = file.FileName,
                    CurrentFileDownloaded = bytesDownloaded,
                    CurrentFileTotal = totalBytes,
                    TotalDownloaded = previousFilesTotal + bytesDownloaded,
                    TotalExpected = model.TotalSizeBytes,
                    FileIndex = fileIndex,
                    FileCount = model.Files.Count,
                });
            }

            // Flush and close before moving
            await fileStream.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Clean up partial download on cancellation
            TryDeleteFile(tempPath);
            throw;
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }

        // Move temp file to final location
        if (File.Exists(filePath))
            File.Delete(filePath);

        File.Move(tempPath, filePath);
    }

    /// <summary>
    /// Validates that a file size is within 10% of the expected size.
    /// This accommodates minor version differences in model files.
    /// </summary>
    private static bool IsFileSizeAcceptable(long actualSize, long expectedSize)
    {
        if (expectedSize <= 0) return actualSize > 0;
        double ratio = (double)actualSize / expectedSize;
        return ratio is >= 0.9 and <= 1.1;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
