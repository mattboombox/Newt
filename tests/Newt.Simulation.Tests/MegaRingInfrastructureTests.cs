using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MegaRingInfrastructureTests
{
    [Fact]
    public void BothMegaFabricatorsRecruitAndRepairNearbyCustodians()
    {
        var world = new SimulationWorld(WorldPreset.RingMega.Width, 4, Terrain.MegaWall)
            { Body = WorldBody.RingWorld };
        world.SeedRingWorldCustodians();
        Assert.Equal(2, world.CustodianFabricators.Count);
        Assert.Equal(10, world.CustodianCapacity);
        Assert.Equal(10, world.GetCritterCount(CritterSpecies.Custodian));
        while (world.CritterCount > 0)
            world.RemoveCritterAt(world.GetCritter(0).Position);
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world,
            (long)SimulationWorld.CustodianRecruitmentIntervalTicks);
        world.AdvanceCustodianFabricator();
        Assert.Equal(2, world.CritterCount);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        for (var i = 0; i < 2; i++)
        {
            var location = world.GetCritter(i).Position;
            Assert.True(Math.Abs(location.X - world.CustodianFabricators[i].X) <= 1);
            energies[i] = 1;
            typeof(SimulationWorld).GetMethod("TryMoveCustodian", flags)!.Invoke(world, [i, null]);
            Assert.Equal(CritterNutritions.Get(CritterSpecies.Custodian).MaximumEnergy, energies[i]);
            Assert.Equal(location, world.GetCritter(i).Position);
        }
    }
}
