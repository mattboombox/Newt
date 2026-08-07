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

Geological tools and discrete erosion events may still alter a river's terrain.
Hydrology can then be recalculated explicitly rather than continuously reshaping
the map every tick.

## Basin sizing

Lake size is determined by topography, not by a random radius.

Starting from the river's terminal depression, a bounded minimax search finds the
lowest possible spill route to ocean or existing water. The highest elevation on
that best route is the basin's spill elevation. Every tile connected to the sink
below that elevation belongs to the lake. A local tile's water depth is:

```text
lake surface elevation - ground elevation
```

The spill tile is retained as hydrological information, but river tracing stops
when the lake forms. Lakes do not automatically create downstream rivers.

This basin filler is implemented in the simulation hydrology system.

## Safety limits

- Basin and outlet searches have explicit tile budgets.
- A failed or over-budget basin remains a terminal inland lake rather than causing
  an unbounded search.
- Lake filling is an event, not work repeated every simulation tick.
- No new terrain enum is created for every freshwater depth category.

## Consequences

Lakes have natural elevation-defined shapes and support future freshwater ecology.
Rivers remain legible and inexpensive. Newt gains meaningful hydrology without
becoming a fluid or geomorphology simulator.
