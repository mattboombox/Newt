using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class EmergencyFarmTests
{
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
