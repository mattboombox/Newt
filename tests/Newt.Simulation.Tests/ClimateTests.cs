using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ClimateTests
{
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
}
