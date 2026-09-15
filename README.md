# Newt

Newt is a deterministic ecosystem simulation in C# with
MonoGame.

Newt's north star is an autonomous living terrarium: species evolve and establish
themselves, civilizations rise and fall, and player control remains an optional
way to inhabit the simulation. Read the full [product vision](docs/vision.md).

## Prerequisites

- A compatible .NET 10 SDK (the solution accepts newer .NET 10 feature bands)
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

Press Escape to exit.

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
