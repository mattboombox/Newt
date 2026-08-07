# Geology and hydrology

Newt treats terrain as a visible classification derived from persistent elevation.
Geological operations change elevation first; terrain is then rebuilt from the
new physical state.

## First live operation: radial uplift

Hover a world tile and press `U`. A circular region rises with a smooth falloff:
the center receives the full uplift while the edge receives almost none. World X
coordinates wrap, so an uplift at the left edge continues naturally at the right
edge. Terrain and coastlines are reclassified immediately.

This is a deliberately small primitive. Tectonic ridges, island chains, and
volcanic cones can later be composed from sequences of localized uplifts.

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
6. Stop the spring when the lake has formed.

This naturally produces lakes where terrain encloses water. A lake is not placed
randomly; its shape follows the elevation contours of its basin.

This is implemented. It determines lake size from topography rather than a random
radius. Search budgets prevent an accidental continent-scale or unbounded
calculation. When a flowing spring reaches a basin, the lake fills as one event
and the spring terminates. The spill calculation controls lake shape and depth but
does not create a second downstream river.

## First spring prototype

The initial downhill tracer is now available. Hover an above-sea-level land tile
and press `F`. The spring marks its source as river water and then extends by one
tile per fixed simulation tick. Each step chooses the lowest strictly lower
neighbor and stops when it:

- Reaches ocean or ocean shallows.
- Joins an existing river or freshwater lake.
- Encounters a closed basin.
- Reaches its safety length limit.

The result and river length appear in the window title. If the bounded outlet
search cannot escape a depression, it becomes a terminal inland lake.

Every river tile stores directional connections to the preceding and following
tiles. Rendering draws those connections through tile edges and corners, producing
one unbroken channel even when flow moves diagonally. Connectivity is simulation
state rather than a visual guess, so it can later support flow direction,
tributaries, bridges, navigation, and channel erosion.

## Water representation

Terrain, elevation, and surface water are separate properties:

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

## Deliberate limits

Rivers remain single-tile connected channels. We are not adding discharge tiers,
variable widths, continuous bank erosion, sediment transport, or erosion-driven
meandering. Those mechanisms are interesting but would turn a supporting world
system into the game's dominant simulation.

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
