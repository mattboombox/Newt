namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<(int First, int Second), int[]> _villageRoads = [];
    private readonly Dictionary<int, RiverConnection> _roadConnections = [];
    public int VillageRoadCount => _villageRoads.Count;

    public RiverConnection GetRoadConnections(GridPosition position) =>
        _roadConnections.GetValueOrDefault(GetIndex(position));

    private bool IsRoadVillage(int tile) =>
        _apeStructures.TryGetValue(tile, out var kind) && kind is ApeStructureKind.Village &&
        !_barbarianVillageTiles.Contains(tile);

    private readonly Dictionary<int, int> _roadPopulationChecks = [];

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

    public bool TryPlaceVillageRoad(GridPosition position, bool force = true)
    {
        if (!Contains(position) || GetApeStructureVillage(position) is not { } village)
            return false;
        var start = GetIndex(village);
        if (!IsRoadVillage(start) || (!force && GetApeVillageResidentCountByTile(start) < 50))
            return false;
        var end = _apeStructures.Keys.Where(tile => tile != start && IsRoadVillage(tile) &&
                (force || GetApeVillageResidentCountByTile(tile) >= 50) &&
                !_villageRoads.ContainsKey(start < tile ? (start, tile) : (tile, start)))
            .OrderBy(tile => WrappedManhattanDistance(village, GetPosition(tile)))
            .ThenBy(tile => tile).FirstOrDefault(-1);
        if (end < 0 || !IsRoadTerrain(start) || !IsRoadTerrain(end))
            return false;
        var key = start < end ? (start, end) : (end, start);
        if (_villageRoads.ContainsKey(key))
            return false;

        var previous = new Dictionary<int, int> { [start] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(start);
        while (pending.TryDequeue(out var current) && !previous.ContainsKey(end))
        {
            var origin = GetPosition(current);
            foreach (var direction in MovementDirections.OrderBy(direction =>
                WrappedManhattanDistance(new GridPosition(Mod(origin.X + direction.X, Width), origin.Y + direction.Y), GetPosition(end))))
            {
                var next = new GridPosition(Mod(origin.X + direction.X, Width), origin.Y + direction.Y);
                if (!Contains(next))
                    continue;
                var tile = GetIndex(next);
                if (previous.ContainsKey(tile) || !IsRoadTerrain(tile) ||
                    (tile != end && _apeStructures.TryGetValue(tile, out var structure) &&
                        structure is ApeStructureKind.Village))
                    continue;
                previous[tile] = current;
                pending.Enqueue(tile);
            }
        }
        if (!previous.ContainsKey(end))
            return false;
        var path = new List<int>();
        for (var tile = end; tile >= 0; tile = previous[tile])
            path.Add(tile);
        path.Reverse();
        _villageRoads[key] = path.ToArray();
        RebuildRoadConnections();
        return true;
    }

    public bool RemoveVillageRoadAt(GridPosition position)
    {
        if (!Contains(position))
            return false;
        var tile = GetIndex(position);
        var owner = GetApeStructureVillage(position);
        var village = owner is { } center ? GetIndex(center) : -1;
        return RemoveVillageRoadsWhere(pair => pair.Key.First == village || pair.Key.Second == village ||
            pair.Value.Contains(tile));
    }

    private void RemoveRoadsForVillage(int village)
    {
        _roadPopulationChecks.Remove(village);
        RemoveVillageRoadsWhere(pair => pair.Key.First == village || pair.Key.Second == village);
    }

    private void ValidateVillageRoads() => RemoveVillageRoadsWhere(pair =>
        !IsRoadVillage(pair.Key.First) || !IsRoadVillage(pair.Key.Second));

    private bool RemoveVillageRoadsWhere(Func<KeyValuePair<(int First, int Second), int[]>, bool> predicate)
    {
        var removed = false;
        foreach (var pair in _villageRoads.ToArray())
            if (predicate(pair))
                removed |= _villageRoads.Remove(pair.Key);
        if (removed)
            RebuildRoadConnections();
        return removed;
    }

    private void RebuildRoadConnections()
    {
        _roadConnections.Clear();
        foreach (var path in _villageRoads.Values)
        for (var index = 1; index < path.Length; index++)
        {
            var first = GetPosition(path[index - 1]);
            var second = GetPosition(path[index]);
            var east = second.X != first.X && second.X == Mod(first.X + 1, Width);
            var west = second.X != first.X && !east;
            var direction = second.Y < first.Y
                ? east ? RiverConnection.NorthEast : west ? RiverConnection.NorthWest : RiverConnection.North
                : second.Y > first.Y
                    ? east ? RiverConnection.SouthEast : west ? RiverConnection.SouthWest : RiverConnection.South
                    : east ? RiverConnection.East : RiverConnection.West;
            var opposite = direction switch
            {
                RiverConnection.North => RiverConnection.South,
                RiverConnection.South => RiverConnection.North,
                RiverConnection.East => RiverConnection.West,
                RiverConnection.West => RiverConnection.East,
                RiverConnection.NorthEast => RiverConnection.SouthWest,
                RiverConnection.NorthWest => RiverConnection.SouthEast,
                RiverConnection.SouthEast => RiverConnection.NorthWest,
                _ => RiverConnection.NorthEast,
            };
            _roadConnections[path[index - 1]] = _roadConnections.GetValueOrDefault(path[index - 1]) | direction;
            _roadConnections[path[index]] = _roadConnections.GetValueOrDefault(path[index]) | opposite;
        }
    }
}
