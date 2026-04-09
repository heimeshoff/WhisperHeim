using System.Diagnostics;
using System.IO;
using System.Text;

namespace WhisperHeim.Services.CallTranscription;

/// <summary>
/// Validates WAV files and attempts to repair corrupted headers.
/// Handles the common case where WASAPI recording produces a file with
/// missing or damaged fmt/data chunks (e.g. due to interrupted writes
/// or cloud sync corruption).
/// </summary>
public static class WavFileValidator
{
    /// <summary>
    /// Known recording format: 16kHz mono 32-bit IEEE float.
    /// This matches the format used by both CallRecordingService and LoopbackCaptureService.
    /// </summary>
    private const int SampleRate = 16000;
    private const short Channels = 1;
    private const short BitsPerSample = 32;
    private const short AudioFormat = 3; // IEEE float
    private const int BlockAlign = Channels * (BitsPerSample / 8); // 4
    private const int ByteRate = SampleRate * BlockAlign; // 64000

    /// <summary>
    /// Validates a WAV file by checking that it has valid RIFF, fmt, and data chunks.
    /// Returns null if valid, or an error description if invalid.
    /// </summary>
    public static string? Validate(string wavFilePath)
    {
        if (!File.Exists(wavFilePath))
            return $"File not found: {wavFilePath}";

        try
        {
            using var stream = File.OpenRead(wavFilePath);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 44)
                return "File too small to be a valid WAV (less than 44 bytes)";

            // Check RIFF header
            var riffTag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (riffTag != "RIFF")
                return $"Missing RIFF header (found '{riffTag}')";

            var riffSize = reader.ReadInt32();

            var waveTag = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (waveTag != "WAVE")
                return $"Missing WAVE identifier (found '{waveTag}')";

            // A RIFF size of 0 means the header sizes were never finalized
            // (e.g. WaveFileWriter.Dispose() didn't complete). NAudio uses
            // the declared RIFF size to limit chunk scanning, so it won't
            // find fmt/data even though they exist in the file bytes.
            if (riffSize == 0 && stream.Length > 44)
                return "WAV header sizes are zeroed (recording was not finalized)";

            // Scan for fmt and data chunks
            bool foundFmt = false;
            bool foundData = false;
            int dataSize = 0;

            while (stream.Position < stream.Length - 8)
            {
                var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                var chunkSize = reader.ReadInt32();

                if (chunkId == "fmt ")
                    foundFmt = true;
                else if (chunkId == "data")
                {
                    foundData = true;
                    dataSize = chunkSize;
                }

                if (foundFmt && foundData)
                    break;

                // Skip to next chunk (word-aligned)
                var skipBytes = chunkSize + (chunkSize % 2);
                if (stream.Position + skipBytes > stream.Length)
                    break;
                stream.Seek(skipBytes, SeekOrigin.Current);
            }

            if (!foundFmt)
                return "Invalid WAV file - No fmt chunk found";
            if (!foundData)
                return "Invalid WAV file - No data chunk found";

            // Data chunk exists but has zero size — same finalization issue
            if (foundData && dataSize == 0 && stream.Length > 44)
                return "WAV data chunk size is zero (recording was not finalized)";

            return null; // valid
        }
        catch (Exception ex)
        {
            return $"Error reading WAV file: {ex.Message}";
        }
    }

    /// <summary>
    /// Attempts to repair a WAV file by reconstructing its header.
    /// Creates a backup of the original file before repair.
    /// Returns true if repair succeeded, false otherwise.
    /// </summary>
    public static bool TryRepair(string wavFilePath, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            if (!File.Exists(wavFilePath))
            {
                errorMessage = "File not found";
                return false;
            }

            var fileBytes = File.ReadAllBytes(wavFilePath);
            if (fileBytes.Length < 12)
            {
                errorMessage = "File too small to repair";
                return false;
            }

            // Back up original before any modification
            var backupPath = wavFilePath + ".corrupted";
            if (!File.Exists(backupPath))
            {
                File.Copy(wavFilePath, backupPath);
                Trace.TraceInformation(
                    "[WavFileValidator] Backed up corrupted file to {0}", backupPath);
            }

            // Strategy 1: Try patching zeroed sizes in-place.
            // This handles the common case where WaveFileWriter.Dispose() didn't
            // finalize — the chunks are intact but RIFF size and data size are 0.
            if (TryPatchZeroedSizes(fileBytes, wavFilePath))
            {
                var verifyError = Validate(wavFilePath);
                if (verifyError == null)
                {
                    double duration = (double)(fileBytes.Length - 44) / ByteRate;
                    Trace.TraceInformation(
                        "[WavFileValidator] Repaired by patching zeroed sizes in {0} (~{1:F1}s)",
                        wavFilePath, duration);
                    return true;
                }
                // Patching didn't fully fix it — fall through to full rebuild
            }

            // Strategy 2: Full rebuild — extract raw audio data and create new file
            byte[]? rawAudioData = TryExtractAudioData(fileBytes);
            if (rawAudioData == null || rawAudioData.Length == 0)
            {
                RestoreBackup(backupPath, wavFilePath);
                errorMessage = "Could not extract audio data from file";
                return false;
            }

            // Sanity check: audio data should be a multiple of block align (4 bytes for 32-bit float mono)
            if (rawAudioData.Length % BlockAlign != 0)
            {
                // Trim to nearest block boundary
                int trimmedLength = rawAudioData.Length - (rawAudioData.Length % BlockAlign);
                if (trimmedLength == 0)
                {
                    RestoreBackup(backupPath, wavFilePath);
                    errorMessage = "Audio data too small after alignment correction";
                    return false;
                }

                Trace.TraceWarning(
                    "[WavFileValidator] Trimming {0} trailing bytes for block alignment",
                    rawAudioData.Length - trimmedLength);
                var trimmed = new byte[trimmedLength];
                Array.Copy(rawAudioData, trimmed, trimmedLength);
                rawAudioData = trimmed;
            }

            // Build a valid WAV file with proper header
            var repairedBytes = BuildWavFile(rawAudioData);
            File.WriteAllBytes(wavFilePath, repairedBytes);

            // Verify the repair
            var verifyError2 = Validate(wavFilePath);
            if (verifyError2 != null)
            {
                RestoreBackup(backupPath, wavFilePath);
                errorMessage = $"Repair verification failed: {verifyError2}";
                return false;
            }

            double durationSeconds = (double)rawAudioData.Length / ByteRate;
            Trace.TraceInformation(
                "[WavFileValidator] Successfully repaired {0} ({1:F1}s, {2:N0} bytes audio data)",
                wavFilePath, durationSeconds, rawAudioData.Length);

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Repair failed: {ex.Message}";
            Trace.TraceError("[WavFileValidator] Repair failed for {0}: {1}", wavFilePath, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Restores the original file from backup after a failed repair attempt.
    /// </summary>
    private static void RestoreBackup(string backupPath, string wavFilePath)
    {
        try
        {
            if (File.Exists(backupPath))
                File.Copy(backupPath, wavFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[WavFileValidator] Could not restore backup: {0}", ex.Message);
        }
    }

    /// <summary>
    /// Patches zeroed RIFF and data chunk sizes in-place.
    /// This is the most common corruption: WaveFileWriter.Dispose() didn't run,
    /// so the chunk structure is intact but the sizes are all zero.
    /// </summary>
    private static bool TryPatchZeroedSizes(byte[] fileBytes, string wavFilePath)
    {
        if (fileBytes.Length < 44)
            return false;

        // Verify RIFF + WAVE header is present
        if (fileBytes[0] != 'R' || fileBytes[1] != 'I' || fileBytes[2] != 'F' || fileBytes[3] != 'F')
            return false;
        if (fileBytes[8] != 'W' || fileBytes[9] != 'A' || fileBytes[10] != 'V' || fileBytes[11] != 'E')
            return false;

        bool patched = false;

        // Patch RIFF size (offset 4) if zero
        int riffSize = BitConverter.ToInt32(fileBytes, 4);
        if (riffSize == 0)
        {
            int correctRiffSize = fileBytes.Length - 8;
            var riffSizeBytes = BitConverter.GetBytes(correctRiffSize);
            Array.Copy(riffSizeBytes, 0, fileBytes, 4, 4);
            patched = true;

            Trace.TraceInformation(
                "[WavFileValidator] Patched RIFF size: 0 -> {0}", correctRiffSize);
        }

        // Find and patch data chunk size if zero
        int dataChunkOffset = FindChunk(fileBytes, "data");
        if (dataChunkOffset >= 0 && dataChunkOffset + 8 <= fileBytes.Length)
        {
            int dataSize = BitConverter.ToInt32(fileBytes, dataChunkOffset + 4);
            if (dataSize == 0)
            {
                int correctDataSize = fileBytes.Length - (dataChunkOffset + 8);
                var dataSizeBytes = BitConverter.GetBytes(correctDataSize);
                Array.Copy(dataSizeBytes, 0, fileBytes, dataChunkOffset + 4, 4);
                patched = true;

                Trace.TraceInformation(
                    "[WavFileValidator] Patched data chunk size: 0 -> {0}", correctDataSize);
            }
        }

        // Also patch fact chunk sample count if zero
        int factChunkOffset = FindChunk(fileBytes, "fact");
        if (factChunkOffset >= 0 && factChunkOffset + 12 <= fileBytes.Length)
        {
            int sampleCount = BitConverter.ToInt32(fileBytes, factChunkOffset + 8);
            if (sampleCount == 0 && dataChunkOffset >= 0)
            {
                int dataBytes = fileBytes.Length - (dataChunkOffset + 8);
                int correctSampleCount = dataBytes / BlockAlign;
                var sampleCountBytes = BitConverter.GetBytes(correctSampleCount);
                Array.Copy(sampleCountBytes, 0, fileBytes, factChunkOffset + 8, 4);

                Trace.TraceInformation(
                    "[WavFileValidator] Patched fact sample count: 0 -> {0}", correctSampleCount);
            }
        }

        if (patched)
        {
            File.WriteAllBytes(wavFilePath, fileBytes);
        }

        return patched;
    }

    /// <summary>
    /// Attempts to extract raw audio data from a possibly-corrupted WAV file.
    /// Tries multiple strategies:
    /// 1. Find the data chunk in a partially valid WAV
    /// 2. Strip existing (broken) header and treat remaining bytes as raw audio
    /// 3. Treat the entire file as raw audio data (headerless)
    /// </summary>
    private static byte[]? TryExtractAudioData(byte[] fileBytes)
    {
        // Strategy 1: Try to find a data chunk even if fmt is missing/corrupted
        var dataOffset = FindChunk(fileBytes, "data");
        if (dataOffset >= 0 && dataOffset + 8 <= fileBytes.Length)
        {
            int dataSize = BitConverter.ToInt32(fileBytes, dataOffset + 4);
            int audioStart = dataOffset + 8;

            // Use declared size if reasonable, otherwise take everything after the chunk header
            int availableBytes = fileBytes.Length - audioStart;
            int bytesToRead = (dataSize > 0 && dataSize <= availableBytes) ? dataSize : availableBytes;

            if (bytesToRead > 0)
            {
                Trace.TraceInformation(
                    "[WavFileValidator] Found data chunk at offset {0}, extracting {1:N0} bytes",
                    dataOffset, bytesToRead);
                var data = new byte[bytesToRead];
                Array.Copy(fileBytes, audioStart, data, 0, bytesToRead);
                return data;
            }
        }

        // Strategy 2: Has RIFF header but corrupted chunks — skip first 12 bytes (RIFF+WAVE)
        // and scan for raw float data patterns
        bool hasRiff = fileBytes.Length >= 4 &&
                       fileBytes[0] == 'R' && fileBytes[1] == 'I' &&
                       fileBytes[2] == 'F' && fileBytes[3] == 'F';

        if (hasRiff && fileBytes.Length > 44)
        {
            // Try skipping typical header area (44 bytes for standard WAV) and use the rest
            // This works when the header is garbled but audio data follows
            Trace.TraceInformation(
                "[WavFileValidator] RIFF header present but chunks corrupted, extracting from offset 44");
            int audioLength = fileBytes.Length - 44;
            var data = new byte[audioLength];
            Array.Copy(fileBytes, 44, data, 0, audioLength);
            return data;
        }

        // Strategy 3: No header at all — treat entire file as raw audio
        // Plausibility check: file should be reasonably large (at least 1 second of audio)
        if (fileBytes.Length >= ByteRate)
        {
            Trace.TraceInformation(
                "[WavFileValidator] No WAV header found, treating entire file as raw audio ({0:N0} bytes)",
                fileBytes.Length);
            return fileBytes;
        }

        return null;
    }

    /// <summary>
    /// Searches for a named chunk in WAV file bytes.
    /// Returns the offset of the chunk ID, or -1 if not found.
    /// </summary>
    private static int FindChunk(byte[] data, string chunkId)
    {
        var pattern = Encoding.ASCII.GetBytes(chunkId);
        for (int i = 0; i <= data.Length - 4; i++)
        {
            if (data[i] == pattern[0] && data[i + 1] == pattern[1] &&
                data[i + 2] == pattern[2] && data[i + 3] == pattern[3])
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Builds a complete WAV file from raw PCM audio data with a proper header.
    /// Uses the known recording format (16kHz mono 32-bit IEEE float).
    /// </summary>
    private static byte[] BuildWavFile(byte[] audioData)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // RIFF header
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + audioData.Length); // file size - 8
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(18); // chunk size (18 for IEEE float with cbSize)
        writer.Write(AudioFormat);     // audio format (3 = IEEE float)
        writer.Write(Channels);        // number of channels
        writer.Write(SampleRate);      // sample rate
        writer.Write(ByteRate);        // byte rate
        writer.Write(BlockAlign);      // block align
        writer.Write(BitsPerSample);   // bits per sample
        writer.Write((short)0);        // cbSize (extra format bytes)

        // data chunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(audioData.Length);
        writer.Write(audioData);

        return ms.ToArray();
    }
}
