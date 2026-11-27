using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeBuildingRecoveryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Home = new(10, 10), Other = new(40, 10);
    private static SimulationWorld World()
    {
        var world = new SimulationWorld(70, 40, Terrain.Plains) { SeasonsEnabled = false, ApeVillageMergingEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Home));
        return world;
    }
    private static IEnumerable<GridPosition> Buildings(SimulationWorld world, ApeStructureKind kind) =>
        Enumerable.Range(0, world.Width * world.Height).Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) == kind && world.GetApeStructureVillage(position) == Home);
    private static void RebuildAt(SimulationWorld world, long tick)
    {
        typeof(SimulationWorld).GetProperty(nameof(world.Tick))!.SetValue(world, tick);
        typeof(SimulationWorld).GetMethod("AdvanceApeBuildingReplacements", Private)!
            .Invoke(world, [Home.Y * world.Width + Home.X]);
    }

    [Theory]
    [InlineData(ApeCulture.Luddite, ApeStructureKind.LudditeTemple)]
    [InlineData(ApeCulture.Monarchy, ApeStructureKind.Castle)]
    public void DestroyedCulturalBuildingStripsCultureWithoutAutomaticRestoration(ApeCulture culture, ApeStructureKind kind)
    {
        var world = World();
        Assert.True(world.TryAssignApeCulture(Home, culture, ignorePopulationRequirement: true));
        Assert.True(world.RemoveApeStructureAt(Assert.Single(Buildings(world, kind))));
        Assert.Null(world.GetApeVillageCulture(Home));
        RebuildAt(world, SimulationWorld.ApeUniqueBuildingRebuildDelayTicks - 1);
        Assert.Empty(Buildings(world, kind));
        RebuildAt(world, SimulationWorld.ApeUniqueBuildingRebuildDelayTicks);
        Assert.Empty(Buildings(world, kind));
        Assert.Null(world.GetApeVillageCulture(Home));
        RebuildAt(world, 2L * SimulationWorld.ApeUniqueBuildingRebuildDelayTicks);
        Assert.Empty(Buildings(world, kind));
    }

    [Theory]
    [InlineData(ApeStructureKind.Library)]
    [InlineData(ApeStructureKind.Market)]
    public void DestroyedLibraryOrMarketWaitsThenReplacesItselfEvenBelowInitialPopulationThreshold(ApeStructureKind kind)
    {
        var world = World();
        Assert.True(world.TrySpawnTestApeVillage(Other));
        Assert.True(world.TryConnectVillageRoad(Home, Other, out _));
        var site = new GridPosition(10, 11);
        world.RemoveCritterAt(site);
        Assert.True(world.TryPlaceApeBuilding(site, kind));
        Assert.True(world.RemoveApeStructureAt(site));
        var tile = Home.Y * world.Width + Home.X;
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageFood", Private)!.GetValue(world)!)[tile] = 10;
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageWood", Private)!.GetValue(world)!)[tile] = 20;
        Assert.False(world.TryBuildApeStructure(tile, kind));
        RebuildAt(world, SimulationWorld.ApeUniqueBuildingRebuildDelayTicks - 1);
        Assert.Empty(Buildings(world, kind));
        RebuildAt(world, SimulationWorld.ApeUniqueBuildingRebuildDelayTicks);
        Assert.Single(Buildings(world, kind));
        RebuildAt(world, 2L * SimulationWorld.ApeUniqueBuildingRebuildDelayTicks);
        Assert.Single(Buildings(world, kind));
    }

    [Fact]
    public void OldTempleRebuildDoesNotOverrideNewlyChosenMonarchy()
    {
        var world = World();
        Assert.True(world.TryAssignApeCulture(Home, ApeCulture.Luddite, ignorePopulationRequirement: true));
        Assert.True(world.RemoveApeStructureAt(Assert.Single(Buildings(world, ApeStructureKind.LudditeTemple))));
        Assert.True(world.TryAssignApeCulture(Home, ApeCulture.Monarchy, ignorePopulationRequirement: true));
        RebuildAt(world, SimulationWorld.ApeUniqueBuildingRebuildDelayTicks);
        Assert.Equal(ApeCulture.Monarchy, world.GetApeVillageCulture(Home));
        Assert.Single(Buildings(world, ApeStructureKind.Castle));
        Assert.Empty(Buildings(world, ApeStructureKind.LudditeTemple));
    }
}
