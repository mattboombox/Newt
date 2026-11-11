using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PopulationCountsTests
{
    private static readonly GridPosition Camp = new(15, 10);

    private static SimulationWorld CreateWorld(bool coastal = false)
    {
        var world = new SimulationWorld(30, 20, coastal ? Terrain.Beach : Terrain.Plains, seed: 17) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        return world;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CondensedTotalIncludesBarbarianChiefWarriorsAndPirates(bool coastal)
    {
        var world = CreateWorld(coastal);
        if (coastal) world.SetTerrain(new(15, 11), Terrain.Shallows);
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        world.AddCritter(CritterSpecies.Ape, new(0, 0));
        world.AddCritter(CritterSpecies.Ape, new(1, 0));
        Assert.Equal(12, PopulationCounts.ApeTotal(world));
        var barbarians = world.GetBarbarianPopulationBySpecies();
        Assert.Equal(10, PopulationCounts.BarbarianApeTotal(barbarians));
        Assert.Equal(1, barbarians[(int)CritterSpecies.ApeChieftain]);
        Assert.Equal(coastal ? 5 : 0, barbarians[(int)CritterSpecies.ApeSailor]);
        var detailedTotal = Enum.GetValues<CritterSpecies>().Where(PopulationCounts.IsApe)
            .Sum(species => PopulationCounts.DetailedSpeciesTotal(world, species, barbarians)) +
            PopulationCounts.BarbarianApeTotal(barbarians);
        Assert.Equal(PopulationCounts.ApeTotal(world), detailedTotal);
    }

    [Fact]
    public void EveryApeRoleAndUndeadVariantContributesOnceAndRemovalUpdatesTotal()
    {
        var world = CreateWorld();
        var apeSpecies = Enum.GetValues<CritterSpecies>()
            .Where(species => species.ToString().Contains("Ape") || species is CritterSpecies.UndeadWarrior).ToArray();
        for (var i = 0; i < apeSpecies.Length; i++)
        {
            world.SetTerrain(new(i, 0), Terrain.Shallows);
            world.AddCritter(apeSpecies[i], new(i, 0));
        }
        world.AddCritter(CritterSpecies.Dog, new(0, 1));
        world.AddCritter(CritterSpecies.Monkey, new(1, 1));
        world.AddCritter(CritterSpecies.Vampire, new(2, 1));
        Assert.Equal(apeSpecies.Length, PopulationCounts.ApeTotal(world));
        world.RemoveCritterAt(new(0, 0));
        Assert.Equal(apeSpecies.Length - 1, PopulationCounts.ApeTotal(world));
    }

    [Fact]
    public void TwoApesAndEightCampDogsAreNotReportedAsTenBarbarianApes()
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        var chiefIndex = Enumerable.Range(0, world.CritterCount)
            .Single(index => world.GetCritter(index).Species is CritterSpecies.ApeChieftain);
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(Camp) < 8; roll++)
            world.TryRecruitDogFromWolfKill(chiefIndex);
        var warriors = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.ApeWarrior).Skip(1).ToArray();
        foreach (var warrior in warriors) world.RemoveCritterAt(warrior.Position);
        var barbarians = world.GetBarbarianPopulationBySpecies();
        Assert.Equal(8, barbarians[(int)CritterSpecies.Dog]);
        Assert.Equal(2, PopulationCounts.ApeTotal(world));
        Assert.Equal(2, PopulationCounts.BarbarianApeTotal(barbarians));
        Assert.Equal(1, PopulationCounts.BarbarianApeTotal(barbarians) - barbarians[(int)CritterSpecies.ApeChieftain]);
        Assert.Equal(8, PopulationCounts.DetailedSpeciesTotal(world, CritterSpecies.Dog, barbarians));
    }
}
