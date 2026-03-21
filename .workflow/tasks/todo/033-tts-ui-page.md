# Task: TTS UI page

**ID:** 033
**Milestone:** M4 - Text-to-Speech
**Size:** Small
**Created:** 2026-03-21
**Dependencies:** 029
**Parent:** 023

## Objective
Create a WPF UI page for text-to-speech with text input, voice selection, and playback controls.

## Details
- New `TextToSpeechPage.xaml` in Views/Pages
- Text input: multi-line text box for pasting or typing text
- Voice selector: dropdown listing built-in voices + custom voices (with voice type indicator)
- Play/Stop buttons with generation progress indicator
- Voice preview button (reads a short test phrase)
- Navigation entry in MainWindow sidebar
- Follow existing Fluent UI patterns (WPF-UI)

## Acceptance Criteria
- [ ] Text input field accepts multi-line text
- [ ] Voice selector shows all available voices (built-in + custom)
- [ ] Play button generates and plays speech
- [ ] Stop button halts playback
- [ ] Progress indicator during generation
- [ ] Voice preview with test phrase

## Work Log
<!-- Appended by /work during execution -->
