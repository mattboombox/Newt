using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class HungerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale, 19, true)]
    [InlineData(CritterSpecies.ToothedWhale, 20, false)]
    [InlineData(CritterSpecies.MegaSpider, 7, true)]
    [InlineData(CritterSpecies.MegaSpider, 8, false)]
    [InlineData(CritterSpecies.MegaToad, 7, true)]
    [InlineData(CritterSpecies.MegaToad, 8, false)]
    [InlineData(CritterSpecies.Therapsid, 5, true)]
    [InlineData(CritterSpecies.Therapsid, 6, false)]
    public void HungerUsesStrictTwoThirdsBoundary(CritterSpecies species, int energy, bool hungry)
    {
        Assert.Equal(hungry, CritterNutritions.Get(species).IsHungryAt(energy));
    }

    [Theory]
    [InlineData(CritterSpecies.Squid, CritterSpecies.Fish, Terrain.Ocean)]
    [InlineData(CritterSpecies.SeaScorpion, CritterSpecies.Fish, Terrain.Ocean)]
    [InlineData(CritterSpecies.MegaToad, CritterSpecies.Fish, Terrain.Shallows)]
    [InlineData(CritterSpecies.Wolf, CritterSpecies.Monkey, Terrain.Plains)]
    [InlineData(CritterSpecies.MegaSpider, CritterSpecies.Monkey, Terrain.Plains)]
    public void PredatorsOnlyTargetFoodBelowHungerThreshold(
        CritterSpecies species, CritterSpecies prey, Terrain terrain)
    {
        var world = new SimulationWorld(10, 1, terrain, seed: 17);
        world.AddCritter(species, new(0, 0));
        var food = new GridPosition(1, 0);
        world.AddCritter(prey, food);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        var threshold = CritterNutritions.Get(species).HungryThreshold;
        energies[0] = threshold + 1;
        Assert.False(world.GetCritter(0).IsHunting);
        Assert.Null(world.FindHunterPrey(0, species, 4, null));
        energies[0] = threshold;
        Assert.True(world.GetCritter(0).IsHunting);
        Assert.Equal(food, world.FindHunterPrey(0, species, 4, null));
    }

    [Fact]
    public void HungryMegaToadCanReachBreedingEnergyFromFish()
    {
        var world = new SimulationWorld(10, 1, Terrain.Shallows, seed: 17);
        world.AddCritter(CritterSpecies.MegaToad, new(0, 0));
        var food = new GridPosition(1, 0);
        world.AddCritter(CritterSpecies.Fish, food);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energies[0] = 7;
        Assert.True(world.GetCritter(0).IsHunting);
        Assert.Equal(food, world.FindHunterPrey(0, CritterSpecies.MegaToad, 4, null));
        world.CommitEncounter(new(0, 0), food);
        Assert.Equal(11, world.GetCritter(0).Energy);
        Assert.True(world.GetCritter(0).CanReproduce);
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale, CritterSpecies.Crab)]
    [InlineData(CritterSpecies.BaleenWhale, CritterSpecies.Plankton)]
    public void WhalesHuntUntilFullAndCanReachBreedingEnergy(CritterSpecies species, CritterSpecies prey)
    {
        var world = new SimulationWorld(10, 1, Terrain.Ocean, seed: 17);
        world.AddCritter(species, new(0, 0));
        var food = new GridPosition(1, 0);
        world.AddCritter(prey, food);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        var nutrition = CritterNutritions.Get(species);
        Assert.True(nutrition.HuntsUntilFull);
        energies[0] = nutrition.MaximumEnergy - 1;
        Assert.False(world.GetCritter(0).IsHungry);
        Assert.True(world.GetCritter(0).IsHunting);
        Assert.Equal(food, world.FindHunterPrey(0, species, 4, null));
        world.CommitEncounter(new(0, 0), food);
        Assert.Equal(nutrition.MaximumEnergy, world.GetCritter(0).Energy);
        Assert.True(world.GetCritter(0).CanReproduce);
        Assert.False(world.GetCritter(0).IsHunting);
        world.AddCritter(prey, new(2, 0));
        Assert.Null(world.FindHunterPrey(0, species, 4, null));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt, Biome.Bog)]
    [InlineData(CritterSpecies.Therapsid, Biome.Bog)]
    [InlineData(CritterSpecies.Monkey, Biome.Bog)]
    [InlineData(CritterSpecies.Deer, Biome.Forest)]
    public void TileFeedersEatAboveHungerThreshold(CritterSpecies species, Biome biome)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 17);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, biome);
        world.AddCritter(species, position);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        var energy = CritterNutritions.Get(species).MaximumEnergy - 1;
        energies[0] = energy;
        var food = world.GetTileNutrition(position);
        Assert.False(world.GetCritter(0).IsHungry);
        Assert.False(world.GetCritter(0).IsHunting);
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", Private)!.Invoke(world, [0])!);
        Assert.Equal(energy + 1, world.GetCritter(0).Energy);
        Assert.Equal(food - 1, world.GetTileNutrition(position));
    }
}
