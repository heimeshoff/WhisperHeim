# Research: MacParakeet Feature Comparison — Gaps vs WhisperHeim

**Date:** 2026-04-20
**Status:** Complete
**Relevance:** Vision alignment across Milestones 1-3 (dictation UX, templates, transcription library). Sources ideas for the next planning round without committing to any.

## Summary

MacParakeet (moona3k/macparakeet, GPL-3.0, v0.5.5, April 2026) is the macOS sibling of what WhisperHeim is building for Windows: local Parakeet TDT on ANE via FluidAudio CoreML, tray-style dictation, file + YouTube transcription, meeting recording with dual capture. We already ship the three core pillars (live dictation, dual-capture call recording, YouTube/stream transcription) plus features MacParakeet doesn't have yet (TTS with voice cloning, Read Aloud).

The meaningful gaps are in **text post-processing** (deterministic clean pipeline — filler removal, custom words, in-dictation snippet expansion), **dictation history** (per-utterance log with search/playback, which unlocks private mode, stats, and favorites), **export depth** (SRT/VTT/DOCX/PDF/JSON with word-level timestamps), and the **LLM layer** (prompt library, multi-summary, transcript chat). Secondary gaps: soft-cancel undo, clipboard preservation, first-run onboarding, CLI tool, persistent-vs-push-to-talk hotkey modes.

Nothing here requires rethinking WhisperHeim's architecture. Most items are additive on top of the existing transcription + templates + LLM-analysis foundation.

## Key Findings

### Already in WhisperHeim (no action needed)

| MacParakeet feature | WhisperHeim equivalent |
|---|---|
| System-wide dictation hotkey | done (tasks 004, 009, 011) |
| Pill overlay with waveform | done (tasks 012, 068, 070) |
| File transcription drag-drop | done (tasks 020, 021) |
| YouTube URL transcription via yt-dlp | done (Streams tab, task 096) |
| Meeting recording with dual mic+loopback | done (tasks 015, 016, 018) |
| Speaker diarization + rename | done (tasks 017, 037) |
| Transcript viewer with audio playback | done (tasks 019, 038) |
| Transcription queue with bottom bar | done (task 075) |
| Local LLM transcript analysis (Ollama) | done (task 069) |
| Templates (hotkey-triggered) | done (task 013) — note: *different* from MacParakeet's in-dictation snippet expansion (see below) |
| Auto-update distribution | researched (auto-update-and-distribution.md — Velopack chosen) |
| Voice cloning / TTS | done (tasks 029-033) — MacParakeet does **not** have this |

### Gap 1: Deterministic Clean-Text Pipeline (spec/07)

MacParakeet runs a <1ms post-ASR pipeline on every dictation in four fixed steps:

1. **Filler removal** — three tiers:
   - Multi-word (unconditional): "you know", "I mean", "sort of", "kind of"
   - Single-word (unconditional): "um", "uh", "umm", "uhh"
   - Sentence-start-only (conditional): "so", "well", "like", "right" — only stripped at sentence boundaries
2. **Custom word replacements** — case-insensitive whole-word, database-ordered. Two use-cases: vocabulary anchors ("kubernetes" → "Kubernetes") and STT-error corrections ("aye pee eye" → "API"). Entries toggleable via `isEnabled` flag.
3. **Snippet expansion** — in-dictation trigger phrases ("my signature" → full signature block). Sorted by trigger-length descending to prevent prefix collisions. Tracks `useCount` for analytics. **Distinct from WhisperHeim templates**: these fire mid-dictation based on spoken phrases, not via a separate hotkey.
4. **Whitespace cleanup** — collapse spaces, trim, capitalize first char, fix spacing around punctuation.

User toggles a **Raw / Clean mode** in settings. Raw ships the ASR output untouched (useful for debugging).

WhisperHeim has this as research (`filler-words-and-custom-vocabulary.md`, 2026-03-31) but not implemented. The research already endorses regex filters for fillers and a replacement dictionary for custom vocab. Tier-1 gap for dictation UX.

### Gap 2: Dictation History (spec F4)

Per-utterance database of every dictation:

