using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianReproductionTests
{
    [Theory]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.Ape)]
    public void ReadyRaidersReturnToCampAndReproduce(CritterSpecies species)
    {
        var world = new SimulationWorld(30, 10,
            species == CritterSpecies.ApeSailor ? Terrain.Beach : Terrain.Plains, seed: 12);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        var camp = new GridPosition(5, 5);
        if (species == CritterSpecies.ApeSailor)
        {
            world.SetTerrain(camp, Terrain.Beach);
            for (var x = 6; x < world.Width; x++)
                world.SetTerrain(new GridPosition(x, 5), Terrain.Shallows);
        }
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        while (world.CritterCount > 0)
            world.RemoveCritterAt(world.GetCritter(0).Position);
        var parent = world.AddCritter(species, new GridPosition(16, 5));
        Assert.True(world.TryAssignApeToVillage(parent, camp));
        for (var meal = 0; meal < 10; meal++)
        {
            Assert.True(world.TryGetCritter(parent, out var before));
            var prey = new GridPosition(before.Position.X == 16 ? 17 : 16, 5);
            world.AddCritter(CritterSpecies.Elk, prey);
            world.CommitEncounter(before.Position, prey);
        }
        Assert.True(world.TryGetCritter(parent, out var ready));
        Assert.True(ready.CanReproduce);

        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond &&
            world.GetApeVillageResidentCount(camp) == 1; tick++)
            world.AdvanceOneTick();

        Assert.Equal(2, world.GetApeVillageResidentCount(camp));
        Assert.True(world.TryGetCritter(parent, out var after));
        Assert.True(Math.Abs(after.Position.X - camp.X) <= 1);
        Assert.Contains(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            critter => critter.Id != parent && world.IsBarbarianApe(critter.Id));
    }
}
