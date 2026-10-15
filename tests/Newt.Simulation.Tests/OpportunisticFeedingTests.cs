using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class OpportunisticFeedingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(8)]
    public void DriftingJellyfishEatPlanktonInsteadOfShovingEvenWhenFull(int energy)
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 17);
        var jellyfish = world.AddCritter(CritterSpecies.Jellyfish, new(0, 0));
        var plankton = world.AddCritter(CritterSpecies.Plankton, new(0, 1));
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energies[0] = energy;
        Assert.True(world.GetCritter(0).IsHunting);
        var destination = (GridPosition?)typeof(SimulationWorld).GetMethod("TryMove", Private)!
            .Invoke(world, [0, null, true]);
        Assert.Equal(new GridPosition(0, 1), destination);
        world.CommitEncounter(new(0, 0), destination!.Value);
        Assert.False(world.TryGetCritter(plankton, out _));
        Assert.True(world.TryGetCritter(jellyfish, out var fed));
        Assert.Equal(new GridPosition(0, 1), fed.Position);
        Assert.Equal(Math.Min(8, energy + 1), fed.Energy);
    }

    [Fact]
    public void MonkeysEatSnailsAndDoNotHuntNewts()
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(CritterSpecies.Monkey, CritterSpecies.Snail, 1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(CritterSpecies.Monkey, CritterSpecies.Snail, 2));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Monkey, CritterSpecies.Newt));
        var world = new SimulationWorld(10, 1, Terrain.Plains, seed: 17);
        var monkey = world.AddCritter(CritterSpecies.Monkey, new(0, 0));
        var snail = world.AddCritter(CritterSpecies.Snail, new(1, 0));
        var destination = (GridPosition?)typeof(SimulationWorld).GetMethod("TryMoveMonkey", Private)!
            .Invoke(world, [0, null]);
        Assert.Equal(new GridPosition(1, 0), destination);
        world.CommitEncounter(new(0, 0), destination!.Value);
        Assert.False(world.TryGetCritter(snail, out _));
        Assert.True(world.TryGetCritter(monkey, out var fed));
        Assert.Equal(5, fed.Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Snail, 5, true)]
    [InlineData(CritterSpecies.Crab, 5, true)]
    [InlineData(CritterSpecies.Snail, 6, false)]
    [InlineData(CritterSpecies.Crab, 6, false)]
    public void MonkeysNeverTrackDistantSnailsOrCrabs(CritterSpecies prey, int energy, bool hunts)
    {
        var world = new SimulationWorld(20, 1, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Monkey, new(0, 0));
        var target = new GridPosition(3, 0);
        world.AddCritter(prey, target);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energies[0] = energy;
        Assert.Equal(hunts, world.GetCritter(0).IsHunting);
        Assert.Null(world.FindHunterPrey(0, CritterSpecies.Monkey, 4, null));
        world.CommitEncounter(new(0, 0), target);
        Assert.Equal(2, world.CritterCount);
    }
}
