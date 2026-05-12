# Task: FFmpeg Detection + First-Use Install Prompt

**ID:** 110
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Medium
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** None

## Objective

WhisperHeim does not bundle FFmpeg. Instead, the app detects FFmpeg at startup and — when a feature that needs it is invoked but FFmpeg is missing — surfaces a modal install prompt with one-click options (`winget install -e --id Gyan.FFmpeg`, manual download link, "I installed it"). This sidesteps LGPL/GPL redistribution obligations entirely: the user picks the build under their own license; WhisperHeim just shells out via `Process.Start`.

## Details

### 1. `FfmpegDetector` service

A small singleton service (DI-registered) with:

- `Task<FfmpegInfo?> DetectAsync(CancellationToken ct = default)` — runs `ffmpeg -version` with a 2 s kill timeout. Returns version string + resolved absolute path on success, `null` on failure.
- `FfmpegInfo? CachedInfo { get; }` — last successful detection result for this session.
- `event EventHandler StateChanged` — fires when `CachedInfo` transitions null→present or present→null.

Detection logic:
1. Try `Process.Start("ffmpeg", ...)` — PATH-resolved.
2. If that fails, check well-known winget install locations (`%LOCALAPPDATA%\Microsoft\WinGet\Packages\Gyan.FFmpeg_*\ffmpeg-*\bin\ffmpeg.exe`) as a courtesy in case the user installed it via winget but their PATH hasn't been refreshed.
3. Cache the resolved absolute path so subsequent invocations don't re-shell.

Run detection once on `App.OnStartup` after the model dialog closes. Re-run on demand from the prompt's "I installed it" button.

### 2. Surface in General/About

Add a small "FFmpeg" status card to `GeneralPage.xaml`:
- ✅ `FFmpeg 7.x detected at C:\Users\...\ffmpeg.exe`, or
- ⚠️ `FFmpeg not installed — needed for YouTube/Stream transcription` with a "Install now" button that opens the prompt below.

### 3. `FfmpegMissingDialog` modal

WPF-UI styled modal shown when:
- The user clicks "Install now" on the General page, **or**
- `StreamTranscriptionService` or other FFmpeg-dependent code is invoked while `FfmpegDetector.CachedInfo == null`.

Contents:
- One-paragraph explainer: "WhisperHeim uses FFmpeg to transcribe YouTube videos, web streams, and certain audio formats. It's a separate program — install it once and WhisperHeim will find it automatically."
- **Primary button:** "Install with Windows Package Manager" — runs `winget install -e --id Gyan.FFmpeg` in a child process, streams stdout/stderr into a small log textbox, shows a progress spinner, re-runs detection on exit.
- **Secondary link:** "Open download page" → `https://www.gyan.dev/ffmpeg/builds/` (use `Process.Start` with `UseShellExecute=true`).
- **Tertiary button:** "I already installed it" — re-runs `FfmpegDetector.DetectAsync()`, dismisses on success, surfaces "still not found, check PATH" on failure.
- **Cancel:** closes the dialog. The caller (e.g. `StreamTranscriptionService.StartAsync`) handles the cancellation by surfacing a friendly "Stream transcription requires FFmpeg" message and bailing.

### 4. Update FFmpeg-invocation sites

Two known call sites (confirm via Grep before merging):
- `src/WhisperHeim/Services/Streams/StreamTranscriptionService.cs`
- `src/WhisperHeim/Services/FileTranscription/AudioFileDecoder.cs:88` (`DecodeOggWithFfmpeg`)

Change them to:
1. Call `FfmpegDetector.CachedInfo`; if null, surface the modal on the UI thread via a `IFfmpegPromptService` abstraction (so the services stay UI-agnostic and testable).
2. On detection success after the modal closes, retry the original operation once.
3. `AudioFileDecoder.DecodeOgg` already has a Concentus fallback — keep that fallback ahead of any modal prompt, since OGG decode should not block a transcription on a UI dialog. Only `StreamTranscriptionService` hard-requires FFmpeg.

### 5. winget edge cases to handle

- winget not present (rare on Win11, but possible on Win10 / heavily managed machines): show a "winget unavailable — please use the download link" message.
- winget install requires accepting source agreements on first run; pass `--accept-source-agreements --accept-package-agreements`.
- winget may need elevation. If the install fails with `ERROR: Access denied`, surface a "run elevated" hint.
- PATH refresh: after `winget install` exits, the new ffmpeg won't be on this process's PATH until the env is refreshed. Re-detect via the well-known winget install location (step 1.2) — that's why we check it.

## Acceptance Criteria

- [ ] `FfmpegDetector` service exists, DI-registered, runs detection once on startup with a 2 s kill timeout
- [ ] FFmpeg status visible on General page (✅ detected with version + path, or ⚠️ missing with Install button)
- [ ] `FfmpegMissingDialog` exists, WPF-UI styled, with winget / download / re-detect / cancel actions
- [ ] `winget install -e --id Gyan.FFmpeg --accept-source-agreements --accept-package-agreements` runs from the modal with stdout/stderr streamed into a log textbox; detection re-runs on exit and the dialog dismisses on success
- [ ] Well-known winget install location is checked when PATH lookup fails (handles "installed but PATH not refreshed yet")
- [ ] `StreamTranscriptionService` shows the modal when FFmpeg is missing, retries the operation once on success, surfaces a clean "FFmpeg required" message on cancel
- [ ] `AudioFileDecoder.DecodeOgg` keeps the Concentus fallback ahead of any modal prompt
- [ ] winget-absent / install-denied edge cases surface specific, actionable error messages
- [ ] README distribution note mentions optional `winget install Gyan.FFmpeg` for YouTube/Stream features (handed off to Task 114)

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §2 "FFmpeg — user-installed, with first-use prompt" + Implications #4.
- Decision rationale (preserved in research): we sidestep LGPL by not redistributing FFmpeg at all. User installs under their own license — gyan.dev (GPL) is fine because it lives in the user's environment, not in our binary.
- The Concentus fallback for OGG was added earlier (see `AudioFileDecoder.DecodeOgg`). Don't accidentally regress it.
- If FFmpeg detection becomes a hot path later, consider caching the path in `bootstrap.json` (machine-local) so cold starts skip the shell-out. Not in scope for this task.

## Work Log
<!-- Appended by /work during execution -->
