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
Plankton reproduction has a five-percent default chance to produce a jellyfish.
Any non-Plankton critter can shove blocking Plankton into an adjacent valid empty
water tile and enter the vacated tile. Deliberate Plankton predators still consume
their target, while escape routes and travel paths use shoving instead.

Critter evolution is represented as a parent/child tree. The current primitive
branches are Plankton -> Jellyfish; Plankton -> Worm -> Fish -> Newt -> Mega Toad
or Therapsid -> Monkey, Deer -> Elk or Gazelle, or Wolf;
Plankton -> Worm -> Nautilus -> Squid; and Plankton -> Trilobite -> Crab or Sea Scorpion.
Manual Evolve
events select an available child branch; de-evolution follows the species' unique
parent without replacing or moving the critter. Each world stores an evolution
chance from 0 to 100 percent in exact half-percent steps; reproduction rolls that
chance before selecting a birth tile, so an evolved offspring's own habitat rules
are used for placement. Terminal species reproduce as themselves.

Critters move to any of the eight surrounding tiles. This lets amphibians follow
diagonally connected river and lake corridors through otherwise impassable
Mountain terrain. Shoreline feeding and reproduction adjacency remain cardinal,
and hunter perception continues to use Manhattan distance.

Worms are marine and freshwater detritus seekers. They gain ambient food in Deep
Ocean, Shallows, Rivers, and unfrozen Freshwater Lakes, while ordinary Ocean and
frozen lakes remain transit-only habitat. They can follow freshwater across land
and Mountain tiles. Outside a feeding zone, they inspect the eight surrounding
tiles for a food cue before falling back to blind wandering. Jellyfish predation
offsets this broader feeding range.
Trilobites occupy the same broad saltwater range but graze ambient seafloor
detritus only in Ocean and Deep Ocean tiles. From shallows they favor adjacent
deep-water habitat, while occasional wandering still lets the branch reach the
coastal habitat needed by Crab offspring.
Sea Scorpions are active aquatic hunters descended from Trilobites. Every three
seconds they pursue fish, worms, trilobites, crabs, and newts within a Manhattan radius
of four. They can cross Deep Ocean, Ocean, Shallows, and Beach, giving them a
narrow shoreline overlap with crabs and newts without allowing inland pursuit.
Jellyfish remain collision predators and may consume plankton or worms. Neither
Sea Scorpions nor Squid hunt them, preserving Jellyfish control of Plankton. Adult
crabs can roam across ordinary land but gain ambient detritus every eight seconds
only on Beach and Shallows tiles. They reproduce at five energy for a cost of three,
leaving the parent safely fed while rapidly replenishing fish prey. Feeding remains
coastal, but reproduction and offspring placement may occur anywhere Crabs can live.

Fish are the first active hunters. Every two seconds, a fish retains or searches
for plankton, worms, and crabs within a Manhattan radius of six, examining at most 84
tiles, then takes one greedy eight-way step. It never runs A*. Missing, blocked, or
out-of-range prey returns the fish to ordinary wandering. Initial fish movement
is staggered across the two-second interval to spread large-cohort perception
cost across simulation ticks. Fish can enter Rivers and Freshwater Lakes,
including freshwater Mountain corridors, but never treat Newts as prey.
Fish scan five Manhattan tiles for any species capable of eating them before
they hunt. Sea Scorpions, Squid, Mega Toads, and Therapsids therefore make Fish choose the
open eight-way move that maximizes distance from the nearest threat. Detecting
a predator suppresses hunting even when no escape tile is available.

Nautiluses are slow shelled hunters evolved from Worms. Every six seconds they
pursue Plankton within a Manhattan radius of five. Jellyfish and Sea Scorpions
cannot eat them; Mega Toads can still attempt to swallow Nautiluses encountered
in Shallows. Nautiluses evolve into Squid, which hunt fish, trilobites, crabs,
newts, Nautiluses, and Sea Scorpions every three seconds within a
Manhattan radius of five.
Squid reproduction lays a Squid Egg rather than a live offspring. Eggs drift
with the same random movement and five-second interval as Plankton, then hatch
in place when eligible Squid prey comes within two Manhattan tiles.

