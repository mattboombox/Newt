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
    public void FilledLakeSpreadsMoistureBeforeOverflowRiverFinishes()
    {
        var world = new SimulationWorld(13, 9, Terrain.Plains, seed: 17)
        {
            HasOceans = false,
        };
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.4f);
            }
        }
        for (var y = 2; y <= 6; y++)
        {
            for (var x = 3; x <= 7; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }
        world.SetElevation(new GridPosition(8, 4), 0.25f);
        world.SetElevation(new GridPosition(9, 4), 0.20f);
        world.SetElevation(new GridPosition(10, 4), 0.15f);
        world.SetElevation(new GridPosition(11, 4), 0.10f);
        world.SetElevation(new GridPosition(12, 4), 0.05f);
        TerrainClassifier.RebuildAll(world);
        var nearby = new GridPosition(6, 1);
        var before = world.GetMoisture(nearby);

        Hydrology.StartSpring(world, new GridPosition(5, 4));
        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(new GridPosition(5, 4)));
        Assert.Equal(1, world.ActiveSpringCount);
        Assert.True(world.GetMoisture(nearby) > before + 0.2f);
    }

    [Fact]
    public void RiverFillsSuccessiveDepressionsAndStartsNewOutflowsUntilOcean()
    {
        var world = new SimulationWorld(15, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.9f);
            }
        }

        float[] channelElevations =
            [0.9f, 0.9f, 0.8f, 0.3f, 0.1f, 0.6f, 0.2f, 0.05f,
                0.4f, 0.25f, 0.15f, 0.05f, 0f, -0.1f];
        for (var x = 0; x < channelElevations.Length; x++)
        {
            world.SetElevation(new GridPosition(x, 3), channelElevations[x]);
        }
        world.SetTerrain(new GridPosition(14, 3), Terrain.Ocean);

        var result = Hydrology.TraceSpring(world, new GridPosition(2, 3));

        Assert.Equal(SpringTermination.ReachedOcean, result.Termination);
        Assert.Equal(
            SurfaceWaterKind.FreshwaterLake,
            world.GetSurfaceWater(new GridPosition(4, 3)));
        Assert.Equal(
            SurfaceWaterKind.FreshwaterLake,
            world.GetSurfaceWater(new GridPosition(7, 3)));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(new GridPosition(5, 3)));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(new GridPosition(8, 3)));
    }

    [Fact]
    public void OverflowRouteHandsDescendingTerrainBackToLakeDetection()
    {
        var world = new SimulationWorld(7, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }

        var source = new GridPosition(3, 2);
        var secondBasin = new GridPosition(3, 3);
        var bypass = new GridPosition(3, 4);
        world.SetElevation(source, 0.6f);
        world.SetElevation(secondBasin, 0.2f);
        world.SetElevation(bypass, 0.7f);
        TerrainClassifier.RebuildAll(world);
        Hydrology.StartSpring(world, source);
        world.ActiveSprings[0].PlannedRoute.Enqueue(secondBasin);
        world.ActiveSprings[0].PlannedRoute.Enqueue(bypass);

        world.AdvanceOneTick();
        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(secondBasin));
        Assert.NotEqual(SurfaceWaterKind.River, world.GetSurfaceWater(bypass));
    }

    [Fact]
    public void OverflowDoesNotFlowBackIntoItsUpstreamLake()
    {
        var world = new SimulationWorld(7, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }

        var outlet = new GridPosition(3, 3);
        var upstreamLake = new GridPosition(2, 3);
        var downstream = new GridPosition(4, 3);
        world.SetElevation(outlet, 0.6f);
        world.SetElevation(upstreamLake, 0.1f);
        world.SetElevation(downstream, 0.4f);
        TerrainClassifier.RebuildAll(world);
        world.SetSurfaceWater(outlet, SurfaceWaterKind.River);
        world.SetSurfaceWater(upstreamLake, SurfaceWaterKind.FreshwaterLake);
        var spring = new ActiveSpring(outlet);
        spring.UpstreamLake.Add(upstreamLake);
        world.ActiveSprings.Add(spring);

        world.AdvanceOneTick();

        Assert.Equal(1, world.ActiveSpringCount);
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(downstream));
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(upstreamLake));
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
    public void DeepInlandCraterOverflowsItsLocalRim()
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        world.SeaLevel = -1f;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }

        var sink = new GridPosition(4, 4);
        var rim = new GridPosition(5, 4);
        var downstream = new GridPosition(6, 4);
        world.SetElevation(sink, -0.9f);
        world.SetElevation(rim, 0.3f);
        world.SetElevation(downstream, 0.1f);
        TerrainClassifier.RebuildAll(world);

        world.SetSurfaceWater(sink, SurfaceWaterKind.River);
        world.ActiveSprings.Add(new ActiveSpring(sink));
        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(sink));
        Assert.Equal(1.2f, world.GetWaterDepth(sink), precision: 5);
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(rim));
        Assert.Equal(1, world.ActiveSpringCount);

        world.AdvanceOneTick();

        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(downstream));
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
    public void LargeCraterSizedBasinCanExceedLegacyLakeBudget()
    {
        var world = new SimulationWorld(70, 70, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }

        for (var y = 5; y < 65; y++)
        {
            for (var x = 5; x < 65; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }

        var outlet = new GridPosition(65, 35);
        world.SetElevation(outlet, 0.4f);
        world.SetElevation(new GridPosition(66, 35), 0.2f);
        TerrainClassifier.RebuildAll(world);

        var result = Hydrology.FillBasin(world, new GridPosition(35, 35));

        Assert.True(result.Created);
        Assert.Equal(3_600, result.LakeTileCount);
        Assert.True(result.LakeTileCount > 2_048);
        Assert.Equal(0.4f, result.SurfaceElevation, precision: 5);
        Assert.Equal(outlet, result.Outlet);
    }

    [Fact]
    public void UnevenCraterIgnoresInteriorFalseSpillAndFillsToOceanBoundRim()
    {
        var world = new SimulationWorld(25, 15, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }

        for (var y = 4; y <= 10; y++)
        {
            for (var x = 2; x <= 9; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }

        var sink = new GridPosition(5, 7);
        world.SetElevation(sink, 0f);
        world.SetElevation(new GridPosition(6, 7), 0.2f);
        world.SetElevation(new GridPosition(7, 7), 0.05f);
        world.SetElevation(new GridPosition(10, 7), 0.6f);
        for (var x = 11; x < 24; x++)
        {
            world.SetElevation(new GridPosition(x, 7), 0.5f - (x - 11) * 0.03f);
        }
        world.SetTerrain(new GridPosition(24, 7), Terrain.Ocean);

        var result = Hydrology.FillBasin(world, sink);

        Assert.True(result.Created);
        Assert.Equal(56, result.LakeTileCount);
        Assert.Equal(0.6f, result.SurfaceElevation, precision: 5);
        Assert.Equal(new GridPosition(10, 7), result.Outlet);
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
