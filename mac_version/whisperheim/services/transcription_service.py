"""Transcription service — sherpa-onnx OfflineRecognizer with Parakeet TDT 0.6B."""

import logging
import os
import threading
import time
from dataclasses import dataclass
from typing import Optional

import numpy as np
import sherpa_onnx

from whisperheim.services.settings_service import TranscriptionSettings

logger = logging.getLogger(__name__)


@dataclass
class TranscriptionResult:
    """Result of a transcription operation."""
    text: str
    audio_duration_s: float
    transcription_duration_s: float
    real_time_factor: float


class TranscriptionService:
    """Transcribes audio using Parakeet TDT 0.6B via sherpa-onnx OfflineRecognizer.

    Thread-safe: transcription is guarded by a lock so the model
    is never accessed concurrently.
    """

    def __init__(self, settings: Optional[TranscriptionSettings] = None):
        self._settings = settings or TranscriptionSettings()
        self._recognizer: Optional[sherpa_onnx.OfflineRecognizer] = None
        self._lock = threading.Lock()

    @property
    def is_loaded(self) -> bool:
        return self._recognizer is not None

    def load_model(
        self,
        encoder_path: str,
        decoder_path: str,
        joiner_path: str,
        tokens_path: str,
    ) -> None:
        """Load the Parakeet TDT model. Must be called before transcribing."""
        if self._recognizer is not None:
            return

        for name, path in [
            ("encoder", encoder_path),
            ("decoder", decoder_path),
            ("joiner", joiner_path),
            ("tokens", tokens_path),
        ]:
            if not os.path.exists(path):
                raise FileNotFoundError(
                    f"Model file not found: {name} at '{path}'. "
                    "Ensure models have been downloaded via the Model Manager."
                )

        num_threads = min(self._settings.num_threads, os.cpu_count() or 4)

        logger.info(
            "[TranscriptionService] Loading Parakeet TDT 0.6B model (this may take 30-60s)..."
        )

        recognizer = sherpa_onnx.OfflineRecognizer.from_transducer(
            encoder=encoder_path,
            decoder=decoder_path,
            joiner=joiner_path,
            tokens=tokens_path,
            num_threads=num_threads,
            sample_rate=self._settings.sample_rate,
            feature_dim=self._settings.feature_dim,
            decoding_method=self._settings.decoding_method,
            provider="cpu",
            debug=False,
        )

        self._recognizer = recognizer
        logger.info(
            "[TranscriptionService] Parakeet TDT 0.6B loaded (threads=%d, provider=cpu)",
            num_threads,
        )

    def transcribe(self, samples: np.ndarray, sample_rate: int = 16000) -> TranscriptionResult:
        """Transcribe audio samples. Thread-safe via locking."""
        if self._recognizer is None:
            raise RuntimeError("Model not loaded. Call load_model() first.")

        if len(samples) == 0:
            return TranscriptionResult("", 0.0, 0.0, 0.0)

        audio_duration = len(samples) / sample_rate
        start = time.monotonic()

        with self._lock:
            stream = self._recognizer.create_stream()
            stream.accept_waveform(sample_rate, samples.tolist())
            self._recognizer.decode_stream(stream)
            text = stream.result.text.strip()

        elapsed = time.monotonic() - start
        rtf = elapsed / audio_duration if audio_duration > 0 else 0.0

        logger.info(
            "[TranscriptionService] Transcribed %.2fs audio in %.0fms (RTF=%.3f): \"%s\"",
            audio_duration, elapsed * 1000, rtf, text,
        )

        return TranscriptionResult(text, audio_duration, elapsed, rtf)

    def dispose(self) -> None:
        """Release model resources."""
        self._recognizer = None
