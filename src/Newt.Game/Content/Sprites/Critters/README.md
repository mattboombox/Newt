# Critter sprites

Place transparent PNG critter sprites in this folder. The game currently recognizes:

- `ape.png`
- `ape-sailor.png`
- `baleen-whale.png`
- `deer.png`
- `mega-toad.png`
- `newt.png`
- `plankton.png`
- `toothed-whale.png`
- `wolf.png`

Sprites are drawn into one square map tile, so square images are recommended. Keep transparent
padding inside the image if a critter should not fill the entire tile. PNG files in this folder
are copied to build and publish output automatically. When a recognized sprite is absent, the
game continues to use its existing colored-square marker.
