using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class DamageFlashSourceTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void DrainNow(SimulationWorld world, int index)
    {
        var infections = (Dictionary<int, (PlagueKind Kind, long InfectedTick, long DrainTick)>)
            typeof(SimulationWorld).GetField("_plagues", PrivateInstance)!.GetValue(world)!;
        var id = world.GetCritter(index).Id.Value;
        infections[id] = (world.GetCritter(index).Plague, 0, world.Tick);
        typeof(SimulationWorld).GetMethod("DrainPlagueEnergy", PrivateInstance)!.Invoke(world, [index]);
    }

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    [InlineData(PlagueKind.Vampire)]
    public void SicknessDamageFlashExpiresWithItsTimer(PlagueKind kind)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.Ape, new(0, 0));
        Assert.True(world.TryInfectApeAt(new(0, 0), kind, 0));
        DrainNow(world, 0);
        Assert.True(world.GetCritter(0).IsDamageFlashing);
        Assert.True(world.GetCritter(0).IsSicknessDamageFlashing);
        for (var tick = 0; tick < SimulationWorld.CombatDamageFlashTicks; tick++) world.AdvanceOneTick();
        Assert.False(world.GetCritter(0).IsDamageFlashing);
        Assert.False(world.GetCritter(0).IsSicknessDamageFlashing);
    }

    [Fact]
    public void CombatOnSickApeReplacesGreenFlashAndNextSicknessTickRestoresIt()
    {
        var testedHits = 0;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(2, 1, Terrain.Plains, seed);
            var ape = world.AddCritter(CritterSpecies.Ape, new(0, 0));
            world.AddCritter(CritterSpecies.Wolf, new(1, 0));
            var energy = (int[])typeof(SimulationWorld).GetField("_energy", PrivateInstance)!.GetValue(world)!;
            energy[0] = 12;
            energy[1] = 5;
            Assert.True(world.TryInfectApeAt(new(0, 0), PlagueKind.Plague, 0));
            DrainNow(world, 0);
            var before = world.GetCritter(0).Energy;
            world.CommitEncounter(new(1, 0), new(0, 0));
            Assert.True(world.TryGetCritter(ape, out var after));
            if (after.Energy >= before) continue;
            testedHits++;
            Assert.True(after.IsDamageFlashing);
            Assert.False(after.IsSicknessDamageFlashing);
            Assert.Equal(PlagueKind.Plague, after.Plague);
            DrainNow(world, 0);
            Assert.True(world.GetCritter(0).IsSicknessDamageFlashing);
        }
        Assert.True(testedHits > 0);
    }

    [Fact]
    public void FlashSourceFollowsCritterWhenArraysCompactAndResetsForNewCritters()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Ape, new(0, 0));
        var sick = world.AddCritter(CritterSpecies.Ape, new(1, 0));
        Assert.True(world.TryInfectApeAt(new(1, 0), PlagueKind.Plague, 1));
        DrainNow(world, 1);
        world.RemoveCritterAt(new(0, 0));
        Assert.True(world.TryGetCritter(sick, out var moved));
        Assert.True(moved.IsSicknessDamageFlashing);
        world.AddCritter(CritterSpecies.Ape, new(2, 0));
        Assert.False(world.GetCritter(1).IsSicknessDamageFlashing);
        Assert.False(world.GetCritter(1).IsDamageFlashing);
    }
}
