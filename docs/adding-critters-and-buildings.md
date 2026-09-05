# Adding critters and buildings

This guide is for someone who knows C# but is new to Newt's code. It covers a
standalone critter, a settlement building, and a critter with its own building.
Rabbit, Workshop, and Beaver/Lodge below are examples to implement, not existing
features. Suggested numbers are illustrative balance values.

## How the project is organized

| Location | Responsibility |
| --- | --- |
| `src/Newt.Simulation` | World state, movement, food, reproduction, buildings, and rules |
| `src/Newt.Game/NewtGame.cs` | MonoGame drawing, tools, input, and inspection text |
| `tests/Newt.Simulation.Tests` | Simulation tests that run without opening the game |

The simulation must not reference MonoGame. The game reads simulation state and
calls simulation methods to make changes.

There is no `Rabbit : Critter` inheritance tree. `SimulationWorld` stores species,
positions, energy, and other fields in parallel arrays. Switches select behavior.
Some systems live in separate files as parts of the same `partial` class; for
example, [MegaSpiders.cs](../src/Newt.Simulation/MegaSpiders.cs).

Important conventions:

- Use `CritterId` for ownership or state that survives across ticks. Array indexes
  can change when another creature dies or is removed.
- One ordinary critter occupies each tile. A building is separate tile state.
- X coordinates wrap around the world; Y coordinates do not.
- Use simulation ticks and seeded randomness (`NextInt` inside the world), not
  wall-clock time, `Thread.Sleep`, or a new unseeded `Random`.
- The simulation currently runs at 20 ticks per second. Express durations using
  `SimulationWorld.TicksPerSecond` instead of embedding that number everywhere.
- Append species/building enum entries to preserve existing numeric values.
- Follow working code and tests when older design documents disagree with them.

See [architecture.md](architecture.md) and [performance.md](performance.md) for
more context.

## 1. Add a standalone critter

For a first attempt, add a Rabbit that behaves like a deer. Start by searching
for the closest existing species:

```powershell
rg -n 'CritterSpecies.Deer' src tests -g '*.cs' -g '!**/bin/**' -g '!**/obj/**'
```

Treat the results as a checklist to review, not text to replace automatically.

### Define its identity, energy, and habitat

1. Append `Rabbit` in [CritterSpecies.cs](../src/Newt.Simulation/CritterSpecies.cs).
2. Add its nutrition record and its `CritterNutritions.Get` mapping in
   [CritterNutrition.cs](../src/Newt.Simulation/CritterNutrition.cs).
3. Add its habitat mapping in
   [CritterHabitat.cs](../src/Newt.Simulation/CritterHabitat.cs).

For example, inside `CritterNutritions`:

```csharp
private static readonly CritterNutrition Rabbit = new(
    BodySize: CritterBodySize.Small,
    FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
    InitialEnergy: 4,
    MaximumEnergy: 8,
    HungryThreshold: 3,
    MetabolismIntervalTicks: 30 * SimulationWorld.TicksPerSecond,
    MetabolismCost: 1,
    ReproductionThreshold: 7,
    ReproductionCost: 4);

// Add to the existing Get switch:
// CritterSpecies.Rabbit => Rabbit,
```

Energy is shared by hunger, survival, and reproduction. Body size also determines
the food energy another animal receives from eating this critter. Declaring
`TerrainOnly` does not implement a food source: wire up actual feeding below.

### Connect behavior

Most integration points are in
[SimulationWorld.cs](../src/Newt.Simulation/SimulationWorld.cs):

| Area | What to check |
| --- | --- |
| Movement dispatch | Include Rabbit in the group calling `TryMoveGrazer`, or add a new behavior method. |
| Movement rate | Add a `GetMovementIntervalTicks` entry. |
| Food | Connect `TryFeedFromTerrain`, `IsGrazerFoliageTile`, and related grazer rules. Decide what gets consumed. |
| Habitat | Review `CanLiveOn`; some species have extra checks beyond the general habitat mapping. |
| Predator relationships | Update `CanEat` for predators that should eat rabbits. Review fleeing rules too. |
| Combat and displacement | Review `GetCombatDamage`, `CanFightBackAgainst`, and `CanDisplace` if relevant. |
| Lifecycle | Reuse the normal metabolism and reproduction flow unless the species needs special rules. |

Generic reproduction finds a valid empty adjacent tile and spends the parent's
energy. Do not subtract the cost again in your movement method. A failed birth
should not consume energy.

