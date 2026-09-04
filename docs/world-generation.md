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
5. Find the largest connected below-sea-level basin and place the world's primary
   saltwater seed at its deepest tile. Disconnected basins covering at least one
   percent of the map, with a 64-tile minimum, receive secondary saltwater seeds.
   Smaller enclosed depressions remain eligible freshwater lakes.
6. Build a seeded temperature field from latitude and elevation.
7. Classify physical landforms and saltwater coastlines.
8. Build moisture from saltwater distance, freshwater, elevation, and seeded
   regional variation.
9. Classify biomes and choose the visible lowland surface.
10. Seed spaced active volcanoes on mountain chains.
11. Select a small, spaced set of mountain tiles and start animated springs from them.

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

Natural spring selection is seeded and requests six sources on a Micro world,
fourteen on Small, and up to twenty-four on Standard and larger worlds. Fewer are used when the generated
snow line does not contain enough suitably spaced candidates. These springs use
the same tile-by-tile travel animation as springs created with `F`.

## Presets

Press `M` to open the new-world menu. World size and map shape are selected
independently. The current shapes are Continents, Pangaea, Archipelago, Water World,
Ring World, and Earth. Earth supports every regular size by resampling the embedded
1280 × 642 NOAA relief grid; Standard (320 × 192) is the default while
Huge retains the full source detail. Ring World keeps its fixed 1280 × 40
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
| 2 | Small | 160 × 96 |
| 3 | Standard (default) | 320 × 192 |
| 4 | Large | 640 × 311 |
| 5 | Huge (stress test) | 1280 × 642 |
| 6 | Ring World | 1280 × 40 |
| 7 | Earth | 1280 × 642 |

Ring World is an artificial megastructure conservatory rather than a planet.
Every large disconnected below-sea-level basin receives a saltwater source, while
small enclosed depressions remain available for freshwater lakes. The primary
source remains movable with OceanSeed; the other ocean sources remain fixed.
The OceanSeed tool can also add sources with right-click without moving the primary.
Oversized freshwater basins (more than 1,500 tiles) add a source automatically at
their lowest point if it is at or below sea level. Right-click an existing oversized
lake with OceanSeed to convert it by the same rule; this measurement excludes rivers.

Its climate varies along the ring instead of by latitude. Repeating engineered
hot, cold, wet, and dry longitudinal sections create distinct conservatory
regions. Planetary seasons are permanently disabled for this preset.

Continuous steel-colored Ring World Wall tiles run along its top and bottom
edges. Their structural elevation is `2.15`, slightly above the normal `2.0`
terrain ceiling, and terrain-changing events cannot permanently erode them.

## Prototype controls

The game starts in Critter Tools with Plankton selected.

- `M`: open or close the world setup menu; use arrows and Enter to generate. On
  the Seed row, type digits to replace the seed and use Backspace to edit it.
- `N`: increment the seed and generate a new world.
- `Q` / `E`: cycle backward or forward through tools in the current category.
- `R`: cycle tool categories.
- Events / Colonist: click a Village to choose a valid destination automatically,
  or click a valid distant tile to send a colonist there from the nearest Village.
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
  Fish, Newt, Mega Toad, Therapsid, Monkey, Ape, Deer, Elk, Gazelle, Wolf, Crab,
  Toothed Whale, or Baleen Whale. The Plankton tool retains its stricter Deep Ocean
  requirement.
