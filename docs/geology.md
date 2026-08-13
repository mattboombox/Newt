# Geology and hydrology

Newt treats terrain as a visible classification derived from persistent elevation.
Geological operations change elevation first; terrain is then rebuilt from the
new physical state.

## First live operation: elevation tool

The initial category is World Tools and its first selection is Elevation. Left
click a world tile to raise a circular region, or right click to lower it. Both
directions use a smooth falloff: the center receives the full change while the
edge receives almost none. World X coordinates wrap, so a change at the left edge
continues naturally at the right edge. Terrain and coastlines are reclassified
immediately.

`Q` and `E` cycle tools. World Tools contains Elevation, SeaLevel, OceanSeed,
Temperature, Moisture, Seasons, Volcano, and River. Events contains Meteor,
Tsunami, and NaturalEvents. `R` cycles between the two categories.

Natural terrain events are deterministic and occur rarely in simulation time.
They create a random meteor impact or volcano roughly every 6 to 16 simulated
minutes. The NaturalEvents tool enables them with left click and disables them
with right click.

The Tsunami tool starts a wave only when left-clicked over ocean. Right click
cycles magnitude. It reuses the impact-wave animation with a blue front and
lowers the elevation of land reached by the wave.

`<` and `>` step the simulation rate through 0.25x, 0.5x, 1x, 2x, 4x,
8x, and 16x. `P` pauses or resumes simulation time without disabling tools or
camera controls. The current rate and pause state appear beside the world tick.

Temperature and Moisture are global climate tools. Left click adds 0.05 and right
click subtracts 0.05 from the corresponding world-wide climate offset. Each
offset is capped to the range -1.00 through +1.00, while final per-tile climate
values remain normalized from zero to one. Temperature changes can create or melt
sea ice; both tools immediately rebuild biome classifications.

Elevation, SeaLevel, Temperature, and Moisture may also be adjusted continuously
by holding the corresponding mouse button. Repetition starts after a short delay;
River and OceanSeed remain deliberate single-click actions.

The Seasons tool enables the hemisphere-opposed seasonal temperature cycle with
left click and disables it with right click. Disabling seasons immediately restores
the baseline temperatures for the current climate offset. Equatorial tiles are
unaffected by the seasonal temperature change.

## Sea-level tool

The SeaLevel tool uses the same mouse buttons as Elevation: left click raises the
global sea surface by 0.01 and right click lowers it by 0.01. Ground elevation is
not changed. Each world has one persistent saltwater seed, initially at the map
center. Sea water flood-fills outward from that tile wherever ground is at or
below sea level. The OceanSeed tool moves it to the clicked tile and immediately
rebuilds saltwater and freshwater. If the seed itself is above sea level there is
no ocean until the sea rises above it or the seed is moved. East and west wrap,
matching the world geometry.

Exposed seabed keeps its elevation and becomes Lowlands, Canyon, or Trench as it
gets progressively deeper below the original zero datum. Existing biome rules are
used on these dry landforms. Lakes are not created from rainfall or automatically
filled merely because a depression is below sea level.

Sea level is limited to the range -1.0 through +1.0. Ground elevation is limited
to -1.0 through +2.0. The mountain ceiling is high enough for elevation cooling to produce
Arctic mountain climate even at the equator.

Closed river basins form bounded terminal lakes instead of falling back to a
single wet tile. A terminal lake can cover at most 128 tiles. Its surface is set
by the lowest elevations needed to consume that area budget, without an
independent depth cap. This allows compact deep craters to fill while preventing
one spring from flooding a continent-sized shallow depression.

Freshwater lake rendering darkens strongly with local depth. The lake remains one
water body and biome influence; the gradient is a visual depth cue rather than a
separate shallow/deep freshwater terrain classification.

## Volcanism and surface recovery

Volcanic material is a temporary surface-cover layer rather than a biome or
landform replacement in simulation state:

```text
tile
  elevation: +0.412
  terrain: Hills
  biome: Forest
  surface cover: Lava
```

Lava visually and ecologically covers the biome, raises ground elevation by a
small randomized amount, then cools to Stone. Stone eventually clears to reveal
the climate-derived biome that remained underneath. No separate soil or pioneer
stage is modeled. Exposed Stone retains its landform label and shading, producing
names such as `Stone Plains`, `Stone Hills`, `Stone Canyon`, and `Stone Trench`.
Deeper landforms use progressively darker gray. Stone on a mountain uses the
normal Mountain or Snowy Mountain presentation because `Stone Mountain` would be
redundant.

An eruption combines one to three animated directional flow lobes with one to
three immediate airborne chunks. Each lobe advances one tile every five ticks,
preferring low ground while retaining directional momentum and a small random
turning chance. This approximates downhill lava without a world-scale fluid
solver. Airborne chunks land farther from the vent and create small irregular
outcrops. Deposits on submerged terrain cool three times faster.

Active volcanoes erupt periodically and eventually become Dormant. Dormant vents
can reawaken or become extinct. Extinction removes the volcano entity and leaves
a mountain at its former vent. Before removal it has a chance to spawn a new
Active vent on an adjacent tile, continuing its preferred direction and gradually
building a mountain ridge. Only active lava, stone, volcanoes, and flow fronts are
processed each tick; the system does not scan unaffected world tiles.

