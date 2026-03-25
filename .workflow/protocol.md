# Protocol

---

## 2026-03-25 23:15 -- Task Completed: 083 - Unify Recordings & File Transcription

**Type:** Task Completion
**Task:** 083 - Unify Recordings & File Transcription
**Summary:** Added Start/Stop Recording and Browse buttons to the Recordings page, implemented file import with move/copy-to-session-dir, produces CallTranscript-compatible JSON, supports re-transcription with diarization. Removed TranscribeFilesPage and Transcriptions nav item. Build passes, 32 tests pass.
**Files changed:** 8 files

---

## 2026-03-25 23:06 -- Task Started: 083 - Unify Recordings & File Transcription

**Type:** Task Start
**Task:** 083 - Unify Recordings & File Transcription
**Milestone:** M3 (Voice Message Transcription)

---

## 2026-03-25 23:05 -- Task Completed: 084 - Sidebar Collapse Icon & Branding Reshuffle

**Type:** Task Completion
**Task:** 084 - Sidebar Collapse Icon & Branding Reshuffle
**Summary:** Replaced sidebar collapse button with a chevron on the right edge, moved About to bottom of nav, replaced Dictation header with About-style branding, unified subtitle text.
**Files changed:** 4 files

---

## 2026-03-25 23:04 -- Task Completed: 082 - Fix Date Column & Sorting

**Type:** Task Completion
**Task:** 082 - Fix Date Column & Sorting
**Summary:** Fixed date parsing for new-format session directories and added clickable column headers with within-group sorting and sort direction indicators.
**Files changed:** 2 files

---

## 2026-03-25 23:00 -- Batch Started: [082, 084]

**Type:** Batch Start
**Tasks:** 082 - Fix Date Column & Sorting, 084 - Sidebar Collapse Icon & Branding Reshuffle
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 22:45 -- Idea Captured: Sidebar Collapse Icon & Branding Reshuffle

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/084-sidebar-collapse-icon-and-branding-reshuffle.md
**Summary:** Replace sidebar collapse button with a chevron on the right edge, move About to bottom of nav, replace Dictation page header with About-style branding (logo + title + version), unify subtitle text across both pages using the Dictation page's version.

---

## 2026-03-25 22:30 -- Idea Captured: Unify Recordings & File Transcription

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/083-unify-recordings-and-file-transcription.md
**Summary:** Merge the Transcriptions page into the Recordings page. Add Start/Stop Recording and Browse buttons. Imported files get moved into recordings/ and transcribed without diarization by default. Re-transcribe in drawer triggers diarization when multiple speakers defined. Remove TranscribeFilesPage and dead code afterward.

---

## 2026-03-25 21:00 -- Idea Captured: Fix Date Column & Add Column Sorting

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/082-fix-date-column-and-sorting.md
**Summary:** Fix the recordings date column showing "transcript" instead of actual dates (caused by new session-directory format not being parsed), and add click-to-sort on column headers with toggle asc/desc within groups.

---

## 2026-03-25 19:15 -- Task Completed: 081 - Fix Library Voices Combo Box

**Type:** Task Completion
**Task:** 081 - Fix Library Voices Combo Box
**Summary:** Replaced hardcoded CustomVoicesDir with DataPathService.VoicesPath and fixed LibraryVoice_Click ID comparison to use "custom:{name}" format.
**Files changed:** 4 files

---

## 2026-03-25 19:15 -- Task Completed: 080 - Drawer No Overlay Crossfade

**Type:** Task Completion
**Task:** 080 - Drawer No Overlay Crossfade
**Summary:** Removed dark overlay from drawer, added crossfade animation when switching recordings while drawer is open, close only via close button or Escape.
**Files changed:** 3 files

---

## 2026-03-25 19:00 -- Batch Started: [080, 081]

**Type:** Batch Start
**Tasks:** 080 - Drawer No Overlay Crossfade, 081 - Fix Library Voices Combo Box
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 18:45 -- Idea Captured: Fix Library Voices Not Showing in TTS Combo Box

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/081-fix-library-voices-combo-box.md
**Summary:** Path mismatch bug -- custom data path causes TTS service to scan a different voices directory than where the page saves/lists cloned voices. Secondary bug: library voice card click uses wrong ID format for combo box lookup.

---

## 2026-03-25 18:30 -- Idea Captured: Drawer -- Remove Overlay, Crossfade Between Recordings

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/080-drawer-no-overlay-crossfade.md
**Summary:** Remove the dark overlay behind the detail drawer (drop shadow is sufficient), enable clicking other recordings to crossfade drawer content in-place, close only via close button or Escape key.

---

## 2026-03-25 16:45 -- Task Completed: 079 - Fix Speaker Assignment UI

**Type:** Task Completion
**Task:** 079 - Fix Speaker Assignment UI
**Summary:** Fixed ComboBox click event bubbling to audio playback handler. Added per-segment speaker reassignment with "Apply to all" bulk update prompt. Speaker name header editing propagates renames to all matching segments.
**Files changed:** 2 files

---

## 2026-03-25 16:30 -- Task Completed: 078 - Fix Temporal Ordering (clock drift)

**Type:** Task Completion
**Task:** 078 - Fix Temporal Ordering -- Clock Drift Correction
**Summary:** Linear clock drift correction scales loopback segment timestamps by micDuration/loopbackDuration before merging, fixing out-of-order segments from WASAPI hardware clock divergence. Drift logged for diagnostics.
**Files changed:** 1 file

---

## 2026-03-25 16:30 -- Task Completed: 076 - Active Recording Card + Auto-Transcribe

**Type:** Task Completion
**Task:** 076 - Active Recording Card + Auto-Transcribe on Stop
**Summary:** Active recording card at top of Transcripts page with pulsing red indicator, live duration counter, and drawer for editing title/speaker count/speaker names. Auto-enqueues into TranscriptionQueueService on recording stop with pre-filled metadata.
**Files changed:** 3 files

---

## 2026-03-25 16:10 -- Batch Started: [076, 078]

**Type:** Batch Start
**Tasks:** 076 - Active Recording Card + Auto-Transcribe, 078 - Fix Temporal Ordering (clock drift)
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 16:00 -- Task Completed: 077 - Fix Diarization (VAD mic + constrained loopback)

**Type:** Task Completion
**Task:** 077 - Fix Diarization -- VAD-Only Mic + Constrained Loopback
**Summary:** VAD-only mic stream processing replaces fixed 120s chunks. Loopback diarization constrained with NumClusters from speaker count. Threshold raised to 0.80. Cross-chunk speaker ID consistency for group calls. Out-of-process worker updated.
**Files changed:** 5 files

---

## 2026-03-25 16:00 -- Task Completed: 075 - Transcription Queue Service + Bottom Bar UI

**Type:** Task Completion
**Task:** 075 - Transcription Queue Service + Bottom Bar UI
**Summary:** Replaced modal TranscriptionProgressDialog and TranscriptionBusyService with FIFO TranscriptionQueueService and persistent TranscriptionBottomBar. Sequential background processing with per-item stage tracking, cancel/remove/retry, collapsible bar across all pages.
**Files changed:** 9 files

