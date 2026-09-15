using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SmallPreyTests
{
    [Theory]
    [InlineData(CritterSpecies.Nautilus)]
    [InlineData(CritterSpecies.Newt)]
    [InlineData(CritterSpecies.Crab)]
    [InlineData(CritterSpecies.Monkey)]
    [InlineData(CritterSpecies.Trilobite)]
    [InlineData(CritterSpecies.Worm)]
    public void EveryPredatorRequiresSmallPreyToBeAdjacent(CritterSpecies prey)
    {
        foreach (var predator in Enum.GetValues<CritterSpecies>())
        {
            if (!SimulationWorld.CanEat(predator, prey))
                continue;
            Assert.True(SimulationWorld.CanPursuePreyAtDistance(predator, prey, 1));
            Assert.False(SimulationWorld.CanPursuePreyAtDistance(predator, prey, 2));
            Assert.False(SimulationWorld.CanPursuePreyAtDistance(predator, prey, 6));
        }

        foreach (var position in new[] { new GridPosition(1, 3), new GridPosition(1, 4),
            new GridPosition(8, 4), new GridPosition(2, 3), new GridPosition(7, 3) })
        {
            var world = new SimulationWorld(9, 9, Terrain.Shallows, seed: 90);
            world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 3));
            world.AddCritter(prey, position);
            var adjacent = position.X is 1 or 8;
            Assert.Equal(adjacent ? (GridPosition?)position : null,
                world.FindHunterPrey(0, CritterSpecies.MegaToad, 6, null));
            if (!adjacent)
            {
                world.CommitEncounter(new GridPosition(0, 3), position);
                Assert.Equal(1, world.GetCritterCount(prey));
            }
        }
    }
}
