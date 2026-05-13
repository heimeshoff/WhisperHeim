# infrastructure

## Purpose
This BC owns *globally-true* infra concerns for WhisperHeim — runtime, packaging, distribution, code signing, FFmpeg detection, settings/data-path resolution, GitHub Actions release pipeline. BC-local infra (audio device adapters, transcription queue plumbing inside `main/`) stays inside the originating BC.

## Classification
generic

Standard ops/distribution plumbing. Not differentiating; chosen for fit (Velopack for delta updates, GitHub Releases for distribution, winget for FFmpeg sidestep on LGPL/GPL).

## Actors
- **Maintainer** — tags releases, watches workflow runs, decides when to sign.
- **End user (transitively)** — experiences the infra layer as first-run model download, auto-update, uninstall behavior.

## Ubiquitous language
- **Velopack / vpk** — packaging + auto-update framework. `vpk pack` emits `Setup.exe` + delta nupkg + RELEASES manifest.
- **Bootstrap config** — small pointer file in `%AppData%\WhisperHeim\` that locates the real configurable data path (supports cloud-synced setups).
- **Data path** — user-configurable folder containing `settings.json`, `recordings/`, `voices/`. Defaults next to the bootstrap config but may live in a cloud-synced drive.
- **SmartScreen / SAC** — Windows reputation / Smart App Control gates. The app ships unsigned today; SAC hard-blocks unsigned binaries in 25H2, mitigated via a release-page click-through video.

## Aggregates
- **Release artifact** — a tagged `v*` build comprising `Setup.exe`, delta nupkg, RELEASES manifest. Reproducible from the GitHub Actions workflow.
- **Bootstrap+data-path pair** — protects the invariant that machine-local settings never sync, while user data can.

## Key events
- `ReleaseTagged` / `ReleasePublished`
- `ModelDownloadStarted` / `ModelDownloadCompleted` (first-run)
- `FFmpegDetected` / `FFmpegMissing`

## Key commands
- `PackRelease` (CI)
- `BootstrapDataPath` (first run)
- `ResolveFFmpeg`

## Relationships with other contexts
- **Upstream of:** `main/` — every domain feature runs on top of this BC's runtime + packaging foundation.

## Open questions
- Code signing — currently stubbed (Task `main-115`); to be flipped post-UG registration. Decision between signtool with PFX vs. Azure Trusted Signing still open.
- One-time-purchase license-key infrastructure — implied by monetization research; no task yet.
