using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WarriorVeteranTests
{
    [Theory]
    [InlineData(CritterSpecies.ApeWarrior, false, CritterSpecies.Wolf, Terrain.Plains)]
    [InlineData(CritterSpecies.ApeWarrior, true, CritterSpecies.Wolf, Terrain.Plains)]
    [InlineData(CritterSpecies.Ape, true, CritterSpecies.Wolf, Terrain.Plains)]
    [InlineData(CritterSpecies.ApeSailor, true, CritterSpecies.SeaScorpion, Terrain.Shallows)]
    [InlineData(CritterSpecies.ApeWarrior, true, CritterSpecies.Squid, Terrain.Shallows)]
    public void PredatorKillsPromoteWarriorsBarbariansAndPirates(
        CritterSpecies species, bool barbarian, CritterSpecies prey, Terrain terrain)
    {
        var world = new SimulationWorld(40, 20, Terrain.Plains, seed: 17);
        var camp = new GridPosition(5, 5);
        if (barbarian)
        {
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
            Assert.True(world.TrySpawnBarbarianApeVillage(camp));
            while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        }
        var start = new GridPosition(15, 10);
        world.SetTerrain(start, terrain);
        var id = world.AddCritter(species, start);
        if (barbarian) Assert.True(world.TryAssignApeToVillage(id, camp));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", flags)!.GetValue(world)!;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        for (var kill = 1; kill <= 5; kill++)
        {
            Assert.True(world.TryGetCritter(id, out var fighter));
            var target = new GridPosition(fighter.Position.X + 1, fighter.Position.Y);
            // Wolf kills may recruit a dog into the next target tile.
            world.RemoveCritterAt(target);
            world.SetTerrain(target, terrain);
            var enemy = world.AddCritter(prey, target);
            // Isolate kill credit from the chance of losing a long series of combat rolls.
            energy[indices[enemy.Value]] = 1;
            for (var exchange = 0; exchange < 100 && world.TryGetCritter(enemy, out _); exchange++)
            {
                energy[indices[id.Value]] = CritterNutritions.Get(species).MaximumEnergy;
                world.CommitEncounter(fighter.Position, target);
            }
            Assert.False(world.TryGetCritter(enemy, out _));
            Assert.True(world.TryGetCritter(id, out fighter));
            Assert.Equal(kill, fighter.CombatKills);
            Assert.Equal(kill == 5, fighter.IsVeteranWarrior);
        }
        Assert.Equal(barbarian && terrain == Terrain.Shallows, world.IsApePirate(id));
        Assert.True(world.TryGetCritter(id, out var veteran));
        Assert.Equal(barbarian ? 5 : 6, world.GetCritterCombatDamage(id));
        Assert.Equal(CritterNutritions.Get(species).MaximumEnergy, veteran.Energy);
        if (barbarian)
        {
            // The renderer chooses pirate-veteran afloat and barbarian-veteran ashore.
            world.SetTerrain(veteran.Position, Terrain.Plains);
            Assert.False(world.IsApePirate(id));
            Assert.True(world.TryGetCritter(id, out veteran));
            Assert.True(veteran.IsVeteranWarrior);
            typeof(SimulationWorld).GetMethod("ChangeCritterSpecies", flags)!
                .Invoke(world, [indices[id.Value], CritterSpecies.ApeSailor, true, true]);
            Assert.True(world.TryGetCritter(id, out veteran));
            Assert.True(veteran.IsVeteranWarrior);
            typeof(SimulationWorld).GetMethod("ChangeCritterSpecies", flags)!
                .Invoke(world, [indices[id.Value], CritterSpecies.ApeWarrior, true, true]);
            Assert.True(world.TryGetCritter(id, out veteran));
            Assert.True(veteran.IsVeteranWarrior);
            Assert.Equal(5, veteran.CombatKills);
        }
    }

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
            Assert.Equal(baseDamage + (kill >= 5 ? 2 : 0), world.GetCritterCombatDamage(warrior));
            Assert.Equal(CritterSpecies.ApeWarrior, fighter.Species);
            if (kill == 5)
                Assert.Equal(CritterNutritions.Get(CritterSpecies.ApeWarrior).MaximumEnergy, fighter.Energy);
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
    public void VeteranEnergyRefillHappensOnlyOnPromotion()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 17);
        var id = world.AddCritter(CritterSpecies.ApeWarrior, new(5, 5));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var record = typeof(SimulationWorld).GetMethod("RecordWarriorCombatKill", flags)!;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        for (var i = 0; i < 4; i++) record.Invoke(world, [0]);
        energy[0] = 1;
        record.Invoke(world, [0]);
        Assert.True(world.TryGetCritter(id, out var promoted));
        Assert.Equal(12, promoted.Energy);
        energy[0] = 2;
        record.Invoke(world, [0]);
        Assert.Equal(2, world.GetCritter(0).Energy);
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