Squid and Sea Scorpions are mutual predators. Their encounters use combat
instead of instant predation: each attack makes an even deterministic roll to
choose which participant loses one energy. Combat repeats as they continue to
hunt; at zero energy the loser dies and the winner receives its food energy.
Other predator-prey encounters remain immediate.

Newts are land dwellers that gain ambient food while standing in rivers or
unfrozen freshwater lakes, from a cardinally adjacent shore tile, or by modestly
grazing foliage while standing in swamps and jungles. Hungry Newts wait within
feeding range rather than wandering away before their next feeding interval.
Freshwater remains their migration target: a hungry Newt outside feeding range
scans only an eight-tile Manhattan neighborhood and takes one greedy step toward
detected freshwater.

All reproducing species retain at least two energy after paying their birth
cost. Plankton, Worms, and Trilobites therefore wait for one additional stored
energy before reproducing instead of dropping to one energy immediately after
birth.
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
perception radius of three and greedy eight-way movement to hunt broad prey,
including Deer, Elk, and Gazelles, without general pathfinding. If another Mega Toad and
any eligible non-Toad prey are both visible, the hunter chooses the Toad category
50 percent of the time and the non-Toad category 50 percent of the time. Ordinary
non-Toad prey remain preferable to Newts, and Therapsids are used only as the
final non-Toad fallback. Coastal predation keeps deep-feeding trilobites vulnerable whenever
they wander into Shallows. Mega Toads inherit the Newt's ordinary land range
and may also enter shallows and freshwater lakes, but open and deep ocean are
invalid habitat. Adults must stand in or beside a River or unfrozen Freshwater
Lake to reproduce, and their offspring must be placed directly in that water.

Wolves are a fast third branch from Therapsids. They scan six tiles and move every
two seconds while hunting the same broad prey as Mega Toads, including Deer, Elk,
and Gazelles. Mega Toads and Therapsids are held as last-resort targets; because each can
also attack Wolves, those encounters use the mutual-predator 50/50 damage roll.

Wolf reproduction is structure-mediated. On first reaching reproduction energy,
a Wolf reserves the nearest suitable Hill within eight tiles, falling back to its
current tile when no Hill is available, and travels there before spending energy.
The resulting passable Wolf Den stores one charge per reproduction. Ordinary wolf
prey moving onto or beside a charged den consumes at most one charge and spawns
one Wolf on the den or an adjacent valid tile. A blocked den retains its charge.
Dens and unused charges persist after the parent dies, but meteor excavation and
ejecta, tsunami erosion, and lava deposits remove them.
The presentation exposes structures through a dedicated, cycleable Building Tools
category. Player-placed Wolf Dens begin empty and use the same simulation state,
rendering, inspection, triggering, and destruction rules as wolf-built dens.
The Other category includes a global Jump Start action that enables life and fills
every empty Deep Ocean tile with one Plankton without replacing existing critters.

Monkeys, Deer, Elk, and Gazelles scan five tiles for any species whose diet includes them
and prioritize a step that increases their distance from the nearest threat.
Predation transfers food energy equal to half of the prey species' maximum energy,
rounded down with a minimum value of one; the predator's own maximum still caps
the result.

Newts scan within a Manhattan radius of four for Mega Toads before waiting at a
feeding site, migrating, or wandering.
When threatened, they take the available eight-way step that maximizes distance
from the nearest detected Toad; detecting a threat suppresses shoreline waiting
even when every escape step is blocked.

Therapsids form the terrestrial amniote branch from Newts and evolve into
Monkeys, Deer, or Wolves; Deer continue into Elk or Gazelle. Gazelles use the Deer
lifecycle and movement pace while grazing Grassland and Arid foliage. Therapsids
hunt the same prey roster as Mega Toads with a Manhattan radius
of four, including Mega Toads themselves. Mega Toads treat Therapsids as
last-choice prey; because each can eat the other, an encounter uses the same
50/50 one-energy combat rolls as Squid and Sea Scorpions rather than instant
predation. Preferred prey always outranks a visible Therapsid.

Monkeys hunt Newts and Crabs within four tiles and also gain ambient food from
Jungle foliage every eighteen seconds. A hungry Monkey already standing in a
Jungle waits for that feeding interval instead of wandering away. Mega Toads
and Therapsids may eat Monkeys through ordinary predation.

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
