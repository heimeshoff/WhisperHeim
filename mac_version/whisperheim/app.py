"""WhisperHeim macOS menu bar app — rumps-based UI with status indicator.

States:
- Idle: ready, waiting for hotkey
- Recording: hotkey held, capturing audio
- Transcribing: processing captured audio
"""

import logging
import platform
import sys
import threading
from typing import Optional

from whisperheim.services.audio_capture import AudioCaptureService
from whisperheim.services.dictation_pipeline import DictationPipeline
from whisperheim.services.hotkey_service import HotkeyService
from whisperheim.services.model_manager import ModelManager
from whisperheim.services.settings_service import SettingsService
from whisperheim.services.templates.template_orchestrator import TemplateOrchestrator
from whisperheim.services.templates.template_service import TemplateService
from whisperheim.services.text_inserter import TextInserter
from whisperheim.services.transcription_service import TranscriptionService
from whisperheim.services.vad_service import VadService

logger = logging.getLogger(__name__)

# Menu bar status icons (emoji fallback for systems without custom icons)
ICON_IDLE = "🎙️"
ICON_RECORDING = "🔴"
ICON_TRANSCRIBING = "⏳"
ICON_ERROR = "❌"

# Menu bar title text (shown next to icon)
TITLE_IDLE = "WhisperHeim"
TITLE_RECORDING = "Recording..."
TITLE_TRANSCRIBING = "Transcribing..."