Warm, wet stone recovers faster than cold, dry stone. A river on or beside Stone
accelerates its remaining recovery approximately fourfold. Lava and Stone block
critter occupation until the underlying biome is exposed again.

## Basin-shaped lakes

Springs may originate on sufficiently high, wet mountain terrain. Water follows
the lowest neighboring elevation, using the wrapping world geometry.

A downhill-only walk is insufficient because it stops in a local depression. The
drainage algorithm therefore treats a basin as a container:

1. Follow the steepest available descent.
2. If no lower neighbor exists, find the lowest-elevation escape route from the
   depression using a bounded minimax search.
3. Use the highest point on that best route as the basin's spill elevation.
4. Flood-fill connected basin tiles below the spill elevation as a lake.
5. Record the spill tile as part of the lake calculation.
6. Continue the spring along the calculated overflow route toward the ocean.

This naturally produces lakes where terrain encloses water. A lake is not placed
randomly; its shape follows the elevation contours of its basin.

This is implemented. It determines lake size from topography rather than a random
radius. Search budgets prevent an accidental continent-scale or unbounded
calculation. When a flowing spring reaches a basin, the lake fills as one event
and the spring continues from its outlet. Lake budgets limit surface area rather
than depth, so compact deep craters can fill to their rim without allowing broad
shallow depressions to become unbounded lakes.

## First spring prototype

The initial downhill tracer is now available. Hover an above-sea-level land tile
and press `F`. The spring marks its source as river water and then extends by one
tile per fixed simulation tick. Each step chooses the lowest strictly lower
neighbor and stops when it:

- Reaches ocean or ocean shallows.
- Joins an existing river or freshwater lake.
- Encounters a closed basin.
- Reaches its safety length limit.

If the bounded outlet search cannot escape a depression, it becomes a terminal
inland lake.

Completed springs retain their source tile as persistent state. If an uplift
changes a tile occupied by a river or lake, freshwater is cleared and every
retained source is retraced immediately against the new elevation. The original
player-created spring still grows one tile per tick; only this response to geology
is instantaneous. A retraced river may divert, and its terminal lake may shrink,
expand, or move to a different basin.

New worlds also begin with a few animated springs. Their sources are seeded,
suitably spaced Mountain or Snowy Mountain tiles.
They obey the same tracing, basin filling, and persistent-source rules as springs
created by the player.

With the River tool selected, left click any Mountain or Snowy Mountain to start
an animated spring. The `F` shortcut uses the same rule.
Right click any freshwater tile to remove its connected river and lake system.
If tributaries have joined, every registered source feeding that connected system
is removed together; unrelated freshwater is redrawn and remains in the world.

Every river tile stores directional connections to the preceding and following
tiles. Rendering draws those connections through tile edges and corners, producing
one unbroken channel even when flow moves diagonally. Connectivity is simulation
state rather than a visual guess, so it can later support flow direction,
tributaries, bridges, navigation, and channel erosion.

## Water representation

Terrain, elevation, and surface water are separate properties:

Terrain Tools preserves that separation. Stone places a temporary surface cover
on only the clicked tile and does not alter its elevation; the biome eventually
reclaims it, with nearby rivers accelerating recovery. Lava places a temporary
cover on only the clicked tile and adds `0.03` elevation, matching a small volcanic
deposit. Right click with either tool clears the geological cover without undoing
any elevation previously deposited by lava.

```text
tile
  elevation: +0.184
  terrain: Plains
  biome: Grassland
  surface water: River
```

Ocean remains part of terrain classification because sea level submerges the base
landform globally. Rivers and lakes occupy a surface-water layer above land.

We do not need separate permanent terrain enums for shallow freshwater and deep
freshwater. Each lake stores a water-surface elevation. Its local depth is:

```text
water depth = lake surface elevation - ground elevation
```

Rendering and species habitat rules can classify that numeric depth as shoreline,
shallow, or deep when needed. Salinity remains independent: ocean water is salt,
while rivers and lakes are fresh. At a river mouth, the final river tile remains
freshwater and the adjacent ocean tile remains saltwater; later estuary rules can
model the mixing zone without corrupting either terrain type.

Freshwater lakes over Arctic biomes render with the same ice color as frozen sea
water. This is currently a visual treatment only: the lake retains its freshwater
identity, basin shape, depth, and moisture influence.

## Deliberate limits

Rivers remain single-tile connected channels. We are not adding discharge tiers,
variable widths, continuous bank erosion, sediment transport, or erosion-driven
meandering. Lake changes do not conserve or redistribute a previous lake's water
volume. Those mechanisms are interesting but would turn a supporting world system
into the game's dominant simulation.

Terrain can still change through explicit tectonic, volcanic, painting, or
discrete erosion events. Hydrology may be recalculated after those events rather
than modifying elevation continuously around every river.

See [ADR 0002](decisions/0002-bounded-hydrology.md) for the scope decision.

## Planned erosion

Erosion will modify elevation rather than randomly swapping terrain types:

- Steep exposed slopes lose material.
- Material moves toward lower neighbors.
- Valleys and lowlands receive sediment.
- Volcanic and tectonic terrain gradually softens without becoming static.

The same operations will eventually run quickly during world generation and more
slowly while the terrarium is alive.
