using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianDogFeedingTests
{
    private static readonly GridPosition Camp = new(10, 10);

    private static SimulationWorld World(bool barbarian)
    {
        var world = new SimulationWorld(30, 30, Terrain.Plains, 7) { SeasonsEnabled = false, ApeVillageMergingEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        foreach (var i in Enumerable.Range(0, world.Width * world.Height))
            world.SetBiome(new(i % world.Width, i / world.Width), Biome.Grassland);
        Assert.True(barbarian ? world.TrySpawnBarbarianApeVillage(Camp) : world.TrySpawnTestApeVillage(Camp));
        world.StoreApeVillageFood(Camp, 50);
        return world;
    }

    private static bool Feed(SimulationWorld world, CritterId id)
    {
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
        return (bool)typeof(SimulationWorld).GetMethod("TryFeedApeFromVillage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [index, CritterNutritions.Get(CritterSpecies.Dog)])!;
    }

    private static CritterId[] RecruitDogs(SimulationWorld world, int count)
    {
        var owner = Enumerable.Range(0, world.CritterCount).First(i => world.GetCritter(i).Species != CritterSpecies.Dog);
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(Camp) < count; roll++)
            world.TryRecruitDogFromWolfKill(owner);
        Assert.Equal(count, world.GetApeVillageDogCount(Camp));
        return Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(c => c.Species == CritterSpecies.Dog).Select(c => c.Id).ToArray();
    }

    [Fact]
    public void BarbarianDogsEatAboveHungerThresholdUntilReadyToBreed()
    {
        var world = World(true);
        var dog = RecruitDogs(world, 1)[0];
        Assert.Equal(8, world.GetApeVillageDogCapacity(Camp));
        var nutrition = CritterNutritions.Get(CritterSpecies.Dog);
        while (world.TryGetCritter(dog, out var before) && before.Energy < nutrition.ReproductionThreshold)
            Assert.True(Feed(world, dog));
        Assert.False(Feed(world, dog));
        Assert.True(world.TryGetCritter(dog, out var fed));
        Assert.Equal(nutrition.ReproductionThreshold, fed.Energy);
        world.AdvanceOneTick();
        Assert.Equal(2, world.GetApeVillageDogCount(Camp));
    }

    [Fact]
    public void AtEightDogsCampStopsFeedingForGrowthAndRecruiting()
    {
        var world = World(true);
        var dogs = RecruitDogs(world, 8);
        Assert.Equal(8, world.GetApeVillageDogCount(Camp));
        Assert.False(Feed(world, dogs[0]));
        var owner = Enumerable.Range(0, world.CritterCount).First(i => world.GetCritter(i).Species != CritterSpecies.Dog);
        for (var roll = 0; roll < 100; roll++) world.TryRecruitDogFromWolfKill(owner);
        Assert.Equal(8, world.GetApeVillageDogCount(Camp));
    }

    [Fact]
    public void PeacefulDogsKeepExistingHungerFeedingRule()
    {
        var world = World(false);
        var dog = world.AddCritter(CritterSpecies.Dog, new(15, 15));
        Assert.False(Feed(world, dog));
    }
}