---

## 2026-03-25 15:30 -- Batch Started: [075, 077]

**Type:** Batch Start
**Tasks:** 075 - Transcription Queue Service + Bottom Bar UI, 077 - Fix Diarization (VAD mic + constrained loopback)
**Mode:** Parallel (batch of 2)

---

## 2026-03-25 15:00 -- Idea Captured: Transcription Engine Overhaul

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/075 through 079
**Summary:** Overhaul the transcription engine into 5 tasks: (075) transcription queue with bottom bar UI, (076) active recording card with auto-transcribe on stop, (077) fix diarization with VAD-only mic + constrained loopback, (078) fix temporal ordering via clock drift correction, (079) fix speaker assignment UI combo box bug + per-segment reassignment. All filed to todo, all under M2.

---

## 2026-03-25 14:00 -- Research: Transcription Engine Overhaul

**Type:** Research
**Topic:** Diarization accuracy, dual-stream merging, long-recording stability, and transcription queue architecture
**File:** research/transcription-engine-overhaul.md
**Key findings:**
- For dual-stream recordings, replace full diarization with VAD-only per stream (mic = "You", loopback = "Remote Speaker") -- eliminates over-segmentation, fixes ordering, reduces memory usage
- Over-segmentation caused by low clustering threshold (0.5) and independent per-chunk speaker IDs; fix by setting NumClusters explicitly or raising threshold to 0.75-0.85
- Clock drift between WASAPI mic and loopback streams (~1s per 16min) causes temporal ordering errors; fix with linear drift correction based on WAV duration comparison
- Replace modal TranscriptionProgressDialog with persistent bottom-bar queue UI; auto-enqueue recordings on stop and files on import

---

## 2026-03-24 -- Task Completed: 071 - Notion-Style List View with Detail Drawer

**Type:** Task Completion
**Task:** 071 - Notion-Style List View with Detail Drawer
**Summary:** Replaced side-by-side card layouts on TemplatesPage and TranscriptsPage with compact table views and right-side overlay detail drawers. Transcripts grouped by date with collapsible sections. Delete actions moved inside drawers. 32/32 tests pass.
**Files changed:** 5 files

---

## 2026-03-24 -- Task Started: 071 - Notion-Style List View with Detail Drawer

**Type:** Task Start
**Task:** 071 - Notion-Style List View with Detail Drawer
**Milestone:** --

---

## 2026-03-24 -- Task Completed: 073 - Speaker Name List and Manual Transcribe

**Type:** Task Completion
**Task:** 073 - Speaker Name List and Manual Transcribe
**Summary:** Removed auto-transcription, added speaker name list with add/remove/edit, numSpeakers hint for loopback diarization, skipped mic diarization, DefaultSpeakerName setting, editable ComboBox for speaker name selection in transcript viewer, and re-transcribe button. Build clean, 32/32 tests passed.
**Files changed:** 10 files

---

## 2026-03-24 -- Task Completed: 074 - Transcription Engine Busy Guard

**Type:** Task Completion
**Task:** 074 - Transcription Engine Busy Guard
**Summary:** Created centralized TranscriptionBusyService with TryAcquire/Release pattern, integrated into all transcription entry points with disabled buttons and "Engine busy" overlay when engine is in use.
**Files changed:** 6 files

---

## 2026-03-24 -- Task Completed: 072 - Fix Recording Delete Stale UI

**Type:** Task Completion
**Task:** 072 - Fix Recording Delete Stale UI
**Summary:** Fixed stale UI after recording deletion by moving LoadTranscriptList() outside try block and adding Loaded event handler to refresh list on page navigation.
**Files changed:** 2 files

---

## 2026-03-24 -- Batch Started: [072, 073, 074]

**Type:** Batch Start
**Tasks:** 072 - Fix Recording Delete Stale UI, 073 - Speaker Name List and Manual Transcribe, 074 - Transcription Engine Busy Guard
**Mode:** Parallel (batch of 3)

---

## 2026-03-24 -- Idea Captured: Transcription Engine Busy Guard

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/074-transcription-engine-busy-guard.md
**Summary:** Bug fix — concurrent transcriptions silently fail (stuck "Transcribing" forever). Add engine busy detection, gray out transcribe buttons with "Engine busy" label, auto-enable when engine frees up. No parallel transcriptions allowed.

---

## 2026-03-24 -- Idea Captured: Speaker Name List and Manual Transcription

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/073-speaker-list-and-manual-transcribe.md
**Summary:** Rework call recording flow: no auto-transcription, user defines remote speaker names, count hint fixes diarization over-segmentation, skip mic diarization, name dropdown in transcript viewer, re-transcribe button for re-processing.

---

## 2026-03-24 -- Idea Captured: Fix Recording Delete Stale UI

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/072-fix-recording-delete-stale-ui.md
**Summary:** Bug fix — deleting a recording removes files on disk but the list item remains in the UI, even after tab navigation. State/cache not being invalidated after delete.

---

## 2026-03-24 -- Idea Captured: Notion-Style List View with Detail Drawer

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/071-notion-style-list-and-drawer.md
**Summary:** Replace side-by-side card layout on Templates and Recordings pages with compact single-row list view (with grouping) and a right-side overlay detail drawer. Improves information density, scannability, and organization.

---

## 2026-03-23 -- Task Completed: 070 - Fix Pill Overlay Visualization

**Type:** Task Completion
**Task:** 070 - Fix Pill Overlay Visualization
**Summary:** Fixed two bugs: bars invisible because WPF Canvas reported 0x0 inside Border (wrapped in Grid to propagate size), and border always blue because Idle/Speaking both used BlueBorderColor (Idle now uses grey border/bars, Speaking uses blue/orange).
**Files changed:** 2 files

---

## 2026-03-23 -- Task Started: 070 - Fix Pill Overlay Visualization

**Type:** Task Start
**Task:** 070 - Fix Pill Overlay Visualization
**Milestone:** Bug Fix

---

## 2026-03-23 -- Idea Captured: Fix Pill Overlay Visualization

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/070-fix-pill-overlay-visualization.md
**Summary:** Pill overlay shows no bars and border is always blue. Two bugs: Canvas likely has 0 size so bars never render, and Idle/Speaking states both use blue instead of grey for idle.

---

## 2026-03-23 -- Task Completed: 069 - Fix Start Minimized Setting

**Type:** Task Completion
**Task:** 069 - Fix Start Minimized Setting Ignored on Launch
**Summary:** Replaced --minimized CLI flag check with StartMinimized setting read, removed --minimized from registry command. Build clean.
**Files changed:** 2 files

---

## 2026-03-23 -- Task Started: 069 - Fix Start Minimized Setting

**Type:** Task Start
**Task:** 069 - Fix Start Minimized Setting Ignored on Launch
**Milestone:** Bug Fix

---

## 2026-03-23 -- Idea Captured: Fix Start Minimized Setting Ignored

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/069-fix-start-minimized-setting.md
**Summary:** StartMinimized setting is ignored because App.xaml.cs checks only the --minimized CLI flag instead of the setting. Fix: use the setting, drop the flag.

