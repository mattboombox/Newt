using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillagePopulationTests
{
    [Fact]
    public void VillageAndNecropolisAgesUseTheirStructureFoundingTimes()
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains) { ApeVillageMergingEnabled = false };
        SetGrassland(world);
        var tick = typeof(SimulationWorld).GetProperty(nameof(world.Tick))!;
        tick.SetValue(world, 100L);
        Assert.True(world.TrySpawnTestApeVillage(new(10, 10)));
        tick.SetValue(world, 150L);
        Assert.True(world.TryPlaceNecropolis(new(35, 15)));
        tick.SetValue(world, 200L);
        Assert.True(world.TrySpawnTestApeVillage(new(60, 10)));
        Assert.Equal(new long[] { 100, 150, 200 }, world.GetVillagePopulations()
            .OrderBy(row => row.FoundedTick).Select(row => row.FoundedTick));
    }
    private static void SetGrassland(SimulationWorld world)
    {
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
    }
    [Fact]
    public void CountsEachRoleByHomeVillageAndUpdatesAfterResidentRemoval()
    {
        var world = new SimulationWorld(60, 40, Terrain.Plains) { SeasonsEnabled = false };
        SetGrassland(world);
        var normal = new GridPosition(10, 10);
        var camp = new GridPosition(40, 25);
        Assert.True(world.TrySpawnTestApeVillage(normal));
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        world.AddCritter(CritterSpecies.Ape, new(0, 0)); // No home: excluded from village totals.
        var populations = world.GetVillagePopulations();
        Assert.Equal(2, populations.Count);
        Assert.Equal(populations.OrderBy(village => village.Id), populations);
        var settlement = Assert.Single(populations, village => village.Position == normal);
        Assert.False(settlement.Barbarian);
        Assert.Equal(2, settlement.ApeCount);
        Assert.Equal(2, settlement.Counts[CritterSpecies.Ape]);
        var barbarians = Assert.Single(populations, village => village.Position == camp);
        Assert.True(barbarians.Barbarian);
        Assert.Equal(10, barbarians.ApeCount);
        Assert.Equal(9, barbarians.Counts[CritterSpecies.ApeWarrior]);
        Assert.Equal(1, barbarians.Counts[CritterSpecies.ApeChieftain]);
        var warrior = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .First(critter => critter.Species == CritterSpecies.ApeWarrior);
        world.RemoveCritterAt(warrior.Position);
        var updated = Assert.Single(world.GetVillagePopulations(), village => village.Position == camp);
        Assert.Equal(9, updated.ApeCount);
        Assert.Equal(8, updated.Counts[CritterSpecies.ApeWarrior]);
        Assert.Equal(10, barbarians.ApeCount); // A snapshot remains unchanged.
        Assert.Equal(world.GetApeVillageResidentCount(camp), updated.ApeCount);
        Assert.True(world.RemoveApeStructureAt(normal));
        Assert.DoesNotContain(world.GetVillagePopulations(), village => village.Position == normal);
    }

    [Fact]
    public void DogsAppearAsTheirOwnRoleAndDoNotIncreaseTheApeTotal()
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains) { SeasonsEnabled = false };
        SetGrassland(world);
        var camp = new GridPosition(15, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        var chief = Enumerable.Range(0, world.CritterCount)
            .Single(index => world.GetCritter(index).Species == CritterSpecies.ApeChieftain);
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(camp) < 2; roll++)
            world.TryRecruitDogFromWolfKill(chief);
        var village = Assert.Single(world.GetVillagePopulations());
        Assert.Equal(10, village.ApeCount);
        Assert.Equal(2, village.Counts[CritterSpecies.Dog]);
    }
}
