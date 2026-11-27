using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MilitaryConstructionTests
{
    [Fact]
    public void SavingForMilitaryDoesNotBlockFoodExpansionWhenNoMilitarySiteExists()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17);
        NaturalEvents.SetEnabled(world, false);
        var village = new GridPosition(10, 10);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var home = village.Y * world.Width + village.X;
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", flags)!.GetValue(world)!;
        for (var i = 0; i < 18; i++)
            homes[world.AddCritter(CritterSpecies.Ape, new(20 + i % 10, 20 + i / 10)).Value] = home;
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (new GridPosition(x, y) != village) world.SetTerrain(new(x, y), Terrain.Ocean);
        var aquaculture = new GridPosition(10, 11);
        world.RemoveCritterAt(aquaculture);
        world.SetTerrain(aquaculture, Terrain.Shallows);
        var wood = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageWood", flags)!.GetValue(world)!;
        wood[home] = 2;

        Assert.False(world.TryBuildApeStructure(home, ApeStructureKind.MilitaryDistrict));
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryBuildNextApeVillageExpansion", flags)!
            .Invoke(world, [home])!);
        Assert.True(world.GetApeStructure(aquaculture) is ApeStructureKind.Farm or ApeStructureKind.Aquaculture);
    }

    [Fact]
    public void GrowingVillageSavesForMilitaryDistrictInsteadOfSpendingEverySmallWoodDelivery()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var world = new SimulationWorld(60, 40, Terrain.Plains, seed: 17)
            { SeasonsEnabled = false, ApeVillageMergingEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var home = village.Y * world.Width + village.X;
        for (var i = 0; i < 5; i++) Assert.True(world.TryBuildApeStructure(home, ApeStructureKind.ResidentialDistrict));
        Assert.True(world.TryBuildApeStructure(home, ApeStructureKind.Farm));
        Assert.True(world.TryBuildApeStructure(home, ApeStructureKind.LumberCamp));
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", flags)!.GetValue(world)!;
        var wood = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageWood", flags)!.GetValue(world)!;
        var food = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageFood", flags)!.GetValue(world)!;
        wood[home] = 0;
        var resident = 0;
        for (var delivery = 1; delivery <= 12; delivery++)
        {
            while (world.GetApeVillageResidentCount(village) < world.GetApeVillagePopulationCapacity(village))
            {
                var id = world.AddCritter(CritterSpecies.Ape, new(30 + resident % 20, 25 + resident / 20));
                homes[id.Value] = home;
                resident++;
            }
            food[home] = 50;
            wood[home] += 1;
            typeof(SimulationWorld).GetProperty(nameof(world.Tick))!.SetValue(world, (long)delivery);
            typeof(SimulationWorld).GetMethod("AdvanceApeVillages", flags)!.Invoke(world, null);
        }
        Assert.Contains(Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width)),
            position => world.GetApeStructure(position) is ApeStructureKind.MilitaryDistrict &&
                world.GetApeStructureVillage(position) == village);
    }
}
