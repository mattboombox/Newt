using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class GeologyTests
{
    [Fact]
    public void UpliftRaisesCenterMoreThanEdge()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        var center = new GridPosition(10, 10);
        var edge = new GridPosition(14, 10);

        Geology.ApplyRadialUplift(world, center, radius: 5, strength: 0.5f);

        Assert.Equal(0.5f, world.GetElevation(center), precision: 5);
        Assert.InRange(world.GetElevation(edge), 0.01f, 0.5f);
        Assert.True(world.GetElevation(center) > world.GetElevation(edge));
    }

    [Fact]
    public void UpliftCanRaiseOceanIntoLand()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        var center = new GridPosition(10, 10);
        Geology.ApplyRadialUplift(world, center, radius: 4, strength: 0.4f);

        Assert.Equal(Terrain.Hills, world.GetTerrain(center));
        Assert.Contains(
            Terrain.Shallows,
            NeighborTerrains(world, center));
    }

    [Fact]
    public void UpliftWrapsAcrossHorizontalWorldEdge()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        Geology.ApplyRadialUplift(world, new GridPosition(0, 10), radius: 3, strength: 0.3f);

        Assert.True(world.GetElevation(new GridPosition(19, 10)) > 0);
    }

    [Fact]
    public void RadialLoweringLowersTheCenterMoreThanTheEdge()
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains);
        var center = new GridPosition(10, 10);
        var edge = new GridPosition(13, 10);

        Geology.ApplyRadialLowering(world, center, radius: 5, strength: 0.5f);

        Assert.Equal(-0.5f, world.GetElevation(center), precision: 5);
        Assert.True(world.GetElevation(center) < world.GetElevation(edge));
        Assert.True(world.GetElevation(edge) < 0);
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(center));
    }

    [Fact]
    public void LoweringRiverTileInstantlyRetracesItsSource()
    {
        var world = CreateForkedValley();
        var riverTile = new GridPosition(7, 6);
        Hydrology.TraceSpring(world, new GridPosition(7, 2));
        world.LastCompletedSpring = null;

        Geology.ApplyRadialLowering(world, riverTile, radius: 1, strength: 0.1f);

        Assert.Equal(0, world.ActiveSpringCount);
        Assert.NotNull(world.LastCompletedSpring);
        Assert.NotEqual(SurfaceWaterKind.None, world.GetSurfaceWater(riverTile));
    }

    [Fact]
    public void UpliftOnRiverInstantlyRetracesItFromItsSource()
    {
        var world = CreateForkedValley();
        var source = new GridPosition(7, 2);
        var raisedRiverTile = new GridPosition(7, 6);
        var diversion = new GridPosition(6, 6);
        Hydrology.TraceSpring(world, source);

        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(raisedRiverTile));

        Geology.ApplyRadialUplift(world, raisedRiverTile, radius: 1, strength: 0.5f);

        Assert.Equal(0, world.ActiveSpringCount);
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(raisedRiverTile));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(diversion));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(source));
        Assert.Equal(RiverConnection.None, world.GetRiverConnections(raisedRiverTile));
    }

    [Fact]
    public void UpliftOnLakeInstantlyRebuildsItsShape()
    {
        var world = CreateBasinWorld();
        var source = new GridPosition(4, 4);
        var raisedLakeTile = new GridPosition(3, 4);
        Hydrology.TraceSpring(world, source);

        Assert.Equal(25, CountWaterTiles(world, SurfaceWaterKind.FreshwaterLake));

        Geology.ApplyRadialUplift(world, raisedLakeTile, radius: 1, strength: 0.3f);

        Assert.Equal(24, CountWaterTiles(world, SurfaceWaterKind.FreshwaterLake));
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(raisedLakeTile));
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(source));
    }

    private static IEnumerable<Terrain> NeighborTerrains(SimulationWorld world, GridPosition center)
    {
        for (var y = center.Y - 5; y <= center.Y + 5; y++)
        {
            for (var x = center.X - 5; x <= center.X + 5; x++)
            {
                if (x < 0 || x >= world.Width || y < 0 || y >= world.Height)
                {
                    continue;
                }

                yield return world.GetTerrain(new GridPosition(x, y));
            }
        }
    }

    private static SimulationWorld CreateForkedValley()
    {
        var world = new SimulationWorld(15, 14, Terrain.Ocean);
        for (var y = 1; y <= 11; y++)
        {
            for (var x = 5; x <= 9; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.9f);
            }

            var valleyElevation = 0.9f - y * 0.06f;
            world.SetElevation(new GridPosition(6, y), valleyElevation);
            world.SetElevation(new GridPosition(7, y), valleyElevation);
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static SimulationWorld CreateBasinWorld()
    {
        var world = new SimulationWorld(9, 9, Terrain.Ocean);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.2f);
            }
        }

        for (var y = 1; y <= 7; y++)
        {
            for (var x = 1; x <= 7; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.4f);
            }
        }

        for (var y = 2; y <= 6; y++)
        {
            for (var x = 2; x <= 6; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }

        world.SetElevation(new GridPosition(7, 4), 0.25f);
        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static int CountWaterTiles(SimulationWorld world, SurfaceWaterKind water)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetSurfaceWater(new GridPosition(x, y)) == water)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
