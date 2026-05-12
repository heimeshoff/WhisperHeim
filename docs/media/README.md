# docs/media

Screen recordings and screenshots linked from the top-level `README.md` and
`docs/why-unsigned.md`.

## Pending artifacts

- **`install.mp4`** (or `.webp` / `.gif`) — 20-30 s recording of the
  SmartScreen click-through on a fresh Windows 11 install:
  1. Download `Setup.exe` from a Release page.
  2. Double-click; SmartScreen "Windows protected your PC" appears.
  3. Click **More info** → **Run anyway**.
  4. Velopack setup runs, app launches to the tray.
  Record at 1080p, mute audio, trim aggressively. Keep under ~3 MB so the
  README image preview works without LFS.

- **Hero screenshot / dictation screen-recording** — short clip of live
  dictation typing into a target app via the Ctrl+Win hotkey. Same size
  budget.

Once recorded, drop the file here and remove the matching `<!-- TODO -->`
comments in `README.md`. No code changes needed; the README link paths already
point at this folder.
