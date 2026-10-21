using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

internal static class LegacyApeResearch
{
    // Preserve coverage for knowledge inherited from older worlds without adding a researchable technology.
    internal static void Seed(SimulationWorld world, int village)
    {
        var knowledge = (Dictionary<int, HashSet<ApeTechnology>>)typeof(SimulationWorld)
            .GetField("_apeVillageTechnologies", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        knowledge[village] = [ApeTechnology.Aquaculture];
    }
}
