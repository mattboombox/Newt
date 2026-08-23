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
engineered dimensions. Each game launch chooses a fresh initial seed. Select the
Seed row and type digits to replace it, or use Backspace and the left/right arrows
to edit it. Reusing the displayed seed keeps world generation deterministic.

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

- `M`: open or close the world setup menu; use arrows and Enter to generate. On
  the Seed row, type digits to replace the seed and use Backspace to edit it.
- `N`: increment the seed and generate a new world.
- `Q` / `E`: cycle backward or forward through tools in the current category.
- `R`: cycle tool categories.
- Other / Population: left click opens a live population window and right click
  closes it. The window lists only species whose current count is above zero.
- Other / Inspect: left click a critter to show its full entity details and keep
  the camera centered on it. Right click stops following. Inspection also clears
  automatically if the followed critter dies.
- Terrain Tools: Elevation, River, Volcano, Stone, and Lava. Stone temporarily
  covers one tile without changing elevation before the biome reclaims it;
  Lava covers one tile and deposits `+0.03` elevation. Right click clears
  either cover.
- Critter Tools: left click an empty valid habitat tile to spawn the selected
  Plankton, Jellyfish, Worm, Trilobite, Sea Scorpion, Nautilus, Squid, Squid Egg,
  Fish, Newt, Mega Toad, Therapsid, Monkey, Ape, Deer, Elk, Gazelle, Wolf, or Crab. The Plankton tool retains its stricter Deep Ocean
  requirement.
