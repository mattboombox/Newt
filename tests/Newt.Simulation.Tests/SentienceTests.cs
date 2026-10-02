using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SentienceTests
{
    [Fact]
    public void SentienceControlsMonkeyOffspringAndCanBeReenabled()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
        Assert.True(world.SentienceEnabled);
        Assert.Equal(CritterSpecies.Ape, world.ChooseOffspringSpecies(CritterSpecies.Monkey));

        world.SentienceEnabled = false;
        for (var i = 0; i < 100; i++)
            Assert.Equal(CritterSpecies.Monkey, world.ChooseOffspringSpecies(CritterSpecies.Monkey));
        Assert.Equal(CritterSpecies.MegaSpider, world.ChooseOffspringSpecies(CritterSpecies.SeaScorpion));
        Assert.Equal(CritterSpecies.Ape, world.ChooseOffspringSpecies(CritterSpecies.ApeFarmer));

        world.SentienceEnabled = true;
        Assert.Equal(CritterSpecies.Ape, world.ChooseOffspringSpecies(CritterSpecies.Monkey));
    }

    [Fact]
    public void ManualEvolutionRespectsSentienceWithoutRemovingExistingApes()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        var position = new GridPosition(5, 5);
        world.AddCritter(CritterSpecies.Monkey, position);
        world.SentienceEnabled = false;
        Assert.False(world.TryEvolveCritterAt(position));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));

        world.SentienceEnabled = true;
        Assert.True(world.TryEvolveCritterAt(position));
        world.SentienceEnabled = false;
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
        Assert.True(world.TryDevolveCritterAt(position));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
    }
}
