using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class UndeadWaterTests
{
    [Theory]
    [InlineData(CritterSpecies.Lich)]
    [InlineData(CritterSpecies.UndeadWarrior)]
    public void UndeadCanOccupyWaterAndMovesAtOneThirdLandSpeed(CritterSpecies species)
    {
        foreach (var (terrain, water) in new[]
        {
            (Terrain.Shallows, SurfaceWaterKind.None),
            (Terrain.Ocean, SurfaceWaterKind.None),
            (Terrain.DeepOcean, SurfaceWaterKind.None),
            (Terrain.Plains, SurfaceWaterKind.FreshwaterLake),
            (Terrain.Plains, SurfaceWaterKind.River),
        })
        {
            var world = new SimulationWorld(20, 20, terrain, seed: 17);
            world.SeasonsEnabled = false;
            NaturalEvents.SetEnabled(world, false);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetSurfaceWater(new(x, y), water);
            var id = world.AddCritter(species, new(10, 10));
            Assert.Equal(9 * SimulationWorld.TicksPerSecond, world.GetCritterMovementIntervalTicks(0));
            var schedule = (long[])typeof(SimulationWorld).GetField("_nextMovementTicks",
                BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(world)!;
            schedule[0] = 1;
            world.AdvanceOneTick();
            Assert.Equal(1L + 9 * SimulationWorld.TicksPerSecond, schedule[0]);
            for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond; tick++)
                world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(id, out var critter));
            Assert.Equal(CritterNutritions.Get(species).InitialEnergy, critter.Energy);
        }
        var land = new SimulationWorld(20, 20, Terrain.Plains);
        land.AddCritter(species, new(10, 10));
        Assert.Equal(3 * SimulationWorld.TicksPerSecond, land.GetCritterMovementIntervalTicks(0));
        Assert.False(CritterHabitats.CanOccupy(CritterHabitats.GetHabitat(species), Terrain.Mountain, SurfaceWaterKind.None));
        Assert.False(CritterHabitats.CanOccupy(CritterHabitats.GetHabitat(species), Terrain.Plains,
            SurfaceWaterKind.None, surfaceCover: SurfaceCover.Lava));
    }
}
