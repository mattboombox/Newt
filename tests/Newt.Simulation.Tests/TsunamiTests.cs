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
    public void NaturalEventsCanBeDisabled()
    {
        var world = CreateCoastalWorld();

        NaturalEvents.SetEnabled(world, false);

        Assert.False(world.NaturalEventsEnabled);
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
