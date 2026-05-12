# Task: Bundle Silero VAD + Pyannote Seg in the Publish Output

**ID:** 109
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Small
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** None (independent of 107/108 but most useful in combination)

## Objective

Stop downloading the two small models (Silero VAD ~2 MB, Pyannote Segmentation 3.0 ~1.5 MB) on first run by shipping them inside the publish output. The app should be usable for recording immediately on install with no network call required — only the large Parakeet model needs the first-run dialog.

## Details

### 1. Vendor the model files

Either:
- Commit the two model files into `src/WhisperHeim/Assets/Models/` (~3.5 MB total — small enough for the repo), or
- Add a `pre-publish` MSBuild target that downloads them once from `ModelManagerService`'s known URLs, caches them under `~/.nuget`-style local cache, and copies them into `publish/`.

Recommendation: vendor them in the repo. They never change, are tiny, and committing them removes a build-time network dependency that would otherwise have to be reproduced in the CI workflow (Task 113).

### 2. csproj wiring

```xml
<ItemGroup>
  <None Include="Assets\Models\silero-vad\silero_vad.onnx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\silero-vad\silero_vad.onnx</Link>
  </None>
  <None Include="Assets\Models\pyannote-segmentation-3.0\segmentation-3.0.int8.onnx">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <Link>models\pyannote-segmentation-3.0\segmentation-3.0.int8.onnx</Link>
  </None>
</ItemGroup>
```

Result: at runtime, `{AppDir}\models\silero-vad\` and `{AppDir}\models\pyannote-segmentation-3.0\` are populated alongside `WhisperHeim.exe`.

### 3. `ModelManagerService` lookup order

Change the model-locating logic to prefer the bundled location (next to the EXE) and fall back to the per-user models folder (`%APPDATA%\WhisperHeim\models`) — never the other way around. For Parakeet specifically, the bundled location won't have anything; the per-user folder is authoritative.

```csharp
string ResolveModelPath(ModelDefinition def, string fileName)
{
    var appDirCandidate = Path.Combine(AppContext.BaseDirectory, "models", def.SubDirectory, fileName);
    if (File.Exists(appDirCandidate)) return appDirCandidate;
    return Path.Combine(ModelsRoot, def.SubDirectory, fileName);
}
```

### 4. Don't list bundled models in the first-run dialog

After this lands, `GetMissingRequiredModels()` (cf. Task 108) should see Silero VAD + Pyannote already resolved and not prompt the user about them.

### 5. License attribution

Both models have permissive licenses (MIT for Silero VAD, MIT for sherpa-onnx's Pyannote conversion). Add brief attribution to the About page or a `LICENSES/` folder shipped alongside the EXE.

## Acceptance Criteria

- [ ] `Assets/Models/silero-vad/silero_vad.onnx` and `Assets/Models/pyannote-segmentation-3.0/segmentation-3.0.int8.onnx` are present in the repo (or, if downloaded at build time, the build target is documented)
- [ ] `dotnet publish` produces `publish/models/silero-vad/silero_vad.onnx` and `publish/models/pyannote-segmentation-3.0/segmentation-3.0.int8.onnx`
- [ ] `ModelManagerService` resolves bundled models before per-user models
- [ ] Fresh install (delete `%APPDATA%\WhisperHeim\models`, launch packed build) can record + diarize without any network call — only Parakeet triggers a download
- [ ] License/attribution for both models is reachable from the About page or a shipped `LICENSES/` folder
- [ ] Tests still pass; no regression in existing dictation / call recording paths

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §1 "Bundling models" + Implications #3.
- The big model stays unbundled — see Task 108 for its UX.
- The bundled-first lookup is also what enables Task 111's GitHub Actions workflow to skip any model-download step entirely.

## Work Log
<!-- Appended by /work during execution -->
