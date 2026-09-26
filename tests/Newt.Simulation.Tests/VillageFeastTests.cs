using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillageFeastTests
{
    private static readonly GridPosition Village = new(10, 10);

    private static SimulationWorld CreateWorld(ulong seed = 17)
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        return world;
    }

    private static void SetResidentEnergy(SimulationWorld world, int energy)
    {
        var values = (int[])typeof(SimulationWorld).GetField("_energy",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        for (var i = 0; i < world.CritterCount; i++)
            values[i] = energy;
    }

    [Fact]
    public void FeastSpendsStoredFoodToReachReproductionEnergy()
    {
        var world = CreateWorld();
        SetResidentEnergy(world, 11);
        world.StoreApeVillageFood(Village, 2);
        Assert.True(world.TryStartVillageFeast(Village));
        Assert.Equal(0, world.GetApeVillageFood(Village));
        Assert.False(world.IsVillageFeasting(Village));
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), ape =>
        {
            Assert.Equal(12, ape.Energy);
            Assert.True(ape.CanReproduce);
        });
    }

    [Fact]
    public void RandomSelectionFullyFeedsOneApeBeforeTheNext()
    {
        var winners = new HashSet<int>();
        for (ulong seed = 1; seed <= 50; seed++)
        {
            var world = CreateWorld(seed);
            SetResidentEnergy(world, 10);
            world.StoreApeVillageFood(Village, 3);
            Assert.True(world.TryStartVillageFeast(Village));
            var apes = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
            Assert.Equal(new[] { 11, 12 }, apes.Select(ape => ape.Energy).Order().ToArray());
            winners.Add(apes.Single(ape => ape.Energy == 12).Id.Value);
            Assert.Equal(0, world.GetApeVillageFood(Village));
        }
        Assert.Equal(2, winners.Count);
    }

    [Fact]
    public void SurplusRemainsInFeastUntilResidentsCanEatAgain()
    {
        var world = CreateWorld();
        SetResidentEnergy(world, 11);
        world.StoreApeVillageFood(Village, 5);
        Assert.True(world.TryStartVillageFeast(Village));
        Assert.Equal(3, world.GetApeVillageFood(Village));
        Assert.True(world.IsVillageFeasting(Village));
        Assert.False(world.TryStartVillageFeast(Village));
        for (var tick = 0; tick < 100 && world.IsVillageFeasting(Village); tick++)
            world.AdvanceOneTick();
        Assert.False(world.IsVillageFeasting(Village));
        Assert.Equal(0, world.GetApeVillageFood(Village));
    }

    [Fact]
    public void FeastDoesNotFeedSickApesOrUnassignedOutsiders()
    {
        var world = CreateWorld();
        SetResidentEnergy(world, 11);
        var sick = world.GetCritter(0);
        Assert.True(world.TryInfectApeAt(sick.Position, PlagueKind.Plague, 0));
        var outsider = world.AddCritter(CritterSpecies.Ape, new(30, 20));
        world.StoreApeVillageFood(Village, 1);
        Assert.True(world.TryStartVillageFeast(Village));
        Assert.True(world.TryGetCritter(sick.Id, out var unchanged));
        Assert.Equal(11, unchanged.Energy);
        Assert.True(world.TryGetCritter(outsider, out var remote));
        Assert.Equal(6, remote.Energy);
        Assert.Equal(12, world.GetCritter(1).Energy);
    }

    [Fact]
    public void OnlyVillagesWithFoodCanFeastAndDestructionClearsEvent()
    {
        var world = CreateWorld();
        Assert.False(world.TryStartVillageFeast(Village));
        Assert.False(world.TryStartVillageFeast(new(-1, -1)));
        Assert.False(world.TryStartVillageFeast(new(30, 20)));
        var camp = new GridPosition(30, 20);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        world.StoreApeVillageFood(camp, 5);
        Assert.False(world.TryStartVillageFeast(camp));
        SetResidentEnergy(world, 12);
        world.StoreApeVillageFood(Village, 5);
        Assert.True(world.TryStartVillageFeast(Village));
        Assert.True(world.RemoveApeStructureAt(Village));
        Assert.False(world.IsVillageFeasting(Village));
    }

    [Fact]
    public void NaturalFeastsRequireNaturalEventsAndOnlyTargetVillages()
    {
        var world = CreateWorld();
        world.StoreApeVillageFood(Village, 5);
        var tile = Village.Y * world.Width + Village.X;
        for (var attempt = 0; attempt < 100; attempt++)
            Assert.False(world.TryStartNaturalVillageFeast(tile));
        NaturalEvents.SetEnabled(world, true);
        Assert.False(world.TryStartNaturalVillageFeast(0));
        var triggered = false;
        for (var attempt = 0; attempt < 10000 && !triggered; attempt++)
            triggered = world.TryStartNaturalVillageFeast(tile);
        Assert.True(triggered);
        Assert.Equal(0, world.GetApeVillageFood(Village));
    }
}
