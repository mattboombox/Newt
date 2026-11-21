using System.Numerics;

namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private sealed record VillagePerimeter(long Revision, long BuiltAt, int[] Tiles);
    private readonly Dictionary<int, VillagePerimeter> _villagePerimeters = [];
    private readonly Dictionary<int, int> _warriorPatrolTargets = [];

    public int? GetApeMilitaryDistrictNumber(GridPosition position)
    {
        var tile = GetIndex(position);
        if (_apeStructures.GetValueOrDefault(tile) is not ApeStructureKind.MilitaryDistrict ||
            !_apeAuxiliaryVillages.TryGetValue(tile, out var village)) return null;
        return Array.IndexOf(GetVillageMilitaryDistricts(village), tile) + 1;
    }

    private int[] GetVillageMilitaryDistricts(int village) => _apeAuxiliaryVillages
        .Where(pair => pair.Value == village && _apeStructures.GetValueOrDefault(pair.Key) is ApeStructureKind.MilitaryDistrict)
        .Select(pair => pair.Key).Order().ToArray();

    public int? GetApeWarriorSquadNumber(CritterId id)
    {
        if (!_critterIndicesById.TryGetValue(id.Value, out var index) ||
            !_apeVillageHomes.TryGetValue(id.Value, out var village) || IsBarbarianApe(index)) return null;
        return CreateVillageSquadRoster(village, includeWar: true).TryGetValue(id.Value, out var squad)
            ? squad.Number : null;
    }

    private int[] GetVillagePerimeter(int village)
    {
        if (_villagePerimeters.TryGetValue(village, out var cached) && cached.Revision == _transportRevision &&
            Tick - cached.BuiltAt < 30 * TicksPerSecond) return cached.Tiles;
        var buildings = EnumerateConnectedApeStructureTiles(village)
            .Where(tile => _apeStructures.GetValueOrDefault(tile) is not ApeStructureKind.Ruin).ToHashSet();
        var distances = buildings.ToDictionary(tile => tile, _ => 0);
        var pending = new Queue<int>(buildings);
        var perimeter = new List<int>();
        while (pending.TryDequeue(out var tile))
        {
            var distance = distances[tile];
            if (distance == 2)
            {
                if (CanLiveOn(CritterSpecies.ApeWarrior, tile)) perimeter.Add(tile);
                continue;
            }
            foreach (var next in RoadNeighbors(tile))
                if (!distances.ContainsKey(next) && CanLiveOn(CritterSpecies.ApeWarrior, next))
                {
                    distances[next] = distance + 1;
                    pending.Enqueue(next);
                }
        }
        var home = GetPosition(village);
        var normal = Body == WorldBody.Planetoid ? GetPlanetoidTileDirection(home) : Vector3.UnitY;
        var east = Body == WorldBody.Planetoid
            ? Vector3.Normalize(Vector3.Cross(Math.Abs(normal.Y) < 0.95f ? Vector3.UnitY : Vector3.UnitZ, normal))
            : Vector3.UnitX;
        var south = Vector3.Cross(normal, east);
        double Angle(int tile)
        {
            var position = GetPosition(tile);
            if (Body == WorldBody.Planetoid)
            {
                var offset = GetPlanetoidTileDirection(position) - normal;
                return Math.Atan2(Vector3.Dot(offset, south), Vector3.Dot(offset, east));
            }
            var dx = position.X - home.X;
            if (dx > Width / 2) dx -= Width;
            if (dx < -Width / 2) dx += Width;
            return Math.Atan2(position.Y - home.Y, dx);
        }
        var tiles = perimeter.OrderBy(Angle).ThenBy(tile => tile).ToArray();
        _villagePerimeters[village] = new(_transportRevision, Tick, tiles);
        return tiles;
    }

    private void MoveVillagePerimeterPatrol(int index, int village, int squadNumber,
        IReadOnlySet<GridPosition>? reserved)
    {
        var perimeter = GetVillagePerimeter(village);
        if (perimeter.Length == 0) return;
        var id = _critterIds[index].Value;
        var current = GetIndex(_positions[index]);
        var waypoint = _warriorPatrolTargets.TryGetValue(id, out var target) ? Array.IndexOf(perimeter, target) : -1;
        if (waypoint < 0)
        {
            // Start near the squad; alternating directions keep squads circulating.
            waypoint = Enumerable.Range(0, perimeter.Length)
                .OrderBy(i => WrappedManhattanDistance(GetPosition(current), GetPosition(perimeter[i])))
                .ThenBy(i => (i + squadNumber * 7) % perimeter.Length).First();
        }
        var direction = squadNumber % 2 == 0 ? -1 : 1;
        for (var attempt = 0; attempt < Math.Min(perimeter.Length, 4); attempt++)
        {
            if (perimeter[waypoint] == current)
                waypoint = Mod(waypoint + direction, perimeter.Length);
            _warriorPatrolTargets[id] = perimeter[waypoint];
            TryMoveAlongSurfacePath(index, perimeter[waypoint], reserved);
            if (GetIndex(_positions[index]) != current) return;
            waypoint = Mod(waypoint + direction, perimeter.Length);
        }
        // Congested or temporarily unreachable patrol goals must not park a squad.
        foreach (var neighbor in RoadNeighbors(current))
            if (_occupants[neighbor] is var blocker && blocker >= 0 && AreFriendlyMilitary(index, blocker) &&
                TrySwapPastMovementBlocker(index, neighbor, reserved)) return;
    }
}
