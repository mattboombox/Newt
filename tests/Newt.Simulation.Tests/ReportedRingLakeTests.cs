using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ReportedRingLakeTests
{
    [Fact]
    public void ReportedPangaeaSeedKeepsLakesWithinTheirBounds()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Ring, 17860253125702599316UL, MapType: WorldMapType.Pangaea));
        for (var step = 0; step < 10000 && world.ActiveSpringCount > 0; step++)
            Hydrology.AdvanceSprings(world);
        Assert.Equal(0, world.ActiveSpringCount);
        var lakes = new List<GridPosition>();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var tile = new GridPosition(x, y);
            if (world.GetSurfaceWater(tile) != SurfaceWaterKind.FreshwaterLake) continue;
            lakes.Add(tile);
            Assert.InRange(world.GetLakeTileCount(tile), 1, Hydrology.MaximumLakeTileCount);
            Assert.True(world.GetWaterSurfaceElevation(tile) >= world.GetElevation(tile));
        }
        var nearby = lakes.Where(p => p.X >= 676 && p.X <= 726 && p.Y <= 4).ToArray();
        Assert.NotEmpty(nearby);
        Assert.True(nearby.Min(world.GetElevation) > world.SeaLevel);
    }
}

