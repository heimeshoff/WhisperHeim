using System.Diagnostics;
using System.IO;

namespace WhisperHeim.Services.Ffmpeg;

/// <summary>
/// Resolved FFmpeg metadata. Returned by <see cref="FfmpegDetector.DetectAsync"/>
/// and exposed via <see cref="FfmpegDetector.CachedInfo"/>.
/// </summary>
/// <param name="ExecutablePath">Absolute path that successfully responded to <c>-version</c>.</param>
/// <param name="VersionText">Short version banner — first line of FFmpeg's stdout.</param>
public sealed record FfmpegInfo(string ExecutablePath, string VersionText);

/// <summary>
/// Detects whether FFmpeg is available on the user's machine and caches the
/// result for the lifetime of the process. WhisperHeim does not bundle FFmpeg
/// (see <c>.workflow/research/installer-and-github-distribution.md</c> §2);
/// we shell out via <see cref="Process.Start"/> to whichever build the user
/// has installed.
///
/// Detection probes:
///   1. <c>ffmpeg -version</c> resolved via PATH (covers winget / manual installs
///      that updated PATH and any subsequent shell session).
///   2. The well-known winget install location
///      <c>%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_*\ffmpeg-*\bin\ffmpeg.exe</c>
///      as a courtesy for users who installed FFmpeg but whose PATH hasn't been
///      refreshed in the current process (very common right after winget install
///      runs from inside our modal — Environment.PATH was captured at startup).
///
/// Singleton lifetime in DI; subscribe to <see cref="StateChanged"/> for UI
/// updates that should react when detection flips null→present (e.g. the
/// General page's FFmpeg status card after the user installs FFmpeg via the
/// modal).
/// </summary>
public sealed class FfmpegDetector
{
    private const int VersionTimeoutMs = 2000;

    private readonly object _lock = new();
    private FfmpegInfo? _cached;

    /// <summary>Last successful detection result for this session, or null.</summary>
    public FfmpegInfo? CachedInfo
    {
        get { lock (_lock) return _cached; }
    }

    /// <summary>True when a prior <see cref="DetectAsync"/> located FFmpeg.</summary>
    public bool IsAvailable => CachedInfo is not null;

    /// <summary>Fired when <see cref="CachedInfo"/> transitions null↔present.</summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Runs detection. PATH lookup first, then the well-known winget location.
    /// Each candidate is given a 2 s kill timeout so a wedged binary cannot
    /// hang the app. Result is cached and exposed via <see cref="CachedInfo"/>.
    /// </summary>
    public async Task<FfmpegInfo?> DetectAsync(CancellationToken cancellationToken = default)
    {
        // 1. PATH-resolved ffmpeg.
        var pathInfo = await TryProbeAsync("ffmpeg", cancellationToken);
        if (pathInfo is not null)
        {
            UpdateCache(pathInfo);
            return pathInfo;
        }

        // 2. Well-known winget install location. winget installs FFmpeg under
        // %LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_<hash>\ffmpeg-<version>\bin\ffmpeg.exe.
        // We can't predict the hash / version directory names so we glob.
        var wingetExe = TryFindWingetInstall();
        if (wingetExe is not null)
        {
            var wingetInfo = await TryProbeAsync(wingetExe, cancellationToken);
            if (wingetInfo is not null)
            {
                UpdateCache(wingetInfo);
                return wingetInfo;
            }
        }

        UpdateCache(null);
        return null;
    }

    /// <summary>
    /// Probes a single ffmpeg candidate. Captures stdout, kills after 2 s.
    /// </summary>
    private static async Task<FfmpegInfo?> TryProbeAsync(string fileName, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            using var reg = ct.Register(() =>
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            });

            var stdoutTask = process.StandardOutput.ReadToEndAsync();

            var exited = process.WaitForExit(VersionTimeoutMs);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return null;
            }

            if (process.ExitCode != 0) return null;

            var stdout = await stdoutTask;
            if (string.IsNullOrWhiteSpace(stdout)) return null;

            var firstLine = stdout.Split('\n', 2)[0].Trim();
            // Resolve to absolute path. For PATH lookup the process's MainModule
            // points at the actual binary; for the winget path it's already
            // absolute.
            string resolved;
            try
            {
                resolved = process.MainModule?.FileName ?? fileName;
            }
            catch
            {
                resolved = fileName;
            }

            return new FfmpegInfo(resolved, firstLine);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // ENOENT / file not found — typical "ffmpeg not on PATH" path.
            return null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[FfmpegDetector] Probe of '{0}' failed: {1}", fileName, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Searches the standard winget install location for an installed
    /// <c>ffmpeg.exe</c>. Returns null if none found.
    /// </summary>
    private static string? TryFindWingetInstall()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData)) return null;

            var packagesRoot = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (!Directory.Exists(packagesRoot)) return null;

            // Gyan.FFmpeg_* package dir → ffmpeg-* version dir → bin\ffmpeg.exe
            var packageDirs = Directory.GetDirectories(packagesRoot, "Gyan.FFmpeg*");
            foreach (var packageDir in packageDirs)
            {
                var versionDirs = Directory.GetDirectories(packageDir, "ffmpeg-*");
                foreach (var versionDir in versionDirs)
                {
                    var exe = Path.Combine(versionDir, "bin", "ffmpeg.exe");
                    if (File.Exists(exe))
                        return exe;
                }

                // Some winget packages drop the exe directly at the package root
                // without an inner version dir.
                var direct = Path.Combine(packageDir, "ffmpeg.exe");
                if (File.Exists(direct))
                    return direct;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("[FfmpegDetector] winget probe failed: {0}", ex.Message);
        }

        return null;
    }

    private void UpdateCache(FfmpegInfo? newInfo)
    {
        bool changed;
        lock (_lock)
        {
            var oldPresent = _cached is not null;
            var newPresent = newInfo is not null;
            changed = oldPresent != newPresent ||
                      (oldPresent && newPresent && _cached!.ExecutablePath != newInfo!.ExecutablePath);
            _cached = newInfo;
        }

        if (changed)
        {
            try { StateChanged?.Invoke(this, EventArgs.Empty); }
            catch (Exception ex)
            {
                Trace.TraceWarning("[FfmpegDetector] StateChanged handler threw: {0}", ex.Message);
            }
        }
    }
}
