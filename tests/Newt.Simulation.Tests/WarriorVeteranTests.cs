using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WarriorVeteranTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void FiveCombatKillsPromoteWarriorWithoutChangingSpecies(bool defending, bool barbarian, bool sailing)
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains, seed: 17);
        var camp = new GridPosition(5, 5);
        if (barbarian)
        {
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetBiome(new(x, y), Biome.Grassland);
            Assert.True(world.TrySpawnBarbarianApeVillage(camp));
            while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        }
        // Keep this combat test's zombie strain immune to the warrior's ID remainder.
        for (var x = 0; x < 4; x++)
            world.AddCritter(CritterSpecies.Ape, new GridPosition(x, 0));
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, new GridPosition(10, 10));
        if (barbarian) Assert.True(world.TryAssignApeToVillage(warrior, camp));
        var baseDamage = barbarian ? 3 : 4;
        for (var kill = 1; kill <= 6; kill++)
        {
            Assert.True(world.TryGetCritter(warrior, out var fighter));
            if (sailing) world.SetTerrain(fighter.Position, Terrain.Ocean);
            Assert.Equal(sailing, world.IsApePirate(warrior));
            var enemy = world.AddCritter(CritterSpecies.UndeadApe, new GridPosition(fighter.Position.X + 1, fighter.Position.Y));
            var immunity = (Dictionary<int, int>)typeof(SimulationWorld)
                .GetField("_plagueImmunityRemainders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .GetValue(world)!;
            immunity[enemy.Value] = warrior.Value % 5;
            for (var exchange = 0; exchange < 100 && world.TryGetCritter(enemy, out var opponent); exchange++)
            {
                Assert.True(world.TryGetCritter(warrior, out fighter));
                world.CommitEncounter(defending ? opponent.Position : fighter.Position,
                    defending ? fighter.Position : opponent.Position);
            }
            Assert.False(world.TryGetCritter(enemy, out _));
            Assert.True(world.TryGetCritter(warrior, out fighter));
            Assert.Equal(kill, fighter.CombatKills);
            Assert.Equal(kill >= 5, fighter.IsVeteranWarrior);
            Assert.Equal(baseDamage + (kill >= 5 ? 1 : 0), world.GetCritterCombatDamage(warrior));
            Assert.Equal(CritterSpecies.ApeWarrior, fighter.Species);
        }
        Assert.True(world.TryGetCritter(warrior, out var veteran));
        world.RemoveCritterAt(veteran.Position);
        world.SetTerrain(veteran.Position, Terrain.Plains);
        var replacement = world.AddCritter(CritterSpecies.ApeWarrior, veteran.Position);
        Assert.True(world.TryGetCritter(replacement, out var recruit));
        Assert.False(recruit.IsVeteranWarrior);
        Assert.Equal(0, recruit.CombatKills);
        Assert.Equal(4, world.GetCritterCombatDamage(replacement));
    }

    [Fact]
    public void BarbarianHuntingNonCombatPreyDoesNotAwardVeterancy()
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        var camp = new GridPosition(10, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, camp);
        Assert.True(world.TryAssignApeToVillage(warrior, camp));
        for (var kill = 0; kill < 6; kill++)
        {
            Assert.True(world.TryGetCritter(warrior, out var hunter));
            var prey = new GridPosition(hunter.Position.X + 1, hunter.Position.Y);
            world.AddCritter(CritterSpecies.Deer, prey);
            world.CommitEncounter(hunter.Position, prey);
        }
        Assert.True(world.TryGetCritter(warrior, out var regular));
        Assert.False(regular.IsVeteranWarrior);
        Assert.Equal(0, regular.CombatKills);
        Assert.Equal(3, world.GetCritterCombatDamage(warrior));
    }
}
