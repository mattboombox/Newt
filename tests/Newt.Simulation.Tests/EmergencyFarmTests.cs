using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class EmergencyFarmTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape, false)]
    [InlineData(CritterSpecies.ApeWarrior, true)]
    [InlineData(CritterSpecies.ApeSailor, true)]
    public void LastResidentTakesOverAFarmImmediately(CritterSpecies species, bool existingFarm)
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);

        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        while (world.CritterCount > 0)
            world.RemoveCritterAt(world.GetCritter(0).Position);
        if (existingFarm)
            Assert.True(world.TryBuildApeStructure(village.Y * world.Width + village.X, ApeStructureKind.Farm));

        var survivorPosition = village;
        if (species is CritterSpecies.ApeSailor)
        {
            survivorPosition = new GridPosition(9, 9);
            world.SetTerrain(survivorPosition, Terrain.Beach);
        }
        var survivor = world.AddCritter(species, survivorPosition);
        Assert.True(world.TryAssignApeToVillage(survivor, village));
        var neighbor = new GridPosition(11, 11);
        var lost = world.AddCritter(CritterSpecies.Ape, neighbor);
        Assert.True(world.TryAssignApeToVillage(lost, village));
        Assert.True(world.RemoveCritterAt(neighbor));

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetApeVillageResidentCount(village));
        Assert.Equal(1, world.GetApeVillageFarmCount(village));
        Assert.True(world.TryGetCritter(survivor, out var farmer));
        Assert.Equal(CritterSpecies.ApeFarmer, farmer.Species);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void StaffedVillageAddsFarmBelowTenFoodWithoutHousingRequirement(int food, bool builds)
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var extra = world.AddCritter(CritterSpecies.Ape, new(15, 15));
        Assert.True(world.TryAssignApeToVillage(extra, village));
        for (var tick = 0; tick <= 30 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.Equal(1, world.GetApeVillageFarmCount(village));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ApeFarmer));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
        var tile = village.Y * world.Width + village.X;
        // Isolate the shortage decision after the recruitment/harvest grace period.
        var stores = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageFood",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        stores[tile] = food;
        var checks = (Dictionary<int, long>)typeof(SimulationWorld).GetField("_apeFoodExpansionTicks",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        checks[tile] = world.Tick;
        var wood = world.GetApeVillageWood(village);
        world.AdvanceOneTick();
        Assert.Equal(builds ? 2 : 1, world.GetApeVillageFarmCount(village));
        Assert.Equal(wood - (builds ? 2 : 0), world.GetApeVillageWood(village));
    }
}
