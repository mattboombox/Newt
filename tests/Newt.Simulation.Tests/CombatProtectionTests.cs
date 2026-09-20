using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CombatProtectionTests
{
    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void EverySpeciesEitherIgnoresTraderOrResolvesCombat(CritterSpecies traderSpecies)
    {
        foreach (var attackerSpecies in Enum.GetValues<CritterSpecies>())
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(2, 1, Terrain.Shallows, seed);
            var attackPosition = new GridPosition(0, 0);
            var traderPosition = new GridPosition(1, 0);
            var attacker = world.AddCritter(attackerSpecies, attackPosition);
            var trader = world.AddCritter(traderSpecies, traderPosition);
            var beforeAttacker = world.GetCritter(0);
            var beforeTrader = world.GetCritter(1);
            world.CommitEncounter(attackPosition, traderPosition);
            Assert.True(world.TryGetCritter(trader, out var afterTrader), $"{attackerSpecies} ate {traderSpecies}");
            Assert.True(world.TryGetCritter(attacker, out var afterAttacker));
            var lost = beforeTrader.Energy + beforeAttacker.Energy - afterTrader.Energy - afterAttacker.Energy;
            if (SimulationWorld.CanEat(attackerSpecies, traderSpecies))
                Assert.Contains(lost, new[] { 1, world.GetCritterCombatDamage(attacker) });
            else
                Assert.Equal(0, lost);
        }
    }

    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void BarbarianDietOverrideStillRequiresCombatWithTraders(CritterSpecies traderSpecies)
    {
        foreach (var attackerSpecies in new[] { CritterSpecies.Ape, CritterSpecies.ApeFarmer,
            CritterSpecies.ApeLumberjack, CritterSpecies.ApeSailor, CritterSpecies.ApeWarrior,
            CritterSpecies.ApeScholar, CritterSpecies.ApeChieftain })
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(20, 10, Terrain.Plains, seed);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetBiome(new(x, y), Biome.Grassland);
            var camp = new GridPosition(5, 5);
            Assert.True(world.TrySpawnBarbarianApeVillage(camp));
            while (world.CritterCount > 0)
                world.RemoveCritterAt(world.GetCritter(0).Position);
            var attackPosition = new GridPosition(5, 4);
            var traderPosition = new GridPosition(6, 4);
            world.SetTerrain(attackPosition, Terrain.Shallows);
            world.SetTerrain(traderPosition, Terrain.Shallows);
            var attacker = world.AddCritter(attackerSpecies, attackPosition);
            Assert.True(world.TryAssignApeToVillage(attacker, camp));
            var trader = world.AddCritter(traderSpecies, traderPosition);
            var total = world.GetCritter(0).Energy + world.GetCritter(1).Energy;
            world.CommitEncounter(attackPosition, traderPosition);
            Assert.True(world.TryGetCritter(trader, out var afterTrader));
            Assert.True(world.TryGetCritter(attacker, out var afterAttacker));
            Assert.Contains(total - afterTrader.Energy - afterAttacker.Energy, new[] { 1, 3 });
        }
    }

    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void SpiderCannotStoreLivingTraderFromWebWithoutCombat(CritterSpecies traderSpecies)
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(2, 1, Terrain.Plains, seed);
            var attackPosition = new GridPosition(0, 0);
            var web = new GridPosition(1, 0);
            var spider = world.AddCritter(CritterSpecies.MegaSpider, attackPosition);
            Assert.True(world.TryCreateMegaSpiderWeb(spider, web));
            // Include a stranded sailor so even an unusual web placement cannot bypass combat.
            Assert.True(world.TrySpawnCritter(traderSpecies, web));
            Assert.True(world.TryGetCritterAt(web, out var trader));
            Assert.True(world.IsCritterCaughtInMegaSpiderWeb(trader.Id));
            var total = trader.Energy + world.GetCritter(0).Energy;
            world.CommitEncounter(attackPosition, web);
            Assert.True(world.TryGetCritter(trader.Id, out var afterTrader));
            Assert.True(world.TryGetCritter(spider, out var afterSpider));
            Assert.Contains(total - afterTrader.Energy - afterSpider.Energy, new[] { 1, 2 });
            Assert.Equal(0, world.GetMegaSpiderWebFood(web));
        }
    }
    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void TradersDealOneDamageAndMustBeDefeatedInCombat(CritterSpecies species)
    {
        var sawRetaliation = false;
        var sawPredatorHit = false;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var world = new SimulationWorld(2, 1, Terrain.Shallows, seed);
            var attackerPosition = new GridPosition(0, 0);
            var traderPosition = new GridPosition(1, 0);
            var attacker = world.AddCritter(CritterSpecies.MegaSpider, attackerPosition);
            var trader = world.AddCritter(species, traderPosition);
            Assert.True(world.TryGetCritter(attacker, out var beforeAttacker));
            Assert.True(world.TryGetCritter(trader, out var beforeTrader));
            Assert.Equal(1, world.GetCritterCombatDamage(trader));

            world.CommitEncounter(attackerPosition, traderPosition);

            Assert.True(world.TryGetCritter(attacker, out var afterAttacker));
            Assert.True(world.TryGetCritter(trader, out var afterTrader));
            if (afterAttacker.Energy < beforeAttacker.Energy)
            {
                sawRetaliation = true;
                Assert.Equal(beforeAttacker.Energy - 1, afterAttacker.Energy);
                Assert.Equal(beforeTrader.Energy, afterTrader.Energy);
            }
            else
            {
                sawPredatorHit = true;
                Assert.Equal(beforeTrader.Energy - 2, afterTrader.Energy);
            }
        }
        Assert.True(sawRetaliation);
        Assert.True(sawPredatorHit);
    }
    [Fact]
    public void CombatCapablePreyTakesCombatDamageFromEveryEligibleSpecies()
    {
        foreach (var attacker in Enum.GetValues<CritterSpecies>())
        foreach (var defender in Enum.GetValues<CritterSpecies>())
        {
            if (!SimulationWorld.CanEat(attacker, defender))
                continue;
            // Cannibal spiders only attack while hungry; newly spawned ones are full.
            if (attacker == CritterSpecies.MegaSpider && defender == attacker)
                continue;
            var world = new SimulationWorld(2, 1, Terrain.Shallows, seed: 52);
            var attackPosition = new GridPosition(0, 0);
            var defensePosition = new GridPosition(1, 0);
            var attackId = world.AddCritter(attacker, attackPosition);
            var defenseId = world.AddCritter(defender, defensePosition);
            var attackDamage = world.GetCritterCombatDamage(attackId);
            var defenseDamage = world.GetCritterCombatDamage(defenseId);
            var initial = world.GetCritter(0).Energy + world.GetCritter(1).Energy;
            if (defenseDamage == 0 || world.GetCritter(0).Energy <= defenseDamage ||
                world.GetCritter(1).Energy <= attackDamage)
                continue;

            world.CommitEncounter(attackPosition, defensePosition);

            Assert.True(world.TryGetCritter(attackId, out var survivingAttacker), $"{attacker} vs {defender}");
            Assert.True(world.TryGetCritter(defenseId, out var survivingDefender), $"{attacker} vs {defender}");
            if ((attacker is CritterSpecies.Vampire && survivingAttacker.Energy > CritterNutritions.Get(attacker).InitialEnergy) ||
                (defender is CritterSpecies.Vampire && survivingDefender.Energy > CritterNutritions.Get(defender).InitialEnergy))
            {
                Assert.Equal(initial, survivingAttacker.Energy + survivingDefender.Energy);
                continue;
            }
            Assert.Contains(initial - survivingAttacker.Energy - survivingDefender.Energy,
                new[] { attackDamage, defenseDamage });
        }
    }

    [Fact]
    public void BarbarianWarriorsMustFightTherapsids()
    {
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(20, 10, Terrain.Plains, seed);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetBiome(new GridPosition(x, y), Biome.Grassland);
            var camp = new GridPosition(5, 5);
            Assert.True(world.TrySpawnBarbarianApeVillage(camp));
            while (world.CritterCount > 0)
                world.RemoveCritterAt(world.GetCritter(0).Position);
            var attacker = new GridPosition(5, 4);
            var defender = new GridPosition(6, 4);
            var warrior = world.AddCritter(CritterSpecies.ApeWarrior, attacker);
            Assert.True(world.TryAssignApeToVillage(warrior, camp));
            var therapsid = world.AddCritter(CritterSpecies.Therapsid, defender);
            var initial = world.GetCritter(0).Energy + world.GetCritter(1).Energy;

            world.CommitEncounter(attacker, defender);

            Assert.True(world.TryGetCritter(warrior, out var survivingWarrior));
            Assert.True(world.TryGetCritter(therapsid, out var survivingTherapsid));
            Assert.Contains(initial - survivingWarrior.Energy - survivingTherapsid.Energy, new[] { 1, 3 });
        }
    }
}
