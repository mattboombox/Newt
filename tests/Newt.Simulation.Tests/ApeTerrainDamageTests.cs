using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTerrainDamageTests
{
    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void MountainRiversDoNotCauseMountainExposureDamage(CritterSpecies species)
    {
        var world = CreateWorld(species, Terrain.Mountain);
        world.SetSurfaceWater(new(0, 0), SurfaceWaterKind.River);
        var before = world.GetCritter(0).Energy;
        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.Equal(before, world.GetCritter(0).Energy);
        Assert.False(world.GetCritter(0).IsDamageFlashing);
    }

    public static IEnumerable<object[]> ExposureCases()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>().Where(species => species.ToString().StartsWith("Ape")))
        foreach (var terrain in new[] { Terrain.Mountain, Terrain.Ocean, Terrain.DeepOcean, Terrain.Plains, Terrain.Shallows })
        {
            var sailor = species is CritterSpecies.ApeSailor or CritterSpecies.ApeTraderSailor or
                CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber;
            var damage = terrain is Terrain.Mountain || !sailor && terrain is (Terrain.Ocean or Terrain.DeepOcean);
            yield return [species, terrain, damage];
        }
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeSailor)]
    public void PiratesTakeNoOceanExposureDamage(CritterSpecies species)
    {
        foreach (var terrain in new[] { Terrain.Ocean, Terrain.DeepOcean })
        {
            var world = new SimulationWorld(30, 20, Terrain.Plains) { SeasonsEnabled = false };
            NaturalEvents.SetEnabled(world, false);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetBiome(new(x, y), Biome.Grassland);
            var camp = new GridPosition(10, 10);
            Assert.True(world.TrySpawnBarbarianApeVillage(camp));
            while (world.CritterCount > 0)
                world.RemoveCritterAt(world.GetCritter(0).Position);
            var position = new GridPosition(20, 10);
            world.SetTerrain(position, Terrain.Beach);
            var id = world.AddCritter(species, position);
            Assert.True(world.TryAssignApeToVillage(id, camp));
            world.SetTerrain(position, terrain);
            Assert.True(world.IsApePirate(id));
            Assert.True(world.TryGetCritter(id, out var before));
            // Isolate exposure while keeping the camp membership and water permissions.
            foreach (var scheduleName in new[] { "_nextMovementTicks", "_nextMetabolismTicks" })
                ((long[])typeof(SimulationWorld).GetField(scheduleName, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(world)!)[0] = long.MaxValue;
            for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
                world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(id, out var after));
            Assert.Equal(before.Energy, after.Energy);
            Assert.False(after.IsDamageFlashing);
        }
    }

    [Theory]
    [MemberData(nameof(ExposureCases))]
    public void ExposureHurtsApesButSailorsAreSafeOnWater(CritterSpecies species, Terrain terrain, bool takesDamage)
    {
        var world = CreateWorld(species, terrain);
        var before = world.GetCritter(0).Energy;
        for (var tick = 0; tick < SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        var critter = world.GetCritter(0);
        Assert.Equal(before - (takesDamage ? 1 : 0), critter.Energy);
        Assert.Equal(takesDamage, critter.IsDamageFlashing);
        Assert.False(critter.IsSicknessDamageFlashing);
    }

    [Fact]
    public void ExposureCanKillAStrandedApe()
    {
        var world = CreateWorld(CritterSpecies.Ape, Terrain.Mountain);
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        energy[0] = 1;
        for (var tick = 0; tick < SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.Equal(0, world.CritterCount);
    }

    private static SimulationWorld CreateWorld(CritterSpecies species, Terrain terrain)
    {
        var world = new SimulationWorld(1, 1, terrain, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        Assert.True(world.TrySpawnCritter(species, new(0, 0)));
        // Isolate terrain exposure from the normal hunger schedule.
        var metabolism = (long[])typeof(SimulationWorld).GetField("_nextMetabolismTicks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        metabolism[0] = long.MaxValue;
        return world;
    }
}
