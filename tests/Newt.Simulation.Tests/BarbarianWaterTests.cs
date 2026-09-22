using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianWaterTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    public void BarbarianCanHuntAcrossWaterWithoutChangingRoleOrEnergyOnEntry(CritterSpecies species)
    {
        var world = new SimulationWorld(20, 10, Terrain.Plains, seed: 42);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var camp = new GridPosition(5, 5);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        while (world.CritterCount > 0)
            world.RemoveCritterAt(world.GetCritter(0).Position);
        var start = new GridPosition(10, 5);
        world.SetTerrain(start, Terrain.Beach);
        for (var y = 0; y < world.Height; y++)
        for (var x = 11; x < world.Width; x++)
            world.SetTerrain(new(x, y), Terrain.Ocean);
        var id = world.AddCritter(species, start);
        Assert.True(world.TryAssignApeToVillage(id, camp));
        var energy = world.GetCritter(0).Energy;
        var prey = new GridPosition(13, 5);
        world.AddCritter(CritterSpecies.Squid, prey);
        Assert.Equal(prey, world.FindHunterPrey(0, species, 6, null));
        Assert.False(world.IsApePirate(id));

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(id, out var barbarian));
            if (!world.IsApePirate(id))
                continue;
            Assert.Equal(species, barbarian.Species);
            Assert.Equal(energy, barbarian.Energy);
            Assert.Equal(camp, world.GetApeHomeVillage(id));
            world.SetTerrain(barbarian.Position, Terrain.Beach);
            Assert.False(world.IsApePirate(id));
            return;
        }
        Assert.Fail("Barbarian did not enter water to pursue marine prey.");
    }
}
