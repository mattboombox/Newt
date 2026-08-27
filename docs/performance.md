# Performance contract

## Initial targets

- Reference world: Large preset at 640 × 311 tiles.
- Reference population: 10,000 active critters.
- Simulation rate: 20 ticks per second.
- Presentation target: 60 frames per second on the development machine.
- Steady-state simulation allocations: zero bytes per tick after warm-up.

These are engineering targets, not final hardware requirements. Each substantial
system needs a representative benchmark before its population scale increases.
The 1280 × 642 Huge preset is an intentionally demanding stress-test world.

## Rules

- No map-wide or population-wide search inside an individual critter update.
- Spatial queries use the grid or bounded spatial indexes.
- Pathfinding requests are bounded, cached, shared, or budgeted.
- Rendering may interpolate but never controls simulation state.
- Volcanic deposits batch global landform refreshes, and reroute freshwater only
  when lava actually touches a river or lake.
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

## Plague benchmark (2026-08-26)

`tools/PlagueBenchmark.cs` is a headless .NET file-based benchmark. Run it with
`dotnet run --file tools/PlagueBenchmark.cs -c Release`. It creates 10,000 apes on
a 100 × 100 land patch in a 640 × 311 world, disables seasons and natural events,
and exposes every thirteenth starting ape. It warms up 200 ticks and measures
1,400 more, including spread, deaths, resurrection, and combat.

Measured locally on Windows 10.0.26200, .NET 10.0.11, 16 logical CPUs, using
`-c CodexPlagueRelease -p:Optimize=true` to keep optimized outputs separate from
the developer's existing builds:

| Scenario | Mean ms/tick | Allocated bytes/tick | Final living apes | Final undead |
| --- | ---: | ---: | ---: | ---: |
| No infection | 0.233 | 112 | 10,000 | 0 |
| Plague | 0.228 | 537 | 2,000 | 0 |
| Zombie plague | 0.440 | 12,545 | 62 | 7,783 |

These are full-scenario averages, not isolated disease overhead: population and
combat workload diverge. All were below the 50 ms tick budget in this run. The
zero-allocation target is not met during outbreaks; infection dictionary growth
and existing deferred death/combat collections allocate. Spread does only eight
occupancy lookups per contagious ape once per second; undead reuse bounded hunter
perception and stagger their movement cadence.
