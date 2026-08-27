// Run: dotnet run --file tools/PlagueBenchmark.cs -c Release
#:project ../src/Newt.Simulation/Newt.Simulation.csproj

using System.Diagnostics;
using System.Runtime.InteropServices;
using Newt.Simulation;

Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {Environment.ProcessorCount} logical CPUs");
foreach (var kind in new[] { PlagueKind.None, PlagueKind.Plague, PlagueKind.Zombie })
{
    var world = new SimulationWorld(640, 311, Terrain.Plains, seed: 2101);
    SeasonSystem.SetEnabled(world, false);
    NaturalEvents.SetEnabled(world, false);
    for (var index = 0; index < 10_000; index++)
    {
        var position = new GridPosition(index % 100, index / 100);
        world.SetTerrain(position, Terrain.Plains);
        world.AddCritter(CritterSpecies.Ape, position);
        if (index % 13 == 0)
        {
            world.TryInfectApeAt(position, kind);
        }
    }
    for (var tick = 0; tick < 200; tick++) world.AdvanceOneTick();
    const int measuredTicks = 1_400;
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var stopwatch = Stopwatch.StartNew();
    for (var tick = 0; tick < measuredTicks; tick++) world.AdvanceOneTick();
    stopwatch.Stop();
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
    Console.WriteLine($"{kind}: {stopwatch.Elapsed.TotalMilliseconds / measuredTicks:F3} ms/tick; {allocated / measuredTicks:N0} bytes/tick; final apes={world.GetCritterCount(CritterSpecies.Ape)}, undead={world.GetCritterCount(CritterSpecies.UndeadApe)}");
}
