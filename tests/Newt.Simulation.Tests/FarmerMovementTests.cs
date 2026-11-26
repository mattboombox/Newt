using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class FarmerMovementTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssignedFarmerReachesExactFarmThenStaysEvenWhenHungryOrReadyToBreed(bool fullEnergy)
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains, seed: 17) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        var home = new GridPosition(10, 10);
        var farm = new GridPosition(11, 10);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(home));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        Assert.True(world.TryPlaceApeBuilding(farm, ApeStructureKind.Farm));
        var farmer = world.AddCritter(CritterSpecies.Ape, new(12, 10));
        Assert.True(world.TryAssignApeToVillage(farmer, home));
        typeof(SimulationWorld).GetMethod("AssignApeFarmer", Private)!
            .Invoke(world, [farmer.Value, farm.Y * world.Width + farm.X]);
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energy[0] = fullEnergy ? 12 : 2;
        var move = typeof(SimulationWorld).GetMethod("TryMoveApe", Private)!;
        move.Invoke(world, [0, null]);
        Assert.Equal(farm, world.GetCritter(0).Position);
        world.AddCritter(CritterSpecies.Monkey, new(12, 10));
        for (var turn = 0; turn < 30; turn++) move.Invoke(world, [0, null]);
        Assert.Equal(farm, world.GetCritter(0).Position);
        Assert.Equal(fullEnergy ? 12 : 2, world.GetCritter(0).Energy);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
        var paths = (Dictionary<int, (int TargetTile, Queue<int> Tiles)>)typeof(SimulationWorld)
            .GetField("_apeWorkerPaths", Private)!.GetValue(world)!;
        Assert.False(paths.ContainsKey(farmer.Value));
        Assert.True(world.IsApeStructureStaffed(farm));
    }
}
