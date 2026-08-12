using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LakeTests
{
    [Fact]
    public void BasinFillsToItsLowestSpillElevation()
    {
        var world = CreateBasinWorld();
        var sink = new GridPosition(4, 4);

        var result = Hydrology.FillBasin(world, sink);

        Assert.True(result.Created);
        Assert.Equal(25, result.LakeTileCount);
        Assert.Equal(0.25f, result.SurfaceElevation, precision: 5);
        Assert.Equal(new GridPosition(7, 4), result.Outlet);
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(sink));
        Assert.Equal(0.15f, world.GetWaterDepth(sink), precision: 5);
    }

    [Fact]
    public void LakeShapeIncludesOnlyConnectedTilesBelowSpillElevation()
    {
        var world = CreateBasinWorld();

        Hydrology.FillBasin(world, new GridPosition(4, 4));

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(new GridPosition(2, 2)));
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(new GridPosition(1, 1)));
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(new GridPosition(7, 4)));
    }

    [Fact]
    public void ActiveSpringContinuesFromLakeOverflow()
    {
        var world = CreateBasinWorld();
        Hydrology.StartSpring(world, new GridPosition(4, 4));

        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(new GridPosition(4, 4)));
        Assert.Equal(1, world.ActiveSpringCount);
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(new GridPosition(7, 4)));

        while (world.ActiveSpringCount > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(SpringTermination.ReachedOcean, world.LastCompletedSpring?.Termination);
    }

    [Fact]
    public void DeepTerminalCraterFillsToItsRimWithoutADepthCap()
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        world.SeaLevel = -1f;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.5f);
            }
        }
        for (var y = 3; y <= 5; y++)
        {
            for (var x = 3; x <= 5; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.9f);
            }
        }
        TerrainClassifier.RebuildAll(world);

        var result = Hydrology.FillBasin(world, new GridPosition(4, 4));

        Assert.Equal(9, result.LakeTileCount);
        Assert.Equal(0.5f, result.SurfaceElevation, precision: 5);
        Assert.Equal(1.4f, world.GetWaterDepth(new GridPosition(4, 4)), precision: 5);
    }

    [Fact]
    public void ShallowTerminalLakeIsLimitedBySurfaceArea()
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }
        TerrainClassifier.RebuildAll(world);

        var result = Hydrology.FillBasin(
            world,
            new GridPosition(10, 10),
            searchBudget: 1,
            lakeTileBudget: 16);

        Assert.Equal(16, result.LakeTileCount);
        Assert.InRange(result.SurfaceElevation, 0.1f, 0.101f);
    }

    [Fact]
    public void RemovingLakeAlsoRemovesItsRiverSource()
    {
        var world = CreateBasinWorld();
        var sink = new GridPosition(4, 4);
        Hydrology.TraceSpring(world, sink);

        Assert.True(Hydrology.RemoveFreshwaterAt(world, sink));

        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(sink));
        Assert.Empty(world.SpringSources);
    }

    private static SimulationWorld CreateBasinWorld()
    {
        var world = new SimulationWorld(9, 9, Terrain.Ocean);
        world.OceanSeed = new GridPosition(0, 0);
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
}
