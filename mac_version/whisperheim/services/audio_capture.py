"""Audio capture service — 16kHz mono via sounddevice."""

import glob
import logging
import os
import sys
import threading
from typing import Callable, Optional

# When running inside a .app bundle, PortAudio dylib is in Frameworks/.
# Pre-load it via ctypes before sounddevice tries to find it.
if getattr(sys, "frozen", False):
    import ctypes
    _fw_dir = os.path.join(
        os.path.dirname(os.path.dirname(os.path.dirname(sys.executable))),
        "Frameworks",
    )
    _pa_libs = glob.glob(os.path.join(_fw_dir, "libportaudio*"))
    if _pa_libs:
        ctypes.cdll.LoadLibrary(_pa_libs[0])

import numpy as np
import sounddevice as sd

logger = logging.getLogger(__name__)

SAMPLE_RATE = 16000
CHANNELS = 1
BLOCK_SIZE = 1024  # ~64ms at 16kHz


class AudioCaptureService:
    """Captures microphone audio at 16kHz mono float32 using sounddevice.

    Audio data is delivered via a callback as float32 numpy arrays.
    """

    def __init__(self):
        self._stream: Optional[sd.InputStream] = None
        self._is_capturing = False
        self._lock = threading.Lock()
        self._on_audio_data: Optional[Callable[[np.ndarray], None]] = None

    @property
    def is_capturing(self) -> bool:
        return self._is_capturing

    def set_audio_callback(self, callback: Callable[[np.ndarray], None]) -> None:
        """Set the callback that receives float32 audio chunks."""
        self._on_audio_data = callback

    def start_capture(self, device_index: Optional[int] = None) -> None:
        """Start capturing audio from the microphone."""
        with self._lock:
            if self._is_capturing:
                return

            try:
                self._stream = sd.InputStream(
                    samplerate=SAMPLE_RATE,
                    channels=CHANNELS,
                    dtype="float32",
                    blocksize=BLOCK_SIZE,
                    device=device_index,
                    callback=self._audio_callback,
                )
                self._stream.start()
                self._is_capturing = True
                logger.info(
                    "[AudioCapture] Started capture: %dHz, %d channels, block=%d",
                    SAMPLE_RATE, CHANNELS, BLOCK_SIZE,
                )
            except Exception as e:
                logger.error("[AudioCapture] Failed to start capture: %s", e)
                self._stream = None
                raise

    def stop_capture(self) -> None:
        """Stop capturing audio."""
        with self._lock:
            if not self._is_capturing:
                return

            self._is_capturing = False

            if self._stream is not None:
                try:
                    self._stream.stop()
                    self._stream.close()
                except Exception as e:
                    logger.warning("[AudioCapture] Error stopping stream: %s", e)
                finally:
                    self._stream = None

            logger.info("[AudioCapture] Stopped capture.")

    def _audio_callback(self, indata: np.ndarray, frames: int, time_info, status) -> None:
        """sounddevice callback — called from audio thread."""
        if status:
            logger.warning("[AudioCapture] Stream status: %s", status)

        if self._on_audio_data is not None:
            # indata is (frames, channels); flatten to 1D float32
            samples = indata[:, 0].copy()
            self._on_audio_data(samples)

    def dispose(self) -> None:
        """Clean up resources."""
        self.stop_capture()
