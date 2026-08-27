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
Species define their metabolism, hunger threshold, body size, feeding strategy, and reproduction
cost. This prevents a hunger timer, starvation timer, and meal counter from
drifting into contradictory states. Reproduction spends only surplus energy;
if no adjacent habitat is open, the parent retains that energy for a later tick.

The Events category includes Plague and Zombie Plague tools. Left-click a living
Ape or Ape Sailor to infect it; stable critter IDs divisible by five resist both
strains. Every simulation second, infected apes expose their eight neighboring
tiles, including horizontal wrap. Newly infected apes cannot spread again in the
same tick. Both strains drain one energy every ten simulation seconds, in addition
to normal metabolism; feeding can prolong survival. Ordinary plague is yellow,
zombie infection purple, and undead apes green. Inspection reports infection or
immunity. Reapplying a strain does not reset its damage timer; zombie plague can
upgrade ordinary plague, but not the reverse.

With natural events enabled, each village with more than 200 assigned residents
(including sailors) has a 1% chance per simulation minute to seed ordinary plague
in one random non-immune resident. Villages at 200 or fewer residents are exempt;
unassigned apes do not count. A village with any already infected resident skips
the outbreak roll. These spontaneous outbreaks never create zombie plague.

Zombie-infected apes that die from energy loss or predation rise on their current
tile with the same ID as Undead Apes. Reanimation detaches village membership and
carried food, resets energy to six, and does not also feed the killer. Destructive
terrain tools and disasters remove the body completely and do not reanimate it.
Undead spread zombie plague, including during contact with living apes in combat,
and hunt only living Apes and Ape Sailors within six Manhattan tiles, even when
full. They use land habitat, act every three seconds, and retain ordinary ape
metabolism, but never reproduce, found villages, or collect village food. Immune
apes can still be killed in combat. Undead can be killed and do not rise again.
Sparse disease state uses stable IDs and is removed on death or transformation
out of a living ape species. Spread uses eight occupancy lookups per carrier;
there is no per-ape population search.

Primitive ecological interactions are resolved in a deferred movement commit.
A jellyfish that selects an adjacent plankton reserves that prey for the tick;
the plankton cannot move, and the jellyfish consumes it before entering its tile.
This keeps predation deterministic while critters are stored in compact arrays.
Plankton reproduction has a half-percent default chance to produce a jellyfish.
With life enabled, complete Plankton extinction starts a 15,000-tick recovery timer
that restores one Plankton on an available Deep Ocean tile.
Body size has six ordered levels whose numeric values are also prey food energy:
Tiny 1, Small 2, Medium 3, Big 4, Large 5, and Huge 8. A critter can normally shove
only a smaller blocker into adjacent valid empty habitat. Plankton are the exception:
every critter except a Squid Egg can shove them, including other Plankton. If no
single-tile or four-Plankton-chain shove destination exists, the blocking Plankton
dies and the mover enters its tile. The death provides meal energy only when that
species can legally eat Plankton. For other blockers, if no shove destination exists, the
blocker dies and counts as a meal only when the mover can legally eat that species;
otherwise movement remains blocked. Deliberate hunting always consumes legal prey.

Ambient food is finite per tile. Terrestrial capacity is Jungle 6, Swamp 5,
Forest 4, Grassland and Taiga 3, Bog and Arid 2, Tundra 1, and Desert and Arctic
0. Ice Sheets over Deep Ocean hold 1; shallower Ice Sheets hold 0. Beaches hold
0 when freezing and 1 otherwise. Shallows hold
2 freezing, 3 cold, and 4 temperate or hot. Ordinary Ocean is always 0. Deep
Ocean holds 2 when cold and 1 otherwise.

