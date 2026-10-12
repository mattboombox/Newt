using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MegaSlugTests
{
    [Fact]
    public void SnailsEvolveIntoMegaSlugs()
    {
        Assert.Equal(1, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.Snail));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(CritterSpecies.Snail, out var evolved));
        Assert.Equal(CritterSpecies.MegaSlug, evolved);
        Assert.Equal(CritterSpecies.MegaSlug,
            CritterEvolution.ChooseOffspring(CritterSpecies.Snail, 0, CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(CritterSpecies.MegaSlug, out var ancestor));
        Assert.Equal(CritterSpecies.Snail, ancestor);
        var world = new SimulationWorld(1, 1, Terrain.Plains) { SeasonsEnabled = false };
        world.SetBiome(new(0, 0), Biome.Forest);
        var id = world.AddCritter(CritterSpecies.Snail, new(0, 0));
        Assert.True(world.TryEvolveCritterAt(new(0, 0)));
        Assert.True(world.TryGetCritter(id, out var slug));
        Assert.Equal(CritterSpecies.MegaSlug, slug.Species);
    }

    [Fact]
    public void MegaSlugsAreSlowAndHaveMoreEnergyThanDeer()
    {
        var slug = CritterNutritions.Get(CritterSpecies.MegaSlug);
        var deer = CritterNutritions.Get(CritterSpecies.Deer);
        Assert.True(slug.InitialEnergy > deer.InitialEnergy);
        Assert.True(slug.MaximumEnergy > deer.MaximumEnergy);
        Assert.Equal(8 * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.MegaSlug));
        Assert.Equal(CritterHabitat.LandDweller, CritterHabitats.GetHabitat(CritterSpecies.MegaSlug));
    }

    [Fact]
    public void EveryDeerPredatorCanHuntMegaSlugs()
    {
        foreach (var predator in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(SimulationWorld.CanEat(predator, CritterSpecies.Deer),
                SimulationWorld.CanEat(predator, CritterSpecies.MegaSlug));
            Assert.Equal(SimulationWorld.CanPursuePreyAtDistance(predator, CritterSpecies.Deer, 3),
                SimulationWorld.CanPursuePreyAtDistance(predator, CritterSpecies.MegaSlug, 3));
        }
    }

    [Theory]
    [InlineData(Biome.Swamp, true)]
    [InlineData(Biome.Jungle, true)]
    [InlineData(Biome.Bog, true)]
    [InlineData(Biome.Forest, true)]
    [InlineData(Biome.Grassland, false)]
    [InlineData(Biome.Desert, false)]
    [InlineData(Biome.Taiga, false)]
    public void MegaSlugFeedsOnlyInItsForagingBiomes(Biome biome, bool feeds)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        world.SetBiome(new(0, 0), biome);
        var id = world.AddCritter(CritterSpecies.MegaSlug, new(0, 0));
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(feeds, feed.Invoke(world, [0]));
        Assert.True(world.TryGetCritter(id, out var slug));
        Assert.Equal(CritterNutritions.Get(CritterSpecies.MegaSlug).InitialEnergy + (feeds ? 1 : 0), slug.Energy);
    }
}
