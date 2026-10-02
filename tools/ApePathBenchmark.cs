#:property PublishAot=false
// Build Newt.Simulation in Release first, then run:
// dotnet run --file tools/ApePathBenchmark.cs -c Release -- <simulation-dll-path>
// Accepts an older DLL as well, for before/after comparisons without changing the checkout.
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

if (args.Length != 1)
    throw new ArgumentException("Supply the path to Newt.Simulation.dll.");
var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var worldType = assembly.GetType("Newt.Simulation.SimulationWorld")!;
var terrainType = assembly.GetType("Newt.Simulation.Terrain")!;
var speciesType = assembly.GetType("Newt.Simulation.CritterSpecies")!;
var positionType = assembly.GetType("Newt.Simulation.GridPosition")!;
var world = Activator.CreateInstance(worldType, 320, 192, Enum.Parse(terrainType, "Plains"), 1UL)!;
var ape = Enum.Parse(speciesType, "Ape");
var position = Activator.CreateInstance(positionType, 2, 2)!;
worldType.GetMethod("AddCritter")!.Invoke(world, [ape, position]);
var find = worldType.GetMethod("FindApeSettlerPath", BindingFlags.Instance | BindingFlags.NonPublic)!;
object?[] search = [0, 642, 646, null, null, ape];
for (var i = 0; i < 200; i++) find.Invoke(world, search);
const int searches = 10_000;
var stopwatch = new Stopwatch();
var allocated = GC.GetAllocatedBytesForCurrentThread();
stopwatch.Start();
for (var i = 0; i < searches; i++)
{
    var result = (Queue<int>?)find.Invoke(world, search);
    if (result is null || result.Count != 4 || result.Last() != 646)
        throw new InvalidOperationException("Unexpected route.");
}
stopwatch.Stop();
allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {Environment.ProcessorCount} logical CPUs");
Console.WriteLine($"{searches:N0} four-step paths on 320 x 192: {stopwatch.Elapsed.TotalMilliseconds / searches:F6} ms/search; {allocated / searches:N0} bytes/search");
Console.WriteLine("Isolated warm path searches, including reflection and route validation; not a whole-world tick benchmark.");
