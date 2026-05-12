# Task: Velopack End-to-End Dry Run (Sanity Check Before 1.0.0)

**ID:** 114
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Small
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 107 (Velopack bootstrap), 109 (bundled small models), 110 (FFmpeg detection), 111 (release workflow)

## Objective

Before tagging the first public release, do a full end-to-end Velopack dry run on a real Windows 11 machine: build, pack, install, verify, update, verify again. Catch any packaging issues — install-dir flash, tray icon registration, model paths, FFmpeg prompt timing, missing DLLs — while there's no public Release to be embarrassed by.

## Details

### Dry run protocol

Use a clean(ish) Windows 11 user profile (or a fresh VM) to avoid local-dev leakage.

1. **Build a v0.0.1-test publish** locally:
   ```pwsh
   dotnet publish src/WhisperHeim/WhisperHeim.csproj -c Release -r win-x64 --self-contained -p:PublishReadyToRun=true -o publish
   vpk pack --packId WhisperHeim --packVersion 0.0.1-test --packDir publish --mainExe WhisperHeim.exe --packTitle WhisperHeim --packAuthors "Marco Heimeshoff"
   ```
2. Run `Releases\WhisperHeim-Setup.exe`. Watch for:
   - Visible Setup UI vs. silent install behaviour.
   - SmartScreen prompt — confirm "More info" → "Run anyway" still works on this Windows version.
   - Any window flash from WhisperHeim during install (Task 106 regression check).
   - Tray icon registers correctly (recent fix in `71d5a2f`).
3. First launch — verify:
   - First-run model download dialog appears (Task 108).
   - Or, if models are already cached in `%APPDATA%`, the app drops straight to tray.
   - FFmpeg detection runs without UI noise (Task 110 — modal should NOT appear until user invokes a feature that needs it).
4. Use the app — record a short session, transcribe, confirm transcript appears.
5. **Build a v0.0.2-test** with a trivial code change.
6. Run `vpk pack --packVersion 0.0.2-test`. Confirm delta package is produced.
7. From the installed app, trigger an update check (or copy the new `Releases\` to a known feed location and call `UpdateManager.CheckForUpdates`). Verify:
   - Delta is downloaded, not full.
   - Update applies, app restarts.
   - User data (`%APPDATA%\WhisperHeim\`) is preserved across the update.
   - Tray icon still works after restart.
8. **Uninstall** via Add/Remove Programs. Verify:
   - Install dir (`%LocalAppData%\WhisperHeim\`) is gone.
   - User data in `%APPDATA%\WhisperHeim\` is preserved (Task 113).
   - No orphan tray icon, no orphan registry keys (check `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\WhisperHeim`).

### Failure handling

Any failure or weird behaviour during the dry run gets filed as a new task or a regression note on the relevant M5 task. This task is a checklist + scratchpad, not a substantive implementation.

### Smart App Control note

If the test machine has Smart App Control on, the install may hard-block with no override. Document the exact dialog and behaviour in the work log so Task 112's README content reflects reality.

## Acceptance Criteria

- [ ] Local `vpk pack` + `Setup.exe` install on a clean(ish) Win11 profile succeeds without errors
- [ ] First-run model download dialog (Task 108) appears as expected
- [ ] No window flash on start-minimized (Task 106 regression check)
- [ ] Tray icon registers correctly after install (regression check on `71d5a2f` fix)
- [ ] FFmpeg detection runs silently at startup; modal only appears on FFmpeg-dependent feature invocation
- [ ] Delta update from v0.0.1-test → v0.0.2-test downloads a delta package (not full), applies cleanly, preserves user data
- [ ] Uninstall removes install dir; preserves `%APPDATA%\WhisperHeim\`; no orphan tray / registry artefacts
- [ ] Smart App Control behaviour documented (whether the test machine has SAC on or off) — fed back to Task 112's README content
- [ ] Any issues found are filed as new tasks or notes on existing M5 tasks before tagging the first public release

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), Open Questions §5 "Velopack with our exact ~110 MB publish output" + Implications #8.
- This is one of those tasks that feels like overhead but pays for itself the first time a public release would have shipped broken.
- After this passes, the next tag should be `v0.1.0` (or whatever the first public version is), not another `-test` version.

## Work Log
<!-- Appended by /work during execution -->
