#!/bin/bash
# WhisperHeim macOS build script
# Produces dist/WhisperHeim.app
#
# Usage:
#   ./build.sh          # Full build (install deps + create .app)
#   ./build.sh --clean  # Remove previous build artifacts first
#
# Requirements:
#   - macOS 12+ with Xcode command line tools
#   - Python 3.10+ (python3 must be on PATH)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

APP_NAME="WhisperHeim"
VENV_DIR=".venv"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

info() { echo -e "${GREEN}[build]${NC} $*"; }
warn() { echo -e "${YELLOW}[build]${NC} $*"; }
error() { echo -e "${RED}[build]${NC} $*" >&2; }

# --- Pre-flight checks ---

if [[ "$(uname)" != "Darwin" ]]; then
    error "This script must be run on macOS."
    exit 1
fi

# Find the best Python version (prefer 3.12, then 3.11, then 3.10, avoid 3.14+)
PYTHON=""
for candidate in python3.12 python3.11 python3.10 python3.13 python3; do
    if command -v "$candidate" &>/dev/null; then
        PYTHON="$candidate"
        break
    fi
done

if [[ -z "$PYTHON" ]]; then
    error "Python 3.10+ not found. Install Python 3.12 from python.org/downloads"
    exit 1
fi

PYTHON_VERSION=$($PYTHON -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")')
PYTHON_MAJOR=$(echo "$PYTHON_VERSION" | cut -d. -f1)
PYTHON_MINOR=$(echo "$PYTHON_VERSION" | cut -d. -f2)

if [[ "$PYTHON_MAJOR" -lt 3 ]] || [[ "$PYTHON_MAJOR" -eq 3 && "$PYTHON_MINOR" -lt 10 ]]; then
    error "Python 3.10+ required, found $PYTHON_VERSION"
    exit 1
fi

if [[ "$PYTHON_MINOR" -ge 14 ]]; then
    warn "Python $PYTHON_VERSION detected — sherpa-onnx may not support it yet."
    warn "Recommended: install Python 3.12 from python.org/downloads"
fi

info "Using $PYTHON (Python $PYTHON_VERSION)"

# --- Clean if requested ---

if [[ "${1:-}" == "--clean" ]]; then
    info "Cleaning previous build artifacts..."
    rm -rf build dist .eggs *.egg-info
fi

# --- Virtual environment ---

if [[ ! -d "$VENV_DIR" ]]; then
    info "Creating virtual environment..."
    $PYTHON -m venv "$VENV_DIR"
fi

info "Activating virtual environment..."
source "$VENV_DIR/bin/activate"

# --- Install dependencies ---

info "Installing dependencies..."
pip install --upgrade pip setuptools wheel >/dev/null 2>&1
pip install -r requirements.txt >/dev/null 2>&1
pip install py2app >/dev/null 2>&1

# --- Generate placeholder icon if missing ---

if [[ ! -f "resources/WhisperHeim.icns" ]]; then
    info "Generating placeholder app icon..."
    mkdir -p resources
    python3 scripts/generate_icon.py
fi

# --- Find and include sherpa-onnx native libraries ---

info "Locating sherpa-onnx native libraries..."
SHERPA_LIBS=$(python3 -c "
import sherpa_onnx
import os
lib_dir = os.path.dirname(sherpa_onnx.__file__)
libs = []
for f in os.listdir(lib_dir):
    if f.endswith('.dylib') or f.endswith('.so'):
        libs.append(os.path.join(lib_dir, f))
# Also check lib subdirectory
sub_lib = os.path.join(lib_dir, 'lib')
if os.path.isdir(sub_lib):
    for f in os.listdir(sub_lib):
        if f.endswith('.dylib') or f.endswith('.so'):
            libs.append(os.path.join(sub_lib, f))
print(':'.join(libs))
")

if [[ -n "$SHERPA_LIBS" ]]; then
    info "Found sherpa-onnx libraries, will be bundled automatically via py2app."
else
    warn "No sherpa-onnx .dylib/.so files found — py2app will attempt to bundle from packages."
fi

# --- Build the .app ---

info "Building ${APP_NAME}.app..."
python3 setup.py py2app 2>&1 | tail -5

# --- Post-build: fix native libraries ---

if [[ -d "dist/${APP_NAME}.app" ]]; then
    RESOURCES="dist/${APP_NAME}.app/Contents/Resources"
    FRAMEWORKS="dist/${APP_NAME}.app/Contents/Frameworks"
    mkdir -p "$FRAMEWORKS"

    # Extract PortAudio dylib — sounddevice needs it as a real file, not inside a zip
    info "Fixing PortAudio library bundling..."
    PORTAUDIO_SRC=$(python3 -c "
import _sounddevice_data
import os, glob
base = os.path.dirname(_sounddevice_data.__file__)
libs = glob.glob(os.path.join(base, 'portaudio-binaries', 'libportaudio.*'))
print(libs[0] if libs else '')
" 2>/dev/null || true)

    if [[ -n "$PORTAUDIO_SRC" && -f "$PORTAUDIO_SRC" ]]; then
        cp "$PORTAUDIO_SRC" "$FRAMEWORKS/"
        info "Copied PortAudio to Frameworks/"
    else
        warn "Could not locate PortAudio dylib — sounddevice may not work."
    fi

    # Also copy any sherpa-onnx dylibs into Frameworks
    info "Fixing sherpa-onnx library bundling..."
    python3 -c "
import sherpa_onnx, os, shutil
lib_dir = os.path.dirname(sherpa_onnx.__file__)
fw = 'dist/${APP_NAME}.app/Contents/Frameworks'
for root, dirs, files in os.walk(lib_dir):
    for f in files:
        if f.endswith('.dylib') or f.endswith('.so'):
            src = os.path.join(root, f)
            dst = os.path.join(fw, f)
            if not os.path.exists(dst):
                shutil.copy2(src, dst)
                print(f'  Copied {f}')
" 2>/dev/null || warn "Could not copy sherpa-onnx libraries."
fi

if [[ -d "dist/${APP_NAME}.app" ]]; then
    echo ""
    info "Build successful!"
    info "App bundle: dist/${APP_NAME}.app"
    info ""
    info "To run:"
    info "  open dist/${APP_NAME}.app"
    info ""
    info "On first launch:"
    info "  - Models will download automatically (~670 MB)"
    info "  - Grant Microphone access when prompted"
    info "  - Grant Accessibility access in System Settings > Privacy & Security"
    echo ""

    # Show bundle size
    BUNDLE_SIZE=$(du -sh "dist/${APP_NAME}.app" | cut -f1)
    info "Bundle size: ${BUNDLE_SIZE}"
else
    error "Build failed — dist/${APP_NAME}.app not found."
    error "Check the output above for errors."
    exit 1
fi
