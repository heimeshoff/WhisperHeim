namespace WhisperHeim.Services.Models;

/// <summary>
/// Describes a single downloadable model file.
/// </summary>
public sealed record ModelFileDefinition(
    string FileName,
    string DownloadUrl,
    long ExpectedSizeBytes,
    string? Sha256Hash = null);

/// <summary>
/// Describes a model consisting of one or more files.
/// </summary>
public sealed record ModelDefinition(
    string Name,
    string Description,
    string SubDirectory,
    IReadOnlyList<ModelFileDefinition> Files)
{
    /// <summary>Total expected size across all files.</summary>
    public long TotalSizeBytes => Files.Sum(f => f.ExpectedSizeBytes);
}

/// <summary>
/// Status of a model on disk.
/// </summary>
public enum ModelStatus
{
    /// <summary>Model files are missing.</summary>
    Missing,

    /// <summary>Model files are partially downloaded.</summary>
    Incomplete,

    /// <summary>All model files are present and verified.</summary>
    Ready
}

/// <summary>
/// Snapshot of a model's status.
/// </summary>
public sealed record ModelStatusInfo(
    ModelDefinition Definition,
    ModelStatus Status,
    string ModelDirectory,
    long DownloadedBytes);
