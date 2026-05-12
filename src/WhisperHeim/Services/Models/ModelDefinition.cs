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
    IReadOnlyList<ModelFileDefinition> Files,
    string? ProjectUrl = null)
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

/// <summary>
/// One row in <c>models/manifest.json</c>. Lets subsequent launches
/// fast-path past the first-run download dialog when models are already
/// present, without re-stat-ing every file.
/// </summary>
public sealed class ManifestEntry
{
    /// <summary>ISO-8601 UTC timestamp of when the model was finalized on disk.</summary>
    public string DownloadedAt { get; set; } = string.Empty;

    /// <summary>Total bytes across all files of the model.</summary>
    public long SizeBytes { get; set; }
}
