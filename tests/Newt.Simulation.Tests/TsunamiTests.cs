using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class TsunamiTests
{
    [Fact]
    public void TsunamiMustStartInOcean()
    {
        var world = CreateCoastalWorld();

        Assert.False(Tsunamis.Create(world, new GridPosition(15, 10), 0.5f));
        Assert.Equal(0, world.ActiveImpactWaveCount);
    }

    [Fact]
    public void TsunamiUsesBlueWaveKindAndLowersReachedLand()
    {
        var world = CreateCoastalWorld();
        var coast = new GridPosition(10, 10);
        var originalElevation = world.GetElevation(coast);

        Assert.True(Tsunamis.Create(world, new GridPosition(5, 10), 0.8f));
        Assert.Equal(WaveKind.Tsunami, world.GetImpactWave(0).Kind);
        while (world.ActiveImpactWaveCount > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetElevation(coast) < originalElevation);
    }

    [Fact]
    public void TsunamiDestroysReachedWolfDenAndStoredCharges()
    {
        var world = CreateCoastalWorld();
        var coast = new GridPosition(10, 10);
        Assert.True(world.AddWolfDenCharge(coast));

        Assert.True(Tsunamis.Create(world, new GridPosition(5, 10), 0.8f));
        while (world.ActiveImpactWaveCount > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.Null(world.GetWolfDenCharges(coast));
    }

    [Fact]
    public void NaturalEventsCanSpawnTsunamisAndScheduleNextEvent()
    {
        // Seed 3 selects a tsunami for the first natural event.
        var world = new SimulationWorld(31, 21, Terrain.DeepOcean, seed: 3);
        world.NextNaturalEventTick = 0;

        NaturalEvents.Advance(world);

        Assert.Equal(1, world.ActiveImpactWaveCount);
        var wave = world.GetImpactWave(0);
        Assert.Equal(WaveKind.Tsunami, wave.Kind);
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(wave.Center));
        Assert.InRange(world.NextNaturalEventTick,
            6L * 60 * SimulationWorld.TicksPerSecond,
            16L * 60 * SimulationWorld.TicksPerSecond);
    }

    [Fact]
    public void RandomTsunamiDoesNotStartOnLand()
    {
        var world = new SimulationWorld(31, 21, Terrain.Plains, seed: 3);
        world.NextNaturalEventTick = 0;

        NaturalEvents.Advance(world);

        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.Equal(0, world.VolcanoCount);
        Assert.True(world.NextNaturalEventTick > world.Tick);
    }

    [Fact]
    public void NaturalEventsCanBeDisabled()
    {
        var world = new SimulationWorld(31, 21, Terrain.DeepOcean, seed: 3);
        world.NextNaturalEventTick = 0;

        NaturalEvents.SetEnabled(world, false);
        NaturalEvents.Advance(world);

        Assert.False(world.NaturalEventsEnabled);
        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.Equal(0, world.NextNaturalEventTick);
    }

    private static SimulationWorld CreateCoastalWorld()
    {
        var world = new SimulationWorld(31, 21, Terrain.DeepOcean, seed: 81);
        world.OceanSeed = new GridPosition(0, 10);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 10; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.25f);
            }
        }
        TerrainClassifier.RebuildAll(world);
        return world;
    }
}
