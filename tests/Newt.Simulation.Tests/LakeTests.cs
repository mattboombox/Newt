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
    public void ActiveSpringStopsWhenItFormsALake()
    {
        var world = CreateBasinWorld();
        Hydrology.StartSpring(world, new GridPosition(4, 4));

        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(new GridPosition(4, 4)));
        Assert.Equal(0, world.ActiveSpringCount);
        Assert.Equal(SpringTermination.FormedLake, world.LastCompletedSpring?.Termination);
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(new GridPosition(7, 4)));
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
}
