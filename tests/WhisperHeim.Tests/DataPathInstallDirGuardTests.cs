using System;
using System.IO;
using WhisperHeim.Services.Settings;
using Xunit;

namespace WhisperHeim.Tests;

/// <summary>
/// Verifies the guard that prevents the user (or future programmatic
/// callers) from pointing the configurable <c>DataPath</c> at a directory
/// that Velopack wipes on uninstall or replaces on update. Task 113.
/// </summary>
public class DataPathInstallDirGuardTests
{
    [Fact]
    public void NullOrWhitespace_IsNotForbidden()
    {
        Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot(null));
        Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot(""));
        Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot("   "));
    }

    [Fact]
    public void RoamingAppDataPath_IsAllowed()
    {
        // %APPDATA%\WhisperHeim is the *expected* default and must NOT
        // be flagged -- Velopack preserves it on uninstall.
        var roaming = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WhisperHeim");
        Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot(roaming));
    }

    [Fact]
    public void DocumentsPath_IsAllowed()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrWhiteSpace(docs))
            Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot(docs));
    }

    [Fact]
    public void LocalAppDataRoot_IsForbidden()
    {
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhisperHeim");
        Assert.True(DataPathService.IsInsideInstallOrLocalAppDataRoot(localRoot));
    }

    [Fact]
    public void LocalAppDataSubdirectory_IsForbidden()
    {
        var sub = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhisperHeim",
            "current");
        Assert.True(DataPathService.IsInsideInstallOrLocalAppDataRoot(sub));
    }

    [Fact]
    public void SimilarlyNamedSiblingDirectory_IsAllowed()
    {
        // "%LocalAppData%\WhisperHeimNotes" is NOT inside the install root
        // -- prefix matching must respect directory boundaries.
        var sibling = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WhisperHeimNotes");
        Assert.False(DataPathService.IsInsideInstallOrLocalAppDataRoot(sibling));
    }

    [Fact]
    public void InstallBaseDirectory_IsForbidden()
    {
        // AppContext.BaseDirectory is where WhisperHeim.exe lives at
        // runtime; pointing DataPath there would be wiped on update.
        Assert.True(DataPathService.IsInsideInstallOrLocalAppDataRoot(AppContext.BaseDirectory));
    }

    [Fact]
    public void MalformedPath_DoesNotThrow()
    {
        // The guard must never throw -- malformed input should bubble up
        // as a normal "this is not a forbidden path" result and let
        // ValidatePath surface the real "invalid path" error. We accept
        // either true or false here; the only thing we test is "no
        // exception", because Windows accepts surprisingly weird input
        // (Path.GetFullPath on "???***|||" does not throw, it just
        // returns a relative path against cwd).
        var ex = Record.Exception(() =>
            DataPathService.IsInsideInstallOrLocalAppDataRoot("\0\0invalid"));
        Assert.Null(ex);
    }
}
