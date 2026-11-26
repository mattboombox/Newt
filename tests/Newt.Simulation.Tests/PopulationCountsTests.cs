using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PopulationCountsTests
{
    [Fact]
    public void LandAndSailorTradersShareCountsAndCameraSelection()
    {
        var world = CreateWorld();
        var land = world.AddCritter(CritterSpecies.ApeTrader, new(0, 0));
        var water = new GridPosition(1, 0);
        world.SetTerrain(water, Terrain.Ocean);
        var sailor = world.AddCritter(CritterSpecies.ApeTraderSailor, water);
        var barbarians = world.GetBarbarianPopulationBySpecies();
        Assert.Equal(2, PopulationCounts.DetailedSpeciesTotal(world, CritterSpecies.ApeTrader, barbarians));
        Assert.Equal(0, PopulationCounts.DetailedSpeciesTotal(world, CritterSpecies.ApeTraderSailor, barbarians));
        Assert.Equal(sailor, PopulationCounts.FindCritter(world, CritterSpecies.ApeTrader, previous: land)!.Value.Id);
        Assert.Equal(land, PopulationCounts.FindCritter(world, CritterSpecies.ApeTrader, previous: sailor)!.Value.Id);
        var village = new VillagePopulationSnapshot(1, new(5, 5), false, null, 5,
            new Dictionary<CritterSpecies, int> { [CritterSpecies.ApeTrader] = 2, [CritterSpecies.ApeTraderSailor] = 3 });
        var rows = PopulationCounts.VillageRows(village);
        Assert.Equal(5, rows[CritterSpecies.ApeTrader]);
        Assert.False(rows.ContainsKey(CritterSpecies.ApeTraderSailor));
        world.RemoveCritterAt(water);
        Assert.Equal(1, PopulationCounts.DetailedSpeciesTotal(world, CritterSpecies.ApeTrader, world.GetBarbarianPopulationBySpecies()));
    }

    [Fact]
    public void VillageRowsKeepPiratesGroupedWithBarbariansOnLandAndWater()
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        var warrior = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .First(critter => critter.Species == CritterSpecies.ApeWarrior);
        world.RemoveCritterAt(warrior.Position);
        var position = new GridPosition(0, 0);
        world.SetTerrain(position, Terrain.Beach);
        var sailor = world.AddCritter(CritterSpecies.ApeSailor, position);
        Assert.True(world.TryAssignApeToVillage(sailor, Camp));
        foreach (var terrain in new[] { Terrain.Beach, Terrain.Ocean, Terrain.DeepOcean, Terrain.Beach })
        {
            world.SetTerrain(position, terrain);
            var village = Assert.Single(world.GetVillagePopulations());
            var rows = PopulationCounts.VillageRows(village);
            Assert.Equal(9, rows[CritterSpecies.ApeWarrior]);
            Assert.Equal(1, rows[CritterSpecies.ApeChieftain]);
            Assert.False(rows.ContainsKey(CritterSpecies.ApeSailor));
            Assert.Equal(village.ApeCount, rows.Values.Sum());
        }
    }

    [Fact]
    public void ColonistsHaveTheirOwnDetailedCountAndSelectionWithoutDoubleCounting()
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        var civilian = world.AddCritter(CritterSpecies.Ape, new(0, 0));
        ColonistTestFood.Fund(world, Camp);
        Assert.True(world.TrySendApeColonist(Camp));
        ColonistTestFood.Fund(world, Camp);
        Assert.True(world.TrySendApeColonist(Camp));
        var barbarians = world.GetBarbarianPopulationBySpecies();
        Assert.Equal(2, world.ApeColonistCount);
        Assert.Equal(1, PopulationCounts.DetailedSpeciesTotal(world, CritterSpecies.Ape, barbarians));
        Assert.Equal(civilian, PopulationCounts.FindCritter(world, CritterSpecies.Ape)!.Value.Id);
        var first = PopulationCounts.FindCritter(world, CritterSpecies.Ape, PopulationCounts.Selection.Colonists)!.Value;
        Assert.True(first.IsColonist);
        var next = PopulationCounts.FindCritter(world, CritterSpecies.Ape, PopulationCounts.Selection.Colonists,
            previous: first.Id)!.Value;
        Assert.True(next.IsColonist);
        Assert.NotEqual(first.Id, next.Id);
        var detailedTotal = Enum.GetValues<CritterSpecies>().Where(PopulationCounts.IsApe)
            .Sum(species => PopulationCounts.DetailedSpeciesTotal(world, species, barbarians)) +
            PopulationCounts.BarbarianApeTotal(barbarians) + world.ApeColonistCount;
        Assert.Equal(PopulationCounts.ApeTotal(world), detailedTotal);
        world.RemoveCritterAt(first.Position);
        Assert.Equal(1, world.ApeColonistCount);
        world.RemoveCritterAt(next.Position);
        Assert.Equal(0, world.ApeColonistCount);
        Assert.Null(PopulationCounts.FindCritter(world, CritterSpecies.Ape, PopulationCounts.Selection.Colonists));
    }

    [Fact]
    public void RepeatedSelectionsAvoidPreviousCritterUnlessItIsTheOnlyMatch()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        var first = world.AddCritter(CritterSpecies.Ape, new(1, 1));
        var second = world.AddCritter(CritterSpecies.Ape, new(2, 2));
        var previous = first;
        for (var click = 0; click < 20; click++)
        {
            var next = PopulationCounts.FindCritter(world, CritterSpecies.Ape, previous: previous)!.Value;
            Assert.NotEqual(previous, next.Id);
            previous = next.Id;
        }
        world.RemoveCritterAt(new(2, 2));
        Assert.Equal(first, PopulationCounts.FindCritter(world, CritterSpecies.Ape, previous: first)!.Value.Id);
        Assert.Equal(first, PopulationCounts.FindCritter(world, CritterSpecies.Ape, previous: second)!.Value.Id);
    }

    [Fact]
    public void RepeatedVillageSelectionsStayInVillageAndKeepExactRolePriority()
    {
        var world = new SimulationWorld(60, 40, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(30, 20);
        Assert.True(world.TrySpawnBarbarianApeVillage(village));
        var first = PopulationCounts.FindCritter(world, CritterSpecies.ApeWarrior, village: village)!.Value;
        var next = PopulationCounts.FindCritter(world, CritterSpecies.ApeWarrior, village: village, previous: first.Id)!.Value;
        Assert.NotEqual(first.Id, next.Id);
        Assert.Equal(village, world.GetApeHomeVillage(next.Id));
        Assert.Equal(CritterSpecies.ApeWarrior, next.Species);
        var chief = PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain, village: village)!.Value;
        Assert.Equal(chief.Id, PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain,
            village: village, previous: chief.Id)!.Value.Id);
    }

    [Fact]
    public void VillageSelectionFindsItsOwnChiefAndFallsBackOnlyWithinThatVillage()
    {
        var world = new SimulationWorld(60, 40, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var first = new GridPosition(10, 10);
        var second = new GridPosition(40, 25);
        Assert.True(world.TrySpawnBarbarianApeVillage(first));
        Assert.True(world.TrySpawnBarbarianApeVillage(second));
        var chief = PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain, village: second)!.Value;
        Assert.Equal(CritterSpecies.ApeChieftain, chief.Species);
        Assert.Equal(second, world.GetApeHomeVillage(chief.Id));
        world.RemoveCritterAt(chief.Position);
        var fallback = PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain, village: second)!.Value;
        Assert.Equal(second, world.GetApeHomeVillage(fallback.Id));
        Assert.NotEqual(CritterSpecies.ApeChieftain, fallback.Species);
        Assert.Null(PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain, village: new(0, 0)));
    }

    [Fact]
    public void GlobalSelectionRespectsGroupedRowsAndMissingSpecies()
    {
        var world = new SimulationWorld(60, 40, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnBarbarianApeVillage(new(30, 20)));
        var civilian = world.AddCritter(CritterSpecies.ApeWarrior, new(0, 0));
        Assert.Equal(civilian, PopulationCounts.FindCritter(world, CritterSpecies.ApeWarrior)!.Value.Id);
        var barbarian = PopulationCounts.FindCritter(world, CritterSpecies.ApeWarrior, PopulationCounts.Selection.Barbarians)!.Value;
        Assert.True(world.IsBarbarianApe(barbarian.Id));
        Assert.NotEqual(CritterSpecies.ApeChieftain, barbarian.Species);
        Assert.Equal(CritterSpecies.ApeChieftain, PopulationCounts.FindCritter(world, CritterSpecies.ApeChieftain,
            PopulationCounts.Selection.BarbarianChiefs)!.Value.Species);
        Assert.NotNull(PopulationCounts.FindCritter(world, CritterSpecies.Ape, PopulationCounts.Selection.CondensedApes));
        Assert.Null(PopulationCounts.FindCritter(world, CritterSpecies.Fish));
        Assert.Null(PopulationCounts.FindCritter(world, CritterSpecies.Ape, PopulationCounts.Selection.SickApes));
    }

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
    public void CondensedChartListsUndeadSeparatelyWithoutCountingThemAsLivingApes()
    {
        var world = CreateWorld();
        world.AddCritter(CritterSpecies.Ape, new(0, 0));
        world.AddCritter(CritterSpecies.ApeWarrior, new(1, 0));
        world.AddCritter(CritterSpecies.UndeadApe, new(2, 0));
        world.AddCritter(CritterSpecies.UndeadWarrior, new(3, 0));
        Assert.False(PopulationCounts.IsCondensedApe(CritterSpecies.UndeadApe));
        Assert.False(PopulationCounts.IsCondensedApe(CritterSpecies.UndeadWarrior));
        Assert.Equal(2, PopulationCounts.CondensedApeTotal(world));
        var separateRows = Enum.GetValues<CritterSpecies>().Where(species => !PopulationCounts.IsCondensedApe(species))
            .Sum(world.GetCritterCount);
        Assert.Equal(world.CritterCount, PopulationCounts.CondensedApeTotal(world) + separateRows);
        world.RemoveCritterAt(new(0, 0));
        Assert.Equal(1, PopulationCounts.CondensedApeTotal(world));
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