---

## 2026-03-23 -- Task Completed: 068 - Transcripts Export Cleanup

**Type:** Task Completion
**Task:** 068 - Transcripts Export Cleanup
**Summary:** Removed TXT download button, changed Copy to use FormatAsMarkdown(), added transient "Copied!" indicator, removed ExportText_Click handler.
**Files changed:** 2 files

---

## 2026-03-23 -- Task Completed: 068 - Pill Waveform Overlay

**Type:** Task Completion
**Task:** 068 - Pill Waveform Overlay
**Summary:** Replaced circular overlay with pill-shaped frequency bar visualizer (18 bars, RMS-driven). Added global mouse hook for click-position tracking. Applied brand colors. Removed old position/size settings.
**Files changed:** 4 files

---

## 2026-03-23 -- Task Completed: 067 - Dictation Page Responsive Layout

**Type:** Task Completion
**Task:** 067 - Dictation Page Responsive Layout
**Summary:** Aligned bento grid width with audio input card, added responsive stacking at 640px breakpoint, made warning card 50/50 in wide mode, swapped hotkey row content order.
**Files changed:** 2 files

---

## 2026-03-23 -- Batch Started: [067, 068-pill, 068-export]

**Type:** Batch Start
**Tasks:** 067 - Dictation Page Responsive Layout, 068 - Pill Waveform Overlay, 068 - Transcripts Export Cleanup
**Mode:** Parallel (batch of 3)

---

## 2026-03-23 -- Idea Captured: Transcript Analysis with Local LLM

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/backlog/069-transcript-analysis-with-local-llm.md
**Summary:** AI-powered transcript analysis using Ollama + local LLM (Qwen 2.5 14B). User-defined prompt templates (action items, decisions, ideas, etc.) run against any recording. Streams results in real-time. Fully local, zero cost.

---

## 2026-03-23 14:30 -- Idea Captured: Pill-Shaped Waveform Overlay

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/068-pill-waveform-overlay.md
**Summary:** Replace circular pulsing mic overlay with a horizontal pill containing animated frequency bars (orange on blue border, brand colors). Pill anchors at last global mouse click position extending rightward. Bars simulate frequency response driven by RMS amplitude with per-bar random variation. Removes old overlay position settings.

---

## 2026-03-23 -- Research: Transcript Analysis with LLM

**Type:** Research
**Topic:** How to analyze transcripts with AI using an existing Anthropic subscription, or alternatives
**File:** research/transcript-analysis-with-llm.md
**Key findings:**
- Claude subscription cannot be used programmatically — Anthropic blocked OAuth in third-party apps since Jan 2026, API is separately billed
- Best alternative: Ollama + local LLM (Qwen 2.5 14B) — zero cost, fully local, aligns with WhisperHeim vision
- OllamaSharp NuGet provides mature .NET integration via IChatClient (Microsoft.Extensions.AI)
- 1.5h transcripts (~18K tokens) fit easily in Qwen 2.5's 128K context window

---

## 2026-03-23 -- Idea Captured: Transcripts Page Export Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/068-transcripts-export-cleanup.md
**Summary:** Remove TXT download button from transcripts page, keep only MD and JSON. Change Copy button to copy Markdown format instead of plain text, and add a brief "Copied!" tooltip for feedback.

---

## 2026-03-23 14:00 -- Idea Captured: Dictation Page Responsive Layout

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/067-dictation-page-responsive-layout.md
**Summary:** Fix card width alignment on dictation page, add responsive stacking (row → column) for narrow windows, make warning card responsive 50/50 → stacked, and swap hotkey row content order (pills left, label right).

---

## 2026-03-22 -- Task Completed: 063 - Configurable Data Path

**Type:** Task Completion
**Task:** 063 - Configurable Config/Data Path for Cloud Sync
**Summary:** Implemented bootstrap config in %APPDATA%, DataPathService for path resolution, folder picker in Settings UI, per-session recording folders, machine-local settings split, and migration from old flat structure. 32 tests pass.
**Files changed:** 13 files

---

## 2026-03-22 -- Task Completed: 065 - About Page with Profile

**Type:** Task Completion
**Task:** 065 - About Page with Profile, Contact Links & Ko-fi
**Summary:** Added profile section with photo and bio, contact links (website, Bluesky, LinkedIn), Ko-fi support button, and GitHub link. Wired page into sidebar navigation with collapse support.
**Files changed:** 7 files

---

## 2026-03-22 -- Task Completed: 066 - Template Delete Hover Trash

**Type:** Task Completion
**Task:** 066 - Template Delete with Hover Trash Icon & Confirmation Dialog
**Summary:** Added hover trash icon to template list items with fade animation (matching TranscriptsPage pattern), wired to DeleteConfirmationDialog, removed old Delete Template button from detail panel.
**Files changed:** 2 files

---

## 2026-03-22 -- Batch Started: [063, 065, 066]

**Type:** Batch Start
**Tasks:** 063 - Configurable Data Path, 065 - About Page with Profile, 066 - Template Delete Hover Trash
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 -- Idea Captured: Template Delete with Hover Trash Icon

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/066-template-delete-hover-trash.md
**Summary:** Replace the detail panel "Delete Template" button with a hover trash icon on each template card, using the existing DeleteConfirmationDialog. Matches the recordings deletion pattern.

---

## 2026-03-22 -- Idea Captured: About Page with Profile & Ko-fi

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/065-about-page-with-profile.md
**Summary:** Add personal profile, contact links, Ko-fi support button, and GitHub link to the About page (modeled after VocalFold). Wire the existing AboutPage into the sidebar navigation.

---

## 2026-03-22 -- Task Completed: 064 - Fix Opaque Backgrounds and Delete Dialog

**Type:** Task Completion
**Task:** 064 - Fix Opaque Backgrounds and Delete Dialog
**Summary:** Removed opaque ApplicationBackgroundBrush from TranscriptsPage and TemplatesPage root grids so mica shows through. Replaced glass effect on DeleteConfirmationDialog with solid theme-aware CardBackgroundFillColorDefaultBrush.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Completed: 062 - TTS Page Layout Cleanup

**Type:** Task Completion
**Task:** 062 - TTS Page Layout Cleanup
**Summary:** Removed "INPUT WORKSPACE" label and relocated voice/speaker selector below play/stop buttons, left-aligned. Build verified clean.
**Files changed:** 2 files

---

## 2026-03-22 -- Task Completed: 061 - Update Dictation Hotkey Labels

**Type:** Task Completion
**Task:** 061 - Update Dictation Hotkey Labels
**Summary:** Labels already matched acceptance criteria — no code changes needed.
**Files changed:** 1 file

---

## 2026-03-22 -- Batch Started: [061, 062, 064]

**Type:** Batch Start
**Tasks:** 061 - Update Dictation Hotkey Labels, 062 - TTS Page Layout Cleanup, 064 - Fix Opaque Backgrounds and Delete Dialog
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 -- Idea Captured: Fix Opaque Backgrounds and Delete Dialog

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/063-fix-opaque-backgrounds-and-dialog.md
**Summary:** Fix recordings & templates pages having wrong opaque background (should match other screens). Replace ugly glass/transparency effect on delete confirmation dialog with solid theme-aware surface color.

