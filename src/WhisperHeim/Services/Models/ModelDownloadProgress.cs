namespace WhisperHeim.Services.Models;

/// <summary>
/// Reports progress for a model download operation.
/// </summary>
public sealed class ModelDownloadProgress
{
    /// <summary>Name of the model currently being downloaded.</summary>
    public string ModelName { get; init; } = string.Empty;

    /// <summary>Name of the file currently being downloaded.</summary>
    public string CurrentFileName { get; init; } = string.Empty;

    /// <summary>Bytes downloaded for the current file.</summary>
    public long CurrentFileDownloaded { get; init; }

    /// <summary>Total bytes for the current file.</summary>
    public long CurrentFileTotal { get; init; }

    /// <summary>Total bytes downloaded across all files in this model.</summary>
    public long TotalDownloaded { get; init; }

    /// <summary>Total bytes expected across all files in this model.</summary>
    public long TotalExpected { get; init; }

    /// <summary>Index of the current file (0-based).</summary>
    public int FileIndex { get; init; }

    /// <summary>Total number of files to download.</summary>
    public int FileCount { get; init; }

    /// <summary>Overall progress percentage (0-100).</summary>
    public double OverallPercent =>
        TotalExpected > 0 ? Math.Min(100.0, (double)TotalDownloaded / TotalExpected * 100.0) : 0.0;
}
