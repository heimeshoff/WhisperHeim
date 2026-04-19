"""Template orchestrator — voice-triggered template workflow.

Ported from Windows WhisperHeim TemplateOrchestrator.cs.

Workflow:
1. User holds template hotkey
2. Audio capture + transcription starts
3. User speaks a template name
4. On release, spoken text is fuzzy-matched against templates
5. Matched template's expanded text is typed into the active app
"""

import logging
import threading
from typing import Callable, Optional

import numpy as np

from whisperheim.services.audio_capture import AudioCaptureService
from whisperheim.services.dictation_pipeline import DictationPipeline
from whisperheim.services.hotkey_service import HotkeyService
from whisperheim.services.settings_service import HotkeySettings, SettingsService
from whisperheim.services.templates.template_service import (
    SYSTEM_REPEAT_ACTION_ID,
    TemplateMatchResult,
    TemplateService,
)
from whisperheim.services.text_inserter import TextInserter

logger = logging.getLogger(__name__)

# Maximum time (seconds) to listen for a template name after hotkey press
LISTEN_TIMEOUT_S = 4.0


class TemplateOrchestrator:
    """Orchestrates the template workflow: hotkey -> capture -> match -> type.

    Uses the same DictationPipeline as regular dictation but with its own
    hotkey binding and result handling.
    """

    def __init__(
        self,
        settings_service: SettingsService,
        pipeline: DictationPipeline,
        template_service: TemplateService,
        text_inserter: TextInserter,
        notify_callback: Optional[Callable[[str], None]] = None,
        last_dictation_callback: Optional[Callable[[], Optional[str]]] = None,
    ):
        self._settings = settings_service
        self._pipeline = pipeline
        self._template_service = template_service
        self._text_inserter = text_inserter
        self._notify = notify_callback or (lambda msg: None)
        self._get_last_dictation = last_dictation_callback or (lambda: None)

        # Template hotkey — separate from dictation hotkey
        hotkey_settings = settings_service.settings.template_hotkey
        self._hotkey = HotkeyService(hotkey_settings)

        self._lock = threading.Lock()
        self._is_listening = False
        self._timeout_timer: Optional[threading.Timer] = None

    def start(self) -> None:
        """Start listening for the template hotkey."""
        self._hotkey.set_callbacks(
            on_activated=self._on_hotkey_activated,
            on_deactivated=self._on_hotkey_deactivated,
        )
        self._hotkey.start()
        logger.info("[TemplateOrchestrator] Started. Listening for template hotkey.")

    def stop(self) -> None:
        """Stop listening."""
        self._hotkey.stop()
        self._cancel_listening()
        logger.info("[TemplateOrchestrator] Stopped.")

    def _on_hotkey_activated(self) -> None:
        """Template hotkey pressed — start recording for template name."""
        with self._lock:
            if self._is_listening:
                return
            self._is_listening = True

        logger.info("[TemplateOrchestrator] Template hotkey pressed, starting voice capture...")

        # Save and replace pipeline callbacks for template mode
        self._pipeline.set_callbacks(
            on_recording_started=lambda: None,
            on_recording_stopped=lambda: None,
            on_transcription_started=lambda: None,
            on_transcription_result=self._on_transcription_result,
            on_transcription_error=self._on_transcription_error,
        )

        try:
            self._pipeline.start_recording()
        except Exception as e:
            logger.error("[TemplateOrchestrator] Failed to start pipeline: %s", e)
            with self._lock:
                self._is_listening = False
            self._notify("Template: Failed to start voice capture.")
            return

        # Set a timeout
        self._timeout_timer = threading.Timer(LISTEN_TIMEOUT_S, self._on_timeout)
        self._timeout_timer.daemon = True
        self._timeout_timer.start()

    def _on_hotkey_deactivated(self) -> None:
        """Template hotkey released — stop recording."""
        with self._lock:
            if not self._is_listening:
                return

        if self._timeout_timer:
            self._timeout_timer.cancel()
            self._timeout_timer = None

        self._pipeline.stop_recording()
        # Result will arrive via _on_transcription_result callback

    def _on_transcription_result(self, text: str) -> None:
        """Pipeline produced transcription — match against templates."""
        with self._lock:
            if not self._is_listening:
                return
            self._is_listening = False

        if not text or not text.strip():
            logger.info("[TemplateOrchestrator] Empty transcription, ignoring.")
            self._notify("Template: No speech detected.")
            return

        logger.info('[TemplateOrchestrator] Captured spoken text: "%s"', text)
        self._process_template_match(text.strip())

    def _on_transcription_error(self, error: str) -> None:
        """Pipeline error during template capture."""
        logger.error("[TemplateOrchestrator] Pipeline error: %s", error)
        self._cancel_listening()
        self._notify("Template: Voice capture error.")

    def _on_timeout(self) -> None:
        """Recording timeout — stop and process whatever we have."""
        logger.info("[TemplateOrchestrator] Timeout reached.")
        with self._lock:
            if not self._is_listening:
                return
        self._pipeline.stop_recording()
        # Result will come via callback

    def _process_template_match(self, spoken_text: str) -> None:
        """Fuzzy match spoken text against templates and type result."""
        result = self._template_service.match_and_expand(spoken_text)

        if result is None:
            self._notify(f'Template: No match for "{spoken_text}"')
            return

        # Handle system templates
        if result.is_system_template:
            if result.system_action_id == SYSTEM_REPEAT_ACTION_ID:
                last_text = self._get_last_dictation()
                if last_text:
                    logger.info("[TemplateOrchestrator] Repeat: re-typing last dictation.")
                    self._text_inserter.insert_text(last_text)
                    self._notify("Template: Repeat")
                else:
                    self._notify("Template: Nothing to repeat.")
            return

        # Type the expanded template text
        logger.info(
            '[TemplateOrchestrator] Matched template "%s", inserting text.',
            result.template_name,
        )
        self._text_inserter.insert_text(result.expanded_text)
        self._notify(f"Template: {result.template_name}")

    def _cancel_listening(self) -> None:
        """Cancel active listening."""
        with self._lock:
            self._is_listening = False
        if self._timeout_timer:
            self._timeout_timer.cancel()
            self._timeout_timer = None