---

## 2026-03-22 -- Idea Promoted: Configurable Config/Data Path for Cloud Sync

**Type:** Idea Promotion
**From:** ideas/2026-03-22-configurable-config-path.md
**To:** tasks/todo/063-configurable-data-path.md
**Summary:** Configurable data path with bootstrap config, per-session recording folders, cloud sync support via Google Drive.

---

## 2026-03-22 -- Idea Refined: Configurable Config/Data Path for Cloud Sync

**Type:** Idea Refinement
**Idea:** ideas/2026-03-22-configurable-config-path.md
**Status:** Ready
**Summary:** Resolved all open questions: defined sync vs. local split, chose last-write-wins for conflicts, lightweight path validation. Redesigned recordings as first-class data with per-session folders. Idea is ready to promote.

---

## 2026-03-22 -- Idea Captured: TTS Page Layout Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/062-tts-page-layout-cleanup.md
**Summary:** Remove "INPUT WORKSPACE" label from TTS card header; relocate voice selector ComboBox to below play/stop buttons, left-aligned.

---

## 2026-03-22 -- Idea Captured: Update Dictation Page Hotkey Labels

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/061-update-dictation-hotkey-labels.md
**Summary:** Fix hotkey labels on dictation page: rename Start/Stop to Dictation, correct Read Aloud shortcut from Shift+Win+A to Ctrl+Win+^, keep Call Recording as-is.

---

## 2026-03-22 -- Task Completed: 060 - Show Full Logo in Collapsed Sidebar

**Type:** Task Completion
**Task:** 060 - Show Full Logo in Collapsed Sidebar
**Summary:** Increased SidebarCollapsedWidth from 60px to 64px and adjusted logo margin to 0 when collapsed, ensuring the 32px logo fits perfectly centered with no clipping.
**Files changed:** 2 files

---

## 2026-03-22 -- Task Started: 060 - Show Full Logo in Collapsed Sidebar

**Type:** Task Start
**Task:** 060 - Show Full Logo in Collapsed Sidebar
**Milestone:** --

---

## 2026-03-22 -- Task Completed: 058 - Layout Fixes and Branding Cleanup

**Type:** Task Completion
**Task:** 058 - Layout Fixes and Branding Cleanup
**Summary:** Fixed transcript list card overflow with ClipToBounds, made Transcripts and TTS pages stretch to fill available width, removed "LOCAL-FIRST AI" subtitle from sidebar.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Started: 058 - Layout Fixes and Branding Cleanup

**Type:** Task Start
**Task:** 058 - Layout Fixes and Branding Cleanup
**Milestone:** --

---

## 2026-03-22 -- Task Completed: 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page

**Type:** Task Completion
**Task:** 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page
**Summary:** Changed hotkey from Shift+Win+Ä to Ctrl+Win+Ä with new flow: captures selected text, brings window to foreground, navigates to TTS page, pastes text. Removed all overlay infrastructure (3 files deleted) and inline TTS logic.
**Files changed:** 7+ files

---

## 2026-03-22 -- Task Completed: 057 - Redesign WhisperHeim Logo

**Type:** Task Completion
**Task:** 057 - Redesign WhisperHeim Logo
**Summary:** Replaced gradient borders with solid blue border + transparent blue-tinted background, replaced SymbolIcon with custom two-tone XAML paths (blue mic head, orange stand), added programmatic window icon generation for taskbar/Alt+Tab.
**Files changed:** 4 files

---

## 2026-03-22 -- Batch Started: [057, 059]

**Type:** Batch Start
**Tasks:** 057 - Redesign WhisperHeim Logo, 059 - Rework Read-Aloud Hotkey to Navigate to TTS Page
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 -- Idea Captured: Make Templates Work

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/2026-03-22-make-templates-work.md

---

## 2026-03-22 -- Idea Captured: Configurable Config Path for Cloud Sync

**Type:** Idea Capture
**Mode:** Quick
**Filed to:** ideas/2026-03-22-configurable-config-path.md

---

## 2026-03-22 -- Idea Captured: Show Full Logo in Collapsed Sidebar

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/060-collapsed-sidebar-logo-visible.md
**Summary:** Increase collapsed sidebar width from 60px to 64px so the logo isn't clipped. Hide app name when collapsed, show both logo and name when expanded.

---

## 2026-03-22 -- Idea Captured: Rework Read-Aloud Hotkey

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/059-rework-read-aloud-hotkey.md
**Summary:** Change hotkey to Ctrl+Win+Ä, rework flow to capture text → bring window to foreground → navigate to TTS page → paste into input workspace. Remove read-aloud overlay entirely.

---

## 2026-03-22 -- Idea Captured: Layout Fixes and Branding Cleanup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/058-layout-fixes-and-branding-cleanup.md
**Summary:** Fix transcript card overflow, fix Transcripts and TTS pages not filling available width, remove "LOCAL-FIRST AI" sidebar subtitle.

---

## 2026-03-22 -- Idea Captured: Redesign WhisperHeim Logo

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/057-logo-redesign.md
**Summary:** Redesign logo with subtle blue-tinted transparent background, solid blue border (no gradient), two-tone XAML microphone (blue head, orange stand), and set as taskbar/window icon.

---

## 2026-03-22 17:27 -- Task Completed: 051 - Reduce Templates List Column Width

**Type:** Task Completion
**Task:** 051 - Reduce Templates List Column Width
**Summary:** Reduced templates list column from 300px to 200px with responsive min/max constraints, tightened padding for more edit space.
**Files changed:** 2 files

---

## 2026-03-22 17:25 -- Task Started: 051 - Reduce Templates List Column Width

**Type:** Task Start
**Task:** 051 - Reduce Templates List Column Width
**Milestone:** --

---

## 2026-03-22 17:24 -- Task Completed: 054 - Hover Trash Icon per Transcript

**Type:** Task Completion
**Task:** 054 - Hover Trash Icon per Transcript
**Summary:** Replaced "Delete Selected" button with per-item hover trash icon at bottom-right of each transcript card, triggering existing delete confirmation.
**Files changed:** 3 files

---

## 2026-03-22 17:23 -- Task Completed: 045 - Consistent Max-Width Across All Pages

**Type:** Task Completion
**Task:** 045 - Consistent Max-Width Across All Pages
**Summary:** Standardized MaxWidth=900 with centered alignment across 6 pages for consistent content width on wide screens.
**Files changed:** 7 files

---

## 2026-03-22 17:19 -- Batch Started: [045, 054]

**Type:** Batch Start
**Tasks:** 045 - Consistent Max-Width Across All Pages, 054 - Hover Trash Icon per Transcript
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 17:18 -- Task Completed: 056 - Link AI Model Cards to GitHub Projects

**Type:** Task Completion
**Task:** 056 - Link AI Model Cards to GitHub Projects
**Summary:** Added clickable project links to all 6 AI model cards on GeneralPage and AboutPage, opening GitHub/HuggingFace pages in default browser.
**Files changed:** 6 files

