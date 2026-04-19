"""Generate a placeholder .icns app icon for WhisperHeim.

Creates a simple microphone icon using Core Graphics (macOS only).
Falls back to creating a minimal valid .icns from a solid-color PNG
if running on a system without Core Graphics.
"""

import os
import struct
import subprocess
import sys
import tempfile


def create_png_icon(size: int) -> bytes:
    """Create a minimal PNG with a colored background as placeholder.

    This creates a valid PNG without any external dependencies.
    Uses a dark blue/purple gradient-like appearance.
    """
    import zlib

    width = height = size

    # Build raw pixel data (RGBA)
    rows = []
    for y in range(height):
        row = b"\x00"  # filter byte (None)
        for x in range(width):
            # Dark gradient background
            r = int(40 + 30 * (y / height))
            g = int(30 + 20 * (y / height))
            b = int(80 + 60 * (y / height))
            a = 255

            # Simple microphone shape in the center
            cx, cy = width // 2, height // 2
            dx = abs(x - cx)
            dy = y - cy

            # Microphone body (rounded rectangle)
            mic_w = width // 6
            mic_h = height // 4
            if dx <= mic_w and -mic_h <= dy <= mic_h // 3:
                r, g, b = 220, 220, 230
            # Microphone base/stand
            elif dx <= width // 12 and mic_h // 3 < dy <= mic_h:
                r, g, b = 180, 180, 190
            # Small base
            elif dx <= mic_w and mic_h - 2 <= dy <= mic_h + 2:
                r, g, b = 180, 180, 190

            row += struct.pack("BBBB", r, g, b, a)
        rows.append(row)

    raw_data = b"".join(rows)

    # Build PNG file
    def make_chunk(chunk_type: bytes, data: bytes) -> bytes:
        chunk = chunk_type + data
        crc = zlib.crc32(chunk) & 0xFFFFFFFF
        return struct.pack(">I", len(data)) + chunk + struct.pack(">I", crc)

    png = b"\x89PNG\r\n\x1a\n"
    # IHDR
    ihdr_data = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    png += make_chunk(b"IHDR", ihdr_data)
    # IDAT
    compressed = zlib.compress(raw_data, 9)
    png += make_chunk(b"IDAT", compressed)
    # IEND
    png += make_chunk(b"IEND", b"")

    return png


def generate_icns_with_iconutil(output_path: str) -> bool:
    """Generate .icns using macOS iconutil (preferred method)."""
    try:
        with tempfile.TemporaryDirectory() as tmpdir:
            iconset_dir = os.path.join(tmpdir, "WhisperHeim.iconset")
            os.makedirs(iconset_dir)

            # Required icon sizes for .iconset
            sizes = [16, 32, 64, 128, 256, 512]
            for size in sizes:
                # 1x
                png_data = create_png_icon(size)
                with open(
                    os.path.join(iconset_dir, f"icon_{size}x{size}.png"), "wb"
                ) as f:
                    f.write(png_data)

                # 2x (retina)
                png_data_2x = create_png_icon(size * 2)
                with open(
                    os.path.join(iconset_dir, f"icon_{size}x{size}@2x.png"), "wb"
                ) as f:
                    f.write(png_data_2x)

            # Use iconutil to create .icns
            result = subprocess.run(
                ["iconutil", "-c", "icns", iconset_dir, "-o", output_path],
                capture_output=True,
                text=True,
            )

            if result.returncode == 0:
                return True
            else:
                print(f"iconutil failed: {result.stderr}", file=sys.stderr)
                return False

    except FileNotFoundError:
        return False


def generate_icns_fallback(output_path: str) -> None:
    """Generate a minimal .icns file without macOS tools.

    Creates a valid .icns with just a 32x32 icon.
    """
    png_data = create_png_icon(256)

    # .icns format: header + icon entries
    # ic08 = 256x256 PNG
    icon_type = b"ic08"
    entry_size = 8 + len(png_data)  # type(4) + size(4) + data
    entry = icon_type + struct.pack(">I", entry_size) + png_data

    # File header: 'icns' + total file size
    total_size = 8 + len(entry)
    icns_data = b"icns" + struct.pack(">I", total_size) + entry

    with open(output_path, "wb") as f:
        f.write(icns_data)


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    project_dir = os.path.dirname(script_dir)
    output_path = os.path.join(project_dir, "resources", "WhisperHeim.icns")

    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    print(f"Generating icon: {output_path}")

    # Try macOS iconutil first
    if sys.platform == "darwin" and generate_icns_with_iconutil(output_path):
        print("Icon generated using iconutil.")
    else:
        # Fallback: create minimal .icns directly
        generate_icns_fallback(output_path)
        if sys.platform != "darwin":
            print("Icon generated using fallback (not on macOS).")
        else:
            print("Icon generated using fallback method.")

    size = os.path.getsize(output_path)
    print(f"Icon size: {size:,} bytes")


if __name__ == "__main__":
    main()
