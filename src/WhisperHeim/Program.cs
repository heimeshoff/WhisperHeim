using System;
using System.Diagnostics;
using Velopack;

namespace WhisperHeim;

/// <summary>
/// Custom WPF entry point that hosts <see cref="VelopackApp"/> before WPF
/// initializes. Velopack requires this pattern so its install / update /
/// uninstall / first-run hooks can intercept the process *before* any WPF
/// window is constructed (the hooks themselves run with a 15&#x202F;s timeout
/// and forbid UI -- see <c>OnFirstRun</c> below).
/// </summary>
/// <remarks>
/// Because we use this custom entry point, App.xaml is registered as a
/// <c>&lt;Page&gt;</c> (not <c>ApplicationDefinition</c>) in the csproj and
/// <c>&lt;StartupObject&gt;</c> points at this class. <c>vpk pack</c> will
/// emit a warning that <c>VelopackApp.Run()</c> is not in the entry-point
/// assembly -- that is expected with custom Main and documented by Velopack.
/// </remarks>
public static class Program
{
    /// <summary>
    /// Set by <see cref="VelopackApp.OnFirstRun"/> when this process is the
    /// very first launch after a Velopack install. <see cref="App.OnStartup"/>
    /// reads it (alongside the <c>VELOPACK_FIRSTRUN</c> environment variable
    /// Velopack also sets) and decides whether the first-run model download
    /// dialog should be surfaced. The dialog itself is *not* shown from the
    /// hook -- Velopack hooks have a 15&#x202F;s timeout and explicitly forbid
    /// UI. Task 108 owns the UI; this flag is the handoff.
    /// </summary>
    public static bool IsFirstRun { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            VelopackApp.Build()
                .OnFirstRun(_ =>
                {
                    // NO UI HERE. Hook has a 15 s timeout and Velopack
                    // explicitly forbids window construction. Just flip the
                    // flag; App.OnStartup consumes it once WPF is alive.
                    IsFirstRun = true;
                })
                .Run();
        }
        catch (Exception ex)
        {
            // VelopackApp.Run() handles its own --veloapp-* CLI verbs and
            // exits the process when invoked by the installer/updater. A
            // failure here in the regular launch path is non-fatal: log it
            // and continue into WPF so the user still gets the app.
            Trace.TraceWarning("[Program] VelopackApp.Run() failed: {0}", ex);
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
