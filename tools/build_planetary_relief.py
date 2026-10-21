"""Build signed little-endian meter grids from NASA LOLA/MOLA PDS products.

Requires numpy and Pillow. Downloads are cached outside the embedded assets.
Run from any directory: python tools/build_planetary_relief.py
"""

from pathlib import Path
import hashlib
import urllib.request

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "TestResults" / "PlanetaryRelief"
DESTINATION = ROOT / "src" / "Newt.Simulation"
PRODUCTS = {
    "moon": (
        "https://imbrium.mit.edu/DATA/LOLA_GDR/CYLINDRICAL/IMG/LDEM_16",
        "<i2", 0.5,
    ),
    "mars": (
        "https://pds-geosciences.wustl.edu/mgs/mgs-m-mola-5-megdr-l3-v1/mgsl_300x/meg016/megt90n000eb",
        ">i2", 1.0,
    ),
}


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    for planet, (url, encoding, meter_scale) in PRODUCTS.items():
        for extension in ("img", "lbl"):
            target = CACHE / f"{planet}.{extension}"
            if not target.exists():
                suffix = extension.upper() if planet == "moon" else extension
                urllib.request.urlretrieve(f"{url}.{suffix}", target)
        raw = (CACHE / f"{planet}.img").read_bytes()
        if len(raw) != 5760 * 2880 * 2:
            raise ValueError(f"Unexpected {planet} source size: {len(raw)}")
        meters = np.frombuffer(raw, dtype=encoding).reshape(2880, 5760).astype(np.float32) * meter_scale
        # Both PDS products run north to south, east-positive 0..360 longitude.
        # Keep that orientation and average actual observations into a game-sized
        # grid, twice the largest playable width rather than upscaling old data.
        resized = Image.fromarray(meters).resize((2560, 1280), Image.Resampling.BOX)
        output = np.rint(np.asarray(resized)).astype("<i2").tobytes()
        destination = DESTINATION / f"{planet}-elevation-2560x1280.bin"
        destination.write_bytes(output)
        print(f"{planet}: {len(output)} bytes; source SHA256 {hashlib.sha256(raw).hexdigest()}; "
              f"asset SHA256 {hashlib.sha256(output).hexdigest()}")


if __name__ == "__main__":
    main()
