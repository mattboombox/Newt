# World generation

New worlds are generated from a size preset, a 64-bit seed, and a target land
fraction. Identical options produce identical terrain.

## Current algorithm

1. Build a small number of broad, overlapping continental masses. Each mass uses
   several offset lobes so it has peninsulas and irregular edges rather than a
   single oval outline.
2. Walk a few semi-random volcanic chains across those foundations. Continental
   lobes stay comparatively low while the volcanic shields are narrow and steep,
   producing concentrated regional ridges without shrinking the landmass. A
   tightly focused summit boost raises and cools their cores without widening
   the shields, creating more snow-covered peaks. Chains remain anchored to the
   continental foundation so tiny offshore islands do not become bare summits.
3. Add several fixed-scale bands of smooth seeded elevation noise for bays,
   valleys, broken coastlines, and varied continental interiors, plus fine-scale
   relief that gives shores sharper inlets and projections. Larger presets add
   more continents and ranges instead of stretching these features.
4. Select an elevation threshold that approximates the requested land fraction.
5. Find the largest connected below-sea-level basin and place the world's single
   saltwater seed at its deepest tile. Smaller disconnected depressions remain
   dry inland basins rather than becoming separate saltwater oceans.
5. Build a seeded temperature field from latitude and elevation.
6. Classify physical landforms and saltwater coastlines.
7. Build moisture from saltwater distance, freshwater, elevation, and seeded
   regional variation.
8. Classify biomes and choose the visible lowland surface.
9. Seed spaced active volcanoes on mountain chains.
10. Select a small, spaced set of mountain tiles and start animated springs from them.

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

Press `M` to open the new-world menu. World size and map shape are selected
independently. The current shapes are Continents, Pangaea, Archipelago, Water World,
Ring World, and Earth. Earth supports every regular size by resampling the embedded
1280 × 642 NOAA relief grid; Large (320 × 192) is the practical default while
Massive retains the full source detail. Ring World keeps its fixed 1280 × 40
engineered dimensions.

Continents retains the balanced 38% land layout. Pangaea concentrates roughly
48% land into one dominant, heavily lobed supercontinent. Archipelago lowers land
to roughly 22% and scatters many small island groups through open ocean. Water World
keeps every tile submerged while retaining varied bathymetry and polar sea ice.

| Key | Preset | Dimensions |
| --- | --- | --- |
| 1 | Micro | 80 × 48 |
| 2 | Standard | 160 × 96 |
| 3 | Large | 320 × 192 |
| 4 | Huge | 640 × 311 |
| 5 | Ring World | 1280 × 40 |
| 6 | Earth | 1280 × 642 |
| 7 | Massive (stress test) | 1280 × 642 |

Ring World is an artificial megastructure conservatory rather than a planet.
Every large disconnected below-sea-level basin receives a saltwater source, while
small enclosed depressions remain available for freshwater lakes. The primary
source remains movable with OceanSeed; the other ocean sources remain fixed.

Its climate varies along the ring instead of by latitude. Repeating engineered
hot, cold, wet, and dry longitudinal sections create distinct conservatory
regions. Planetary seasons are permanently disabled for this preset.

Continuous steel-colored Ring World Wall tiles run along its top and bottom
edges. Their structural elevation is `2.15`, slightly above the normal `2.0`
terrain ceiling, and terrain-changing events cannot permanently erode them.

## Prototype controls

- `M`: open or close the world setup menu; use arrows and Enter to generate.
- `N`: increment the seed and generate a new world.
- `Q` / `E`: cycle backward or forward through tools in the current category.
- `R`: cycle tool categories.
- Other / Inspect: left click a critter to show its full entity details and keep
  the camera centered on it. Right click stops following. Inspection also clears
  automatically if the followed critter dies.
- Terrain Tools: Elevation, River, Volcano, Stone, and Lava. Stone temporarily
  covers one tile without changing elevation before the biome reclaims it;
  Lava covers one tile and deposits `+0.03` elevation. Right click clears
  either cover.
- Critter Tools: left click an empty valid habitat tile to spawn the selected
  Plankton, Jellyfish, Worm, Trilobite, Sea Scorpion, Nautilus, Squid, Squid Egg,
  Fish, Newt, Mega Toad, Therapsid, Monkey, Deer, Elk, Gazelle, Wolf, or Crab. The Plankton tool retains its stricter Deep Ocean
  requirement.
