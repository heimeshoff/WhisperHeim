"""Global hotkey service — pynput hold-to-talk with key-down/key-up events."""

import logging
import threading
from typing import Callable, Optional, Set

from pynput import keyboard

from whisperheim.services.settings_service import HotkeySettings

logger = logging.getLogger(__name__)

# Map string names to pynput Key objects
_KEY_MAP = {
    "shift": keyboard.Key.shift,
    "shift_l": keyboard.Key.shift_l,
    "shift_r": keyboard.Key.shift_r,
    "ctrl": keyboard.Key.ctrl,
    "ctrl_l": keyboard.Key.ctrl_l,
    "ctrl_r": keyboard.Key.ctrl_r,
    "alt": keyboard.Key.alt,
    "alt_l": keyboard.Key.alt_l,
    "alt_r": keyboard.Key.alt_r,
    "cmd": keyboard.Key.cmd,
    "cmd_l": keyboard.Key.cmd_l,
    "cmd_r": keyboard.Key.cmd_r,
    "space": keyboard.Key.space,
    "tab": keyboard.Key.tab,
    "caps_lock": keyboard.Key.caps_lock,
    "f1": keyboard.Key.f1,
    "f2": keyboard.Key.f2,
    "f3": keyboard.Key.f3,
    "f4": keyboard.Key.f4,
    "f5": keyboard.Key.f5,
    "f6": keyboard.Key.f6,
    "f7": keyboard.Key.f7,
    "f8": keyboard.Key.f8,
    "f9": keyboard.Key.f9,
    "f10": keyboard.Key.f10,
    "f11": keyboard.Key.f11,
    "f12": keyboard.Key.f12,
}


def _resolve_key(name: str):
    """Resolve a key name to a pynput key."""
    name_lower = name.lower()
    if name_lower in _KEY_MAP:
        return _KEY_MAP[name_lower]
    # Try as single character
    if len(name) == 1:
        return keyboard.KeyCode.from_char(name)
    raise ValueError(f"Unknown key: {name}")


class HotkeyService:
    """Global hotkey listener for hold-to-talk dictation.

    Monitors a key combo (e.g., Cmd+Shift). When all required keys are
    held down simultaneously, fires on_activated. When any key is released,
    fires on_deactivated.

    Uses pynput which requires Accessibility permissions on macOS.
    """

    def __init__(self, settings: Optional[HotkeySettings] = None):
        settings = settings or HotkeySettings()

        # Resolve key names to pynput key objects
        self._trigger_key = _resolve_key(settings.key)
        self._modifier_keys = {_resolve_key(m) for m in settings.modifiers}
        self._all_keys = self._modifier_keys | {self._trigger_key}

        self._listener: Optional[keyboard.Listener] = None
        self._pressed_keys: Set = set()
        self._is_active = False
        self._lock = threading.Lock()

        # Callbacks
        self._on_activated: Optional[Callable[[], None]] = None
        self._on_deactivated: Optional[Callable[[], None]] = None

        logger.info(
            "[HotkeyService] Configured: trigger=%s, modifiers=%s",
            settings.key, settings.modifiers,
        )

    def set_callbacks(
        self,
        on_activated: Optional[Callable[[], None]] = None,
        on_deactivated: Optional[Callable[[], None]] = None,
    ) -> None:
        """Set activation/deactivation callbacks."""
        self._on_activated = on_activated
        self._on_deactivated = on_deactivated

    @property
    def is_active(self) -> bool:
        with self._lock:
            return self._is_active

    def start(self) -> None:
        """Start listening for hotkey events."""
        if self._listener is not None:
            return

        self._listener = keyboard.Listener(
            on_press=self._on_press,
            on_release=self._on_release,
        )
        self._listener.daemon = True
        self._listener.start()
        logger.info("[HotkeyService] Listening for hotkey.")

    def stop(self) -> None:
        """Stop listening for hotkey events."""
        if self._listener is not None:
            self._listener.stop()
            self._listener = None
            with self._lock:
                self._pressed_keys.clear()
                if self._is_active:
                    self._is_active = False
            logger.info("[HotkeyService] Stopped listening.")

    def _normalize_key(self, key):
        """Normalize key to match our configured keys."""
        # pynput may report Key.shift_l when we configured Key.shift
        # Map specific left/right variants to their generic form
        generic_map = {
            keyboard.Key.shift_l: keyboard.Key.shift,
            keyboard.Key.shift_r: keyboard.Key.shift,
            keyboard.Key.ctrl_l: keyboard.Key.ctrl,
            keyboard.Key.ctrl_r: keyboard.Key.ctrl,
            keyboard.Key.alt_l: keyboard.Key.alt,
            keyboard.Key.alt_r: keyboard.Key.alt,
            keyboard.Key.cmd_l: keyboard.Key.cmd,
            keyboard.Key.cmd_r: keyboard.Key.cmd,
        }
        return generic_map.get(key, key)

    def _on_press(self, key) -> None:
        """Handle key press event."""
        normalized = self._normalize_key(key)

        with self._lock:
            if normalized in self._all_keys:
                self._pressed_keys.add(normalized)

            # Check if all required keys are now pressed
            if not self._is_active and self._all_keys.issubset(self._pressed_keys):
                self._is_active = True
                logger.info("[HotkeyService] Hotkey activated.")
                if self._on_activated:
                    self._on_activated()

    def _on_release(self, key) -> None:
        """Handle key release event."""
        normalized = self._normalize_key(key)

        with self._lock:
            self._pressed_keys.discard(normalized)

            # If we were active and any required key was released, deactivate
            if self._is_active and not self._all_keys.issubset(self._pressed_keys):
                self._is_active = False
                logger.info("[HotkeyService] Hotkey deactivated.")
                if self._on_deactivated:
                    self._on_deactivated()
