using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class LichPlagueLineageTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    private static Dictionary<int, int> Immunity(SimulationWorld world) =>
        (Dictionary<int, int>)typeof(SimulationWorld).GetField("_plagueImmunityRemainders", Private)!.GetValue(world)!;
    private static Dictionary<int, int> Army(SimulationWorld world) =>
        (Dictionary<int, int>)typeof(SimulationWorld).GetField("_lichArmy", Private)!.GetValue(world)!;
    private static int Remainder(SimulationWorld world, CritterId id) =>
        (int)typeof(SimulationWorld).GetMethod("GetPlagueImmunityRemainder", Private)!.Invoke(world, [Index(world, id)])!;
    private static bool Spread(SimulationWorld world, CritterId source, GridPosition target, int remainder) =>
        (bool)typeof(SimulationWorld).GetMethod("TrySpreadCritterPlague", Private)!
            .Invoke(world, [Index(world, source), target, PlagueKind.Zombie, remainder])!;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ConvertedZombiesAndLichAlwaysShareOneImmunityId(int remainder)
    {
        var world = new SimulationWorld(100, 5, Terrain.Plains, seed: 17);
        var lich = world.AddCritter(CritterSpecies.Lich, new(0, 2));
        Immunity(world)[lich.Value] = remainder;
        var convert = typeof(SimulationWorld).GetMethod("TryLichConversion", Private)!;
        var zombies = new List<CritterId>();
        for (var x = 10; x < 30; x++)
        {
            var victim = world.AddCritter(CritterSpecies.Ape, new(x, 2));
            // Conversion must replace any unrelated strain the victim previously carried.
            Immunity(world)[victim.Value] = 1 - remainder;
            Assert.True((bool)convert.Invoke(world, [Index(world, lich), Index(world, victim)])!);
            Assert.Equal(remainder, Remainder(world, victim));
            zombies.Add(victim);
        }
        var targets = Enumerable.Range(50, 20)
            .Select(x => world.AddCritter(CritterSpecies.Ape, new(x, 2))).ToArray();
        foreach (var source in zombies.Prepend(lich))
            foreach (var target in targets)
            {
                Assert.True(world.TryGetCritter(target, out var ape));
                Spread(world, source, ape.Position, 1 - remainder);
            }
        Assert.Equal(10, world.SickApeCount);
        foreach (var target in targets)
        {
            Assert.True(world.TryGetCritter(target, out var ape));
            Assert.Equal(target.Value % 2 == remainder ? PlagueKind.None : PlagueKind.Zombie, ape.Plague);
            if (ape.Plague is PlagueKind.Zombie)
                Assert.Equal(lich.Value, Army(world)[target.Value]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void InfectionReanimationAndLichDeathPreserveTheOriginalStrain(int remainder)
    {
        var world = new SimulationWorld(60, 5, Terrain.Plains, seed: 17);
        var lich = world.AddCritter(CritterSpecies.Lich, new(0, 2));
        Immunity(world)[lich.Value] = remainder;
        var candidates = Enumerable.Range(10, 20)
            .Select(x => world.AddCritter(CritterSpecies.Ape, new(x, 2))).ToArray();
        var susceptible = candidates.Where(id => id.Value % 2 != remainder).ToArray();
        var patient = susceptible[0];
        Assert.True(world.TryGetCritter(patient, out var before));
        Assert.True(Spread(world, lich, before.Position, remainder));
        Assert.Equal(lich.Value, Army(world)[patient.Value]);
        Assert.Equal(remainder, Remainder(world, patient));
        var reanimate = typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!;
        Assert.True((bool)reanimate.Invoke(world, [Index(world, patient)])!);
        Assert.True(world.TryGetCritter(patient, out var zombie));
        Assert.Equal(CritterSpecies.UndeadApe, zombie.Species);
        Assert.Equal(remainder, Remainder(world, patient));
        var second = susceptible[1];
        Assert.True(world.TryGetCritter(second, out var neighbor));
        Assert.True(Spread(world, patient, neighbor.Position, Remainder(world, patient)));
        Assert.Equal(lich.Value, Army(world)[second.Value]);
        Assert.True((bool)reanimate.Invoke(world, [Index(world, second)])!);
        Assert.Equal(remainder, Remainder(world, second));
        // Removing the lich compacts arrays and detaches surviving zombies.
        Assert.True(world.RemoveCritterAt(new(0, 2)));
        Assert.Equal(remainder, Remainder(world, patient));
        Assert.Equal(remainder, Remainder(world, second));
        foreach (var target in candidates.Where(id => id != patient && id != second))
        {
            Assert.True(world.TryGetCritter(target, out var ape));
            Spread(world, second, ape.Position, Remainder(world, second));
            Assert.True(world.TryGetCritter(target, out var after));
            Assert.Equal(target.Value % 2 == remainder ? PlagueKind.None : PlagueKind.Zombie, after.Plague);
        }
    }
}
