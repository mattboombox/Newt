using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SnailTests
{
    [Theory]
    [InlineData(Biome.None)]
    [InlineData(Biome.Forest)]
    public void SnailCannotFeedOnBeaches(Biome biome)
    {
        var world = new SimulationWorld(2, 1, Terrain.Beach) { SeasonsEnabled = false };
        world.SetTemperature(new GridPosition(0, 0), 0.5f);
        world.SetTemperature(new GridPosition(1, 0), 0.5f);
        world.SetBiome(new(0, 0), biome);
        var snail = world.AddCritter(CritterSpecies.Snail, new(0, 0));
        world.AddCritter(CritterSpecies.Newt, new(1, 0));
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(false, feed.Invoke(world, [0]));
        Assert.Equal(false, feed.Invoke(world, [1]));
        Assert.True(world.TryGetCritter(snail, out var fed));
        Assert.Equal(CritterNutritions.Get(CritterSpecies.Snail).InitialEnergy, fed.Energy);
    }
    [Fact]
    public void NautilusCanEvolveIntoEitherSquidOrSnail()
    {
        Assert.Equal(2, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.Nautilus));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(CritterSpecies.Nautilus, 0, out var squid));
        Assert.Equal(CritterSpecies.Squid, squid);
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(CritterSpecies.Nautilus, 1, out var snail));
        Assert.Equal(CritterSpecies.Snail, snail);
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(CritterSpecies.Snail, out var ancestor));
        Assert.Equal(CritterSpecies.Nautilus, ancestor);
    }

    [Fact]
    public void SnailMatchesNewtStatsAndPredatorsButMovesMoreSlowly()
    {
        Assert.Equal(CritterNutritions.Get(CritterSpecies.Newt), CritterNutritions.Get(CritterSpecies.Snail));
        Assert.Equal(6 * SimulationWorld.TicksPerSecond, SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Snail));
        Assert.True(SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Snail) > SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Newt));
        foreach (var predator in Enum.GetValues<CritterSpecies>())
        {
            if (predator is not CritterSpecies.Monkey)
                Assert.Equal(SimulationWorld.CanEat(predator, CritterSpecies.Newt), SimulationWorld.CanEat(predator, CritterSpecies.Snail));
            if (!SimulationWorld.CanEat(predator, CritterSpecies.Snail)) continue;
            Assert.True(SimulationWorld.CanPursuePreyAtDistance(predator, CritterSpecies.Snail, 1));
            Assert.Equal(predator is CritterSpecies.Monkey,
                SimulationWorld.CanPursuePreyAtDistance(predator, CritterSpecies.Snail, 2));
        }
    }

    [Theory]
    [InlineData(Biome.Swamp, SurfaceWaterKind.None, true)]
    [InlineData(Biome.Jungle, SurfaceWaterKind.None, true)]
    [InlineData(Biome.Forest, SurfaceWaterKind.None, false)]
    [InlineData(Biome.Grassland, SurfaceWaterKind.River, false)]
    [InlineData(Biome.Grassland, SurfaceWaterKind.FreshwaterLake, false)]
    [InlineData(Biome.Grassland, SurfaceWaterKind.None, false)]
    public void SnailFeedsInWetlandsButNotForestOrFreshwater(Biome biome, SurfaceWaterKind water, bool feeds)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, biome);
        world.SetSurfaceWater(position, water);
        var id = world.AddCritter(CritterSpecies.Snail, position);
        var feed = typeof(SimulationWorld).GetMethod("TryFeedFromTerrain", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.Equal(feeds, feed.Invoke(world, [0]));
        Assert.True(world.TryGetCritter(id, out var snail));
        Assert.Equal(CritterNutritions.Get(CritterSpecies.Snail).InitialEnergy + (feeds ? 1 : 0), snail.Energy);
    }
}
