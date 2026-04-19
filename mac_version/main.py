"""WhisperHeim — macOS dictation app entry point.

Usage:
    python main.py              # Run the app
    python main.py --download   # Download models only
    python main.py --headless   # Run without menu bar UI
"""

import argparse
import logging
import os
import sys

from whisperheim.services.model_manager import ModelManager
from whisperheim.services.settings_service import SettingsService


def setup_logging() -> None:
    """Configure logging with timestamps.

    When running inside a .app bundle, logs go to a file in
    ~/Library/Application Support/WhisperHeim/whisperheim.log
    since there's no terminal. Otherwise, logs go to stderr.
    """
    log_kwargs = {
        "level": logging.INFO,
        "format": "%(asctime)s [%(levelname)s] %(message)s",
        "datefmt": "%H:%M:%S",
    }

    if _is_app_bundle():
        log_dir = os.path.expanduser(
            "~/Library/Application Support/WhisperHeim"
        )
        os.makedirs(log_dir, exist_ok=True)
        log_kwargs["filename"] = os.path.join(log_dir, "whisperheim.log")
        log_kwargs["filemode"] = "w"  # overwrite each launch
    else:
        log_kwargs["stream"] = sys.stderr

    logging.basicConfig(**log_kwargs)


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


def _is_app_bundle() -> bool:
    """Check if running inside a .app bundle (py2app)."""
    return getattr(sys, "frozen", False) or ".app/Contents" in (
        os.path.abspath(sys.argv[0]) if sys.argv else ""
    )


def main() -> None:
    # When launched from a .app bundle, macOS may inject extra argv
    # (e.g., -psn_* process serial number). Filter those out.
    if _is_app_bundle():
        sys.argv = [a for a in sys.argv if not a.startswith("-psn")]

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
