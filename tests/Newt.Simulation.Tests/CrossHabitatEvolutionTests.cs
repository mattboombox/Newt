using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CrossHabitatEvolutionTests
{
    [Theory]
    [InlineData(Terrain.Plains)]
    [InlineData(Terrain.Ocean)]
    public void EverySpeciesCanHaveABirthPositionOutsideItsHabitat(Terrain terrain)
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 2, terrain, seed: 17);
            world.AddCritter(CritterSpecies.Custodian, new(0, 0));
            var position = (GridPosition?)typeof(SimulationWorld)
                .GetMethod("FindBirthPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, [0, species, new HashSet<GridPosition>()]);
            Assert.Equal(new GridPosition(0, 1), position);
        }
    }

    [Theory]
    [InlineData(Terrain.Plains)]
    [InlineData(Terrain.Ocean)]
    public void EveryEvolutionAndDevolutionIgnoresCurrentHabitat(Terrain terrain)
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 1, terrain, seed: 17);
            var position = new GridPosition(0, 0);
            Assert.True(world.TrySpawnCritter(species, position));
            var original = world.GetCritter(0).Species;
            if (CritterEvolution.GetEvolvedSpeciesCount(original) > 0)
            {
                Assert.True(world.TryEvolveCritterAt(position), original.ToString());
                Assert.Equal(position, world.GetCritter(0).Position);
                Assert.True(world.TryDevolveCritterAt(position));
                Assert.Equal(original, world.GetCritter(0).Species);
            }
            if (CritterEvolution.TryGetDevolvedSpecies(original, out var ancestor))
            {
                Assert.True(world.TryDevolveCritterAt(position), original.ToString());
                Assert.Equal(ancestor, world.GetCritter(0).Species);
            }
        }
    }

    [Fact]
    public void TherapsidsGiveBirthToWhalesOnLandAndPayReproductionCost()
    {
        var whaleBirths = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var world = new SimulationWorld(1, 2, Terrain.Plains, seed);
            world.SeasonsEnabled = false;
            NaturalEvents.SetEnabled(world, false);
            world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
            var parent = world.AddCritter(CritterSpecies.Therapsid, new(0, 0));
            var nutrition = CritterNutritions.Get(CritterSpecies.Therapsid);
            var energies = (int[])typeof(SimulationWorld)
                .GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
            energies[0] = nutrition.MaximumEnergy;
            world.AdvanceOneTick();
            Assert.Equal(2, world.CritterCount);
            Assert.True(world.TryGetCritter(parent, out var after));
            Assert.Equal(nutrition.MaximumEnergy - nutrition.ReproductionCost, after.Energy);
            whaleBirths += world.GetCritterCount(CritterSpecies.ToothedWhale);
        }
        Assert.True(whaleBirths > 0);
    }
}
