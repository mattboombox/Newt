# ADR 0002: Bounded hydrology

Status: Accepted

## Context

Elevation and animated downhill springs make naturally located freshwater
possible. Full hydrological simulation could also introduce discharge, river-width
classes, bank erosion, sediment transport, flooding, and channel meandering. Those
systems would be expensive to implement, difficult to explain, and capable of
dominating both performance and game design.

## Decision

Newt will model:

- Single-tile connected rivers.
- River confluences and ocean mouths.
- Lakes that fill closed elevation basins.
- Lake surface elevation and derived local depth.
- A computed spill elevation that determines lake shape and depth.

Newt will not initially model:

- Multiple river width or flow-volume tiers.
- Continuous riverbank or channel erosion.
- Sediment transport.
- Natural meandering produced by erosion.
- Detailed rainfall, evaporation, or seasonal discharge.
- Conservation or redistribution of lake water when a basin changes.

Geological tools and discrete erosion events may still alter a river's terrain.
When such an event adjusts an occupied freshwater tile, registered spring sources
are retraced immediately. River and lake tiles are derived state; the initial
player-created spring animation remains tile-by-tile. Hydrology is not otherwise
recalculated continuously every tick.

## Basin sizing

Lake size is determined by topography, not by a random radius.

Starting from the river's terminal depression, a bounded minimax search finds an
ocean-bound route. The highest elevation on that best route is the basin's spill
elevation, preventing small bumps and hollows inside an irregular crater from
masquerading as its rim. Routes with equal spill elevation prefer fewer steps and
use the ocean seed as a search tie-breaker instead of expanding aimlessly. Every tile
connected to the sink below that elevation belongs to the lake. A local tile's
water depth is:

```text
lake surface elevation - ground elevation
```

The spill tile is retained as hydrological information and river tracing resumes
from it as a new downstream river segment. If that segment reaches another
depression, the cycle repeats: fill, spill, and continue. Upstream channels and
lakes are excluded when an outlet would otherwise loop backward. Rivers have no
channel-length cutoff; they keep trying until they reach ocean, join an existing
watercourse, or encounter a genuinely oceanless terminal basin.

Persistent spring sources record whether they are natural or player-created.
Watershed Shift events may retire only natural sources. The remaining sources are
retraced, then a different eligible mountain starts a replacement natural river.
If no replacement can start, the old source is restored, making the shift atomic.
Player-created sources are never selected to dry up.

This basin filler is implemented in the simulation hydrology system.

## Safety limits

- Basin and outlet searches have explicit tile budgets. Outlet searches may
  examine up to 1,048,576 tiles, covering the full largest preset, while lakes
  may use up to 65,536 tiles, enough for maximum-size generated meteor craters;
  genuinely terminal lakes retain a conservative 128-tile cap.
- A deliberately reduced search budget can still produce a terminal inland lake
  rather than causing an unbounded search; normal worlds permit a full-world search.
- Lake filling is an event, not work repeated every simulation tick.
- No new terrain enum is created for every freshwater depth category.

## Consequences

Lakes have natural elevation-defined shapes and support future freshwater ecology.
Rivers remain legible and inexpensive. Newt gains meaningful hydrology without
becoming a fluid or geomorphology simulator.
