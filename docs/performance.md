# Performance contract

## Initial targets

- Reference world: 252 × 130 tiles.
- Reference population: 10,000 active critters.
- Simulation rate: 20 ticks per second.
- Presentation target: 60 frames per second on the development machine.
- Steady-state simulation allocations: zero bytes per tick after warm-up.

These are engineering targets, not final hardware requirements. Each substantial
system needs a representative benchmark before its population scale increases.

## Rules

- No map-wide or population-wide search inside an individual critter update.
- Spatial queries use the grid or bounded spatial indexes.
- Pathfinding requests are bounded, cached, shared, or budgeted.
- Rendering may interpolate but never controls simulation state.
- Cleanup uses dense collections or deferred batches, not repeated linear removal.
- Diagnostics expose tick duration, population by species, births, deaths,
  movements, blocked movements, and pathfinding work.

## Performance gates

Before porting each major feature:

1. Add behavioral tests.
2. Add or extend a headless benchmark scenario.
3. Measure release builds, not debugger builds.
4. Record the result and hardware here.
5. Reject changes that introduce unbounded per-agent work.