Rivers and Freshwater Lakes both replace underlying tile nutrition with a flat
capacity of 2. Since freshwater determines the tile's ecological role, its value
does not stack with the biome and does not vary with temperature. Only Worms,
Fish, and Newts consume freshwater nutrition; Crabs, Therapsids, Apes, Monkeys, Deer, Elk,
and Gazelles may cross supported freshwater habitat but do not graze it. This rule
also prevents those terrestrial feeders from consuming deposited nutrients in freshwater.
Feeding consumes one unit. Regeneration is
computed lazily only when a tile is queried, at `480 / capacity` seconds per unit,
so no world-sized nutrition update runs each tick. Capacity differences are retained
instead of flattening biomes; only the shared regeneration curve is slower. Jungle
therefore needs 80 seconds per unit, slightly longer than a Therapsid's 70-second
metabolism, so a continent packed tile-for-tile cannot support the population forever.
When a critter starves, it leaves one deposited nutrient on its tile. Deposited
nutrition is stored as a separate byte per tile, can temporarily exceed the natural
capacity, and remains edible even on terrain whose natural capacity is zero. The
deposit is made during the existing lifecycle pass, so it adds no map-wide scan.
Terrain feeding is part of the critter's normal action, with no separate feeding timer.
Terrain-first omnivores flee first, eat available terrain food, approach nearby productive
terrain, and hunt the nearest legal prey only when no terrain food is usable. Pure grazers
stop before hunting, while dedicated hunters skip terrain food. Plankton photosynthesize
without consuming tile nutrition and still drift on the same action.

Critter evolution is represented as a parent/child tree. The current primitive
branches are Plankton -> Jellyfish; Plankton -> Worm -> Fish -> Newt -> Mega Toad
or Therapsid -> Monkey -> Ape, Deer -> Elk or Gazelle, Wolf, or Toothed Whale -> Baleen Whale;
Plankton -> Worm -> Nautilus -> Squid; and Plankton -> Trilobite -> Crab or Sea Scorpion.
Manual Evolve
events select an available child branch; de-evolution follows the species' unique
parent without replacing or moving the critter. Each world stores an evolution
chance from 0 to 100 percent in exact half-percent steps; reproduction rolls that
chance before selecting a birth tile, so an evolved offspring's own habitat rules
are used for placement. Therapsid offspring use twice the configured world chance,
capped at 100 percent, because Therapsids support four important descendant branches.
Terminal species reproduce as themselves.

Critters move to any of the eight surrounding tiles. This lets amphibians follow
diagonally connected river and lake corridors through otherwise impassable
Mountain terrain. Shoreline feeding and most reproduction adjacency remain cardinal;
Fish may place offspring in all eight neighboring tiles so diagonal Rivers work.
Hunter perception continues to use Manhattan distance.

Every species may occupy an Ice Sheet: aquatic critters are interpreted as moving
beneath the ice, while terrestrial critters travel on top. A terrain change that
leaves a critter outside its normal habitat no longer removes it immediately.
Stranded critters do not feed, reproduce, hunt, or willingly enter invalid terrain
from valid habitat. Instead, they move at one quarter of their normal cadence and
take bounded recovery steps toward the nearest open valid tile within sixteen
tiles, wandering slowly until habitat comes within range.

Adjacent-only prey rules restrict eligibility, never target priority. Active
hunters select the nearest eligible prey and break equal-distance ties randomly,
regardless of species or adjacency restrictions. Collision predators likewise
have no species-specific priority for adjacent prey.

Worms are marine and freshwater detritus seekers. They gain ambient food in Deep
Ocean, Shallows, Rivers, unfrozen Freshwater Lakes, and Ice Sheets over Deep Ocean,
while ordinary Ocean and frozen lakes remain transit-only habitat. They can follow freshwater across land
and Mountain tiles. They move once every eight seconds, four times slower than
Fish. Outside a feeding zone, they inspect the eight surrounding
tiles for a food cue before falling back to blind wandering. Their four-energy
stomach supports reproduction at four energy for a cost of two. Worms can shove a chain of up to four
Plankton through a dense bloom rather than remaining trapped behind it. Squid and
Sea Scorpions may consume Worms only on adjacent encounters, preserving most of
the Fish and Nautilus source population.
Trilobites occupy the same broad saltwater range and graze terrain nutrition in
Deep Ocean, deep-ocean Ice Sheets, and Shallows. Ordinary Ocean remains transit-only because it has no
terrain nutrition. Their simple eyes detect predators
within three Manhattan tiles, prompting the trilobite to take the open eight-way
step that maximizes its distance from the nearest threat. Like Worms, they can
shove through a chain of up to four blocking Plankton.
Sea Scorpions are active aquatic hunters descended from Trilobites. Every four
seconds they pursue Fish, Crabs, Newts, Squid, Therapsids, Monkeys,
Apes, Wolves, Ape Sailors, Deer, Elk, and Gazelles within a Manhattan radius of
four, while Worms, Trilobites, and Nautiluses are eligible only when already adjacent. They can cross Deep Ocean, Ocean, Shallows, and Beach, giving them a
narrow shoreline overlap with crabs, newts, and grazers without allowing inland pursuit.
Jellyfish remain collision predators that consume Plankton and adjacent Crabs; Worms and Fish
are excluded from their diet to protect those lineages. Neither
Sea Scorpions nor Squid hunt them, preserving Jellyfish control of Plankton. Adult
crabs can roam across ordinary Ocean, Shallows, Beach, Ice Sheets, and non-Arctic land while
Deep Ocean remains impassable. Every active predator except Fish may consume Crabs only on adjacent
encounters rather than pursuing them at range, including strikes across habitat boundaries.
Crabs consume detritus from Beaches, Shallows, Swamps, and Jungles without freshwater on
their normal movement action, remain there while building breeding energy,
and reproduce directly on those coastal tiles at five energy for a cost of three,
leaving the parent safely fed. Feeding remains
wetland and coastal, and hungry Crabs detect suitable food within five Manhattan tiles
and move back toward it. Rivers and Freshwater Lakes remain traversable but provide no
natural or deposited food for Crabs, even over Swamp or Jungle biomes.
Reproduction and offspring placement may occur anywhere Crabs can live.

