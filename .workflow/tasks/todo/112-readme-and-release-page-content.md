# Task: Public README + GitHub Release Page Content

**ID:** 112
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Medium
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 111 (release workflow — provides the artifacts to document)

## Objective

Produce the public-facing content a first-time visitor needs to download, install, and use WhisperHeim from GitHub Releases. Cover: SmartScreen click-through, Smart App Control caveat, SHA-256 verification, optional `winget install Gyan.FFmpeg` step, hotkeys, and where the user's data lives.

## Details

### 1. README.md (top-level, replaces or augments any existing internal-only README)

Structure:

- **Hero**: one paragraph — "WhisperHeim is the audio Swiss army knife for Windows 11. Local dictation, call transcription with speaker separation, voice message transcription. No cloud, no subscription, no internet at runtime."
- **Screenshot / screen-recording** of dictation in action.
- **Download** — direct link to the latest Release's `Setup.exe`.
- **Install** — SmartScreen click-through (numbered steps + screenshot/recording).
  - Click "More info" → "Run anyway" if "Windows protected your PC" appears.
  - If "Smart App Control" blocks instead, we cannot override (no signing yet). State that signing is planned post-UG.
- **First run** — what to expect:
  - The setup window (Task 108) downloads ~640 MB Parakeet model from Hugging Face. One-time, then cached in `%APPDATA%\WhisperHeim\models`.
  - Tray icon appears; right-click for menu.
  - Default hotkeys (Ctrl+Win to dictate, etc.).
- **Optional: FFmpeg** — "For YouTube/Stream transcription, install FFmpeg: `winget install -e --id Gyan.FFmpeg`. WhisperHeim will prompt you the first time you need it."
- **Where is my data?** — `%APPDATA%\WhisperHeim` for settings/recordings/transcripts by default. Configurable via the General settings page. Uninstall does NOT delete this data.
- **System requirements** — Windows 11 (24H2 or later recommended), x64, ~2 GB RAM idle / ~3 GB during transcription, ~700 MB disk for the Parakeet model.
- **Privacy** — bullet list: all transcription is local, no telemetry, no cloud accounts.
- **License** — TBD (the licensing decision comes out of the monetization research; placeholder for now).
- **Status / disclaimer** — "Currently unsigned because Microsoft Trusted Signing isn't available to individual developers in Germany yet. Signing is planned post-UG registration. SHA-256 of every Setup.exe is published in the Release notes for verification."

### 2. GitHub Release notes template

Each release should include:
- **What changed** — bullet list (manual or auto-generated from commits).
- **Install** — link back to README install section + brief reminder of SmartScreen click-through.
- **Verification** — `SHA-256: <hash from Task 111 workflow output>`.
- **Known issues** — anything explicit.

Store this as `.github/release-template.md` so the workflow (or a manual step) can pre-fill it. Velopack's `vpk upload --releaseNotes` flag can consume this.

### 3. Screen recording

A 20–30 s `install.mp4` (or animated `.webp` / `.gif`) showing the click-through. Host at the repo root or in a `docs/media/` folder. Linked from the README install section.

### 4. SHA-256 surfacing

Task 111's workflow emits the Setup.exe SHA-256 as a step output. Either:
- Append it to the Release body automatically via `gh release edit` in the same workflow, or
- Include it manually in the Release notes from the workflow output.

Choose whichever is simpler at implementation time — the goal is "user can find the hash without grepping logs".

### 5. SmartScreen + SAC explainer

A short FAQ-style section (in README or a `docs/why-unsigned.md`):
- What is SmartScreen?
- Why is WhisperHeim flagged?
- How can you verify the download is safe? (SHA-256, source code, no telemetry.)
- What is Smart App Control and why might it block the install entirely?
- When will WhisperHeim be signed? (Post-UG; see roadmap.)

## Acceptance Criteria

- [ ] Top-level `README.md` covers: hero, screenshots, download, install (SmartScreen), first-run, optional FFmpeg, data location, requirements, privacy, license placeholder, unsigned disclaimer
- [ ] `install.mp4` (or `.webp`/`.gif`) recorded and linked from README
- [ ] `.github/release-template.md` exists with placeholders for changelog + SHA-256
- [ ] SHA-256 from Task 111 workflow shows up in Release body (automated or manual, documented in `docs/release.md`)
- [ ] `docs/why-unsigned.md` or equivalent section explains SmartScreen and SAC with clear next-steps for the user
- [ ] Optional FFmpeg `winget` line present in README install section
- [ ] User-tested: a friend / colleague who hasn't seen WhisperHeim can install it from the GitHub Release with no extra hand-holding

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §5 "SmartScreen + GitHub Releases UX" + Implications #6.
- Related research: `.workflow/research/macwhisper-growth-playbook.md` — the README and Release page are part of the launch surface; should at minimum read as well as MacWhisper's.
- Don't write "WhisperHeim respects your privacy" without backing it up — the data-location section, the no-telemetry claim, and the open-source nature of the dependencies are the proof.

## Work Log
<!-- Appended by /work during execution -->
