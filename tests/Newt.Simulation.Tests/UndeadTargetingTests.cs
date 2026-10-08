using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class UndeadTargetingTests
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    public static IEnumerable<object[]> ApeTargets()
    {
        foreach (var hunter in new[] { CritterSpecies.Lich, CritterSpecies.UndeadApe, CritterSpecies.UndeadWarrior })
        foreach (var prey in new[] { CritterSpecies.Ape, CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack,
            CritterSpecies.ApeScholar, CritterSpecies.ApeChieftain, CritterSpecies.ApeWarrior,
            CritterSpecies.ApeSailor, CritterSpecies.ApeTrader, CritterSpecies.ApeTraderSailor })
        foreach (var barbarian in new[] { false, true })
            yield return [hunter, prey, barbarian];
    }

    [Theory]
    [MemberData(nameof(ApeTargets))]
    public void EveryUndeadHunterPursuesEveryApeRoleRegardlessOfFaction(
        CritterSpecies hunter, CritterSpecies prey, bool barbarian)
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(barbarian ? world.TrySpawnBarbarianApeVillage(village) : world.TrySpawnTestApeVillage(village));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        world.AddCritter(hunter, new(15, 10));
        if (prey is CritterSpecies.ApeSailor or CritterSpecies.ApeTraderSailor)
            world.SetTerrain(new(16, 10), Terrain.Shallows);
        var victim = world.AddCritter(prey, new(16, 10));
        // Include faction affiliation even for trader roles not normally recruited by camps.
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        homes[victim.Value] = village.Y * world.Width + village.X;
        Assert.Equal(barbarian, world.IsBarbarianApe(victim));
        Assert.True(SimulationWorld.CanEat(hunter, prey));
        Assert.True((bool)typeof(SimulationWorld).GetMethod("CanPursuePrey", Private)!.Invoke(world, [0, 1])!);
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energy[1] = 1;
        world.CommitEncounter(new(15, 10), new(16, 10));
        Assert.True(world.GetCritter(0).IsDamageFlashing ||
            !world.TryGetCritter(victim, out var after) || after.IsDamageFlashing || after.Species != prey);
    }

    [Theory]
    [InlineData(CritterSpecies.Lich)]
    [InlineData(CritterSpecies.UndeadApe)]
    [InlineData(CritterSpecies.UndeadWarrior)]
    public void ColonistsAreValidTargetsForAllUndeadHunters(CritterSpecies hunter)
    {
        var world = new SimulationWorld(70, 30, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnBarbarianApeVillage(new(10, 10)));
        Assert.True(world.TrySendApeColonist(new(40, 15)));
        var colonist = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).Single(critter => critter.IsColonist);
        var id = world.AddCritter(hunter, new(55, 25));
        int Index(CritterId critter) => Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == critter);
        Assert.True((bool)typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", Private)!
            .Invoke(world, [Index(id), Index(colonist.Id)])!);
    }
}
