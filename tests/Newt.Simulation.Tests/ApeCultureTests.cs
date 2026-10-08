using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeCultureTests
{
    private static readonly GridPosition First = new(15, 15), Second = new(35, 15);
    private static SimulationWorld World()
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains, 17) { ApeVillageMergingEnabled = false, SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        foreach (var p in Positions(world)) world.SetBiome(p, Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(First));
        return world;
    }
    private static IEnumerable<GridPosition> Positions(SimulationWorld w) => Enumerable.Range(0, w.Width * w.Height)
        .Select(i => new GridPosition(i % w.Width, i / w.Width));
    private static CritterSnapshot[] Residents(SimulationWorld w, GridPosition village) =>
        Enumerable.Range(0, w.CritterCount).Select(w.GetCritter).Where(c => w.GetApeHomeVillage(c.Id) == village).ToArray();
    private static int Tile(SimulationWorld w, GridPosition p) => p.Y * w.Width + p.X;
    private static Dictionary<int, int> Homes(SimulationWorld w) =>
        (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!;
    private static void Add(SimulationWorld w, GridPosition village, CritterSpecies species, int count)
    {
        foreach (var p in Positions(w).Where(p => !w.IsOccupied(p) && w.GetApeStructure(p) is null).Take(count).ToArray())
            Homes(w)[w.AddCritter(species, p).Value] = Tile(w, village);
    }
    private static void Convert(SimulationWorld w, GridPosition village) => Assert.True(w.TryAssignApeCulture(village, ApeCulture.Luddite, true));

    [Fact]
    public void ManualConversionCreatesThreePriestsKeepsChiefAndStripsResearch()
    {
        var w = World();
        Add(w, First, CritterSpecies.ApeChieftain, 1);
        var chief = Residents(w, First).Single(c => c.Species == CritterSpecies.ApeChieftain).Id;
        Assert.Contains(Positions(w), p => w.TryPlaceApeBuilding(p, ApeStructureKind.Library));
        Assert.True(w.TryResearchApeTechnology(Tile(w, First), ApeTechnology.Aquaculture));
        Assert.False(w.TryAssignApeCulture(First, ApeCulture.Luddite));
        var before = w.CritterCount;
        Convert(w, First);
        Assert.Equal(before + 3, w.CritterCount);
        Assert.Equal(ApeCulture.Luddite, w.GetApeVillageCulture(First));
        Assert.Equal(3, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
        Assert.True(w.TryGetCritter(chief, out var retained));
        Assert.Equal(CritterSpecies.ApeChieftain, retained.Species);
        Assert.Single(Positions(w), p => w.GetApeStructure(p) == ApeStructureKind.LudditeTemple);
        Assert.DoesNotContain(Positions(w), p => w.GetApeStructure(p) == ApeStructureKind.Library);
        Assert.Empty(w.GetApeVillageTechnologies(First));
        Assert.False(w.TryResearchApeTechnology(Tile(w, First), ApeTechnology.Aquaculture));
        Assert.False(w.TryPlaceApeBuilding(First, ApeStructureKind.Library));
        Assert.False(w.TryBuildApeStructure(Tile(w, First), ApeStructureKind.Library));
        Assert.False(w.TryAssignApeCulture(First, ApeCulture.Luddite, true));
    }

    [Fact]
    public void NaturalCultureRequiresTwoHundredResidentsEnabledEventsAndMinuteBoundary()
    {
        var w = World();
        typeof(SimulationWorld).GetProperty(nameof(w.Tick))!.SetValue(w, (long)SimulationWorld.ApeCultureCheckIntervalTicks);
        NaturalEvents.SetEnabled(w, true);
        for (var i = 0; i < 500; i++) Assert.False(w.TryDevelopApeCulture(Tile(w, First)));
        Add(w, First, CritterSpecies.Ape, 200 - Residents(w, First).Length);
        NaturalEvents.SetEnabled(w, false);
        for (var i = 0; i < 500; i++) Assert.False(w.TryDevelopApeCulture(Tile(w, First)));
        NaturalEvents.SetEnabled(w, true);
        typeof(SimulationWorld).GetProperty(nameof(w.Tick))!.SetValue(w, (long)SimulationWorld.ApeCultureCheckIntervalTicks + 1);
        for (var i = 0; i < 500; i++) Assert.False(w.TryDevelopApeCulture(Tile(w, First)));
        typeof(SimulationWorld).GetProperty(nameof(w.Tick))!.SetValue(w, (long)SimulationWorld.ApeCultureCheckIntervalTicks);
        var converted = false;
        for (var i = 0; i < 2000 && !converted; i++) converted = w.TryDevelopApeCulture(Tile(w, First));
        Assert.True(converted);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    public void WarBombersRoundUpAndRestoreWarriorsOnPeace(int count, int bombers)
    {
        var w = World();
        Assert.True(w.TrySpawnTestApeVillage(Second));
        foreach (var c in Residents(w, First)) w.RemoveCritterAt(c.Position);
        Add(w, First, CritterSpecies.ApeWarrior, count);
        Convert(w, First);
        Assert.True(w.TryStartApeWar(First, Second));
        Assert.Equal(bombers, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeBomber));
        Assert.All(Residents(w, First).Where(c => c.Species == CritterSpecies.ApeWarrior), c => Assert.True(w.UsesLudditeWarriorAppearance(c.Id)));
        Assert.True(w.TryStopApeWar(First));
        Assert.DoesNotContain(Residents(w, First), c => c.Species == CritterSpecies.ApeBomber);
        Assert.Equal(count, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeWarrior));
        Assert.Equal(3, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CivilWarCultureFollowsBlueDefendersOutcome(bool blueWins)
    {
        var w = World();
        Add(w, First, CritterSpecies.ApeWarrior, 10);
        Convert(w, First);
        Assert.True(w.TryStartApeCivilWar(First));
        var fighters = Residents(w, First).Where(c => w.TryGetApeWarSide(c.Id, out _)).ToArray();
        Assert.DoesNotContain(fighters, c => c.Species == CritterSpecies.ApeLudditePriest);
        var blue = fighters.Where(c => w.TryGetApeWarSide(c.Id, out var red) && !red).ToArray();
        var blueWarriors = blue.Count(c => c.Species is CritterSpecies.ApeWarrior or CritterSpecies.ApeBomber);
        Assert.Equal((blueWarriors + 3) / 4, blue.Count(c => c.Species == CritterSpecies.ApeBomber));
        Assert.All(fighters.Where(c => c.Species == CritterSpecies.ApeWarrior), c =>
        {
            w.TryGetApeWarSide(c.Id, out var red);
            Assert.Equal(!red, w.UsesLudditeWarriorAppearance(c.Id));
        });
        foreach (var c in fighters.Where(c => { w.TryGetApeWarSide(c.Id, out var red); return red == blueWins; })) w.RemoveCritterAt(c.Position);
        w.AdvanceApeWars();
        Assert.False(w.IsApeVillageAtWar(First));
        Assert.Equal(blueWins ? ApeCulture.Luddite : (ApeCulture?)null, w.GetApeVillageCulture(First));
        Assert.Equal(blueWins ? 3 : 0, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
        Assert.Equal(blueWins ? 1 : 0, Positions(w).Count(p => w.GetApeStructure(p) == ApeStructureKind.LudditeTemple));
        if (!blueWins) Assert.True(w.TryResearchApeTechnology(Tile(w, First), ApeTechnology.Aquaculture));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.Therapsid)]
    public void BombersReflectActualDamageAndDieImmediatelyWithoutFeeding(CritterSpecies prey)
    {
        var hits = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var w = new SimulationWorld(2, 1, Terrain.Plains, seed);
            var bomber = w.AddCritter(CritterSpecies.ApeBomber, new(0, 0));
            var victim = w.AddCritter(prey, new(1, 0));
            var initial = w.GetCritter(1).Energy;
            Assert.Equal(20, w.GetCritterCombatDamage(bomber));
            typeof(SimulationWorld).GetMethod("CommitMutualPredatorCombat", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(w, [new GridPosition(0, 0), new GridPosition(1, 0)]);
            if (!w.TryGetCritter(victim, out var after) || after.Energy < initial)
            {
                hits++;
                var dealt = Math.Min(20, initial);
                Assert.Equal(initial - dealt, w.TryGetCritter(victim, out after) ? after.Energy : 0);
                if (dealt == 20) Assert.False(w.TryGetCritter(bomber, out _));
                else { Assert.True(w.TryGetCritter(bomber, out var survivor)); Assert.Equal(20 - dealt, survivor.Energy); }
            }
        }
        Assert.True(hits > 0);
    }

    [Fact]
    public void TempleRecruitsReplacementPriestsWithoutDuplicates()
    {
        var w = World(); Convert(w, First);
        var priest = Residents(w, First).First(c => c.Species == CritterSpecies.ApeLudditePriest);
        w.RemoveCritterAt(priest.Position);
        for (var i = 0; i < 2 * SimulationWorld.TicksPerSecond; i++) w.AdvanceOneTick();
        Assert.Equal(3, Residents(w, First).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
    }

    [Fact]
    public void BombersAttackEnemyVillagersThroughRegularWarCombat()
    {
        var w = World(); Assert.True(w.TrySpawnTestApeVillage(Second));
        Add(w, First, CritterSpecies.ApeWarrior, 1); Convert(w, First);
        Assert.True(w.TryStartApeWar(First, Second, true));
        var bomber = Residents(w, First).Single(c => c.Species == CritterSpecies.ApeBomber);
        var victim = Residents(w, Second).First(c => c.Species == CritterSpecies.ApeWarrior);
        var hit = false;
        for (var i = 0; i < 50 && w.TryGetCritter(bomber.Id, out _) && w.TryGetCritter(victim.Id, out _); i++)
        {
            w.CommitEncounter(bomber.Position, victim.Position);
            if (!w.TryGetCritter(victim.Id, out _)) hit = true;
        }
        Assert.True(hit);
        Assert.False(w.TryGetCritter(bomber.Id, out _));
    }

    [Fact]
    public void LudditeVillageMobilizesBombersAgainstLich()
    {
        var w = World(); Add(w, First, CritterSpecies.ApeWarrior, 4); Convert(w, First);
        w.AddCritter(CritterSpecies.Lich, Second);
        Assert.True(w.TryStartApeWar(Second, First));
        Assert.Contains(Residents(w, First), c => c.Species == CritterSpecies.ApeBomber);
        Assert.True(w.TryStopApeWar(First));
        Assert.DoesNotContain(Residents(w, First), c => c.Species == CritterSpecies.ApeBomber);
    }

    [Fact]
    public void PeacefulMergeKeepsOnlyCultureAndRemovesIncomingTechnologyAndScholars()
    {
        var w = World(); Assert.True(w.TrySpawnTestApeVillage(Second));
        Add(w, First, CritterSpecies.ApeScholar, 2);
        Assert.True(w.TryResearchApeTechnology(Tile(w, First), ApeTechnology.Aquaculture));
        Convert(w, Second);
        for (var i = 0; i < 1500 && w.ApeVillageCount > 1; i++)
        {
            Assert.True(w.TryBuildApeStructure(Tile(w, First), ApeStructureKind.ResidentialDistrict));
            w.ApeVillageMergingEnabled = true; w.MergeAdjacentApeVillages();
        }
        Assert.Equal(1, w.ApeVillageCount);
        Assert.Equal(Second, w.GetApeStructureVillage(First));
        Assert.Equal(ApeCulture.Luddite, w.GetApeVillageCulture(Second));
        Assert.Empty(w.GetApeVillageTechnologies(Second));
        Assert.DoesNotContain(Residents(w, Second), c => c.Species == CritterSpecies.ApeScholar);
        Assert.Equal(3, Residents(w, Second).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
    }

    [Fact]
    public void TempleDestructionDemotesPriestsAndReleasesTechnologyRestrictions()
    {
        var w = World(); Convert(w, First);
        var temple = Positions(w).Single(p => w.GetApeStructure(p) == ApeStructureKind.LudditeTemple);
        Assert.True(w.RemoveApeStructureAt(temple));
        Assert.Null(w.GetApeVillageCulture(First));
        Assert.DoesNotContain(Residents(w, First), c => c.Species == CritterSpecies.ApeLudditePriest);
        Assert.Contains(Positions(w), p => w.TryPlaceApeBuilding(p, ApeStructureKind.Library));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TouchingCulturesFightAndWinnerAbsorbsVillage(bool attackerWins)
    {
        var w = World(); Assert.True(w.TrySpawnTestApeVillage(Second));
        Add(w, First, CritterSpecies.ApeWarrior, 5); Add(w, Second, CritterSpecies.ApeWarrior, 5);
        Convert(w, First); Convert(w, Second);
        for (var i = 0; i < 1500; i++)
        {
            w.TryBuildApeStructure(Tile(w, First), ApeStructureKind.ResidentialDistrict);
            // Stop once the merge scan starts a war.
            w.ApeVillageMergingEnabled = true; w.MergeAdjacentApeVillages();
            if (w.IsApeVillageAtWar(First)) break;
        }
        Assert.True(w.IsApeVillageAtWar(First)); Assert.Equal(2, w.ApeVillageCount);
        var war = ((System.Collections.IDictionary)typeof(SimulationWorld).GetField("_apeWars", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(w)!)[Tile(w, First)]!;
        Assert.True((bool)war.GetType().GetField("BloodWar")!.GetValue(war)!);
        var attacker = (int)war.GetType().GetField("Attacker")!.GetValue(war)!;
        var winner = (Tile(w, First) == attacker) == attackerWins ? First : Second;
        var loser = winner == First ? Second : First;
        foreach (var c in Residents(w, loser)) w.RemoveCritterAt(c.Position);
        w.AdvanceApeWars();
        Assert.Equal(1, w.ApeVillageCount);
        Assert.Equal(winner, w.GetApeStructureVillage(loser));
        Assert.Equal(ApeCulture.Luddite, w.GetApeVillageCulture(winner));
        Assert.Single(Positions(w), p => w.GetApeStructure(p) == ApeStructureKind.LudditeTemple);
        Assert.Equal(3, Residents(w, winner).Count(c => c.Species == CritterSpecies.ApeLudditePriest));
    }
}
