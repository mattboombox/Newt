using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MegaSpiderStoredFoodTests
{
    [Fact]
    public void StoredFoodLetsSatiatedSpiderReachBreedingEnergy()
    {
        var world = new SimulationWorld(5, 3, Terrain.Plains) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Forest);
        var position = new GridPosition(2, 1);
        var id = world.AddCritter(CritterSpecies.MegaSpider, position);
        Assert.True(world.TryCreateMegaSpiderWeb(id, position));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        var food = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_megaSpiderWebFood", flags)!.GetValue(world)!;
        energy[0] = 8;
        Assert.False(CritterNutritions.Get(CritterSpecies.MegaSpider).IsHungryAt(8));
        food[position.Y * world.Width + position.X] = 10;

        for (var tick = 0; tick < 4 * SimulationWorld.GetMovementIntervalTicks(CritterSpecies.MegaSpider); tick++)
            world.AdvanceOneTick();

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.MegaSpider));
        Assert.True(world.GetMegaSpiderWebFood(position) < 10);
    }

    [Fact]
    public void FullSpiderDoesNotWasteStoredFood()
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, Biome.Forest);
        var id = world.AddCritter(CritterSpecies.MegaSpider, position);
        Assert.True(world.TryCreateMegaSpiderWeb(id, position));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        var food = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_megaSpiderWebFood", flags)!.GetValue(world)!;
        var nutrition = CritterNutritions.Get(CritterSpecies.MegaSpider);
        energy[0] = nutrition.MaximumEnergy;
        food[0] = 5;
        var feed = typeof(SimulationWorld).GetMethod("TryFeedMegaSpiderFromWeb", flags)!;
        Assert.Equal(false, feed.Invoke(world, [0, nutrition]));
        Assert.Equal(5, world.GetMegaSpiderWebFood(position));
    }
}