Fish are terrain-first omnivores. Every two seconds, a fish searches
within a Manhattan radius of six, examining at most 84
tiles, then takes one greedy eight-way step. It never runs A*. Missing, blocked, or
out-of-range prey returns the fish to ordinary wandering. Initial fish movement
is staggered across the two-second interval to spread large-cohort perception
cost across simulation ticks. Shallows, Rivers, and Freshwater Lakes provide terrain
food. Fish eat or approach that food before hunting Plankton, their only animal prey.
Fish can enter Rivers and Freshwater Lakes, including freshwater Mountain corridors.
A Fish can shove a smaller Worm aside while travelling, but an immovable Worm blocks
the route without being harmed. Crabs, Worms, and Newts are never converted into food.
Fish scan five Manhattan tiles for any species capable of eating them before
they hunt. Sea Scorpions, Squid, Mega Toads, and Therapsids therefore make Fish choose the
open eight-way move that maximizes distance from the nearest threat. Detecting
a predator suppresses hunting even when no escape tile is available.

Nautiluses are slow shelled hunters evolved from Worms. Every six seconds they
eat available terrain food in Deep Ocean, deep-ocean Ice Sheets, or Shallows before pursuing the nearest
Plankton within a Manhattan radius of five. Like Trilobites, they roam Ocean and
Deep Ocean, return from depleted Shallows toward adjacent deep-sea terrain, and flee
visible predators within three tiles. Ordinary Ocean is transit and hunting habitat only.
Jellyfish cannot eat Nautiluses. Sea Scorpions and Squid can consume them only on
adjacent encounters; Mega Toads can still attempt to swallow
Nautiluses encountered in Shallows. Nautiluses evolve into Squid, which
hunt fish, trilobites, crabs, newts, Sea Scorpions, Deer, Elk, and Gazelles every
three seconds within a Manhattan radius of five; Worms are eligible only when
already adjacent.
Squid reproduction lays a Squid Egg rather than a live offspring. Eggs drift
with the same random movement and five-second interval as Plankton, then hatch
in place when eligible Squid prey comes within two Manhattan tiles or immediately
upon drifting into Shallows.

Squid and Sea Scorpions are mutual predators. Their encounters use combat
instead of instant predation: each attack makes an even deterministic roll to
choose which participant loses one energy. Combat repeats as they continue to
hunt; at zero energy the loser dies and the winner receives its food energy.
Other predator-prey encounters remain immediate.

Newts are land dwellers that gain terrain food while standing in rivers or
unfrozen freshwater lakes, from a cardinally adjacent shore tile, or by modestly
grazing foliage while standing in swamps and jungles. Newts feed on their normal
five-second action and wait within feeding range while nutrition remains.
Outside feeding range they scan only an eight-tile Manhattan neighborhood and
take one greedy step toward a tile that still has nutrition available.

All reproducing species retain at least two energy after paying their birth
cost. Plankton, Worms, and Trilobites therefore wait for one additional stored
energy before reproducing instead of dropping to one energy immediately after
birth.
This bounded scan performs at most 145 tile checks every five seconds and never
runs general pathfinding. Newts no longer make a map-wide migration toward
freshwater, preventing distant populations from converging on the same food tiles.
Rivers and lakes act as habitat corridors through Mountain tiles, including
Arctic Snowy Mountains, for amphibians that can otherwise enter that kind of
freshwater. Dry Mountain tiles remain impassable, and frozen lakes still provide
no food to Newts.

