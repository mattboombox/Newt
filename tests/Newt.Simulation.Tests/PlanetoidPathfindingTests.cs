using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlanetoidPathfindingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition[] Cardinals = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void EveryDirectedFaceEdgeHasAContinuousOneStepRoute(int size)
    {
        var world = World(size);
        var id = world.AddCritter(CritterSpecies.ApeFarmer, new(1, 1));
        for (var face = 0; face < 6; face++)
        for (var edge = 0; edge < 4; edge++)
        for (var along = 0; along < size; along++)
        {
            var start = Edge(face, edge, along, size);
            var target = PlanetoidLayout.Offset(start, Cardinals[edge].X, Cardinals[edge].Y, size);
            var path = Find(world, start, target);
            Assert.NotNull(path);
            Assert.Equal(Index(world, target), Assert.Single(path));
            Assert.True(Neighbors(world, start, target));
            Assert.True(Neighbors(world, target, start));
        }
        Assert.True(world.TryGetCritter(id, out _));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeFarmer, Terrain.Plains)]
    [InlineData(CritterSpecies.ApeLumberjack, Terrain.Plains)]
    [InlineData(CritterSpecies.Ape, Terrain.Plains)]
    [InlineData(CritterSpecies.ApeSailor, Terrain.Ocean)]
    [InlineData(CritterSpecies.Custodian, Terrain.Plains)]
    public void RandomLongRoutesMatchIndependentSurfaceBreadthFirstSearch(CritterSpecies species, Terrain terrain)
    {
        const int size = 8;
        var world = World(size, terrain);
        world.AddCritter(species, new(1, 1));
        var random = new Random(771);
        for (var sample = 0; sample < 100; sample++)
        {
            var start = new GridPosition(random.Next(world.Width), random.Next(world.Height));
            var target = new GridPosition(random.Next(world.Width), random.Next(world.Height));
            var path = Find(world, start, target, species);
            Assert.NotNull(path);
            Assert.Equal(ShortestDistance(world, start, target), path.Count);
            var previous = start;
            foreach (var tile in path)
            {
                var next = Position(world, tile);
                Assert.True(Neighbors(world, previous, next), $"Invalid surface step {previous} -> {next}");
                previous = next;
            }
            Assert.Equal(target, previous);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FarmerReturnsToAssignedFarmAcrossRotatedSeamAndRespectsReservations(bool reserve)
    {
        const int size = 16;
        var world = World(size);
        var home = new GridPosition(8, 4);
        Assert.True(world.TrySpawnTestApeVillage(home));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var farm = new GridPosition(8, 3);
        Assert.True(world.TryPlaceApeBuilding(farm, ApeStructureKind.Farm));
        var start = new GridPosition(2 * size - 3, size + 7);
        var farmer = world.AddCritter(CritterSpecies.Ape, start);
        Assert.True(world.TryAssignApeToVillage(farmer, home));
        Invoke(world, "AssignApeFarmer", farmer.Value, Index(world, farm));
        var route = Find(world, start, farm)!;
        if (reserve)
        {
            var blocked = new HashSet<GridPosition> { Position(world, route.Peek()) };
            Invoke(world, "TryMoveApe", 0, blocked);
            Assert.Equal(start, world.GetCritter(0).Position);
        }
        var previous = world.GetCritter(0).Position;
        for (var step = 0; step < 3 * size; step++)
        {
            if (previous == farm) break;
            Invoke(world, "TryMoveApe", 0, null);
            var current = world.GetCritter(0).Position;
            Assert.True(Neighbors(world, previous, current));
            previous = current;
        }
        Assert.Equal(farm, previous);
        Assert.Equal(CritterSpecies.ApeFarmer, world.GetCritter(0).Species);
        Assert.Equal(home, world.GetApeHomeVillage(farmer));
    }

    [Fact]
    public void ObstructedRoutesMatchReferenceSearchAndNeverCrossWater()
    {
        var world = World(12);
        var random = new Random(443);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (random.Next(4) == 0) world.SetTerrain(new(x, y), Terrain.Ocean);
        var start = new GridPosition(6, 0);
        world.SetTerrain(start, Terrain.Plains);
        world.AddCritter(CritterSpecies.ApeFarmer, start);
        for (var sample = 0; sample < 100; sample++)
        {
            var target = new GridPosition(random.Next(world.Width), random.Next(world.Height));
            if (world.GetTerrain(target) != Terrain.Plains) continue;
            var expected = ShortestDistance(world, start, target, avoidWater: true);
            var path = Find(world, start, target);
            if (expected < 0) Assert.Null(path);
            else
            {
                Assert.NotNull(path);
                Assert.Equal(expected, path.Count);
                Assert.All(path, tile => Assert.Equal(Terrain.Plains, world.GetTerrain(Position(world, tile))));
            }
        }
    }

    [Fact]
    public void SailorReturnsToLandingAcrossRotatedSeam()
    {
        const int size = 16;
        var world = World(size, Terrain.Ocean);
        var village = new GridPosition(8, 0);
        world.SetTerrain(village, Terrain.Plains);
        world.SetTerrain(new(7, 0), Terrain.Plains);
        world.SetTerrain(new(8, 1), Terrain.Plains);
        Assert.True(world.TrySpawnTestApeVillage(village));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var landing = PlanetoidLayout.Offset(village, 0, -1, size);
        var start = new GridPosition(landing.X, landing.Y - 3);
        var sailor = world.AddCritter(CritterSpecies.ApeSailor, start);
        var previous = start;
        for (var step = 0; step < 10 && previous != landing; step++)
        {
            world.MoveApeSailorTowardVillage(sailor, Index(world, village));
            var next = world.GetCritter(0).Position;
            Assert.True(Neighbors(world, previous, next));
            previous = next;
        }
        Assert.Equal(landing, previous);
    }

    [Fact]
    public void SurfaceReturnRouteApproachesOccupiedEnemy()
    {
        const int size = 8;
        var world = World(size);
        var start = new GridPosition(4, 0);
        var across = PlanetoidLayout.Offset(start, 0, -1, size);
        var target = new GridPosition(across.X, across.Y - 3);
        world.AddCritter(CritterSpecies.Wolf, start);
        world.AddCritter(CritterSpecies.ApeWarrior, target);
        var previous = start;
        for (var step = 0; step < 8 && !Neighbors(world, previous, target); step++)
        {
            Invoke(world, "TryMoveTowardWolfDen", 0, Index(world, target), null);
            var next = world.GetCritter(0).Position;
            Assert.True(Neighbors(world, previous, next));
            previous = next;
        }
        Assert.True(Neighbors(world, previous, target));
    }

    [Fact]
    public void WorkerDiscardsLegacyAtlasRouteAndReplansOnTheSurface()
    {
        var world = World(8);
        var start = new GridPosition(7, 7);
        var fakeStep = new GridPosition(8, 8);
        var target = new GridPosition(10, 10);
        var worker = world.AddCritter(CritterSpecies.ApeFarmer, start);
        var paths = (Dictionary<int, (int TargetTile, Queue<int> Tiles)>)typeof(SimulationWorld)
            .GetField("_apeWorkerPaths", Private)!.GetValue(world)!;
        paths[worker.Value] = (Index(world, target), new Queue<int>([Index(world, fakeStep), Index(world, target)]));
        Invoke(world, "TryMoveApeWorkerToward", 0, Index(world, target), null);
        var next = world.GetCritter(0).Position;
        Assert.NotEqual(fakeStep, next);
        Assert.True(Neighbors(world, start, next));
    }

    [Fact]
    public void AtlasNeighborDoesNotConnectIsolatedSurfaceIslands()
    {
        const int size = 8;
        var world = World(size, Terrain.Ocean);
        var start = new GridPosition(size - 1, size - 1);
        var fakeNeighbor = new GridPosition(size, size);
        world.SetTerrain(start, Terrain.Plains);
        world.SetTerrain(fakeNeighbor, Terrain.Plains);
        world.AddCritter(CritterSpecies.ApeFarmer, start);
        Assert.False(Neighbors(world, start, fakeNeighbor));
        Assert.Null(Find(world, start, fakeNeighbor));
    }

    [Fact]
    public void StopAdjacentAndReachabilityUseSurfaceRatherThanAtlasEdges()
    {
        const int size = 16;
        var world = World(size);
        var village = new GridPosition(8, 0);
        Assert.True(world.TrySpawnTestApeVillage(village));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var across = PlanetoidLayout.Offset(village, 0, -1, size);
        var start = new GridPosition(across.X, across.Y - 3);
        world.AddCritter(CritterSpecies.Ape, start);
        Assert.True((bool)Invoke(world, "CanApeReachVillage", 0, Index(world, village))!);
        var route = Find(world, start, village, CritterSpecies.Ape, stopAdjacent: true)!;
        Assert.Equal(3, route.Count);
        Assert.True(Neighbors(world, Position(world, route.Last()), village));
        var reserved = new HashSet<GridPosition> { Position(world, route.Peek()) };
        var alternate = Find(world, start, village, CritterSpecies.Ape, reserved: reserved)!;
        Assert.DoesNotContain(alternate, tile => reserved.Contains(Position(world, tile)));
    }

    private static SimulationWorld World(int size, Terrain terrain = Terrain.Plains)
    {
        var world = new SimulationWorld(3 * size, 2 * size, terrain, seed: 17)
        { Body = WorldBody.Planetoid, SeasonsEnabled = false, ApeVillageMergingEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        return world;
    }

    private static GridPosition Edge(int face, int edge, int along, int size) => new(
        face % 3 * size + (edge == 1 ? size - 1 : edge == 3 ? 0 : along),
        face / 3 * size + (edge == 0 ? 0 : edge == 2 ? size - 1 : along));
    private static int Index(SimulationWorld world, GridPosition p) => p.Y * world.Width + p.X;
    private static GridPosition Position(SimulationWorld world, int tile) => new(tile % world.Width, tile / world.Width);
    private static object? Invoke(SimulationWorld world, string name, params object?[] args) =>
        typeof(SimulationWorld).GetMethod(name, Private)!.Invoke(world, args);
    private static bool Neighbors(SimulationWorld world, GridPosition a, GridPosition b) =>
        SurfaceNeighbors(world, a).Contains(b);
    private static IEnumerable<GridPosition> SurfaceNeighbors(SimulationWorld world, GridPosition position)
    {
        var size = world.Width / 3;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            var next = PlanetoidLayout.Offset(position, dx, dy, size);
            if (dx != 0 && dy != 0 && PlanetoidLayout.FaceAt(position.X, position.Y, size) !=
                PlanetoidLayout.FaceAt(next.X, next.Y, size)) continue;
            yield return next;
        }
    }
    private static Queue<int>? Find(SimulationWorld world, GridPosition start, GridPosition target,
        CritterSpecies species = CritterSpecies.ApeFarmer, bool stopAdjacent = false,
        IReadOnlySet<GridPosition>? reserved = null) =>
        (Queue<int>?)Invoke(world, "FindApeSettlerPath", 0, Index(world, start), Index(world, target),
            null, reserved, species, stopAdjacent);
    private static int ShortestDistance(SimulationWorld world, GridPosition start, GridPosition target, bool avoidWater = false)
    {
        var queue = new Queue<(GridPosition Position, int Distance)>();
        var seen = new HashSet<GridPosition> { start };
        queue.Enqueue((start, 0));
        while (queue.TryDequeue(out var node))
        {
            if (node.Position == target) return node.Distance;
            foreach (var next in SurfaceNeighbors(world, node.Position))
                if ((!avoidWater || world.GetTerrain(next) == Terrain.Plains) && seen.Add(next))
                    queue.Enqueue((next, node.Distance + 1));
        }
        return -1;
    }
}
