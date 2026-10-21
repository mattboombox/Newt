using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeVillageReachabilityTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape, false, false)]
    [InlineData(CritterSpecies.Ape, true, true)]
    [InlineData(CritterSpecies.ApeWarrior, false, true)]
    [InlineData(CritterSpecies.ApeSailor, false, true)]
    [InlineData(CritterSpecies.ApeFarmer, false, true)]
    public void PeriodicCheckDetachesOnlyUnreachableUnemployedApes(
        CritterSpecies species, bool landBridge, bool retainsMembership)
    {
        var world = new SimulationWorld(40, 20, Terrain.Plains, seed: 1701);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        foreach (var critter in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(critter.Position);

        // A nearby island is inside the claim radius, but ordinary apes cannot cross its moat.
        var island = new GridPosition(15, 10);
        for (var y = 9; y <= 11; y++)
        for (var x = 14; x <= 16; x++)
            if (new GridPosition(x, y) != island)
                world.SetTerrain(new GridPosition(x, y), Terrain.Ocean);
        if (landBridge)
            world.SetTerrain(new GridPosition(14, 10), Terrain.Plains);
        if (species is CritterSpecies.ApeSailor)
            world.SetTerrain(island, Terrain.Ocean);
        var id = world.AddCritter(species, island);
        Assert.True(world.TryAssignApeToVillage(id, village));
        world.CheckApeVillageReachability(0);
        Assert.Equal(village, world.GetApeHomeVillage(id));

        // Keep the test ape on its tile while advancing the periodic schedule.
        var tickProperty = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!;
        tickProperty.SetValue(world, (long)(30 * SimulationWorld.TicksPerSecond));
        world.CheckApeVillageReachability(0);
        Assert.Equal(village, world.GetApeHomeVillage(id));

        tickProperty.SetValue(world, (long)(5 * 60 * SimulationWorld.TicksPerSecond));
        world.CheckApeVillageReachability(0);
        Assert.Equal(retainsMembership ? (GridPosition?)village : null, world.GetApeHomeVillage(id));
    }
}
