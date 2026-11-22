using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RifleTests
{
    [Theory]
    [InlineData(WorldBody.Earth, 12, 8)]
    [InlineData(WorldBody.Earth, 0, 0)]
    [InlineData(WorldBody.Planetoid, 4, 0)]
    [InlineData(WorldBody.Planetoid, 7, 7)]
    [InlineData(WorldBody.Planetoid, 15, 10)]
    public void RifleStrikeAffectsOnlyOneTileWithoutAShockwave(WorldBody body, int x, int y)
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains) { Body = body };
        for (var row = 0; row < world.Height; row++)
        for (var column = 0; column < world.Width; column++)
        {
            var tile = new GridPosition(column, row);
            world.SetBiome(tile, Biome.Grassland);
            world.SetElevation(tile, 0.3f);
            world.AddCritter(CritterSpecies.Wolf, tile);
        }
        var center = new GridPosition(x, y);
        var result = Impacts.CreateRifleImpact(world, center);
        Assert.Equal(0, result.CraterRadius);
        Assert.Equal(0, result.ShockRadius);
        Assert.Equal(0, result.FragmentCount);
        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.Equal(0, world.ActiveLavaFlowCount);
        for (var tick = 0; tick < 20; tick++) Impacts.Advance(world);
        for (var row = 0; row < world.Height; row++)
        for (var column = 0; column < world.Width; column++)
        {
            var tile = new GridPosition(column, row);
            if (tile == center)
            {
                Assert.InRange(world.GetElevation(tile), 0.259f, 0.261f);
                Assert.False(world.IsOccupied(tile));
                Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(tile));
            }
            else
            {
                Assert.Equal(0.3f, world.GetElevation(tile));
                Assert.True(world.IsOccupied(tile));
                Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(tile));
            }
        }
    }

    [Fact]
    public void RifleTracerNeverDisplaysAShockwave()
    {
        var flight = new RifleShotTiming(60);
        Assert.True(flight.Advance(flight.TravelSeconds));
        Assert.False(flight.ShockwaveVisible);
        Assert.True(flight.Opacity > 0);
    }

    [Fact]
    public void MissileShockwaveOutlastsFastSimulationAndStillExpiresInRealTime()
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains);
        var flight = new RifleShotTiming(60, shockwave: true);
        Assert.True(flight.Advance(flight.TravelSeconds + 0.001f));
        Impacts.CreateMissileImpact(world, new(12, 8));
        // At fast simulation rates the physical wave can finish within one frame.
        for (var tick = 0; tick < 20; tick++) Impacts.Advance(world);
        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.True(flight.ShockwaveVisible);
        flight.Advance(0.3f);
        Assert.True(flight.ShockwaveVisible);
        Assert.InRange(flight.ShockwaveProgress, 0.49f, 0.51f);
        flight.Advance(0.31f);
        Assert.False(flight.ShockwaveVisible);
        Assert.False(flight.Finished); // The tracer can continue fading afterward.
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(60f)]
    [InlineData(6000f)]
    public void TracerArrivesBeforeItsSingleImpactThenFades(float distance)
    {
        var flight = new RifleShotTiming(distance);
        Assert.InRange(flight.TravelSeconds, 0.06f, 0.3f);
        Assert.False(flight.Advance(flight.TravelSeconds / 2));
        Assert.InRange(flight.Progress, 0.49f, 0.51f);
        Assert.False(flight.Impacted);
        Assert.True(flight.Advance(flight.TravelSeconds / 2 + 0.001f));
        Assert.Equal(1, flight.Progress);
        Assert.False(flight.Advance(0.4f));
        Assert.InRange(flight.Opacity, 0.7f, 0.8f);
        Assert.False(flight.Finished);
        Assert.False(flight.Advance(1.3f));
        Assert.True(flight.Finished);
        Assert.Equal(0, flight.Opacity);
    }

    [Theory]
    [InlineData(WorldBody.Earth, 12, 8)]
    [InlineData(WorldBody.Earth, 0, 0)]
    [InlineData(WorldBody.Planetoid, 4, 0)]
    [InlineData(WorldBody.Planetoid, 7, 7)]
    [InlineData(WorldBody.Planetoid, 15, 10)]
    public void MissileImpactStillAffectsTargetAndImmediateSurfaceNeighbors(WorldBody body, int x, int y)
    {
        var world = new SimulationWorld(24, 16, Terrain.Plains) { Body = body };
        for (var row = 0; row < world.Height; row++)
        for (var column = 0; column < world.Width; column++)
        {
            var tile = new GridPosition(column, row);
            world.SetBiome(tile, Biome.Grassland);
            world.SetElevation(tile, 0.3f);
            world.AddCritter(CritterSpecies.Wolf, tile);
        }
        var center = new GridPosition(x, y);
        var footprint = new HashSet<GridPosition>();
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var tile = world.OffsetTile(center, new(dx, dy));
            if (world.Contains(tile)) footprint.Add(tile);
        }
        var result = Impacts.CreateMissileImpact(world, center);
        Assert.Equal(1, result.CraterRadius);
        Assert.Equal(1, result.ShockRadius);
        Assert.Equal(1, world.ActiveImpactWaveCount);
        var initialWave = world.GetImpactWave(0);
        Assert.Equal(WaveKind.Impact, initialWave.Kind);
        for (var step = 0; step < 20; step++) Impacts.Advance(world);
        Assert.Equal(0, world.ActiveImpactWaveCount);
        Assert.Equal(0, world.ActiveLavaFlowCount);
        for (var row = 0; row < world.Height; row++)
        for (var column = 0; column < world.Width; column++)
        {
            var tile = new GridPosition(column, row);
            if (footprint.Contains(tile))
            {
                Assert.NotEqual(0.3f, world.GetElevation(tile));
                if (tile == center)
                {
                    Assert.True(world.GetElevation(tile) < 0.3f);
                    Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(tile));
                    Assert.False(world.IsOccupied(tile));
                }
            }
            else
            {
                Assert.Equal(0.3f, world.GetElevation(tile));
                Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(tile));
                Assert.True(world.IsOccupied(tile));
            }
        }
    }
}