Mega Toads are the first amphibian predator. They use a bounded Manhattan
perception radius of three and greedy eight-way movement to hunt the nearest legal prey,
including Deer, Elk, and Gazelles, without general pathfinding or meal preference tiers.
Coastal predation keeps deep-feeding trilobites vulnerable whenever
they wander into Shallows. Mega Toads inherit the Newt's ordinary land range
and may also enter shallows and freshwater lakes, but open and deep ocean are
invalid habitat. Reproduction has no additional freshwater or terrain gate;
offspring only need an adjacent tile in which they can survive.

Wolves are a fast third branch from Therapsids. They scan five tiles and move every
2.5 seconds while hunting broad terrestrial prey, including Deer, Elk, and
Gazelles. Therapsids are held as last-resort targets. Wolves never pursue Mega
Toads, but Mega Toads do hunt Wolves; a Toad-initiated encounter still uses the
mutual-predator 50/50 damage roll rather than instant predation.

Wolf reproduction is structure-mediated. On first reaching reproduction energy,
a Wolf reserves the nearest suitable Hill within eight tiles, falling back to its
current tile when no Hill is available, and travels there before spending energy.
The resulting passable Wolf Den stores one charge per reproduction, capped at
five charges. If a Wolf reproduces at a den already holding five charges, it
produces an adjacent Wolf offspring instead and leaves all five charges intact.
Ordinary wolf
prey moving onto or beside a charged den consumes at most one charge and spawns
one Wolf on the den or an adjacent valid tile. A blocked den retains its charge.
Stored charges also decay passively at one charge every two simulation minutes.
An empty unassociated den is removed, preventing dens from persisting indefinitely
on preyless islands. Meteor excavation and ejecta, tsunami erosion, and lava deposits
also remove dens immediately.
The presentation exposes structures through a dedicated, cycleable Building Tools
category. Player-placed Wolf Dens begin with one charge and use the same simulation state,
rendering, inspection, triggering, and destruction rules as wolf-built dens. A Wolf
spawned from a den remains associated with it; once a den has zero charges and its
last associated or inbound Wolf is gone, the empty den is removed automatically.
The Other category includes a global Jump Start action that enables life and fills
every empty Deep Ocean tile with one Plankton without replacing existing critters.
Its Population tool opens a live overlay built from the world's per-species counters.
Only nonzero species populations are rendered, with a responsive multi-column layout for
short windows. Each row includes a colored five-second-sample sparkline retaining
roughly seven and a half minutes of history; the simulation continues updating beneath it.
An additional yellow Sick Apes row counts living Apes and Ape Sailors infected by
either plague strain, excluding Undead Apes. It is a subset of the existing species
totals, not an additional population. The row remains visible at zero while living
apes or recent sickness history exist, and its history resets with a new world.

Monkeys, Deer, Elk, and Gazelles scan five tiles for any species whose diet includes them
and prioritize a step that increases their distance from the nearest threat.
Predation transfers food energy equal to the prey species' body-size value; the
predator's own maximum still caps the result. Mega Toads cap at 16 energy, giving well-fed adults a larger reserve
for repeated predator combat than Therapsids or Wolves.

Newts scan within a Manhattan radius of four for Mega Toads before waiting at a
feeding site, seeking nearby available food, or wandering.
When threatened, they take the available eight-way step that maximizes distance
from the nearest detected Toad; detecting a threat suppresses shoreline waiting
even when every escape step is blocked.
Reproduction has no species-specific terrain requirement. Parents still need an
open adjacent tile that is valid habitat for the offspring.

