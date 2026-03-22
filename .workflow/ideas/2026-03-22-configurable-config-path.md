# Idea: Configurable Config/Data Path for Cloud Sync

**Captured:** 2026-03-22
**Source:** User input
**Status:** Raw
**Last Refined:** --

## Description
The application's configuration and data should be stored in a configurable path. Only a small bootstrap config file stays in the fixed default location (e.g., AppData), pointing to the actual config/data folder. By default, the actual config lives next to the bootstrap file, but the user can change this path in the Settings page to point to a cloud-synced folder (Google Drive, OneDrive, Dropbox, etc.), enabling cross-machine sync.

## Initial Thoughts
- This is a two-tier config pattern: a "pointer" config at the fixed location, and the real config/data at the configurable location
- Settings page needs a folder picker to change the path
- Need to handle: what happens if the target folder is unavailable (offline, unmounted)?
- Transcripts, TTS voices, templates, and other user data would likely also live in the configurable path
- Migration from current location to new path needs consideration

## Open Questions
- Which data files should move to the configurable path vs. stay local (e.g., window position probably stays local)?
- How to handle conflicts if the same app runs on two machines pointing to the same synced folder?
- Should the app validate the target path is writable before accepting it?

## Refinement Log