- Chronological list grouped by Today/Yesterday/date
- Per-entry: time, duration, transcript text, audio snippet, three-dot menu (download, delete)
- Bottom-bar Spotify-style audio player
- Substring case-insensitive search over transcripts
- Cmd+Backspace / Del to remove (with confirmation)
- Schema: `dictations(createdAt, durationMs, rawTranscript, cleanTranscript, audioPath, pastedToApp, processingMode, status, errorMessage, updatedAt)`

WhisperHeim has a transcript library for **recordings and file transcriptions**, but not a dedicated **dictation log** — every ad-hoc dictation is fire-and-forget. Adding this unlocks three dependent features:

- **Private Dictation Mode** (F17) — `hidden` column excludes sensitive dictations from list + stats
- **Voice Stats** (F18) — cached per-dictation word count surfaces daily/weekly usage
- **Favorites** (F21) — `isFavorite` column + filter

### Gap 3: Export Depth (spec F12)

MacParakeet exports TXT, **SRT, VTT, DOCX, PDF, JSON** — plus Markdown. WhisperHeim currently exports TXT/MD. The interesting ones:

- **SRT / VTT** — word-level timestamps let subtitles split at speaker boundaries
- **JSON** — preserves word-level timestamps + confidence scores + segments; enables downstream tooling
- **DOCX / PDF** — stakeholder-friendly deliverables for meeting transcripts

Parakeet provides word-level timestamps already; the blocker is plumbing them through the export layer. This is a feature multiplier for the Streams + meeting-recording pillars.

### Gap 4: LLM Layer — Prompt Library, Multi-Summary, Chat (spec/11, spec/12)

WhisperHeim has single-shot local-LLM transcript analysis. MacParakeet has a layered system:

- **Prompt Library** (F28) — reusable prompts stored in SQLite, with community/built-in prompts shipped hidden-but-used, custom prompts user-editable, per-card auto-run flag.
- **Multi-Summary** (F29) — multiple named summaries per transcript from different prompts, tabbed, sequential execution. Auto-run fires *every* auto-flagged prompt after transcription completes.
- **Transcript Chat** (F10c, F19) — scoped chat per transcript, **multiple named conversations** per transcript (separate table, FK + CASCADE), bounded-context assembly with oldest-turns-drop, streaming responses.
- **Providers**: Anthropic / OpenAI / Gemini / OpenRouter / Ollama / LM Studio, plus CLI subprocess (Claude Code, Codex) — keys in Keychain, never UserDefaults.
- **AI Formatter** (F8) — *optional* LLM polish *after* the deterministic pipeline (fallback to deterministic on provider error/timeout). Toggleable independently of Raw/Clean.

The research `transcript-analysis-with-llm.md` already endorses Ollama + Qwen 2.5 14B as WhisperHeim's default, so the provider layer is settled — the gap is the **prompt library + multi-summary + chat** UX on top.

### Gap 5: Dictation UX Polish (spec F1)

Small items that add up to a refined feel:

- **Soft cancel with 5-second undo** — Esc cancels, overlay shows countdown ring with Undo button, hotkey blocked during window. Prevents "oh no I didn't mean to send that" regret.
- **Clipboard preservation** — MacParakeet uses simulated Cmd+V, so it saves + restores the prior clipboard. WhisperHeim uses SendInput (keystroke simulation), which bypasses the clipboard — so this is **not directly applicable**, but: some apps (elevated processes, Citrix, RDP) reject SendInput. A clipboard-paste fallback with preservation would cover those cases.
- **Double-tap-to-latch vs hold-to-push-to-talk** — single press = latched dictation (press again to stop); hold >400ms = push-to-talk (releases stops); double-tap <400ms = persistent mode. MacParakeet's default trigger is bare `Fn`. WhisperHeim has single-press toggle; adding a hold-mode variant is ~1 day of work and covers chat / terminal / quick-reply flows better.
- **Overlay states** — success flash (green check, 500ms auto-dismiss), error toast ("Couldn't hear you — check mic", 3s), processing spinner. WhisperHeim's overlay handles recording states well; the post-recording feedback is sparser.
- **First-run onboarding** — permission checks (mic, accessibility), model download with retry, "Open Settings" fallback. WhisperHeim has model download (task 005) but no guided first-run flow.

