"""Dictation pipeline — audio capture -> VAD -> accumulate speech -> transcribe.

Orchestrates the hold-to-talk dictation flow:
1. User presses hotkey -> pipeline starts audio capture
2. Audio is fed to VAD for speech detection
3. Speech segments accumulate
4. User releases hotkey -> pipeline stops capture, transcribes accumulated audio
5. Transcribed text is delivered via callback

This is a simpler model than the Windows streaming pipeline because
hold-to-talk means we know exactly when the user is done speaking.
"""

import logging
import threading
import time
from typing import Callable, Optional

import numpy as np

from whisperheim.services.audio_capture import AudioCaptureService
from whisperheim.services.transcription_service import TranscriptionResult, TranscriptionService
from whisperheim.services.vad_service import VadService

logger = logging.getLogger(__name__)


class DictationPipeline:
    """Hold-to-talk dictation: capture -> VAD -> transcribe on release."""

    def __init__(
        self,
        audio_capture: AudioCaptureService,
        vad: VadService,
        transcription: TranscriptionService,
        sample_rate: int = 16000,
    ):
        self._audio = audio_capture
        self._vad = vad
        self._transcription = transcription
        self._sample_rate = sample_rate

        self._lock = threading.Lock()
        self._is_recording = False
        self._speech_buffer: list[np.ndarray] = []

        # Callbacks
        self._on_recording_started: Optional[Callable[[], None]] = None
        self._on_recording_stopped: Optional[Callable[[], None]] = None
        self._on_transcription_started: Optional[Callable[[], None]] = None
        self._on_transcription_result: Optional[Callable[[str], None]] = None
        self._on_transcription_error: Optional[Callable[[str], None]] = None

        # Wire up audio capture callback
        self._audio.set_audio_callback(self._on_audio_data)

        # Wire up VAD callbacks
        self._vad.set_callbacks(
            on_speech_started=self._on_speech_started,
            on_speech_ended=self._on_speech_ended,
        )

    def set_callbacks(
        self,
        on_recording_started: Optional[Callable[[], None]] = None,
        on_recording_stopped: Optional[Callable[[], None]] = None,
        on_transcription_started: Optional[Callable[[], None]] = None,
        on_transcription_result: Optional[Callable[[str], None]] = None,
        on_transcription_error: Optional[Callable[[str], None]] = None,
    ) -> None:
        """Set pipeline event callbacks."""
        self._on_recording_started = on_recording_started
        self._on_recording_stopped = on_recording_stopped
        self._on_transcription_started = on_transcription_started
        self._on_transcription_result = on_transcription_result
        self._on_transcription_error = on_transcription_error

    @property
    def is_recording(self) -> bool:
        with self._lock:
            return self._is_recording

    def start_recording(self) -> None:
        """Start recording audio (called on hotkey press)."""
        with self._lock:
            if self._is_recording:
                return
            self._is_recording = True
            self._speech_buffer.clear()
            self._vad.reset()

        try:
            self._audio.start_capture()
            logger.info("[DictationPipeline] Recording started.")
            if self._on_recording_started:
                self._on_recording_started()
        except Exception as e:
            with self._lock:
                self._is_recording = False
            logger.error("[DictationPipeline] Failed to start recording: %s", e)
            if self._on_transcription_error:
                self._on_transcription_error(f"Failed to start recording: {e}")

    def stop_recording(self) -> None:
        """Stop recording and transcribe accumulated speech (called on hotkey release)."""
        with self._lock:
            if not self._is_recording:
                return
            self._is_recording = False
            # Grab accumulated speech
            speech_chunks = list(self._speech_buffer)
            self._speech_buffer.clear()

        self._audio.stop_capture()
        logger.info("[DictationPipeline] Recording stopped.")

        if self._on_recording_stopped:
            self._on_recording_stopped()

        # Flush VAD to capture any trailing speech
        self._vad.flush()

        # Re-grab after flush (flush may have added segments)
        with self._lock:
            speech_chunks.extend(self._speech_buffer)
            self._speech_buffer.clear()

        if not speech_chunks:
            logger.info("[DictationPipeline] No speech detected.")
            return

        # Concatenate all speech segments
        all_speech = np.concatenate(speech_chunks)
        duration = len(all_speech) / self._sample_rate
        logger.info("[DictationPipeline] Transcribing %.2fs of speech audio.", duration)

        # Transcribe in a background thread
        thread = threading.Thread(
            target=self._transcribe_async,
            args=(all_speech,),
            daemon=True,
        )
        thread.start()

    def _on_audio_data(self, samples: np.ndarray) -> None:
        """Audio callback from capture service — feed to VAD."""
        with self._lock:
            if not self._is_recording:
                return
        self._vad.process_audio(samples)

    def _on_speech_started(self) -> None:
        """VAD detected speech start."""
        logger.debug("[DictationPipeline] VAD: speech started.")

    def _on_speech_ended(self, speech_audio: np.ndarray) -> None:
        """VAD detected speech end — accumulate the segment."""
        with self._lock:
            self._speech_buffer.append(speech_audio)
        logger.debug(
            "[DictationPipeline] VAD: speech segment accumulated (%.2fs).",
            len(speech_audio) / self._sample_rate,
        )

    def _transcribe_async(self, audio: np.ndarray) -> None:
        """Run transcription in background thread and deliver result."""
        try:
            if self._on_transcription_started:
                self._on_transcription_started()

            result = self._transcription.transcribe(audio, self._sample_rate)
            text = result.text.strip()

            if not text:
                logger.info("[DictationPipeline] Transcription returned empty text.")
                return

            logger.info(
                "[DictationPipeline] Result: \"%s\" (audio=%.2fs, transcribe=%.0fms, RTF=%.3f)",
                text, result.audio_duration_s,
                result.transcription_duration_s * 1000, result.real_time_factor,
            )

            if self._on_transcription_result:
                self._on_transcription_result(text)

        except Exception as e:
            logger.error("[DictationPipeline] Transcription error: %s", e)
            if self._on_transcription_error:
                self._on_transcription_error(str(e))

    def dispose(self) -> None:
        """Clean up resources."""
        self._audio.stop_capture()
