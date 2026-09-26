using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WarriorVeteranTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiveCombatKillsPromoteWarriorWithoutChangingSpecies(bool defending)
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains, seed: 17);
        // Keep this combat test's zombie strain immune to the warrior's ID remainder.
        for (var x = 0; x < 4; x++)
            world.AddCritter(CritterSpecies.Ape, new GridPosition(x, 0));
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, new GridPosition(10, 10));
        for (var kill = 1; kill <= 6; kill++)
        {
            Assert.True(world.TryGetCritter(warrior, out var fighter));
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
            Assert.Equal(kill >= 5 ? 5 : 4, world.GetCritterCombatDamage(warrior));
            Assert.Equal(CritterSpecies.ApeWarrior, fighter.Species);
        }
        Assert.True(world.TryGetCritter(warrior, out var veteran));
        world.RemoveCritterAt(veteran.Position);
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
