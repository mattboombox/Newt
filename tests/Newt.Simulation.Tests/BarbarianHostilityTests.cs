using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianHostilityTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    public void WellFedBarbariansAttackOtherCampsButProtectTheirOwn(CritterSpecies species)
    {
        var world = new SimulationWorld(60, 30, Terrain.Beach, seed: 1);
        // Keep feast food spending out of this faction-combat scenario.
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var first = new GridPosition(10, 10);
        var second = new GridPosition(35, 10);
        world.SetTerrain(new(10, 11), Terrain.Shallows);
        world.SetTerrain(new(35, 11), Terrain.Shallows);
        Assert.True(world.TrySpawnBarbarianApeVillage(first));
        Assert.True(world.TrySpawnBarbarianApeVillage(second));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var attackerPosition = new GridPosition(20, 10);
        var rivalPosition = new GridPosition(21, 10);
        var allyPosition = new GridPosition(20, 13);
        var attacker = world.AddCritter(species, attackerPosition);
        var rival = world.AddCritter(CritterSpecies.ApeWarrior, rivalPosition);
        var ally = world.AddCritter(CritterSpecies.ApeWarrior, allyPosition);
        Assert.True(world.TryAssignApeToVillage(attacker, first));
        Assert.True(world.TryAssignApeToVillage(ally, first));
        Assert.True(world.TryAssignApeToVillage(rival, second));
        world.StoreApeVillageFood(first, 50);
        world.StoreApeVillageFood(second, 50);
        Assert.False(world.ShouldApeHunt(attacker));
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", flags)!;
        Assert.Equal(true, canAttack.Invoke(world, [IndexOf(attacker), IndexOf(rival)]));
        Assert.Equal(true, canAttack.Invoke(world, [IndexOf(rival), IndexOf(attacker)]));
        Assert.Equal(false, canAttack.Invoke(world, [IndexOf(attacker), IndexOf(ally)]));
        var move = typeof(SimulationWorld).GetMethod("TryMoveHunter", flags)!;
        Assert.Equal(rivalPosition, (GridPosition)move.Invoke(world, [IndexOf(attacker), 6, null, true, null])!);
        Assert.True(world.TryGetCritter(rival, out var before));
        Assert.True(world.TryGetCritter(attacker, out var beforeAttacker));
        world.CommitEncounter(attackerPosition, rivalPosition);
        Assert.True(!world.TryGetCritter(rival, out var after) || !world.TryGetCritter(attacker, out var afterAttacker) ||
            after.Energy + afterAttacker.Energy < before.Energy + beforeAttacker.Energy);

        int IndexOf(CritterId id) => Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    }
}
