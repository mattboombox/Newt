using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LichTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(50, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        return world;
    }
    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    private static int[] Energy(SimulationWorld world) =>
        (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeScholar)]
    [InlineData(CritterSpecies.ApeFarmer)]
    [InlineData(CritterSpecies.ApeLumberjack)]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void LichConvertsEveryApeRoleAndRestoresEnergy(CritterSpecies species)
    {
        var world = CreateWorld();
        var lich = world.AddCritter(CritterSpecies.Lich, new(10, 10));
        var tile = new GridPosition(11, 10);
        if (species is CritterSpecies.ApeSailor or CritterSpecies.ApeTraderSailor)
            world.SetTerrain(tile, Terrain.Shallows);
        var ape = world.AddCritter(species, tile);
        for (var encounter = 0; encounter < 100 && world.GetCritter(Index(world, ape)).Species == species; encounter++)
            world.CommitEncounter(new(10, 10), tile);
        Assert.True(world.TryGetCritter(ape, out var raised));
        Assert.Equal(CritterSpecies.UndeadApe, raised.Species);
        Assert.False(world.IsBarbarianApe(ape));
        Assert.False(raised.IsColonist);
        Assert.True(world.TryGetCritter(lich, out var after));
        Assert.True(after.Energy > 0);
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Lich, CritterSpecies.Dog));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Lich, CritterSpecies.UndeadWarrior));
    }

    [Theory]
    [InlineData(CritterSpecies.Lich)]
    [InlineData(CritterSpecies.UndeadWarrior)]
    public void UndeadHasNoMetabolismOrReproduction(CritterSpecies species)
    {
        var world = CreateWorld();
        var id = world.AddCritter(species, new(10, 10));
        Assert.False(CritterNutritions.Get(species).HasMetabolism);
        for (var tick = 0; tick < 180 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var after));
        Assert.Equal(CritterNutritions.Get(species).InitialEnergy, after.Energy);
        Assert.False(after.CanReproduce);
    }

    [Fact]
    public void LichRaisesArmyAndFirstTwentyWarriorsFollowAndCanBeShoved()
    {
        var world = CreateWorld();
        var lich = world.AddCritter(CritterSpecies.Lich, new(25, 15));
        var soldiers = new List<CritterId>();
        for (var n = 0; n < 22; n++)
        {
            var ape = world.AddCritter(CritterSpecies.Ape, new(2 + n, 5));
            Energy(world)[Index(world, ape)] = 0;
            typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
                .Invoke(world, [new GridPosition(25, 15), new GridPosition(2 + n, 5), true]);
            Assert.Equal(CritterSpecies.UndeadApe, world.GetCritter(Index(world, ape)).Species);
            Energy(world)[Index(world, ape)] = 0;
            soldiers.Add(ape);
        }
        world.AdvanceOneTick();
        Assert.All(soldiers, id => Assert.Equal(CritterSpecies.UndeadWarrior, world.GetCritter(Index(world, id)).Species));
        Assert.Equal(soldiers.Take(20), world.GetLichEscort(lich));
        var follower = soldiers[0];
        for (var step = 0; step < 60; step++)
            typeof(SimulationWorld).GetMethod("TryMoveUndeadWarrior", Private)!.Invoke(world, [Index(world, follower), null]);
        var position = world.GetCritter(Index(world, follower)).Position;
        Assert.InRange(Math.Abs(position.X - 25) + Math.Abs(position.Y - 15), 1, 3);
        var shoved = (bool)typeof(SimulationWorld).GetMethod("TryShoveMovementBlocker", Private)!
            .Invoke(world, [Index(world, lich), position.Y * world.Width + position.X, null])!;
        Assert.True(shoved);
        Assert.NotEqual(position, world.GetCritter(Index(world, follower)).Position);
        Assert.True(SimulationWorld.CanDisplace(CritterSpecies.Lich, CritterSpecies.UndeadApe));
        world.RemoveCritterAt(world.GetCritter(Index(world, lich)).Position);
        Assert.Empty(world.GetLichEscort(lich));
    }

    [Fact]
    public void LichKillRestoresFiveEnergyAndIgnoresBarbarianAndColonistAffiliation()
    {
        var world = CreateWorld();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var camp = new GridPosition(10, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        Assert.True(world.TrySendApeColonist(new GridPosition(35, 15)));
        var lich = world.AddCritter(CritterSpecies.Lich, new(30, 20));
        var targets = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(critter => critter.IsColonist || world.IsBarbarianApe(critter.Id)).ToArray();
        Assert.Contains(targets, critter => critter.IsColonist);
        Assert.Contains(targets, critter => world.IsBarbarianApe(critter.Id));
        foreach (var target in targets)
        {
            Energy(world)[Index(world, lich)] = 3;
            Energy(world)[Index(world, target.Id)] = 0;
            typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
                .Invoke(world, [new GridPosition(30, 20), target.Position, true]);
            Assert.Equal(CritterSpecies.UndeadApe, world.GetCritter(Index(world, target.Id)).Species);
            Assert.Equal(8, world.GetCritter(Index(world, lich)).Energy);
        }
    }

    [Fact]
    public void OrdinaryZombieEventNeverProducesWarriors()
    {
        var world = CreateWorld();
        var ape = world.AddCritter(CritterSpecies.Ape, new(10, 10));
        Assert.True(world.TryInfectApeAt(new(10, 10), PlagueKind.Zombie, (ape.Value + 1) % 2));
        Energy(world)[Index(world, ape)] = 0;
        world.AdvanceOneTick();
        Assert.Equal(CritterSpecies.UndeadApe, world.GetCritter(Index(world, ape)).Species);
        Energy(world)[Index(world, ape)] = 0;
        world.AdvanceOneTick();
        Assert.False(world.TryGetCritter(ape, out _));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.UndeadWarrior));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZombieSpreadPreservesLichOriginAcrossMultipleInfections(bool lichOrigin)
    {
        var world = CreateWorld();
        var lich = world.AddCritter(CritterSpecies.Lich, new(20, 20));
        var source = world.AddCritter(CritterSpecies.Ape, new(10, 10));
        if (lichOrigin)
        {
            Energy(world)[Index(world, source)] = 0;
            typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
                .Invoke(world, [new GridPosition(20, 20), new GridPosition(10, 10), true]);
        }
        else
        {
            Assert.True(world.TryInfectApeAt(new(10, 10), PlagueKind.Zombie, (source.Value + 1) % 2));
            Energy(world)[Index(world, source)] = 0;
            typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!.Invoke(world, [Index(world, source)]);
        }
        var first = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        var second = world.AddCritter(CritterSpecies.Ape, new(12, 10));
        var spread = typeof(SimulationWorld).GetMethod("TrySpreadCritterPlague", Private)!;
        Assert.True((bool)spread.Invoke(world,
            [Index(world, source), new GridPosition(11, 10), PlagueKind.Zombie, (first.Value + 1) % 2])!);
        Assert.True((bool)spread.Invoke(world,
            [Index(world, first), new GridPosition(12, 10), PlagueKind.Zombie, (second.Value + 1) % 2])!);
        // Both infection hops preserve the origin while their lich is alive.
        foreach (var id in new[] { first, second })
        {
            Energy(world)[Index(world, id)] = 0;
            var rise = typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!;
            Assert.True((bool)rise.Invoke(world, [Index(world, id)])!);
            Assert.Equal(CritterSpecies.UndeadApe, world.GetCritter(Index(world, id)).Species);
            Energy(world)[Index(world, id)] = 0;
            Assert.Equal(lichOrigin, (bool)rise.Invoke(world, [Index(world, id)])!);
            Assert.Equal(lichOrigin ? CritterSpecies.UndeadWarrior : CritterSpecies.UndeadApe,
                world.GetCritter(Index(world, id)).Species);
        }
    }

    [Fact]
    public void LichDeathKillsAllItsWarriorsAndDisablesItsRemainingZombieLineage()
    {
        var world = CreateWorld();
        var lich = world.AddCritter(CritterSpecies.Lich, new(20, 20));
        var otherLich = world.AddCritter(CritterSpecies.Lich, new(35, 20));
        var warriors = new List<CritterId>();
        CritterId Raise(CritterId leader, GridPosition tile, bool warrior)
        {
            var id = world.AddCritter(CritterSpecies.Ape, tile);
            typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!
                .Invoke(world, [Index(world, leader), Index(world, id)]);
            if (warrior)
                typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!.Invoke(world, [Index(world, id)]);
            return id;
        }
        for (var n = 0; n < 22; n++)
            warriors.Add(Raise(lich, new(2 + n, 5), true));
        var zombie = Raise(lich, new(10, 10), false);
        var otherWarrior = Raise(otherLich, new(35, 10), true);
        var infected = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TrySpreadCritterPlague", Private)!
            .Invoke(world, [Index(world, zombie), new GridPosition(11, 10), PlagueKind.Zombie, (infected.Value + 1) % 2])!);
        world.RemoveCritterAt(world.GetCritter(Index(world, lich)).Position);
        Assert.All(warriors, id => Assert.False(world.TryGetCritter(id, out _)));
        Assert.True(world.TryGetCritter(otherWarrior, out var survivor));
        Assert.Equal(CritterSpecies.UndeadWarrior, survivor.Species);
        Assert.True(world.TryGetCritter(zombie, out _));
        Energy(world)[Index(world, zombie)] = 0;
        world.AdvanceOneTick();
        Assert.False(world.TryGetCritter(zombie, out _));
        Energy(world)[Index(world, infected)] = 0;
        world.AdvanceOneTick();
        Assert.Equal(CritterSpecies.UndeadApe, world.GetCritter(Index(world, infected)).Species);
        Energy(world)[Index(world, infected)] = 0;
        world.AdvanceOneTick();
        Assert.False(world.TryGetCritter(infected, out _));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.UndeadWarrior));
    }

    [Fact]
    public void CombatKillOfLichCollapsesArmyWithoutRemovingItsKiller()
    {
        var world = CreateWorld();
        var killer = world.AddCritter(CritterSpecies.ApeWarrior, new(10, 10));
        var lich = world.AddCritter(CritterSpecies.Lich, new(11, 10));
        var soldier = world.AddCritter(CritterSpecies.Ape, new(15, 10));
        typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!
            .Invoke(world, [Index(world, lich), Index(world, soldier)]);
        typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!.Invoke(world, [Index(world, soldier)]);
        Energy(world)[Index(world, lich)] = 0;
        typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
            .Invoke(world, [new GridPosition(10, 10), new GridPosition(11, 10), true]);
        Assert.False(world.TryGetCritter(lich, out _));
        Assert.False(world.TryGetCritter(soldier, out _));
        Assert.True(world.TryGetCritter(killer, out _));
    }

    [Fact]
    public void WarriorGainsEnergyFromKillAndDoesNotConvertVictim()
    {
        var world = CreateWorld();
        var warrior = world.AddCritter(CritterSpecies.UndeadWarrior, new(10, 10));
        var ape = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        Energy(world)[Index(world, warrior)] = 5;
        Energy(world)[Index(world, ape)] = 0;
        typeof(SimulationWorld).GetMethod("CommitPredation", Private)!
            .Invoke(world, [new GridPosition(10, 10), new GridPosition(11, 10), true]);
        Assert.False(world.TryGetCritter(ape, out _));
        Assert.True(world.TryGetCritter(warrior, out var after));
        Assert.Equal(6, after.Energy);
    }
}
