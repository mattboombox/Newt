# Newt

## Controls

### Startup Menu

- `Up` / `W`: move selection up
- `Down` / `S`: move selection down
- `Enter` / `Space`: confirm the selected map size or temperature
- `1`-`9`: pick a listed size directly
- `Esc`: close the game

The `Micro` map size creates a 40 x 24 tile world for quick testing.
Its HUD expands vertically and wraps status information so no controls or
hover details are clipped by the narrow window.

The `Ring World` map size creates a wide 252 x 40 tile world.

The `Custom` size option uses `A`/Left and `D`/Right for width, and `W`/Up
and `S`/Down to increase or decrease height. Each press adjusts one tile;
hold `Shift` to adjust by ten. It cannot be smaller than Micro or larger than
the current display/4K limit, with a 10-pixel window safety margin.

After choosing a map size, choose a world temperature:

- `Hot`: no polar ice caps
- `Normal`: default ice caps
- `Cold`: larger ice caps
- `Frozen`: near-global ice with only a narrow equatorial strip

Finally, choose the world type, which sets its default terrain:

- `Wet`: world starts as ocean
- `Dry`: world starts as sand
- `Molten`: young world starts as lava

### In Game

- `Left Click`: use the active tool on the hovered tile
- `Left Click` + drag in terrain mode: paint the selected terrain
- `Left Click` + drag in Critters or Alt Critters: spawn the selected critter
  on valid tiles
- `Left Click` in Buildings: place the selected building
- `Left Click` in event mode: trigger the selected event
- `Left Click` with the Inspect tool: select a critter, show its live status, and
  follow it with the camera while zoomed in; click an empty tile to clear
- `Left Click` with the Evolve tool: create an evolved offspring
- `Right Click` with the Evolve tool: create a de-evolved offspring
- `Left Click` with the War tool: make the selected village declare war on
  the nearest other populated village
- `Right Click` with other tools: delete the critter on the hovered tile, or the building if
  no critter is present; when stacked, two clicks remove them in that order
- `A` / `D`: cycle the active terrain, critter, building, event, or tool selection
- `Q` / `E` in terrain mode: decrease or increase brush size
- `E` in spawning mode: cycle Critters, Alt Critters, and Buildings
- `,` / `.`: slow down or speed up the simulation
- `P`: pause or unpause
- `R`: cycle Terrain, Events, Spawning, and Tools categories
- `X`: quit

### Tool Notes

- Terrain mode includes direct trench tile painting. The trench event can
  also collapse land into a trench with an ocean ring and shallows coastline.
- Tsunamis, meteor and comet waves, tectonic chains, trenches, and volcanic
  spread wrap across the east and west edges of the world.
- Sand worms are player-spawned desert critters. They begin at length 2,
  grow after every 200 sand tiles crossed, and split into two young worms
  after completing a growth cycle at their maximum length of 9.
- The alien Critter Printer periodically creates a random species on any
  nearby open tile, even when that terrain is incompatible with the critter.
- Buildings contains villages, farms, residential, naval, and military
  districts, wolf dens, and Critter Printers.
- Event mode includes `meteor`, `mega meteor`, `comet`, `tsunami`, `tectonic uplift`, `island uplift`, and `trench event`.
- Alt Critters contains species that cannot be reached as an evolution
  result, plus manually spawnable specialists such as Ape Sailors, Ape
  Warriors, Saint Smashers, Undead, and Undead Beasts. Tools contains
  Inspect, Evolve, and War.

## Settlements and Specialists

- A homeless ape eats its own catches and does not found a village until it
  has eaten enough for its first reproduction. At that point it joins a nearby
  village with room or founds one, returns to the settlement, and reproduces.
- A homeless Ape Sailor follows the same reproduction rule, but only joins a
  village with a reachable harbor. If it founds a coastal village, the village
  receives a free connected Naval District as its initial harbor.
- Sailors who survive their village's defeat become war refugees. They may
  hunt and accumulate reproduction meals, but cannot found a village for 60
  seconds and cannot rebuild within 12 tiles of the defeated village's ruins.
  Joining an existing reachable harbor or founding elsewhere ends the refugee
  restriction.
- Village apes deliver catches as shared food. Farms provide more food, while
  residential districts increase population capacity.
- Military and naval districts recruit Ape Warriors and Ape Sailors for free;
  constructing the district is the settlement's cost.
- Civilian apes can tame wolves into Dogs. Ape Warriors can tame deer and
  become fast Ape Cavalry. Dogs and warriors prioritize nearby undead, and
  cavalry still counts toward a village's warrior cap.
- During a village war, Ape Warriors and Ape Cavalry from both sides hunt
  every ape belonging to the enemy village. War kills are never eaten or
  carried home as food. A warring village collapses into ruins when no
  residents, or only hard-to-reach Ape Sailors, remain. Peacetime sailor
  villages remain active. When one village is defeated, all of its stored food
  is transferred to the surviving enemy village.

## Combat and Special Critters

- Fighters use health and combat power. A contested attack deals one damage;
  the stronger combatant is more likely to land it. Fighters retaliate against
  their attacker, flash red when hurt, heal slowly over time, and heal a little
  when they eat while hungry.
- Ordinary prey hunting remains the lightweight hunt system rather than
  becoming a full combat exchange. Noncombat apes cannot eat combat-capable
  predators.
- The Lich is a player-spawned special critter and do not
  become hungry. A Lich prioritizes raising reachable dying critters: apes,
  including Ape Warriors, become Undead while other eligible critters become
  Undead Beasts.
- Undead roam independently and frequently attack apes. Undead and Undead
  Beasts can rise again after entering the dying state.
