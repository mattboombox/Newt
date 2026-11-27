using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class NecropolisTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(true, 10)]
    [InlineData(false, 0)]
    [InlineData(false, 10)]
    public void RetreatOnlyEndsWarWhenLichIsAttacking(bool lichAttacking, int reserve)
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Assert.True(lichAttacking ? world.TryStartApeWar(Origin, Village) : world.TryStartApeWar(Village, Origin));
        Earn(world, lich, reserve);
        Move(world, lich, new(45, 12));
        Energy(world, lich, 12);
        Assert.True(MoveMember(world, lich));
        Assert.True(world.IsApeVillageAtWar(Village));
        Move(world, lich, Origin);
        Assert.Equal(lichAttacking || reserve < 10, MoveMember(world, lich));
        world.AdvanceApeWars();
        Assert.Equal(!lichAttacking, world.IsApeVillageAtWar(Village));
        Assert.Equal(!lichAttacking, world.GetLichNecropolis(lich)!.AtWar);
        Assert.Equal(reserve == 10 ? 40 : 12, Snapshot(world, lich).Energy);
        if (!lichAttacking)
        {
            Assert.Single(world.GetActiveApeWars());
            var soldier = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
                .First(c => c.Species == CritterSpecies.ApeWarrior && world.GetApeHomeVillage(c.Id) == Village);
            Assert.True(world.TryGetApeWarSide(soldier.Id, out var attacking));
            Assert.True(attacking);
        }
    }

    [Fact]
    public void AttacksOnLichAtHomeCanDeclareWarOnOffendingVillageWithoutNaturalEvents()
    {
        var world = World(out var oldLich);
        world.RemoveCritterAt(Snapshot(world, oldLich).Position);
        Assert.True(world.TryPlaceNecropolis(Origin));
        var lich = world.GetNecropolis(Origin)!.Lich;
        Assert.True(world.TrySpawnTestApeVillage(Village));
        var attacker = world.AddCritter(CritterSpecies.ApeWarrior, new(Origin.X + 1, Origin.Y + 1));
        Assert.True(world.TryAssignApeToVillage(attacker, Village));
        var hit = typeof(SimulationWorld).GetMethod("ApplyCombatDamage", Private)!;
        for (var i = 0; i < 100 && !world.IsApeVillageAtWar(Village); i++)
        {
            Energy(world, lich, 40);
            hit.Invoke(world, [Index(world, attacker), Index(world, lich)]);
        }
        Assert.True(world.IsApeVillageAtWar(Village));
        Assert.True(world.TryGetApeWarSide(lich, out var attacking));
        Assert.True(attacking);
        Assert.Equal($"Village #{world.GetApeVillageId(Village)}", Assert.Single(world.GetActiveApeWars()).Defender);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void NecropolisRetaliationRequiresHomeTileAndAffiliatedAttacker(bool atHome, bool affiliated)
    {
        var world = World(out var oldLich);
        world.RemoveCritterAt(Snapshot(world, oldLich).Position);
        Assert.True(world.TryPlaceNecropolis(Origin));
        var lich = world.GetNecropolis(Origin)!.Lich;
        Assert.True(world.TrySpawnTestApeVillage(Village));
        var attacker = world.AddCritter(CritterSpecies.ApeWarrior, new(50, 25));
        if (affiliated) Assert.True(world.TryAssignApeToVillage(attacker, Village));
        if (!atHome) Move(world, lich, new(40, 12));
        var declare = typeof(SimulationWorld).GetMethod("TryDeclareNecropolisRetaliationWar", Private)!;
        for (var i = 0; i < 100; i++)
            Assert.False((bool)declare.Invoke(world, [Index(world, attacker), Index(world, lich)])!);
        Assert.Empty(world.GetActiveApeWars());
    }

    [Fact]
    public void EscortsTakeFourDistinctCornersAtHomeAndResumeFollowingWhenLichLeaves()
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains);
        NaturalEvents.SetEnabled(world, false);
        Assert.True(world.TryPlaceNecropolis(Origin));
        var lich = world.GetNecropolis(Origin)!.Lich;
        Earn(world, lich, 40);
        Advance(world);
        var escorts = world.GetLichEscort(lich);
        Assert.Equal(4, escorts.Length);
        for (var step = 0; step < 20; step++)
        foreach (var escort in escorts) Assert.True(MoveMember(world, escort));
        var positions = escorts.Select(id => Snapshot(world, id).Position).ToArray();
        Assert.Equal(4, positions.Distinct().Count());
        Assert.All(positions, position =>
        {
            Assert.Equal(1, Math.Abs(position.X - Origin.X));
            Assert.Equal(1, Math.Abs(position.Y - Origin.Y));
        });
        for (var step = 0; step < 3; step++)
        foreach (var escort in escorts) MoveMember(world, escort);
        Assert.Equal(positions, escorts.Select(id => Snapshot(world, id).Position).ToArray());
        Move(world, lich, new(42, 12));
        for (var step = 0; step < 20; step++)
        foreach (var escort in escorts) MoveMember(world, escort);
        Assert.All(escorts, escort => Assert.True(Snapshot(world, escort).Position.X > Origin.X + 1));
    }

    [Fact]
    public void PlayerPlacementCreatesLichOnBuildingWithExactlyFourGuardsAndNoEscorts()
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains);
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(10, 10);
        Assert.True(world.TryPlaceNecropolis(position));
        var home = world.GetNecropolis(position)!;
        Assert.True(world.TryGetCritterAt(position, out var lich));
        Assert.Equal(CritterSpecies.Lich, lich.Species);
        Assert.Equal(lich.Id, home.Lich);
        Assert.Equal(1, world.NecropolisCount);
        Assert.Equal(5, world.CritterCount);
        Assert.Equal(4, home.Guards);
        Assert.Equal(0, home.Escorts);
        Assert.Equal(0, home.ApeKills);
        Assert.Equal(0, home.StoredKills);
        Assert.Empty(world.GetLichEscort(lich.Id));
        Assert.Equal(4, world.GetLichUndeadWarriorCount(lich.Id));
        for (var i = 0; i < world.CritterCount; i++)
        {
            var critter = world.GetCritter(i);
            if (critter.Species is not CritterSpecies.UndeadWarrior) continue;
            Assert.Equal(1, Math.Max(Math.Abs(critter.Position.X - position.X), Math.Abs(critter.Position.Y - position.Y)));
            Assert.True(MoveMember(world, critter.Id));
            Assert.Equal(critter.Position, Snapshot(world, critter.Id).Position);
        }
        Advance(world);
        Assert.Equal(5, world.CritterCount);
        Assert.Equal(0, world.GetNecropolis(position)!.Escorts);
        Assert.False(world.TryPlaceNecropolis(position));
        Assert.Equal(5, world.CritterCount);
    }

    [Fact]
    public void FailedPlayerPlacementDoesNotCreateAnythingOrReplaceCritters()
    {
        var world = new SimulationWorld(30, 20, Terrain.Mountain);
        var position = new GridPosition(10, 10);
        world.SetTerrain(position, Terrain.Plains);
        for (var x = 9; x <= 11; x++) world.SetTerrain(new(x, 9), Terrain.Plains);
        var existing = world.AddCritter(CritterSpecies.Ape, new(9, 9));
        Assert.False(world.TryPlaceNecropolis(position));
        Assert.Equal(0, world.NecropolisCount);
        Assert.Equal(1, world.CritterCount);
        Assert.True(world.TryGetCritter(existing, out var ape));
        Assert.Equal(CritterSpecies.Ape, ape.Species);
        Assert.False(world.TryPlaceNecropolis(new(-1, 0)));
        Assert.False(world.TryPlaceNecropolis(new(5, 5)));
    }

    [Fact]
    public void PeacefulLichReturnsToExactNecropolisTileAndStaysThere()
    {
        var world = World(out var lich);
        Earn(world, lich, 100);
        Advance(world);
        Move(world, lich, new(40, 12));
        for (var i = 0; i < 20; i++) Assert.True(MoveMember(world, lich));
        Assert.Equal(Origin, Snapshot(world, lich).Position);
        for (var i = 0; i < 5; i++) Assert.True(MoveMember(world, lich));
        Assert.Equal(Origin, Snapshot(world, lich).Position);
        Assert.False(world.GetLichNecropolis(lich)!.Retreating);
    }

    [Fact]
    public void HealthyLichAtWarCanLeaveItsNecropolisAndReturnsWhenWarEnds()
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Assert.True(world.TryStartApeWar(Origin, Village));
        Assert.False(MoveMember(world, lich));
        Move(world, lich, new(40, 12));
        Assert.False(MoveMember(world, lich));
        Assert.True(world.TryStopApeWar(Village));
        Assert.True(MoveMember(world, lich));
        Assert.True(Snapshot(world, lich).Position.X < 40);
    }

    [Fact]
    public void WarRecruitmentStopsAtCapAndKeepsUnspentKillsForReplacingLosses()
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Earn(world, lich, 2000);
        Assert.True(world.TryStartApeWar(Origin, Village));
        Assert.Equal(25, world.GetLichUndeadWarriorCount(lich));
        Assert.Equal(25, world.GetCritterCount(CritterSpecies.UndeadWarrior));
        Assert.Equal(1979, world.GetLichNecropolis(lich)!.StoredKills);
        Advance(world);
        Assert.Equal(1979, world.GetLichNecropolis(lich)!.StoredKills);
        var warrior = world.GetLichEscort(lich)[0];
        world.RemoveCritterAt(Snapshot(world, warrior).Position);
        Assert.Equal(24, world.GetLichUndeadWarriorCount(lich));
        Advance(world);
        Assert.Equal(25, world.GetLichUndeadWarriorCount(lich));
        Assert.Equal(1978, world.GetLichNecropolis(lich)!.StoredKills);
    }

    [Fact]
    public void FullWarriorArmyStillAllowsUnlimitedUndeadApesButTheyCannotRiseAsWarriors()
    {
        var world = World(out var lich);
        CritterId RaiseAt(GridPosition position)
        {
            var ape = world.AddCritter(CritterSpecies.Ape, position);
            Assert.True((bool)typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!
                .Invoke(world, [Index(world, lich), Index(world, ape)])!);
            return ape;
        }
        bool Reanimate(CritterId id) => (bool)typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!
            .Invoke(world, [Index(world, id)])!;
        for (var i = 0; i < 21; i++)
            Assert.True(Reanimate(RaiseAt(new(50 + i % 20, 20 + i / 20))));
        Assert.Equal(25, world.GetLichUndeadWarriorCount(lich));
        for (var i = 0; i < 150; i++) RaiseAt(new(10 + i % 50, 30 + i / 50));
        Assert.Equal(150, world.GetCritterCount(CritterSpecies.UndeadApe));
        var victim = RaiseAt(new(69, 36));
        Energy(world, victim, 0);
        world.AddCritter(CritterSpecies.ApeWarrior, new(68, 36));
        typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
            .Invoke(world, [new GridPosition(68, 36), new GridPosition(69, 36), false]);
        Assert.False(world.TryGetCritter(victim, out _));
        Assert.Equal(150, world.GetCritterCount(CritterSpecies.UndeadApe));
        Assert.Equal(25, world.GetLichUndeadWarriorCount(lich));
        var lostWarrior = world.GetLichEscort(lich)[0];
        world.RemoveCritterAt(Snapshot(world, lostWarrior).Position);
        Assert.Equal(24, world.GetLichUndeadWarriorCount(lich));
        Assert.True(world.TryGetCritterAt(new(10, 30), out var zombie));
        Assert.True(Reanimate(zombie.Id));
        Assert.Equal(25, world.GetLichUndeadWarriorCount(lich));
        var secondLich = world.AddCritter(CritterSpecies.Lich, new(5, 6));
        Assert.Equal(4, world.GetLichUndeadWarriorCount(secondLich));
        Assert.Equal(29, world.GetCritterCount(CritterSpecies.UndeadWarrior));
    }

    [Theory]
    [InlineData(18, true)]
    [InlineData(33, false)]
    public void NaturalNecropolisWarsRespectToggleRangeAndCooldown(int distance, bool nearby)
    {
        var world = World(out var lich);
        var target = new GridPosition(Origin.X + distance, Origin.Y);
        Assert.True(world.TrySpawnTestApeVillage(target));
        Earn(world, lich, 100);
        Advance(world);
        Earn(world, lich, 90);
        typeof(SimulationWorld).GetProperty(nameof(world.Tick))!.SetValue(world,
            (long)SimulationWorld.VillageWarCooldownTicks);
        for (var i = 0; i < 100; i++) world.AdvanceNecropolises();
        Assert.False(world.GetLichNecropolis(lich)!.AtWar);
        NaturalEvents.SetEnabled(world, true);
        for (var i = 0; i < 2000 && !world.GetLichNecropolis(lich)!.AtWar; i++) world.AdvanceNecropolises();
        Assert.Equal(nearby, world.GetLichNecropolis(lich)!.AtWar);
        if (!nearby) return;
        Assert.Equal(69, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.True(world.TryStopApeWar(target));
        for (var i = 0; i < 100; i++) world.AdvanceNecropolises();
        Assert.False(world.GetLichNecropolis(lich)!.AtWar);
    }

    [Fact]
    public void BlockedSpawnsKeepTheirKillsUntilConnectedSpaceBecomesAvailable()
    {
        var world = World(out var lich);
        Earn(world, lich, 100);
        Advance(world);
        for (var y = Origin.Y - 8; y <= Origin.Y + 8; y++)
        for (var x = Origin.X - 8; x <= Origin.X + 8; x++)
            if (!world.IsOccupied(new(x, y))) world.SetTerrain(new(x, y), Terrain.Mountain);
        Earn(world, lich, 10);
        Advance(world);
        Assert.Equal(10, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.Equal(4, world.GetCritterCount(CritterSpecies.UndeadWarrior));
        world.SetTerrain(new(Origin.X + 1, Origin.Y), Terrain.Plains);
        Advance(world);
        Assert.Equal(9, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.Equal(5, world.GetCritterCount(CritterSpecies.UndeadWarrior));
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Origin = new(30, 12), Village = new(12, 12);
    private static SimulationWorld World(out CritterId lich)
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        lich = world.AddCritter(CritterSpecies.Lich, Origin);
        return world;
    }
    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    private static void Earn(SimulationWorld world, CritterId lich, int kills)
    {
        var victimPosition = new GridPosition(60, 25);
        for (var i = 0; i < kills; i++)
        {
            var victim = world.AddCritter(CritterSpecies.Ape, victimPosition);
            Assert.True((bool)typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!
                .Invoke(world, [Index(world, lich), Index(world, victim)])!);
            world.RemoveCritterAt(victimPosition);
        }
    }
    private static void Advance(SimulationWorld world)
    {
        typeof(SimulationWorld).GetProperty(nameof(world.Tick))!.SetValue(world,
            (world.Tick / SimulationWorld.TicksPerSecond + 1) * SimulationWorld.TicksPerSecond);
        world.AdvanceNecropolises();
    }
    private static void Energy(SimulationWorld world, CritterId id, int value) =>
        ((int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!)[Index(world, id)] = value;
    private static void Move(SimulationWorld world, CritterId id, GridPosition position) =>
        typeof(SimulationWorld).GetMethod("MoveCritter", Private)!.Invoke(world,
            [Index(world, id), position.Y * world.Width + position.X, position, false]);
    private static CritterSnapshot Snapshot(SimulationWorld world, CritterId id) => world.GetCritter(Index(world, id));
    private static bool MoveMember(SimulationWorld world, CritterId id) =>
        (bool)typeof(SimulationWorld).GetMethod("TryMoveNecropolisMember", Private)!
            .Invoke(world, [Index(world, id), null, null])!;

    [Fact]
    public void HundredCombinedApeKillsFoundOneNecropolisWithZeroReserveAndFourGuards()
    {
        var world = World(out var lich);
        Earn(world, lich, 99);
        Advance(world);
        Assert.Equal(0, world.NecropolisCount);
        var warrior = world.GetLichEscort(lich)[0];
        var victimPosition = new GridPosition(50, 25);
        var victim = world.AddCritter(CritterSpecies.Ape, victimPosition);
        Energy(world, victim, 0);
        typeof(SimulationWorld).GetMethod("CommitPredation", Private)!.Invoke(world,
            [Snapshot(world, warrior).Position, victimPosition, false]);
        Advance(world);
        var home = Assert.IsType<NecropolisSnapshot>(world.GetLichNecropolis(lich));
        Assert.Equal(100, home.ApeKills);
        Assert.Equal(0, home.StoredKills);
        Assert.Equal(4, home.Guards);
        Assert.Equal(0, home.Escorts);
        Assert.Equal(1, world.NecropolisCount);
        Assert.Empty(world.GetLichEscort(lich));
        Advance(world);
        Assert.Equal(1, world.NecropolisCount);
    }

    [Fact]
    public void NonApeKillsAndUnownedUndeadDoNotFundLich()
    {
        var world = World(out var lich);
        var deer = world.AddCritter(CritterSpecies.Deer, new(50, 25));
        var independent = world.AddCritter(CritterSpecies.UndeadWarrior, new(52, 25));
        var ape = world.AddCritter(CritterSpecies.Ape, new(53, 25));
        var record = typeof(SimulationWorld).GetMethod("RecordLichApeKill", Private)!;
        record.Invoke(world, [Index(world, lich), Index(world, deer)]);
        record.Invoke(world, [Index(world, independent), Index(world, ape)]);
        Assert.Equal(0, world.GetLichApeKills(lich));
    }

    [Fact]
    public void OneKillBuysOneWarriorAndNextFourBecomeEscortsThenReserveAccumulates()
    {
        var world = World(out var lich);
        Earn(world, lich, 100);
        Advance(world);
        Advance(world);
        Assert.Equal(0, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.Empty(world.GetLichEscort(lich));
        Earn(world, lich, 1);
        Advance(world);
        Assert.Single(world.GetLichEscort(lich));
        Assert.Equal(0, world.GetLichNecropolis(lich)!.StoredKills);
        Earn(world, lich, 4);
        Advance(world);
        Assert.Equal(4, world.GetLichEscort(lich).Length);
        Assert.Equal(4, world.GetLichNecropolis(lich)!.Guards);
        Assert.Equal(1, world.GetLichNecropolis(lich)!.StoredKills);
    }

    [Fact]
    public void WarSpendsWholeReserveAndPreservesFourGuardsAndFourEscorts()
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Earn(world, lich, 77);
        Assert.True(world.TryStartApeWar(Origin, Village));
        var home = world.GetLichNecropolis(lich)!;
        Assert.Equal(56, home.StoredKills);
        Assert.Equal(4, home.Guards);
        Assert.Equal(4, home.Escorts);
        Assert.Equal(25, world.GetCritterCount(CritterSpecies.UndeadWarrior));
        Assert.True(home.AtWar);
        Assert.Equal(26, world.GetApeWarStatus(lich)!.Attacking.StartingFighters);
        Assert.True(world.TryStopApeWar(home.Position));
    }

    [Fact]
    public void WoundedLichRetreatsDuringWarAndSpendsTenKillsToFullyHealAtHome()
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Assert.True(world.TryStartApeWar(Origin, Village));
        Earn(world, lich, 10);
        Move(world, lich, new(40, 12));
        Energy(world, lich, 13);
        Assert.True(MoveMember(world, lich));
        Assert.True(world.GetLichNecropolis(lich)!.Retreating);
        Assert.True(Snapshot(world, lich).Position.X < 40);
        Assert.Equal(13, Snapshot(world, lich).Energy);
        Assert.Equal(10, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.True(world.IsApeVillageAtWar(Village));
        Move(world, lich, Origin);
        Assert.True(MoveMember(world, lich));
        Assert.Equal(Snapshot(world, lich).MaximumEnergy, Snapshot(world, lich).Energy);
        Assert.Equal(0, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.False(world.GetLichNecropolis(lich)!.Retreating);
        Assert.False(world.IsApeVillageAtWar(Village));
        Assert.Empty(world.GetActiveApeWars());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void ReturningToNecropolisEndsWarEvenWithoutEnoughKillsToHeal(int reserve)
    {
        var world = World(out var lich);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Earn(world, lich, 100);
        Advance(world);
        Assert.True(world.TryStartApeWar(Origin, Village));
        Earn(world, lich, reserve);
        Move(world, lich, new(40, 12));
        Energy(world, lich, 12);
        Assert.True(MoveMember(world, lich));
        Assert.True(world.IsApeVillageAtWar(Village));
        Move(world, lich, new(Origin.X + 1, Origin.Y + 1));
        Assert.True(MoveMember(world, lich));
        Assert.True(world.GetLichNecropolis(lich)!.Retreating);
        Assert.False(world.GetLichNecropolis(lich)!.AtWar);
        Assert.False(world.IsApeVillageAtWar(Village));
        Assert.Empty(world.GetActiveApeWars());
        Assert.Equal(reserve, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.Equal(12, Snapshot(world, lich).Energy);
    }

    [Fact]
    public void RetreatWaitsForEnoughKillsWithoutSpendingThemOnSoldiers()
    {
        var world = World(out var lich);
        Earn(world, lich, 100);
        Advance(world);
        Earn(world, lich, 9);
        Energy(world, lich, 12);
        Advance(world);
        Assert.Equal(12, Snapshot(world, lich).Energy);
        Assert.Equal(9, world.GetLichNecropolis(lich)!.StoredKills);
        Assert.True(world.GetLichNecropolis(lich)!.Retreating);
        Assert.Equal(4, world.GetCritterCount(CritterSpecies.UndeadWarrior));
        Earn(world, lich, 1);
        Energy(world, lich, 12); // Conversion itself also restores a little energy.
        Advance(world);
        Assert.Equal(40, Snapshot(world, lich).Energy);
        Assert.Equal(0, world.GetLichNecropolis(lich)!.StoredKills);
    }

    [Fact]
    public void GuardsStayAtHomeWhileFourEscortsFollowLich()
    {
        var world = World(out var lich);
        var guard = world.GetLichEscort(lich)[0];
        Earn(world, lich, 100);
        Advance(world);
        Earn(world, lich, 40);
        Advance(world);
        var escort = world.GetLichEscort(lich)[0];
        Move(world, lich, new(42, 12));
        for (var i = 0; i < 5; i++) MoveMember(world, guard);
        Assert.InRange(Math.Abs(Snapshot(world, guard).Position.X - Origin.X), 0, 2);
        var before = Snapshot(world, escort).Position.X;
        Assert.True(MoveMember(world, escort));
        Assert.True(Snapshot(world, escort).Position.X > before);
    }

    [Fact]
    public void LichDeathRemovesNecropolisAndArmyAfterIndexCompaction()
    {
        var world = World(out var lich);
        Earn(world, lich, 100);
        Advance(world);
        Assert.NotNull(world.GetLichNecropolis(lich));
        world.RemoveCritterAt(Snapshot(world, lich).Position);
        Assert.Equal(0, world.NecropolisCount);
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.UndeadWarrior));
        Advance(world);
        Assert.Equal(0, world.NecropolisCount);
    }
}
