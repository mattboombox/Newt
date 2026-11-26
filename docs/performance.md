# Performance contract

## Stationary farmers (2026-11-26)

Assigned farmers now return to the exact farm tile and stay there. On arrival,
movement skips ordinary ape foraging, hunting, settlement-return searches, and
wandering. Displaced farmers retain the existing cached route and collision rules.
Food production and lifecycle updates are independent and retain their cadence.

The `AdvanceCritterMovementsFarmers` case in `tools/VillageBenchmark.cs` puts
5,000 farmers directly on assigned farms in a 1280 × 44 synthetic world. On Windows
10.0.26200, .NET 11.0.0, 16 logical CPUs, optimized DLLs measured **104.834 ms/tick
before and 0.062 ms/tick after**, with allocations falling from 36,437 to 124 bytes
per tick. These are movement-phase averages over twenty ticks after three warm-up
calls, excluding rendering and returning-to-farm pathfinding. This is a settled
farmer stress case, not a whole-game speedup or a mixed-population estimate.

## Large village updates (2026-11-25)

Village food/housing capacity queries share a per-tick building-count cache, invalidated
on construction, removal, conversion, and village merges. Resident and sustaining
population counts share a per-tick index maintained through home changes, births,
deaths, and species changes. Both caches rebuild once on the next tick, bounding stale
state while avoiding a full settlement scan inside each ape's update.

Villages with at least 64 residents perform management once per simulated second,
with stable settlement-specific phases. Small villages retain immediate management,
and abandonment checks remain immediate. Food production catches up using its
existing due-time loop. Movement, metabolism, births, and growth feeding retain their
normal cadence. Culture recruitment follows the management phase rather than requiring
every settlement to run on the same second boundary.

`tools/VillageBenchmark.cs` accepts a Release simulation DLL for before/after
comparisons. It constructs a synthetic 1280 × 44 world with approximately 5,000
assigned apes, 500 farms, and 500 residential districts. Timings are isolated phases,
with no rendering, rather than live-game frame rates. Each phase warms up three
times and measures twenty ticks. The reproduction-ready variant starts every ape
at full energy in a settlement with no housing vacancies.

| Phase | Before ms/tick | After ms/tick |
| --- | ---: | ---: |
| Village management | 2.281 | 0.693 |
| Ordinary lifecycle | 0.523 | 0.519 |
| Reproduction-ready lifecycle | 257.369 | 2.118 |
| Crowded movement | 35.618 | 24.981 |

The reproduction-ready lifecycle allocation dropped from approximately 800 KB per
tick to 26 bytes in this run. Results vary with population, congestion, jobs, and
active wars; they do not establish a whole-game speedup or meet the zero-allocation
target for every phase. Run with:

```powershell
dotnet run --file tools/VillageBenchmark.cs -c Release -- src/Newt.Simulation/bin/Release/net10.0/Newt.Simulation.dll
```

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
- Structural terrain rebuilds check occupancy before removing critters. Empty Mega
  Wall tiles must not trigger the defensive population scan in `RemoveCritterAt`;
  Nexus includes hundreds of thousands of exterior wall tiles.
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

## Ape worker and pathfinding optimization (2026-09-29)

Farmers and lumberjacks now resolve their workplace through a worker-ID index
instead of scanning all staff assignments. Recruitment, death, role changes,
and building removal maintain the index; home-village validation remains in place.

Ape worker, settler, and sailor searches share world-local scratch arrays, a
priority queue, and a reverse-path stack. Visit generations avoid clearing the
whole map per search. Each returned route remains separately owned. Scratch
arrays allocate lazily and retain approximately 12 bytes per map tile, plus
queue/stack capacity. Searches remain sequential within a world; future parallel
pathfinding will need separate scratch storage per worker.

`tools/ApePathBenchmark.cs` accepts a built simulation DLL so the same workload
can compare builds. Build the simulation in Release, then run:

```powershell
dotnet run --file tools/ApePathBenchmark.cs -c Release -- src/Newt.Simulation/bin/Release/net10.0/Newt.Simulation.dll
```

Measured locally on Windows 10.0.26200, .NET 10.0.12, 16 logical CPUs, using
optimized Release DLLs before and after this change. The workload warms 200
searches then measures 10,000 four-step routes on a 320 × 192 plain world:

| Implementation | Mean ms/search | Allocated bytes/search |
| --- | ---: | ---: |
| Previous | 0.046466 | 493,070 |
| Reused scratch storage | 0.009671 | 208 |

These figures include reflection and route validation. They demonstrate reduced
short-search overhead, not a full-world speedup or a worker-lookup benchmark.
Path exploration, collision rules, and route tie-breaking are unchanged; search
budgets and congestion retry behavior are not part of this change.
