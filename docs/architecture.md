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

## Data layout

Critter fields begin in parallel, fixed-capacity arrays. The world grid owns an
occupant index for constant-time collision checks. This avoids one heap allocation
and one virtual update call per critter per tick.

Species behavior will be systems operating over these arrays. Composition is
preferred over a deep inheritance hierarchy.

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
