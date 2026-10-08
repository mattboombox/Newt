using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlagueImmunityTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void LichBorneZombiePlagueProtectsHalfEvenAfterRepeatedExposure(int remainder)
    {
        var world = new SimulationWorld(21, 1, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Lich, new(0, 0));
        var spread = typeof(SimulationWorld).GetMethod("TrySpreadCritterPlague",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        for (var x = 1; x <= 20; x++) world.AddCritter(CritterSpecies.Ape, new(x, 0));
        for (var exposure = 0; exposure < 5; exposure++)
            for (var x = 1; x <= 20; x++)
                spread.Invoke(world, [0, new GridPosition(x, 0), PlagueKind.Zombie, remainder]);
        Assert.Equal(10, world.SickApeCount);
        for (var index = 1; index < world.CritterCount; index++)
        {
            var ape = world.GetCritter(index);
            Assert.Equal(ape.Id.Value % 2 == remainder ? PlagueKind.None : PlagueKind.Zombie, ape.Plague);
        }
    }
    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    [InlineData(PlagueKind.Vampire)]
    public void EachRemainderProtectsExactlyHalfAndOtherOutbreaksCanInfectSurvivors(PlagueKind kind)
    {
        for (var remainder = 0; remainder < 2; remainder++)
        {
            var world = new SimulationWorld(20, 1, Terrain.Plains, seed: 17);
            var immune = new List<GridPosition>();
            for (var x = 0; x < 20; x++)
            {
                var position = new GridPosition(x, 0);
                var id = world.AddCritter(CritterSpecies.Ape, position);
                var infected = world.TryInfectApeAt(position, kind, remainder);
                Assert.Equal(id.Value % 2 != remainder, infected);
                if (!infected) immune.Add(position);
            }
            Assert.Equal(10, immune.Count);
            foreach (var position in immune)
                Assert.True(world.TryInfectApeAt(position, kind, (remainder + 1) % 2));
        }
    }

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    [InlineData(PlagueKind.Vampire)]
    public void NewOutbreaksCanProtectEitherParity(PlagueKind kind)
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
