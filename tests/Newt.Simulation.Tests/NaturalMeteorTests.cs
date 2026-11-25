using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class NaturalMeteorTests
{
    [Fact]
    public void NaturalMeteorUsesLauncherAndWaitsForArrivalWhenPresentationIsAttached()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var probe = new SimulationWorld(30, 30, Terrain.Plains, seed);
            var expected = new GridPosition(probe.NextInt(30), probe.NextInt(30));
            if (probe.NextInt(4) != 0) continue;
            var world = new SimulationWorld(30, 30, Terrain.Plains, seed);
            GridPosition? target = null;
            float magnitude = 0;
            world.NaturalMeteorLauncher = (tile, size) => { target = tile; magnitude = size; };
            var before = world.GetElevation(expected);

            Assert.True(NaturalEvents.SpawnRandom(world));

            Assert.Equal(expected, target);
            Assert.InRange(magnitude, 0.08f, 0.65f);
            Assert.Equal(before, world.GetElevation(expected));
            Assert.Empty(world.ImpactWaves);
            Impacts.CreateToolMeteorImpact(world, target!.Value, magnitude);
            Assert.True(world.GetElevation(expected) < before);
            Assert.NotEmpty(world.ImpactWaves);
            return;
        }
        Assert.Fail("No meteor seed found in the deterministic sample.");
    }
}
