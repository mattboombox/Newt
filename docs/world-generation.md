# World generation

New worlds are generated from a size preset, a 64-bit seed, and a target land
fraction. Identical options produce identical terrain.

## Current algorithm

1. Begin with a flat elevation field.
2. Walk several semi-random volcanic chains across the wrapping world. Each step
   adds an overlapping shield-shaped cone, producing ridges, island arcs, and
   irregular volcanic uplands.
3. Select an elevation threshold that approximates the requested land fraction.
4. Carve a central ocean basin around the world's initial saltwater seed.
5. Build a seeded temperature field from latitude and elevation.
6. Classify physical landforms and saltwater coastlines.
7. Build moisture from saltwater distance, freshwater, elevation, and seeded
   regional variation.
8. Classify biomes and choose the visible lowland surface.
9. Seed spaced active volcanoes on mountain chains.
10. Select a small, spaced set of non-Arctic mountain tiles beside Arctic
   mountains and start animated snowmelt springs from them.

Elevation remains part of world state after generation. It is stored as a signed
absolute value against the original zero datum and compared with the mutable sea
level to determine submersion. Hovering a tile displays this
value together with normalized temperature, moisture, and its biome. Latitude
does not choose terrain directly; it affects temperature. Extreme cold produces
the Arctic biome on land and sea ice over saltwater without erasing landform.

The ancient shield-chain pass creates the initial geological history, while the
same world begins with live volcanoes that continue changing elevation after
generation. Each newly spawned volcano raises a compact mountain shoulder around
its vent and a lower skirt of hills. That footprint remains after dormancy or
extinction, allowing successive adjacent volcanoes to grow natural-looking ridges.

Natural spring selection is seeded and scales from two requested sources on a
Micro world to at most six on a Large world. Fewer are used when the generated
snow line does not contain enough suitably spaced candidates. These springs use
the same tile-by-tile travel animation as springs created with `F`.

## Presets

| Key | Preset | Dimensions |
| --- | --- | --- |
| 1 | Micro | 80 × 48 |
| 2 | Standard | 160 × 96 |
| 3 | Large | 252 × 130 |
| 4 | Ring World | 504 × 40 |

## Prototype controls

- `1`–`4`: generate the corresponding preset with the current seed.
- `N`: increment the seed and generate a new world.
- `Q` / `E`: cycle backward or forward through tools in the current category.
- `R`: cycle tool categories.
- Elevation tool: left click raises terrain and right click lowers it.
- Sea-level tool: left click raises the ocean and right click lowers it.
- Ocean-seed tool: click a tile to move the world's single saltwater source.
- Temperature tool: left click warms globally and right click cools globally.
- Moisture tool: left click makes the world wetter and right click makes it drier.
- Volcano tool: left click spawns a new active volcano on an unoccupied tile.
- Meteor tool: left click strikes the pointed tile; right click cycles magnitude
  from `0.0` through `1.0` in `0.1` steps. The active magnitude appears in the HUD.
- River tool: left click starts a spring and right click removes the connected
  river and lake system under the pointer.

Elevation, SeaLevel, Temperature, and Moisture repeat while their mouse button is
held. The first repeat begins after 0.25 seconds and continues every 0.075 seconds.
OceanSeed, Volcano, Meteor, and River remain single-click tools.
- `F`: create a spring on the hovered land tile and trace it downhill.
- Arrow keys or `WASD`: move the camera.
- Hold Shift while moving: pan four times faster.
- Mouse wheel: zoom.
- Hover a tile: inspect terrain, biome, water, elevation, temperature, moisture,
  and occupancy in the bottom HUD.

The bottom HUD always reserves a fixed-height area below the map. Its World
section shows the preset, dimensions, seed, tick, sea level, ocean seed, climate
offsets. Active Tool shows controls and shortcuts. Tile shows
the complete hovered-tile inspection. Map zoom changes only the map viewport and
clicks inside the HUD never activate world tools.

Tile identity combines biome and landform in natural reading order, such as
`Arid Plains` or `Forest Trench`. Mountains omit the biome name and display as
`Mountain`, or `Snowy Mountain` for Arctic peaks. Frozen saltwater displays as
`Ice Sheet`. Elevation is shown separately as a numeric value.
Freshwater lakes on Arctic tiles display as `Frozen Lake` in the water row while
retaining their freshwater identity and numeric depth.

Beaches use their temperature band rather than the inland biome label: `Freezing
Beach`, `Cold Beach`, `Temperate Beach`, or `Hot Beach`. Their sand palette shifts
from pale frozen shore through muted and temperate sand to warm golden sand.

Meteor impacts excavate an irregular circular basin, raise a stony rim, and lay
thinning ejecta around it. Small impacts form simple bowls. Large impacts develop
flatter, terraced floors and central peaks; the largest add a peak ring. Impact
melt enters the existing animated lava-flow and cooling system instead of
appearing all at once. Higher magnitudes also throw distant fragments that make
smaller secondary craters along a loose downrange corridor.

Each impact launches an expanding, slightly irregular circular shockwave. Its
strength fades with distance and removes critters as its visible front reaches
them. The wave itself does not erase biomes: only the crater, rim, and ejecta are
temporarily stripped to stone, so distant land keeps its biome identity.
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
