using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RoadNetworkTests
{
    private static readonly GridPosition First = new(10, 10);
    private static readonly GridPosition Second = new(40, 10);
    private static readonly GridPosition Branch = new(25, 20);
    private static readonly (RiverConnection Flag, int X, int Y)[] Directions =
    [
        (RiverConnection.North, 0, -1), (RiverConnection.NorthEast, 1, -1),
        (RiverConnection.East, 1, 0), (RiverConnection.SouthEast, 1, 1),
        (RiverConnection.South, 0, 1), (RiverConnection.SouthWest, -1, 1),
        (RiverConnection.West, -1, 0), (RiverConnection.NorthWest, -1, -1),
    ];

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(100, 60, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in Positions(world))
            world.SetBiome(position, Biome.Grassland);
        return world;
    }

    private static IEnumerable<GridPosition> Positions(SimulationWorld world) =>
        Enumerable.Range(0, world.Width * world.Height).Select(i => new GridPosition(i % world.Width, i / world.Width));

    private static Dictionary<GridPosition, RiverConnection> Roads(SimulationWorld world) =>
        Positions(world).Where(p => world.GetRoadConnections(p) != RiverConnection.None)
            .ToDictionary(p => p, world.GetRoadConnections);

    private static HashSet<GridPosition> Reachable(SimulationWorld world, GridPosition start)
    {
        var visited = new HashSet<GridPosition>();
        var pending = new Queue<GridPosition>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
                continue;
            var connections = world.GetRoadConnections(current);
            foreach (var (flag, x, y) in Directions)
                if ((connections & flag) != 0)
                {
                    var neighbor = new GridPosition((current.X + x + world.Width) % world.Width, current.Y + y);
                    var opposite = Directions.Single(d => d.X == -x && d.Y == -y).Flag;
                    Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(neighbor) & opposite);
                    pending.Enqueue(neighbor);
                }
        }
        return visited;
    }

    private static SimulationWorld CreateTrunk()
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        Assert.True(world.TryPlaceVillageRoad(First));
        return world;
    }

    [Fact]
    public void NewVillageJoinsTrunkWithShortBranchAndConnectedVillagesDoNotAddRoads()
    {
        var world = CreateTrunk();
        var original = Roads(world);
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.Equal(original.Count + 10, Roads(world).Count);
        Assert.Equal(2, world.VillageRoadCount);
        var connected = Reachable(world, Branch);
        Assert.Contains(First, connected);
        Assert.Contains(Second, connected);
        Assert.Equal(Roads(world).Count, connected.Count);
        var before = Roads(world);
        for (var attempt = 0; attempt < 10; attempt++)
            foreach (var village in new[] { First, Second, Branch })
                Assert.False(world.TryPlaceVillageRoad(village));
        Assert.Equal(before.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X), Roads(world).OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X));
    }

    [Fact]
    public void DeletingOriginalVillageKeepsSharedRouteBetweenSurvivors()
    {
        var world = CreateTrunk();
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.True(world.RemoveApeStructureAt(First));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(First));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new(20, 10)));
        Assert.Contains(Second, Reachable(world, Branch));
        Assert.Equal(1, world.VillageRoadCount);
        Assert.True(world.RemoveApeStructureAt(Second));
        Assert.Empty(Roads(world));
        Assert.Equal(0, world.VillageRoadCount);
    }

    [Fact]
    public void DeletingBranchVillageLeavesOriginalTrunkUnchanged()
    {
        var world = CreateTrunk();
        var original = Roads(world);
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.True(world.RemoveApeStructureAt(Branch));
        Assert.Equal(original.OrderBy(p => p.Key.X), Roads(world).OrderBy(p => p.Key.X));
        Assert.Contains(Second, Reachable(world, First));
    }

    [Fact]
    public void RemovingVillageOnThroughRoadPreservesThroughTraffic()
    {
        var world = CreateTrunk();
        var middle = new GridPosition(25, 10);
        Assert.True(world.TrySpawnTestApeVillage(middle));
        Assert.False(world.TryPlaceVillageRoad(middle));
        Assert.Equal(2, world.VillageRoadCount);
        Assert.True(world.RemoveApeStructureAt(middle));
        Assert.Contains(Second, Reachable(world, First));
        Assert.Equal(RiverConnection.East | RiverConnection.West, world.GetRoadConnections(middle));
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Fact]
    public void ManuallyRemovingBranchPreservesTrunkAndAllowsReconnection()
    {
        var world = CreateTrunk();
        var original = Roads(world);
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.True(world.RemoveVillageRoadAt(new(25, 15)));
        Assert.Equal(original.Count, Roads(world).Count);
        Assert.Contains(Second, Reachable(world, First));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.Contains(First, Reachable(world, Branch));
    }

    [Fact]
    public void ManuallyRemovingJunctionPrunesDisconnectedDeadEnds()
    {
        var world = CreateTrunk();
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.True(world.RemoveVillageRoadAt(new(25, 10)));
        Assert.Empty(Roads(world));
    }

    [Fact]
    public void DiagonalContactCreatesReciprocalConnection()
    {
        var world = CreateTrunk();
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        // The last step into the trunk must be diagonal.
        for (var x = 0; x < world.Width; x++)
            if (x != 25)
                world.SetTerrain(new(x, 11), Terrain.Mountain);
        world.SetTerrain(new(25, 10), Terrain.Mountain);
        Assert.True(world.TryPlaceVillageRoad(Branch));
        Assert.Contains(Second, Reachable(world, Branch));
        Assert.Contains(Roads(world), pair => pair.Key.Y == 10 &&
            (pair.Value & (RiverConnection.SouthEast | RiverConnection.SouthWest)) != 0);
    }

    [Fact]
    public void UnreachableNearestVillageDoesNotPreventReachableConnection()
    {
        var world = CreateWorld();
        var isolated = new GridPosition(25, 10);
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(isolated));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        foreach (var (_, x, y) in Directions)
            world.SetTerrain(new(isolated.X + x, isolated.Y + y), Terrain.Mountain);
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Contains(Second, Reachable(world, First));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(isolated));
    }

    [Fact]
    public void ManyVillagesShareACompactNetworkThatSurvivesMultipleRemovals()
    {
        var world = CreateWorld();
        var villages = new List<GridPosition>();
        for (var y = 10; y <= 40; y += 15)
        for (var x = 10; x <= 85; x += 15)
        {
            var village = new GridPosition(x, y);
            Assert.True(world.TrySpawnTestApeVillage(village));
            villages.Add(village);
            Assert.Equal(villages.Count > 1, world.TryPlaceVillageRoad(village));
        }
        Assert.Equal(villages.Count - 1, world.VillageRoadCount);
        var before = Roads(world);
        Assert.True(before.Count <= 1 + 15 * (villages.Count - 1));
        var connected = Reachable(world, villages[0]);
        Assert.All(villages, village => Assert.Contains(village, connected));
        var edges = before.Values.Sum(flags => System.Numerics.BitOperations.PopCount((uint)flags)) / 2;
        Assert.Equal(before.Count - 1, edges); // A connected network without redundant loops.
        foreach (var village in villages)
            Assert.False(world.TryPlaceVillageRoad(village));
        Assert.Equal(before.Count, Roads(world).Count);

        foreach (var village in villages.Where((_, i) => i % 2 == 0))
            Assert.True(world.RemoveApeStructureAt(village));
        var survivors = villages.Where((_, i) => i % 2 != 0).ToArray();
        connected = Reachable(world, survivors[0]);
        Assert.All(survivors, village => Assert.Contains(village, connected));
        Assert.Equal(survivors.Length - 1, world.VillageRoadCount);
        Assert.True(Roads(world).Count <= before.Count);
    }

    [Theory]
    [InlineData(Terrain.Plains, SurfaceWaterKind.FreshwaterLake)]
    [InlineData(Terrain.Ocean, SurfaceWaterKind.None)]
    [InlineData(Terrain.DeepOcean, SurfaceWaterKind.None)]
    [InlineData(Terrain.Shallows, SurfaceWaterKind.None)]
    [InlineData(Terrain.Mountain, SurfaceWaterKind.None)]
    [InlineData(Terrain.Ice, SurfaceWaterKind.None)]
    public void ChangedTerrainReroutesExistingRoadEvenBelowPopulationThreshold(Terrain terrain, SurfaceWaterKind water)
    {
        var world = CreateTrunk();
        var obstruction = new GridPosition(25, 10);
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(obstruction));
        world.SetTerrain(obstruction, terrain);
        world.SetSurfaceWater(obstruction, water);
        world.AdvanceOneTick();
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(obstruction));
        Assert.Contains(Second, Reachable(world, First));
        Assert.Equal(1, world.VillageRoadCount);
        Assert.All(Roads(world).Keys, tile =>
        {
            Assert.DoesNotContain(world.GetTerrain(tile), new[] { Terrain.Ocean, Terrain.DeepOcean, Terrain.Shallows, Terrain.Mountain, Terrain.Ice });
            Assert.NotEqual(SurfaceWaterKind.FreshwaterLake, world.GetSurfaceWater(tile));
        });
        var repaired = Roads(world);
        world.AdvanceOneTick();
        Assert.Equal(repaired.OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X), Roads(world).OrderBy(p => p.Key.Y).ThenBy(p => p.Key.X));
    }

    [Fact]
    public void FloodedJunctionReconnectsAllThreeVillagesWithoutLoops()
    {
        var world = CreateTrunk();
        Assert.True(world.TrySpawnTestApeVillage(Branch));
        Assert.True(world.TryPlaceVillageRoad(Branch));
        var junction = new GridPosition(25, 10);
        world.SetSurfaceWater(junction, SurfaceWaterKind.FreshwaterLake);
        world.AdvanceOneTick();
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(junction));
        var connected = Reachable(world, First);
        Assert.Contains(Second, connected);
        Assert.Contains(Branch, connected);
        Assert.Equal(2, world.VillageRoadCount);
        var roads = Roads(world);
        Assert.Equal(roads.Count - 1, roads.Values.Sum(flags => System.Numerics.BitOperations.PopCount((uint)flags)) / 2);
    }

    [Fact]
    public void MultipleObstructionsDoNotLeaveOrphanRoadsOrPreventRepair()
    {
        var world = CreateTrunk();
        world.SetTerrain(new(20, 10), Terrain.Mountain);
        world.SetSurfaceWater(new(30, 10), SurfaceWaterKind.FreshwaterLake);
        world.AdvanceOneTick();
        Assert.Contains(Second, Reachable(world, First));
        Assert.Equal(Roads(world).Count, Reachable(world, First).Count);
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new(20, 10)));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new(30, 10)));
    }

    [Fact]
    public void ImpassableOceanBarrierRemovesUnusableRoads()
    {
        var world = CreateTrunk();
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(25, y), Terrain.Ocean);
            world.SetTerrain(new(75, y), Terrain.Ocean);
        }
        world.AdvanceOneTick();
        Assert.Empty(Roads(world));
        Assert.Equal(0, world.VillageRoadCount);
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(First));
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(Second));
    }

    [Fact]
    public void NewRiverKeepsExistingCrossing()
    {
        var world = CreateTrunk();
        var original = Roads(world);
        world.SetSurfaceWater(new(25, 10), SurfaceWaterKind.River);
        world.AdvanceOneTick();
        Assert.Equal(original.OrderBy(p => p.Key.X), Roads(world).OrderBy(p => p.Key.X));
    }

    [Theory]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Beach)]
    [InlineData(Terrain.Trench)]
    public void NewRoadPrefersDetourAroundRoughTerrain(Terrain terrain)
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        for (var x = 20; x <= 30; x++)
        for (var y = 9; y <= 11; y++)
            world.SetTerrain(new(x, y), terrain);
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Contains(Second, Reachable(world, First));
        Assert.All(Roads(world).Keys, tile => Assert.NotEqual(terrain, world.GetTerrain(tile)));
        Assert.Contains(Roads(world).Keys, tile => tile.Y < 9 || tile.Y > 11);
    }

    [Theory]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Beach)]
    [InlineData(Terrain.Trench)]
    public void RoughTerrainRemainsUsableWhenItIsTheOnlyCrossing(Terrain terrain)
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(25, y), Terrain.Mountain);
            world.SetTerrain(new(75, y), Terrain.Ocean);
        }
        world.SetTerrain(new(25, 10), terrain);
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Contains(Second, Reachable(world, First));
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(new(25, 10)));
    }

    [Fact]
    public void IceSheetsNeverAllowNewRoadCrossings()
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(25, y), Terrain.Ice);
            world.SetTerrain(new(75, y), Terrain.Ice);
        }
        Assert.False(world.TryPlaceVillageRoad(First));
        Assert.Empty(Roads(world));
        world.SetTerrain(new(25, 10), Terrain.Plains);
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.All(Roads(world).Keys, tile => Assert.NotEqual(Terrain.Ice, world.GetTerrain(tile)));
    }

    [Theory]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Bog)]
    [InlineData(Biome.Jungle)]
    public void NewRoadPrefersDetourAroundDifficultBiome(Biome biome)
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        for (var x = 20; x <= 30; x++)
        for (var y = 9; y <= 11; y++)
            world.SetBiome(new(x, y), biome);

        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Contains(Second, Reachable(world, First));
        Assert.All(Roads(world).Keys, tile => Assert.NotEqual(biome, world.GetBiome(tile)));
        Assert.Contains(Roads(world).Keys, tile => tile.Y < 9 || tile.Y > 11);
    }

    [Theory]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Bog)]
    [InlineData(Biome.Jungle)]
    public void DifficultBiomeRemainsUsableWhenItIsTheOnlyCrossing(Biome biome)
    {
        var world = CreateWorld();
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(25, y), Terrain.Mountain);
            world.SetTerrain(new(75, y), Terrain.Ocean);
        }
        var crossing = new GridPosition(25, 10);
        world.SetTerrain(crossing, Terrain.Plains);
        world.SetBiome(crossing, biome);

        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Contains(Second, Reachable(world, First));
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(crossing));
    }
}
