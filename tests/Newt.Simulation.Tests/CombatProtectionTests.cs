using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CombatProtectionTests
{
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
            Assert.Contains(initial - survivingWarrior.Energy - survivingTherapsid.Energy, new[] { 1, 4 });
        }
    }
}