---

## 2026-03-22 17:17 -- Task Completed: 053 - Reduce Transcripts List Column Width

**Type:** Task Completion
**Task:** 053 - Reduce Transcripts List Column Width
**Summary:** Reduced transcripts list column from fixed 280px to 200px with MinWidth=160/MaxWidth=280 for responsive behavior.
**Files changed:** 2 files

---

## 2026-03-22 17:16 -- Task Completed: 050 - Collapsible Sidebar Menu

**Type:** Task Completion
**Task:** 050 - Collapsible Sidebar Menu
**Summary:** Implemented collapsible sidebar with toggle button, animated between 200px and 60px icons-only mode, state persisted in settings.
**Files changed:** 4 files

---

## 2026-03-22 17:11 -- Batch Started: [050, 053, 056]

**Type:** Batch Start
**Tasks:** 050 - Collapsible Sidebar Menu, 053 - Reduce Transcripts List Column Width, 056 - Link AI Model Cards to GitHub Projects
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 17:10 -- Task Completed: 055 - Rename Export Button to MD

**Type:** Task Completion
**Task:** 055 - Rename Export Button to MD
**Summary:** Renamed "EXPORT" button to "MD" in TranscriptsPage for consistent naming (MD, JSON, TXT).
**Files changed:** 2 files

---

## 2026-03-22 17:09 -- Task Completed: 049 - Add WhisperHeim Logo to Sidebar

**Type:** Task Completion
**Task:** 049 - Add WhisperHeim Logo to Sidebar
**Summary:** Added logo with blue-to-orange gradient to sidebar header, updated GeneralPage and AboutPage logos to use brand colors.
**Files changed:** 4 files

---

## 2026-03-22 17:08 -- Task Completed: 047 - Fix TTS Voice Cards Dark Mode Background

**Type:** Task Completion
**Task:** 047 - Fix TTS Voice Cards Dark Mode Background
**Summary:** Replaced hardcoded white background with theme-aware CardBackgroundFillColorDefaultBrush and brightened delete button for dark mode.
**Files changed:** 2 files

---

## 2026-03-22 17:06 -- Batch Started: [047, 049, 055]

**Type:** Batch Start
**Tasks:** 047 - Fix TTS Voice Cards Dark Mode Background, 049 - Add WhisperHeim Logo to Sidebar, 055 - Rename Export Button to MD
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 17:05 -- Task Completed: 052 - Remove Magic Replace from Edit Template

**Type:** Task Completion
**Task:** 052 - Remove Magic Replace from Edit Template
**Summary:** Removed magic replace UI card, clipboard placeholder pill, and clipboard expansion logic from template editor.
**Files changed:** 3 files

---

## 2026-03-22 17:04 -- Task Completed: 048 - Tray Icon Green When Recording

**Type:** Task Completion
**Task:** 048 - Tray Icon Green When Recording
**Summary:** Changed tray icon recording color from red to green (0x44CC44), matching the overlay indicator.
**Files changed:** 2 files

---

## 2026-03-22 17:03 -- Task Completed: 046 - Remember Window Size and Position

**Type:** Task Completion
**Task:** 046 - Remember Window Size and Position
**Summary:** Implemented window size/position persistence with 1200x800 default, save on close, restore on startup with off-screen guard using Win32 EnumDisplayMonitors.
**Files changed:** 4 files

---

## 2026-03-22 17:00 -- Batch Started: [046, 048, 052]

**Type:** Batch Start
**Tasks:** 046 - Remember Window Size and Position, 048 - Tray Icon Green When Recording, 052 - Remove Magic Replace from Edit Template
**Mode:** Parallel (batch of 3)

---

## 2026-03-22 16:45 -- Ideas Captured: UI Polish Batch

