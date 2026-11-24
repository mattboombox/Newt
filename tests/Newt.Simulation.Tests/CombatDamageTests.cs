using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CombatDamageTests
{
    [Fact]
    public void UndeadWarriorDealsThreeDamage()
    {
        var world = new SimulationWorld(10, 1, Terrain.Plains);
        world.AddCritter(CritterSpecies.UndeadWarrior, new(0, 0));
        world.AddCritter(CritterSpecies.Ape, new(1, 0));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        energies[1] = 6;
        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", flags)!.Invoke(world, [0, 1]);
        Assert.Equal(3, energies[1]);
        Assert.Equal(3, SimulationWorld.GetCombatDamage(CritterSpecies.UndeadWarrior));
    }

    [Theory]
    [InlineData(CritterSpecies.Therapsid)]
    [InlineData(CritterSpecies.Deer)]
    [InlineData(CritterSpecies.Elk)]
    [InlineData(CritterSpecies.Gazelle)]
    [InlineData(CritterSpecies.MegaSlug)]
    public void WolfDealsFourDamageRegardlessOfPreyStartingEnergy(CritterSpecies prey)
    {
        var world = new SimulationWorld(10, 1, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Wolf, new(0, 0));
        world.AddCritter(prey, new(1, 0));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        energies[1] = CritterNutritions.Get(prey).MaximumEnergy;
        var before = energies[1];
        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", flags)!.Invoke(world, [0, 1]);
        Assert.Equal(before - 4, energies[1]);
        energies[1] = 2;
        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", flags)!.Invoke(world, [0, 1]);
        Assert.Equal(0, energies[1]);
    }
}