### Gap 6: CLI Tool

MacParakeet ships a standalone CLI (`macparakeet-cli`) for batch transcription, LLM ops (`llm test-connection`, `llm summarize`, `llm chat`), and dev workflows. For power users on Windows, this enables:

- Scripted batch transcription of an archive folder
- CI / automation pipelines (e.g., transcribe every new recording dropped into a watched folder)
- Test harness for ASR regression

A C# console project sharing WhisperHeim.Core services is low-effort if the services are already separated from the WPF layer.

### Gap 7: Synced Playback Word-Highlighting (spec F25)

Active word/segment highlighted during audio playback (binary search on timestamp). Auto-scroll follows playhead. Click any timestamp to seek. Requires word-level timestamps in the transcript (which Parakeet provides).

WhisperHeim has playback; this is the next layer of polish for the transcript viewer and pairs naturally with the JSON export format above.

## Implications for This Project

**Priority ranking** (my read, happy to be wrong):

1. **Clean-text pipeline** (filler removal + custom words + whitespace + Raw/Clean toggle) — biggest per-use-case UX win, research already done, no dependencies.
2. **In-dictation snippet expansion** — complementary to existing template system; templates are explicit (hotkey + speak), snippets are implicit (speak trigger phrase mid-sentence). Consider whether both earn their keep or whether snippets replace templates.
3. **Dictation history + private mode + favorites + voice stats** — one cohesive chunk. Needed to differentiate casual dictation from transcripts. Depends on clean pipeline (stores both raw + clean).
4. **Export depth (SRT/VTT/JSON first, DOCX/PDF second)** — feature multiplier for Streams + Recordings pillars. Word-level timestamp plumbing is the real work.
5. **Prompt library + multi-summary + multi-conversation chat** — expands the LLM pillar from "analyze once" to "ongoing workspace per transcript". Launch-worthy feature if monetization research pans out.
6. **Dictation UX polish** (soft-cancel undo, push-to-talk mode, success/error flashes, first-run onboarding) — small individually, large together.
7. **Synced playback highlighting** — nice-to-have once word-level timestamps are already wired for export.
8. **CLI tool** — power-user + automation; low priority unless there's demand.

**What not to copy**: MacParakeet's "meeting note enrichment, calendar integration, entity extraction" non-goals align with WhisperHeim's. Their agent workflows (spec/13) are still a proposal on their side — no urgency to follow.

**Positioning angle**: WhisperHeim has TTS + voice cloning + Read Aloud; MacParakeet does not. The "full voice I/O stack, local, Windows" story is still differentiated — closing these gaps tightens dictation UX without diluting it.

## Open Questions

- Do in-dictation snippet expansions coexist with the existing template system, or replace it? User-testing question.
- For the dictation history log: should it auto-expire entries (privacy) or keep forever (stats)? MacParakeet keeps forever with a "Clear All" button.
- For the prompt library: ship a curated "community" set (MacParakeet's model) or start fully user-driven?
- SendInput clipboard-fallback: is there measurable demand (users running elevated apps / RDP), or premature?
- CLI tool: Windows users' appetite is lower than macOS/Linux — worth a small user survey before building.

## Sources

- Repo: https://github.com/moona3k/macparakeet
- README: https://raw.githubusercontent.com/moona3k/macparakeet/main/README.md
- Features spec: https://raw.githubusercontent.com/moona3k/macparakeet/main/spec/02-features.md
- Text processing spec: https://raw.githubusercontent.com/moona3k/macparakeet/main/spec/07-text-processing.md
- LLM integration spec: https://raw.githubusercontent.com/moona3k/macparakeet/main/spec/11-llm-integration.md
- Agent workflows proposal: https://raw.githubusercontent.com/moona3k/macparakeet/main/spec/13-agent-workflows.md
- Docs index: https://github.com/moona3k/macparakeet/tree/main/docs
- Spec index: https://github.com/moona3k/macparakeet/tree/main/spec
- Related WhisperHeim research: `filler-words-and-custom-vocabulary.md`, `transcript-analysis-with-llm.md`, `auto-update-and-distribution.md`, `macwhisper-growth-playbook.md`