class WhisperHeimApp:
    """Main application class — ties all services together.

    On macOS with rumps available, runs as a menu bar app.
    Otherwise, runs as a headless CLI app.
    """

    def __init__(self):
        self._settings_service = SettingsService()
        self._model_manager = ModelManager(self._settings_service.models_dir)
        self._audio_capture = AudioCaptureService()
        self._text_inserter = TextInserter()
        self._hotkey_service = HotkeyService(self._settings_service.settings.hotkey)

        # These get initialized after model loading
        self._vad: Optional[VadService] = None
        self._transcription: Optional[TranscriptionService] = None
        self._pipeline: Optional[DictationPipeline] = None

        # Template system
        self._template_service: Optional[TemplateService] = None
        self._template_orchestrator: Optional[TemplateOrchestrator] = None
        self._template_editor: Optional[TemplateEditorWindow] = None
        self._last_dictation_text: Optional[str] = None

        self._rumps_app = None

    def run(self) -> None:
        """Start the application."""
        logger.info("[App] WhisperHeim starting...")

        # Ensure models are downloaded
        if not self._model_manager.are_models_downloaded():
            logger.info("[App] Models not found, downloading...")
            self._model_manager.ensure_models(
                progress_callback=lambda msg: logger.info("[App] %s", msg)
            )

        # Initialize services
        self._init_services()

        # Start hotkey listener
        self._hotkey_service.set_callbacks(
            on_activated=self._on_hotkey_activated,
            on_deactivated=self._on_hotkey_deactivated,
        )
        self._hotkey_service.start()

        # Initialize template system
        self._init_templates()

        # Run the UI
        if platform.system() == "Darwin":
            self._run_menubar()
        else:
            self._run_headless()

    def _init_services(self) -> None:
        """Initialize VAD, transcription, and pipeline."""
        settings = self._settings_service.settings

        # VAD
        self._vad = VadService(
            model_path=self._model_manager.silero_vad_path,
            settings=settings.vad,
        )

        # Transcription
        self._transcription = TranscriptionService(settings=settings.transcription)
        self._transcription.load_model(
            encoder_path=self._model_manager.parakeet_encoder_path,
            decoder_path=self._model_manager.parakeet_decoder_path,
            joiner_path=self._model_manager.parakeet_joiner_path,
            tokens_path=self._model_manager.parakeet_tokens_path,
        )

        # Dictation pipeline
        self._pipeline = DictationPipeline(
            audio_capture=self._audio_capture,
            vad=self._vad,
            transcription=self._transcription,
            sample_rate=settings.dictation.sample_rate,
        )
        self._pipeline.set_callbacks(
            on_recording_started=self._on_recording_started,
            on_recording_stopped=self._on_recording_stopped,
            on_transcription_started=self._on_transcription_started,
            on_transcription_result=self._on_transcription_result,
            on_transcription_error=self._on_transcription_error,
        )

    def _init_templates(self) -> None:
        """Initialize the template system."""
        self._template_service = TemplateService(self._settings_service)
        # Template editor is created lazily when opened (avoids tkinter import at startup)
        self._template_editor = None

        if self._pipeline:
            self._template_orchestrator = TemplateOrchestrator(
                settings_service=self._settings_service,
                pipeline=self._pipeline,
                template_service=self._template_service,
                text_inserter=self._text_inserter,
                notify_callback=self._on_template_notification,
                last_dictation_callback=self._get_last_dictation,
            )
            self._template_orchestrator.start()
            logger.info("[App] Template system initialized.")

    def _on_template_notification(self, message: str) -> None:
        """Show a template-related notification."""
        logger.info("[App] %s", message)
        self._update_status("idle")

    def _get_last_dictation(self) -> Optional[str]:
        """Return the last dictation text for the Repeat system template."""
        return self._last_dictation_text

    def _run_menubar(self) -> None:
        """Run as a macOS menu bar app using rumps."""
        try:
            import rumps

            class MenuBarApp(rumps.App):
                def __init__(self, parent):
                    super().__init__(
                        TITLE_IDLE,
                        quit_button="Quit",
                    )
                    self.parent = parent
                    self.menu = [
                        rumps.MenuItem("Status: Idle"),
                        None,  # separator
                        rumps.MenuItem("Edit Templates...", callback=self._open_templates),
                        rumps.MenuItem("Settings...", callback=self._open_settings),
                    ]

                def _open_templates(self, _sender):
                    if self.parent._template_editor is None:
                        from whisperheim.services.templates.template_editor import TemplateEditorWindow
                        self.parent._template_editor = TemplateEditorWindow(self.parent._template_service)
                    self.parent._template_editor.show()

                def _open_settings(self, _sender):
                    path = str(self.parent._settings_service._path)
                    rumps.alert(
                        title="Settings",
                        message=f"Edit settings at:\n{path}\n\nRestart the app after changes.",
                    )

            self._rumps_app = MenuBarApp(self)
            self._update_status("idle")
            logger.info("[App] Running as menu bar app. Hold hotkey to dictate.")
            self._rumps_app.run()

        except ImportError:
            logger.warning("[App] rumps not available, falling back to headless mode.")
            self._run_headless()

    def _run_headless(self) -> None:
        """Run as a headless CLI app (fallback for non-macOS or missing rumps)."""
        logger.info("[App] Running in headless mode. Hold hotkey to dictate. Ctrl+C to quit.")
        try:
            # Block forever (hotkey listener runs in background thread)
            threading.Event().wait()
        except KeyboardInterrupt:
            logger.info("[App] Shutting down...")
            self._shutdown()

    def _on_hotkey_activated(self) -> None:
        """Hotkey pressed — start recording."""
        if self._pipeline:
            self._pipeline.start_recording()

    def _on_hotkey_deactivated(self) -> None:
        """Hotkey released — stop recording and transcribe."""
        if self._pipeline:
            self._pipeline.stop_recording()

    def _on_recording_started(self) -> None:
        """Pipeline started recording."""
        self._update_status("recording")

    def _on_recording_stopped(self) -> None:
        """Pipeline stopped recording."""
        pass  # Status will change to transcribing or idle

    def _on_transcription_started(self) -> None:
        """Pipeline started transcribing."""
        self._update_status("transcribing")

    def _on_transcription_result(self, text: str) -> None:
        """Pipeline produced a transcription result — insert into active app."""
        logger.info("[App] Inserting text: \"%s\"", text)
        self._last_dictation_text = text
        self._text_inserter.insert_text(text)
        self._update_status("idle")

    def _on_transcription_error(self, error: str) -> None:
        """Pipeline encountered an error."""
        logger.error("[App] Transcription error: %s", error)
        self._update_status("idle")

    def _update_status(self, status: str) -> None:
        """Update the menu bar status indicator."""
        if self._rumps_app is None:
            return

        try:
            if status == "recording":
                self._rumps_app.title = TITLE_RECORDING
                if hasattr(self._rumps_app, "menu") and "Status: Idle" in self._rumps_app.menu:
                    pass  # Update status menu item
            elif status == "transcribing":
                self._rumps_app.title = TITLE_TRANSCRIBING
            else:  # idle
                self._rumps_app.title = TITLE_IDLE
        except Exception as e:
            logger.debug("[App] Error updating status: %s", e)

    def _shutdown(self) -> None:
        """Clean shutdown of all services."""
        if self._template_orchestrator:
            self._template_orchestrator.stop()
        self._hotkey_service.stop()
        if self._pipeline:
            self._pipeline.dispose()
        self._audio_capture.dispose()
        if self._transcription:
            self._transcription.dispose()
        logger.info("[App] Shutdown complete.")
