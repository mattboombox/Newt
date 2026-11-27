using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LumberCampRecoveryTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void VillageCanReplaceLostLumberCampWithNoWoodOrFoodAndResumeProduction(int population)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var world = new SimulationWorld(40, 30, Terrain.Plains)
            { SeasonsEnabled = false, ApeVillageMergingEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        var home = village.Y * world.Width + village.X;
        Assert.True(world.TrySpawnTestApeVillage(village));
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", flags)!.GetValue(world)!;
        for (var i = 2; i < population; i++)
            homes[world.AddCritter(CritterSpecies.Ape, new(20 + i, 20)).Value] = home;
        Assert.True(world.TryBuildApeStructure(home, ApeStructureKind.LumberCamp));
        IEnumerable<GridPosition> Camps() => Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is ApeStructureKind.LumberCamp);
        Assert.True(world.RemoveApeStructureAt(Assert.Single(Camps())));
        var wood = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageWood", flags)!.GetValue(world)!;
        var food = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageFood", flags)!.GetValue(world)!;
        wood[home] = food[home] = 0;
        var tick = typeof(SimulationWorld).GetProperty(nameof(world.Tick))!;
        tick.SetValue(world, 1L);
        var manage = typeof(SimulationWorld).GetMethod("AdvanceApeVillages", flags)!;
        manage.Invoke(world, null);
        var camp = Assert.Single(Camps());
        Assert.Equal(0, world.GetApeVillageWood(village));
        Assert.Equal(0, world.GetApeVillageFood(village));
        tick.SetValue(world, 31L * SimulationWorld.TicksPerSecond);
        manage.Invoke(world, null);
        Assert.Single(Camps());
        Assert.True(world.IsApeStructureOperational(camp));
        var produce = typeof(SimulationWorld).GetMethod("AdvanceApeLumberCamp", flags)!;
        tick.SetValue(world, 68L * SimulationWorld.TicksPerSecond);
        produce.Invoke(world, [home, camp.Y * world.Width + camp.X]);
        Assert.True(world.GetApeVillageWood(village) > 0);
    }
}
