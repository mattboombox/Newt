using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianColonistTests
{
    private static readonly GridPosition Camp = new(10, 10);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(100, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        return world;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    [InlineData(50)]
    public void CampEventLaunchesWithAnyFoodAndConsumesUpToFifty(int food)
    {
        var world = CreateWorld();
        world.StoreApeVillageFood(Camp, food);
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!.SetValue(world,
            (long)(60 * SimulationWorld.TicksPerSecond));
        var advance = typeof(SimulationWorld).GetMethod("AdvanceApeVillages", PrivateInstance)!;
        for (var roll = 0; roll < 5000 && !Residents(world).Any(critter => critter.IsColonist); roll++)
            advance.Invoke(world, null);
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        Assert.Equal(CritterSpecies.Ape, colonist.Species);
        Assert.False(world.IsBarbarianApe(colonist.Id));
        Assert.Equal(0, world.GetApeVillageFood(Camp));
        Assert.NotNull(colonist.ColonistDestination);
    }

    [Fact]
    public void OriginApesAndDogsCannotAttackColonistAndRegularWarriorsDoNotTargetIt()
    {
        var world = CreateWorld();
        Assert.True(world.TrySendApeColonist(Camp));
        var chief = Residents(world).Single(critter => critter.Species == CritterSpecies.ApeChieftain);
        var chiefIndex = Index(world, chief.Id);
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(Camp) < 5; roll++)
            world.TryRecruitDogFromWolfKill(chiefIndex);
        Assert.Equal(5, world.GetApeVillageDogCount(Camp));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var regular = world.AddCritter(CritterSpecies.ApeWarrior, new(80, 25));
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", PrivateInstance)!;
        foreach (var resident in Residents(world).Where(critter => critter.Id != colonist.Id))
            Assert.False((bool)canAttack.Invoke(world, [Index(world, resident.Id), Index(world, colonist.Id)])!);
        Assert.False(world.IsBarbarianApe(regular));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ColonistVillageTypeUsesOriginChance(bool barbarian, bool regularOrigin)
    {
        var world = CreateWorld();
        if (regularOrigin)
        {
            var camps = (HashSet<int>)typeof(SimulationWorld).GetField("_barbarianVillageTiles", PrivateInstance)!.GetValue(world)!;
            camps.Clear();
        }
        var destination = Enumerable.Range(50, 35).Select(x => new GridPosition(x, 10))
            .First(position => ((world.Seed ^ ((ulong)(uint)(position.Y * world.Width + position.X) *
                2_654_435_761UL)) % 100 < (regularOrigin ? 25UL : 50UL)) == barbarian);
        Assert.True(world.TrySendApeColonist(destination));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var advance = typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!;
        for (var step = 0; step < 200 && world.GetApeStructure(destination) is null; step++)
            advance.Invoke(world, [Index(world, colonist.Id)]);
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(destination));
        Assert.Equal(barbarian, world.IsBarbarianVillage(destination));
        Assert.True(world.TryGetCritter(colonist.Id, out var founder));
        Assert.False(founder.IsColonist);
        Assert.Equal(barbarian, world.IsBarbarianApe(founder.Id));
    }

    [Theory]
    [InlineData(1, Terrain.Plains, true)]
    [InlineData(2, Terrain.Plains, false)]
    [InlineData(1, Terrain.Shallows, false)]
    public void LowEnergyColonistSettlesImmediatelyNearOrigin(int energy, Terrain terrain, bool settles)
    {
        var world = CreateWorld();
        Assert.True(world.TrySendApeColonist(new(60, 10)));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        // Emergency settlement is reserved for automatically dispatched colonists.
        ((HashSet<int>)typeof(SimulationWorld).GetField("_apeFixedSettlerDestinations", PrivateInstance)!
            .GetValue(world)!).Remove(colonist.Id.Value);
        var index = Index(world, colonist.Id);
        var position = new GridPosition(12, 10);
        world.SetTerrain(position, terrain);
        typeof(SimulationWorld).GetMethod("MoveCritter", PrivateInstance)!
            .Invoke(world, [index, position.Y * world.Width + position.X, position, false]);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", PrivateInstance)!.GetValue(world)!;
        energies[index] = energy;

        typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!.Invoke(world, [index]);

        Assert.Equal(settles ? ApeStructureKind.Village : (ApeStructureKind?)null,
            world.GetApeStructure(position));
        Assert.True(world.TryGetCritter(colonist.Id, out var founder));
        Assert.Equal(!settles, founder.IsColonist);
        if (settles)
            Assert.Equal(CritterNutritions.Get(CritterSpecies.Ape).InitialEnergy, founder.Energy);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ColonistFoundsOccupiedDestinationWithoutRemovingCritter(bool automatic, bool occupiedBeforeLaunch)
    {
        var world = CreateWorld();
        var destination = new GridPosition(60, 10);
        CritterId blocker = default;
        if (!automatic && occupiedBeforeLaunch)
            blocker = world.AddCritter(CritterSpecies.Therapsid, destination);
        Assert.True(world.TrySendApeColonist(automatic ? Camp : destination));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        destination = colonist.ColonistDestination!.Value;
        if (automatic || !occupiedBeforeLaunch)
            blocker = world.AddCritter(CritterSpecies.Therapsid, destination);

        var advance = typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!;
        for (var step = 0; step < 200 && world.GetApeStructure(destination) is null; step++)
        {
            advance.Invoke(world, [Index(world, colonist.Id)]);
            Assert.True(world.TryGetCritter(colonist.Id, out var traveling));
            if (traveling.IsColonist)
                Assert.Equal(destination, traveling.ColonistDestination);
        }

        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(destination));
        Assert.True(world.TryGetCritter(blocker, out var occupant));
        Assert.Equal(destination, occupant.Position);
        Assert.True(world.TryGetCritter(colonist.Id, out var founder));
        Assert.False(founder.IsColonist);
    }

    [Fact]
    public void AutomaticSiteSelectionIncludesOccupiedTiles()
    {
        var world = CreateWorld();
        var occupied = new HashSet<int>();
        for (var x = 50; x < 85; x += 2)
        for (var y = 2; y < 28; y += 2)
        {
            world.AddCritter(CritterSpecies.Therapsid, new(x, y));
            occupied.Add(y * world.Width + x);
        }
        var select = typeof(SimulationWorld).GetMethod("FindDistantApeVillageSite", PrivateInstance)!;
        var origin = Camp.Y * world.Width + Camp.X;
        Assert.Contains(Enumerable.Range(0, 100).Select(_ => (int)select.Invoke(world, [origin])!),
            tile => occupied.Contains(tile));
    }

    [Fact]
    public void ToolColonistKeepsDestinationThroughLowEnergyAndTemporaryInvalidity()
    {
        var world = CreateWorld();
        var destination = new GridPosition(60, 10);
        Assert.True(world.TrySendApeColonist(destination));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var index = Index(world, colonist.Id);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", PrivateInstance)!.GetValue(world)!;
        energies[index] = 1;
        world.SetTerrain(destination, Terrain.Mountain);
        var advance = typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!;
        for (var step = 0; step < 100; step++)
        {
            advance.Invoke(world, [index]);
            Assert.True(world.TryGetCritter(colonist.Id, out var waiting));
            Assert.True(waiting.IsColonist);
            Assert.Equal(destination, waiting.ColonistDestination);
        }
        world.SetTerrain(destination, Terrain.Plains);
        for (var step = 0; step < 200 && world.GetApeStructure(destination) is null; step++)
            advance.Invoke(world, [index]);
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(destination));
    }

    [Theory]
    [InlineData(Terrain.Plains, CritterSpecies.Therapsid, false)]
    [InlineData(Terrain.Plains, CritterSpecies.Therapsid, true)]
    [InlineData(Terrain.Ocean, CritterSpecies.Fish, false)]
    [InlineData(Terrain.Ocean, CritterSpecies.Fish, true)]
    public void ColonistShovesAndSwapsThroughBlockedLandAndWaterRoutes(
        Terrain terrain, CritterSpecies blockerSpecies, bool requireSwap)
    {
        var world = CreateWorld();
        Assert.True(world.TrySendApeColonist(new(60, 10)));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var index = Index(world, colonist.Id);
        for (var x = 19; x <= 23; x++)
        {
            world.SetTerrain(new(x, 14), Terrain.Mountain);
            world.SetTerrain(new(x, 16), Terrain.Mountain);
        }
        for (var x = 20; x <= 23; x++)
            world.SetTerrain(new(x, 15), terrain);
        var origin = new GridPosition(20, 15);
        typeof(SimulationWorld).GetMethod("MoveCritter", PrivateInstance)!
            .Invoke(world, [index, 15 * world.Width + 20, origin, false]);
        var blocker = world.AddCritter(blockerSpecies, new(21, 15));
        if (requireSwap)
            world.AddCritter(blockerSpecies, new(22, 15));
        typeof(SimulationWorld).GetMethod("MoveApeSettlerToward", PrivateInstance)!
            .Invoke(world, [index, 15 * world.Width + 30]);
        Assert.Equal(new GridPosition(21, 15), world.GetCritter(index).Position);
        Assert.True(world.TryGetCritter(blocker, out var displaced));
        Assert.Equal(requireSwap ? origin : new GridPosition(22, 15), displaced.Position);
        Assert.Equal(Residents(world).Length, Residents(world).Select(critter => critter.Position).Distinct().Count());
    }

    private static CritterSnapshot[] Residents(SimulationWorld world) =>
        Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();

    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
}

