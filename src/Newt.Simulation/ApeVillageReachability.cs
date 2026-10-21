namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int ApeVillageReachabilityIntervalTicks = 5 * 60 * TicksPerSecond;
    private readonly Dictionary<int, long> _apeVillageReachabilityTicks = [];
    private long _lastApeVillageReachabilityTick = -1;
    private readonly Queue<int> _apeReachabilityFrontier = new();
    private int[]? _apeReachabilityVisits;
    private int _apeReachabilityGeneration;

    private bool IsUnemployedResidentApe(int index) =>
        _species[index] is CritterSpecies.Ape &&
        !_apeWorkplaces.ContainsKey(_critterIds[index].Value) &&
        !_apeSettlerTargets.ContainsKey(_critterIds[index].Value) &&
        !IsBarbarianApe(index);

    internal void CheckApeVillageReachability(int index)
    {
        var id = _critterIds[index].Value;
        if (!IsUnemployedResidentApe(index) ||
            !_apeVillageHomes.TryGetValue(id, out var village) ||
            _lastApeVillageReachabilityTick == Tick)
            return;

        if (!_apeVillageReachabilityTicks.TryGetValue(id, out var nextTick))
        {
            // Delay initial checks by 2.5–5 minutes and spread consecutive IDs across
            // that window so newly loaded populations do not cause a burst of searches.
            var halfInterval = ApeVillageReachabilityIntervalTicks / 2;
            var stagger = unchecked((uint)id * 2654435761u) % (uint)halfInterval;
            _apeVillageReachabilityTicks[id] = Tick + halfInterval + stagger;
            return;
        }
        if (Tick < nextTick)
            return;

        _lastApeVillageReachabilityTick = Tick;
        _apeVillageReachabilityTicks[id] = Tick + ApeVillageReachabilityIntervalTicks;
        if (!CanApeReachVillage(index, village))
            DetachApeFromVillage(id);
    }

    private bool CanApeReachVillage(int index, int village)
    {
        if (IsAtOrAdjacentToConnectedApeStructure(index, village))
            return true;

        var goals = new HashSet<int>();
        foreach (var structure in EnumerateConnectedApeStructureTiles(village))
        {
            goals.Add(structure);
            var position = GetPosition(structure);
            foreach (var direction in MovementDirections)
            {
                var y = position.Y + direction.Y;
                if (y >= 0 && y < Height)
                    goals.Add(GetIndex(new GridPosition(Mod(position.X + direction.X, Width), y)));
            }
        }

        var visits = _apeReachabilityVisits ??= new int[_terrain.Length];
        if (_apeReachabilityGeneration == int.MaxValue)
        {
            Array.Clear(visits);
            _apeReachabilityGeneration = 0;
        }
        var generation = ++_apeReachabilityGeneration;
        var start = GetIndex(_positions[index]);
        visits[start] = generation;
        _apeReachabilityFrontier.Clear();
        _apeReachabilityFrontier.Enqueue(start);
        while (_apeReachabilityFrontier.TryDequeue(out var tile))
        {
            if (goals.Contains(tile))
                return true;
            var position = GetPosition(tile);
            foreach (var direction in MovementDirections)
            {
                var y = position.Y + direction.Y;
                if (y < 0 || y >= Height)
                    continue;
                var neighbor = GetIndex(new GridPosition(Mod(position.X + direction.X, Width), y));
                // Occupants are temporary obstacles; habitat restrictions determine stranding.
                if (visits[neighbor] == generation || !CanCritterLiveOn(index, neighbor))
                    continue;
                visits[neighbor] = generation;
                _apeReachabilityFrontier.Enqueue(neighbor);
            }
        }
        return false;
    }
}
