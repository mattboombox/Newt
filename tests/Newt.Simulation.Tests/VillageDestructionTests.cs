using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillageDestructionTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Village = new(15, 15), Survivor = new(25, 15);

    public static IEnumerable<object[]> Roles() => new[]
    {
        CritterSpecies.Ape, CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack,
        CritterSpecies.ApeScholar, CritterSpecies.ApeSailor, CritterSpecies.ApeWarrior,
        CritterSpecies.ApeChieftain, CritterSpecies.ApeKing, CritterSpecies.ApeQueen,
        CritterSpecies.ApeRoyalGuard, CritterSpecies.ApeBishop, CritterSpecies.ApeLudditePriest,
        CritterSpecies.ApeBomber, CritterSpecies.ApeTrader, CritterSpecies.ApeTraderSailor,
    }.SelectMany(role => new[] { new object[] { role, false }, new object[] { role, true } });

    private static (SimulationWorld World, CritterId Ape) Create(CritterSpecies role, bool barbarian)
    {
        var world = new SimulationWorld(50, 30, Terrain.Plains, seed: 17) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(barbarian ? world.TrySpawnBarbarianApeVillage(Village) : world.TrySpawnTestApeVillage(Village));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        if (role is CritterSpecies.ApeSailor or CritterSpecies.ApeTraderSailor)
            world.SetTerrain(Survivor, role is CritterSpecies.ApeTraderSailor ? Terrain.Ocean : Terrain.Beach);
        world.SetBiome(Survivor, Biome.Jungle);
        var ape = world.AddCritter(role, Survivor);
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!)
            [ape.Value] = Village.Y * world.Width + Village.X;
        ((int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!)[0] = 2;
        return (world, ape);
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void DestroyedSettlementResetsSurvivorRolesAndAllowsJungleForaging(CritterSpecies role, bool barbarian)
    {
        var (world, ape) = Create(role, barbarian);
        Assert.True(world.RemoveApeStructureAt(Village));
        Assert.True(world.TryGetCritter(ape, out var survivor));
        Assert.Equal(CritterSpecies.Ape, survivor.Species);
        Assert.Equal(2, survivor.Energy);
        Assert.Null(world.GetApeHomeVillage(ape));
        Assert.False(world.IsBarbarianApe(ape));
        Assert.False(survivor.IsVeteranWarrior);
        world.SetTerrain(Survivor, Terrain.Plains);
        typeof(SimulationWorld).GetMethod("TryMoveApe", Private)!.Invoke(world, [0, null]);
        Assert.True(world.TryGetCritter(ape, out survivor));
        Assert.Equal(3, survivor.Energy);
    }

    [Fact]
    public void MeteorSurvivingChiefBecomesAnUnsettledApeAndForages()
    {
        var (world, ape) = Create(CritterSpecies.ApeChieftain, false);
        Impacts.CreateMeteorImpact(world, Village, 0f);
        Assert.Null(world.GetApeStructure(Village));
        Assert.True(world.TryGetCritter(ape, out var survivor));
        Assert.Equal(CritterSpecies.Ape, survivor.Species);
        Assert.Null(world.GetApeHomeVillage(ape));
        // Impact climate rebuilding can change untouched biomes; provide surviving jungle.
        world.SetTerrain(Survivor, Terrain.Plains);
        world.SetBiome(Survivor, Biome.Jungle);
        typeof(SimulationWorld).GetMethod("TryMoveApe", Private)!.Invoke(world, [0, null]);
        Assert.True(world.TryGetCritter(ape, out var foraging));
        Assert.True(foraging.Energy > survivor.Energy);
    }
}
