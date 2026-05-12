# Task: First-Run Model Download Dialog

**ID:** 108
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Medium
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 107 (Velopack bootstrap)

## Objective

Replace the current "download on first attempted transcription" UX with a "download on first launch, with a modal progress dialog" UX. Surface the ~640 MB Parakeet download upfront so users on slow links know what they signed up for and the app never silently stalls on first dictation.

## Details

Today: `ModelManagerService` triggers a download lazily when the first transcription is attempted. The user has no visibility until they try to dictate, which then hangs on a silent network fetch.

Target: on `App.OnStartup`, if any required model is missing (or `VELOPACK_FIRSTRUN` is set), show a modal `FirstRunSetupWindow` before the main window loads.

### 1. `FirstRunSetupWindow`

WPF-UI styled modal window containing:
- App logo + one-line headline ("Setting up WhisperHeim").
- Per-model row:
  - Model name (Parakeet TDT 0.6B v3 / Silero VAD / Pyannote Seg 3.0).
  - Size estimate (e.g. "~640 MB").
  - Progress bar + bytes-downloaded / total.
  - Per-file pause / resume button (resume continues from current offset; HTTP Range requests).
- Bottom row:
  - Primary button: **Continue** (disabled until all required models are present).
  - Secondary link: **Skip for now** — closes the dialog, falls back to today's lazy-download behaviour the next time the user attempts a transcription.
- Persistent text: "One-time download. About 60 s on a 100 Mbps connection. Models are stored in `%APPDATA%\WhisperHeim\models` and survive uninstall."

### 2. `ModelManagerService` refactor

- Add `IAsyncEnumerable<ModelDownloadProgress> EnsureModelsAsync(IEnumerable<ModelDefinition> models, CancellationToken ct)` that streams progress per file.
- Existing lazy-download path stays as a fallback for users who skipped first-run setup.
- Persist `models/manifest.json` after a successful download so subsequent launches can fast-path past the dialog without re-hashing every file.

### 3. App startup flow

In `App.OnStartup` (after `VelopackApp.Build().Run()` returns):
1. Check `ModelManagerService.GetMissingRequiredModels()`.
2. If non-empty or `VELOPACK_FIRSTRUN=true`: show `FirstRunSetupWindow` modally on the UI thread before any other window construction.
3. After dialog closes, proceed with normal startup (tray icon, main window or start-minimized).

### 4. Bundled-models awareness

After Task 109 lands, Silero VAD and Pyannote Seg will be present in the publish output and never need downloading. The dialog should not list them when they're already on disk — show only Parakeet for new users in the common case.

### 5. Resilience

- Catch HF CDN failures (timeout, DNS, 5xx) and surface a friendly "model server unreachable — retry" UI inside the dialog with an exponential-backoff retry button.
- Cancellation: cancelling the dialog (Esc or close) marks the run as "skipped" and stores that state for the session (don't re-prompt on every window event).

## Acceptance Criteria

- [ ] `FirstRunSetupWindow.xaml` exists, WPF-UI styled, modal
- [ ] Shows on first launch (`VELOPACK_FIRSTRUN`) or whenever any required model is missing
- [ ] Per-model progress bar, byte counters, pause/resume, cancel
- [ ] **Skip for now** closes the dialog; lazy-download fallback still works
- [ ] `models/manifest.json` written on completion; subsequent launches skip the dialog when models are present
- [ ] HF CDN failure produces a retry UI, not a silent stall
- [ ] Start-minimized still works: when "start minimized" is on AND models are present, the dialog does not appear and there's no window flash
- [ ] When "start minimized" is on AND a model is missing, the dialog still shows (we can't sensibly proceed minimized) — confirmed with the user before merging if this UX is contested

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §1 "Bundling models" + §4 "First-run model download UX" + Implications #2.
- Reference apps (research §4): LM Studio (lazy), Whisper Desktop (first-launch picker), Buzz (bundled base + on-demand), Ollama (CLI `pull`). WhisperHeim user is closer to Whisper Desktop's target.
- HF availability is identified in Open Questions of the research as the main weak point — see §6 Notes there for a fallback-mirror idea (host model on WhisperHeim GitHub Releases as a 2nd source).
- The pause-on-large-download is important — user may be on tethered/metered connections.

## Work Log
<!-- Appended by /work during execution -->
