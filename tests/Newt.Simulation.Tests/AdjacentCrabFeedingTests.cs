using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class AdjacentCrabFeedingTests
{
    [Theory]
    [InlineData(CritterSpecies.Newt, 1, true)]
    [InlineData(CritterSpecies.Newt, 7, true)]
    [InlineData(CritterSpecies.Newt, 2, false)]
    [InlineData(CritterSpecies.Therapsid, 1, true)]
    [InlineData(CritterSpecies.Therapsid, 7, true)]
    [InlineData(CritterSpecies.Therapsid, 2, false)]
    public void MovementSelectsOnlyAdjacentCrabsAndEncounterFeeds(CritterSpecies species, int crabX, bool adjacent)
    {
        var world = new SimulationWorld(8, 3, Terrain.Plains, seed: 17);
        var predatorTile = new GridPosition(0, 1);
        var crabTile = new GridPosition(crabX, 1);
        world.SetTerrain(crabTile, Terrain.Beach);
        world.AddCritter(species, predatorTile);
        var crab = world.AddCritter(CritterSpecies.Crab, crabTile);
        var energy = world.GetCritter(0).Energy;
        var method = species is CritterSpecies.Newt ? "TryMoveNewt" : "TryMoveTherapsid";
        var target = (GridPosition?)typeof(SimulationWorld).GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, null]);
        Assert.Equal(adjacent ? (GridPosition?)crabTile : null, target);
        if (target is { } prey) world.CommitEncounter(predatorTile, prey);
        Assert.Equal(!adjacent, world.TryGetCritter(crab, out _));
        if (adjacent) Assert.True(world.GetCritter(0).Energy > energy);
    }
}
