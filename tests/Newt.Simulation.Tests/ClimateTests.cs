using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ClimateTests
{
    [Fact]
    public void LifelessWorldKeepsDesertAndArcticBiomes()
    {
        var world = new SimulationWorld(5, 1, Terrain.Plains);
        world.HasOceans = false;
        var ordinary = new GridPosition(0, 0);
        var desert = new GridPosition(1, 0);
        var arcticPlain = new GridPosition(2, 0);
        var snowyMountain = new GridPosition(3, 0);
        var ice = new GridPosition(4, 0);
        world.SetTerrain(snowyMountain, Terrain.Mountain);
        world.SetTerrain(ice, Terrain.Ice);
        world.SetTemperature(ordinary, 0.5f);
        world.SetMoisture(ordinary, 0.5f);
        world.SetTemperature(desert, 0.9f);
        world.SetMoisture(desert, 0.1f);
        world.SetTemperature(arcticPlain, 0.1f);
        world.SetMoisture(arcticPlain, 0.5f);
        world.SetTemperature(snowyMountain, 0.1f);
        world.SetMoisture(snowyMountain, 0.5f);
        world.SetTemperature(ice, 0.1f);
        world.SetMoisture(ice, 0.5f);

        LifeSystem.SetEnabled(world, false);
        // Reapply controlled fields after the life toggle's climate refresh.
        world.SetTemperature(ordinary, 0.5f);
        world.SetMoisture(ordinary, 0.5f);
        world.SetTemperature(desert, 0.9f);
        world.SetMoisture(desert, 0.1f);
        world.SetTemperature(arcticPlain, 0.1f);
        world.SetMoisture(arcticPlain, 0.5f);
        world.SetTemperature(snowyMountain, 0.1f);
        world.SetMoisture(snowyMountain, 0.5f);
        ClimateSystem.RebuildBiomesFromCurrentMoisture(world);

        Assert.Equal(Biome.None, world.GetBiome(ordinary));
        Assert.Equal(Biome.Desert, world.GetBiome(desert));
        Assert.Equal(Biome.Arctic, world.GetBiome(arcticPlain));
        Assert.Equal(Biome.Arctic, world.GetBiome(snowyMountain));
        Assert.Equal(Biome.None, world.GetBiome(ice));
    }

    [Fact]
    public void ReenabledLifeReclaimsBarrenStoneTerrainGradually()
    {
        var world = new SimulationWorld(8, 4, Terrain.Plains, seed: 73);
        world.HasOceans = false;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                world.SetTemperature(position, 0.5f);
                world.SetMoisture(position, 0.5f);
            }
        }

        LifeSystem.SetEnabled(world, false);
        LifeSystem.SetEnabled(world, true);

        Assert.All(AllPositions(world), position =>
        {
            Assert.True(world.IsLifeRecoveryPending(position));
            Assert.Equal(Biome.None, world.GetBiome(position));
        });

        for (var tick = 0; tick < 46 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.All(AllPositions(world), position =>
        {
            Assert.False(world.IsLifeRecoveryPending(position));
            Assert.NotEqual(Biome.None, world.GetBiome(position));
        });
    }

    [Theory]
    [InlineData(Terrain.Plains, true)]
    [InlineData(Terrain.Hills, true)]
    [InlineData(Terrain.Lowlands, true)]
    [InlineData(Terrain.Canyon, true)]
    [InlineData(Terrain.Trench, true)]
    [InlineData(Terrain.Beach, false)]
    [InlineData(Terrain.Mountain, false)]
    public void BarrenStonePresentationExcludesBeachesAndMountains(
        Terrain terrain,
        bool expected)
    {
        Assert.Equal(expected, LifeSystem.IsBarrenStoneTerrain(terrain));
    }

    [Theory]
    [InlineData(Terrain.Plains, true)]
    [InlineData(Terrain.Hills, true)]
    [InlineData(Terrain.Lowlands, true)]
    [InlineData(Terrain.Canyon, true)]
    [InlineData(Terrain.Trench, true)]
    [InlineData(Terrain.Beach, false)]
    [InlineData(Terrain.Mountain, false)]
    [InlineData(Terrain.Ocean, false)]
    public void NoneBiomeMeansStoneOnlyOnOrdinaryBarrenLand(
        Terrain terrain,
        bool expected)
    {
        var world = new SimulationWorld(1, 1, terrain);
        world.SetBiome(new GridPosition(0, 0), Biome.None);

        Assert.Equal(expected, LifeSystem.IsStoneBiome(world, new GridPosition(0, 0)));
    }

    [Fact]
    public void FreezingDeepOceanFormsSeaIce()
    {
        var world = new SimulationWorld(9, 9, Terrain.DeepOcean, seed: 6);
        world.OceanSeed = new GridPosition(4, 0);
        world.GlobalTemperatureOffset = -1f;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.5f);
            }
        }

        TerrainClassifier.RebuildAll(world);

        var polarDeepOcean = new GridPosition(4, 0);
        Assert.True(world.SeaLevel - world.GetElevation(polarDeepOcean) > 0.20f);
        Assert.Equal(TemperatureBand.Freezing, world.GetTemperatureBand(polarDeepOcean));
        Assert.Equal(Terrain.Ice, world.GetTerrain(polarDeepOcean));
    }

    [Fact]
    public void HemispheresHaveOppositeSeasonsAndEquatorIsUnaffected()
    {
        var world = CreateFlatLand(width: 21, height: 21, seed: 7);
        TerrainClassifier.RebuildAll(world);
        var north = new GridPosition(10, 0);
        var equator = new GridPosition(10, 10);
        var south = new GridPosition(10, 20);
        var equatorialTemperature = world.GetTemperature(equator);

        for (var tick = 0; tick < SeasonSystem.CycleSeconds * SimulationWorld.TicksPerSecond / 4; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(Season.Summer, SeasonSystem.GetSeason(world, north));
        Assert.Equal(Season.Winter, SeasonSystem.GetSeason(world, south));
        Assert.Equal(Season.PermanentSummer, SeasonSystem.GetSeason(world, equator));
        Assert.Equal(equatorialTemperature, world.GetTemperature(equator), precision: 5);
        Assert.Equal(0f, SeasonSystem.GetTemperatureChange(world, equator));
        Assert.Equal(0f, SeasonSystem.GetMoistureChange(world, north));
        Assert.InRange(SeasonSystem.GetTemperatureChange(world, north), 0.09f, 0.10f);
        Assert.True(world.GetTemperature(north) > world.GetTemperature(south));
    }

    [Fact]
    public void DisablingSeasonsRestoresBaselineTemperature()
    {
        var seasonal = CreateFlatLand(width: 21, height: 21, seed: 9);
        var baseline = CreateFlatLand(width: 21, height: 21, seed: 9);
        TerrainClassifier.RebuildAll(seasonal);
        SeasonSystem.SetEnabled(baseline, false);

        for (var tick = 0; tick < SeasonSystem.CycleSeconds * SimulationWorld.TicksPerSecond / 4; tick++)
        {
            seasonal.AdvanceOneTick();
        }

        SeasonSystem.SetEnabled(seasonal, false);
        var position = new GridPosition(10, 0);
        Assert.Equal(baseline.GetTemperature(position), seasonal.GetTemperature(position));
        Assert.Equal(Season.PermanentSummer, SeasonSystem.GetSeason(seasonal, position));
    }

    [Fact]
    public void DisabledSeasonsPauseTheSeasonalCalendar()
    {
        var world = CreateFlatLand(width: 5, height: 5, seed: 10);
        TerrainClassifier.RebuildAll(world);
        world.AdvanceOneTick();
        Assert.Equal(1, world.SeasonTick);

        SeasonSystem.SetEnabled(world, false);
        world.AdvanceOneTick();

        Assert.Equal(2, world.Tick);
        Assert.Equal(1, world.SeasonTick);
        Assert.Equal(0, world.Year);
    }

    [Fact]
    public void SeasonalClimateRefreshRunsEveryThirtySecondsAcrossSpacedStages()
    {
        var world = new SimulationWorld(3, 3, Terrain.Plains, seed: 12);
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in AllPositions(world))
        {
            world.SetElevation(position, -0.5f);
        }
        var target = new GridPosition(1, 1);
        world.SetBiome(target, Biome.Jungle);
        world.SeasonTick = SeasonSystem.ClimateRefreshTicks - 1;

        world.AdvanceOneTick();

        Assert.Equal(30, SeasonSystem.ClimateRefreshSeconds);
        Assert.Equal(Terrain.Plains, world.GetTerrain(target));
        Assert.Equal(Biome.Jungle, world.GetBiome(target));

        for (var tick = 0; tick < SeasonSystem.ClimateRefreshStageSpacingTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetTerrain(target) is Terrain.DeepOcean or Terrain.Ice);
        Assert.Equal(Biome.Jungle, world.GetBiome(target));

        for (var tick = 0; tick < SeasonSystem.ClimateRefreshStageSpacingTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(Biome.None, world.GetBiome(target));
    }

    [Fact]
    public void PeakSummerLeavesSomeNorthernArcticClimateOnStandardWorld()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Standard, Seed: 20260806));

        for (var tick = 0; tick < SeasonSystem.CycleSeconds * SimulationWorld.TicksPerSecond / 4; tick++)
        {
            world.AdvanceOneTick();
        }

        var northernArcticTiles = 0;
        for (var y = 0; y < Math.Max(1, world.Height / 10); y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetBiome(new GridPosition(x, y)) is Biome.Arctic)
                {
                    northernArcticTiles++;
                }
            }
        }

        Assert.True(northernArcticTiles > 0, "Peak northern summer should leave a natural polar ice edge.");
    }

    [Fact]
    public void GlobalClimateToolsAdjustFieldsAndStopAtReasonableCaps()
    {
        var world = CreateFlatLand(width: 21, height: 21, seed: 11);
        var position = new GridPosition(10, 5);
        TerrainClassifier.RebuildAll(world);
        var originalTemperature = world.GetTemperature(position);
        var originalMoisture = world.GetMoisture(position);

        ClimateSystem.AdjustGlobalTemperature(world, ClimateSystem.GlobalClimateEditStep);
        ClimateSystem.AdjustGlobalMoisture(world, ClimateSystem.GlobalClimateEditStep);

        Assert.True(world.GetTemperature(position) > originalTemperature);
        Assert.True(world.GetMoisture(position) > originalMoisture);

        ClimateSystem.AdjustGlobalTemperature(world, 10f);
        ClimateSystem.AdjustGlobalMoisture(world, 10f);
        Assert.Equal(1f, SimulationWorld.MaximumGlobalClimateOffset);
        Assert.Equal(SimulationWorld.MaximumGlobalClimateOffset, world.GlobalTemperatureOffset);
        Assert.Equal(SimulationWorld.MaximumGlobalClimateOffset, world.GlobalMoistureOffset);

        ClimateSystem.AdjustGlobalTemperature(world, -10f);
        ClimateSystem.AdjustGlobalMoisture(world, -10f);
        Assert.Equal(-1f, SimulationWorld.MinimumGlobalClimateOffset);
        Assert.Equal(SimulationWorld.MinimumGlobalClimateOffset, world.GlobalTemperatureOffset);
        Assert.Equal(SimulationWorld.MinimumGlobalClimateOffset, world.GlobalMoistureOffset);
    }

    [Fact]
    public void EquatorialLowlandIsWarmerThanPolarLowland()
    {
        var world = CreateFlatLand(width: 41, height: 41, seed: 17);
        TerrainClassifier.RebuildAll(world);

        var polar = world.GetTemperature(new GridPosition(20, 0));
        var equatorial = world.GetTemperature(new GridPosition(20, 20));

        Assert.True(equatorial > polar + 0.5f);
    }

    [Fact]
    public void HillsAreColderAndDrierThanPlainsAtTheSameClimateLocation()
    {
        var lowland = CreateFlatLand(width: 41, height: 41, seed: 23);
        var highland = CreateFlatLand(width: 41, height: 41, seed: 23);
        for (var y = 0; y < lowland.Height; y++)
        {
            lowland.SetElevation(new GridPosition(0, y), -0.3f);
            highland.SetElevation(new GridPosition(0, y), -0.3f);
        }
        lowland.OceanSeed = new GridPosition(0, lowland.Height / 2);
        highland.OceanSeed = new GridPosition(0, highland.Height / 2);

        var position = new GridPosition(2, 20);
        highland.SetElevation(position, 0.4f);

        TerrainClassifier.RebuildAll(lowland);
        TerrainClassifier.RebuildAll(highland);

        Assert.Equal(Terrain.Plains, lowland.GetTerrain(position));
        Assert.Equal(Terrain.Hills, highland.GetTerrain(position));
        Assert.True(highland.GetTemperature(position) < lowland.GetTemperature(position) - 0.1f);
        Assert.True(highland.GetMoisture(position) < lowland.GetMoisture(position) - 0.04f);
    }

    [Fact]
    public void OceanMoistureDecaysInland()
    {
        var world = CreateFlatLand(width: 41, height: 21, seed: 31);
        for (var y = 0; y < world.Height; y++)
        {
            world.SetElevation(new GridPosition(0, y), -0.3f);
        }

        TerrainClassifier.RebuildAll(world);

        var nearCoast = world.GetMoisture(new GridPosition(2, 10));
        var inland = world.GetMoisture(new GridPosition(20, 10));
        Assert.True(nearCoast > inland);
    }

    [Fact]
    public void FreshwaterRaisesNearbyMoisture()
    {
        var world = CreateFlatLand(width: 41, height: 21, seed: 47);
        var lake = new GridPosition(20, 10);
        var nearby = new GridPosition(22, 10);
        TerrainClassifier.RebuildAll(world);
        var before = world.GetMoisture(nearby);

        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);
        world.SetWaterSurfaceElevation(lake, world.GetElevation(lake) + 0.1f);
        TerrainClassifier.RebuildAll(world);

        Assert.True(world.GetMoisture(nearby) > before + 0.2f);
    }

    [Fact]
    public void LakesCreateWetterAndBroaderMoistureZonesThanRivers()
    {
        var riverWorld = CreateFlatLand(width: 41, height: 21, seed: 49);
        var lakeWorld = CreateFlatLand(width: 41, height: 21, seed: 49);
        riverWorld.HasOceans = false;
        lakeWorld.HasOceans = false;
        var source = new GridPosition(20, 10);
        riverWorld.SetSurfaceWater(source, SurfaceWaterKind.River);
        lakeWorld.SetSurfaceWater(source, SurfaceWaterKind.FreshwaterLake);

        TerrainClassifier.RebuildAll(riverWorld);
        TerrainClassifier.RebuildAll(lakeWorld);

        var adjacent = new GridPosition(21, 10);
        var fartherAway = new GridPosition(27, 10);

        Assert.True(lakeWorld.GetMoisture(adjacent) > riverWorld.GetMoisture(adjacent));
        Assert.True(lakeWorld.GetMoisture(fartherAway) > riverWorld.GetMoisture(fartherAway));
    }

    [Fact]
    public void HotLowlandBesideLakeBecomesJungle()
    {
        var world = CreateFlatLand(width: 41, height: 21, seed: 49);
        world.HasOceans = false;
        var lake = new GridPosition(20, 10);
        var lakeside = new GridPosition(21, 10);
        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);
        TerrainClassifier.RebuildAll(world);

        foreach (var position in AllPositions(world))
        {
            world.SetTemperature(position, 0.8f);
        }

        ClimateSystem.RebuildMoistureAndBiomes(world);

        Assert.Equal(MoistureBand.Wet, ClimateSystem.ClassifyMoisture(world.GetMoisture(lakeside)));
        Assert.Equal(Biome.Jungle, world.GetBiome(lakeside));
    }

    [Fact]
    public void RiverCreatesAGraduallyFadingRiparianCorridor()
    {
        var world = CreateFlatLand(width: 41, height: 21, seed: 53);
        var river = new GridPosition(20, 10);
        var threeTilesAway = new GridPosition(20, 7);
        var sixTilesAway = new GridPosition(20, 4);
        TerrainClassifier.RebuildAll(world);
        var dryMoistureAtThree = world.GetMoisture(threeTilesAway);
        var dryMoistureAtSix = world.GetMoisture(sixTilesAway);

        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        TerrainClassifier.RebuildAll(world);

        var influenceAtThree = world.GetMoisture(threeTilesAway) - dryMoistureAtThree;
        var influenceAtSix = world.GetMoisture(sixTilesAway) - dryMoistureAtSix;
        Assert.True(influenceAtThree > 0.2f);
        Assert.True(influenceAtSix > 0.1f);
        Assert.True(influenceAtThree > influenceAtSix);
    }

    [Fact]
    public void GrasslandClimateDoesNotFlattenHills()
    {
        var world = CreateFlatLand(width: 41, height: 41, seed: 71);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.4f);
            }
        }

        var position = new GridPosition(20, 13);
        TerrainClassifier.RebuildAll(world);

        Assert.Equal(Biome.Grassland, world.GetBiome(position));
        Assert.Equal(Terrain.Hills, world.GetTerrain(position));
    }

    [Fact]
    public void ArcticBiomeDoesNotReplaceLandform()
    {
        var world = CreateFlatLand(width: 41, height: 41, seed: 79);
        var plains = new GridPosition(10, 0);
        var hills = new GridPosition(20, 0);
        var mountain = new GridPosition(30, 0);
        world.SetElevation(hills, 0.4f);
        world.SetElevation(mountain, 0.7f);

        TerrainClassifier.RebuildAll(world);

        Assert.Equal(Terrain.Plains, world.GetTerrain(plains));
        Assert.Equal(Terrain.Hills, world.GetTerrain(hills));
        Assert.Equal(Terrain.Mountain, world.GetTerrain(mountain));
        Assert.Equal(Biome.Arctic, world.GetBiome(plains));
        Assert.Equal(Biome.Arctic, world.GetBiome(hills));
        Assert.Equal(Biome.Arctic, world.GetBiome(mountain));
    }

    [Theory]
    [InlineData(0.10f, 0.10f, Biome.Arctic)]
    [InlineData(0.10f, 0.90f, Biome.Arctic)]
    [InlineData(0.20f, 0.20f, Biome.Tundra)]
    [InlineData(0.20f, 0.50f, Biome.Taiga)]
    [InlineData(0.20f, 0.80f, Biome.Bog)]
    [InlineData(0.50f, 0.20f, Biome.Grassland)]
    [InlineData(0.50f, 0.50f, Biome.Forest)]
    [InlineData(0.55f, 0.80f, Biome.Swamp)]
    [InlineData(0.80f, 0.20f, Biome.Desert)]
    [InlineData(0.80f, 0.50f, Biome.Arid)]
    [InlineData(0.80f, 0.80f, Biome.Jungle)]
    public void TemperatureAndMoistureClassifyBiome(float temperature, float moisture, Biome expected)
    {
        Assert.Equal(expected, ClimateSystem.ClassifyBiome(temperature, moisture));
    }

    [Theory]
    [InlineData(0.00f, TemperatureBand.Freezing)]
    [InlineData(0.17f, TemperatureBand.Freezing)]
    [InlineData(0.18f, TemperatureBand.Cold)]
    [InlineData(0.32f, TemperatureBand.Cold)]
    [InlineData(0.33f, TemperatureBand.Temperate)]
    [InlineData(0.66f, TemperatureBand.Temperate)]
    [InlineData(0.67f, TemperatureBand.Hot)]
    [InlineData(1.00f, TemperatureBand.Hot)]
    public void SharedTemperatureThresholdsClassifyLandAndOcean(float temperature, TemperatureBand expected)
    {
        Assert.Equal(expected, ClimateSystem.ClassifyTemperature(temperature));
    }

    [Theory]
    [InlineData(0.00f, MoistureBand.Dry)]
    [InlineData(0.32f, MoistureBand.Dry)]
    [InlineData(0.33f, MoistureBand.Normal)]
    [InlineData(0.66f, MoistureBand.Normal)]
    [InlineData(0.67f, MoistureBand.Wet)]
    [InlineData(1.00f, MoistureBand.Wet)]
    public void SharedMoistureThresholdsMatchBiomeClassification(float moisture, MoistureBand expected)
    {
        Assert.Equal(expected, ClimateSystem.ClassifyMoisture(moisture));
    }

    private static SimulationWorld CreateFlatLand(int width, int height, ulong seed)
    {
        var world = new SimulationWorld(width, height, Terrain.Plains, seed);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.1f);
            }
        }

        return world;
    }

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
