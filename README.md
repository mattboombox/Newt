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
- `Left Click` + drag in critter mode: spawn the selected critter on valid tiles
- `Left Click` in building mode: place the selected building
- `Left Click` in event mode: trigger the selected event
- `Right Click`: delete the critter on the hovered tile, or the building if
  no critter is present; when stacked, two clicks remove them in that order
- `A` / `D`: cycle the active terrain, critter, building, or event selection
- `Q` / `E`: decrease or increase brush size
- `,` / `.`: slow down or speed up the simulation
- `P`: pause or unpause
- `R`: cycle between terrain, critter, building, and event mode
- `X`: quit

### Tool Notes

- Terrain mode includes direct trench tile painting. The trench event can
  also collapse land into a trench with an ocean ring and shallows coastline.
- Sand worms are player-spawned desert critters. They begin at length 2,
  grow after every 200 sand tiles crossed, and split into two young worms
  after completing a growth cycle at their maximum length of 9.
- The alien Critter Printer periodically creates a random species on any
  nearby open tile, even when that terrain is incompatible with the critter.
- Building mode currently places villages.
- Event mode includes `meteor`, `mega meteor`, `comet`, `tsunami`, `tectonic uplift`, `island uplift`, and `trench event`.
