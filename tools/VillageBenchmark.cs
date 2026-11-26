#:property PublishAot=false
// Build the simulation Release DLL, then:
// dotnet run --file tools/VillageBenchmark.cs -c Release -- <simulation-dll-path>
// Synthetic dense village; phase timings exclude rendering and world generation.
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

Console.WriteLine($"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}; {Environment.ProcessorCount} logical CPUs");

var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
var type = assembly.GetType("Newt.Simulation.SimulationWorld")!;
var terrain = assembly.GetType("Newt.Simulation.Terrain")!;
var species = assembly.GetType("Newt.Simulation.CritterSpecies")!;
var position = assembly.GetType("Newt.Simulation.GridPosition")!;
var kind = assembly.GetType("Newt.Simulation.ApeStructureKind")!;
var biome = assembly.GetType("Newt.Simulation.Biome")!;
const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
object Position(int x, int y) => Activator.CreateInstance(position, x, y)!;
foreach (var phase in new[] { "AdvanceApeVillages", "AdvanceCritterLifecycles", "AdvanceCritterLifecyclesReady", "AdvanceCritterMovements", "AdvanceCritterMovementsFarmers" })
{
    var world = Activator.CreateInstance(type, 1280, 44, Enum.Parse(terrain, "Plains"), 17UL)!;
    type.GetProperty("SeasonsEnabled")!.SetValue(world, false);
    type.GetProperty("ApeVillageMergingEnabled")!.SetValue(world, false);
    assembly.GetType("Newt.Simulation.NaturalEvents")!.GetMethod("SetEnabled")!.Invoke(null, [world, false]);
    for (var y = 0; y < 44; y++)
    for (var x = 0; x < 1280; x++) type.GetMethod("SetBiome", flags)!.Invoke(world, [Position(x, y), Enum.Parse(biome, "Grassland")]);
    type.GetMethod("TrySpawnTestApeVillage")!.Invoke(world, [Position(20, 20)]);
    var homes = (IDictionary)type.GetField("_apeVillageHomes", flags)!.GetValue(world)!;
    const int village = 20 * 1280 + 20;
    var structures = (IDictionary)type.GetField("_apeStructures", flags)!.GetValue(world)!;
    var auxiliaries = (IDictionary)type.GetField("_apeAuxiliaryVillages", flags)!.GetValue(world)!;
    for (var i = 0; i < 5000; i++)
    {
        var isFarmer = phase.EndsWith("Farmers");
        var id = type.GetMethod("AddCritter")!.Invoke(world, [Enum.Parse(species, isFarmer ? "ApeFarmer" : "Ape"), Position(100 + i % 1000, 2 + i / 1000)])!;
        homes[id.GetType().GetProperty("Value")!.GetValue(id)!] = village;
        if (isFarmer)
        {
            var tile = (2 + i / 1000) * 1280 + 100 + i % 1000;
            var workerId = id.GetType().GetProperty("Value")!.GetValue(id)!;
            structures[tile] = Enum.Parse(kind, "Farm");
            auxiliaries[tile] = village;
            ((IDictionary)type.GetField("_apeFarmers", flags)!.GetValue(world)!)[tile] = workerId;
            ((IDictionary)type.GetField("_apeWorkplaces", flags)!.GetValue(world)!)[workerId] = tile;
        }
    }
    for (var i = 0; i < 1000; i++)
    {
        var tile = 12 * 1280 + 100 + i;
        structures[tile] = Enum.Parse(kind, i < 500 ? "Farm" : "ResidentialDistrict");
        auxiliaries[tile] = village;
    }
    if (phase.EndsWith("Ready"))
    {
        var energy = (int[])type.GetField("_energy", flags)!.GetValue(world)!;
        Array.Fill(energy, 12);
    }
    var method = type.GetMethod(phase.Replace("Ready", "").Replace("Farmers", ""), flags)!;
    for (var i = 0; i < 3; i++) method.Invoke(world, null);
    const int turns = 20;
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    for (var i = 0; i < turns; i++)
    {
        type.GetProperty("Tick")!.SetValue(world, (long)i + 60);
        method.Invoke(world, null);
    }
    watch.Stop();
    Console.WriteLine($"{phase}: {watch.Elapsed.TotalMilliseconds / turns:F3} ms/tick, {(GC.GetAllocatedBytesForCurrentThread() - allocated) / turns:N0} bytes/tick");
}
