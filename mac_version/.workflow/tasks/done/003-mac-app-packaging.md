# Task 003: Package as macOS .app Bundle via py2app

**Status:** Done
**Size:** Medium
**Created:** 2026-04-19
**Milestone:** MVP

## Description

Package WhisperHeim Mac as a proper macOS .app bundle using py2app so the user can double-click to run it. No terminal commands needed after the initial build. Include a build script and instructions for building the .app on a Mac.

## Subtasks

- [x] Add setup.py with py2app configuration
- [x] Add py2app to requirements.txt (as dev dependency or separate)
- [x] Create app icon (simple placeholder .icns file or convert from Windows version)
- [x] Add build script (build.sh) that creates the .app bundle
- [x] Handle model download on first launch within the .app context
- [x] Ensure Accessibility permission prompts work from the .app
- [x] Add Info.plist customizations (LSUIElement=true for menu bar app, no dock icon)
- [x] Update README with build instructions
- [x] Test that the built .app works standalone (no Python installation needed by end user)

## Acceptance Criteria

- Running `./build.sh` on a Mac produces `dist/WhisperHeim.app`
- Double-clicking WhisperHeim.app launches the menu bar app
- App does not show in the Dock (LSUIElement=true, menu bar only)
- Models auto-download on first launch
- All dictation and template features work from the .app bundle
- README explains the build process clearly

## Dependencies

- Task 001 (dictation core)
- Task 002 (template system)

## Technical Notes

- py2app creates standalone .app bundles from Python scripts
- LSUIElement=true in Info.plist hides from Dock (menu bar apps)
- Model files should download to ~/Library/Application Support/WhisperHeim/models/ (not inside the .app bundle)
- The .app needs to include sherpa-onnx native libraries
- User still needs to grant Accessibility permissions on first run

## Work Log

### 2026-04-19 — Implementation complete

**Files created:**
- `setup.py` — py2app configuration with LSUIElement=true, microphone/accessibility usage descriptions, sherpa-onnx includes, package bundling
- `build.sh` — Build script that creates venv, installs deps + py2app, generates placeholder icon, locates sherpa-onnx native libs, runs py2app, produces `dist/WhisperHeim.app`
- `scripts/generate_icon.py` — Generates a placeholder .icns icon using either macOS `iconutil` or a pure-Python fallback
- `resources/` — Directory for the generated .icns icon file

**Files modified:**
- `main.py` — Added `_is_app_bundle()` detection, `-psn` argv filtering for .app launches, file-based logging when running inside .app bundle (logs to `~/Library/Application Support/WhisperHeim/whisperheim.log`)
- `requirements.txt` — Added comment noting py2app as build dependency (installed by build.sh, not a runtime requirement)
- `README.md` — Added "Quick Start — Build the App" section with build.sh usage, moved existing setup to "Development Setup" section

**Key decisions:**
- py2app installed by build.sh (not in requirements.txt) since it's a build-time-only dependency
- Models download at runtime to ~/Library/Application Support/, not bundled in .app (keeps bundle small)
- Placeholder icon generated at build time via Python script (no binary .icns committed to repo)
- Logging redirects to file when running as .app since there's no terminal
- setup.py excludes tkinter, unittest, etc. to keep bundle size down
