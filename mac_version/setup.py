"""py2app build configuration for WhisperHeim.

Usage:
    python setup.py py2app
"""

import os
import sys

from setuptools import setup

# Ensure we can find the package
sys.path.insert(0, os.path.dirname(__file__))

APP = ["main.py"]
APP_NAME = "WhisperHeim"

# py2app options
OPTIONS = {
    "argv_emulation": False,
    "iconfile": "resources/WhisperHeim.icns",
    "plist": {
        "CFBundleName": APP_NAME,
        "CFBundleDisplayName": APP_NAME,
        "CFBundleIdentifier": "com.heimeshoff.whisperheim",
        "CFBundleVersion": "1.0.0",
        "CFBundleShortVersionString": "1.0.0",
        "LSUIElement": True,  # Menu bar app — no Dock icon
        "NSMicrophoneUsageDescription": (
            "WhisperHeim needs microphone access to capture your speech for dictation."
        ),
        "NSAppleEventsUsageDescription": (
            "WhisperHeim needs Accessibility access to insert transcribed text."
        ),
    },
    "packages": [
        "whisperheim",
        "rumps",
        "pynput",
        "sounddevice",
        "numpy",
        "requests",
    ],
    "includes": [
        "sherpa_onnx",
        "AppKit",
        "Foundation",
        "Quartz",
        "objc",
    ],
    "frameworks": [],
    "resources": [],
    "excludes": [
        "tkinter",
        "unittest",
        "email",
        "html",
        "http",
        "xmlrpc",
        "doctest",
        "pydoc",
        "pdb",
    ],
    # Let py2app handle dylib bundling
    "strip": True,
    "semi_standalone": False,
}

setup(
    name=APP_NAME,
    app=APP,
    options={"py2app": OPTIONS},
    setup_requires=["py2app"],
)
