---
name: deploy
description: Kill running WhisperHeim, rebuild, and launch. Use when the user says "deploy", "redeploy", "restart the app", or wants to test changes in the running application.
---

# Deploy WhisperHeim

Invoke the `/deploy` slash command. It runs `deploy.cmd` (→ `scripts/publish.ps1`), which kills any running WhisperHeim.exe, publishes a self-contained Release build to `publish/`, and launches the new exe.

Do not run the publish commands manually — the script encodes the correct flags (notably: no `PublishSingleFile`, which would make this WPF app crash on startup).
