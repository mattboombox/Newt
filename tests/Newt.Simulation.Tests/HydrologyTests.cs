using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class HydrologyTests
{
    [Fact]
    public void SpringFlowsDownhillAndStopsAtOcean()
    {
        var world = CreateDescendingValley();

        var result = Hydrology.TraceSpring(world, new GridPosition(10, 2));

        Assert.Equal(SpringTermination.ReachedOcean, result.Termination);
        Assert.True(result.RiverTileCount > 1);
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(new GridPosition(10, 2)));
    }

    [Fact]
    public void RiverIsAWaterLayerAndDoesNotReplaceUnderlyingTerrain()
    {
        var world = CreateDescendingValley();
        var source = new GridPosition(10, 2);
        var originalTerrain = world.GetTerrain(source);

        Hydrology.TraceSpring(world, source);

        Assert.Equal(originalTerrain, world.GetTerrain(source));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(source));
    }

    [Fact]
    public void SpringFormsTerminalLakeInClosedDepression()
    {
        var world = new SimulationWorld(7, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.5f);
            }
        }

        var basin = new GridPosition(3, 3);
        world.SetElevation(basin, 0.1f);
        TerrainClassifier.RebuildAll(world);

        var result = Hydrology.TraceSpring(world, basin);

        Assert.Equal(SpringTermination.FormedLake, result.Termination);
        Assert.Equal(1, result.RiverTileCount);
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(basin));
    }

    [Fact]
    public void RiverFormsBoundedMultiTileLakeInBroadBelowSeaLevelBasin()
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.5f);
            }
        }

        for (var y = 2; y <= 17; y++)
        {
            for (var x = 2; x <= 17; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.3f);
            }
        }

        var source = new GridPosition(10, 1);
        world.OceanSeed = new GridPosition(0, 0);
        world.SetElevation(source, 0.6f);
        TerrainClassifier.RebuildAll(world);

        var result = Hydrology.TraceSpring(world, source);
        var lakeTiles = AllPositions(world).Count(position =>
            world.GetSurfaceWater(position) is SurfaceWaterKind.FreshwaterLake);

        Assert.Equal(SpringTermination.FormedLake, result.Termination);
        Assert.InRange(lakeTiles, 2, 128);
    }

    [Fact]
    public void ActiveSpringExtendsByOneTilePerSimulationTick()
    {
        var world = CreateDescendingValley();
        var source = new GridPosition(10, 2);

        var started = Hydrology.StartSpring(world, source);

        Assert.Equal(SpringTermination.Flowing, started.Termination);
        Assert.Equal(1, CountRiverTiles(world));
        world.AdvanceOneTick();
        Assert.Equal(2, CountRiverTiles(world));
        Assert.Equal(1, world.ActiveSpringCount);
    }

    [Fact]
    public void FlowingRiverStoresAContinuousConnectionChain()
    {
        var world = CreateDescendingValley();
        var source = new GridPosition(10, 2);
        Hydrology.StartSpring(world, source);

        world.AdvanceOneTick();

        Assert.NotEqual(RiverConnection.None, world.GetRiverConnections(source));
        Assert.Contains(
            AllPositions(world),
            position => position != source &&
                world.GetSurfaceWater(position) is SurfaceWaterKind.River &&
                world.GetRiverConnections(position) is not RiverConnection.None);
    }

    [Fact]
    public void RemovingConnectedFreshwaterRemovesEverySourceFeedingIt()
    {
        var world = CreateJoinedRivers();
        Hydrology.TraceSpring(world, new GridPosition(4, 2));
        Hydrology.TraceSpring(world, new GridPosition(3, 2));

        Assert.Equal(2, world.SpringSources.Count);

        var removed = Hydrology.RemoveFreshwaterAt(world, new GridPosition(4, 5));

        Assert.True(removed);
        Assert.Empty(world.SpringSources);
        Assert.DoesNotContain(
            AllPositions(world),
            position => world.GetSurfaceWater(position) is not SurfaceWaterKind.None);
    }

    [Fact]
    public void RemovingFreshwaterFromDryLandDoesNothing()
    {
        var world = CreateDescendingValley();

        Assert.False(Hydrology.RemoveFreshwaterAt(world, new GridPosition(0, 0)));
    }

    private static SimulationWorld CreateDescendingValley()
    {
        var world = new SimulationWorld(21, 20, Terrain.Ocean);
        world.OceanSeed = new GridPosition(10, 19);
        for (var y = 1; y < 18; y++)
        {
            var elevation = 0.85f - y * 0.045f;
            for (var x = 7; x <= 13; x++)
            {
                world.SetElevation(new GridPosition(x, y), elevation);
            }
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static SimulationWorld CreateJoinedRivers()
    {
        var world = new SimulationWorld(9, 11, Terrain.Ocean);
        for (var y = 1; y <= 9; y++)
        {
            for (var x = 2; x <= 6; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.9f);
            }

            var channelElevation = 0.9f - y * 0.07f;
            world.SetElevation(new GridPosition(3, y), channelElevation);
            world.SetElevation(new GridPosition(4, y), channelElevation);
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static int CountRiverTiles(SimulationWorld world) =>
        AllPositions(world).Count(position => world.GetSurfaceWater(position) is SurfaceWaterKind.River);

    private static IEnumerable<GridPosition> AllPositions(SimulationWorld world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                yield return new GridPosition(x, y);
            }
        }
    }
}