- Primitive evolution currently branches from Plankton to Jellyfish, Worm, or
  Trilobite. Worms continue to Fish or Nautilus, Nautilus to Squid, and Fish to
  Newt. Newts branch to Mega Toad or Therapsid, Therapsids branch to Monkey,
  Deer, Wolf, or Toothed Whale, Toothed Whales continue to Baleen Whales, and
  Monkeys continue to Ape; Deer continue to Elk or Gazelle.
  Every natural offspring receives a thirty-second truce with its parent's
  species. The protection works in both directions, so mutant children cannot
  immediately eat their parents and parents cannot immediately eat mutants.
  Therapsid offspring use twice the configured evolution chance, capped at 100%,
  to support their Monkey, Deer, Wolf, and Toothed Whale descendant branches.
  Ambient feeding draws from finite tile nutrition. Land capacity is Jungle 6,
  Swamp 5, Forest 4, Grassland/Taiga 3, Bog/Arid 2, Tundra 1, and
  Desert/Arctic 0. Ice Sheets over Deep Ocean hold 1, while shallower Ice Sheets
  hold 0. Beaches are 0 freezing and 1 otherwise; Shallows are
  2 freezing, 3 cold, and 4 temperate/hot; Ocean is 0; Deep Ocean is 2 cold and
  1 otherwise. Rivers and Lakes both replace the underlying tile with a flat
  capacity of 2, independent of biome and temperature. Only Worms, Fish,
  and Newts consume this freshwater nutrition; Crabs and terrestrial foliage feeders may
  cross supported freshwater tiles but cannot graze natural or deposited food there.
  Tiles regenerate one unit
  every `480 / capacity` seconds. Even Jungle takes 80 seconds per unit, so dense
  terrain-feeder populations consume nutrients faster than occupied tiles recover. Nutrition updates
  lazily when queried, and hungry critters leave depleted feeding tiles instead of
  remaining indefinitely in dense stationary clusters. A critter that starves deposits
  one edible nutrient on its tile, including terrain with zero natural capacity; this
  uses a separate byte per tile and requires no additional world scan. Plankton photosynthesize,
  so their ambient feeding neither checks nor consumes tile nutrition. Complete
  Plankton extinction recovers one Deep Ocean Plankton after 15,000 ticks while life is enabled.
  Trilobites continue to Crab or Sea Scorpion. Worms share Trilobite survival behavior:
  they move every six seconds, inhabit marine water and Beaches, seek feeding terrain
  within five Manhattan tiles, and flee predators detected within three. They graze
  detritus in Deep Ocean, Shallows, Beaches, and Ice Sheets over Deep Ocean, carry up
  to seven energy, and reproduce at five energy for a cost of three. Squid and Sea Scorpions consume
  Worms only when adjacent, preserving most of the Fish and Nautilus branches.
  jellyfish consume only plankton on contact and leave worms and fish alone; fish consume or seek
  available terrain food first, then pursue Plankton as their only animal prey.
  Fish forage on their normal action in Rivers, Freshwater Lakes, and
  all Shallows, seek those tiles when hungry, remain while building
  breeding energy, place offspring across diagonal River connections, and do not hunt Newts. Fish and Mega Toads
  can shove blocking Newts into adjacent valid empty habitat.
  Fish flee nearby Sea Scorpions, Squid, and Mega Toads before hunting. Crabs may
  travel through ordinary Ocean, Shallows, Beaches, and non-Arctic land, but not
  Deep Ocean. They feed during normal actions on Beaches, Shallows, Rivers, Freshwater
  Lakes, Swamps, and Jungles, detect feeding terrain within five Manhattan tiles,
  move back toward it when hungry, and reproduce throughout valid habitat,
  creating a rapidly renewing feeder population. Every active predator except Fish consumes
  Crabs only on adjacent encounters and does not pursue them at range, including
  strikes across habitat boundaries. Cold and freezing Beaches and Ice Sheets remain valid crab habitat,
  while other Arctic terrain does not. Trilobites graze terrain nutrition in Deep Ocean
  and Shallows, including Ice Sheets over Deep Ocean, while ordinary Ocean remains transit-only, and flee predators detected
  within three Manhattan tiles. Larger critters can shove smaller blockers into adjacent open
  habitat rather than losing their movement step; Worms and Trilobites can push
  through chains of up to four Plankton in dense blooms. Active Plankton hunters still eat them.
  Slow shelled Nautiluses eat terrain food in Deep Ocean, Shallows, or deep-ocean Ice Sheets before hunting nearby Plankton,
  flee visible predators, and are protected from Jellyfish. Sea Scorpions and Squid
  consume Nautiluses only on adjacent encounters.
  Squid hunt fish, trilobites, crabs, newts, Sea Scorpions, Deer, Elk, and Gazelles
  in shared saltwater habitat. They consume Worms and Nautiluses only when already adjacent.
  They lay drifting eggs that move like Plankton and hatch when Squid prey comes
  within two tiles or when they enter Shallows. Squid and Sea Scorpions resolve mutual attacks with a 50/50
  roll; either species deals two energy damage when it wins. Sea Scorpions
  move every four seconds and hunt Fish, Crabs, Newts, Squid,
  Therapsids, Monkeys, Apes, Wolves, Ape Sailors, Deer, Elk, and Gazelles
  across saltwater and Beaches; they consume Worms, Trilobites, and Nautiluses only when adjacent, while
  Mega Toads hunt trilobites, fish, crabs, Monkeys, Deer, Elk, and Gazelles,
  choosing the nearest legal prey without species preference tiers. Therapsids hunt only Fish,
  including strikes against that prey in adjacent lakes, and move every
  six seconds, and use available Jungle, Swamp, or Arid forage within four tiles while
  below breeding energy. They eat terrain food on their normal action and hunt when
  no usable forage is available. They do not hunt Mega Toads or Wolves, but defend
  themselves with the standard 50% chance to win when either predator attacks.
  A Therapsid win deals one energy damage, while a win by a Toothed Whale, Sea
  Scorpion, Mega Toad, Mega Spider, Wolf, or Squid deals two. Apes remain
  one-damage combatants.
  Monkeys are non-predatory,
  feed and remain on Swamp, Jungle, or Forest foliage until ready to breed,
  and reproduce at eight energy for a cost of five. Deer evolve as a second
  Therapsid branch, move every three seconds, and graze Grasslands but not Forests; slower, more
  reproduction-intensive Elk evolve from Deer and can graze Grasslands, Tundra,
  and Taiga while moving every six seconds. Gazelles also evolve from Deer, move
  every three seconds, and graze Arid and Grassland
  biomes, but not Forests or Deserts.
  Deer remain valid inhabitants of Swamps and Jungles even though those wetland
  biomes do not feed them. Deer feed only on Grassland and Forest, Elk only on
  Grassland, Tundra, and Taiga, and Gazelles only on Grassland and Arid. These
  restrictions apply to natural and deposited nutrition alike.
  Deer, Elk, and Gazelles use the same open, habitable offspring placement rule
  as other ordinary species.
  Apes evolve from Monkeys, hunt all non-Plankton, non-Worm critters outside their own civilization in
  shared habitat, with assigned land Apes also hunting Worms when their home village has zero stored food.
  They forage from Swamp and Jungle foliage and must found or join a
  village before producing offspring. Assigned Apes stop hunting while their home
  village has at least ten stored food; unassigned Apes continue hunting normally.
  A founder spends its first reproduction event creating only a Village beside
  an available Grassland, Arid, or Beach district site. Rivers block placement of Villages
  and land districts, while Harbors are the sole exception. At five residents the village
  adds a Grassland or Arid Farm, Swamp Rice Paddy, Forest Orchard, or Shallows or
  Freshwater Lake Aquaculture when possible, otherwise
  a Harbor on a Beach or freshwater tile. Assigned Apes apply each kill's food energy to themselves until reaching reproduction
  energy, then carry only the remaining portion back to the Village or a connected district;
  a return that makes no progress for thirty seconds transfers its carried food
  automatically, preventing resident crowds from trapping food-bearing Apes forever;
  hungry assigned land Apes can consume one stored food from that village
  on a metabolism tick regardless of distance, so they do not need to return home to eat;
  capped villages alternate five-food Residential Districts with additional
  biome-appropriate food districts, falling back to housing when no connected
  Grassland, Arid, Swamp, Forest, or Shallows site remains. Grassland Farms, Rice Paddies, and
  Orchards produce one food every fourteen seconds; Arid Farms produce every twenty-eight
  seconds and use a drier ochre palette. Aquaculture works in Shallows and Freshwater
  Lakes, producing every thirty seconds in Hot water, thirty-four in Temperate, thirty-eight
  in Cold, and forty-four in Freezing water, and is rendered as a teal water pen. Hovering
  a production district reports its food or wood output per simulated minute.
  Construction strongly favors sites within
  four tiles of the Village. A Residential District increases capacity
  by five. Housing beyond the amount needed by the current population becomes a ruin
  after two simulated minutes of continuous underuse. Each village may add a Harbor later if its connected building network
  reaches an open Beach, River, or Freshwater Lake tile; freshwater Harbors must
  border dry land, including diagonally. Harbor construction can also connect
  diagonally to the village building network. Harbors recruit without changing total population,
  allowing one Sailor per five residents up to four at twenty residents. Sailors
  have 28 maximum energy and replenish by hunting, without automatic village refills.
  They move in all eight directions through Beach, saltwater, Rivers, and Freshwater
  Lakes, including diagonal freshwater Mountain corridors and horizontal map wrap. They never
  reproduce, hunt all implemented sea life except Plankton and Worms, and return their catches
  to a connected Harbor. A village may
  support one Harbor and one Lumber Camp initially, plus another of each for every 150 residents.
  Every 50 residents permits one Military District, which converts up to four civilian Apes into
  non-reproducing Ape Warriors without changing village population. Warriors hunt the species that
  prey on ordinary Apes and deal three energy damage on a successful combat roll. A Military District
  costs six wood; its first Warrior is free and later Warriors cost two wood. Infection immediately
  converts a Warrior into a regular sick Ape, preserving its identity and village membership. Villages
  remain more than twelve tiles apart.
  Village food storage is capped at five plus ten per Farm, Rice Paddy, Orchard, or Aquaculture district.
  Food districts inactive for more than two simulated years become ruins. When a village
  loses every resident, its Village and all connected buildings become unowned ruins.
  Ruins are non-operational, can be replaced by compatible Ape construction, and decay
  completely after two simulated years if they remain undisturbed.
  Every newly founded village starts with twenty of a maximum thirty wood. A three-food Lumber Camp
  can harvest connected Jungle every ten seconds, Forest every fourteen, Taiga every
  eighteen, or Swamp every twenty-four; low-yield Grassland and Arid camps produce every
  thirty-six seconds, including on biome-compatible Beaches. The first
  food district is free; later ones cost two wood.
  Residential Districts cost five food and four wood, Harbors and Military Districts cost six wood, and each
  Sailor after the Harbor's included first boat costs two wood.
  Seasonal biome changes leave Farms, Rice Paddies, Orchards, and Aquaculture standing but pause
  their production until Grassland or Arid returns for Farms, Swamp for Rice Paddies,
  Forest for Orchards, or Shallows or Freshwater Lakes for Aquaculture; districts inactive for over two
  simulated years become ruins. Housing that remains unneeded for two simulated minutes
  also becomes ruins. Coastline refreshes preserve
  Harbors through intermediate Beach reclassification.
  Villages with at least one hundred residents can rarely spend one hundred stored food to
  send an unaffiliated Ape settler with 100 energy to a valid distant village site. Settlers
  ignore predators and cross all non-Mountain land and water terrain, including sea ice
  and Stone, routing around Mountain barriers when a passable route exists. They push
  through blocking Plankton chains and shove Newts into nearby valid
  habitat; Mountains, Lava, and Ring World
  walls remain impassable. Their label and lighter Ape color persist
  until they found and join a new village. Founding resets the colonist to ordinary Ape
  starting energy and creates exactly one companion Ape; later growth requires normal
  reproduction. Clicking a Village with the Colonist tool
  chooses a distant site automatically; clicking a valid non-Village tile sends one
  there from the nearest Village. Inspection shows its destination X and Y coordinates.
  A Sailor left as a village's only resident for sixty simulated seconds converts into
  a same-ID colonist and seeks a valid distant site, leaving the old settlement as ruins.
  If no destination exists, it remains a Sailor and retries sixty seconds later.
  Terrestrial critters can traverse exposed Lowlands, Canyons, and Trenches.
  Wolves form a third Therapsid branch. They move every 2.5 seconds with the
  broad terrestrial diet, including Deer, Elk, and Gazelles. They engage
  Therapsids as ordinary vulnerable prey and never hunt Mega Toads, though
  a Toad that hunts a Wolf still initiates combat. Wolves and Mega Toads may eat
  adjacent Monkeys, including diagonal and horizontally wrapped neighbors, without
  prioritizing them or pursuing them at range. A Wolf's first reproduction
  selects a den site, preferring nearby Hills, and the Wolf must return there for
  every later reproduction. Reproduction stores up to five charges instead of immediately
  producing pups; once the den is full, later reproduction creates an adjacent Wolf
  while preserving all five charges. One charge creates one Wolf when ordinary prey
  moves beside the den. Stored charges decay by one every two simulation minutes,
  and an empty unassociated den disappears. Meteors, tsunami waves, and lava destroy
  dens and their stored charges immediately.
  Toothed Whales form the fourth Therapsid branch. They inhabit Deep Ocean, Ocean,
  and Shallows, move every four seconds, and scan seven tiles for animal-eating
  marine prey: Sea Scorpions, Nautiluses, Fish, Squid, and Ape Sailors. They also
  consume adjacent feeder Crabs. Large land animals become prey only while standing
  in Shallows; whales ignore them on land or in deeper water. They do not eat
  Jellyfish, Plankton, Worms, Squid Eggs, or smaller land animals.
  Baleen Whales evolve from Toothed Whales with the same body size, energy,
  metabolism, reproduction, movement timing, perception range, and saltwater
  habitat. They eat only Plankton.
  Building Tools can be cycled like Critter Tools; the Wolf Den entry places a
  den with one charge on left click and removes one with right click.
  Other / Jump Start enables life and fills every unoccupied Deep Ocean tile with
  one Plankton, preserving any critters already occupying those tiles.
  Other / Population opens a live, non-pausing window of extant species counts;
  each count has a colored population-history sparkline, and species disappear
  from the list when their population reaches zero.
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
  is limited to adjacent encounters. A cannibalistic meal restores eight, leaving
  the survivor one energy short of another birth.
  Newts live on land and feed in swamps, jungles, rivers, and freshwater lakes.
  They seek only nearby tiles with nutrition remaining and do not make a map-wide
  freshwater migration after birth or evolution. Mega
  Toads hunt fish and newts locally. Toads can enter land, shallows, and
  freshwater lakes, but not open ocean.
  Mega Spiders evolve from Sea Scorpions as huge land predators that also use
  Shallows. They hunt all other critters but never each other. Other predators
  fight them with the standard 50/50 combat roll instead of being eaten outright;
  Web-caught prey remains a guaranteed stored catch. Their first reproduction builds a single Web
  instead of offspring; later reproduction is normal while that Web survives.
  Passing non-spiders become stuck, prompting the owner to return and cache them
  as food that offsets later metabolism. Orphaned and terrain-invalid Webs vanish.
