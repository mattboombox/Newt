using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class TileNutritionTests
{
    [Theory]
    [InlineData(Terrain.DeepOcean, Biome.None, SurfaceWaterKind.None, 1)]
    [InlineData(Terrain.Shallows, Biome.None, SurfaceWaterKind.None, 4)]
    [InlineData(Terrain.Beach, Biome.None, SurfaceWaterKind.None, 1)]
    [InlineData(Terrain.Plains, Biome.Tundra, SurfaceWaterKind.None, 1)]
    [InlineData(Terrain.Plains, Biome.Grassland, SurfaceWaterKind.None, 3)]
    [InlineData(Terrain.Plains, Biome.Taiga, SurfaceWaterKind.None, 3)]
    [InlineData(Terrain.Plains, Biome.Forest, SurfaceWaterKind.None, 4)]
    [InlineData(Terrain.Plains, Biome.Swamp, SurfaceWaterKind.None, 5)]
    [InlineData(Terrain.Plains, Biome.Jungle, SurfaceWaterKind.None, 6)]
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

        Assert.Equal(3, world.GetTileNutritionCapacity(temperate));
        Assert.Equal(3, world.GetTileNutritionCapacity(warm));
        Assert.Equal(3, world.GetTileNutritionCapacity(warmWet));
    }

    [Theory]
    [InlineData(Terrain.Beach, 0.10f, 0)]
    [InlineData(Terrain.Beach, 0.25f, 1)]
    [InlineData(Terrain.Beach, 0.50f, 1)]
    [InlineData(Terrain.Beach, 0.80f, 1)]
    [InlineData(Terrain.Shallows, 0.10f, 2)]
    [InlineData(Terrain.Shallows, 0.25f, 3)]
    [InlineData(Terrain.Shallows, 0.50f, 4)]
    [InlineData(Terrain.Shallows, 0.80f, 4)]
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

    [Theory]
    [InlineData(Biome.Jungle, 6)]
    [InlineData(Biome.Swamp, 5)]
    [InlineData(Biome.Forest, 4)]
    [InlineData(Biome.Grassland, 3)]
    [InlineData(Biome.Taiga, 3)]
    [InlineData(Biome.Bog, 2)]
    [InlineData(Biome.Arid, 2)]
    [InlineData(Biome.Tundra, 1)]
    [InlineData(Biome.Desert, 0)]
    [InlineData(Biome.Arctic, 0)]
    public void LandNutritionFollowsBiomeHierarchy(Biome biome, int expectedCapacity)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, biome);

        Assert.Equal(expectedCapacity, world.GetTileNutritionCapacity(position));
    }

    [Theory]
    [InlineData(Terrain.Ocean, 0.10f, 0)]
    [InlineData(Terrain.Ocean, 0.25f, 0)]
    [InlineData(Terrain.Ocean, 0.50f, 0)]
    [InlineData(Terrain.Ocean, 0.80f, 0)]
    [InlineData(Terrain.DeepOcean, 0.10f, 1)]
    [InlineData(Terrain.DeepOcean, 0.25f, 2)]
    [InlineData(Terrain.DeepOcean, 0.50f, 1)]
    [InlineData(Terrain.DeepOcean, 0.80f, 1)]
    public void OpenOceanNutritionFollowsDepthAndTemperature(
        Terrain terrain,
        float temperature,
        int expectedCapacity)
    {
        var world = new SimulationWorld(1, 1, terrain);
        var position = new GridPosition(0, 0);
        world.SetTemperature(position, temperature);

        Assert.Equal(expectedCapacity, world.GetTileNutritionCapacity(position));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River, 0.10f, 2)]
    [InlineData(SurfaceWaterKind.River, 0.25f, 2)]
    [InlineData(SurfaceWaterKind.River, 0.50f, 2)]
    [InlineData(SurfaceWaterKind.River, 0.80f, 2)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 0.10f, 2)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 0.25f, 2)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 0.50f, 2)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 0.80f, 2)]
    public void FreshwaterReplacesBiomeAndTemperatureNutrition(
        SurfaceWaterKind water,
        float temperature,
        int expectedCapacity)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, Biome.Grassland);
        world.SetTemperature(position, temperature);
        world.SetSurfaceWater(position, water);

        Assert.Equal(expectedCapacity, world.GetTileNutritionCapacity(position));
    }

    [Fact]
    public void IceSheetsHaveNoNutritionEvenWithAnUnderlyingBiome()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ice);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, Biome.Jungle);

        Assert.Equal(0, world.GetTileNutritionCapacity(position));
    }

    [Fact]
    public void IceSheetOverDeepOceanHasOneNutrition()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ice);
        var position = new GridPosition(0, 0);
        world.SetElevation(position, -0.3f);

        Assert.Equal(1, world.GetTileNutritionCapacity(position));
        Assert.Equal(1, world.GetTileNutrition(position));
    }

    [Fact]
    public void FeedingDepletesAndLazilyRegeneratesTileNutrition()
    {
        var world = new SimulationWorld(1, 1, Terrain.Shallows, seed: 2001);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Worm, position);

        for (var tick = 0; tick < 12 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetTileNutrition(position));
        Assert.True(world.RemoveCritterAt(position));
        var regenerationSeconds = SimulationWorld.TileNutritionRegenerationSeconds /
            world.GetTileNutritionCapacity(position);
        for (var tick = 0;
            tick < (regenerationSeconds + 1) * SimulationWorld.TicksPerSecond;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetTileNutrition(position));
    }

    [Fact]
    public void StarvationDepositsNutritionEvenWhenNaturalCapacityIsZero()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean, seed: 2002);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Fish, position);

        for (var tick = 0; tick < 3 * 45 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.CritterCount);
        Assert.Equal(0, world.GetTileNutritionCapacity(position));
        Assert.Equal(1, world.GetTileNutrition(position));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.None, 4)]
    [InlineData(SurfaceWaterKind.River, 3)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 3)]
    public void CrabConsumesDepositedNutritionOnlyOutsideFreshwater(
        SurfaceWaterKind water,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean, seed: 2003);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Fish, position);
        for (var tick = 0; tick < 3 * 45 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        world.SetSurfaceWater(position, water);
        var nutritionBeforeFeeding = world.GetTileNutrition(position);
        world.AddCritter(CritterSpecies.Crab, position);
        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
        Assert.Equal(
            nutritionBeforeFeeding - (water is SurfaceWaterKind.None ? 1 : 0),
            world.GetTileNutrition(position));
    }

    [Theory]
    [InlineData(CritterSpecies.Deer)]
    [InlineData(CritterSpecies.Elk)]
    [InlineData(CritterSpecies.Gazelle)]
    public void GrazersCannotConsumeDepositedNutritionOutsideSupportedBiomes(
        CritterSpecies species)
    {
        foreach (var biome in Enum.GetValues<Biome>())
        {
            if (IsSupportedGrazerBiome(species, biome))
            {
                continue;
            }

            var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 2004);
            world.SeasonsEnabled = false;
            var position = new GridPosition(0, 0);
            world.SetBiome(position, biome);
            world.AddCritter(CritterSpecies.Wolf, position);
            for (var tick = 0; tick < 5 * 60 * SimulationWorld.TicksPerSecond; tick++)
            {
                world.AdvanceOneTick();
            }

            var nutritionBeforeFeeding = world.GetTileNutrition(position);
            world.AddCritter(species, position);
            for (var tick = 0; tick < 7 * SimulationWorld.TicksPerSecond; tick++)
            {
                world.AdvanceOneTick();
            }

            Assert.Equal(CritterNutritions.Get(species).InitialEnergy, world.GetCritter(0).Energy);
            Assert.Equal(nutritionBeforeFeeding, world.GetTileNutrition(position));
        }
    }

    private static bool IsSupportedGrazerBiome(CritterSpecies species, Biome biome) =>
        species switch
        {
            CritterSpecies.Deer => biome is Biome.Grassland or Biome.Forest,
            CritterSpecies.Elk => biome is Biome.Grassland or Biome.Tundra or Biome.Taiga,
            CritterSpecies.Gazelle => biome is Biome.Grassland or Biome.Arid,
            _ => false,
        };
}