For novel behavior, put related methods in a focused partial-class file rather
than making the main world file harder to navigate. Connect the new method to
the existing movement/lifecycle dispatch; creating the file alone does nothing.

### Add tools and visuals

In [NewtGame.cs](../src/Newt.Game/NewtGame.cs), follow Deer through:

- The `WorldTool` enum and critter tool order.
- The input switch that handles critter spawning/removal.
- The tool-to-species mapping and spawn hint.
- Inspection descriptions, behavior text, and fallback color.
- `LoadCritterSprite`, if you have a PNG.

Put sprite PNGs under `src/Newt.Game/Content/Sprites/Critters`. The project already
copies PNGs from that directory into builds and publishes. Simulation code must
not load textures.

### Optional: evolution and natural appearance

In [CritterEvolution.cs](../src/Newt.Simulation/CritterEvolution.cs), add the
parent's branch count, evolved-species mapping, and reverse mapping if the new
species belongs in the evolution tree. Check `ChooseOffspringSpecies` for any
special offspring rules. A tool-only species can skip evolution initially.

Adding an enum entry does not automatically seed the species into generated
worlds. Decide whether it should appear through evolution, player spawning, or
an explicit population-seeding rule.

### Test a small world first

After adding the species, a minimal setup looks like:

```csharp
var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 123);
var position = new GridPosition(5, 5);
world.SetBiome(position, Biome.Grassland);
var rabbit = world.AddCritter(CritterSpecies.Rabbit, position);
world.AdvanceOneTick();
```

Test habitat rejection, feeding and energy gain, starvation, reproduction cost,
blocked birth tiles, and the intended predator/prey relationship. Match the
fixtures to actual food requirements; a Plains tile alone may not provide food.

## 2. Add a building

First decide which building system fits. Ape districts belong to a village;
wolf dens, spider webs, and teleporters have their own state and rules. There is
no single generic building class for all of them.

### Example: an ape Workshop

Use a Lumber Camp as the nearest reference for a timed production building.

1. Append `Workshop` to
   [ApeStructureKind.cs](../src/Newt.Simulation/ApeStructureKind.cs).
2. Define construction requirements in `IsValidApeAuxiliaryTile` and review
   `CanBuildApeStructureOn`. Decide which terrain, biome, water, and surface
   cover combinations are allowed.
3. Define food and wood costs in `GetApeStructureFoodCost` and
   `GetApeStructureWoodCost`.
4. Decide when the village wants one. Add its limit and priority to
   `AdvanceApeVillages` or `GetApeVillageExpansionCandidates`, as appropriate.
5. Initialize any production schedule when `TryBuildApeStructure` succeeds.
6. Add its update to `AdvanceApeVillages`, following `AdvanceApeLumberCamp`.
7. Handle validity and removal through `RemoveInvalidApeStructureAt`,
   `RevalidateApeStructures`, and the structure-removal flow.

The distinction between these methods matters:

```text
TryPurchaseApeStructure
  checks resources
  calls TryBuildApeStructure
  deducts resources only after successful construction

TryBuildApeStructure
  finds a valid connected site
  places the structure and records village ownership
  initializes its schedule
  does not charge resources
```

Use the purchase path for normal paid construction. Calling the lower-level
build method directly makes it free.

For production, define the interval, output, storage cap, and conditions that
pause production. Use `_apeStructureNextActionTicks` or similarly explicit
scheduled state. Do not accidentally produce once per frame or once per tick.

Keep **placement**, **continued structural validity**, and **current operation**
separate. For example, a seasonal biome change can pause an existing farm rather
than destroy it. Review `IsApeStructureOperational` so inspection matches the rule.

### Draw and inspect it

In `NewtGame.cs`, update the ape-structure drawing switch,
`GetApeStructureDisplayName`, and building inspection. Make the UI show the real
output, state, and ownership rather than duplicating simulation rules.

Ape districts are normally built autonomously. Only add a placement tool if you
want direct player placement. For a standalone player-placed building, use the
Wolf Den or Teleporter tool flow as a reference: tool entry, tool ordering,
placement/removal commands, drawing, and inspection.

### Tests for a building

- Exact costs on success; no charge on failed placement.
- Valid and invalid sites, ownership, and connected placement.
- Population limits and actual construction decisions.
- One production event at the intended tick, with correct storage caps.
- Terrain changes, removal, and schedule cleanup.
- No resource deadlock: a building needed to obtain wood should not require
  unavailable wood to build, and full housing should not prevent necessary
  housing-support infrastructure.

