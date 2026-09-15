using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianFoodTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.Dog)]
    public void CampResidentsHuntOnlyBelowQuarterCapacity(CritterSpecies species)
    {
        var world = new SimulationWorld(20, 20, Terrain.Beach, seed: 1);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        var camp = new GridPosition(10, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        while (world.CritterCount > 0)
            world.RemoveCritterAt(world.GetCritter(0).Position);
        var resident = world.AddCritter(species == CritterSpecies.Dog ? CritterSpecies.ApeWarrior : species, camp);
        Assert.True(world.TryAssignApeToVillage(resident, camp));
        if (species == CritterSpecies.Dog)
        {
            for (var roll = 0; roll < 5000; roll++)
                world.TryRecruitDogFromWolfKill(0);
            resident = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
                .First(critter => critter.Species == CritterSpecies.Dog).Id;
        }

        Assert.Equal(25, world.GetApeVillageFoodCapacity(camp));
        Assert.True(world.ShouldApeHunt(resident));
        world.StoreApeVillageFood(camp, 6);
        Assert.Equal(6, world.GetApeVillageFood(camp));
        Assert.True(world.ShouldApeHunt(resident));
        world.StoreApeVillageFood(camp, 1);
        Assert.Equal(7, world.GetApeVillageFood(camp));
        Assert.False(world.ShouldApeHunt(resident));
        world.StoreApeVillageFood(camp, 100);
        Assert.Equal(25, world.GetApeVillageFood(camp));
        Assert.False(world.ShouldApeHunt(resident));
    }
}
