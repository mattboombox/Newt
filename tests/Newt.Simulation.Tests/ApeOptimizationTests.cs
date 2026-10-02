using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeOptimizationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData(ApeStructureKind.Farm, CritterSpecies.ApeFarmer)]
    [InlineData(ApeStructureKind.LumberCamp, CritterSpecies.ApeLumberjack)]
    public void WorkplaceTracksRecruitmentDeathReplacementAndDemolition(
        ApeStructureKind kind, CritterSpecies species)
    {
        var world = new SimulationWorld(30, 30, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(10, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        var villageTile = village.Y * world.Width + village.X;
        Assert.True(world.TryBuildApeStructure(villageTile, kind));
        var workplace = Enumerable.Range(0, world.Width * world.Height)
            .First(tile => world.GetApeStructure(new(tile % world.Width, tile / world.Width)) == kind);
        var position = new GridPosition(workplace % world.Width, workplace / world.Width);
        var farmers = kind == ApeStructureKind.Farm;
        var recruit = typeof(SimulationWorld).GetMethod(farmers
            ? "AdvanceApeFarmerRecruitment" : "AdvanceApeLumberjackRecruitment", PrivateInstance)!;
        var due = (Dictionary<int, long>)typeof(SimulationWorld).GetField(farmers
            ? "_apeFarmerRecruitmentTicks" : "_apeLumberjackRecruitmentTicks", PrivateInstance)!.GetValue(world)!;
        void Recruit()
        {
            due[workplace] = 0;
            recruit.Invoke(world, farmers ? [villageTile, workplace, kind] : [villageTile, workplace]);
        }
        int Workplace(CritterId id) => (int)typeof(SimulationWorld)
            .GetMethod("GetApeWorkplace", PrivateInstance)!.Invoke(world, [id.Value, species])!;
        CritterSnapshot Worker() => Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter).Single(c => c.Species == species);

        Recruit();
        var original = Worker();
        Assert.Equal(workplace, Workplace(original.Id));
        Assert.True(world.RemoveCritterAt(original.Position));
        Assert.Equal(-1, Workplace(original.Id));
        Recruit();
        var replacement = Worker();
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal(workplace, Workplace(replacement.Id));
        Assert.True(world.RemoveApeStructureAt(position));
        Assert.Equal(-1, Workplace(replacement.Id));
        Assert.True(world.TryGetCritter(replacement.Id, out var formerWorker));
        Assert.Equal(CritterSpecies.Ape, formerWorker.Species);
    }

    [Fact]
    public void ReusedSearchStorageHandlesFailureChangedTerrainAndGenerationRollover()
    {
        var world = new SimulationWorld(20, 10, Terrain.Plains, seed: 1);
        world.AddCritter(CritterSpecies.Ape, new(2, 2));
        var find = typeof(SimulationWorld).GetMethod("FindApeSettlerPath", PrivateInstance)!;
        Queue<int>? Search(int destination) => (Queue<int>?)find.Invoke(world,
            [0, 42, destination, null, null, CritterSpecies.Ape]);
        var first = Search(46)!.ToArray();
        Assert.Equal(4, first.Length);
        Assert.Equal(46, first[^1]);
        world.SetTerrain(new(6, 2), Terrain.DeepOcean);
        Assert.Null(Search(46));
        Assert.Equal(43, Search(43)!.Single());
        world.SetTerrain(new(6, 2), Terrain.Plains);
        Assert.Equal(first, Search(46)!.ToArray());
        typeof(SimulationWorld).GetField("_apePathGeneration", PrivateInstance)!.SetValue(world, int.MaxValue);
        Assert.Equal(first, Search(46)!.ToArray());
    }

    [Fact]
    public void WarmShortSearchesDoNotAllocateMapSizedBuffers()
    {
        var world = new SimulationWorld(320, 192, Terrain.Plains, seed: 1);
        world.AddCritter(CritterSpecies.Ape, new(2, 2));
        var find = typeof(SimulationWorld).GetMethod("FindApeSettlerPath", PrivateInstance)!;
        object?[] args = [0, 642, 646, null, null, CritterSpecies.Ape];
        for (var i = 0; i < 20; i++) find.Invoke(world, args);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) find.Invoke(world, args);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        // Allows returned paths and reflection overhead, but not even one map-sized array.
        Assert.True(allocated < 100_000, $"100 short searches allocated {allocated:N0} bytes.");
    }
}
