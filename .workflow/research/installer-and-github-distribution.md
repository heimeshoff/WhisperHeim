# Research: Windows Installer & GitHub Distribution for WhisperHeim

**Date:** 2026-05-12
**Status:** Complete
**Relevance:** Distribution / public GitHub release readiness — completes the auto-update-and-distribution research from 2026-03-27 with actionable bundling and FFmpeg decisions.

## Summary

Headline recommendations, in priority order:

1. **Do NOT bundle the Parakeet model (~640 MB) in the Velopack installer. Keep it as a first-run download — but move it from "download on first attempted use" to "download on first launch with a progress dialog gating dictation."** Bundling balloons every release asset to ~750 MB, sits uncomfortably close to GitHub's 2 GB/asset limit when paired with future channels, makes the installer feel heavy, and gains almost nothing because Velopack delta updates already skip unchanged files between versions. The Hugging Face CDN is fast and free, and this is exactly what LM Studio / Buzz / Whisper Desktop / Ollama all do [1][2][12][13][14].
2. **Bundle the small models (Silero VAD ~2 MB, Pyannote Seg 3.0 ~1.5 MB) directly in the Velopack package.** They're tiny, rarely change, and removing the network dependency for them means the app can boot into a usable state (recording works, just transcription is gated) even if the user is offline at install time.
3. **Do NOT bundle FFmpeg. Detect it at startup; if missing, prompt the user to install it themselves** with one-click options (`winget install Gyan.FFmpeg` and a "download page" link). This eliminates the LGPL source-mirror / attribution / license-tracking obligations entirely — *the user* picks the build and license, WhisperHeim just shells out via `Process.Start` to whatever's on PATH. The FFmpeg-dependent features (YouTube/Stream transcription, fallback OGG decode) gracefully degrade with a clear "FFmpeg required" affordance pointing at the same prompt. The licensing/build-provider research below is retained for the day we revisit bundling; it is **not the chosen path**.
4. **Use Velopack 0.0.1589+ (April 2026) with a tag-triggered GitHub Actions workflow** that runs `dotnet publish --self-contained`, `vpk download github`, `vpk pack`, `vpk upload github`. Code signing slot stays empty (`--signParams` / `--azureTrustedSignFile` flags exist now but unused) — flip the flag post-UG without re-architecting [5][6].
5. **Uninstall hygiene is already aligned by Velopack convention if we follow one rule:** user recordings, transcripts, and `bootstrap.json` MUST live in `%APPDATA%\WhisperHeim` (Roaming) or the user-chosen `DataPath`, never under `%LocalAppData%\WhisperHeim` (which is the install dir Velopack wipes on uninstall) [7][8]. Models are a grey area — see Implications.
6. **SmartScreen is worse in 2026 than in March:** Windows 11 24H2/25H2 tightened reputation thresholds (~15 000 safe downloads to clear), and Smart App Control may hard-block unsigned binaries with no "Run anyway" option for SAC-on users [9]. The unsigned-launch story is still viable for power users but needs a 30-second video on the GitHub Releases page, not just a sentence.

## Key Findings

### 1. Bundling models with Velopack

**Delta updates do skip unchanged files.** Velopack's delta packages are zstd-compressed binary diffs at the file level. The docs are explicit: "only transmit changes to files that have been modified. Unchanged files are not re-downloaded" [1]. So if we bundled the 640 MB model and shipped 1.0.0 → 1.0.1 with only a 5 MB code change, users would download a delta in the single-digit MB range, not 640 MB.

**However, there are real costs to bundling:**

- **Hard size ceiling.** Velopack/zstd cannot handle any single file >2 GB [1]. Our largest model file is `encoder.int8.onnx` at ~622 MB — comfortable today, but Parakeet successors or a larger fallback model would crowd this.
- **GitHub Releases per-asset limit is 2 GiB** [10]. A full `Setup.exe` at ~750 MB is fine; two parallel channels (stable + beta) get expensive in storage; sideloading a 1+ GB Whisper Large model later is awkward.
- **Initial download UX.** Users on slow links abandon big downloads. A 100 MB installer that then fetches 640 MB inside the app with a progress bar and a "this is one-time, pause/resume supported" message is materially better than a 750 MB single download with no UI.
- **CDN economics.** Hugging Face hosts the model for free at high speed. GitHub Releases is also free but counts against repo bandwidth — fine until we're popular.
- **No advantage on day-2 updates.** Because delta updates already skip the model, bundling doesn't reduce update bandwidth. It only changes *install* bandwidth, and only the *first* install at that.