- Primitive evolution currently branches from Plankton to Jellyfish, Worm, or
  Trilobite. Worms continue to Fish or Nautilus, Nautilus to Squid, and Fish to
  Newt. Newts branch to Mega Toad or Therapsid, and Therapsids branch to Monkey,
  Deer, or Wolf; Deer continue to Elk or Gazelle.
  Trilobites continue to Crab or Sea Scorpion. Worms traverse Rivers and
  Freshwater Lakes as well as saltwater, and scavenge detritus in Deep Ocean,
  Shallows, Rivers, and unfrozen Freshwater Lakes;
  jellyfish consume plankton and worms on contact; fish locally pursue plankton,
  worms, and crabs, enter Rivers and Freshwater Lakes, and do not hunt Newts.
  Fish flee nearby Sea Scorpions, Squid, and Mega Toads before hunting. Crabs may
  roam and reproduce across all of their valid habitat, but feed only on Beaches
  and Shallows. They feed every eight seconds and breed at a low energy threshold,
  creating a rapidly renewing coastal food source for fish. Cold and freezing Beaches remain valid crab habitat, but ice sheets
  and other Arctic terrain do not. Trilobites graze deep-sea detritus in Ocean and Deep Ocean
  tiles. All non-Plankton critters can shove blocking Plankton into adjacent open
  water rather than losing their movement step; active Plankton hunters still eat them.
  Slow shelled Nautiluses hunt Plankton and are protected from Jellyfish
  and Sea Scorpions. Squid hunt fish, trilobites, crabs, newts,
  Nautiluses, and Sea Scorpions.
  They lay drifting eggs that move like Plankton and hatch when Squid prey comes
  within two tiles. Squid and Sea Scorpions resolve mutual attacks with a 50/50
  roll that deals one energy damage rather than instant predation. Sea Scorpions
  hunt fish, worms, trilobites, crabs, newts, and Squid across saltwater and
  Beaches, while
  Mega Toads hunt worms, trilobites, fish, crabs, Monkeys, Deer, Elk, and Gazelles. When
  another Toad and any non-Toad prey are both visible, they choose between those
  categories with equal probability. Newts remain behind ordinary non-Toad prey,
  while Therapsids remain a final fallback. Therapsids share the Mega Toad diet,
  and mutual Toad-Therapsid attacks use 50/50 one-energy combat rolls. Monkeys
  hunt Newts and Crabs and feed from Jungle foliage. Deer evolve as a second
  Therapsid branch and graze Grasslands and Forests; slower, more
  reproduction-intensive Elk evolve from Deer and can graze Grasslands, Tundra,
  and Taiga. Gazelles also evolve from Deer and graze Grasslands and Arid biomes.
  Wolves form a third Therapsid branch. They are fast hunters with the
  Mega Toad's broad diet, including Deer, Elk, and Gazelles, but engage Mega Toads and
  Therapsids only as last-resort combat targets. A Wolf's first reproduction
  selects a den site, preferring nearby Hills, and the Wolf must return there for
  every later reproduction. Reproduction stores charges instead of immediately
  producing pups; one charge creates one Wolf only when ordinary prey moves beside
  the den. Meteors, tsunami waves, and lava destroy dens and their stored charges.
  Building Tools can be cycled like Critter Tools; the Wolf Den entry places an
  empty den with left click and removes one with right click.
  Other / Jump Start enables life and fills every unoccupied Deep Ocean tile with
  one Plankton, preserving any critters already occupying those tiles.
  Monkeys, Deer, Elk, and Gazelles flee any
  nearby species capable of eating them. Predators gain food energy equal to half
  of the prey's maximum stomach capacity, rounded down with a minimum of one.
  Newts flee nearby Toads, and Toad breeding
  requires Rivers or unfrozen Freshwater Lakes.
  Newts live on land, feed in rivers, freshwater lakes, swamps, and
  jungles, and make one cached migration toward freshwater after they are born or evolved. Mega
  Toads hunt worms, fish, and newts locally and can enter land, shallows, and
  freshwater lakes, but not open ocean.
- Elevation tool: left click raises terrain and right click lowers it.
- Sea-level tool: left click raises the ocean and right click lowers it.
- Ocean-seed tool: click a tile to move the world's single saltwater source.
- Life tool: left click enables critters and ordinary biomes; right click makes
  the current and subsequently generated worlds lifeless. Lifeless worlds keep
  deserts, Arctic snow on every landform, and ice sheets but suppress other living biomes and
  do not seed plankton. Barren land appears as Stone Plains, Stone Hills, Stone
  Lowlands, Stone Canyons, or Stone Trenches. Re-enabling life lets ordinary
  biomes reclaim those surfaces gradually; beaches and mountains are never stone.
  Meteor and volcanic stone is the same biome-less state with a disturbance-specific
  recovery timer.
- Evolution Chance tool: left click adds 0.5 percentage points and right click
  subtracts 0.5, clamped from 0 to 100 percent. This controls whether offspring
  move one step down the evolution tree when they are born.
- Temperature tool: left click warms globally and right click cools globally.
- Moisture tool: left click makes the world wetter and right click makes it drier.

Select Earth in the world menu to generate it at the chosen size. Its elevation and bathymetry
come from a downsampled NOAA ETOPO 2022 ice-surface grid, with sea level preserved.
The source data are public domain under CC0-1.0. Land and ocean heights use separate
display scaling so both mountain ranges and ocean basins remain readable at tile scale.

- Volcano tool: left click spawns a new active volcano on an unoccupied tile.
- Meteor tool: left click strikes the pointed tile; right click cycles magnitude
  from `0.0` through `1.0` in `0.1` steps. The active magnitude appears in the HUD.
- Evolve event: left click a critter to move it one step down the evolution tree;
  right click moves it one step back toward its ancestor.
- Watershed Shift event: left click dries one naturally generated river system
  and starts a replacement on another eligible mountain. Rivers created with the
  River tool or `F` are player-owned and never dry from this event. Automatic
  Natural Events can also produce watershed shifts.
- River tool: left click starts a spring on any Mountain or Snowy Mountain;
  right click removes the connected river and lake system.

Elevation, SeaLevel, Temperature, Moisture, and Evolution Chance repeat while their mouse button is
held. The first repeat begins after 0.25 seconds and continues every 0.075 seconds.
OceanSeed, Volcano, Meteor, Watershed Shift, Evolve, and River remain single-click tools.
- `F`: create a spring on the hovered land tile and trace it downhill.
- Arrow keys or `WASD`: move the camera.
- Hold Shift while moving: pan four times faster.
- Mouse wheel: zoom toward or away from the tile beneath the pointer.
- Maps smaller than the available viewport are centered horizontally and
  vertically; larger maps retain normal panning and horizontal wrapping.
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
River basin searches follow a proven route toward ocean before selecting the
route's highest saddle as the lake surface. This lets large irregular craters fill
past their internal bumps. Each filled depression starts a downstream river at
its spill tile, and that river repeats the process without a channel-length cap
until it reaches ocean or joins another watercourse.
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
