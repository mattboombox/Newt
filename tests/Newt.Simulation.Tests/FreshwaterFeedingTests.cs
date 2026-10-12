using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class FreshwaterFeedingTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(7, 2, true)]
    [InlineData(2, 1, false)]
    public void WolvesOnlyHuntAdjacentFishIncludingAcrossWorldSeam(int fishX, int fishY, bool adjacent)
    {
        var world = new SimulationWorld(8, 3, Terrain.Plains);
        var wolfTile = new GridPosition(0, 1);
        var fishTile = new GridPosition(fishX, fishY);
        world.SetTerrain(fishTile, Terrain.Ocean);
        world.AddCritter(CritterSpecies.Wolf, wolfTile);
        var fish = world.AddCritter(CritterSpecies.Fish, fishTile);
        Assert.Equal(adjacent, world.FindHunterPrey(0, CritterSpecies.Wolf, 6, null) is not null);
        world.CommitEncounter(wolfTile, fishTile);
        Assert.Equal(!adjacent, world.TryGetCritter(fish, out _));
        Assert.Equal(wolfTile, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Snail, SurfaceWaterKind.River, false)]
    [InlineData(CritterSpecies.Snail, SurfaceWaterKind.FreshwaterLake, false)]
    [InlineData(CritterSpecies.Fish, SurfaceWaterKind.River, true)]
    [InlineData(CritterSpecies.Newt, SurfaceWaterKind.FreshwaterLake, true)]
    public void FreshwaterFoodIsReservedForFishAndNewts(CritterSpecies species, SurfaceWaterKind water, bool canFeed)
    {
        var world = new SimulationWorld(1, 1, Terrain.Shallows);
        world.SetSurfaceWater(new(0, 0), water);
        Assert.True(world.TrySpawnCritter(species, new(0, 0)));
        var food = world.GetTileNutrition(new(0, 0));
        var fed = (bool)typeof(SimulationWorld).GetMethod("TryFeedFromTerrain",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(world, [0])!;
        Assert.Equal(canFeed, fed);
        Assert.Equal(food - (canFeed ? 1 : 0), world.GetTileNutrition(new(0, 0)));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void SnailCannotEatFreshwaterFoodFromAdjacentLand(SurfaceWaterKind water)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains);
        world.SetBiome(new(0, 0), Biome.Desert);
        world.SetSurfaceWater(new(1, 0), water);
        world.AddCritter(CritterSpecies.Snail, new(0, 0));
        var fed = (bool)typeof(SimulationWorld).GetMethod("TryFeedFromTerrain",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(world, [0])!;
        Assert.False(fed);
        Assert.Equal(3, world.GetTileNutrition(new(1, 0)));
    }

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
        Assert.Equal(3, world.GetTileNutrition(position));
    }
}
