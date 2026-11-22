using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LaserTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 13)]
    [InlineData(3, 29)]
    [InlineData(4, 49)]
    public void HigherPowerMeltsALargerPatchWithoutCreatingAShockwave(int power, int expected)
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains);
        var center = new GridPosition(12, 8);
        var before = world.GetElevation(center);
        Assert.Equal(expected, Volcanism.MeltWithLaser(world, center, power));
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (world.GetSurfaceCover(new(x, y)) is SurfaceCover.Lava) count++;
        Assert.Equal(expected, count);
        Assert.True(world.GetElevation(center) > before);
        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.True(world.VolcanicTerrainRefreshPending);
    }

    [Fact]
    public void LaserMeltsAnOccupiedTargetAndLeavesItsNeighborsAloneAtRedPower()
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains);
        var center = new GridPosition(12, 8);
        var neighbor = new GridPosition(13, 8);
        world.SetBiome(center, Biome.Grassland);
        world.SetBiome(neighbor, Biome.Grassland);
        world.AddCritter(CritterSpecies.Wolf, center);
        world.AddCritter(CritterSpecies.Wolf, neighbor);
        Assert.Equal(1, Volcanism.MeltWithLaser(world, center, 0));
        Assert.False(world.IsOccupied(center));
        Assert.Equal(SurfaceCover.Lava, world.GetSurfaceCover(center));
        Assert.True(world.IsOccupied(neighbor));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(neighbor));
    }

    [Fact]
    public void LaserPatchCrossesPlanetFaceSeams()
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains) { Body = WorldBody.Planetoid };
        var center = new GridPosition(4, 0);
        var across = PlanetoidLayout.Offset(center, 0, -1, 8);
        Assert.NotEqual(0, PlanetoidLayout.FaceAt(across.X, across.Y, 8));
        Assert.Equal(5, Volcanism.MeltWithLaser(world, center, 1));
        Assert.Equal(SurfaceCover.Lava, world.GetSurfaceCover(across));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void InvalidLaserPowerDoesNotChangeTheWorld(int power)
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains);
        var tile = new GridPosition(12, 8);
        Assert.Throws<ArgumentOutOfRangeException>(() => Volcanism.MeltWithLaser(world, tile, power));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(tile));
    }

    [Fact]
    public void LaserBeamIsInstantAndFadesWithoutADelayedRifleImpact()
    {
        var timing = new RifleShotTiming(0, instant: true, fadeSeconds: 0.12f);
        Assert.Equal(1, timing.Progress);
        Assert.True(timing.Advance(0));
        Assert.False(timing.ShockwaveVisible);
        Assert.False(timing.Advance(0.06f));
        Assert.InRange(timing.Opacity, 0.49f, 0.51f);
        Assert.False(timing.Advance(0.07f));
        Assert.True(timing.Finished);
    }
}
