using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BaleenWhaleTests
{
    [Fact]
    public void BaleenWhaleEvolvesFromToothedWhale()
    {
        Assert.Equal(1, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.ToothedWhale));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(
            CritterSpecies.ToothedWhale,
            out var evolved));
        Assert.Equal(CritterSpecies.BaleenWhale, evolved);
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(evolved, out var ancestor));
        Assert.Equal(CritterSpecies.ToothedWhale, ancestor);
    }

    [Fact]
    public void BaleenWhaleHasSameStatsAsToothedWhale()
    {
        Assert.Equal(
            CritterNutritions.Get(CritterSpecies.ToothedWhale),
            CritterNutritions.Get(CritterSpecies.BaleenWhale));
        Assert.Equal(
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.ToothedWhale),
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.BaleenWhale));
    }

    [Fact]
    public void BaleenWhaleLivesInSaltwaterAndEatsOnlyPlankton()
    {
        foreach (var terrain in new[] { Terrain.DeepOcean, Terrain.Ocean, Terrain.Shallows })
        {
            var world = new SimulationWorld(1, 1, terrain, seed: 5202);
            Assert.True(world.TryAddCritter(CritterSpecies.BaleenWhale, new GridPosition(0, 0)));
        }

        Assert.Equal(
            new HashSet<CritterSpecies> { CritterSpecies.Plankton },
            Enum.GetValues<CritterSpecies>()
                .Where(prey => SimulationWorld.CanEat(CritterSpecies.BaleenWhale, prey))
                .ToHashSet());
    }

    [Fact]
    public void BaleenWhalePursuesAndConsumesPlankton()
    {
        var world = new SimulationWorld(2, 1, Terrain.DeepOcean, seed: 5203);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.BaleenWhale, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(1, 0));

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.BaleenWhale));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
    }
}
