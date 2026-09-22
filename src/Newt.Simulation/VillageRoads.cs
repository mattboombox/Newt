namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    // Shared tile edges form a forest. Villages are terminals, not owners of routes.
    private readonly Dictionary<int, HashSet<int>> _roadLinks = [];
    private readonly HashSet<int> _roadVillages = [];
    private readonly Dictionary<int, RiverConnection> _roadConnections = [];
    private readonly Dictionary<int, int> _roadPopulationChecks = [];

    /// <summary>Number of village-to-network links across all connected road networks.</summary>
    public int VillageRoadCount { get; private set; }

    public RiverConnection GetRoadConnections(GridPosition position) =>
        _roadConnections.GetValueOrDefault(GetIndex(position));

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

    private bool IsRoadTerrain(int tile) =>
        _terrain[tile] is not (Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or
            Terrain.Ice or Terrain.Mountain or Terrain.RingWorldWall) &&
        _surfaceWater[tile] is not SurfaceWaterKind.FreshwaterLake &&
        _surfaceCovers[tile] is SurfaceCover.None;

    private int RoadTerrainCostMultiplier(int tile) =>
        _terrain[tile] is Terrain.Hills or Terrain.Canyon or Terrain.Beach or Terrain.Trench ||
        _biomes[tile] is Biome.Swamp or Biome.Bog or Biome.Jungle ? 3 : 1;

    private IEnumerable<int> RoadNeighbors(int tile)
    {
        var position = GetPosition(tile);
        foreach (var direction in MovementDirections)
        {
            var next = new GridPosition(Mod(position.X + direction.X, Width), position.Y + direction.Y);
            if (Contains(next) && next != position)
                yield return GetIndex(next);
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

        var targets = _roadLinks.Keys.Where(IsRoadTerrain).ToHashSet();
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

    private List<int>? FindRoadPath(IEnumerable<int> origins, HashSet<int> targets)
    {
        var starts = origins.Where(IsRoadTerrain).ToHashSet();
        if (starts.Count == 0 || targets.Count == 0)
            return null;
        var village = GetPosition(starts.Min());
        // Search all destinations together so an inaccessible nearest village cannot
        // prevent a connection to a reachable road or village farther away.
        var preferred = GetPosition(targets.OrderBy(tile =>
            WrappedManhattanDistance(village, GetPosition(tile))).ThenBy(tile => tile).First());
        var previous = new Dictionary<int, int>();
        var costs = new Dictionary<int, int>();
        var pending = new PriorityQueue<int, (int Cost, int Distance, int Tile)>();
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
            var origin = GetPosition(current);
            foreach (var next in RoadNeighbors(current))
            {
                if (!IsRoadTerrain(next) ||
                    (_roadLinks.ContainsKey(next) && !starts.Contains(next) && !targets.Contains(next)) ||
                    (!targets.Contains(next) && _apeStructures.TryGetValue(next, out var structure) &&
                        structure is ApeStructureKind.Village))
                    continue;
                var destination = GetPosition(next);
                var cost = costs[current] +
                    (origin.X != destination.X && origin.Y != destination.Y ? 14 : 10) * RoadTerrainCostMultiplier(next);
                if (costs.TryGetValue(next, out var known) && known <= cost)
                    continue;
                costs[next] = cost;
                previous[next] = current;
                pending.Enqueue(next, (cost, WrappedManhattanDistance(destination, preferred), next));
            }
        }
        if (end < 0)
            return null;

        var path = new List<int>();
        for (var tile = end; tile >= 0; tile = previous[tile])
            path.Add(tile);
        path.Reverse();

        // Terrain penalties must not make a new route run alongside an existing
        // road instead of joining it at the first contact.
        for (var i = 0; i < path.Count - 1; i++)
        {
            var contact = RoadNeighbors(path[i])
                .Where(tile => targets.Contains(tile) && _roadLinks.ContainsKey(tile))
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
        _roadConnections.Clear();
        foreach (var (tile, neighbors) in _roadLinks)
        foreach (var neighbor in neighbors)
        {
            var first = GetPosition(tile);
            var second = GetPosition(neighbor);
            var east = second.X != first.X && second.X == Mod(first.X + 1, Width);
            var west = second.X != first.X && !east;
            var direction = second.Y < first.Y
                ? east ? RiverConnection.NorthEast : west ? RiverConnection.NorthWest : RiverConnection.North
                : second.Y > first.Y
                    ? east ? RiverConnection.SouthEast : west ? RiverConnection.SouthWest : RiverConnection.South
                    : east ? RiverConnection.East : RiverConnection.West;
            _roadConnections[tile] = _roadConnections.GetValueOrDefault(tile) | direction;
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
