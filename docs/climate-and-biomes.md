# Climate and biomes

## Purpose

Newt uses two normalized climate fields to classify biologically useful biomes
on exposed land and temperature environments in the ocean:

- Temperature: `0.0` freezing to `1.0` hot.
- Moisture: `0.0` arid to `1.0` saturated.

These values are stored independently from elevation, terrain, and surface water,
although elevation contributes to both climate fields. A biome is an ecological
interpretation of physical conditions, not a replacement for those conditions.

```text
tile
  elevation: +0.142
  temperature: 0.61
  moisture: 0.84
  biome: Swamp
  surface water: None
```

## Elevation and landform

Elevation is not a third axis in the biome matrix. It has two narrower jobs:

- It contributes cooling and a modest drying effect to the climate fields.
- It classifies physical landform as plains, hills, or mountains.

The biome classifier itself receives only temperature and moisture. This avoids
counting elevation twice and permits combinations such as grassland hills, tundra
hills, or arid hills. At extreme cold, the Arctic biome changes the rendering to
snow cover without replacing the underlying landform.

The current elevation stages are:

| Elevation | Landform |
| --- | --- |
| `<= 0.00` | Submerged |
| `0.00-0.34` | Plains |
| `0.34-0.58` | Hills |
| `> 0.58` | Mountains |

Because climate uses the continuous elevation value, hills are naturally a little
colder and drier than nearby plains. There is no additional hidden "hill biome"
modifier.

## Temperature

Initial temperature is determined by:

```text
temperature = latitude warmth - elevation lapse + broad seeded variation
```

- The equator is warmest.
- The poles are coldest.
- Higher elevation is colder than nearby lowlands.
- Smooth seeded variation prevents perfectly uniform climate bands.

Temperature is clamped to `0.0–1.0`. A twelve-minute seasonal cycle adds a gentle, smooth
temperature offset in opposite directions in the northern and southern hemispheres.
The offset tapers to zero through the equatorial band, whose tiles display
`PermanentSummer`. Seasons currently change temperature, but not moisture. The
Seasons tool can disable the cycle and immediately restore baseline temperatures.
One complete seasonal cycle advances the displayed year by one. Disabling seasons
also pauses this seasonal calendar while ordinary simulation ticks continue.

The Life world tool is a separate global ecology switch. With life disabled,
plankton recovery and critter creation stop, existing critters are removed, and
biome classification retains Desert and Arctic on every landform. This keeps
climate-driven snow visible regardless of the Life setting. Physical ice-sheet
terrain remains unchanged. Biome-less plains, hills, lowlands,
canyons, and trenches are the Stone biome in presentation and inspection. None
retains its literal meaning on other terrain. Re-enabling life restores plankton
recovery immediately, then climate-appropriate biomes reclaim barren stone over
a deterministic 18-to-45-second interval. Beaches and mountains never receive
stone presentation. Meteor ejecta and cooled lava use that same biome-less state;
their surface-cover record only tracks when climate may reclaim the tile. The Life
setting is also applied to subsequently generated worlds.

The model does not simulate daily temperature, air masses, or atmospheric heat transport. Ocean moderation
can be considered later only if the simpler field proves visually insufficient.

All systems share these temperature thresholds:

| Value | Temperature band |
| --- | --- |
| `< 0.18` | Freezing |
| `0.18-0.33` | Cold |
| `0.33-0.67` | Temperate |
| `>= 0.67` | Hot |

## Ocean environments

Ocean depth and temperature remain independent. The terrain continues to record
Shallows, Ocean, or DeepOcean, while the shared temperature band supplies its
marine environment:

| Temperature band | Displayed ocean climate |
| --- | --- |
| Freezing | Freezing |
| Cold | Cold |
| Temperate | Temperate |
| Hot | Hot |

Saltwater below `0.12` becomes physical sea ice at every depth, including deep
ocean. Liquid water between `0.12` and `0.18` retains its depth terrain and uses
the Freezing climate label. Moisture is not used to classify oceans because the
tile is already water-covered.

Ice Sheets are valid habitat for every critter. Aquatic species are interpreted
as remaining beneath the ice, while terrestrial species travel across its surface.
Ice above water deeper than `0.20` retains one unit of deep-ocean nutrition for
Worms, Trilobites, and Nautiluses; shallower Ice Sheets have no natural nutrition.

