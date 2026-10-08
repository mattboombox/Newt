using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeRecruitmentHealthTests
{
    public static IEnumerable<object[]> JobsAndConditions()
    {
        var jobs = new[] { CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack,
            CritterSpecies.ApeScholar, CritterSpecies.ApeSailor, CritterSpecies.ApeWarrior,
            CritterSpecies.ApeChieftain, CritterSpecies.ApeKing, CritterSpecies.ApeQueen,
            CritterSpecies.ApeLudditePriest, CritterSpecies.ApeBishop, CritterSpecies.ApeRoyalGuard,
            CritterSpecies.ApeBomber };
        foreach (var job in jobs)
            foreach (var condition in new[] { PlagueKind.Plague, PlagueKind.Zombie, PlagueKind.Vampire, PlagueKind.None })
                yield return new object[] { job, condition };
    }

    [Theory]
    [MemberData(nameof(JobsAndConditions))]
    public void SickAndUndeadResidentsCannotBecomeSpecialists(CritterSpecies job, PlagueKind condition)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains);
        var position = new GridPosition(0, 0);
        var original = condition == PlagueKind.None ? CritterSpecies.UndeadApe : CritterSpecies.Ape;
        var id = world.AddCritter(original, position);
        if (condition != PlagueKind.None)
            Assert.True(world.TryInfectApeAt(position, condition, (id.Value + 1) % 2));
        typeof(SimulationWorld).GetMethod("ChangeCritterSpecies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [0, job, true, true]);
        Assert.True(world.TryGetCritter(id, out var resident));
        Assert.Equal(original, resident.Species);
        Assert.Equal(condition == PlagueKind.None ? PlagueKind.Zombie : condition, resident.Plague);
        Assert.Equal(0, world.GetCritterCount(job));
    }

    [Fact]
    public void MilitaryRecruitmentSkipsSickCiviliansWithoutConsumingThem()
    {
        var world = new SimulationWorld(30, 30, Terrain.Plains) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var tile = 0; tile < world.Width * world.Height; tile++)
            world.SetBiome(new(tile % world.Width, tile / world.Width), Biome.Grassland);
        var village = new GridPosition(15, 15);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var residents = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        foreach (var resident in residents)
            Assert.True(world.TryInfectApeAt(resident.Position, PlagueKind.Plague, (resident.Id.Value + 1) % 2));
        var district = Enumerable.Range(0, world.Width * world.Height).Select(i => new GridPosition(i % world.Width, i / world.Width))
            .First(p => world.TryPlaceApeBuilding(p, ApeStructureKind.MilitaryDistrict));
        var recruited = typeof(SimulationWorld).GetMethod("TryRecruitApeWarrior", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [village.Y * world.Width + village.X, district.Y * world.Width + district.X]);
        Assert.Equal(false, recruited);
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.ApeWarrior));
        Assert.Equal(residents.Length, world.SickApeCount);
    }
}
