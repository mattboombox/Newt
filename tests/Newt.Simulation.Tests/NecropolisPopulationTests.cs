using System.Reflection;
using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class NecropolisPopulationTests
{
    [Fact]
    public void OwnedUndeadAwayFromHomeAreNotCountedTwiceThroughRetainedVillageMembership()
    {
        var world = World(out var first, out _);
        var village = new GridPosition(65, 25);
        for (var y = 23; y <= 27; y++)
        for (var x = 63; x <= 67; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var position = new GridPosition(25, 20);
        var ape = VulnerableApe(world, position);
        Assert.True(world.TryInfectCritterAt(position, PlagueKind.Zombie, 0, first.Value));
        Rise(world, ape);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        homes[ape.Value] = village.Y * world.Width + village.X; // Legacy retained home must not duplicate an undead row.
        var away = new GridPosition(Second.X + 1, Second.Y + 1);
        typeof(SimulationWorld).GetMethod("MoveCritter", Private)!
            .Invoke(world, [Index(world, ape), away.Y * world.Width + away.X, away, false]);
        Assert.Equal(6, Population(world, First).ApeCount);
        Assert.Equal(5, Population(world, Second).ApeCount);
        Assert.Equal(2, Population(world, village).ApeCount);
        Assert.NotEqual(world.GetPopulationSettlementId(First), world.GetPopulationSettlementId(village));
        Assert.NotEqual(ape, PopulationCounts.FindCritter(world, CritterSpecies.UndeadApe, village: village)!.Value.Id);
        Assert.True(world.RemoveNecropolisAt(First));
        Assert.Equal(2, Population(world, village).ApeCount);
        Assert.Equal(5, Population(world, Second).ApeCount);
        Assert.NotEqual(ape, PopulationCounts.FindCritter(world, CritterSpecies.UndeadApe, village: village)!.Value.Id);
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition First = new(10, 10), Second = new(40, 10);
    private static SimulationWorld World(out CritterId first, out CritterId second)
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        Assert.True(world.TryPlaceNecropolis(First));
        Assert.True(world.TryPlaceNecropolis(Second));
        first = world.GetNecropolis(First)!.Lich;
        second = world.GetNecropolis(Second)!.Lich;
        return world;
    }
    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    private static CritterId VulnerableApe(SimulationWorld world, GridPosition position)
    {
        var ape = world.AddCritter(CritterSpecies.Ape, position);
        if (ape.Value % 2 == 0)
        {
            world.RemoveCritterAt(position);
            ape = world.AddCritter(CritterSpecies.Ape, position);
        }
        return ape;
    }
    private static void Rise(SimulationWorld world, CritterId id) =>
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryReanimateApe", Private)!
            .Invoke(world, [Index(world, id)])!);
    private static VillagePopulationSnapshot Population(SimulationWorld world, GridPosition position) =>
        Assert.Single(world.GetVillagePopulations(), row => row.Position == position);

    [Fact]
    public void OverlappingZombieOutbreaksKeepOriginalOwnerAndExcludeWildUndead()
    {
        var world = World(out var first, out var second);
        var aPosition = new GridPosition(25, 20);
        var bPosition = new GridPosition(26, 20);
        var wildPosition = new GridPosition(27, 20);
        var a = VulnerableApe(world, aPosition);
        var b = VulnerableApe(world, bPosition);
        var wild = VulnerableApe(world, wildPosition);
        Assert.True(world.TryInfectCritterAt(aPosition, PlagueKind.Zombie, 0, first.Value));
        Assert.True(world.TryInfectCritterAt(bPosition, PlagueKind.Zombie, 0, second.Value));
        Assert.True(world.TryInfectCritterAt(wildPosition, PlagueKind.Zombie, 0));
        // Matching immunity remainders do not imply matching army ownership.
        for (var i = 0; i < 5; i++)
        {
            Assert.False(world.TryInfectCritterAt(aPosition, PlagueKind.Zombie, 0, second.Value));
            Assert.False(world.TryInfectCritterAt(bPosition, PlagueKind.Zombie, 0, first.Value));
            Assert.False(world.TryInfectCritterAt(wildPosition, PlagueKind.Zombie, 0, first.Value));
        }
        Assert.Equal(5, Population(world, First).ApeCount); // Infected living apes are not undead residents yet.
        Rise(world, a);
        Rise(world, b);
        Rise(world, wild);
        Assert.Equal(6, Population(world, First).ApeCount);
        Assert.Equal(6, Population(world, Second).ApeCount);
        Assert.Equal(1, Population(world, First).Counts[CritterSpecies.UndeadApe]);
        Assert.Equal(4, Population(world, First).Counts[CritterSpecies.UndeadWarrior]);
        Assert.Equal(First, world.GetNecropolisPopulationHome(a));
        Assert.Equal(Second, world.GetNecropolisPopulationHome(b));
        Assert.Null(world.GetNecropolisPopulationHome(wild));
        Assert.Equal(a, PopulationCounts.FindCritter(world, CritterSpecies.UndeadApe, village: First)!.Value.Id);
        Assert.Equal(b, PopulationCounts.FindCritter(world, CritterSpecies.UndeadApe, village: Second)!.Value.Id);
        var old = Population(world, First);
        Rise(world, a);
        Assert.Equal(0, Population(world, First).Counts.GetValueOrDefault(CritterSpecies.UndeadApe));
        Assert.Equal(5, Population(world, First).Counts[CritterSpecies.UndeadWarrior]);
        Assert.Equal(1, old.Counts[CritterSpecies.UndeadApe]); // History samples remain immutable.
    }

    [Fact]
    public void DescendantSpreadRetainsOwnershipAndLichDeathDoesNotTransferOrphans()
    {
        var world = World(out var first, out var second);
        var sourcePosition = new GridPosition(25, 20);
        var targetPosition = new GridPosition(26, 20);
        var source = VulnerableApe(world, sourcePosition);
        var target = VulnerableApe(world, targetPosition);
        Assert.True(world.TryInfectCritterAt(sourcePosition, PlagueKind.Zombie, 0, first.Value));
        Rise(world, source);
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TrySpreadCritterPlague", Private)!
            .Invoke(world, [Index(world, source), targetPosition, PlagueKind.Zombie, 0])!);
        Assert.False(world.TryInfectCritterAt(targetPosition, PlagueKind.Zombie, 0, second.Value));
        Rise(world, target);
        Assert.Equal(7, Population(world, First).ApeCount);
        Assert.Equal(5, Population(world, Second).ApeCount);
        Assert.NotEqual(world.GetPopulationSettlementId(First), world.GetPopulationSettlementId(Second));
        world.RemoveCritterAt(First);
        Assert.DoesNotContain(world.GetVillagePopulations(), row => row.Position == First);
        Assert.Null(world.GetNecropolisPopulationHome(source));
        Assert.Null(world.GetNecropolisPopulationHome(target));
        Assert.False(world.TryInfectCritterAt(targetPosition, PlagueKind.Zombie, 0, second.Value));
        Assert.Equal(5, Population(world, Second).ApeCount);
        Assert.Null(PopulationCounts.FindCritter(world, CritterSpecies.UndeadApe, village: Second));
    }

    [Fact]
    public void StrongerVampirePlagueRemovesPriorZombieOwnership()
    {
        var world = World(out var first, out _);
        var position = new GridPosition(25, 20);
        var ape = VulnerableApe(world, position);
        Assert.True(world.TryInfectCritterAt(position, PlagueKind.Zombie, 0, first.Value));
        Assert.True(world.TryInfectCritterAt(position, PlagueKind.Vampire, 0));
        Rise(world, ape);
        Assert.Null(world.GetNecropolisPopulationHome(ape));
        Assert.Equal(5, Population(world, First).ApeCount);
    }
}
