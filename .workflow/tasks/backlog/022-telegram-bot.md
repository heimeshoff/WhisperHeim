# Task: Telegram Bot for Voice Message Transcription

**ID:** 022
**Milestone:** M3 - Voice Message Transcription
**Size:** Large
**Created:** 2026-03-21
**Dependencies:** 020

## Objective
Optional Telegram bot that auto-transcribes forwarded voice messages.

## Details
Integrate Telegram.Bot NuGet package. User configures bot token in settings. Bot listens for voice messages and audio files. Downloads the audio, transcribes with Parakeet, replies with the text. Only responds to the configured user (security). Runs as a background service within the app. Can be enabled/disabled in settings. Requires internet for Telegram API (but transcription is still local).

## Acceptance Criteria
- [ ] Bot receives voice messages via Telegram
- [ ] Transcribes locally using Parakeet
- [ ] Replies with transcribed text
- [ ] Only responds to authorized user
- [ ] Toggleable on/off in settings

## Notes
This is a stretch goal. Goes to backlog. Requires internet for Telegram API but all transcription remains local.

## Work Log
<!-- Appended by /work during execution -->
