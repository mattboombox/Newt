namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    // Shared tile edges form a forest. Villages are terminals, not owners of routes.
    private readonly Dictionary<int, HashSet<int>> _roadLinks = [];
    private readonly HashSet<int> _roadVillages = [];
    private readonly Dictionary<int, RiverConnection> _roadConnections = [];
    private readonly Dictionary<int, int> _roadPopulationChecks = [];
    private bool _villageRoadValidationNeeded = true;
    private long _transportRevision;
    internal long VillageRoadValidationCount { get; private set; }

    private void InvalidateTransportNetwork()
    {
        _villageRoadValidationNeeded = true;
        _transportRevision++;
        _traderPathSearches.Clear();
    }

    /// <summary>Number of village-to-network links across all connected road networks.</summary>
    public int VillageRoadCount { get; private set; }

    public RiverConnection GetRoadConnections(GridPosition position) =>
        _roadConnections.GetValueOrDefault(GetIndex(position));

    public bool HasRoadBridge(GridPosition position) =>
        GetRoadConnections(position) != RiverConnection.None && IsBridgeWater(GetIndex(position));

    private bool IsRoadVillage(int tile) =>
        _apeStructures.TryGetValue(tile, out var kind) && kind is ApeStructureKind.Village &&
        !_barbarianVillageTiles.Contains(tile);

    private void CheckVillageRoadPopulation(int village, int population)
    {
        var milestone = population / 50;
        if (milestone <= _roadPopulationChecks.GetValueOrDefault(village))
            return;
        _roadPopulationChecks[village] = milestone;
        TryPlaceVillageRoad(GetPosition(village), force: false);
    }

    private bool IsRoadTerrain(int tile) => IsRoadLandTerrain(tile) || IsRoadBridge(tile);

    private bool IsRoadBridge(int tile)
    {
        if (!IsBridgeWater(tile))
            return false;
        return CardinalDirections.Any(direction =>
        {
            var waterTiles = 1;
            var first = FindBank(direction, ref waterTiles);
            var second = FindBank(new(-direction.X, -direction.Y), ref waterTiles);
            return first >= 0 && second >= 0 && first != second;
        });

        int FindBank(GridPosition direction, ref int waterTiles)
        {
            var current = tile;
            for (var step = 0; step < 2; step++)
            {
                var position = GetPosition(current);
                var next = OffsetTile(position, direction);
                var back = OffsetTile(position, new(-direction.X, -direction.Y));
                if (!Contains(next) || !Contains(back) || next == back ||
                    !_roadLinks.TryGetValue(current, out var links) || links.Count != 2 ||
                    !links.Contains(GetIndex(next)) || !links.Contains(GetIndex(back)))
                    return -1;
                current = GetIndex(next);
                if (IsRoadLandTerrain(current)) return current;
                if (current == tile || !IsBridgeWater(current) || ++waterTiles > 2) return -1;
                direction = ContinueRoadDirection(position, next, direction);
            }
            return -1;
        }
    }

    private bool IsBridgeWater(int tile) =>
        _surfaceCovers[tile] is SurfaceCover.None &&
        (_terrain[tile] is Terrain.Shallows || _terrain[tile] is not Terrain.MegaWall &&
            _surfaceWater[tile] is SurfaceWaterKind.FreshwaterLake);

    private bool IsRoadLandTerrain(int tile) =>
        _terrain[tile] is not (Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or
            Terrain.Ice or Terrain.Mountain or Terrain.MegaWall) &&
        _surfaceWater[tile] is not SurfaceWaterKind.FreshwaterLake &&
        _surfaceCovers[tile] is SurfaceCover.None;

    private int RoadTerrainCostMultiplier(int tile) =>
        GetRoadConstructionCost(_terrain[tile], _biomes[tile]);

    private int RoadBridgeCost(int tile) => tile < 0 ? 0 :
        _surfaceWater[tile] is SurfaceWaterKind.FreshwaterLake ? 150 : 200;

    // Tenths of the grassland/plains cost. Vegetation, climate and earthworks
    // accumulate, so a forested hill costs more than either obstacle alone.
    internal static int GetRoadConstructionCost(Terrain terrain, Biome biome) =>
        (biome switch
        {
            Biome.None or Biome.Grassland => 10,
            Biome.Arid => 11,
            Biome.Desert => 14,
            Biome.Forest => 18,
            Biome.Tundra => 20,
            Biome.Taiga => 23,
            Biome.Arctic => 26,
            Biome.Jungle => 30,
            Biome.Bog or Biome.Swamp => 35,
            _ => 10,
        }) + (terrain switch
        {
            Terrain.Beach => 5,
            Terrain.Hills => 10,
            Terrain.Canyon => 20,
            Terrain.Trench => 25,
            _ => 0,
        });

    private GridPosition ContinueRoadDirection(GridPosition from, GridPosition to, GridPosition direction)
    {
        if (Body is WorldBody.Planetoid && OffsetTile(to, new(-direction.X, -direction.Y)) != from)
            foreach (var back in CardinalDirections)
                if (OffsetTile(to, back) == from)
                    return new(-back.X, -back.Y);
        return direction;
    }

    private IEnumerable<int> RoadNeighbors(int tile) =>
        RoadNeighborSteps(tile).Select(step => step.Tile);

    private IEnumerable<(int Tile, GridPosition Direction)> RoadNeighborSteps(int tile)
    {
        var position = GetPosition(tile);
        foreach (var direction in MovementDirections)
        {
            if (TryGetPathNeighbor(position, direction, out var neighbor))
                yield return (neighbor, direction);
        }
    }

    public bool TryPlaceVillageRoad(GridPosition position, bool force = true)
    {
        if (!Contains(position) || GetApeStructureVillage(position) is not { } village)
            return false;
        var start = GetIndex(village);
        if (!IsRoadVillage(start) || (!force && GetApeVillageResidentCountByTile(start) < 50))
            return false;
        if (_roadLinks.ContainsKey(start))
        {
            // A village founded on a through-road is already connected.
            if (_roadVillages.Add(start))
                RebuildRoadConnections();
            return false;
        }
        if (!IsRoadTerrain(start))
            return false;

        var targets = _roadLinks.Keys.Where(IsRoadLandTerrain).ToHashSet();
        foreach (var tile in _apeStructures.Keys)
            if (tile != start && IsRoadVillage(tile) && IsRoadTerrain(tile) &&
                (force || GetApeVillageResidentCountByTile(tile) >= 50))
                targets.Add(tile);
        if (targets.Count == 0)
            return false;

        var path = FindRoadPath([start], targets);
        if (path is null)
            return false;

        _roadVillages.Add(start);
        if (IsRoadVillage(path[^1]))
            _roadVillages.Add(path[^1]);
        AddRoadPath(path);
        RebuildRoadConnections();
        return true;
    }

    /// <summary>Connects two selected villages, reusing only networks that reach those villages.</summary>
    public bool TryConnectVillageRoad(GridPosition first, GridPosition second, out string status)
    {
        status = "Select two different non-barbarian villages";
        if (!Contains(first) || !Contains(second) ||
            GetApeStructureVillage(first) is not { } firstVillage ||
            GetApeStructureVillage(second) is not { } secondVillage)
            return false;
        var start = GetIndex(firstVillage);
        var destination = GetIndex(secondVillage);
        if (start == destination || !IsRoadVillage(start) || !IsRoadVillage(destination)) return false;

        ValidateVillageRoads();
        var origins = _roadLinks.ContainsKey(start) ? GetRoadComponent(start) : [start];
        if (origins.Contains(destination))
        {
            if (_roadVillages.Add(start) | _roadVillages.Add(destination)) RebuildRoadConnections();
            status = "These villages already share a road network";
            return false;
        }
        var targets = (_roadLinks.ContainsKey(destination) ? GetRoadComponent(destination) : [destination])
            .Where(IsRoadLandTerrain).ToHashSet();
        var path = FindRoadPath(origins, targets);
        if (path is null)
        {
            status = "No valid road route between the selected villages";
            return false;
        }
        _roadVillages.Add(start);
        _roadVillages.Add(destination);
        AddRoadPath(path);
        RebuildRoadConnections();
        status = "Road connected! L select another starting village / R remove";
        return true;
    }

    private List<int>? FindRoadPath(IEnumerable<int> origins, HashSet<int> targets) =>
        FindRoadPath(origins, targets, allowBridges: true);

    private IEnumerable<(int Tile, int Bridge, int SecondBridge, int StepCost)> RoadPathSteps(int tile, bool allowBridges)
    {
        foreach (var (next, direction) in RoadNeighborSteps(tile))
            if (IsRoadLandTerrain(next))
                yield return (next, -1, -1, direction.X != 0 && direction.Y != 0 ? 14 : 10);
        if (!allowBridges) yield break;
        var position = GetPosition(tile);
        foreach (var startDirection in CardinalDirections)
        {
            var bridge = -1;
            var secondBridge = -1;
            var current = position;
            var direction = startDirection;
            for (var distance = 1; distance <= 3; distance++)
            {
                var next = OffsetTile(current, direction);
                if (!Contains(next) || next == position) break;
                var nextTile = GetIndex(next);
                if (IsRoadLandTerrain(nextTile))
                {
                    if (bridge >= 0) yield return (nextTile, bridge, secondBridge, 10);
                    break;
                }
                if (distance == 3 || !IsBridgeWater(nextTile) || _roadLinks.ContainsKey(nextTile)) break;
                if (distance == 1) bridge = nextTile;
                else secondBridge = nextTile;
                direction = ContinueRoadDirection(current, next, direction);
                current = next;
            }
        }
    }

    private List<int>? FindRoadPath(IEnumerable<int> origins, HashSet<int> targets, bool allowBridges)
    {
        var starts = origins.Where(IsRoadLandTerrain).ToHashSet();
        if (starts.Count == 0 || targets.Count == 0)
            return null;
        var village = GetPosition(starts.Min());
        // Search all destinations together so an inaccessible nearest village cannot
        // prevent a connection to a reachable road or village farther away.
        var preferred = GetPosition(targets.OrderBy(tile =>
            WrappedManhattanDistance(village, GetPosition(tile))).ThenBy(tile => tile).First());
        var previous = new Dictionary<int, int>();
        var bridges = new Dictionary<int, (int First, int Second)>();
        var costs = new Dictionary<int, long>();
        var pending = new PriorityQueue<int, (long Cost, int Distance, int Tile)>();
        foreach (var start in starts.Order())
        {
            previous[start] = -1;
            costs[start] = 0;
            pending.Enqueue(start, (0, WrappedManhattanDistance(GetPosition(start), preferred), start));
        }
        var end = -1;
        while (pending.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost != costs[current])
                continue;
            if (targets.Contains(current))
            {
                end = current;
                break;
            }
            foreach (var (next, bridge, secondBridge, stepCost) in RoadPathSteps(current, allowBridges))
            {
                if (!IsRoadTerrain(next) ||
                    (_roadLinks.ContainsKey(next) && !starts.Contains(next) && !targets.Contains(next)) ||
                    (!targets.Contains(next) && _apeStructures.TryGetValue(next, out var structure) &&
                        structure is ApeStructureKind.Village))
                    continue;
                var destination = GetPosition(next);
                var cost = costs[current] +
                    stepCost * RoadTerrainCostMultiplier(next) +
                    // Rivers are cheapest; lake spans cost less than shallows.
                    // Charge each water tile separately, including mixed spans.
                    (_surfaceWater[next] is SurfaceWaterKind.River ? 50 : 0) +
                    RoadBridgeCost(bridge) + RoadBridgeCost(secondBridge);
                if (costs.TryGetValue(next, out var known) && known <= cost)
                    continue;
                costs[next] = cost;
                previous[next] = current;
                bridges[next] = (bridge, secondBridge);
                pending.Enqueue(next, (cost, WrappedManhattanDistance(destination, preferred), next));
            }
        }
        if (end < 0)
            return null;

        var path = new List<int>();
        for (var tile = end; tile >= 0; tile = previous[tile])
        {
            path.Add(tile);
            if (bridges.TryGetValue(tile, out var span))
            {
                if (span.Second >= 0) path.Add(span.Second);
                if (span.First >= 0) path.Add(span.First);
            }
        }
        path.Reverse();

        // Terrain penalties must not make a new route run alongside an existing
        // road instead of joining it at the first contact.
        for (var i = 0; i < path.Count - 1; i++)
        {
            // Keep both banks of a straight bridge; never join a side road
            // from the water tile itself.
            if (!IsRoadLandTerrain(path[i])) continue;
            var contact = RoadNeighbors(path[i])
                .Where(tile => IsRoadLandTerrain(tile) && targets.Contains(tile) && _roadLinks.ContainsKey(tile))
                .OrderBy(RoadTerrainCostMultiplier)
                .ThenBy(tile => WrappedManhattanDistance(GetPosition(path[i]), GetPosition(tile)))
                .ThenBy(tile => tile).FirstOrDefault(-1);
            if (contact < 0)
                continue;
            path.RemoveRange(i + 1, path.Count - i - 1);
            path.Add(contact);
            break;
        }
        return path;
    }

    private void AddRoadPath(IReadOnlyList<int> path)
    {
        for (var i = 1; i < path.Count; i++)
        {
            if (!_roadLinks.TryGetValue(path[i - 1], out var first))
                _roadLinks[path[i - 1]] = first = [];
            if (!_roadLinks.TryGetValue(path[i], out var second))
                _roadLinks[path[i]] = second = [];
            first.Add(path[i]);
            second.Add(path[i - 1]);
        }
    }

    public bool RemoveVillageRoadAt(GridPosition position)
    {
        if (!Contains(position))
            return false;
        if (GetApeStructureVillage(position) is { } village)
        {
            if (!_roadVillages.Remove(GetIndex(village)))
                return false;
        }
        else if (!RemoveRoadTile(GetIndex(position)))
            return false;
        PruneUnusedRoadBranches();
        return true;
    }

    private void RemoveRoadsForVillage(int village)
    {
        _roadPopulationChecks.Remove(village);
        if (_roadVillages.Remove(village))
            PruneUnusedRoadBranches();
    }

    private void ValidateVillageRoads()
    {
        if (!_villageRoadValidationNeeded)
            return;
        _villageRoadValidationNeeded = false;
        VillageRoadValidationCount++;
        if (_roadVillages.RemoveWhere(tile => !IsRoadVillage(tile)) > 0)
            PruneUnusedRoadBranches();
        var blocked = _roadLinks.Keys.Where(tile => !IsRoadTerrain(tile)).ToHashSet();
        if (blocked.Count == 0)
            return;

        // Capture each affected network before cutting it; disconnected networks
        // elsewhere should not be rebuilt or gain new routes during a repair.
        var affected = new List<HashSet<int>>();
        var visited = new HashSet<int>();
        foreach (var tile in blocked.Order())
        {
            if (visited.Contains(tile))
                continue;
            var component = GetRoadComponent(tile);
            visited.UnionWith(component);
            affected.Add(component);
        }
        foreach (var tile in blocked)
            RemoveRoadTile(tile);
        foreach (var component in affected)
            RepairRoadNetwork(component);
        PruneUnusedRoadBranches();
    }

    private HashSet<int> GetRoadComponent(int start)
    {
        var result = new HashSet<int> { start };
        var pending = new Queue<int>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var tile))
            if (_roadLinks.TryGetValue(tile, out var neighbors))
                foreach (var neighbor in neighbors)
                    if (result.Add(neighbor))
                        pending.Enqueue(neighbor);
        return result;
    }

    private void RepairRoadNetwork(HashSet<int> formerNetwork)
    {
        var remaining = formerNetwork.Where(_roadLinks.ContainsKey).ToHashSet();
        var fragments = new List<HashSet<int>>();
        while (remaining.Count > 0)
        {
            var fragment = GetRoadComponent(remaining.Min());
            remaining.ExceptWith(fragment);
            // Detached stretches with no surviving village are no longer needed.
            if (fragment.Any(_roadVillages.Contains))
                fragments.Add(fragment);
            else
                foreach (var tile in fragment)
                    RemoveRoadTile(tile);
        }
        for (var i = 0; i < fragments.Count; i++)
        {
            while (i + 1 < fragments.Count)
            {
                var targets = fragments.Skip(i + 1).SelectMany(fragment => fragment).ToHashSet();
                var path = FindRoadPath(fragments[i], targets);
                if (path is null)
                    break; // No detour exists; keep the surviving networks separate.
                var joined = fragments.FindIndex(i + 1, fragment => fragment.Contains(path[^1]));
                AddRoadPath(path);
                fragments[i].UnionWith(fragments[joined]);
                fragments[i].UnionWith(path);
                fragments.RemoveAt(joined);
            }
        }
    }

    private bool RemoveRoadTile(int tile)
    {
        if (!_roadLinks.Remove(tile, out var neighbors))
            return false;
        foreach (var neighbor in neighbors)
            _roadLinks[neighbor].Remove(tile);
        _roadVillages.Remove(tile);
        return true;
    }

    private void PruneUnusedRoadBranches()
    {
        var pending = new Queue<int>(_roadLinks.Where(pair => pair.Value.Count <= 1).Select(pair => pair.Key));
        while (pending.TryDequeue(out var tile))
        {
            if (!_roadLinks.TryGetValue(tile, out var neighbors) || neighbors.Count > 1 ||
                (neighbors.Count == 1 && _roadVillages.Contains(tile)))
                continue;
            foreach (var neighbor in neighbors)
                pending.Enqueue(neighbor);
            RemoveRoadTile(tile);
        }
        RebuildRoadConnections();
    }

    private void RebuildRoadConnections()
    {
        InvalidateTransportNetwork();
        _roadConnections.Clear();
        foreach (var (tile, neighbors) in _roadLinks)
        foreach (var (neighbor, direction) in RoadNeighborSteps(tile))
        {
            if (!neighbors.Contains(neighbor) ||
                (Width == 2 && Body is not WorldBody.Planetoid && direction.X < 0)) continue;
            var connection = (direction.X, direction.Y) switch
            {
                (0, -1) => RiverConnection.North,
                (1, -1) => RiverConnection.NorthEast,
                (1, 0) => RiverConnection.East,
                (1, 1) => RiverConnection.SouthEast,
                (0, 1) => RiverConnection.South,
                (-1, 1) => RiverConnection.SouthWest,
                (-1, 0) => RiverConnection.West,
                _ => RiverConnection.NorthWest
            };
            _roadConnections[tile] = _roadConnections.GetValueOrDefault(tile) | connection;
        }

        var visited = new HashSet<int>();
        VillageRoadCount = 0;
        foreach (var tile in _roadLinks.Keys)
        {
            if (!visited.Add(tile))
                continue;
            var pending = new Queue<int>();
            pending.Enqueue(tile);
            var villages = 0;
            while (pending.TryDequeue(out var current))
            {
                if (_roadVillages.Contains(current))
                    villages++;
                foreach (var neighbor in _roadLinks[current])
                    if (visited.Add(neighbor))
                        pending.Enqueue(neighbor);
            }
            VillageRoadCount += Math.Max(0, villages - 1);
        }
    }
}
