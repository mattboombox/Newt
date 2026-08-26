using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class FreshwaterFeedingTests
{
    [Theory]
    [InlineData(CritterSpecies.Therapsid, Biome.Jungle, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Monkey, Biome.Jungle, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Ape, Biome.Jungle, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Deer, Biome.Grassland, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Elk, Biome.Grassland, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Gazelle, Biome.Grassland, SurfaceWaterKind.River)]
    [InlineData(CritterSpecies.Therapsid, Biome.Jungle, SurfaceWaterKind.FreshwaterLake)]
    [InlineData(CritterSpecies.Deer, Biome.Grassland, SurfaceWaterKind.FreshwaterLake)]
    public void TerrestrialFoliageFeedersDoNotConsumeFreshwaterNutrition(
        CritterSpecies species,
        Biome biome,
        SurfaceWaterKind water)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 5201);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, biome);
        world.SetSurfaceWater(position, water);
        Assert.True(world.TrySpawnCritter(species, position));
        var initialEnergy = world.GetCritter(0).Energy;

        for (var tick = 0;
            tick <= SimulationWorld.GetMovementIntervalTicks(species);
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(initialEnergy, world.GetCritter(0).Energy);
        Assert.Equal(2, world.GetTileNutrition(position));
    }
}
