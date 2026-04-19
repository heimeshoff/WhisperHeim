"""Text insertion service — clipboard + CGEvent Cmd+V via PyObjC.

On macOS, the most reliable way to insert text into the active application
is to copy it to the clipboard and simulate Cmd+V. This avoids issues with
direct key simulation and works across all applications.

Requires Accessibility permissions.
"""

import logging
import platform
import time
from typing import Optional

logger = logging.getLogger(__name__)


def _is_macos() -> bool:
    return platform.system() == "Darwin"


class TextInserter:
    """Inserts text at cursor position using clipboard + Cmd+V.

    On macOS, uses PyObjC (NSPasteboard + CGEvent).
    On other platforms, falls back to a no-op with a warning.
    """

    def __init__(self):
        self._original_clipboard: Optional[str] = None

        if _is_macos():
            try:
                import AppKit
                import Quartz
                self._AppKit = AppKit
                self._Quartz = Quartz
                self._available = True
                logger.info("[TextInserter] PyObjC available, using clipboard+Cmd+V.")
            except ImportError:
                self._available = False
                logger.warning(
                    "[TextInserter] PyObjC not available. Text insertion will not work."
                )
        else:
            self._available = False
            logger.warning(
                "[TextInserter] Not running on macOS. Text insertion disabled."
            )

    def insert_text(self, text: str) -> None:
        """Insert text at the current cursor position."""
        if not text:
            return

        if not self._available:
            logger.warning("[TextInserter] Text insertion not available. Text: %s", text)
            return

        try:
            # Save current clipboard
            self._save_clipboard()

            # Set text to clipboard
            self._set_clipboard(text)

            # Small delay to ensure clipboard is updated
            time.sleep(0.05)

            # Simulate Cmd+V
            self._simulate_paste()

            # Small delay before restoring clipboard
            time.sleep(0.1)

            # Restore original clipboard
            self._restore_clipboard()

        except Exception as e:
            logger.error("[TextInserter] Failed to insert text: %s", e)
            # Try to restore clipboard even on error
            try:
                self._restore_clipboard()
            except Exception:
                pass

    def _save_clipboard(self) -> None:
        """Save current clipboard contents."""
        pb = self._AppKit.NSPasteboard.generalPasteboard()
        self._original_clipboard = pb.stringForType_(self._AppKit.NSPasteboardTypeString)

    def _set_clipboard(self, text: str) -> None:
        """Set clipboard to the given text."""
        pb = self._AppKit.NSPasteboard.generalPasteboard()
        pb.clearContents()
        pb.setString_forType_(text, self._AppKit.NSPasteboardTypeString)

    def _restore_clipboard(self) -> None:
        """Restore the previously saved clipboard contents."""
        if self._original_clipboard is not None:
            pb = self._AppKit.NSPasteboard.generalPasteboard()
            pb.clearContents()
            pb.setString_forType_(
                self._original_clipboard, self._AppKit.NSPasteboardTypeString
            )
            self._original_clipboard = None

    def _simulate_paste(self) -> None:
        """Simulate Cmd+V using CGEvent."""
        Quartz = self._Quartz

        # Key code for 'V' on macOS
        V_KEYCODE = 0x09

        # Create Cmd+V key down event
        event_down = Quartz.CGEventCreateKeyboardEvent(None, V_KEYCODE, True)
        Quartz.CGEventSetFlags(event_down, Quartz.kCGEventFlagMaskCommand)

        # Create Cmd+V key up event
        event_up = Quartz.CGEventCreateKeyboardEvent(None, V_KEYCODE, False)
        Quartz.CGEventSetFlags(event_up, Quartz.kCGEventFlagMaskCommand)

        # Post the events
        Quartz.CGEventPost(Quartz.kCGHIDEventTap, event_down)
        Quartz.CGEventPost(Quartz.kCGHIDEventTap, event_up)
