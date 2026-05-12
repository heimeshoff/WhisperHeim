# Task: Uninstall Data Preservation (Hygiene + Documentation)

**ID:** 113
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Small
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 107 (Velopack bootstrap)

## Objective

Ensure that uninstalling WhisperHeim via Add/Remove Programs (or Velopack's uninstaller) never deletes user data: recordings, transcripts, settings, downloaded models. Make the uninstall behaviour explicit to the user so nobody loses 50 hours of call transcripts by accident.

## Details

### 1. Audit data locations

Velopack removes the install directory (`%LocalAppData%\WhisperHeim\`) on uninstall but preserves `%AppData%\WhisperHeim\` (Roaming) and any directory the user configured outside the install dir.

Verify every user-data location is OUTSIDE `%LocalAppData%\WhisperHeim\`. Grep checklist (run before merging):

- `Environment.SpecialFolder.LocalApplicationData` — must not appear in any path that stores user data (settings, recordings, transcripts, models, bootstrap.json).
- `Environment.SpecialFolder.ApplicationData` (Roaming) — expected for default data paths. Confirm `ModelManagerService.ModelsRoot`, `DataPathService` defaults, `BootstrapConfig` location.
- `AppContext.BaseDirectory` (install dir) — expected only for bundled models, FFmpeg if ever bundled (not now), the EXE, and DLLs. No user data here.
- The user's configurable `DataPath` — always outside the install dir; the config UI should refuse install-dir paths defensively (one-line validator).

Document the audit results in the work log so a future reviewer can re-verify.

### 2. Add/Remove Programs metadata

Set Velopack pack flags so the entry in Apps & Features reads clearly:

```
--packTitle "WhisperHeim"
--packAuthors "Marco Heimeshoff"
```

(Already in Task 111's workflow.) Additionally, consider adding a `--releaseNotesUrl` pointing at the Releases page so users can find changelog from Add/Remove Programs.

### 3. Optional pre-uninstall hook

Velopack supports an `--veloapp-obsoleted` / pre-uninstall hook (see [App Hooks | Velopack](https://docs.velopack.io/integrating/hooks)). On uninstall, write a small `WhisperHeim-data-location.txt` to the user's desktop with:

```
Thanks for trying WhisperHeim.

Your recordings, transcripts, and settings have NOT been deleted.
They live in:
  %APPDATA%\WhisperHeim\
  <and / or> {configured DataPath}

Delete those folders manually for a fully clean removal.

If WhisperHeim served you well, a star on https://github.com/.../WhisperHeim
would make our day.
```

Keep it short, single file, no UI. Hooks have a 15 s timeout and forbid UI.

### 4. README + Apps & Features description

Add to README (handed off from Task 112): "Uninstall does NOT delete your data. To remove everything, also delete `%APPDATA%\WhisperHeim\` and any configured DataPath."

### 5. Defensive: prevent DataPath = install dir

In the General page's DataPath chooser, validate that the chosen path isn't under `AppContext.BaseDirectory` or `%LocalAppData%\WhisperHeim`. Show an error if the user tries. (Easy mistake to make if someone clicks the install dir; we should not let them shoot themselves in the foot.)

## Acceptance Criteria

- [ ] Audit of all user-data write paths complete; documented in work log
- [ ] No code writes user data to `%LocalAppData%\WhisperHeim\` or `AppContext.BaseDirectory`
- [ ] DataPath picker rejects paths under the install dir / `%LocalAppData%\WhisperHeim\` with a clear error
- [ ] Optional: pre-uninstall hook drops `WhisperHeim-data-location.txt` on desktop pointing at preserved data; if implemented, hook is silent and runs under 15 s
- [ ] Velopack pack flags set `packTitle` and `packAuthors` (verified in Task 111 workflow)
- [ ] README contains a "Uninstall preserves your data" note (delegated to Task 112; just verify the cross-link is correct)
- [ ] Manual test: install via `Setup.exe`, record a session, uninstall via Add/Remove Programs, confirm `%APPDATA%\WhisperHeim\recordings\` is still on disk
- [ ] Manual test: install, change DataPath to a custom folder with content, uninstall, confirm the custom folder is untouched

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §6 "Uninstall hygiene" + Implications #7.
- This task is small but high-stakes — losing user data on uninstall is the single worst possible bug for a transcription app.
- The desktop-note is optional. If it feels presumptuous, drop it and just rely on the README. The audit and the install-dir guard are the hard requirements.

## Work Log
<!-- Appended by /work during execution -->