**Online vs offline installer.** Velopack does not have separate "online" and "offline" installer modes [11]. The `Setup.exe` it produces is always self-contained for whatever is inside `--packDir`. The pattern for "download dependencies at first run" is to **not put them in `--packDir`** and instead use the `.OnFirstRun((v) => {...})` hook (or just normal app code keyed off `VELOPACK_FIRSTRUN`) [15][16]. There is a `--framework` flag that can bootstrap .NET, vcredist, WebView2 etc. before the app launches, but it does not support arbitrary URLs / model files [16].

**User experience comparison (Parakeet handling):**

| Approach | Installer size | First launch | Disk usage | Update bandwidth |
|---|---|---|---|---|
| Bundle | ~750 MB | Instant | ~750 MB | ~5–50 MB (delta skips model) |
| First-run download | ~110 MB | ~30–120 s download + UI | ~750 MB | ~5–50 MB |
| First-use download (current) | ~110 MB | Instant | ~110 MB until used | ~5–50 MB |

**Recommendation:** Move from "first-use" to "first-launch with progress." Reasons:
- Surface the dependency immediately so users on slow links know what they signed up for.
- Avoids the weird state where dictation is configured but silently fails because download hasn't started.
- Matches LM Studio / Buzz / Whisper Desktop / Ollama UX [2][12][13][14].

### 2. FFmpeg — user-installed, with first-use prompt

**Decision: don't ship FFmpeg.** Detect it at app start; if absent, surface a first-use prompt that lets the user install it themselves. Rationale:

- **Zero redistribution obligations.** If WhisperHeim never carries FFmpeg binaries, the LGPL "same-server source mirror + attribution + unmodified-binary" checklist [18] does not apply to us. The user's local FFmpeg install is *their* installation under *their* license — same as if they had it installed for their own use already.
- **Lets the user pick GPL builds freely.** `winget install Gyan.FFmpeg` is one command and pulls the popular gyan.dev GPL build. That's GPL on *the user's machine* and not commingled with our binary, so it does not contaminate WhisperHeim. We get to recommend the easiest install path without inheriting GPL.
- **Smaller installer, smaller updates.** Saves ~25–40 MB on every release asset and every delta.
- **Graceful degradation already exists.** `AudioFileDecoder.DecodeOgg` falls back to Concentus for OGG/Opus when `ffmpeg` is missing (`src/WhisperHeim/Services/FileTranscription/AudioFileDecoder.cs:71-83`). Only `StreamTranscriptionService` (YouTube/Stream URLs) hard-requires FFmpeg today. Drag-and-drop transcription of common audio files keeps working without it.

**Recommended UX:**

1. On `App.OnStartup`, run `Process.Start("ffmpeg", "-version")` (capturing stdout, killing after 2 s) and cache the result for the session.
2. Surface FFmpeg status in General/About: "FFmpeg detected: 7.x" or "FFmpeg not installed — install for Stream transcription".
3. When the user attempts a feature that needs FFmpeg and it's missing, show a modal with:
    - One-paragraph explanation of what FFmpeg unlocks (Stream/YouTube transcription, OGG decode fallback).
    - Primary button: **"Install with Windows Package Manager"** — runs `winget install -e --id Gyan.FFmpeg` in a child process with progress + log tail. (winget is preinstalled on Windows 11.)
    - Secondary button: **"Open download page"** → `https://www.gyan.dev/ffmpeg/builds/` (or BtbN if the user wants LGPL).
    - Tertiary: **"I installed it"** — re-runs detection.
4. On detection success, dismiss permanently; cache the resolved `ffmpeg.exe` path so we don't re-shell on every cold start (handle PATH changes by recovering at next failure).

**Reference / kept for the day we revisit bundling:** the rest of this section documents the build-provider landscape and LGPL compliance checklist if we ever decide to ship FFmpeg directly (e.g. enterprise/offline build, or once a code-signing identity exists and we want a frictionless first-launch). **Skip unless reconsidering the decision.**

