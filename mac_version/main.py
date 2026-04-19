"""WhisperHeim — macOS dictation app entry point.

Usage:
    python main.py              # Run the app
    python main.py --download   # Download models only
    python main.py --headless   # Run without menu bar UI
"""

import argparse
import logging
import sys

from whisperheim.services.model_manager import ModelManager
from whisperheim.services.settings_service import SettingsService


def setup_logging() -> None:
    """Configure logging to stderr with timestamps."""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s [%(levelname)s] %(message)s",
        datefmt="%H:%M:%S",
        stream=sys.stderr,
    )


def download_models() -> None:
    """Download models and exit."""
    settings = SettingsService()
    manager = ModelManager(settings.models_dir)

    if manager.are_models_downloaded():
        print("All models already downloaded.")
        return

    print("Downloading models...")
    manager.ensure_models(
        progress_callback=lambda msg: print(f"  {msg}")
    )
    print("Models downloaded successfully.")


def main() -> None:
    parser = argparse.ArgumentParser(description="WhisperHeim — macOS dictation app")
    parser.add_argument(
        "--download", action="store_true",
        help="Download models only, then exit",
    )
    parser.add_argument(
        "--headless", action="store_true",
        help="Run without menu bar UI",
    )
    args = parser.parse_args()

    setup_logging()

    if args.download:
        download_models()
        return

    from whisperheim.app import WhisperHeimApp

    app = WhisperHeimApp()

    if args.headless:
        # Force headless mode
        app._run_menubar = app._run_headless

    app.run()


if __name__ == "__main__":
    main()
