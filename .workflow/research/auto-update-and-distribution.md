# Research: Auto-Update and Distribution for Windows Desktop Apps

**Date:** 2026-03-27
**Status:** Complete
**Relevance:** Distribution strategy for WhisperHeim — packaging, auto-updates, code signing, and delivery to end users

## Summary

For a .NET 9 WPF app like WhisperHeim, **Velopack** is the clear winner for installer + auto-update. It's free, open-source, written in Rust for native performance, supports delta updates, and has first-class .NET/WPF support. It replaces the now-unmaintained Squirrel.Windows and works with any static file host (GitHub Releases, S3, Azure Blob, etc.).

Code signing is the biggest pain point for an individual developer in Germany. Microsoft's Trusted Signing ($10/month) only supports individual developers in USA/Canada — EU individuals are excluded. Traditional OV certificates cost $200–500/year and require weeks to build SmartScreen reputation. EV certificates ($300–700/year) give instant SmartScreen trust but require a registered business (e.g., a UG). The pragmatic path is: launch unsigned initially (users get a SmartScreen warning they can click through), then sign once the UG is established.

MSIX is overkill for this project. It adds complexity (containerization, manifest declarations, restricted filesystem access) without meaningful benefit for a tray app that needs unrestricted system access (WASAPI loopback, global hotkeys, SendInput).

## Key Findings

### Velopack: The Right Update Framework

Velopack is the successor to Squirrel.Windows, rewritten in Rust. Core features:

- **100% free and open-source** — no paid tiers for the core SDK and CLI
- **Delta updates** — users download only diffs between versions
- **One-click installer** — installs and launches in seconds, no admin required
- **WPF support** — documented pattern: change `App.xaml` Build Action to `Page`, add custom `Main` method, call `VelopackApp.Build().Run()` before WPF startup
- **Cross-platform** — Windows, macOS, Linux (useful if ever needed)
- **Any hosting** — GitHub Releases (free for open-source), S3, Azure Blob, any HTTP server
- **Prerequisite installation** — can install .NET runtime, vcredist, etc. if needed
- **Migration from Squirrel** — seamless migration path if needed

**Integration steps:**
1. `dotnet tool install -g vpk`
2. Add `VelopackApp.Build().Run()` at top of `Main`
3. Use `UpdateManager` to check/download/apply updates
4. `dotnet publish -c Release --self-contained -r win-x64 -o ./publish`
5. `vpk pack --packId WhisperHeim --packVersion 1.0.0 --packDir ./publish --mainExe WhisperHeim.exe`