- Primitive evolution currently branches from Plankton to Jellyfish, Worm, or
  Trilobite. Worms continue to Fish or Nautilus, Nautilus to Squid, and Fish to
  Newt. Newts branch to Mega Toad or Therapsid, Therapsids branch to Monkey,
  Deer, or Wolf, and Monkeys continue to Ape; Deer continue to Elk or Gazelle.
  Every natural offspring receives a thirty-second truce with its parent's
  species. The protection works in both directions, so mutant children cannot
  immediately eat their parents and parents cannot immediately eat mutants.
  Therapsid offspring use twice the configured evolution chance, capped at 100%,
  to support their Monkey, Deer, and Wolf descendant branches.
  Ambient feeding draws from finite tile nutrition. Land capacity is Jungle 6,
  Swamp 5, Forest 4, Grassland/Taiga 3, Bog/Arid 2, Tundra 1, and
  Desert/Arctic/Ice Sheet 0. Beaches are 0 freezing and 1 otherwise; Shallows are
  2 freezing, 3 cold, and 4 temperate/hot; Ocean is 0; Deep Ocean is 2 cold and
  1 otherwise. Rivers and Lakes both replace the underlying tile with a flat
  capacity of 2, independent of biome and temperature. Tiles regenerate one unit
  every `480 / capacity` seconds. Even Jungle takes 80 seconds per unit, so dense
  terrain-feeder populations consume nutrients faster than occupied tiles recover. Nutrition updates
  lazily when queried, and hungry critters leave depleted feeding tiles instead of
  remaining indefinitely in dense stationary clusters. A critter that starves deposits
  one edible nutrient on its tile, including terrain with zero natural capacity; this
  uses a separate byte per tile and requires no additional world scan. Plankton photosynthesize,
  so their ambient feeding neither checks nor consumes tile nutrition.
  Trilobites continue to Crab or Sea Scorpion. Worms move every eight seconds,
  four times slower than Fish, traverse Rivers and
  Freshwater Lakes as well as saltwater, and scavenge detritus in Deep Ocean,
  Shallows, Rivers, and unfrozen Freshwater Lakes;
  jellyfish consume only plankton on contact and leave worms and fish alone; fish consume or seek
  available terrain food first, then pursue Plankton as their only animal prey.
  Fish can shove smaller worms into adjacent valid habitat, but blocked worms survive, and
  forage on their normal action in Rivers, Freshwater Lakes, and
  all Shallows, seek those tiles when hungry, remain while building
  breeding energy, place offspring across diagonal River connections, and do not hunt Newts. Fish and Mega Toads
  can shove blocking Newts into adjacent valid empty habitat.
  Fish flee nearby Sea Scorpions, Squid, and Mega Toads before hunting. Crabs may
  travel through ordinary Ocean, Shallows, Beaches, and non-Arctic land, but not
  Deep Ocean. They feed during normal actions on Beaches and Shallows, remain there
  while building energy, and reproduce directly on either coastal terrain,
  creating a rapidly renewing coastal population. Fish do not eat Crabs. Cold and freezing Beaches remain valid crab habitat, but ice sheets
  and other Arctic terrain do not. Trilobites graze terrain nutrition in Deep Ocean
  and Shallows, while ordinary Ocean remains transit-only, and flee predators detected
  within three Manhattan tiles. Larger critters can shove smaller blockers into adjacent open
  habitat rather than losing their movement step; Worms and Trilobites can push
  through chains of up to four Plankton in dense blooms. Active Plankton hunters still eat them.
  Slow shelled Nautiluses eat Deep Ocean or Shallows terrain food before hunting the nearest legal prey,
  flee visible predators, and are protected from Jellyfish
  and Sea Scorpions. Squid hunt fish, trilobites, crabs, newts,
  Nautiluses, Sea Scorpions, Deer, Elk, and Gazelles in shared saltwater habitat.
  They lay drifting eggs that move like Plankton and hatch when Squid prey comes
  within two tiles or when they enter Shallows. Squid and Sea Scorpions resolve mutual attacks with a 50/50
  roll that deals one energy damage rather than instant predation. Sea Scorpions
  move every four seconds and hunt fish, worms, trilobites, crabs, newts, Squid, Deer, Elk, and Gazelles
  across saltwater and Beaches, while
  Mega Toads hunt worms, trilobites, fish, crabs, Monkeys, Deer, Elk, and Gazelles,
  choosing the nearest legal prey without species preference tiers. Therapsids hunt only Worms, Fish, and
  Newts, including strikes against those prey in adjacent lakes, and move every
  six seconds, and use available Jungle, Swamp, or Arid forage within four tiles while
  below breeding energy. They eat terrain food on their normal action and hunt when
  no usable forage is available. They do not hunt Mega Toads or Wolves, but defend
  themselves with a 20% chance to win each one-damage combat exchange when either
  predator attacks.
  Monkeys are non-predatory,
  feed and remain on Swamp, Jungle, or Forest foliage until ready to breed,
  and reproduce at eight energy for a cost of five. Deer evolve as a second
  Therapsid branch, move every three seconds, and graze Grasslands but not Forests; slower, more
  reproduction-intensive Elk evolve from Deer and can graze Grasslands, Tundra,
  and Taiga while moving every six seconds. Gazelles also evolve from Deer, move
  every three seconds, and graze Arid and Grassland
  biomes, but not Forests or Deserts.
  Deer remain valid inhabitants of Swamps and Jungles even though those wetland
  biomes do not feed them.
  Deer, Elk, and Gazelles use the same open, habitable offspring placement rule
  as other ordinary species.
  Apes evolve from Monkeys, hunt all critters outside their own civilization in
  shared habitat, and must found or join a village before producing offspring.
  A founder spends its first reproduction event creating only a Village beside
  an available Grassland or Beach district site. At five residents the village
  adds a Grassland Farm, Swamp Rice Paddy, or Forest Orchard when possible, otherwise
  a Beach Harbor. Assigned Apes apply each kill's food energy to themselves until reaching reproduction
  energy, then carry only the remaining portion back to the Village or a connected district;
  a return that makes no progress for thirty seconds transfers its carried food
  automatically, preventing resident crowds from trapping food-bearing Apes forever;
  hungry assigned Apes and Ape Sailors can consume one stored food from that village
  on a metabolism tick regardless of distance, so they do not need to return home to eat;
  capped villages alternate five-food Residential Districts with additional
  biome-appropriate food districts, falling back to housing when no connected
  Grassland, Swamp, or Forest site remains; Farms, Rice Paddies, and Orchards each
  produce one food every fourteen seconds. A Residential District increases capacity
  by five. Each village may add one Harbor later if its connected building network
  reaches an open Beach tile. Harbors recruit without changing total population,
  allowing one Sailor per five residents up to four at twenty residents. Sailors
  draw from village food every tick until their energy is full, traverse Beach and
  saltwater, never reproduce, and hunt all implemented
  sea life except Plankton, and return their catches to a connected Harbor. Villages
  remain more than twelve tiles apart.
  Village food storage is capped at five plus ten per Farm, Rice Paddy, or Orchard.
  Villages start with six of a maximum thirty wood. A single three-food Lumber Camp
  can harvest connected Jungle every ten seconds, Forest every fourteen, or Taiga
  every eighteen. The first food district is free; later ones cost two wood.
  Residential Districts cost five food and four wood, Harbors cost six wood, and each
  Sailor after the Harbor's included first boat costs two wood.
  Terrestrial critters can traverse exposed Lowlands, Canyons, and Trenches.
  Wolves form a third Therapsid branch. They are fast hunters with the
  broad terrestrial diet, including Deer, Elk, and Gazelles. They engage
  Therapsids as ordinary vulnerable prey and never hunt Mega Toads, though
  a Toad that hunts a Wolf still initiates combat. A Wolf's first reproduction
  selects a den site, preferring nearby Hills, and the Wolf must return there for
  every later reproduction. Reproduction stores up to five charges instead of immediately
  producing pups; once the den is full, later reproduction creates an adjacent Wolf
  while preserving all five charges. One charge creates one Wolf when ordinary prey
  moves beside the den. Stored charges decay by one every two simulation minutes,
  and an empty unassociated den disappears. Meteors, tsunami waves, and lava destroy
  dens and their stored charges immediately.
  Building Tools can be cycled like Critter Tools; the Wolf Den entry places a
  den with one charge on left click and removes one with right click.
  Other / Jump Start enables life and fills every unoccupied Deep Ocean tile with
  one Plankton, preserving any critters already occupying those tiles.
  Other / Population opens a live, non-pausing window of extant species counts;
  species disappear from the list when their population reaches zero.
  Monkeys, Deer, Elk, and Gazelles flee any
  nearby species capable of eating them. Six body-size levels determine both
  displacement and prey energy: Tiny 1, Small 2, Medium 3, Big 4, Large 5, and Huge 8.
  Every critter except Squid Eggs can shove Plankton, while other displacement still
  requires the mover to be larger than the blocker. If a Plankton cannot be moved
  directly or through a chain of four, the shove kills it; only legal Plankton eaters
  gain meal energy from that death.
  A lethal displacement counts as a meal only when the mover can legally eat the blocker.
  Newts flee nearby Toads. Reproduction has no species-specific terrain gate.
  Mega Toad reproduction requires fourteen energy and costs nine. Both a new
  offspring and its threshold-level parent have five energy, while cannibalism
  restores eight, leaving the survivor one energy short of another birth.
  Newts live on land and feed in swamps, jungles, rivers, and freshwater lakes.
  They seek only nearby tiles with nutrition remaining and do not make a map-wide
  freshwater migration after birth or evolution. Mega
  Toads hunt worms, fish, and newts locally. Toads can enter land, shallows, and
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