- Every species may occupy Ice Sheets: aquatic critters are treated as moving
  beneath them and land critters on top. Critters stranded by terrain or climate
  changes survive and move at one quarter speed toward nearby valid habitat. They
  never voluntarily enter invalid terrain from a valid tile.
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
- Meteor tool: left click strikes the pointed tile. Press `-` or `=` to adjust its
  magnitude from `0.0` through `1.0` in `0.1` steps. The active magnitude appears
  in the HUD.
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
- `H`: toggle the bottom HUD. When hidden, the map fills the full window.
- Maps smaller than the available viewport are centered horizontally and
  vertically; larger maps retain normal panning and horizontal wrapping.
- Hover a tile: inspect terrain, biome, water, elevation, temperature, moisture,
  and occupancy in the bottom HUD.

When visible, the bottom HUD reserves a fixed-height area below the map. Its World
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
Hovering any freshwater lake tile also shows `Lake size`: its connected tile count
and percentage of the whole map. Only lake tiles connected north, south, east, or
west are counted, including horizontal map wrap and frozen lake tiles. Rivers do
not contribute to the count or join separate lakes; river hover has no lake-size
row. Counts are cached per lake and refreshed when lake tiles change, so hovering
large lakes does not repeat a flood fill every frame. This is an inspection aid;
it does not add ocean seeds or change lake filling.

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
