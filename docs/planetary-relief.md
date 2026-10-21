# Planetary relief data

Moon and Mars use real elevation data rather than enlarged versions of the old
240 × 120 assets. Both embedded files contain 2560 × 1280 signed little-endian
16-bit elevations in meters, ordered north to south, longitude 0–360° east.
This exceeds every current Earth-style playable size, including Huge (1280 × 642).
Each asset occupies 6,553,600 bytes.

## Moon

- Source: NASA LRO LOLA `LDEM_16`, version 3.1, 5760 × 2880.
- [Image](https://imbrium.mit.edu/DATA/LOLA_GDR/CYLINDRICAL/IMG/LDEM_16.IMG)
- [PDS label](https://imbrium.mit.edu/DATA/LOLA_GDR/CYLINDRICAL/IMG/LDEM_16.LBL)
- Source format: little-endian signed 16-bit, 0.5 meters per unit, referenced
  to a 1737.4 km lunar radius. The radius offset is not added to terrain heights.
- Source SHA-256: `a511e40d7a3ea3275945b4da2a1df377133264fab0be94b7434b1cf8907254cb`
- Asset SHA-256: `9d87968529150b374133b591658a606c8fcedc5fc3e4fd1bb117ec73560b637b`

## Mars

- Source: NASA MGS MOLA `MEGT90N000EB`, version 2.0, 5760 × 2880.
- [Image](https://pds-geosciences.wustl.edu/mgs/mgs-m-mola-5-megdr-l3-v1/mgsl_300x/meg016/megt90n000eb.img)
- [PDS label](https://pds-geosciences.wustl.edu/mgs/mgs-m-mola-5-megdr-l3-v1/mgsl_300x/meg016/megt90n000eb.lbl)
- Source format: big-endian signed 16-bit meters relative to the Martian areoid.
- Source SHA-256: `d18d9b9ab8c5516d02e157dd2cde0f1d0d160c21940e953ba22391269a545e7b`
- Asset SHA-256: `e5123b22549c401e80b32c8f1d212c2058ed647dfbbcd530b89d7190b19f2c04`

## Conversion and game behavior

Run `python tools/build_planetary_relief.py` with NumPy and Pillow installed.
It downloads the images and labels into `TestResults/PlanetaryRelief/`, converts
their documented units and byte order, box-filters to 2560 × 1280, rounds to
whole meters, and writes the embedded resources. Longitude orientation is
preserved. The script prints source and output checksums.

The game samples cell centers at each selected size. Elevation scaling remains
6000 meters per game unit for the Moon and 10000 for Mars. Normal terrain colors,
climate, ocean basin seeding, and natural springs are retained. Moon and Mars
start with seasons enabled at the neutral equinox position, as Earth does.