**Two build providers matter for Windows in 2026:**

| Provider | Build variants | Binary license | Cadence | Verdict |
|---|---|---|---|---|
| **gyan.dev** | essentials, full, full-shared | **GPLv3 only** [4] | Per release | **Avoid** for WhisperHeim |
| **BtbN/FFmpeg-Builds** | gpl, gpl-shared, lgpl, lgpl-shared, nonfree, nonfree-shared [3] | MIT (build scripts); LGPL/GPL/proprietary per variant | Daily auto-builds at 12:00 UTC, last 14 days + monthly archives kept 2 years [3] | **Use `lgpl-shared`** |

The closed-source path is `lgpl-shared`: dynamic linking, no GPL-only codecs (no x264/x265 — irrelevant for audio decode), no `--enable-nonfree`. This is the long-established commercial-friendly path [17][18][19].

**LGPL compliance for WhisperHeim** (we invoke via `Process.Start` — even safer than dynamic linking, but the same rules apply per FFmpeg's official compliance checklist [18]):

1. Compile from an LGPL build only — no `--enable-gpl`, no `--enable-nonfree`. Using BtbN's `lgpl-shared` artifacts satisfies this by construction.
2. Do not modify the FFmpeg binaries. (We don't — we ship them as-is.)
3. **Provide source code** for the exact FFmpeg version we redistribute, **on the same server** as the binary [18]. In practice: each GitHub Release that bundles FFmpeg needs a sibling `ffmpeg-<version>-source.zip` asset (or a stable mirror link in the release notes that we control). Linking to the upstream tarball is the *fallback*, not the primary mechanism — FFmpeg's legal page explicitly demands same-server hosting.
4. **Include attribution**: LGPL text, copyright notices, list of FFmpeg authors, and a statement that this build is unmodified LGPL FFmpeg. Standard locations: `LICENSES/FFmpeg/` folder next to the binary, and a line in the WhisperHeim About dialog.
5. **EULA / About text**: "This product uses libraries from the FFmpeg project under the LGPLv2.1." Include the LGPL text link.

**Process.Start specifically.** The FFmpeg legal page does not enumerate inter-process invocation. The community consensus — and Medialooks' commercial LGPL guidance [19] — is that running a separate process is *less restrictive* than dynamic linking, since there's no linking at all, only IPC. As long as we ship the unmodified LGPL binaries with proper attribution and source, we're safer than dynamic-linkers, not riskier. We should still treat compliance as if dynamic linking applied (attribution + source) because that's the strictest reading and costs us nothing extra.

**Size:** A BtbN `lgpl-shared` Windows ZIP runs roughly 25–40 MB (the equivalent `gpl-shared` shows 92 MB on the latest release page [20], and `lgpl` builds drop ~half of that by removing libx264/libx265/etc.). The BtbN releases page doesn't fully expand asset sizes in one fetch — confirm with `Invoke-WebRequest -Method Head` against the live URL when packaging. For our needs (OGG/Opus + arbitrary container demux for YouTube/streams), we ship the full `lgpl-shared` rather than trying to slim it further. Disk and bandwidth are not the bottleneck here, simplicity is.

**Should FFmpeg be bundled or downloaded?** **Neither, per the decision above.** Detection + user-driven install via winget. The bundling option is documented here only so a future reversal of the decision (e.g. once we have a signing identity and want a frictionless first-launch) has the LGPL groundwork ready.

### 3. Concrete Velopack pipeline for .NET 9 WPF in 2026

**Latest tooling:** Velopack NuGet `0.0.1589` from 2026-04-14 [21]. Active development continues — the version baseline from the March research is still valid, the surface area hasn't broken.

**WPF custom Main** (unchanged from March research, but here's the clean form Velopack docs recommend [15][22]):

```csharp
[STAThread]
private static void Main(string[] args)
{
    VelopackApp.Build()
        .OnFirstRun(v => { /* set firstRun flag, surface model download dialog */ })
        .Run();

    var app = new App();
    app.InitializeComponent();
    app.Run();
}
```

In `WhisperHeim.csproj`:

```xml
<PropertyGroup>
  <StartupObject>WhisperHeim.Program</StartupObject>
</PropertyGroup>
<ItemGroup>
  <ApplicationDefinition Remove="App.xaml" />
  <Page Include="App.xaml" />
</ItemGroup>
```

`vpk pack` will warn that `VelopackApp.Run()` is not in the entry point of the assembly — this is **expected** with a custom Main and can be ignored [22].

**Recommended GitHub Actions workflow** (sketch based on Velopack docs [5]):

```yaml
name: Release
on:
  push:
    tags: ['v*']

permissions:
  contents: write

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      - name: Extract version
        id: ver
        shell: pwsh
        run: |
          $tag = "${{ github.ref_name }}".TrimStart('v')
          "version=$tag" >> $env:GITHUB_OUTPUT
      - name: Publish
        run: dotnet publish src/WhisperHeim/WhisperHeim.csproj `
             -c Release -r win-x64 --self-contained `
             -p:PublishReadyToRun=true -o publish
      - name: Install vpk
        run: dotnet tool install -g vpk
      - name: Download previous releases
        run: vpk download github --repoUrl https://github.com/${{ github.repository }} --token ${{ secrets.GITHUB_TOKEN }}
      - name: Pack
        run: vpk pack --packId WhisperHeim `
             --packVersion ${{ steps.ver.outputs.version }} `
             --packDir publish `
             --mainExe WhisperHeim.exe `
             --packTitle "WhisperHeim" `
             --packAuthors "Marco Heimeshoff"
      - name: Upload to GitHub Release
        run: vpk upload github --repoUrl https://github.com/${{ github.repository }} `
             --tag v${{ steps.ver.outputs.version }} `
             --releaseName "WhisperHeim ${{ steps.ver.outputs.version }}" `
             --publish `
             --token ${{ secrets.GITHUB_TOKEN }}
```

**Code signing hook for later** [6]: when an EV cert or Azure Trusted Signing comes online, add either:

- `--signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f cert.pfx /p $PASS"` (traditional signtool), or
- `--azureTrustedSignFile signing.json` (Azure Trusted Signing JSON config) [6].

No code or project structure changes needed. The pack command does incremental signing of both app binaries and Velopack's own Update.exe / Setup.exe in the right order, which is why signing must go through `vpk` and not as a post-build step [6].

**Install / update window behavior.** Velopack's `Setup.exe` shows a small progress UI during extraction, then launches the app. With `Setup.exe --silent` no UI is shown at all and the app does not auto-launch — useful for CI / fleet install but not for our consumer flow. After an update, Velopack's `Update.exe` runs the new version's `--veloapp-updated` hook (15 s timeout, no UI allowed) [23] and then starts the app. **The app starts normally** — our existing Task 106 fix (no window frame flash when start-minimized) is unaffected by Velopack as long as we don't put any window-showing code in `OnFirstRun` or the install hooks. The `VELOPACK_FIRSTRUN` environment variable [23] is the clean way to detect first-run inside `App.OnStartup` and decide whether to show a setup dialog vs. drop straight into the tray.

### 4. First-run model download UX

**Velopack does not solve model downloading for you.** The `--framework` flag only handles a fixed allowlist (.NET runtime, vcredist, WebView2, .NET Framework) [16]. Models must be downloaded by WhisperHeim's own code.

**Better pattern than current** ("download on first attempted use" — `ModelManagerService` triggered by the first transcription attempt):

- On `App.OnStartup`, if `VELOPACK_FIRSTRUN=true` or any required model is missing, show a modal **first-run setup window** with: model name, size, download progress, pause/resume, "skip for now" link. Use `HttpClient` with progress, or a small library — we already do `HttpClient` in `ModelManagerService`.
- After the dialog closes (success or skip), hand off to the existing flow. If user skipped and then tries to dictate, fall back to today's lazy-download behavior with the same dialog.
- Persist a small `models/manifest.json` after success so subsequent launches skip the dialog without re-hashing every file.

**What comparable apps do:**

- **LM Studio:** App opens, model list is empty, user clicks search and picks a model. Download is a card with progress. No setup dialog at launch [2].
- **Whisper Desktop:** First screen explicitly asks user to pick and download a model (recommends `ggml-medium.bin`, 1.42 GB) [12].
- **Buzz:** Bundles a small base model in the installer (~1 GB total install) and downloads larger Whisper variants on demand [13].
- **Ollama:** CLI, user pulls models by name with progress in terminal [14].

The closest analogue for WhisperHeim is Whisper Desktop — single bundled default model expected, first-launch download with progress UI. Our user is more advanced than LM Studio's average user, so explaining "Parakeet is 640 MB, this is a one-time download, takes about 60 seconds on a 100 Mbps connection" is well-received, not scary.

### 5. SmartScreen + GitHub Releases UX in 2026

**The landscape since the March research** [9]:

- Windows 11 24H2 and 25H2 raised the reputation threshold; unsigned new releases now need on the order of 15 000 safe downloads to clear, vs. a few thousand historically.
- Smart App Control (SAC) is opt-in but increasingly default on fresh Windows 11 installs. **For SAC-on users, unsigned binaries are hard-blocked with no "Run anyway" option.** This is a real share of new Windows 11 PCs.
- The standard SmartScreen dialog ("Windows protected your PC") still has the "More info" → "Run anyway" path for everyone else.

**Concrete instructions to put on the GitHub Release page:**

1. Download `WhisperHeim-Setup.exe`.
2. Windows will show "Windows protected your PC". Click **More info**, then **Run anyway**.
3. If the button doesn't appear and the dialog says "Smart App Control" instead of "Windows Defender SmartScreen", you have Smart App Control on. There is no override — you would need to disable SAC system-wide (irreversible until OS reinstall) or wait for a signed build. We are working on signing post-UG-registration.

**Release page content:**

- A 20–30 second screen-recording of the click-through.
- A SHA-256 hash of the setup.exe (lets technically inclined users verify).
- A note that the app is unsigned because as a German individual developer Microsoft Trusted Signing is not available; signing will be added once the UG is registered.

**Antivirus false positives** for self-contained single-file .NET apps remain a real annoyance — historic `PublishTrimmed` issues are documented [24], and Defender has had a rocky 2026 with broader certificate-related false positives [25]. Mitigations: don't trim WPF (already decided), submit the unsigned binary to Microsoft via the Defender false-positive form after each release if reports come in, and the long-term fix is signing.

### 6. Uninstall hygiene

**What Velopack removes on uninstall:**

- The entire install directory `%LocalAppData%\WhisperHeim\` — including the `current\` folder with WhisperHeim.exe, all its DLLs, the bundled FFmpeg, and the small bundled models if we put them there. [7][8]
- All shortcuts Velopack itself created (Start menu, desktop). [7]
- The Add/Remove Programs registry entry.

**What Velopack does NOT remove:**

- `%AppData%\WhisperHeim\` (Roaming) — explicitly preserved [8].
- Any directory the user configured outside the install dir (our `DataPath`).

**Implications for our current layout** (verified from `ModelManagerService.cs` and `DataPathService`):

- `ModelManagerService.ModelsRoot` defaults to `Environment.SpecialFolder.ApplicationData` (Roaming) + `WhisperHeim\models`. **This is correct** — survives uninstall, which is what we want for the 640 MB Parakeet (users do not want to re-download after a reinstall).
- User recordings and `bootstrap.json` should likewise be under Roaming or the configurable `DataPath`. (Already the case per recent task 104/105 work.)
- We must **never** put models under `%LocalAppData%\WhisperHeim` — that's the install dir Velopack wipes.

**Pre-uninstall hook** [23] could optionally write a small "thank you / where did your data go" file to the user's desktop, but the simpler approach is a line in the README and Add/Remove Programs description: "User data is preserved in %APPDATA%\WhisperHeim. Delete that folder manually if you want a clean removal."

## Implications for This Project

A worker can turn this into the following tasks. None of them are large.

1. **Add Velopack to the project** — `dotnet add package Velopack`, create `Program.cs` with `[STAThread] static Main`, set `App.xaml` Build Action to `Page`, set `<StartupObject>WhisperHeim.Program</StartupObject>`, add `VelopackApp.Build().Run()` ahead of WPF startup. Verify start-minimized still has no window flash (Task 106 sanity check).
2. **First-run model download dialog** — extend `ModelManagerService` so `App.OnStartup` can call it with progress callbacks; build a modal `FirstRunSetupWindow` with progress bar, pause/cancel, and a "skip and configure later" link. Keep current lazy-download as fallback.
3. **Bundle small models in publish output** — copy Silero VAD and Pyannote Seg 3.0 into `publish/models/` as part of the build (csproj `<Content Include>` or a post-publish step). Update `ModelManagerService` to look in the bundled location before the per-user models folder.
4. **FFmpeg detection + first-use install prompt** — add a small `FfmpegDetector` service that runs `ffmpeg -version` once at startup and caches the result. Surface status in General/About. When a feature that needs FFmpeg is invoked and detection failed, show a modal with: a one-paragraph "what this unlocks" explainer, a primary "Install with Windows Package Manager" button (shells `winget install -e --id Gyan.FFmpeg` with progress), a secondary "Open download page" link, and a tertiary "I installed it" re-detect button. Update `StreamTranscriptionService` and `AudioFileDecoder` to consult the cached detector before shelling out. No bundled binary, no LICENSES folder, no source mirror.
5. **GitHub Actions release workflow** — create `.github/workflows/release.yml` along the lines of section 3 above. Trigger on `v*` tags. No signing flags yet.
6. **README + Release page** — add SmartScreen click-through instructions, a 20–30 s screen recording of the install, SHA-256 of the setup.exe (auto-generated in the workflow), the unsigned-developer explanation, and a short "Optional: install FFmpeg for YouTube/Stream transcription via `winget install Gyan.FFmpeg`" note. Note Smart App Control caveat.
7. **Uninstall README note** — make explicit in the README and the Add/Remove Programs publisher field that uninstall preserves `%AppData%\WhisperHeim` and any configured `DataPath`. Optional pre-uninstall hook to drop a `where-is-my-data.txt` on the desktop.
8. **Validate Velopack with our publish output (one-off test)** — sanity-check that `vpk pack` handles our publish output (~110 MB) cleanly. Doesn't need to be repeated, just once before tagging the first release.
9. **Defer code signing** — leave a TODO in the workflow comments showing where `--signParams` / `--azureTrustedSignFile` plug in. No structural change required to flip later.

## Open Questions

- **Exact BtbN `lgpl-shared` ZIP size.** Page only fully expands GPL builds in a single fetch (92.1 MB shown). LGPL variant should be smaller (~25–40 MB), but confirm at packaging time with a HEAD request and codify the expected size in the release workflow.
- **Hugging Face availability SLA.** The Parakeet download URL is a public HF CDN endpoint with no documented SLA. If HF has an outage on the day someone installs WhisperHeim, first-run download fails. Mitigation: catch and retry, surface a clear "model server unreachable, retry later" UI, optionally mirror the model on the WhisperHeim GitHub Releases page as a backup (~640 MB asset is within the 2 GiB limit).
- **Smart App Control share.** No public data on what fraction of Windows 11 25H2 installs have SAC on. The unsigned-launch instructions handle SmartScreen but not SAC. Re-evaluate signing urgency once we have install-failure telemetry from beta users.
- **Velopack with our exact 110 MB publish output.** Velopack handles "biggest of applications" per docs [1], but we should still do a single end-to-end pack-then-install dry run before tagging 1.0.0. Cheap insurance.
- **`Setup.exe` startup window.** Velopack's setup UI is minimal but we haven't visually verified it on Windows 11 25H2 against the WhisperHeim icon and tray-app flow. Worth a 10-minute manual walkthrough as part of Task 1.

## Sources

1. [Delta Updates | Velopack docs](https://docs.velopack.io/packaging/deltas) — explicit "unchanged files are not re-downloaded"; zstd 2 GB per-file limit.
2. [LM Studio](https://lmstudio.ai/) — model download UX reference.
3. [BtbN/FFmpeg-Builds (GitHub)](https://github.com/BtbN/FFmpeg-Builds) — variants list including `lgpl-shared`; daily cadence; MIT build scripts.
4. [Builds — CODEX FFMPEG @ gyan.dev](https://www.gyan.dev/ffmpeg/builds/) — **GPLv3-only**, not commercial-friendly for closed source.
5. [GitHub Actions | Velopack docs](https://docs.velopack.io/distributing/github-actions) — workflow template with `vpk download`, `vpk pack`, `vpk upload`.
6. [Code Signing | Velopack docs](https://docs.velopack.io/packaging/signing) — `--signParams`, `--azureTrustedSignFile`, incremental signing model.
7. [Windows Overview | Velopack docs](https://docs.velopack.io/packaging/operating-systems/windows) — install path `%LocalAppData%\{packId}`, entire dir replaced on update.
8. [Preserving Files & Settings | Velopack docs](https://docs.velopack.io/integrating/preserved-files) — `%AppData%\{packId}` survives uninstall.
9. [Unsigned EXE and Windows SmartScreen: Bypassing Warnings in 2026 (CopyProgramming)](https://copyprogramming.com/howto/how-does-this-unsigned-exe-launch-without-the-windows-10-smartscreen-warning) — 25H2 tightening, 15 000-download threshold, SAC behavior. Secondary source; cross-check before relying.
10. [About large files on GitHub (GitHub docs)](https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github) — confirms 2 GiB per-release-asset limit.
11. [Packaging Overview | Velopack docs](https://docs.velopack.io/packaging/overview) — Full / Delta / Portable / Setup.exe artifact types; no separate online/offline installer modes.
12. [Whisper Desktop: Free Speech-to-Text in Under 5 Minutes (nolongerset.com)](https://nolongerset.com/whisper-desktop/) — first-run model picker UX.
13. [Buzz Releases (GitHub)](https://github.com/chidiwilliams/buzz/releases) — bundles small base model, downloads variants on demand.
14. [How to Run an Open-Source LLM on Your Personal Computer — Ollama (freeCodeCamp)](https://www.freecodecamp.org/news/how-to-run-an-open-source-llm-on-your-personal-computer-run-ollama-locally/) — Ollama `pull` UX.
15. [Getting Started: .NET / Generic C# App | Velopack docs](https://docs.velopack.io/getting-started/csharp) — WPF custom Main, `App.xaml` → Page, `VelopackApp.Build().Run()`.
16. [Bootstrapping | Velopack docs](https://docs.velopack.io/packaging/bootstrapping) — `--framework` allowlist (dotnet, vcredist, WebView2, .NET Framework). No arbitrary URL support.
17. [FFmpeg/LICENSE.md (master branch, GitHub)](https://github.com/FFmpeg/FFmpeg/blob/master/LICENSE.md) — LGPLv2.1+ baseline; `--enable-gpl` flips to GPL.
18. [FFmpeg License and Legal Considerations (ffmpeg.org)](https://www.ffmpeg.org/legal.html) — LGPL compliance checklist: no `--enable-gpl`, no `--enable-nonfree`, distribute source on same server, attribution.
19. [Using LGPL in commercial software (Medialooks)](https://medialooks.com/lgpl) — commercial-vendor perspective on LGPL dynamic-linking compliance. Vendor source — useful but biased toward "it's fine".
20. [BtbN/FFmpeg-Builds Releases (GitHub)](https://github.com/BtbN/FFmpeg-Builds/releases) — latest GPL-shared Windows ZIP 92.1 MB (2026-05-10); LGPL variant sizes require expanded asset list.
21. [Velopack on NuGet](https://www.nuget.org/packages/velopack) — latest 0.0.1589 (2026-04-14), active development.
22. [App With Velopack Installer (Nicolai Henriksen, GitHub)](https://github.com/nicolaihenriksen/AppWithVelopackInstaller) — working WPF sample with custom Main.
23. [App Hooks | Velopack docs](https://docs.velopack.io/integrating/hooks) — install / obsolete / updated / uninstall hooks; `VELOPACK_FIRSTRUN` and `VELOPACK_RESTART` env vars; "no UI in hooks" rule.
24. [/p:PublishTrimmed=true activates Windows Defender false-positive (dotnet/runtime#33745)](https://github.com/dotnet/runtime/issues/33745) — historical .NET trimming AV false-positive issue.
25. [Microsoft Defender wrongly flags DigiCert certs as Trojan (BleepingComputer, May 2026)](https://www.bleepingcomputer.com/news/security/microsoft-defender-wrongly-flags-digicert-certs-as-trojan-win32-cerdigentadha/) — 2026 Defender false-positive landscape context.
