using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

internal static class ColonistTestFood
{
    // Travel/combat fixtures supply provisions directly so those tests do not
    // depend on building food storage or waiting for farm production first.
    internal static void Fund(SimulationWorld world, GridPosition destination)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var village = (int)typeof(SimulationWorld).GetMethod("FindNearestApeVillage", flags)!
            .Invoke(world, [destination.Y * world.Width + destination.X])!;
        var stores = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageFood", flags)!.GetValue(world)!;
        stores[village] = Math.Max(stores.GetValueOrDefault(village), 25);
    }
}
