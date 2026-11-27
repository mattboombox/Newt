namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ApeSettlementRandomProbes = 128;
    internal const int ApeSettlementScanBudget = 512;
    internal const int ApeSettlementSiteBudgetPerTick = 2048;
    internal const int ApeWorkerPathSearchBudget = 1024;
    internal const int ApeMovementPathBudgetPerTick = 4096;
    internal long ApeSettlementSiteProbes { get; private set; }
    internal long ApePathExpandedTiles { get; private set; }
    private readonly Dictionary<int, int> _apeSettlementScanOffsets = [];
    private readonly Dictionary<int, (int Origin, int Target, CritterSpecies Species, long Revision, long Retry)> _apeWorkerPathFailures = [];
    private long _apeSpacingRevision = -1;
    private int _apeSpacingStructureCount = -1;
    private int[] _apeSpacingCenters = [];
    private long _apePathBudgetTick = -1;
    private int _apePathBudgetUsed;
    private long _apeSiteBudgetTick = -1;
    private int _apeSiteBudgetUsed;
    private sealed class WorkerRouteSearch(int start, int target, CritterSpecies species, long revision, bool settler, bool adjacent)
    {
        internal int Start = start, Target = target, Order;
        internal CritterSpecies Species = species;
        internal long Revision = revision, LastTick = -1;
        internal bool Settler = settler, Adjacent = adjacent;
        internal Dictionary<int, int> Parents = [], Steps = new() { [start] = 0 };
        internal PriorityQueue<(int Tile, int Steps), (int Estimate, int Order)> Frontier = new();
    }
    private readonly Dictionary<int, WorkerRouteSearch> _apeWorkerSearches = [];

    private bool TrySidestepApeSettler(int index, int target)
    {
        var best = -1;
        var distance = int.MaxValue;
        foreach (var direction in MovementDirections)
        {
            if (!TryGetPathNeighbor(_positions[index], direction, out var tile) ||
                _occupants[tile] >= 0 || !IsApeSettlerTransitTile(index, tile)) continue;
            var estimate = GetApeSettlerPathEstimate(tile, target);
            if (estimate >= distance) continue;
            best = tile;
            distance = estimate;
        }
        if (best < 0) return false;
        var id = _critterIds[index].Value;
        _apeWorkerSearches.Remove(id);
        _apeWorkerPathFailures.Remove(id);
        MoveCritter(index, best, GetPosition(best));
        return true;
    }

    private Queue<int>? FindBudgetedApeWorkerPath(int index, int start, int target,
        IReadOnlySet<GridPosition>? reserved, out bool pending, bool settler = false, bool stopAdjacent = false)
    {
        var id = _critterIds[index].Value;
        if (!_apeWorkerSearches.TryGetValue(id, out var search) || search.Start != start || search.Target != target ||
            search.Species != _species[index] || (!settler && search.Revision != _transportRevision) ||
            search.Settler != settler || search.Adjacent != stopAdjacent)
        {
            _apeWorkerSearches[id] = search = new(start, target, _species[index], _transportRevision, settler, stopAdjacent);
            search.Frontier.Enqueue((start, 0), (Estimate(start), search.Order++));
        }
        pending = true;
        if (search.LastTick == Tick) return null;
        if (_apePathBudgetTick != Tick) { _apePathBudgetTick = Tick; _apePathBudgetUsed = 0; }
        if (_apePathBudgetUsed >= ApeMovementPathBudgetPerTick) return null;
        search.LastTick = Tick;
        var expanded = 0;
        // Reserve a share for every potential caller, rather than letting the
        // first four difficult searches monopolize each movement turn.
        var callers = Math.Max(1, _apeSettlerTargets.Count + _apeWorkplaces.Count + _apeWorkerSearches.Count);
        var allowance = Math.Max(1, Math.Min(ApeWorkerPathSearchBudget, ApeMovementPathBudgetPerTick / callers));
        while (expanded < allowance && _apePathBudgetUsed < ApeMovementPathBudgetPerTick &&
            search.Frontier.TryDequeue(out var node, out _))
        {
            if (search.Steps[node.Tile] != node.Steps) continue;
            if (node.Tile == target || stopAdjacent && ArePathNeighbors(node.Tile, target))
            {
                var route = new Stack<int>();
                for (var tile = node.Tile; tile != start; tile = search.Parents[tile]) route.Push(tile);
                _apeWorkerSearches.Remove(id);
                pending = false;
                return new Queue<int>(route);
            }
            expanded++;
            _apePathBudgetUsed++;
            ApePathExpandedTiles++;
            foreach (var direction in MovementDirections)
            {
                // Distant occupants move before we arrive. Check traffic at the
                // first step so a crowd cannot invalidate the entire land route.
                if (!TryGetPathNeighbor(GetPosition(node.Tile), direction, out var next) ||
                    !(settler ? IsApeSettlerTransitTile(index, next) : CanCritterLiveOn(index, next)) ||
                    reserved?.Contains(GetPosition(next)) is true ||
                    (!settler || node.Tile == start) && _occupants[next] >= 0 && _occupants[next] != index &&
                        !CanShoveMovementBlocker(index, next, reserved) && !CanSwapPastMovementBlocker(index, next, reserved, node.Tile)) continue;
                var steps = node.Steps + 1;
                if (search.Steps.TryGetValue(next, out var known) && known <= steps) continue;
                search.Parents[next] = node.Tile;
                search.Steps[next] = steps;
                search.Frontier.Enqueue((next, steps), (steps + Estimate(next), search.Order++));
            }
        }
        pending = search.Frontier.Count > 0;
        if (!pending) _apeWorkerSearches.Remove(id);
        return null;

        int Estimate(int tile) => Math.Max(0, (Body == WorldBody.Planetoid
            ? SurfacePathLowerBound(tile, target) : GetApeSettlerPathEstimate(tile, target)) - (stopAdjacent ? 1 : 0));
    }

    private bool IsFarEnoughFromCachedVillages(int candidate)
    {
        if (_apeSpacingRevision != _transportRevision || _apeSpacingStructureCount != _apeStructures.Count)
        {
            _apeSpacingCenters = _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village).Select(pair => pair.Key).ToArray();
            _apeSpacingRevision = _transportRevision;
            _apeSpacingStructureCount = _apeStructures.Count;
        }
        var position = GetPosition(candidate);
        foreach (var village in _apeSpacingCenters)
            if (WrappedManhattanDistance(position, GetPosition(village)) <= ApeVillageMinimumDistance) return false;
        return true;
    }
}
