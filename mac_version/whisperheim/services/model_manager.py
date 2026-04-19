"""Model manager — downloads Parakeet TDT 0.6B + Silero VAD from HuggingFace."""

import logging
import os
from dataclasses import dataclass
from pathlib import Path
from typing import Optional

import requests

logger = logging.getLogger(__name__)


@dataclass
class ModelFile:
    """A single model file to download."""
    filename: str
    url: str
    expected_size_bytes: int  # Approximate, for progress/validation


@dataclass
class ModelDefinition:
    """A model consisting of one or more files."""
    name: str
    sub_directory: str
    files: list[ModelFile]


# Parakeet TDT 0.6B v3 int8 — same model as Windows WhisperHeim
PARAKEET_TDT_06B = ModelDefinition(
    name="Parakeet TDT 0.6B v3",
    sub_directory="parakeet-tdt-0.6b",
    files=[
        ModelFile(
            "encoder.int8.onnx",
            "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/encoder.int8.onnx",
            652_000_000,
        ),
        ModelFile(
            "decoder.int8.onnx",
            "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/decoder.int8.onnx",
            12_600_000,
        ),
        ModelFile(
            "joiner.int8.onnx",
            "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/joiner.int8.onnx",
            6_400_000,
        ),
        ModelFile(
            "tokens.txt",
            "https://huggingface.co/csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8/resolve/main/tokens.txt",
            9_600,
        ),
    ],
)

# Silero VAD model
SILERO_VAD = ModelDefinition(
    name="Silero VAD",
    sub_directory="silero-vad",
    files=[
        ModelFile(
            "silero_vad.onnx",
            "https://github.com/snakers4/silero-vad/raw/master/src/silero_vad/data/silero_vad.onnx",
            2_300_000,
        ),
    ],
)


class ModelManager:
    """Manages downloading and locating AI model files."""

    def __init__(self, models_dir: Path):
        self._models_dir = models_dir

    # --- Path accessors ---

    @property
    def parakeet_encoder_path(self) -> str:
        return str(self._models_dir / PARAKEET_TDT_06B.sub_directory / "encoder.int8.onnx")

    @property
    def parakeet_decoder_path(self) -> str:
        return str(self._models_dir / PARAKEET_TDT_06B.sub_directory / "decoder.int8.onnx")

    @property
    def parakeet_joiner_path(self) -> str:
        return str(self._models_dir / PARAKEET_TDT_06B.sub_directory / "joiner.int8.onnx")

    @property
    def parakeet_tokens_path(self) -> str:
        return str(self._models_dir / PARAKEET_TDT_06B.sub_directory / "tokens.txt")

    @property
    def silero_vad_path(self) -> str:
        return str(self._models_dir / SILERO_VAD.sub_directory / "silero_vad.onnx")

    # --- Download logic ---

    def ensure_models(self, progress_callback=None) -> None:
        """Download all required models if not already present."""
        self._ensure_model(PARAKEET_TDT_06B, progress_callback)
        self._ensure_model(SILERO_VAD, progress_callback)

    def are_models_downloaded(self) -> bool:
        """Check if all required model files exist."""
        for model in [PARAKEET_TDT_06B, SILERO_VAD]:
            model_dir = self._models_dir / model.sub_directory
            for mf in model.files:
                if not (model_dir / mf.filename).exists():
                    return False
        return True

    def _ensure_model(self, model: ModelDefinition, progress_callback=None) -> None:
        """Download a model's files if any are missing."""
        model_dir = self._models_dir / model.sub_directory
        model_dir.mkdir(parents=True, exist_ok=True)

        for mf in model.files:
            target = model_dir / mf.filename
            if target.exists():
                # Basic size check — if file is suspiciously small, re-download
                if target.stat().st_size > mf.expected_size_bytes * 0.5:
                    logger.info("[ModelManager] %s/%s already exists, skipping.", model.name, mf.filename)
                    continue
                else:
                    logger.warning(
                        "[ModelManager] %s/%s exists but is too small (%d bytes), re-downloading.",
                        model.name, mf.filename, target.stat().st_size,
                    )

            logger.info("[ModelManager] Downloading %s/%s from %s", model.name, mf.filename, mf.url)
            if progress_callback:
                progress_callback(f"Downloading {model.name}: {mf.filename}...")

            self._download_file(mf.url, target, mf.expected_size_bytes, progress_callback)

    @staticmethod
    def _download_file(url: str, target: Path, expected_size: int, progress_callback=None) -> None:
        """Download a file with progress reporting."""
        tmp_path = target.with_suffix(".tmp")

        try:
            response = requests.get(url, stream=True, timeout=600)
            response.raise_for_status()

            total = int(response.headers.get("content-length", expected_size))
            downloaded = 0

            with open(tmp_path, "wb") as f:
                for chunk in response.iter_content(chunk_size=1024 * 1024):  # 1 MB chunks
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)

                        if progress_callback and total > 0:
                            pct = min(100, int(downloaded * 100 / total))
                            progress_callback(
                                f"Downloading {target.name}... {pct}% "
                                f"({downloaded // (1024*1024)}/{total // (1024*1024)} MB)"
                            )

            # Atomic rename
            tmp_path.rename(target)
            logger.info("[ModelManager] Downloaded %s (%d bytes)", target.name, downloaded)

        except Exception:
            # Clean up partial download
            if tmp_path.exists():
                tmp_path.unlink()
            raise
