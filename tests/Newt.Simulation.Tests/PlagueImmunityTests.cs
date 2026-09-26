using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlagueImmunityTests
{
    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    [InlineData(PlagueKind.Vampire)]
    public void EachRemainderProtectsExactlyOneInFiveAndOtherOutbreaksCanInfectSurvivors(PlagueKind kind)
    {
        for (var remainder = 0; remainder < 5; remainder++)
        {
            var world = new SimulationWorld(25, 1, Terrain.Plains, seed: 17);
            var immune = new List<GridPosition>();
            for (var x = 0; x < 25; x++)
            {
                var position = new GridPosition(x, 0);
                var id = world.AddCritter(CritterSpecies.Ape, position);
                var infected = world.TryInfectApeAt(position, kind, remainder);
                Assert.Equal(id.Value % 5 != remainder, infected);
                if (!infected) immune.Add(position);
            }
            Assert.Equal(5, immune.Count);
            foreach (var position in immune)
                Assert.True(world.TryInfectApeAt(position, kind, (remainder + 1) % 5));
        }
    }

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    [InlineData(PlagueKind.Vampire)]
    public void NewOutbreaksDoNotAlwaysProtectIdsDivisibleByFive(PlagueKind kind)
    {
        var outcomes = new HashSet<bool>();
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var world = new SimulationWorld(5, 1, Terrain.Plains, seed: seed);
            for (var x = 0; x < 5; x++) world.AddCritter(CritterSpecies.Ape, new(x, 0));
            outcomes.Add(world.TryInfectApeAt(new(4, 0), kind));
        }
        Assert.Contains(true, outcomes);
        Assert.Contains(false, outcomes);
    }
}
