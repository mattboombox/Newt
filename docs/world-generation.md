# World generation

New worlds are generated from a size preset, a 64-bit seed, and a target land
fraction. Identical options produce identical terrain.

## Current algorithm

1. Begin with a flat elevation field.
2. Add several elliptical land masses whose X distance wraps around the world.
3. Select an elevation threshold that approximates the requested land fraction.
4. Build a seeded temperature field from latitude and elevation.
5. Classify physical landforms and saltwater coastlines.
6. Build moisture from saltwater distance, freshwater, elevation, and seeded
   regional variation.
7. Classify biomes and choose the visible lowland surface.

Elevation remains part of world state after generation. It is stored as a signed
value relative to sea level: zero is the shoreline threshold, negative values are
below sea level, and positive values are above it. Hovering a tile displays this
value together with normalized temperature, moisture, and its biome. Latitude
does not choose terrain directly; it affects temperature. Extreme cold produces
the Arctic biome on land and sea ice over saltwater without erasing landform.

This is intentionally not a recreation of the Python generator. It establishes a
deterministic foundation that can later accept tectonic plates, erosion, mineral
deposits, and geological history.

## Presets

| Key | Preset | Dimensions |
| --- | --- | --- |
| 1 | Micro | 80 × 48 |
| 2 | Standard | 160 × 96 |
| 3 | Large | 252 × 130 |
| 4 | Ring World | 252 × 40 |

## Prototype controls

- `1`–`4`: generate the corresponding preset with the current seed.
- `R`: increment the seed and generate a new world.
- `U`: uplift a soft circular region centered on the hovered tile.
- `F`: create a spring on the hovered land tile and trace it downhill.
- Arrow keys or `WASD`: move the camera.
- Hold Shift while moving: pan four times faster.
- Mouse wheel: zoom.
- Hover a tile: inspect terrain, terrestrial biome or ocean environment, water,
  elevation, temperature, and moisture in the window title.
- Escape: exit.

## Planned extensions

- Explicit world climate settings and rain-shadow evaluation.
- Plate-based mountain chains and tectonic boundaries.
- Watershed-scale drainage generation.
- Strategically meaningful mineral and fuel deposits.
- A generation report containing terrain proportions and timing.

See [climate and biomes](climate-and-biomes.md) for the temperature and moisture
fields that classify plains and uplands.

See [geology and hydrology](geology.md) for live elevation changes, springs,
rivers, lakes, and the planned erosion model.
