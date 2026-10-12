using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BaleenWhaleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void BaleenWhaleFeedsDuringSimulationInAnOceanFilledWithPlankton()
    {
        var world = new SimulationWorld(10, 3, Terrain.Ocean, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(5, 1);
        var whale = world.AddCritter(CritterSpecies.BaleenWhale, position);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (new GridPosition(x, y) != position)
                world.AddCritter(CritterSpecies.Plankton, new(x, y));
        var before = world.GetCritterCount(CritterSpecies.Plankton);
        for (var tick = 0; tick < SimulationWorld.GetMovementIntervalTicks(CritterSpecies.BaleenWhale); tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(whale, out var fed));
        Assert.Equal(CritterNutritions.Get(CritterSpecies.BaleenWhale).InitialEnergy + 2, fed.Energy);
        Assert.True(world.GetCritterCount(CritterSpecies.Plankton) < before);
    }

    [Fact]
    public void CrowdedBaleenWhaleEatsPlanktonInsteadOfShovingAndReachesBreedingEnergy()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var world = new SimulationWorld(10, 3, Terrain.Ocean, (ulong)seed);
            world.AddCritter(CritterSpecies.BaleenWhale, new(2, 1));
            foreach (var position in new[] { new GridPosition(1, 1), new GridPosition(3, 1),
                new GridPosition(2, 0), new GridPosition(2, 2) })
                world.AddCritter(CritterSpecies.Plankton, position);
            var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
            energies[0] = 29;
            var target = (GridPosition?)typeof(SimulationWorld).GetMethod("TryMoveHunter", Private)!
                .Invoke(world, [0, SimulationWorld.BaleenWhalePerceptionRadius, null, false, null]);
            Assert.NotNull(target);
            Assert.Equal(new GridPosition(2, 1), world.GetCritter(0).Position);
            world.CommitEncounter(new(2, 1), target!.Value);
            Assert.Equal(3, world.GetCritterCount(CritterSpecies.Plankton));
            Assert.Equal(30, world.GetCritter(0).Energy);
            Assert.True(world.GetCritter(0).CanReproduce);
        }
    }

    [Fact]
    public void BaleenWhalePushesBlockersTowardPlanktonInsteadOfWandering()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var world = new SimulationWorld(20, 3, Terrain.Ocean, (ulong)seed);
            world.AddCritter(CritterSpecies.BaleenWhale, new(2, 1));
            world.AddCritter(CritterSpecies.Plankton, new(5, 1));
            foreach (var position in new[] { new GridPosition(1, 1), new GridPosition(3, 1),
                new GridPosition(2, 0), new GridPosition(2, 2) })
                world.AddCritter(CritterSpecies.Nautilus, position);
            typeof(SimulationWorld).GetMethod("TryMoveHunter", Private)!
                .Invoke(world, [0, SimulationWorld.BaleenWhalePerceptionRadius, null, false, null]);
            Assert.Equal(new GridPosition(3, 1), world.GetCritter(0).Position);
        }
    }
}
