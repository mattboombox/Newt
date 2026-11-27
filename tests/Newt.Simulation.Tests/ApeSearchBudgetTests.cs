using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeSearchBudgetTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void ImpossibleSettlementSearchSharesBudgetAcrossAllSettlers()
    {
        var world = new SimulationWorld(288, 192, Terrain.Mountain, seed: 17);
        var search = typeof(SimulationWorld).GetMethod("FindDistantApeVillageSite", Private)!;
        var probes = typeof(SimulationWorld).GetProperty("ApeSettlementSiteProbes", Private)!;
        for (var i = 0; i < 20; i++) Assert.Equal(-1, search.Invoke(world, [i]));
        Assert.Equal(2048L, probes.GetValue(world));
        AdvanceTick(world);
        Assert.Equal(-1, search.Invoke(world, [0]));
        Assert.Equal(2688L, probes.GetValue(world));
    }

    [Fact]
    public void LongSettlerRouteResumesUntilCompleteWithinTurnBudget()
    {
        var world = new SimulationWorld(2400, 5, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Ape, new(100, 2));
        var search = typeof(SimulationWorld).GetMethod("FindBudgetedApeWorkerPath", Private)!;
        var expanded = typeof(SimulationWorld).GetProperty("ApePathExpandedTiles", Private)!;
        Queue<int>? route = null;
        var turns = 0;
        for (; turns < 100 && route is null; turns++)
        {
            var before = (long)expanded.GetValue(world)!;
            object?[] args = [0, 2 * world.Width + 100, 2 * world.Width + 1300, null, false, true, false];
            route = (Queue<int>?)search.Invoke(world, args);
            Assert.InRange((long)expanded.GetValue(world)! - before, 0, 1024);
            if (route is null) Assert.True((bool)args[4]!);
            // Unrelated construction must not restart a settler's long search.
            typeof(SimulationWorld).GetMethod("InvalidateTransportNetwork", Private)!.Invoke(world, null);
            AdvanceTick(world);
        }
        Assert.NotNull(route);
        Assert.True(turns > 1);
        Assert.Equal(2 * world.Width + 1300, route.Last());
    }

    private static void AdvanceTick(SimulationWorld world) =>
        typeof(SimulationWorld).GetProperty(nameof(world.Tick))!.SetValue(world, world.Tick + 1);

    [Fact]
    public void MultipleLongRoutesShareOneTickBudget()
    {
        var world = new SimulationWorld(2400, 20, Terrain.Plains, seed: 17);
        var search = typeof(SimulationWorld).GetMethod("FindBudgetedApeWorkerPath", Private)!;
        for (var index = 0; index < 8; index++)
        {
            world.AddCritter(CritterSpecies.Ape, new(100, index + 2));
            object?[] args = [index, (index + 2) * world.Width + 100,
                (index + 2) * world.Width + 1300, null, false, true, false];
            Assert.Null(search.Invoke(world, args));
            Assert.True((bool)args[4]!);
        }
        var expanded = (long)typeof(SimulationWorld).GetProperty("ApePathExpandedTiles", Private)!.GetValue(world)!;
        Assert.InRange(expanded, 1, 4096);
    }

    [Fact]
    public void DistantCrowdDoesNotMakeSettlerRouteUnreachable()
    {
        var world = new SimulationWorld(40, 10, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Ape, new(2, 5));
        for (var y = 0; y < world.Height; y++)
            world.AddCritter(CritterSpecies.ApeTrader, new(10, y));
        var search = typeof(SimulationWorld).GetMethod("FindBudgetedApeWorkerPath", Private)!;
        object?[] args = [0, 5 * world.Width + 2, 5 * world.Width + 20, null, false, true, false];
        var route = (Queue<int>?)search.Invoke(world, args);
        Assert.NotNull(route);
        Assert.Equal(5 * world.Width + 20, route.Last());
    }

    [Fact]
    public void EverySettlerGetsSearchWorkDuringABusyTurn()
    {
        var world = new SimulationWorld(2400, 20, Terrain.Plains, seed: 17);
        var targets = (Dictionary<int, (int OriginVillageTile, int TargetVillageTile)>)typeof(SimulationWorld)
            .GetField("_apeSettlerTargets", Private)!.GetValue(world)!;
        for (var index = 0; index < 12; index++)
        {
            var id = world.AddCritter(CritterSpecies.Ape, new(100, index + 2));
            targets[id.Value] = (0, (index + 2) * world.Width + 1300);
        }
        var expanded = typeof(SimulationWorld).GetProperty("ApePathExpandedTiles", Private)!;
        var move = typeof(SimulationWorld).GetMethod("MoveApeSettlerToward", Private)!;
        for (var index = 0; index < 12; index++)
        {
            var before = (long)expanded.GetValue(world)!;
            move.Invoke(world, [index, (index + 2) * world.Width + 1300]);
            Assert.True((long)expanded.GetValue(world)! > before, $"Settler {index} was starved of search work.");
        }
        Assert.InRange((long)expanded.GetValue(world)!, 1, 4096);
    }

    [Fact]
    public void ChangingDestinationOccupancyDoesNotRestartSettlerSearch()
    {
        var world = new SimulationWorld(2400, 5, Terrain.Plains, seed: 17);
        world.AddCritter(CritterSpecies.Ape, new(100, 2));
        var move = typeof(SimulationWorld).GetMethod("MoveApeSettlerToward", Private)!;
        var searches = (System.Collections.IDictionary)typeof(SimulationWorld)
            .GetField("_apeWorkerSearches", Private)!.GetValue(world)!;
        var target = 2 * world.Width + 1300;
        move.Invoke(world, [0, target]);
        var pending = searches[world.GetCritter(0).Id.Value];
        Assert.NotNull(pending);
        world.AddCritter(CritterSpecies.Monkey, new(1300, 2));
        AdvanceTick(world);
        // Exhaust the shared budget so the pending search remains observable.
        typeof(SimulationWorld).GetField("_apePathBudgetTick", Private)!.SetValue(world, world.Tick);
        typeof(SimulationWorld).GetField("_apePathBudgetUsed", Private)!.SetValue(world, 4096);
        move.Invoke(world, [0, target]);
        Assert.Same(pending, searches[world.GetCritter(0).Id.Value]);
    }
}