**Type:** Idea Capture
**Mode:** Deep (batch)
**Tasks created:**
- `tasks/todo/048-tray-icon-green-recording.md` — Tray icon green instead of red when recording
- `tasks/todo/049-sidebar-logo.md` — Add logo to sidebar with blue (#25abfe) + orange (#ff8b00) colors
- `tasks/todo/050-collapsible-sidebar.md` — Collapsible sidebar menu (Medium)
- `tasks/todo/051-templates-column-width.md` — Reduce templates list column width
- `tasks/todo/052-remove-magic-replace.md` — Remove magic replace from edit template
- `tasks/todo/053-transcripts-list-width.md` — Reduce transcripts list column width
- `tasks/todo/054-hover-delete-transcript.md` — Hover trash icon per transcript instead of delete button
- `tasks/todo/055-export-button-rename-md.md` — Rename Export button to MD
- `tasks/todo/056-model-cards-github-links.md` — Link AI model cards to GitHub projects

---

## 2026-03-22 16:35 -- Idea Captured: TTS Voice Cards Dark Mode

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/047-tts-voice-cards-dark-mode.md
**Summary:** Library voice cards in TTS page use hardcoded white background instead of theme-aware brush. Breaks dark mode.

---

## 2026-03-22 16:30 -- Idea Captured: Window Size and Position

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/046-window-size-and-position.md
**Summary:** Default window size 1200x800 centered. Persist size/position across restarts. Reset to centered if saved position is off-screen.

---

## 2026-03-22 16:20 -- Idea Captured: Consistent Page Max-Width

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/045-consistent-page-max-width.md
**Summary:** Apply the Transcripts page's max-width constraint to all other pages for visual consistency across the app.

---

## 2026-03-22 16:15 -- Task Completed: 044 - Fix Theme Persistence and Settings Highlight

**Type:** Task Completion
**Task:** 044 - Fix Theme Persistence and Settings Highlight
**Summary:** Fixed theme persistence by adding theme restoration on app startup in App.xaml.cs and fixed theme card highlighting in GeneralPage.xaml.cs by moving HighlightActiveTheme() to a Loaded event handler.
**Files changed:** 2 files

---

## 2026-03-22 16:10 -- Task Started: 044 - Fix Theme Persistence and Settings Highlight

**Type:** Task Start
**Task:** 044 - Fix Theme Persistence and Settings Highlight
**Milestone:** --

---

## 2026-03-22 16:00 -- Idea Captured: Fix Theme Persistence

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/044-theme-persistence.md
**Summary:** Theme choice (Light/Dark/System) not restored on startup and not highlighted in settings. Two small fixes needed in App.xaml.cs and GeneralPage.xaml.cs.

---

## 2026-03-22 15:30 -- Task Completed: 043 - Faithful Quiet Engine Restyling

**Type:** Task Completion
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Summary:** Faithfully restyled all 7 pages + sidebar to match inspiration mockups. Applied bento grid layouts, gradient CTAs, kbd pill key caps, ambient tinted shadows, surface hierarchy, ghost borders, editorial typography. Build succeeds.
**Files changed:** 9 files

---

## 2026-03-22 15:20 -- Task Started: 043 - Faithful Quiet Engine Restyling

**Type:** Task Start
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Milestone:** M5 - UI Redesign

---

## 2026-03-22 15:10 -- Task Completed: 042 - TTS Voice Pre-Caching on Startup

**Type:** Task Completion
**Task:** 042 - TTS Voice Pre-Caching on Startup
**Summary:** Enabled sherpa-onnx embedding cache (capacity 10), added in-memory WAV sample cache, and WarmUpAsync() triggered from App.xaml.cs on background thread after UI startup.
**Files changed:** 4 files

---

## 2026-03-22 -- Task Created: 043 - Faithful Quiet Engine Restyling

**Type:** Task Creation
**Task:** 043 - Faithful Quiet Engine Restyling (All Pages)
**Summary:** Redo the visual restyling from Task 040 Phase 3 to faithfully match all inspiration mockups. Covers all 7 pages + sidebar with bento grid layouts, gradient CTAs, ghost borders, ambient shadows, kbd pills, and editorial typography.

---

## 2026-03-22 15:05 -- Task Started: 042 - TTS Voice Pre-Caching on Startup

**Type:** Task Start
**Task:** 042 - TTS Voice Pre-Caching on Startup
**Milestone:** —

---

## 2026-03-22 15:00 -- Task Completed: 041 - Default Read-Aloud Voice

**Type:** Task Completion
**Task:** 041 - Default Read-Aloud Voice
**Summary:** VoiceCombo selection now persists DefaultVoiceId to TtsSettings, and page load pre-selects the saved voice with fallback.
**Files changed:** 3 files

---

## 2026-03-22 14:55 -- Task Started: 041 - Default Read-Aloud Voice

**Type:** Task Start
**Task:** 041 - Default Read-Aloud Voice
**Milestone:** —

---

## 2026-03-22 14:50 -- Task Completed: 040 - UI Redesign Navigation & TTS Merge

**Type:** Task Completion
**Task:** 040 - UI Redesign Navigation & TTS Merge
**Summary:** Merged VoiceCloningPage and VoiceLoopbackCapturePage into TextToSpeechPage, restructured sidebar from 9 to 7 items, restyled all pages to Quiet Engine design language. Build succeeds.
**Files changed:** 15 files

---

## 2026-03-22 -- Idea Captured: TTS Voice Pre-Caching on Startup

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/042-tts-voice-warm-up.md
**Summary:** Pre-cache the default TTS voice on app startup via background thread — load TTS model, warm the sherpa-onnx embedding cache with a dummy generation, and keep the default voice's WAV samples in memory. Eliminates the ~1-3s encoder delay on first read-aloud hotkey press.

---

## 2026-03-22 14:40 -- Task Started: 040 - UI Redesign Navigation & TTS Merge

**Type:** Task Start
**Task:** 040 - UI Redesign Navigation & TTS Merge
**Milestone:** M5 - UI Redesign

---

## 2026-03-22 14:35 -- Task Completed: 038 - Transcript Audio Playback

**Type:** Task Completion
**Task:** 038 - Transcript Audio Playback
**Summary:** Audio preserved alongside transcripts, segment click-to-play with NAudio, play/pause/stop controls, position tracking, currently-playing segment highlight. Old transcripts gracefully hide playback.
**Files changed:** 6 files

---

## 2026-03-22 14:25 -- Task Started: 038 - Transcript Audio Playback

**Type:** Task Start
**Task:** 038 - Transcript Audio Playback
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-22 14:20 -- Task Completed: 037 - Speaker Name Editing

**Type:** Task Completion
**Task:** 037 - Speaker Name Editing
**Summary:** Implemented global rename (click speaker label) and per-segment override (Shift+Click) with SpeakerNameMap dictionary and SpeakerOverride property. 32 tests pass (10 new).
**Files changed:** 6 files

---

## 2026-03-22 14:15 -- Task Started: 037 - Speaker Name Editing

**Type:** Task Start
**Task:** 037 - Speaker Name Editing
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-22 14:10 -- Task Completed: 039 - Read-Aloud Overlay Indicator

**Type:** Task Completion
**Task:** 039 - Read-Aloud Overlay Indicator
**Summary:** Implemented purple-themed read-aloud overlay with Thinking (pulsing/spinning) and Playing (sound wave) animations, lifecycle events on ReadAloudHotkeyService, and onPlaybackStarted callback in SpeakAsync.
**Files changed:** 8 files

---

## 2026-03-22 14:05 -- Task Completed: 036 - Transcript Naming

**Type:** Task Completion
**Task:** 036 - Transcript Naming
**Summary:** Added editable Name property to CallTranscript model with JSON persistence, editable TextBox in transcript viewer header, name display in transcript list, and backward compatibility for existing transcripts.
**Files changed:** 7 files

---

## 2026-03-22 14:00 -- Batch Started: [036, 039]

**Type:** Batch Start
**Tasks:** 036 - Transcript Naming, 039 - Read-Aloud Overlay Indicator
**Mode:** Parallel (batch of 2)

---

## 2026-03-22 -- Idea Captured: Default Read-Aloud Voice from TTS Page

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/041-default-read-aloud-voice.md
**Summary:** Persist the voice selected on the TTS page as the default for the read-aloud hotkey (Shift+Win+Ä). Small wiring change — settings model and hotkey service already support it, just needs the UI to write it back.

---

## 2026-03-22 -- Idea Captured: UI Redesign — Navigation & TTS Merge

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/040-ui-redesign-navigation-and-tts-merge.md
**Summary:** Restructure sidebar from 9 to 7 items (Dictation, Templates, Recordings, Transcriptions, Text to Speech, Settings, Models). Merge TextToSpeechPage + VoiceCloningPage + VoiceLoopbackCapturePage into a single unified TTS page. Apply "Quiet Engine" design language from inspiration files across all pages.

---

## 2026-03-21 23:30 -- Idea Captured: Read-Aloud Overlay Indicator

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/039-read-aloud-overlay.md
**Summary:** Visual overlay indicator for the read-aloud hotkey (Shift+Win+Ä) — shows thinking state while model loads, animated playback state while reading, auto-dismisses on completion, toggle-stops on re-press. Same position as dictation overlay but distinct color.

---

## 2026-03-21 22:15 -- Research: TTS Naturalness & Sentence Boundary Artifacts

**Type:** Research
**Topic:** Why Pocket TTS output sounds rushed with voice-breaking artifacts between sentences, and how to fix it
**File:** research/tts-naturalness-and-pacing.md
**Key findings:**
- sherpa-onnx splits text into sentences, generates each independently via `GenerateSingleSentence()`, and concatenates audio with NO silence between them -- this is the root cause of the "rushed" and "breaking voice" artifacts
- Several `Extra` parameters are available but unused: `frames_after_eos` (default 3), `temperature` (default 0.7), configurable via `genConfig.Extra` hashtable
- `NumSteps` (flow matching diffusion iterations) can be increased from 5 to 8-10 for smoother audio quality
- Best fix: app-level sentence splitting with configurable silence injection (300ms+) between generated segments
- The 15s reference audio gets truncated to 12s; int8 quantization may also degrade voice cloning fidelity

---

## 2026-03-21 21:40 -- Idea Captured: Transcript Usability Improvements

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/036-transcript-naming.md, tasks/todo/037-speaker-name-editing.md, tasks/todo/038-transcript-audio-playback.md
**Summary:** Three tasks to improve transcript usability long-term: (1) editable transcript names with auto-default from date/time, (2) speaker name editing with global rename + per-segment override, (3) click-to-play audio playback from segments with WAV files preserved alongside transcripts. All added to M2 milestone.

---

## 2026-03-21 21:35 -- Milestone M4 Complete: Text-to-Speech (Kyutai Pocket TTS)

**Type:** Milestone Completion
**Milestone:** M4 - Text-to-Speech
**Tasks completed:** 023, 029, 030, 031, 032, 033, 034, 035 (7 subtasks + parent)
**Summary:** Full TTS milestone implemented — Pocket TTS engine via sherpa-onnx, voice cloning from mic and system audio, read-selected-text hotkey, TTS UI page, MP3/OGG/WAV export, configurable settings.

---

## 2026-03-21 21:30 -- Task Completed: 034 - Audio export (MP3/OGG/WAV)

**Type:** Task Completion
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Summary:** Created AudioExportService with WAV, MP3 (resampled to 44.1kHz via MediaFoundationEncoder), and OGG/Opus (resampled to 48kHz via Concentus) export. Added "Save as..." button to TextToSpeechPage with SaveFileDialog and format selection.
**Files changed:** 4 files

---

## 2026-03-21 21:20 -- Task Started: 034 - Audio export (MP3/OGG)

**Type:** Task Start
**Task:** 034 - Audio export (MP3/OGG/WAV)
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 21:15 -- Task Completed: 035 - TTS settings + hotkey configuration

**Type:** Task Completion
**Task:** 035 - TTS settings + hotkey configuration
**Summary:** Added TtsSettings model with DefaultVoiceId, ReadAloudHotkey, PlaybackDeviceId persisted via SettingsService. ReadAloudHotkeyService now reads hotkey config from settings with live re-registration. SpeakAsync accepts playback device parameter.
**Files changed:** 6 files

---

## 2026-03-21 21:12 -- Task Completed: 033 - TTS UI page

**Type:** Task Completion
**Task:** 033 - TTS UI page
**Summary:** Created TextToSpeechPage with multi-line text input, voice selector (built-in + custom), Play/Stop with CancellationTokenSource, indeterminate progress bar, and voice preview button. Wired into MainWindow sidebar navigation.
**Files changed:** 5 files

---

## 2026-03-21 21:05 -- Batch Started: [033, 035]

**Type:** Batch Start
**Tasks:** 033 - TTS UI page, 035 - TTS settings + hotkey configuration
**Mode:** Parallel (batch of 2)

---

## 2026-03-21 21:00 -- Task Completed: 032 - Read selected text via global hotkey

**Type:** Task Completion
**Task:** 032 - Read selected text via global hotkey
**Summary:** Implemented SelectedTextService with cascading capture (UI Automation TextPattern first, then SendInput Ctrl+C with clipboard backup/restore) and ReadAloudHotkeyService (Ctrl+Shift+R default) that speaks captured text via ITextToSpeechService.
**Files changed:** 5 files

---

## 2026-03-21 20:58 -- Task Completed: 031 - Voice cloning from system audio loopback

**Type:** Task Completion
**Task:** 031 - Voice cloning from system audio loopback
**Summary:** Created HighQualityLoopbackService capturing system audio at native 48kHz via WasapiLoopbackCapture. Built VoiceLoopbackCapturePage UI with device selection, level meter, duration display, voice naming, and save to voices directory.
**Files changed:** 6 files

---

## 2026-03-21 20:55 -- Task Completed: 030 - Voice cloning from microphone recording

**Type:** Task Completion
**Task:** 030 - Voice cloning from microphone recording
**Summary:** Implemented HighQualityRecorderService recording mic at 44.1kHz and VoiceCloningPage UI with level meter, duration tracking, 5s minimum indicator, device selection, voice naming, and background noise warning.
**Files changed:** 7 files

---

## 2026-03-21 20:50 -- Batch Started: [030, 031, 032]

**Type:** Batch Start
**Tasks:** 030 - Voice cloning from mic, 031 - Voice cloning from loopback, 032 - Read selected text via hotkey
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 20:45 -- Task Completed: 029 - Pocket TTS engine service + model download

**Type:** Task Completion
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Summary:** Implemented ITextToSpeechService with Pocket TTS via sherpa-onnx C# bindings. Supports GenerateAudioAsync, streaming generation with callback, and SpeakAsync with NAudio WaveOutEvent playback at 24kHz. Added PocketTtsInt8 model (~200MB, 9 files) to ModelManagerService for auto-download from HuggingFace. Build succeeds.
**Files changed:** 5 files

---

## 2026-03-21 19:15 -- Task Started: 029 - Pocket TTS engine service + model download

**Type:** Task Start
**Task:** 029 - Pocket TTS engine service + model download + built-in voice playback
**Milestone:** M4 - Text-to-Speech

---

## 2026-03-21 18:50 -- Idea Captured: Kyutai Pocket TTS Integration

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/023-tts-pocket-tts.md (parent) + subtasks 029–035
**Summary:** Full TTS milestone using Kyutai Pocket TTS — voice cloning from mic/loopback, read-selected-text hotkey (UI Automation + Ctrl+C fallback), TTS UI page, MP3/OGG export, and settings. Researched feasibility of all components: Pocket TTS runs CPU-only via sherpa-onnx (already a dependency), text selection capture is proven pattern, loopback capture infrastructure exists. English-only, 7 tasks total.

---

## 2026-03-21 -- Task Completed: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Completion
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Summary:** Created TranscriptionProgressDialog with dual progress bars, stage description, and cancel button. Wired into MainWindow to auto-trigger pipeline when recording stops, with navigation to TranscriptsPage on success.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 028 - Post-recording transcription pipeline with progress UI

**Type:** Task Start
**Task:** 028 - Post-recording transcription pipeline with progress UI
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 027 - Tray context menu for start/stop call recording

**Type:** Task Completion
**Task:** 027 - Tray context menu for start/stop call recording
**Summary:** Added "Start Call Recording" tray menu item with Record24 icon, Ctrl+Win+R hotkey, and live recording state feedback (orange tray icon, duration in menu text and tooltip). Added DurationUpdated to ICallRecordingService interface.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Started: 027 - Tray context menu for start/stop call recording

**Type:** Task Start
**Task:** 027 - Tray context menu for start/stop call recording
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Task Completed: 026 - Wire call recording services in app startup

**Type:** Task Completion
**Task:** 026 - Wire call recording services in app startup
**Summary:** Wired CallRecordingService, CallTranscriptionPipeline, CallRecordingHotkeyService, SpeakerDiarizationService, and TranscriptStorageService in App.xaml.cs and passed them to MainWindow constructor as fields.
**Files changed:** 3 files

---

## 2026-03-21 -- Task Started: 026 - Wire call recording services in app startup

**Type:** Task Start
**Task:** 026 - Wire call recording services in app startup
**Milestone:** M2 - Audio Capture + Call Transcription

---

## 2026-03-21 -- Planning: Call Recording UI Integration

**Type:** Planning
**Summary:** Planned 3 tasks to wire up the existing call recording backend to the UI — service registration, tray context menu with Ctrl+Win+R hotkey, and post-recording transcription progress dialog with auto-navigation.
**Milestones created/updated:** M2 (added tasks 026-028)
**Tasks created:** 026-wire-call-recording-services, 027-tray-menu-call-recording, 028-post-recording-transcription-ui
**Tasks moved to backlog:** none
**Ideas incorporated:** none

---

## 2026-03-21 -- Task Completed: 025 - Overlay Microphone State Visualization

**Type:** Task Completion
**Task:** 025 - Overlay Microphone State Visualization
**Summary:** Implemented dynamic overlay mic states (green idle, green+RMS-driven ring scaling while speaking, grey for no mic, red for errors). Added OverlayMicState enum, replaced hardcoded red with animated color brushes, wired real-time audio amplitude through orchestrator to drive smooth ring scaling.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Started: 025 - Overlay Microphone State Visualization

**Type:** Task Start
**Task:** 025 - Overlay Microphone State Visualization
**Milestone:** --

---

## 2026-03-21 -- Idea Captured: Overlay Mic State Visualization

**Type:** Idea Capture
**Mode:** Deep
**Filed to:** tasks/todo/025-overlay-mic-state-visualization.md
**Summary:** Dynamic mic icon colors (green=idle/speaking, grey=no mic, red=error) with amplitude-driven ring scaling animation during speech. Overlay only.

---

## 2026-03-21 -- Task Completed: 024 - Windows Auto-Launch

**Type:** Task Completion
**Task:** 024 - Windows Auto-Launch
**Summary:** StartupService manages HKCU Run registry, --minimized flag for tray-only auto-start, path refresh on each launch.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 014 - Microphone Selection

**Type:** Task Completion
**Task:** 014 - Microphone Selection
**Summary:** Dropdown on Dictation page with NAudio device enumeration, persisted selection, fallback for missing devices.
**Files changed:** 5 files

---

## 2026-03-21 -- Task Completed: 005 - Model Manager

**Type:** Task Completion
**Task:** 005 - Model Manager
**Summary:** Auto-downloads Parakeet TDT 0.6B int8 (~661MB) and Silero VAD (~2MB) on first run with progress dialog and cancellation.
**Files changed:** 9 files

---

## 2026-03-21 -- Task Completed: 007 - Silero VAD Integration

**Type:** Task Completion
**Task:** 007 - Silero VAD Integration
**Summary:** ONNX Runtime-based Silero VAD with state machine, configurable thresholds, pre-speech padding, SpeechStarted/SpeechEnded events.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 004 - Global Hotkey

**Type:** Task Completion
**Task:** 004 - Global Hotkey
**Summary:** Win32 RegisterHotKey/UnregisterHotKey with configurable Ctrl+LWin hotkey, event system, and conflict handling.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 003 - Settings Infrastructure

**Type:** Task Completion
**Task:** 003 - Settings Infrastructure
**Summary:** JSON settings with AppSettings model, SettingsService for %APPDATA% persistence, and 4 navigable settings pages in MainWindow.
**Files changed:** 13 files

---

## 2026-03-21 -- Batch Started: [003, 004, 007]

**Type:** Batch Start
**Tasks:** 003 - Settings Infrastructure, 004 - Global Hotkey, 007 - Silero VAD Integration
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 010 - Input Simulation

**Type:** Task Completion
**Task:** 010 - Input Simulation
**Summary:** Win32 SendInput P/Invoke with KEYEVENTF_UNICODE, backspace correction, configurable delay, cancellation support.
**Files changed:** 4 files

---

## 2026-03-21 -- Task Completed: 006 - Audio Capture Service

**Type:** Task Completion
**Task:** 006 - Audio Capture Service
**Summary:** NAudio WaveInEvent capture at 16kHz/mono, float32 conversion, thread-safe ring buffer, device enumeration. 8 passing unit tests.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Completed: 002 - Tray Icon and Window

**Type:** Task Completion
**Task:** 002 - Tray Icon and Window
**Summary:** FluentWindow with Mica backdrop, tray icon with Segoe Fluent microphone glyph, show/hide toggle, right-click context menu.
**Files changed:** 5 files

---

## 2026-03-21 -- Batch Started: [002, 006, 010]

**Type:** Batch Start
**Tasks:** 002 - Tray Icon and Window, 006 - Audio Capture Service, 010 - Input Simulation
**Mode:** Parallel (batch of 3)

---

## 2026-03-21 -- Task Completed: 001 - Project Scaffolding

**Type:** Task Completion
**Task:** 001 - Project Scaffolding
**Summary:** Created .NET 9 WPF solution with all core NuGet packages, x64-only config, and ShutdownMode=OnExplicitShutdown. Builds with 0 warnings.
**Files changed:** 8 files

---

## 2026-03-21 -- Task Started: 001 - Project Scaffolding

**Type:** Task Start
**Task:** 001 - Project Scaffolding
**Milestone:** M1 - Live Dictation + Core App

---

## 2026-03-21 -- Planning: Full roadmap and task breakdown for all milestones

**Type:** Planning
**Summary:** Created 4-milestone roadmap with 24 tasks. M1 (Live Dictation + Core App) has 15 tasks covering project setup through end-to-end dictation with overlay and templates. M2 (Call Transcription) has 5 tasks for WASAPI loopback, diarization, and transcript export. M3 (Voice Messages) has 3 tasks including backlogged Telegram bot. M4 (TTS) is a single placeholder task in backlog.
**Milestones created:** M1, M2, M3, M4
**Tasks created:** 001 through 024
**Tasks moved to backlog:** 022-telegram-bot, 023-tts-integration
**Ideas incorporated:** None (no ideas existed)

---

## 2026-03-21 -- Brainstorm: Initial product vision for WhisperHeim

**Type:** Brainstorm
**Summary:** Defined WhisperHeim as a local-first, Windows 11 tray app unifying all voice workflows: live streaming dictation, call transcription with speaker diarization, voice message transcription, and text-to-speech. Chose C#/WPF with WPF UI for the native shell, Parakeet TDT 0.6B for ASR, sherpa-onnx for diarization, and Silero VAD for streaming.
**Vision updated:** Yes
**Key decisions:**
- Complete restart from VocalFold -- new architecture, no code reuse
- C# across the board (systems-level complexity favors C# over F#)
- WPF + WPF UI (not WinUI 3) for tray app with PowerToys aesthetics
- Parakeet TDT 0.6B over Whisper (faster, no hallucinations, EN/DE sufficient)
- sherpa-onnx for both ASR and diarization (native .NET, no Python sidecar)
- WASAPI loopback for system audio capture (call transcription = Milestone 2)
- Text-to-speech deferred to Milestone 4, details TBD
- No voice commands -- templates only, triggered by hotkey + voice
- Telegram bot integration as stretch goal for voice message transcription

---
