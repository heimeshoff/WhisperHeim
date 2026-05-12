# Task: Fix vpk Version Pin in Release Workflow (0.0.1589 unavailable)

**ID:** 116
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** XS
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 111 (release workflow), 114 (dry-run that surfaced this)

## Objective

`.github/workflows/release.yml` step "Install vpk" pins `--version 0.0.1589`, but that version does not exist on the NuGet feed. As of 2026-05-12 the latest published `vpk` on `https://api.nuget.org/v3/index.json` is **0.0.1298**. Pushing a `v*` tag today would fail the workflow at the install step with: `Version 0.0.1589 of package vpk is not found in NuGet feeds`.

The `0.0.1589` number came from the M5 research file (`installer-and-github-distribution.md`, section 3 + Sources [21]), which cited the version as latest 2026-04-14 from `nuget.org/packages/velopack`. That reference is either wrong, was for a different package id (`Velopack` the library vs. `vpk` the CLI tool), or for a pre-release that has since been delisted. Either way: the workflow as committed will not run.

## Details

### What to change

`.github/workflows/release.yml` line 50, currently:

```yaml
run: dotnet tool install -g vpk --version 0.0.1589
```

Change to one of (pick one):

1. **Pin to a real, verified version.** Recommended:
   ```yaml
   run: dotnet tool install -g vpk --version 0.0.1298
   ```
   Cross-check first: `dotnet tool search vpk --take 5`. Verified locally on 2026-05-12 that 0.0.1298 packs cleanly against the current `src/WhisperHeim/WhisperHeim.csproj` publish output (see Task 114 work log).

2. **Pin to latest at workflow run time** (less reproducible, but reliable):
   ```yaml
   run: dotnet tool install -g vpk
   ```
   Drops the version pin entirely. Acceptable since this is a single-developer release pipeline and a `vpk` minor-version regression would surface in the dry run, not silently corrupt a release.

Option 1 is preferred for reproducibility — match the version that the local dry run verified.

### Cross-checks

- Look for any other reference to `0.0.1589` in the repo: scripts, docs, README, ADRs.
- Update `.workflow/research/installer-and-github-distribution.md` Source [21] if a future Task 114 re-run regenerates it; otherwise leave the research file as-is (it is a snapshot).

### Smoke test

After the change, push a test tag (e.g. `v0.0.1-test`) to a fork or a private branch to confirm the workflow completes the `Install vpk` step. Or run `act` locally if installed. Or just push a real first tag once Task 112 (README content) is also done and rely on the workflow log.

## Acceptance Criteria

- [ ] `release.yml` `vpk` install step uses a version that exists on nuget.org and packs successfully
- [ ] A tag push (real or to a fork) runs the workflow at least through the `Install vpk` and `Pack` steps without failure
- [ ] Any other lingering `0.0.1589` references in the repo are reconciled (or annotated as historical research notes)

## Notes

- Surfaced by Task 114 local dry-run on 2026-05-12.
- This is a one-line fix but worth its own task so the orchestrator does not silently embed it in another change.