## 3. Add a critter with its own building

For a Beaver/Lodge-style feature, first implement the Beaver using section 1.
Then give it a home system. Do not put a Lodge into `ApeStructureKind` unless it
really is an ape-village district.

Two useful references:

| Example | What to learn |
| --- | --- |
| Wolves and dens in `SimulationWorld.cs` | Home/target state, returning home, and den-dependent reproduction (`TryReproduceWolfAtDen`). |
| Spiders and webs in `MegaSpiders.cs` | A focused partial-class system, ownership, building placement, stored food, and detachment. |

### Define ownership and state

For example, inside a new `Beavers.cs` partial class:

```csharp
namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    // Key: stable CritterId.Value. Value: lodge tile index.
    private readonly Dictionary<int, int> _beaverLodgeHomes = [];

    // Key: tile index. Value: stored resources.
    private readonly Dictionary<int, int> _beaverLodgeFood = [];
}
```

This is a starting sketch, not the complete implementation. Decide whether a
lodge has one owner, a family, or multiple unrelated residents. For shared homes,
define capacity and how a newcomer claims a vacancy.

Expose small query/command methods for UI and tests, such as `GetBeaverLodge`,
`GetLodgeFood`, and `TryPlaceLodge`. Keep mutable dictionaries private.

### Specify the behavior before writing the branches

Write down what should happen in each state:

1. **No lodge:** search for or claim a valid lodge site.
2. **Building:** check requirements and spend resources only on success.
3. **Established:** forage, deposit food, maintain the lodge, or defend it.
4. **Ready to reproduce:** check capacity, prioritize returning, then create an
   offspring on a valid tile and assign its home.
5. **Home unavailable:** release stale ownership and choose whether to relocate,
   wait, or resume foraging.

Implement these priorities explicitly in movement and lifecycle methods. A
creature can have enough energy to reproduce yet never get home if hunting
always wins its movement decision.

Reuse a suitable route search, but give it the new species' transit rules and
reachable goals. Colonists can cross terrain that other species cannot. Handle
blocked next steps, changed terrain, failed routes, and retry intervals. Avoid
running a whole-world search for every creature on every tick.

If reproduction requires a lodge, integrate that gate before the generic birth
flow, following the wolf or ape examples. If it replaces the generic birth flow,
ensure only one branch creates offspring and charges energy. Shared housing
needs reservations for births pending in the same tick, as ape villages use.

### Wire in updates and cleanup

If lodges decay or produce resources, add an `AdvanceBeaverLodges` call to the
world tick in a deliberate order. Existing calls include `AdvanceWolfDens` and
`AdvanceMegaSpiderWebs`.

Handle all of these transitions:

- Owner dies or is removed: detach its stable ID.
- Owner changes species: detach state it can no longer use.
- Lodge is removed: clear affected homes, targets, and schedules.
- Terrain becomes invalid: remove, suspend, or relocate according to the design.
- A cached route or target becomes invalid: discard it and retry appropriately.

See `RemoveCritterAtIndex`, `ChangeCritterSpecies`, and `DetachMegaSpiderFromWeb`.
Array compaction makes persistent array-index ownership particularly dangerous.

Add building rendering and inspection, plus the critter's home and current task
to its inspection text. This makes a stuck creature much easier to diagnose.

### Test the complete relationship

Test the critter and building together: establishing a home, returning around an
obstacle, resource transfer, reproduction and offspring ownership, full capacity,
owner death, species change, building destruction, and two creatures competing
for the same site. Also verify that removing an unrelated critter does not break
ownership when array indexes change.

## Build, test, and try it

From the repository root:

```powershell
dotnet build Newt.sln
dotnet test tests/Newt.Simulation.Tests/Newt.Simulation.Tests.csproj
dotnet run --project src/Newt.Game/Newt.Game.csproj
```

Keep tests deterministic: fixed seeds, small worlds, and explicit terrain and
food. Disable seasons or natural events in focused tests when those systems are
not what you are testing. Use existing fixtures to see how.

Finally, spawn the feature in the game and inspect its energy, ownership, target,
and production. A successful build does not prove the new behavior is reachable.
Existing release ZIPs are snapshots; rebuild the Windows package separately when
you want friends to receive the new feature.
