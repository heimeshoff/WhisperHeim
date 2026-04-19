"""Voice Activity Detection service using sherpa-onnx Silero VAD."""

import logging
import threading
from typing import Callable, Optional

import numpy as np
import sherpa_onnx

from whisperheim.services.settings_service import VadSettings

logger = logging.getLogger(__name__)


class VadService:
    """Silero VAD via sherpa-onnx — detects speech segments in audio.

    Processes audio in fixed-size windows and fires callbacks on speech
    start/end transitions. Speech end delivers the accumulated speech audio.
    """

    def __init__(self, model_path: str, settings: Optional[VadSettings] = None):
        settings = settings or VadSettings()
        self._settings = settings
        self._window_size = settings.window_size
        self._sample_rate = settings.sample_rate
        self._lock = threading.Lock()

        # Pending samples buffer (accumulates until we have a full window)
        self._pending: list[float] = []

        # State
        self._was_speech = False

        # Callbacks
        self._on_speech_started: Optional[Callable[[], None]] = None
        self._on_speech_ended: Optional[Callable[[np.ndarray], None]] = None

        # Create sherpa-onnx VAD
        config = sherpa_onnx.VadModelConfig()
        config.silero_vad.model = model_path
        config.silero_vad.threshold = settings.speech_threshold
        config.silero_vad.min_silence_duration = settings.min_silence_duration_ms / 1000.0
        config.silero_vad.min_speech_duration = settings.min_speech_duration_ms / 1000.0
        config.silero_vad.max_speech_duration = 30.0
        config.silero_vad.window_size = self._window_size
        config.sample_rate = settings.sample_rate
        config.num_threads = 1
        config.debug = False

        self._vad = sherpa_onnx.VoiceActivityDetector(config, buffer_size_in_seconds=60)

        logger.info(
            "[SileroVAD] Initialized: window=%d, threshold=%.2f, sample_rate=%d",
            self._window_size, settings.speech_threshold, settings.sample_rate,
        )

    def set_callbacks(
        self,
        on_speech_started: Optional[Callable[[], None]] = None,
        on_speech_ended: Optional[Callable[[np.ndarray], None]] = None,
    ) -> None:
        """Set speech start/end callbacks."""
        self._on_speech_started = on_speech_started
        self._on_speech_ended = on_speech_ended

    @property
    def is_speech_detected(self) -> bool:
        with self._lock:
            return self._was_speech

    def process_audio(self, samples: np.ndarray) -> None:
        """Feed audio samples to the VAD. Fires callbacks on speech transitions."""
        if len(samples) == 0:
            return

        with self._lock:
            self._pending.extend(samples.tolist())

            while len(self._pending) >= self._window_size:
                chunk = self._pending[:self._window_size]
                self._pending = self._pending[self._window_size:]

                self._vad.accept_waveform(np.array(chunk, dtype=np.float32))

                is_speech = self._vad.is_speech_detected()

                # Detect speech start
                if is_speech and not self._was_speech:
                    self._was_speech = True
                    logger.info("[SileroVAD] Speech started.")
                    if self._on_speech_started:
                        self._on_speech_started()

                # Check for completed speech segments
                while not self._vad.empty():
                    segment = self._vad.front
                    self._vad.pop()

                    self._was_speech = False
                    speech_audio = np.array(segment.samples, dtype=np.float32)

                    logger.info(
                        "[SileroVAD] Speech ended. Segment: %d samples (%.2fs)",
                        len(speech_audio),
                        len(speech_audio) / self._sample_rate,
                    )

                    if self._on_speech_ended:
                        self._on_speech_ended(speech_audio)

    def reset(self) -> None:
        """Reset VAD state."""
        with self._lock:
            self._pending.clear()
            self._was_speech = False
            self._vad.reset()

    def flush(self) -> None:
        """Flush any remaining audio through the VAD."""
        with self._lock:
            self._vad.flush()
            while not self._vad.empty():
                segment = self._vad.front
                self._vad.pop()
                speech_audio = np.array(segment.samples, dtype=np.float32)
                if self._on_speech_ended:
                    self._on_speech_ended(speech_audio)