Cold ocean tiles render five percent darker than temperate water, freezing water
ten percent darker, and warm water six percent lighter. These restrained changes
preserve the common ocean palette while making climate regions readable.

Depth and temperature can later combine in habitat rules without adding compound
terrain values. Warm shallows can support reefs or warm-water feeding grounds,
while cold shallows can support seasonal plankton blooms and different coastal
species.

Inspection does not repeat the word "ocean": examples are `Shallows, Hot`,
`Ocean, Cold`, `DeepOcean, Temperate`, and `Ice, Freezing`.

The numeric inspection values also include their bands, such as
`temperature 0.524 (Temperate), moisture 0.281 (Dry)`.

Elevation uses the same readable treatment: `elevation +0.412 (Hills)`. Ocean
tiles report Deep Ocean, Ocean, or Shallows; beaches report their underlying
land elevation stage.

For ordinary land, inspection shows the biome once and leaves Plains, Hills, or
Mountain beside the elevation value. Ocean, beach, and ice terrain labels remain
visible because they describe more than land elevation.

## Moisture

Initial moisture is determined by:

```text
moisture = baseline
         + ocean-distance influence
         + freshwater influence
         + broad seeded variation
         - elevation penalty
         - optional rain-shadow penalty
```

- Saltwater moisture decays inland using one multi-source distance calculation.
- Rivers create narrow riparian corridors, while lakes add stronger moisture over
  a wider area to favor wet lake shores and nearby jungle in hot climates.
- High exposed terrain receives a modest penalty.
- Smooth seeded variation produces broad wet and dry regions.

All systems share these moisture thresholds:

| Value | Moisture band |
| --- | --- |
| `< 0.33` | Dry |
| `0.33-0.67` | Normal |
| `>= 0.67` | Wet |

A simple static rain-shadow approximation may be added after the current climate
field is evaluated. It would inspect terrain upwind during climate generation;
Newt will not simulate clouds or rainfall particles.

## Biome matrix

Temperature has three biological bands plus an extreme freezing state; moisture
has three broad bands. Biomes are deliberately few and visually legible:

| Temperature | Moisture | Biome |
| --- | --- | --- |
| Freezing | Any | Arctic |
| Cold | Dry | Tundra |
| Cold | Normal | Taiga |
| Cold | Wet | Bog |
| Temperate | Dry | Grassland |
| Temperate | Normal | Forest |
| Temperate | Wet | Swamp |
| Hot | Dry | Desert |
| Hot | Normal | Arid |
| Hot | Wet | Jungle |

Arctic land remains Plains, Hills, or Mountain and is rendered with snow cover.
Its moisture value is retained even though Arctic is the readable biome at
freezing temperatures. Saltwater can still become physical sea ice. Beaches
remain saltwater coastal terrain. Freshwater shores inherit the surrounding
biome rather than receiving automatic ocean beaches.

This deliberately groups both frozen dry and frozen wet tiles under Arctic. Real
polar deserts exist, but a separate cold-desert biome would add little visual or
gameplay value at this stage. Moisture remains available if that distinction
becomes useful later.

The bog classification is a deliberate simplification. Real bogs depend on
drainage, acidity, and nutrients; cold plus wet is sufficient for Newt's readable
ecological model.

## Classification order

1. Latitude, elevation, and seeded variation build temperature.
2. Elevation determines submerged water, plains, hills, or mountains; extreme
   cold can create sea ice without replacing a landform.
3. Saltwater adjacency creates beaches and shallows.
4. Saltwater distance, freshwater, elevation, and seeded variation build
   moisture.
5. Temperature and moisture classify biomes; extreme freezing maps all land
   moisture bands to Arctic.
6. Plains and hills retain their landform and receive biome-specific colors.
7. Rivers and lakes remain surface-water overlays.

When a lake finishes filling, climate is rebuilt immediately even if its overflow
river is still traveling toward the ocean. Lakes formed on the same tick share
one rebuild. Climate is not rebuilt for every growing river tile, which keeps the
per-tick cost bounded.

## Gameplay consequences

Climate and biome will eventually influence:

- Species habitat and migration.
- Plant food availability.
- Farming suitability.
- Settlement growth and resource pressure.
- Mutation and evolutionary opportunity.
- The ecological aftermath of climate and geological events.

Biome classification should remain cheap and deterministic. Detailed soil,
nutrient, rainfall, and vegetation-succession simulation are outside the initial
scope.