Sources: [Velopack docs](https://docs.velopack.io/), [GitHub](https://github.com/velopack/velopack), [.NET Getting Started](https://docs.velopack.io/getting-started/csharp)

### MSIX: Not a Good Fit

MSIX is Microsoft's modern packaging format with containerization. Reasons it's wrong for WhisperHeim:

- **Containerization restricts system access** — MSIX apps can't freely write to registry, AppData (shared), or install directory. WhisperHeim needs unrestricted access for WASAPI loopback, global hotkeys, and SendInput.
- **No tray icon issues** — WPF tray apps in MSIX containers can have quirks with system tray integration
- **Overkill complexity** — manifest declarations, packaging project, signing requirements
- **Store not needed** — WhisperHeim's value is being independent; Microsoft Store distribution adds friction, not value

MSIX is better suited for enterprise deployment (IT-managed machines) or Store distribution, neither of which applies here.

Sources: [TechTarget MSI vs MSIX](https://www.techtarget.com/searchenterprisedesktop/tip/Comparing-MSI-vs-MSIX), [MSIX docs](https://learn.microsoft.com/en-us/windows/msix/)

### Code Signing Options for a German Individual Developer

| Option | Cost | SmartScreen | Availability |
|--------|------|-------------|-------------|
| **No signing** | Free | Warning shown, user must click through | Immediate |
| **OV certificate** | $200–500/year | Builds reputation over 2–8 weeks | Requires identity verification |
| **EV certificate** | $300–700/year | Instant trust | Requires registered business (UG/GmbH) |
| **Microsoft Trusted Signing** | $10/month (~$120/yr) | Instant trust | USA/Canada individuals only; EU orgs only |

**Key points:**
- As of March 2026, Microsoft Trusted Signing does **not** support individual developers in Germany/EU — only organizations with a business registration
- Since February 2026, code signing certificate max lifetime is 459 days (~15 months)
- Self-signed certificates do **not** bypass SmartScreen at all
- The cheapest legitimate option for an individual: OV cert (~$130–200/yr from budget CAs like CheapSSLSecurity), accept 2–8 week SmartScreen warming period
- Once the UG is registered (per legal research), an EV cert or Trusted Signing becomes available

**Recommended path:**
1. **Pre-launch:** Distribute unsigned. Early adopters expect this. Document "how to install" with SmartScreen click-through instructions.
2. **Post-UG registration:** Get an EV certificate or use Microsoft Trusted Signing (if EU org support is available). Instant SmartScreen trust.

Sources: [Trusted Signing announcement](https://techcommunity.microsoft.com/blog/microsoft-security-blog/trusted-signing-is-now-open-for-individual-developers-to-sign-up-in-public-previ/4273554), [SmartScreen guide](https://www.advancedinstaller.com/prevent-smartscreen-from-appearing.html), [SignMyCode pricing](https://signmycode.com/)

### Publishing and Size Optimization

For `dotnet publish` with .NET 9 WPF:

- **Self-contained** is required (users shouldn't need to install .NET runtime separately)
- **Single-file** (`PublishSingleFile`) bundles everything into one .exe — works with WPF
- **Trimming** (`PublishTrimmed`) is **risky with WPF** — .NET 9 has known issues with missing WPF assemblies (PresentationCore.dll, PresentationFramework.dll). **Do not trim WPF apps** without thorough testing.
- **ReadyToRun** (`PublishReadyToRun`) pre-compiles IL to native — faster startup, slightly larger binary. Recommended.
- Expected size: ~80–150 MB self-contained (before Velopack delta compression)
- Velopack delta updates will reduce subsequent update downloads to only changed files

**Recommended publish command:**
```
dotnet publish -c Release --self-contained -r win-x64 -p:PublishReadyToRun=true -o ./publish
```

Sources: [.NET trimming docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained), [Single file docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)

### Hosting Update Files

Velopack needs a static HTTP endpoint serving release files. Options:

| Host | Cost | Notes |
|------|------|-------|
| **GitHub Releases** | Free (public repos) | Best for open-source phase; Velopack has built-in GitHub support |
| **Azure Blob Storage** | ~$0.02/GB/month | Cheap, integrates with Trusted Signing |
| **AWS S3** | ~$0.023/GB/month | Standard option |
| **Any web server** | Varies | Just needs to serve static files over HTTPS |

For WhisperHeim's freemium model: use GitHub Releases for the free tier, and a private update URL for paid features (Velopack supports custom update URLs per instance).

## Implications for This Project

1. **Add Velopack early** — integrating it now costs ~30 minutes of setup and makes every future release painless. The `VelopackApp.Build().Run()` bootstrap and `UpdateManager` are minimal code.
2. **Don't bother with MSIX** — it would restrict the system-level access WhisperHeim needs.
3. **Ship unsigned initially** — code signing is expensive and bureaucratic. Early adopters will click through SmartScreen. Add signing after UG registration.
4. **Use GitHub Releases** for hosting updates during development/beta. Switch to a private server when the paid tier launches.
5. **Don't trim WPF** — use self-contained + ReadyToRun instead. Accept the ~100 MB base size; delta updates keep subsequent downloads small.
6. **CI/CD with GitHub Actions** — `vpk pack` can run in a GitHub Actions workflow, auto-publishing releases on tagged commits.

## Open Questions

- What's the exact Velopack update UX? Can we show a "new version available" notification in the tray icon rather than auto-restarting?
- Does Velopack handle the large ONNX model files (~600 MB) well, or should models be downloaded separately (current approach) and excluded from the package?
- When the UG is registered, which EV certificate vendor offers the best deal for small German companies?
- Can Velopack enforce license keys for the paid tier (or is that a separate concern)?

## Sources

- [Velopack official site](https://velopack.io)
- [Velopack GitHub](https://github.com/velopack/velopack)
- [Velopack docs — Getting Started C#](https://docs.velopack.io/getting-started/csharp)
- [Velopack docs — Distributing](https://docs.velopack.io/distributing/overview)
- [Velopack migration from Squirrel](https://docs.velopack.io/migrating/squirrel)
- [Microsoft MSIX documentation](https://learn.microsoft.com/en-us/windows/msix/)
- [TechTarget — MSI vs MSIX](https://www.techtarget.com/searchenterprisedesktop/tip/Comparing-MSI-vs-MSIX)
- [Microsoft Trusted Signing announcement](https://techcommunity.microsoft.com/blog/microsoft-security-blog/trusted-signing-is-now-open-for-individual-developers-to-sign-up-in-public-previ/4273554)
- [Microsoft Trusted Signing — country availability Q&A](https://learn.microsoft.com/en-us/answers/questions/5810735/cant-create-a-new-trusted-signing-individual-ident)
- [SmartScreen avoidance guide](https://www.advancedinstaller.com/prevent-smartscreen-from-appearing.html)
- [.NET trimming docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [.NET single file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Rick Strahl — Microsoft Trusted Signing experience](https://weblog.west-wind.com/posts/2025/Jul/20/Fighting-through-Setting-up-Microsoft-Trusted-Signing)
