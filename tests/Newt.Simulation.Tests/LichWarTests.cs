using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LichWarTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Village = new(10, 10), LichTile = new(35, 10);
    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    private static SimulationWorld CreateWorld(bool barbarian)
    {
        var world = new SimulationWorld(70, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(barbarian ? world.TrySpawnBarbarianApeVillage(Village) : world.TrySpawnTestApeVillage(Village));
        world.AddCritter(CritterSpecies.Lich, LichTile);
        return world;
    }
    private static CritterId Raise(SimulationWorld world, CritterId lich, GridPosition position)
    {
        var ape = world.AddCritter(CritterSpecies.Ape, position);
        typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!.Invoke(world, [Index(world, lich), Index(world, ape)]);
        typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!.Invoke(world, [Index(world, ape)]);
        return ape;
    }

    [Theory]
    [InlineData(CritterSpecies.UndeadApe)]
    [InlineData(CritterSpecies.UndeadWarrior)]
    [InlineData(CritterSpecies.Lich)]
    public void UndeadWithEnemyVillageHomeNeverAttackLich(CritterSpecies species)
    {
        var world = CreateWorld(false);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        var position = new GridPosition(LichTile.X + 1, LichTile.Y);
        var undead = world.AddCritter(species, position);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        homes[undead.Value] = Village.Y * world.Width + Village.X;
        Assert.True(world.TryStartApeWar(LichTile, Village));
        var enemies = typeof(SimulationWorld).GetMethod("AreLichWarEnemies", Private)!;
        Assert.Equal(false, enemies.Invoke(world, [Index(world, lich.Id), Index(world, undead)]));
        Assert.Equal(false, enemies.Invoke(world, [Index(world, undead), Index(world, lich.Id)]));
        var lichEnergy = world.GetCritter(Index(world, lich.Id)).Energy;
        var undeadEnergy = world.GetCritter(Index(world, undead)).Energy;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            world.CommitEncounter(LichTile, position);
            world.CommitEncounter(position, LichTile);
        }
        Assert.True(world.TryGetCritter(lich.Id, out var survivingLich));
        Assert.True(world.TryGetCritter(undead, out var survivingUndead));
        Assert.Equal(lichEnergy, survivingLich.Energy);
        Assert.Equal(undeadEnergy, survivingUndead.Energy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AttackedVillageCanDeclareWarOnLichWithoutPeriodicChecks(bool barbarian)
    {
        var world = CreateWorld(barbarian);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        var victimPosition = new GridPosition(LichTile.X + 1, LichTile.Y);
        var victim = world.AddCritter(CritterSpecies.Ape, victimPosition);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        homes[victim.Value] = Village.Y * world.Width + Village.X;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        for (var attempt = 0; attempt < 100 && !world.IsApeVillageAtWar(Village); attempt++)
        {
            // Isolate declaration from deaths in repeated combat exchanges.
            energy[Index(world, lich.Id)] = 100;
            energy[Index(world, victim)] = 100;
            world.CommitEncounter(LichTile, victimPosition);
        }
        Assert.True(world.IsApeVillageAtWar(Village));
        Assert.True(world.TryGetApeWarSide(lich.Id, out var lichAttacking));
        Assert.False(lichAttacking);
        Assert.True(world.TryGetApeWarSide(victim, out var villageAttacking));
        Assert.True(villageAttacking);
        var declare = typeof(SimulationWorld).GetMethod("TryDeclareWarOnAttackingLich", Private)!;
        Assert.Equal(false, declare.Invoke(world, [Index(world, lich.Id), Index(world, victim)]));
    }

    [Fact]
    public void LichAttackingUnaffiliatedApeDoesNotDeclareWar()
    {
        var world = CreateWorld(false);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        var wild = world.AddCritter(CritterSpecies.Ape, new(LichTile.X + 1, LichTile.Y));
        var declare = typeof(SimulationWorld).GetMethod("TryDeclareWarOnAttackingLich", Private)!;
        for (var attempt = 0; attempt < 100; attempt++)
            Assert.Equal(false, declare.Invoke(world, [Index(world, lich.Id), Index(world, wild)]));
        Assert.False(world.IsApeVillageAtWar(Village));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void WarWorksInBothDirectionsAgainstVillagesAndCamps(bool barbarian, bool lichAttacking)
    {
        var world = CreateWorld(barbarian);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(Village) < world.GetApeVillageDogCapacity(Village); roll++)
            world.TryRecruitDogFromWolfKill(0);
        Assert.True(world.GetApeVillageDogCount(Village) >= 2);
        var army = Enumerable.Range(0, 22).Select(n => Raise(world, lich.Id, new(35 + n, 20))).ToArray();
        Assert.True(world.TryStartApeWar(lichAttacking ? LichTile : Village, lichAttacking ? Village : LichTile));
        Assert.True(world.IsApeVillageAtWar(Village));
        Assert.True(world.TryGetApeWarSide(lich.Id, out var side));
        Assert.Equal(lichAttacking, side);
        var warrior = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .First(critter => world.GetApeHomeVillage(critter.Id) == Village && critter.Species is CritterSpecies.ApeWarrior);
        Assert.True(world.TryGetApeWarSide(warrior.Id, out side));
        Assert.Equal(!lichAttacking, side);
        var enemies = typeof(SimulationWorld).GetMethod("AreWarEnemies", Private)!;
        Assert.All(army.Append(lich.Id), id =>
        {
            Assert.True((bool)enemies.Invoke(world, [Index(world, warrior.Id), Index(world, id)])!);
            Assert.True((bool)enemies.Invoke(world, [Index(world, id), Index(world, warrior.Id)])!);
        });
        var campMembers = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(critter => world.GetApeHomeVillage(critter.Id) == Village).ToArray();
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", Private)!;
        Assert.All(campMembers, member =>
        {
            Assert.True((bool)canAttack.Invoke(world, [Index(world, lich.Id), Index(world, member.Id)])!);
            Assert.True((bool)canAttack.Invoke(world, [Index(world, member.Id), Index(world, army[^1])])!);
        });
        // All soldiers participate, including those outside the twenty-warrior escort.
        var soldier = army[^1];
        var before = world.GetCritter(Index(world, soldier)).Position;
        typeof(SimulationWorld).GetMethod("TryMoveWarApe", Private)!.Invoke(world, [Index(world, soldier), null]);
        var after = world.GetCritter(Index(world, soldier)).Position;
        int Distance(GridPosition point) => Math.Min(Math.Abs(point.X - Village.X),
            world.Width - Math.Abs(point.X - Village.X)) + Math.Abs(point.Y - Village.Y);
        Assert.True(Distance(after) < Distance(before));
        world.AdvanceApeWars();
        Assert.True(world.IsApeVillageAtWar(Village));
        Assert.True(world.TryStopApeWar(lichAttacking ? Village : LichTile));
        Assert.False(world.IsApeVillageAtWar(Village));
        Assert.False(world.TryGetApeWarSide(lich.Id, out _));
    }

    [Fact]
    public void LichesCannotDeclareWarOnEachOtherAndParticipantsCannotJoinTwoWars()
    {
        var world = CreateWorld(false);
        var otherLich = new GridPosition(45, 5);
        world.AddCritter(CritterSpecies.Lich, otherLich);
        Assert.False(world.TryStartApeWar(LichTile, otherLich));
        Assert.True(world.TryStartApeWar(Village, LichTile));
        Assert.False(world.TryStartApeWar(Village, otherLich));
        var otherVillage = new GridPosition(5, 22);
        Assert.True(world.TrySpawnTestApeVillage(otherVillage));
        Assert.False(world.TryStartApeWar(otherVillage, LichTile));
        Assert.False(world.TryStartApeWar(new(-1, -1), Village));
    }

    [Fact]
    public void NaturalVillageWarsCanSelectLiches()
    {
        var world = CreateWorld(false);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        var villageTile = Village.Y * world.Width + Village.X;
        for (var n = 0; n < 98; n++)
        {
            var id = world.AddCritter(CritterSpecies.Ape, new(15 + n % 40, 22 + n / 40));
            homes[id.Value] = villageTile;
        }
        NaturalEvents.SetEnabled(world, true);
        var tick = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!;
        tick.SetValue(world, (long)SimulationWorld.VillageWarCooldownTicks);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        var launched = false;
        for (var attempt = 0; attempt < 5000 && !launched; attempt++)
        {
            if (!world.TryStartVillageWar(villageTile)) continue;
            launched = world.TryGetApeWarSide(lich.Id, out _);
            if (!launched)
            {
                Assert.True(world.TryStopApeWar(Village));
                tick.SetValue(world, world.Tick + SimulationWorld.VillageWarCooldownTicks);
            }
        }
        Assert.True(launched);
        Assert.True(world.IsApeVillageAtWar(Village));
    }

    [Fact]
    public void LichDeathDestroysArmyAndEndsWar()
    {
        var world = CreateWorld(false);
        Assert.True(world.TryGetCritterAt(LichTile, out var lich));
        var soldier = Raise(world, lich.Id, new(36, 10));
        Assert.True(world.TryStartApeWar(Village, LichTile));
        world.RemoveCritterAt(LichTile);
        world.AdvanceApeWars();
        Assert.False(world.TryGetCritter(soldier, out _));
        Assert.False(world.IsApeVillageAtWar(Village));
    }
}
