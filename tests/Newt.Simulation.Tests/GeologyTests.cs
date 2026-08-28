using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class GeologyTests
{
    [Theory]
    [InlineData(0.9f)]
    [InlineData(-0.9f)]
    public void SmoothingSoftensPeaksAndDipsFromOriginalNeighborHeights(float height)
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        var center = new GridPosition(4, 4);
        world.SetElevation(center, height);

        Geology.ApplyRadialSmoothing(world, center, 2, 1f);

        Assert.Equal(height / 9, world.GetElevation(center), precision: 5);
        Assert.Equal(height / 9 * 0.5625f, world.GetElevation(new GridPosition(5, 4)), precision: 5);
        Assert.Equal(world.GetElevation(new GridPosition(3, 4)), world.GetElevation(new GridPosition(5, 4)));
        Assert.Equal(0f, world.GetElevation(new GridPosition(6, 4)));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(1f)]
    public void SmoothingStrengthControlsBlend(float strength)
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        var center = new GridPosition(4, 4);
        world.SetElevation(center, 0.9f);

        Geology.ApplyRadialSmoothing(world, center, 1, strength);

        Assert.Equal(0.9f - 0.8f * strength, world.GetElevation(center), precision: 5);
    }

    [Fact]
    public void SmoothingFlatGroundIsNoOp()
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        Assert.Equal(0, Geology.ApplyRadialSmoothing(world, new GridPosition(4, 4), 3, 1f));
    }

    [Fact]
    public void SmoothingWrapsAndDoesNotDuplicateSamplesInTinyWorlds()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean);
        world.SetElevation(new GridPosition(0, 0), 1f);
        Geology.ApplyRadialSmoothing(world, new GridPosition(0, 0), 5, 1f);
        Assert.Equal(0.5f, world.GetElevation(new GridPosition(0, 0)), precision: 5);
        Assert.Equal(0.4608f, world.GetElevation(new GridPosition(1, 0)), precision: 5);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void SmoothingRejectsInvalidStrength(float strength)
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Geology.ApplyRadialSmoothing(world, new GridPosition(4, 4), 3, strength));
    }

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
    public void SmoothingPreservesRingWallsAndExcludesThemFromAverages()
    {
        var world = new SimulationWorld(9, 5, Terrain.Plains);
        world.Body = WorldBody.RingWorld;
        TerrainClassifier.RebuildAll(world);
        var center = new GridPosition(4, 1);
        world.SetElevation(center, 0.9f);

        Geology.ApplyRadialSmoothing(world, center, 2, 1f);

        Assert.Equal(0.15f, world.GetElevation(center), precision: 5);
        Assert.Equal(SimulationWorld.RingWorldWallElevation, world.GetElevation(new GridPosition(4, 0)));
        Assert.Equal(Terrain.RingWorldWall, world.GetTerrain(new GridPosition(4, 0)));
    }

    [Fact]
    public void SmoothingRetracesExistingRivers()
    {
        var world = CreateForkedValley();
        Hydrology.TraceSpring(world, new GridPosition(7, 2));
        world.LastCompletedSpring = null;

        Assert.True(Geology.ApplyRadialSmoothing(world, new GridPosition(7, 6), 2, 0.5f) > 0);

        Assert.NotNull(world.LastCompletedSpring);
        Assert.Equal(0, world.ActiveSpringCount);
    }

    [Fact]
    public void UpliftCanRaiseOceanIntoLand()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        var center = new GridPosition(10, 10);
        world.OceanSeed = new GridPosition(0, 0);
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

        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(source));
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(raisedLakeTile));

        Geology.ApplyRadialUplift(world, raisedLakeTile, radius: 1, strength: 0.3f);

        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(raisedLakeTile));
        Assert.Equal(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(source));
    }

    [Fact]
    public void ElevationEditAwayFromStaleLakeClearsFreshwaterOverOcean()
    {
        var world = new SimulationWorld(9, 7, Terrain.Ocean);
        var staleLake = new GridPosition(4, 3);
        world.SetElevation(staleLake, -0.3f);
        TerrainClassifier.RebuildAll(world);
        world.SetSurfaceWater(staleLake, SurfaceWaterKind.FreshwaterLake);
        world.SetWaterSurfaceElevation(staleLake, 0.1f);

        Geology.ApplyRadialUplift(world, new GridPosition(0, 0), radius: 1, strength: 0.1f);

        Assert.True(world.GetTerrain(staleLake) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows);
        Assert.Equal(SurfaceWaterKind.None, world.GetSurfaceWater(staleLake));
        Assert.Null(world.GetWaterSurfaceElevation(staleLake));
    }

    [Fact]
    public void LoweringSeaLevelExposesSeabedWithoutChangingItsElevation()
    {
        var world = new SimulationWorld(7, 5, Terrain.Ocean);
        var lowlands = new GridPosition(2, 2);
        var canyon = new GridPosition(3, 2);
        var trench = new GridPosition(4, 2);
        world.SetElevation(lowlands, -0.1f);
        world.SetElevation(canyon, -0.3f);
        world.SetElevation(trench, -0.5f);
        TerrainClassifier.RebuildAll(world);

        Geology.ChangeSeaLevel(world, -0.6f);

        Assert.Equal(-0.6f, world.SeaLevel, precision: 5);
        Assert.Equal(-0.1f, world.GetElevation(lowlands), precision: 5);
        Assert.Equal(Terrain.Lowlands, world.GetTerrain(lowlands));
        Assert.Equal(Terrain.Canyon, world.GetTerrain(canyon));
        Assert.Equal(Terrain.Trench, world.GetTerrain(trench));
        Assert.NotEqual(Biome.None, world.GetBiome(lowlands));
    }

    [Fact]
    public void BelowSeaLevelLandlockedBasinRemainsDryUntilOceanCanReachIt()
    {
        var world = new SimulationWorld(7, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.4f);
            }
        }

        var basin = new GridPosition(3, 3);
        var oceanSeed = new GridPosition(3, 0);
        world.OceanSeed = oceanSeed;
        world.SetElevation(oceanSeed, -0.6f);
        world.SetElevation(basin, -0.3f);
        TerrainClassifier.RebuildAll(world);

        Assert.Equal(Terrain.Canyon, world.GetTerrain(basin));

        Geology.ChangeSeaLevel(world, 0.5f);

        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(basin));
    }

    [Fact]
    public void UpliftStopsAtMountainHeightLimit()
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        var summit = new GridPosition(4, 4);

        Geology.ApplyRadialUplift(world, summit, radius: 2, strength: 10f);

        Assert.Equal(SimulationWorld.MaximumGroundElevation, world.GetElevation(summit));
        Assert.Equal(Terrain.Mountain, world.GetTerrain(summit));
    }

    [Fact]
    public void LoweringStopsAtMinimumGroundElevation()
    {
        var world = new SimulationWorld(9, 9, Terrain.Plains);
        var floor = new GridPosition(4, 4);

        Geology.ApplyRadialLowering(world, floor, radius: 2, strength: 10f);

        Assert.Equal(SimulationWorld.MinimumGroundElevation, world.GetElevation(floor));
    }

    [Fact]
    public void MaximumMountainCanHaveArcticClimateAtEquator()
    {
        var world = new SimulationWorld(21, 21, Terrain.Plains, seed: 91);
        var summit = new GridPosition(10, 10);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }

        world.SetElevation(summit, SimulationWorld.MaximumGroundElevation);
        TerrainClassifier.RebuildAll(world);

        Assert.Equal(Terrain.Mountain, world.GetTerrain(summit));
        Assert.Equal(TemperatureBand.Freezing, world.GetTemperatureBand(summit));
        Assert.Equal(Biome.Arctic, world.GetBiome(summit));
    }

    [Fact]
    public void SeaLevelStopsAtItsLimits()
    {
        var world = new SimulationWorld(3, 3, Terrain.Ocean);

        Geology.ChangeSeaLevel(world, 10f);
        Assert.Equal(SimulationWorld.MaximumSeaLevel, world.SeaLevel);

        Geology.ChangeSeaLevel(world, -10f);
        Assert.Equal(SimulationWorld.MinimumSeaLevel, world.SeaLevel);
    }

    [Fact]
    public void FineSeaLevelStepCanFloodTrenchWhileCanyonRemainsDry()
    {
        var world = new SimulationWorld(5, 5, Terrain.Plains);
        var trench = new GridPosition(2, 0);
        var canyon = new GridPosition(2, 2);
        world.OceanSeed = trench;
        world.SetElevation(trench, -0.46f);
        world.SetElevation(canyon, -0.30f);
        TerrainClassifier.RebuildAll(world);
        Geology.ChangeSeaLevel(world, -0.47f);

        Assert.Equal(Terrain.Trench, world.GetTerrain(trench));
        Assert.Equal(Terrain.Canyon, world.GetTerrain(canyon));

        Geology.ChangeSeaLevel(world, Geology.SeaLevelEditStep);

        Assert.Equal(-0.46f, world.SeaLevel, precision: 5);
        Assert.Equal(Terrain.Shallows, world.GetTerrain(trench));
        Assert.Equal(Terrain.Canyon, world.GetTerrain(canyon));
    }

    [Fact]
    public void DeepOceanSeedRemainsFloodedBelowItsShallowCoastalSill()
    {
        var world = new SimulationWorld(7, 7, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.2f);
            }
        }

        var coastalSill = new GridPosition(3, 0);
        var inlandSill = new GridPosition(3, 1);
        var deepOcean = new GridPosition(3, 2);
        world.OceanSeed = deepOcean;
        world.SetElevation(coastalSill, -0.04f);
        world.SetElevation(inlandSill, -0.04f);
        world.SetElevation(deepOcean, -0.5f);
        TerrainClassifier.RebuildAll(world);
        Geology.ChangeSeaLevel(world, -0.05f);

        Assert.Equal(Terrain.Lowlands, world.GetTerrain(coastalSill));
        Assert.False(world.GetTerrain(inlandSill) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows);
        Assert.True(world.GetTerrain(deepOcean) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows);

        Geology.ChangeSeaLevel(world, Geology.SeaLevelEditStep);

        Assert.True(world.GetTerrain(coastalSill) is Terrain.Ocean or Terrain.Shallows);
        Assert.True(world.GetTerrain(inlandSill) is Terrain.Ocean or Terrain.Shallows);
        Assert.True(world.GetTerrain(deepOcean) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows);
    }

    [Fact]
    public void OceanSeedStartsAtCenterAndCanBeMoved()
    {
        var world = new SimulationWorld(9, 7, Terrain.Plains);
        var destination = new GridPosition(1, 2);

        Assert.Equal(new GridPosition(4, 3), world.OceanSeed);

        Geology.MoveOceanSeed(world, destination);

        Assert.Equal(destination, world.OceanSeed);
    }

    [Fact]
    public void AddingOceanSeedsPreservesOtherOceansAndRejectsDuplicatesAndDryGround()
    {
        var world = new SimulationWorld(12, 3, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.8f);
            }
        }
        var primary = new GridPosition(1, 1);
        var second = new GridPosition(5, 1);
        var third = new GridPosition(9, 1);
        world.OceanSeed = primary;
        foreach (var position in new[] { primary, second, third })
        {
            world.SetElevation(position, -0.3f);
        }
        TerrainClassifier.RebuildAll(world);

        Assert.True(Geology.TryAddOceanSeed(world, second));
        Assert.True(Geology.TryAddOceanSeed(world, third));
        Assert.Equal(primary, world.OceanSeed);
        Assert.Equal(new[] { second, third }, world.AdditionalOceanSeeds);
        foreach (var position in new[] { primary, second, third })
        {
            Assert.True(world.GetTerrain(position) is Terrain.Shallows or Terrain.Ocean or Terrain.DeepOcean or Terrain.Ice);
        }
        Assert.False(Geology.TryAddOceanSeed(world, third));
        Assert.False(Geology.TryAddOceanSeed(world, new GridPosition(3, 1)));
        Assert.False(Geology.TryAddOceanSeed(world, new GridPosition(-1, 1)));

        Geology.MoveOceanSeed(world, second);
        Assert.Equal(second, world.OceanSeed);
        Assert.Equal(third, Assert.Single(world.AdditionalOceanSeeds));
    }

    [Fact]
    public void PlacingOceanSeedEnablesOceansOnDryWorld()
    {
        var world = new SimulationWorld(5, 5, Terrain.Plains)
        {
            HasOceans = false,
        };
        var destination = new GridPosition(1, 2);
        world.SetElevation(destination, -0.4f);
        TerrainClassifier.RebuildAll(world);
        Assert.Equal(Terrain.Canyon, world.GetTerrain(destination));

        Geology.MoveOceanSeed(world, destination);

        Assert.True(world.HasOceans);
        Assert.Equal(destination, world.OceanSeed);
        Assert.True(world.GetTerrain(destination) is Terrain.Ocean or Terrain.DeepOcean or Terrain.Shallows);
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
