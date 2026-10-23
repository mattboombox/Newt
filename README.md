# Newt

Newt is a deterministic ecosystem simulation in C# with
MonoGame.

Newt's north star is an autonomous living terrarium: species evolve and establish
themselves, civilizations rise and fall, and player control remains an optional
way to inhabit the simulation. Read the full [product vision](docs/vision.md).

## Prerequisites

- .NET SDK 10.0.300 or newer (including newer major versions and prerelease SDKs)
- MonoGame 3.8.5 project templates

```powershell
dotnet new install MonoGame.Templates.CSharp
```

## Build, test, and run

```powershell
dotnet restore Newt.sln
dotnet build Newt.sln --no-restore
dotnet test Newt.sln --no-build
dotnet run --project src/Newt.Game/Newt.Game.csproj
```

Press Escape to exit. Press **V** to toggle **2D / 3D free camera**.
Press **F** to enter first person or return to the 3D free camera.
First person walks at surface height with smooth **WASD** movement and **Shift**
to move faster. Move the **mouse** to look freely; no button needs to be held.
Arrow keys and the zoom wheel are ignored in first person. The cursor releases
when menus open or the game loses focus. Press **V** or **F** to return to the
3D free camera at your walking position. First-person movement follows curved ring worlds too.
In 3D, terrain forms columns whose heights follow elevation, while buildings
and critters face the camera in both yaw and pitch, remaining visible overhead.
Ocean and lake surfaces share a subtle, nearly neutral translucent sheet.
Lakebeds use the same depth-shaded blue as unfrozen 2D lake tiles.
Frozen oceans and lakes have slightly translucent ice sheets in both views,
with land critters on top and
aquatic critters at the bed beneath them. Frozen ocean beds retain ocean colors;
frozen lakebeds retain lake blue.
Use **WASD** to move relative to the camera, **Shift** to move faster,
**middle mouse drag** or the **arrow keys** to rotate and tilt, and the
**mouse wheel** to zoom. All 3D maps can tilt up to 60 degrees above the horizon
while the camera stays above the terrain. Hover and click terrain to inspect or use the selected
tool, including on column sides. Repeated map copies provide seamless camera
wrapping east–west; north–south edges remain finite. Picking any copy selects
the original tile. Wide 3D views show each longitude once, shifting the visible
map seam as the camera pans instead of repeating the world many times.
Nexus worlds remain finite in both views and never wrap.
The 3D zoom-out limit adapts to the viewport so it accommodates the widest 2D
view without zooming in when switching. Entering 3D starts directly
overhead, north up, with a view scale based on the current 2D zoom.
Returning to 2D centers on the terrain under the 3D screen center and selects
the nearest supported zoom based on projected tile size, including tilted views.
The simulation retains its world's original topology when changing views.

Ring worlds form a closed, inward-facing ring in 3D. The camera follows the
circumference on a rail, with movement and rotation relative to the local surface.
Terrain, water, ice, roads, buildings, critters, and tile picking follow the curve.
Standard (1,280 × 44), Large (2,560 × 44), and Mega (5,120 × 44) all close at the seam.
Zoom is limited to the ring interior; 2D remains the unrolled map.

For an animal-only run, select **Sentience** under World Tools and right-click
the world to disable monkey-to-ape evolution, then generate a fresh world.
Left-click with the tool to enable it again. The setting defaults to on and
persists across world regeneration. Other evolution continues normally;
existing apes and manually placed apes are unaffected. The Evolve tool also
respects this setting when evolving monkeys.

## Publishing for Windows

```powershell
dotnet publish src/Newt.Game/Newt.Game.csproj -c Release -r win-x64 --self-contained false -o dist/Newt
```

The application is published as `Newt.exe`; distribute the whole output folder.
This build requires the .NET 10 runtime. Use a fresh output folder when replacing
older releases so the former `Newt.Game.exe` does not remain alongside it.

The executable icon is generated from `Content/Sprites/Critters/newt.png`, the
same sprite used for the game window. After changing that sprite, run
`./tools/Update-AppIcon.ps1` in PowerShell on Windows before publishing. The icon
contains seven sizes with nearest-neighbor sampling and transparency masks to
keep the pixel art crisp at small sizes.

The current prototype supports deterministic generated worlds, camera movement,
zoom, and tile inspection. See [world generation](docs/world-generation.md) for
controls and algorithm notes. Live elevation editing and animated freshwater are
described in [geology and hydrology](docs/geology.md).

The implemented temperature, moisture, Arctic, tundra, taiga, bog, grassland,
forest, swamp, desert, arid, and jungle model is documented in
[climate and biomes](docs/climate-and-biomes.md), together with cold, temperate,
and warm ocean environments.

## Project structure

For a hands-on extension guide, see [Adding critters and buildings](docs/adding-critters-and-buildings.md).

- `src/Newt.Simulation` — deterministic rules with no graphics dependency.
- `src/Newt.Game` — MonoGame input, timing, rendering, audio, and platform host.
- `tests/Newt.Simulation.Tests` — fast headless behavioral tests.
- `docs` — architecture, performance budgets, and decision records.
- `Newt` — legacy Python prototype retained temporarily for reference.

Read the [product vision](docs/vision.md),
[canonical emergent scenario](docs/emergent-scenario.md),
[architecture](docs/architecture.md), and
[performance contract](docs/performance.md) before adding simulation systems.
