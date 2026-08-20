using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class TileNutritionTests
{
    [Theory]
    [InlineData(Terrain.DeepOcean, Biome.None, SurfaceWaterKind.None, 2)]
    [InlineData(Terrain.Shallows, Biome.None, SurfaceWaterKind.None, 2)]
    [InlineData(Terrain.Beach, Biome.None, SurfaceWaterKind.None, 2)]
    [InlineData(Terrain.Plains, Biome.Tundra, SurfaceWaterKind.None, 1)]
    [InlineData(Terrain.Plains, Biome.Grassland, SurfaceWaterKind.None, 2)]
    [InlineData(Terrain.Plains, Biome.Taiga, SurfaceWaterKind.None, 3)]
    [InlineData(Terrain.Plains, Biome.Forest, SurfaceWaterKind.None, 4)]
    [InlineData(Terrain.Plains, Biome.Swamp, SurfaceWaterKind.None, 4)]
    [InlineData(Terrain.Plains, Biome.Jungle, SurfaceWaterKind.None, 4)]
    [InlineData(Terrain.Plains, Biome.Desert, SurfaceWaterKind.None, 0)]
    [InlineData(Terrain.Mountain, Biome.Forest, SurfaceWaterKind.None, 0)]
    [InlineData(Terrain.Ocean, Biome.None, SurfaceWaterKind.None, 0)]
    [InlineData(Terrain.Plains, Biome.Desert, SurfaceWaterKind.River, 2)]
    [InlineData(Terrain.Mountain, Biome.None, SurfaceWaterKind.FreshwaterLake, 2)]
    public void CapacityFollowsProductiveTerrainRules(
        Terrain terrain,
        Biome biome,
        SurfaceWaterKind water,
        int expectedCapacity)
    {
        var world = new SimulationWorld(1, 1, terrain);
        var position = new GridPosition(0, 0);
        world.SetTemperature(position, 0.5f);
        world.SetBiome(position, biome);
        world.SetSurfaceWater(position, water);

        Assert.Equal(expectedCapacity, world.GetTileNutritionCapacity(position));
        Assert.Equal(expectedCapacity, world.GetTileNutrition(position));
    }

    [Fact]
    public void TemperatureAndMoistureDoNotChangeBiomeNutrition()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains);
        var temperate = new GridPosition(0, 0);
        var warm = new GridPosition(1, 0);
        var warmWet = new GridPosition(2, 0);
        foreach (var position in new[] { temperate, warm, warmWet })
        {
            world.SetBiome(position, Biome.Grassland);
        }
        world.SetTemperature(warm, 0.8f);
        world.SetTemperature(warmWet, 0.8f);
        world.SetMoisture(warmWet, 0.8f);

        Assert.Equal(2, world.GetTileNutritionCapacity(temperate));
        Assert.Equal(2, world.GetTileNutritionCapacity(warm));
        Assert.Equal(2, world.GetTileNutritionCapacity(warmWet));
    }

    [Theory]
    [InlineData(Terrain.Beach, 0.10f, 1)]
    [InlineData(Terrain.Beach, 0.25f, 1)]
    [InlineData(Terrain.Beach, 0.50f, 2)]
    [InlineData(Terrain.Beach, 0.80f, 3)]
    [InlineData(Terrain.Shallows, 0.10f, 1)]
    [InlineData(Terrain.Shallows, 0.25f, 1)]
    [InlineData(Terrain.Shallows, 0.50f, 2)]
    [InlineData(Terrain.Shallows, 0.80f, 3)]
    public void CoastalNutritionFollowsTemperatureBand(
        Terrain terrain,
        float temperature,
        int expectedCapacity)
    {
        var world = new SimulationWorld(1, 1, terrain);
        var position = new GridPosition(0, 0);
        world.SetTemperature(position, temperature);

        Assert.Equal(expectedCapacity, world.GetTileNutritionCapacity(position));
    }

    [Fact]
    public void FeedingDepletesAndLazilyRegeneratesTileNutrition()
    {
        var world = new SimulationWorld(1, 1, Terrain.Beach, seed: 2001);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.SetTemperature(position, 0.5f);
        world.AddCritter(CritterSpecies.Crab, position);

        for (var tick = 0; tick < 16 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetTileNutrition(position));
        Assert.True(world.RemoveCritterAt(position));
        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetTileNutrition(position));
    }
}
