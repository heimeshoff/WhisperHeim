# Releasing WhisperHeim

WhisperHeim ships via [Velopack](https://docs.velopack.io) and GitHub Releases.
A push of a `v*` tag triggers `.github/workflows/release.yml`, which publishes
the app, packs it with `vpk`, and uploads everything to a GitHub Release named
after the tag.

## What gets uploaded

`vpk upload github` attaches the contents of `Releases/` to the Release:

- `WhisperHeim-{version}-win-Setup.exe` — single-file installer (primary user download)
- `WhisperHeim-{version}-full.nupkg` — full Velopack package
- `WhisperHeim-{version}-win-Delta.nupkg` — delta against the previous release (skipped on the first ever release)
- `RELEASES` — Velopack manifest consumed by in-app auto-update

The job summary also surfaces the **SHA-256 of `Setup.exe`** so it can be pasted
into the Release notes for users who want to verify the download.

## Local-iteration recipe (dry-run before tagging)

Reproduce the exact pipeline locally to smoke-test before pushing a tag:

```pwsh
# 1. Publish self-contained win-x64, ReadyToRun, no trimming
dotnet publish src/WhisperHeim/WhisperHeim.csproj `
  -c Release -r win-x64 --self-contained `
  -p:PublishReadyToRun=true `
  -o publish

# 2. Install vpk (one-time; pinned to the version the CI uses)
dotnet tool install -g vpk --version 0.0.1589

# 3. Pack — produces Releases/*-Setup.exe, *-full.nupkg, RELEASES
vpk pack `
  --packId WhisperHeim `
  --packVersion 0.0.1-local `
  --packDir publish `
  --mainExe WhisperHeim.exe `
  --packTitle "WhisperHeim" `
  --packAuthors "Marco Heimeshoff"

# 4. Manually run the installer to verify it boots into the tray
.\Releases\WhisperHeim-0.0.1-local-win-Setup.exe
```

Delta packages require a previous release. To test delta packing locally, run
`vpk download github --repoUrl https://github.com/<owner>/<repo> --token <PAT>`
before `vpk pack`. On the very first release this is a no-op (and the CI step
is marked `continue-on-error` for that reason).

## Triggering a release

1. Bump the version, commit, push.
2. Tag and push:

   ```pwsh
   git tag v0.0.1
   git push origin v0.0.1
   ```

3. Watch the `Release` workflow run under **Actions**. On success, the Release
   appears at `https://github.com/<owner>/<repo>/releases/tag/v0.0.1` with
   `Setup.exe`, the nupkgs, and `RELEASES` attached.

4. Edit the Release notes to include:
   - SmartScreen click-through instructions (More info → Run anyway)
   - The SHA-256 from the workflow summary
   - The Smart App Control caveat (no override; signing arrives post-UG-registration)

## Signing

Code signing is **deferred** to Task 115. The `vpk pack` step in `release.yml`
has an inline TODO at the spot where `--signParams` or `--azureTrustedSignFile`
will land. No structural change to the workflow is required to flip signing on
once a signing identity exists; `vpk` does incremental signing of both the app
binaries and Velopack's own `Update.exe` / `Setup.exe` in the correct order, so
no post-build `signtool` step is needed.

## Related tasks

- **Task 107** — Velopack bootstrap (custom `Main`, `App.xaml` as `Page`, etc.)
- **Task 109** — Small models bundled in the publish output, so CI doesn't need to fetch them
- **Task 110** — FFmpeg is user-installed; the workflow has no FFmpeg step
- **Task 114** — End-to-end pack dry run + first real tag (manual)
- **Task 115** — Code signing slot (documentation-only; references the TODO in this workflow)
