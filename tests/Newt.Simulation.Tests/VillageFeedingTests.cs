using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillageFeedingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Village = new(10, 10);
    private static SimulationWorld Create(bool camp)
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, 17) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(camp ? world.TrySpawnBarbarianApeVillage(Village) : world.TrySpawnTestApeVillage(Village));
        if (!camp)
        {
            var farm = new GridPosition(11, 10);
            world.RemoveCritterAt(farm);
            Assert.True(world.TryPlaceApeBuilding(farm, ApeStructureKind.Farm));
        }
        return world;
    }
    private static int[] Energies(SimulationWorld world) =>
        (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
    private static void Energy(SimulationWorld world, int value)
    {
        for (var i = 0; i < world.CritterCount; i++) Energies(world)[i] = value;
    }
    private static void Feed(SimulationWorld world) => world.FeedVillageForGrowth(Village.Y * world.Width + Village.X);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoFeedingAtHalfButFullMealCanCrossBelowHalf(bool camp)
    {
        var world = Create(camp);
        Energy(world, 10);
        var half = world.GetApeVillageFoodCapacity(Village) / 2;
        world.StoreApeVillageFood(Village, half);
        Feed(world);
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), ape => Assert.Equal(10, ape.Energy));
        Assert.True(world.ShouldApeHunt(world.GetCritter(0).Id));
        world.StoreApeVillageFood(Village, 1);
        Assert.False(world.ShouldApeHunt(world.GetCritter(0).Id));
        Feed(world);
        Assert.Single(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), ape => ape.Energy == 12);
        Assert.Equal(half - 1, world.GetApeVillageFood(Village));
    }

    [Fact]
    public void RepeatsFullMealsUntilFoodFallsBelowHalf()
    {
        var world = Create(true);
        Energy(world, 6);
        world.StoreApeVillageFood(Village, 50);
        Feed(world);
        var apes = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        Assert.Equal(5, apes.Count(ape => ape.Energy == 12));
        Assert.Equal(5, apes.Count(ape => ape.Energy == 6));
        Assert.Equal(20, world.GetApeVillageFood(Village));
    }

    [Fact]
    public void GrowthFeedingRunsAutomaticallyWithNaturalEventsDisabled()
    {
        var world = Create(true);
        while (world.CritterCount > 2) world.RemoveCritterAt(world.GetCritter(world.CritterCount - 1).Position);
        Energy(world, 10);
        world.StoreApeVillageFood(Village, 26);
        world.AdvanceOneTick();
        Assert.Equal(24, world.GetApeVillageFood(Village));
        Assert.Equal(3, world.GetApeVillageResidentCount(Village));
    }

    [Fact]
    public void DogsAtPopulationCapDoNotConsumeGrowthMeals()
    {
        var world = Create(false);
        Energy(world, 12);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, new(30, 20)));
        Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, new(31, 20)));
        var before = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(critter => critter.Species == CritterSpecies.Dog).ToArray();
        Assert.Equal(world.GetApeVillageDogCapacity(Village), before.Length);
        world.StoreApeVillageFood(Village, 15);
        Feed(world);
        Assert.Equal(15, world.GetApeVillageFood(Village));
        foreach (var dog in before)
        {
            Assert.True(world.TryGetCritter(dog.Id, out var after));
            Assert.Equal(dog.Energy, after.Energy);
        }
    }

    [Fact]
    public void SavesFoodIfNoCompleteMealIsAffordable()
    {
        var world = Create(false);
        Energy(world, 0);
        world.StoreApeVillageFood(Village, 8);
        Feed(world);
        Assert.Equal(8, world.GetApeVillageFood(Village));
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), ape => Assert.Equal(0, ape.Energy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DogsGetCompleteMealsAndReproduceWithoutNaturalEvents(bool camp)
    {
        var world = Create(camp);
        Energy(world, 12);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, new(30, 20)));
        var dog = world.GetCritter(world.CritterCount - 1);
        Energies(world)[world.CritterCount - 1] = dog.MaximumEnergy - 2;
        var start = world.GetApeVillageFoodCapacity(Village) / 2 + 1;
        world.StoreApeVillageFood(Village, start);
        Feed(world);
        Assert.True(world.TryGetCritter(dog.Id, out var fed));
        Assert.Equal(dog.MaximumEnergy, fed.Energy);
        Assert.Equal(start - 2, world.GetApeVillageFood(Village));
        typeof(SimulationWorld).GetMethod("AdvanceCritterLifecycles", Private)!.Invoke(world, null);
        Assert.Equal(2, world.GetApeVillageDogCount(Village));
    }

    [Fact]
    public void SickColonistOutsiderAndNonBreedingRolesAreNotGrowthFed()
    {
        var world = Create(false);
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var sick = world.AddCritter(CritterSpecies.Ape, new(10, 10));
        var scholar = world.AddCritter(CritterSpecies.ApeScholar, new(12, 10));
        var colonist = world.AddCritter(CritterSpecies.Ape, new(13, 10));
        foreach (var id in new[] { sick, scholar, colonist }) Assert.True(world.TryAssignApeToVillage(id, Village));
        world.AddCritter(CritterSpecies.Ape, new(30, 20));
        Assert.True(world.TryInfectApeAt(new(10, 10), PlagueKind.Plague));
        var routes = (Dictionary<int, (int, int)>)typeof(SimulationWorld).GetField("_apeSettlerTargets", Private)!.GetValue(world)!;
        routes[colonist.Value] = (410, 800);
        Energy(world, 6);
        world.StoreApeVillageFood(Village, 15);
        Feed(world);
        Assert.Equal(15, world.GetApeVillageFood(Village));
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), ape => Assert.Equal(6, ape.Energy));
    }
}
