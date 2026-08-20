using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class DeerEvolutionTests
{
    [Theory]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Jungle)]
    public void DeerCanBePlacedOnWetlandBiomesWithoutFeedingThere(Biome biome)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 1801);
        var position = new GridPosition(0, 0);
        world.SeasonsEnabled = false;
        world.SetBiome(position, biome);

        Assert.True(world.TryAddCritter(CritterSpecies.Deer, position));

        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void EvolvedDeerPersistsBesideItsNonPredatoryTherapsidParent()
    {
        SimulationWorld? deerWorld = null;
        CritterId deerId = default;
        for (ulong seed = 1; seed <= 30 && deerWorld is null; seed++)
        {
            var world = new SimulationWorld(2, 1, Terrain.Plains, seed);
            world.SeasonsEnabled = false;
            world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
            world.SetBiome(new GridPosition(0, 0), Biome.Jungle);
            world.SetBiome(new GridPosition(1, 0), Biome.Jungle);
            world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));

            for (var tick = 0; tick < 4 * 60 * SimulationWorld.TicksPerSecond; tick++)
            {
                world.AdvanceOneTick();
                var deer = Enumerable.Range(0, world.CritterCount)
                    .Select(world.GetCritter)
                    .FirstOrDefault(critter => critter.Species is CritterSpecies.Deer);
                if (deer.Id.IsValid)
                {
                    deerWorld = world;
                    deerId = deer.Id;
                    break;
                }
            }
        }

        Assert.NotNull(deerWorld);
        for (var tick = 0; tick < 20 * SimulationWorld.TicksPerSecond; tick++)
        {
            deerWorld.AdvanceOneTick();
        }

        Assert.True(deerWorld.TryGetCritter(deerId, out var survivingDeer));
        Assert.Equal(CritterSpecies.Deer, survivingDeer.Species);
    }
}
