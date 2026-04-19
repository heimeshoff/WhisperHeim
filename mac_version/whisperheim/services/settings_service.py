"""Settings service — JSON config in ~/Library/Application Support/WhisperHeim/settings.json."""

import json
import logging
import os
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Optional

logger = logging.getLogger(__name__)


def _app_support_dir() -> Path:
    """Return ~/Library/Application Support/WhisperHeim/."""
    return Path.home() / "Library" / "Application Support" / "WhisperHeim"


@dataclass
class HotkeySettings:
    """Hotkey configuration for hold-to-talk."""
    key: str = "shift"  # The key to hold (e.g., "shift", "ctrl", "alt")
    modifiers: list[str] = field(default_factory=lambda: ["cmd"])  # Modifier keys


@dataclass
class VadSettings:
    """Silero VAD configuration."""
    speech_threshold: float = 0.5
    min_speech_duration_ms: int = 250
    min_silence_duration_ms: int = 500
    window_size: int = 512
    sample_rate: int = 16000


@dataclass
class TranscriptionSettings:
    """Transcription configuration."""
    num_threads: int = 4
    sample_rate: int = 16000
    feature_dim: int = 80
    decoding_method: str = "greedy_search"


@dataclass
class DictationSettings:
    """Dictation pipeline configuration."""
    partial_result_interval_ms: int = 1500
    min_partial_audio_ms: int = 500
    sample_rate: int = 16000


@dataclass
class Settings:
    """Top-level application settings."""
    hotkey: HotkeySettings = field(default_factory=HotkeySettings)
    vad: VadSettings = field(default_factory=VadSettings)
    transcription: TranscriptionSettings = field(default_factory=TranscriptionSettings)
    dictation: DictationSettings = field(default_factory=DictationSettings)


class SettingsService:
    """Manages loading/saving settings from JSON."""

    def __init__(self, settings_path: Optional[Path] = None):
        self._path = settings_path or (_app_support_dir() / "settings.json")
        self._settings = Settings()
        self._load()

    @property
    def settings(self) -> Settings:
        return self._settings

    @property
    def app_support_dir(self) -> Path:
        return self._path.parent

    @property
    def models_dir(self) -> Path:
        return self._path.parent / "models"

    def save(self) -> None:
        """Save current settings to disk."""
        self._path.parent.mkdir(parents=True, exist_ok=True)
        with open(self._path, "w") as f:
            json.dump(asdict(self._settings), f, indent=2)
        logger.info("Settings saved to %s", self._path)

    def _load(self) -> None:
        """Load settings from disk, or use defaults if file doesn't exist."""
        if not self._path.exists():
            logger.info("No settings file found at %s, using defaults.", self._path)
            return

        try:
            with open(self._path) as f:
                data = json.load(f)

            if "hotkey" in data:
                self._settings.hotkey = HotkeySettings(**data["hotkey"])
            if "vad" in data:
                self._settings.vad = VadSettings(**data["vad"])
            if "transcription" in data:
                self._settings.transcription = TranscriptionSettings(**data["transcription"])
            if "dictation" in data:
                self._settings.dictation = DictationSettings(**data["dictation"])

            logger.info("Settings loaded from %s", self._path)
        except Exception as e:
            logger.warning("Failed to load settings from %s: %s. Using defaults.", self._path, e)
