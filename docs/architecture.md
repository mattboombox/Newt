# Architecture

## Goals

Newt must simulate large worlds and thousands of critters predictably while
remaining approachable to modify. Performance is a design constraint, not a
cleanup phase.

The architecture serves the autonomous-terrarium promise in
[`vision.md`](vision.md). Player and AI control will issue commands through the
same simulation interfaces so possession and settlement control never create a
second, disconnected set of rules.

## Dependency rule

```text
Newt.Game  --->  Newt.Simulation
                       ^
                       |
Newt.Simulation.Tests -+
```

`Newt.Simulation` never references MonoGame. Rendering observes immutable
snapshots and sends explicit commands into the simulation. Tests and benchmarks
therefore run without opening a window.

## Simulation model

- The simulation advances at 20 fixed ticks per second.
- The MonoGame host converts wall-clock time into whole simulation ticks.
- Randomness belongs to the simulation and is explicitly seeded.
- Critters use stable handles; gameplay code does not retain collection indexes.
- One tile has at most one ordinary critter occupant.
- World X coordinates wrap; Y coordinates do not.
- World generation is a pure deterministic operation over explicit options.

## Data layout

Critter fields begin in parallel, fixed-capacity arrays. The world grid owns an
occupant index for constant-time collision checks. This avoids one heap allocation
and one virtual update call per critter per tick.

Species behavior will be systems operating over these arrays. Composition is
preferred over a deep inheritance hierarchy.

Critter hunger, starvation, and reproduction share one integer energy reserve.
Species define their metabolism, hunger threshold, food yield, and reproduction
cost. This prevents a hunger timer, starvation timer, and meal counter from
drifting into contradictory states. Reproduction spends only surplus energy;
if no adjacent habitat is open, the parent retains that energy for a later tick.

Primitive ecological interactions are resolved in a deferred movement commit.
A jellyfish that selects an adjacent plankton reserves that prey for the tick;
the plankton cannot move, and the jellyfish consumes it before entering its tile.
This keeps predation deterministic while critters are stored in compact arrays.
Plankton reproduction has a five-percent default chance to produce a jellyfish. Body
size, shoving, and general displacement remain a later interaction layer.

Critter evolution is represented as a parent/child tree. The current primitive
branches are Plankton -> Jellyfish and Plankton -> Worm -> Fish -> Newt -> Mega Toad. Manual Evolve
events select an available child branch; de-evolution follows the species' unique
parent without replacing or moving the critter. Each world stores an evolution
chance from 0 to 100 percent in exact half-percent steps; reproduction rolls that
chance before selecting a birth tile, so an evolved offspring's own habitat rules
are used for placement. Terminal species reproduce as themselves.

Worms are shallow-water detritus seekers. They can survive in open saltwater but
gain ambient food only in Shallows; outside them, they inspect only four adjacent
tiles for a shallow-water chemical cue before falling back to blind wandering.
Jellyfish remain collision predators and may consume plankton or worms.

Fish are the first active hunters. Every three seconds, a fish retains or searches
for plankton and worms within a Manhattan radius of six, examining at most 84
tiles, then takes one greedy cardinal step. It never runs A*. Missing, blocked, or
out-of-range prey returns the fish to ordinary wandering. Initial fish movement
is staggered across the three-second interval to spread large-cohort perception
cost across simulation ticks.

Newts are land dwellers that gain ambient food from rivers and unfrozen freshwater
lakes while standing in the water or on a cardinally adjacent shore tile. Hungry
Newts wait within feeding range rather than wandering away before their next
feeding interval. A hungry Newt outside that range scans only an eight-tile
Manhattan neighborhood and takes one greedy step toward detected freshwater.
This bounded scan performs at most 145 tile checks every five seconds and never
runs general pathfinding. Each newt makes one freshwater search during its lifetime and snapshots
the resulting route. The expensive map-wide search is a reverse navigation field
shared by every newt and rebuilt only after relevant terrain or freshwater state
changes. A newt may cross saltwater while consuming that migration route, but
ordinary wandering is land-only after the route completes or becomes invalid.
This gives newly evolved ocean fish a viable transition without adding repeated
per-critter pathfinding.
Rivers and lakes act as habitat corridors through Mountain tiles, including
Arctic Snowy Mountains, for amphibians that can otherwise enter that kind of
freshwater. Dry Mountain tiles remain impassable, and frozen lakes still provide
no food to Newts.

Mega Toads are the first amphibian predator. They use a bounded Manhattan
perception radius of three and greedy cardinal movement to hunt worms, fish, and
newts without general pathfinding. They inherit the Newt's ordinary land range
and may also enter shallows and freshwater lakes, but open and deep ocean are
invalid habitat.

Critters have monotonically assigned stable IDs backed by an ID-to-compact-index
map. Array compaction after a death updates that map instead of changing identity.
The Inspect tool can therefore follow one critter in constant time; if its ID
disappears, inspection and camera following end immediately.

Life recovery does not create geological cover for every barren world tile.
Biome.None is the shared stone state for lifeless terrain, meteor ejecta, and
cooled lava. Geological stone cover is retained only as recovery metadata for
the latter disturbances. Biome-less ordinary land uses stone presentation, while a sorted recovery queue
stores only the one-time reclamation schedule after Life is re-enabled. This
avoids scanning an entire massive world on every tick. Due tiles independently
reclassify from their current temperature and moisture over 18 to 45 seconds.

## Intended update order

1. Apply queued player and world commands.
2. Advance environmental systems.
3. Update needs and lifecycle state.
4. Build movement and interaction intents.
5. Resolve conflicts deterministically.
6. Commit births, deaths, and movement.
7. Publish diagnostics and presentation state.

The bootstrap currently commits movement directly. Intent generation and conflict
resolution must replace direct mutation before interactions are introduced.
