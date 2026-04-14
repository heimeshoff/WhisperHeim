---
name: deploy
description: Kill running WhisperHeim, rebuild, and launch. Use when the user says "deploy", "redeploy", "restart the app", or wants to test changes in the running application.
---

# Deploy WhisperHeim

Kill any running instance, publish a self-contained Release build, and launch the new exe.
NOTE: Do NOT use `-p:PublishSingleFile=true` — WPF apps crash on startup with DllNotFoundException when published as single file.

Run these three commands sequentially via Bash:

```bash
taskkill //IM WhisperHeim.exe //F 2>&1 || true
```

```bash
dotnet publish src/WhisperHeim/WhisperHeim.csproj -c Release -r win-x64 --self-contained -o publish -v q 2>&1 | tail -5
```

```bash
start "" "C:/src/heimeshoff/tooling/WhisperHeim/publish/WhisperHeim.exe"
```

If the publish fails, report the error. Do not launch the exe if the build failed.
