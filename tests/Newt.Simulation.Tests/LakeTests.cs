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
        Assert.Equal(result.LakeTileCount, world.GetLakeTileCount(sink));
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
    public void LakeInspectionDoesNotFollowRiverConnectionsIntoAnotherLake()
    {
        var world = new SimulationWorld(9, 3, Terrain.Plains);
        var first = new GridPosition(1, 1);
        var second = new GridPosition(5, 1);
        world.SetSurfaceWater(first, SurfaceWaterKind.FreshwaterLake);
        world.SetSurfaceWater(new GridPosition(2, 1), SurfaceWaterKind.FreshwaterLake);
        world.SetSurfaceWater(new GridPosition(3, 1), SurfaceWaterKind.River);
        world.SetSurfaceWater(new GridPosition(4, 1), SurfaceWaterKind.River);
        world.SetSurfaceWater(second, SurfaceWaterKind.FreshwaterLake);

        Assert.Equal(2, world.GetLakeTileCount(first));
        Assert.Equal(2, world.GetLakeTileCount(new GridPosition(2, 1)));
        Assert.Equal(1, world.GetLakeTileCount(second));
        Assert.Equal(0, world.GetLakeTileCount(new GridPosition(3, 1)));
        Assert.Equal(0, world.GetLakeTileCount(new GridPosition(0, 0)));
    }

    [Fact]
    public void LakeInspectionIncludesFrozenTilesAndHorizontalWrapButNotDiagonalsOrVerticalWrap()
    {
        var world = new SimulationWorld(5, 4, Terrain.Plains);
        var start = new GridPosition(0, 0);
        foreach (var tile in new[] { start, new GridPosition(4, 0), new GridPosition(4, 1),
            new GridPosition(1, 1), new GridPosition(0, 3) })
        {
            world.SetSurfaceWater(tile, SurfaceWaterKind.FreshwaterLake);
            world.SetBiome(tile, Biome.Arctic);
        }

        Assert.Equal(3, world.GetLakeTileCount(start));
        Assert.Equal(3, world.GetLakeTileCount(new GridPosition(4, 1)));
        Assert.Equal(1, world.GetLakeTileCount(new GridPosition(1, 1)));
        Assert.Equal(1, world.GetLakeTileCount(new GridPosition(0, 3)));
    }

    [Fact]
    public void LakeInspectionRefreshesWhenLakesMergeSplitOrDisappear()
    {
        var world = new SimulationWorld(5, 1, Terrain.Plains);
        var first = new GridPosition(1, 0);
        var bridge = new GridPosition(2, 0);
        var last = new GridPosition(3, 0);
        world.SetSurfaceWater(first, SurfaceWaterKind.FreshwaterLake);
        world.SetSurfaceWater(last, SurfaceWaterKind.FreshwaterLake);
        Assert.Equal(1, world.GetLakeTileCount(first));
        Assert.Equal(1, world.GetLakeTileCount(last));

        world.SetSurfaceWater(bridge, SurfaceWaterKind.FreshwaterLake);
        Assert.Equal(3, world.GetLakeTileCount(first));
        Assert.Equal(3, world.GetLakeTileCount(last));

        world.SetSurfaceWater(bridge, SurfaceWaterKind.River);
        Assert.Equal(1, world.GetLakeTileCount(last));
        Assert.Equal(1, world.GetLakeTileCount(first));
        Assert.Equal(0, world.GetLakeTileCount(bridge));

        world.SetSurfaceWater(last, SurfaceWaterKind.None);
        Assert.Equal(0, world.GetLakeTileCount(last));
        world.ClearFreshwater();
        world.SetSurfaceWater(first, SurfaceWaterKind.FreshwaterLake);
        world.SetSurfaceWater(bridge, SurfaceWaterKind.FreshwaterLake);
        Assert.Equal(2, world.GetLakeTileCount(first));
    }

    [Fact]
    public void LakeInspectionCountsNarrowWrappedMapsWithoutDuplicates()
    {
        var world = new SimulationWorld(1, 3, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            world.SetSurfaceWater(new GridPosition(0, y), SurfaceWaterKind.FreshwaterLake);
        }
        Assert.Equal(3, world.GetLakeTileCount(new GridPosition(0, 0)));
        Assert.Equal(3, world.GetLakeTileCount(new GridPosition(0, 2)));
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
    public void OversizedCraterBasinFallsBackToSmallTerminalLake()
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
        Assert.InRange(result.LakeTileCount, 1, 128);
        Assert.Equal(result.LakeTileCount, world.GetLakeTileCount(new GridPosition(35, 35)));
        Assert.True(result.SurfaceElevation < 0.4f);
        Assert.Null(result.Outlet);
    }

    [Theory]
    [InlineData(1_499)]
    [InlineData(1_500)]
    [InlineData(1_501)]
    [InlineData(1_554)]
    [InlineData(1_781)]
    public void FreshwaterLakeLimitIsInclusiveAndRejectsOversizedBasins(int basinSize)
    {
        var world = new SimulationWorld(64, 40, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }
        for (var tile = 0; tile < basinSize; tile++)
        {
            world.SetElevation(new GridPosition(5 + tile % 50, 2 + tile / 50), 0.1f);
        }
        var sink = new GridPosition(5, 2);
        var outlet = new GridPosition(4, 2);
        world.SetElevation(outlet, 0.4f);
        world.SetElevation(new GridPosition(3, 2), 0.2f);
        TerrainClassifier.RebuildAll(world);

        // Callers cannot bypass the cap by requesting a larger area budget.
        var result = Hydrology.FillBasin(world, sink, lakeTileBudget: 65_536);

        Assert.True(result.Created);
        Assert.Equal(result.LakeTileCount, world.GetLakeTileCount(sink));
        if (basinSize <= Hydrology.MaximumLakeTileCount)
        {
            Assert.Equal(basinSize, result.LakeTileCount);
            Assert.Equal(outlet, result.Outlet);
            Assert.Equal(0.4f, result.SurfaceElevation, precision: 5);
        }
        else
        {
            Assert.InRange(result.LakeTileCount, 1, 128);
            Assert.Null(result.Outlet);
            Assert.True(result.SurfaceElevation < 0.4f);
        }
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
    public void RepeatedTerminalFillsCannotGrowAConnectedLakePastTheCap()
    {
        var world = new SimulationWorld(1_600, 1, Terrain.Plains);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetElevation(new GridPosition(x, 0), 0.1f);
        }
        for (var x = 1; x <= Hydrology.MaximumLakeTileCount; x++)
        {
            var tile = new GridPosition(x, 0);
            world.SetSurfaceWater(tile, SurfaceWaterKind.FreshwaterLake);
            world.SetWaterSurfaceElevation(tile, 0.11f);
        }
        var sink = new GridPosition(1_501, 0);
        var result = Hydrology.FillBasin(world, sink, searchBudget: 1, lakeTileBudget: 16);

        Assert.False(result.Created);
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(sink));
        Assert.Equal(Hydrology.MaximumLakeTileCount, world.GetLakeTileCount(new GridPosition(1, 0)));
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

    [Theory]
    [InlineData(-0.1f, -0.5f, true)]
    [InlineData(0.1f, 0f, true)]
    [InlineData(0.1f, 0.01f, false)]
    public void OversizedLakeAddsOceanSeedOnlyWhenItsLowestPointIsAtOrBelowSeaLevel(
        float floor, float minimum, bool converts)
    {
        var (world, sink, lowest) = CreateOceanConversionWorld(floor: floor, minimum: minimum);
        var primary = world.OceanSeed;
        var existing = Assert.Single(world.AdditionalOceanSeeds);

        var result = Hydrology.FillBasin(world, sink);

        Assert.Equal(primary, world.OceanSeed);
        Assert.Contains(existing, world.AdditionalOceanSeeds);
        Assert.Equal(converts ? 2 : 1, world.AdditionalOceanSeeds.Count);
        if (converts)
        {
            Assert.Equal(lowest, result.OceanSeed);
            Assert.False(result.Created);
            Assert.Contains(lowest, world.AdditionalOceanSeeds);
            AssertOcean(world, lowest);
            AssertOcean(world, primary);
            AssertOcean(world, existing);
            Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(lowest));
            Assert.Null(Hydrology.FillBasin(world, lowest).OceanSeed);
            Assert.False(Geology.TryAddOceanSeed(world, lowest));
            Assert.Equal(2, world.AdditionalOceanSeeds.Count);
            if (floor > world.SeaLevel)
            {
                Assert.Equal(Terrain.Plains, world.GetTerrain(sink));
            }
        }
        else
        {
            Assert.Null(result.OceanSeed);
            Assert.True(result.Created);
            Assert.InRange(result.LakeTileCount, 1, 128);
        }
    }

    [Fact]
    public void LakeExactlyAtCapRemainsFreshwaterEvenBelowSeaLevel()
    {
        var (world, sink, _) = CreateOceanConversionWorld(basinSize: 1_500);
        var result = Hydrology.FillBasin(world, sink);

        Assert.True(result.Created);
        Assert.Equal(1_500, result.LakeTileCount);
        Assert.Null(result.OceanSeed);
        Assert.Single(world.AdditionalOceanSeeds);
    }

    [Fact]
    public void OversizedTerminalBasinFindsLowestPointBeyondTheSmallFillBudget()
    {
        var (world, sink, lowest) = CreateOceanConversionWorld();
        var result = Hydrology.FillBasin(world, sink, searchBudget: 1);

        Assert.Equal(lowest, result.OceanSeed);
        Assert.Contains(lowest, world.AdditionalOceanSeeds);
        AssertOcean(world, lowest);
        Assert.Equal(0, world.GetLakeTileCount(sink));
    }

    [Fact]
    public void ExistingFrozenLakeConversionDoesNotFollowRiversAndRemovesOldAboveSeaLevelWater()
    {
        var (world, sink, lowest) = CreateOceanConversionWorld(floor: 0.1f);
        for (var tile = 0; tile < 1_800; tile++)
        {
            var position = new GridPosition(5 + tile % 50, 5 + tile / 50);
            world.SetSurfaceWater(position, SurfaceWaterKind.FreshwaterLake);
            world.SetWaterSurfaceElevation(position, 0.8f);
            world.SetBiome(position, Biome.Arctic);
        }
        var otherLake = new GridPosition(3, 40);
        var river = new GridPosition(4, 40);
        world.SetElevation(otherLake, -0.9f);
        world.SetSurfaceWater(otherLake, SurfaceWaterKind.FreshwaterLake);
        world.SetWaterSurfaceElevation(otherLake, 0.8f);
        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        Assert.Equal(1_800, world.GetLakeTileCount(sink));

        Assert.True(Hydrology.TryConvertOversizedLakeToOcean(world, sink));

        Assert.Contains(lowest, world.AdditionalOceanSeeds);
        Assert.DoesNotContain(otherLake, world.AdditionalOceanSeeds);
        AssertOcean(world, lowest);
        Assert.Equal(0, world.GetLakeTileCount(sink));
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(sink));
        Assert.Null(world.GetWaterSurfaceElevation(sink));
        Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(river));
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(otherLake));
        Assert.False(Hydrology.TryConvertOversizedLakeToOcean(world, otherLake));
        Assert.False(Hydrology.TryConvertOversizedLakeToOcean(world, river));
    }

    [Fact]
    public void MultipleActiveSpringsCanCreateSeparateOceansWithoutReplacingSeeds()
    {
        var (world, _, firstLowest) = CreateOceanConversionWorld();
        var secondLowest = CarveConversionBasin(world, offsetX: 75, basinSize: 1_800, floor: -0.1f, minimum: -0.6f);
        TerrainClassifier.RebuildAll(world);
        var sources = new[] { new GridPosition(4, 5), new GridPosition(4, 15), new GridPosition(74, 5) };
        foreach (var source in sources)
        {
            world.SetElevation(source, 0.9f);
            Assert.Equal(SpringTermination.Flowing, Hydrology.StartSpring(world, source).Termination);
        }
        for (var tick = 0; tick < 100 && world.ActiveSpringCount > 0; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.ActiveSpringCount);
        Assert.Equal(3, world.AdditionalOceanSeeds.Count);
        Assert.Contains(firstLowest, world.AdditionalOceanSeeds);
        Assert.Contains(secondLowest, world.AdditionalOceanSeeds);
        AssertOcean(world, firstLowest);
        AssertOcean(world, secondLowest);
        AssertOcean(world, world.OceanSeed);

        var seeds = world.AdditionalOceanSeeds.ToArray();
        Hydrology.RebuildFreshwater(world);
        Assert.Equal(seeds, world.AdditionalOceanSeeds);
        Assert.Equal(0, world.ActiveSpringCount);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(0.1f)]
    public void OceanConversionDuringFreshwaterRebuildDoesNotReenterOrLoseOtherSources(float floor)
    {
        var (world, _, lowest) = CreateOceanConversionWorld(floor: floor);
        var source = new GridPosition(4, 5);
        world.SetElevation(source, 0.9f);
        Hydrology.StartSpring(world, source);

        Hydrology.RebuildFreshwater(world);

        Assert.Contains(lowest, world.AdditionalOceanSeeds);
        AssertOcean(world, lowest);
        Assert.Equal(SpringTermination.ReachedOcean, world.LastCompletedSpring?.Termination);
        Assert.Single(world.SpringSources);
        Assert.Equal(0, world.ActiveSpringCount);
    }

    private static (SimulationWorld World, GridPosition Sink, GridPosition Lowest) CreateOceanConversionWorld(
        int basinSize = 1_800, float floor = -0.1f, float minimum = -0.5f)
    {
        var world = new SimulationWorld(140, 45, Terrain.Plains)
        {
            SeasonsEnabled = false,
            OceanSeed = new GridPosition(0, 0),
        };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }
        world.SetElevation(world.OceanSeed, -0.4f);
        var existing = new GridPosition(139, 44);
        world.SetElevation(existing, -0.4f);
        world.SetAdditionalOceanSeeds([existing]);
        var lowest = CarveConversionBasin(world, 5, basinSize, floor, minimum);
        TerrainClassifier.RebuildAll(world);
        return (world, new GridPosition(5, 5), lowest);
    }

    private static GridPosition CarveConversionBasin(
        SimulationWorld world, int offsetX, int basinSize, float floor, float minimum)
    {
        for (var tile = 0; tile < basinSize; tile++)
        {
            world.SetElevation(new GridPosition(offsetX + tile % 50, 5 + tile / 50), floor);
        }
        // This far corner is beyond the ordinary fill's first 1,500 visited tiles.
        var lowest = new GridPosition(offsetX + (basinSize - 1) % 50, 5 + (basinSize - 1) / 50);
        world.SetElevation(lowest, minimum);
        return lowest;
    }

    private static void AssertOcean(SimulationWorld world, GridPosition position) =>
        Assert.True(world.GetTerrain(position) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows or Terrain.Ice);

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
