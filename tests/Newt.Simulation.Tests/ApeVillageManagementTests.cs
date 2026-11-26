using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeVillageManagementTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [Fact]
    public void PopulationCacheTracksSameTickReassignmentRoleChangeAndCompactedDeath()
    {
        var world = new SimulationWorld(60, 30, Terrain.Plains) { ApeVillageMergingEnabled = false };
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var first = new GridPosition(10, 10);
        var second = new GridPosition(40, 10);
        Assert.True(world.TrySpawnTestApeVillage(first));
        Assert.True(world.TrySpawnTestApeVillage(second));
        Assert.Equal(2, world.GetApeVillageResidentCount(first));
        Assert.Equal(2, world.GetApeVillageResidentCount(second));
        var ape = world.AddCritter(CritterSpecies.Ape, new(20, 10));
        Assert.True(world.TryAssignApeToVillage(ape, first));
        Assert.Equal(3, world.GetApeVillageResidentCount(first));
        Assert.True(world.TryAssignApeToVillage(ape, second));
        Assert.Equal(2, world.GetApeVillageResidentCount(first));
        Assert.Equal(3, world.GetApeVillageResidentCount(second));
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == ape);
        typeof(SimulationWorld).GetMethod("ChangeCritterSpecies", Private)!
            .Invoke(world, [index, CritterSpecies.ApeTrader, true, true]);
        Assert.Equal(3, world.GetApeVillageResidentCount(second));
        Assert.Equal(2, (int)typeof(SimulationWorld).GetMethod("GetApeVillageSustainingPopulation", Private)!
            .Invoke(world, [second.Y * world.Width + second.X])!);
        world.RemoveCritterAt(world.GetCritter(0).Position);
        Assert.Equal(1, world.GetApeVillageResidentCount(first));
        Assert.True(world.TryGetCritter(ape, out var compacted));
        world.RemoveCritterAt(compacted.Position);
        Assert.Equal(2, world.GetApeVillageResidentCount(second));
    }

    [Fact]
    public void LargeVillageManagementIsStaggeredAndSmallVillageRemainsImmediate()
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains);
        var due = typeof(SimulationWorld).GetMethod("IsVillageManagementDue", Private)!;
        bool Check(int village, int population) => (bool)due.Invoke(world, [village, population])!;
        Assert.True(Check(0, 100));
        Assert.True(Check(1, 100));
        Assert.False(Check(0, 100));
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, 1L);
        Assert.True(Check(1, 100));
        Assert.False(Check(0, 100));
        Assert.True(Check(0, 1));
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, (long)SimulationWorld.TicksPerSecond);
        Assert.True(Check(0, 100));
        Assert.False(Check(1, 100));
    }

    [Fact]
    public void BuildingCacheRefreshesForSameTickConstructionConversionAndRemoval()
    {
        var world = new SimulationWorld(30, 30, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        var farm = new GridPosition(11, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        world.RemoveCritterAt(farm);
        Assert.Equal(5, world.GetApeVillageFoodCapacity(village));
        Assert.True(world.TryPlaceApeBuilding(farm, ApeStructureKind.Farm));
        Assert.Equal(15, world.GetApeVillageFoodCapacity(village));
        typeof(SimulationWorld).GetMethod("SetApeStructure", Private)!
            .Invoke(world, [farm.Y * world.Width + farm.X, ApeStructureKind.ResidentialDistrict]);
        Assert.Equal(5, world.GetApeVillageFoodCapacity(village));
        Assert.Equal(8, world.GetApeVillagePopulationCapacity(village));
        Assert.True(world.RemoveApeStructureAt(farm));
        Assert.Equal(5, world.GetApeVillagePopulationCapacity(village));
    }
}