Therapsids form the terrestrial amniote branch from Newts and evolve into
Monkeys, Deer, Wolves, or Toothed Whales; Deer continue into Elk or Gazelle. Deer and Gazelles
move every three seconds, while Elk move every six seconds. Gazelles graze Arid
and Grassland foliage, but not Forest or Desert. Therapsids hunt only Fish within
a Manhattan radius of four; Monkeys and larger animals are not prey.
Their six-second movement interval gives terrestrial prey more time to escape.
Below breeding energy they remain on, or seek within four tiles, available Jungle,
Swamp, or Arid foliage before hunting. Forest and Bog nutrition is reserved for other
ecological niches. They eat one available terrain nutrient
on their normal six-second action; Therapsids resume hunting when no supported forage is available.
Deer can occupy Forests, Swamps, and Jungles but receive ambient food only from Grassland.
Elk likewise cannot feed from Forest nutrition.
From shore, a Therapsid can strike an immediately adjacent Fish
in a Freshwater Lake and eat it without entering the lake itself.
Mega Toads and Wolves can hunt Therapsids, but the Therapsid has a 20 percent chance
to win each one-damage combat exchange, a 60 percent penalty from the usual chance. Therapsids
still do not include either species in their hunting diet, so they defend themselves
without pursuing them.
Toothed Whales are the marine Therapsid branch. They inhabit Deep Ocean, Ocean,
and Shallows, move every four seconds, and scan seven Manhattan tiles for
animal-eating marine prey: Sea Scorpions, Nautiluses, Fish, Squid, and Ape Sailors.
They also consume feeder Crabs only when adjacent, following the universal Crab rule.
Large land animals are eligible only while standing in Shallows; the whale will not
pursue them on land or in deeper water. Toothed Whales ignore Jellyfish, Plankton,
Worms, Squid Eggs, and smaller land animals.
Baleen Whales evolve from Toothed Whales and reuse their saltwater habitat, huge
body size, energy economy, four-second movement interval, and seven-tile perception
radius. Their diet contains only Plankton.
The critter that loses any mutual-predator combat roll exposes a deterministic
half-second damage-flash state. The presentation renders that critter white for
the duration without changing combat timing or movement.
Every naturally born critter records its parent's species for a deterministic
thirty-second reproduction truce. During that window neither the offspring nor
members of its parent's species can prey upon the other. This applies equally to
same-species births and mutants while leaving unrelated predators unaffected.
Mega Toad cannibalism remains enabled after that truce, but only on adjacent
encounters rather than through active pursuit. They also hunt Fish
where their habitats overlap, including Rivers and unfrozen Freshwater Lakes.
Reproduction requires fourteen energy and costs nine, leaving a threshold-level
parent at five energy. A newborn also starts at five, while eating another Mega
Toad restores eight. Either survivor therefore reaches only thirteen energy—one
short of another birth—so a closed population cannot sustain itself without
outside prey.

Monkeys are non-predatory and eat available Swamp, Jungle, or Forest foliage on their normal
five-second action. Their reproduction threshold is
eight energy with a cost of five. A Monkey below breeding energy remains on
Swamp, Jungle, or Forest foliage to keep feeding, while a hungry Monkey adjacent to
one of those biomes moves toward it. Wolves and Mega Toads may eat Monkeys only
when adjacent, including diagonal neighbors and neighbors across the horizontal
map seam. Monkeys receive no special target priority and are not pursued at range.

Apes extend the Monkey branch and use the land habitat while hunting every species
except Plankton and Worms outside their own civilization that enters shared terrain. They also forage directly
from productive Swamp and Jungle foliage, moving onto adjacent wetland food when hungry.
Village residents hunt only while their home village stores fewer than ten food;
at ten or more they avoid prey while roaming. Unassigned Apes continue hunting so
they can gather enough energy to found a village.
Their first reproduction
is settlement-mediated: the Ape searches within 28 tiles for a village site adjacent
to an unoccupied Grassland or Beach tile. The founding event creates only the Village,
consumes the reproduction cost, and produces no offspring. New villages must be more
than twelve tiles apart. Rivers reject Village and land-district construction,
allowing waterways to remain natural borders while freshwater Harbors provide the exception.

Ordinary Apes take Fish only when already adjacent instead of chasing faster Fish
through Shallows. Ape Sailors move on the same two-second interval as Fish and remain
the civilization's active marine hunters. Their eight-direction movement follows
diagonal Rivers into Freshwater Lakes, including freshwater corridors through
Mountains and across horizontal map wrap.

