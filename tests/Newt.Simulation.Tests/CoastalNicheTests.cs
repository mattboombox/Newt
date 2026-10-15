using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CoastalNicheTests
{
    [Fact]
    public void OnlyCrabsCanConsumeBeachNutritionRegardlessOfBiome()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var consume = typeof(SimulationWorld).GetMethod("TryConsumeEnvironmentalNutrition", flags)!;
        var deposit = typeof(SimulationWorld).GetMethod("DepositTileNutrition", flags)!;
        foreach (var biome in Enum.GetValues<Biome>())
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 1, Terrain.Beach) { SeasonsEnabled = false };
            var position = new GridPosition(0, 0);
            world.SetTemperature(position, 0.5f);
            world.SetBiome(position, biome);
            Assert.Equal(species is CritterSpecies.Crab, consume.Invoke(world, [species, 0]));
            deposit.Invoke(world, [0]);
            Assert.Equal(species is CritterSpecies.Crab, consume.Invoke(world, [species, 0]));
        }
    }

    [Theory]
    [InlineData(CritterSpecies.Deer, Biome.Grassland)]
    [InlineData(CritterSpecies.Deer, Biome.Forest)]
    [InlineData(CritterSpecies.Gazelle, Biome.Grassland)]
    [InlineData(CritterSpecies.Gazelle, Biome.Arid)]
    [InlineData(CritterSpecies.Gazelle, Biome.Desert)]
    [InlineData(CritterSpecies.Elk, Biome.Grassland)]
    [InlineData(CritterSpecies.Elk, Biome.Tundra)]
    [InlineData(CritterSpecies.Elk, Biome.Taiga)]
    [InlineData(CritterSpecies.MegaSlug, Biome.Swamp)]
    [InlineData(CritterSpecies.MegaSlug, Biome.Jungle)]
    [InlineData(CritterSpecies.MegaSlug, Biome.Bog)]
    [InlineData(CritterSpecies.MegaSlug, Biome.Forest)]
    public void GrazersRejectBeachFoodDespiteEdibleBiome(CritterSpecies species, Biome biome)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains) { SeasonsEnabled = false };
        var position = new GridPosition(0, 0);
        world.SetTemperature(position, 0.5f);
        world.SetBiome(position, biome);
        world.AddCritter(species, position);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", flags)!;
        Assert.Equal(true, feed.Invoke(world, [0]));

        world.SetTerrain(position, Terrain.Beach);
        Assert.Equal(false, feed.Invoke(world, [0]));
        typeof(SimulationWorld).GetMethod("DepositTileNutrition", flags)!.Invoke(world, [0]);
        Assert.Equal(false, feed.Invoke(world, [0]));
        var foliage = typeof(SimulationWorld).GetMethod("IsGrazerFoliageTile", flags)!;
        Assert.Equal(false, foliage.Invoke(world, [species, 0]));
    }

    [Fact]
    public void ElkDealsTwoCombatDamage()
    {
        Assert.Equal(2, SimulationWorld.GetCombatDamage(CritterSpecies.Elk));
    }

    [Theory]
    [InlineData(CritterSpecies.Worm, Terrain.Beach)]
    [InlineData(CritterSpecies.Trilobite, Terrain.Beach)]
    [InlineData(CritterSpecies.Therapsid, Terrain.Beach)]
    [InlineData(CritterSpecies.Newt, Terrain.Beach)]
    [InlineData(CritterSpecies.Nautilus, Terrain.DeepOcean)]
    public void RestrictedTerrainCannotFeedEvenWithDepositedNutrition(
        CritterSpecies species, Terrain terrain)
    {
        var world = new SimulationWorld(1, 1, Terrain.Shallows) { SeasonsEnabled = false };
        var position = new GridPosition(0, 0);
        world.AddCritter(species, position);
        world.SetTerrain(position, terrain);
        world.SetBiome(position, Biome.Jungle);
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(SimulationWorld).GetMethod("DepositTileNutrition", flags)!.Invoke(world, [0]);
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", flags)!;
        Assert.Equal(false, feed.Invoke(world, [0]));
    }

    [Fact]
    public void CrabsStillFeedOnBeaches()
    {
        var world = new SimulationWorld(1, 1, Terrain.Beach) { SeasonsEnabled = false };
        world.SetTemperature(new(0, 0), 0.5f);
        world.AddCritter(CritterSpecies.Crab, new(0, 0));
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(true, feed.Invoke(world, [0]));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt, Biome.Swamp)]
    [InlineData(CritterSpecies.Newt, Biome.Jungle)]
    [InlineData(CritterSpecies.Therapsid, Biome.Swamp)]
    [InlineData(CritterSpecies.Therapsid, Biome.Jungle)]
    [InlineData(CritterSpecies.Therapsid, Biome.Bog)]
    [InlineData(CritterSpecies.Therapsid, Biome.Arid)]
    public void BeachOccupantsCannotFeedFromBiomeOrAdjacentRiver(CritterSpecies species, Biome biome)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains) { SeasonsEnabled = false };
        var beach = new GridPosition(0, 0);
        world.SetTerrain(beach, Terrain.Beach);
        world.SetTemperature(beach, 0.5f);
        world.SetBiome(beach, biome);
        world.SetSurfaceWater(new(1, 0), SurfaceWaterKind.River);
        var id = world.AddCritter(species, beach);
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(false, feed.Invoke(world, [0]));
        Assert.True(world.TryGetCritter(id, out var critter));
        Assert.Equal(CritterNutritions.Get(species).InitialEnergy, critter.Energy);
    }
}
