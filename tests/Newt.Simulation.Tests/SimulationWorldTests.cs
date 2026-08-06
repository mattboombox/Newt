using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SimulationWorldTests
{
    [Fact]
    public void SameSeedProducesSameMovement()
    {
        var first = CreateOceanWorld(42);
        var second = CreateOceanWorld(42);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
        }

        Assert.Equal(first.GetCritter(0), second.GetCritter(0));
    }

    [Fact]
    public void OccupiedTileRejectsAnotherCritter()
    {
        var world = CreateOceanWorld(1);

        Assert.Throws<InvalidOperationException>(() =>
            world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2)));
    }

    [Fact]
    public void PlanktonUsesItsFiveSecondMovementInterval()
    {
        var world = CreateOceanWorld(7);
        var initialPosition = world.GetCritter(0).Position;

        for (var tick = 0; tick < 99; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(initialPosition, world.GetCritter(0).Position);
        world.AdvanceOneTick();
        Assert.NotEqual(initialPosition, world.GetCritter(0).Position);
    }

    private static SimulationWorld CreateOceanWorld(ulong seed)
    {
        var world = new SimulationWorld(8, 8, Terrain.Ocean, seed);
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2));
        return world;
    }
}