Villages begin with capacity for five residents. At five residents they build a
Grassland Farm, Swamp Rice Paddy, or Forest Orchard when possible, otherwise a Harbor
on a Beach, River, or Freshwater Lake tile. Harbor sites may connect diagonally to
the village or its districts, and freshwater Harbors accept dry land in any of
the eight neighboring tiles. Other districts retain cardinal construction links.
All three food districts add one stored food every fourteen seconds. An assigned Ape's kill is
conserved rather than counted twice: meal energy first raises the hunter toward its
reproduction threshold, and only the unused remainder becomes carried settlement
food. Residents return that remainder to the Village or any connected district.
This lets hungry Apes keep hunting until they can reproduce instead of turning every
meal into a return trip.
Return trips track the closest distance reached. If an Ape spends thirty seconds
without getting closer—such as when an equal-sized crowd blocks every step—the
carried food transfers automatically and the Ape resumes ordinary behavior. Genuine
long trips do not time out while they continue making progress.
Whenever an assigned land Ape becomes hungry on a metabolism tick, it may
consume one stored food from its home village remotely. This communication has no
distance requirement, so residents do not starve merely because hunting carried them
far from the settlement; unassigned Apes cannot draw from village stores.
At its population cap, a village alternates connected Residential Districts with
additional biome-appropriate food districts. A Residential District costs five food
and adds five capacity; Farms, Rice Paddies, and Orchards remain free to establish but
require open connected Grassland, Swamp, and Forest tiles respectively. If no planned
food district has a valid site, the village may fall back to housing. Expansion choice,
construction cost, and placement remain separate so future building kinds can join the
plan without rewriting village advancement.

Villages store food up to a base pantry of five plus ten for each Farm, Rice Paddy,
or Orchard. They begin with six wood and can store at most thirty. One Lumber Camp may
be built for three food and no wood on a connected Forest, Jungle, Taiga, or Swamp tile. It
produces one wood every fourteen, ten, eighteen, or twenty-four seconds respectively,
making Swamp camps the lowest-yield option. The first
food district is free; later food districts cost two wood. Residential Districts cost
five food and four wood, while a Harbor costs six wood. The Harbor includes its first
boat and therefore its first Sailor; later Sailors cost two wood each.
Food districts persist when seasons temporarily change their biome, but stop producing
until Grassland returns for Farms, Swamp for Rice Paddies, or Forest for Orchards.
Climate coastline rebuilding validates Harbors only after Beach classification finishes,
so the temporary landform stage cannot demolish them.

Harbors recruit ordinary residents into Ape Sailors at thirty-second intervals without
changing total village population or the recruit's stable identity. The village quota
is one Sailor at five residents, two at ten, three at fifteen, and four at twenty or
more. Sailors have a maximum energy reserve of 28, twice the ordinary Ape cap of 14.
Recruitment preserves existing energy; sailors gain energy by hunting and no longer
refill remotely from village stores. They keep catches until their own reserve reaches
28 and carry surplus food home. Metabolism, combat, and plague drain that finite reserve,
so village supplies cannot automatically cancel plague damage. Ape Sailors never reproduce. Sailors
occupy Beach, saltwater, Rivers, and Freshwater Lakes, hunt every implemented sea species except Plankton and Worms, and
return their catches through a connected Harbor. Village, Farm, Harbor, and Residential
District tiles are simulation-owned structures exposed to rendering and inspection;
destructive terrain and surface-cover changes remove invalid structures.
Each village is limited to one Harbor. A village that originally founded beside a food
biome may add that Harbor later once its connected district network reaches an open
Beach or land-adjacent freshwater tile and the settlement has at least five residents.
When a village loses its final living resident, the Village and every connected
district are removed. Each former structure tile receives one deposited nutrient,
representing scavengers reclaiming the abandoned settlement even on terrain that
normally stores no natural nutrition.

Deer, Elk, and Gazelles use the same reproduction placement rule as other
ordinary species; only the chosen offspring tile must be open and habitable.
All terrestrial species may traverse exposed Lowlands, Canyons, and Trenches;
elevation below ordinary Plains does not impose a movement restriction.

Critters have monotonically assigned stable IDs backed by an ID-to-compact-index
map. Array compaction after a death updates that map instead of changing identity.
The Inspect tool can therefore follow one critter in constant time; if its ID
disappears, inspection and camera following end immediately.

Life recovery does not create geological cover for every barren world tile.
Biome.None is the shared stone state for lifeless terrain, meteor ejecta, and
cooled lava. Geological stone cover is retained only as recovery metadata for
the latter disturbances. Biome-less ordinary land uses stone presentation, while a sorted recovery queue
stores only the one-time reclamation schedule after Life is re-enabled. This
avoids scanning an entire maximum-size world on every tick. Due tiles independently
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
