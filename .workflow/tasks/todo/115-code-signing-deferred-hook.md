# Task: Code Signing — Deferred Hook (Wire-Up Now, Flip Post-UG)

**ID:** 115
**Milestone:** M5 - Public Release (GitHub Distribution)
**Size:** Small
**Created:** 2026-05-12
**Status:** Backlog
**Dependencies:** 111 (release workflow)

## Objective

Pre-wire the code-signing slot in the release workflow so flipping it on post-UG-registration is a one-line change, not a re-architecture. Leave explicit TODOs at every relevant spot.

## Details

### What's deferred

Code signing is blocked until the UG is registered (per `.workflow/research/auto-update-and-distribution.md` and `.workflow/research/legal-risks-commercial-release.md`):
- Microsoft Trusted Signing: EU individuals not supported as of 2026; orgs only.
- EV cert: requires registered business.
- OV cert: possible for individuals but needs 2–8 weeks of SmartScreen warming after each renewal.

So all M5 work ships unsigned. This task is the "make the future flip cheap" task.

### What this task does NOT do

- Purchase a certificate.
- Set up Azure Trusted Signing.
- Actually sign anything.

### What this task DOES

1. **Document the two signing paths** in `docs/release.md` (or wherever the release runbook lives — see Task 111):
   - **Traditional signtool** (PFX file + password): `--signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f cert.pfx /p $PASSWORD"`. Requires the PFX file to land in the runner securely (GitHub Actions secret holding base64 of the PFX, decoded at job start).
   - **Azure Trusted Signing** (after UG): `--azureTrustedSignFile signing.json` with the JSON config committed (no secrets in it; Azure auth comes from federated identity / OIDC).
2. **Stub the workflow input.** Add an optional `cert_source` workflow_dispatch input or env var that gates a signing step. Default empty → unsigned. When populated, the step pipes the right flag into `vpk pack`. (Inline TODO comment also acceptable if the workflow_dispatch path adds noise.)
3. **Inline TODO at the `vpk pack` step** in `.github/workflows/release.yml`:
   ```yaml
   # TODO post-UG: insert one of
   #   --signParams "/td sha256 /fd sha256 /tr http://timestamp.acs.microsoft.com /f cert.pfx /p $env:CERT_PASSWORD"
   #   --azureTrustedSignFile signing.json
   ```
4. **README note** (delegated to Task 112): once signed, the README's "unsigned developer" disclaimer changes. Add a TODO bullet in Task 112's content noting the post-sign edit.
5. **Add `.github/workflows/release.yml` signing-secret references** as `secrets.CERT_PFX_BASE64`, `secrets.CERT_PASSWORD` placeholders — commented out or in a `# TODO post-UG` block. Documenting the names ahead of time means whoever wires it up later (could be future-us, could be a contractor) has zero guesswork.
6. **Document the SmartScreen reputation impact** in `docs/release.md`: even after signing, OV certs take ~15 000 safe downloads to clear SmartScreen warnings; EV certs and Trusted Signing give instant trust. Reference the 2026 numbers from `installer-and-github-distribution.md` §5.

### Future task (out of scope here)

When the UG is registered, file a single concrete "Enable code signing for releases" task that:
- Acquires the chosen cert / identity.
- Flips the workflow flag.
- Updates the README disclaimer (Task 112's note).
- Tags a fresh release as the first signed build.
- Submits the signed binary to Defender / SmartScreen if needed.

## Acceptance Criteria

- [ ] `docs/release.md` (or equivalent) documents both signtool and Azure Trusted Signing paths with exact `vpk pack` flags
- [ ] Inline TODO comment at the `vpk pack` step of `.github/workflows/release.yml` showing exactly where signing flags go
- [ ] Signing-secret names (`CERT_PFX_BASE64`, `CERT_PASSWORD`, or chosen alternatives) documented so a future task knows what to add to GitHub Secrets
- [ ] README disclaimer "we ship unsigned, signing planned post-UG" remains accurate (Task 112)
- [ ] Note in docs about post-sign SmartScreen reputation requirements (OV warming period vs EV/Trusted Signing instant trust)
- [ ] No certs purchased, no signing actually happening — this task is documentation + plumbing only

## Notes

- Source: `.workflow/research/installer-and-github-distribution.md` (2026-05-12), §3 "Code signing hook for later" + Implications #9. Also `.workflow/research/auto-update-and-distribution.md` (2026-03-27).
- Reference: [Code Signing | Velopack](https://docs.velopack.io/packaging/signing).
- The point of this task is to never have to think about the signing architecture again. The next signing-related task should be a 5-line PR.

## Work Log
<!-- Appended by /work during execution -->
