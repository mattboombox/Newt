using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BalanceBatchTests
{
    [Theory]
    [InlineData(Terrain.Ocean)]
    [InlineData(Terrain.DeepOcean)]
    [InlineData(Terrain.Shallows)]
    [InlineData(Terrain.Ice)]
    public void PlanktonFeedsAcrossAllOceanTypesWithoutConsumingNutrition(Terrain terrain)
    {
        var world = new SimulationWorld(1, 1, terrain, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        if (terrain == Terrain.Ice) world.SetElevation(new(0, 0), -0.3f);
        var initialNutrition = world.GetTileNutrition(new(0, 0));
        var id = world.AddCritter(CritterSpecies.Plankton, new(0, 0));
        for (var tick = 0; tick < SimulationWorld.PlanktonFeedingIntervalTicks; tick++) world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var plankton));
        Assert.Equal(2, plankton.Energy);
        Assert.Equal(initialNutrition, world.GetTileNutrition(new(0, 0)));
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeFarmer)]
    [InlineData(CritterSpecies.ApeLumberjack)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeScholar)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void EveryLivingApeRoleCanRecruitDogAfterDefendingAgainstWolfWithoutVillage(CritterSpecies species)
    {
        var dogs = 0;
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var world = new SimulationWorld(8, 3, Terrain.Plains, seed);
            var position = new GridPosition(3, 1);
            world.SetTerrain(position, species == CritterSpecies.ApeTraderSailor ? Terrain.Ocean : Terrain.Beach);
            var ape = world.AddCritter(species, position);
            world.AddCritter(CritterSpecies.Wolf, new(4, 1));
            var energy = (int[])typeof(SimulationWorld).GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
            energy[1] = 1;
            // Traders are not on the wolf's normal prey list; exercise their
            // retaliation kill through the same combat resolver directly.
            if (species is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor)
                typeof(SimulationWorld).GetMethod("CommitMutualPredatorCombat", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(world, [new GridPosition(4, 1), position]);
            else
                world.CommitEncounter(new(4, 1), position);
            Assert.True(world.TryGetCritter(ape, out _));
            dogs += world.GetCritterCount(CritterSpecies.Dog);
        }
        Assert.InRange(dogs, 1, 60);
    }

    [Fact]
    public void WarriorsCanPursuePiratesAcrossWaterInPeacetime()
    {
        var world = new SimulationWorld(40, 20, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var camp = new GridPosition(25, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, new(10, 10));
        var pirate = world.AddCritter(CritterSpecies.ApeWarrior, new(13, 10));
        Assert.True(world.TryAssignApeToVillage(pirate, camp));
        for (var y = 0; y < world.Height; y++)
        for (var x = 11; x < 15; x++) world.SetTerrain(new(x, y), Terrain.Ocean);
        Assert.True(world.IsApePirate(pirate));
        Assert.Equal(new GridPosition(13, 10), world.FindHunterPrey(0, CritterSpecies.ApeWarrior, 6, null));
        var move = typeof(SimulationWorld).GetMethod("TryMoveHunter", BindingFlags.Instance | BindingFlags.NonPublic)!;
        move.Invoke(world, [0, 6, null, true, null]);
        Assert.True(world.TryGetCritter(warrior, out var sailing));
        Assert.Equal(Terrain.Ocean, world.GetTerrain(sailing.Position));
    }

    [Fact]
    public void NaturalOutbreaksChooseAllThreeStrainsEqually()
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17);
        NaturalEvents.SetEnabled(world, true);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var tile = village.Y * world.Width + village.X;
        for (var i = 0; i < 200; i++) homes[world.AddCritter(CritterSpecies.Ape, new(i % 40, i / 40)).Value] = tile;
        var counts = new Dictionary<PlagueKind, int>();
        for (var outbreak = 0; outbreak < 300; outbreak++)
        {
            var started = false;
            for (var attempt = 0; attempt < 10000 && !started; attempt++) started = world.TryStartVillagePlague(tile);
            Assert.True(started);
            var sick = Assert.Single(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), c => c.Plague != PlagueKind.None);
            counts[sick.Plague] = counts.GetValueOrDefault(sick.Plague) + 1;
            world.RemoveCritterAt(sick.Position);
            homes[world.AddCritter(CritterSpecies.Ape, sick.Position).Value] = tile;
        }
        Assert.InRange(counts.GetValueOrDefault(PlagueKind.Plague), 70, 130);
        Assert.InRange(counts.GetValueOrDefault(PlagueKind.Zombie), 70, 130);
        Assert.InRange(counts.GetValueOrDefault(PlagueKind.Vampire), 70, 130);
    }

    [Fact]
    public void UndeadApesDealOneDamage()
    {
        Assert.Equal(1, SimulationWorld.GetCombatDamage(CritterSpecies.UndeadApe));
        var world = new SimulationWorld(5, 5, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.UndeadApe, new(1, 1));
        var victim = world.AddCritter(CritterSpecies.Ape, new(2, 1));
        Assert.True(world.TryGetCritter(victim, out var before));
        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [0, 1]);
        Assert.True(world.TryGetCritter(victim, out var after));
        Assert.Equal(before.Energy - 1, after.Energy);
    }
}
