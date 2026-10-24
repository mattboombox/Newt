namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int FarmBlightSpreadIntervalTicks = 30 * TicksPerSecond;
    public const int FarmBlightLifetimeTicks = SeasonSystem.TicksPerYear;
    private readonly Dictionary<int, (long Expires, long NextSpread)> _farmBlight = [];
    private long _nextFarmBlightActionTick = long.MaxValue;

    private static bool IsBlightSusceptible(ApeStructureKind kind) =>
        kind is ApeStructureKind.Farm or ApeStructureKind.RicePaddy or ApeStructureKind.Orchard;

    public long? GetFarmBlightRecoveryTick(GridPosition position) =>
        Contains(position) && _farmBlight.TryGetValue(GetIndex(position), out var blight) ? blight.Expires : null;

    public bool TryBlightFarmAt(GridPosition position)
    {
        if (!LifeEnabled || !Contains(position)) return false;
        var tile = GetIndex(position);
        if (!_apeStructures.TryGetValue(tile, out var kind) || !IsBlightSusceptible(kind)) return false;
        // Removing the working farm releases its staff and removes food production,
        // storage, and ownership. Blighted land survives even if its village disappears.
        RemoveApeStructureAt(position);
        SetApeStructure(tile, ApeStructureKind.BlightedFarm);
        _farmBlight[tile] = (Tick + FarmBlightLifetimeTicks, Tick + FarmBlightSpreadIntervalTicks);
        _nextFarmBlightActionTick = Math.Min(_nextFarmBlightActionTick, Tick + FarmBlightSpreadIntervalTicks);
        return true;
    }

    internal bool TryStartVillageBlight(int village)
    {
        var farms = _apeAuxiliaryVillages.Where(pair => pair.Value == village &&
            _apeStructures.TryGetValue(pair.Key, out var kind) && IsBlightSusceptible(kind))
            .Select(pair => pair.Key).Order().ToArray();
        return farms.Length > 0 && TryBlightFarmAt(GetPosition(farms[NextInt(farms.Length)]));
    }

    internal void AdvanceFarmBlight()
    {
        if (!LifeEnabled || _farmBlight.Count == 0 || Tick < _nextFarmBlightActionTick) return;
        HashSet<int>? infections = null;
        foreach (var (tile, blight) in _farmBlight.ToArray())
        {
            if (Tick >= blight.Expires)
            {
                RemoveApeStructureAt(GetPosition(tile));
                continue;
            }
            if (Tick < blight.NextSpread) continue;
            var origin = GetPosition(tile);
            foreach (var direction in MovementDirections)
            {
                var adjacent = new GridPosition(Mod(origin.X + direction.X, Width), origin.Y + direction.Y);
                if (!Contains(adjacent)) continue;
                var neighbor = GetIndex(adjacent);
                if (_apeStructures.TryGetValue(neighbor, out var kind) && IsBlightSusceptible(kind))
                    (infections ??= []).Add(neighbor);
            }
            _farmBlight[tile] = (blight.Expires, Tick + FarmBlightSpreadIntervalTicks);
        }
        // Stage infections so a single update cannot sweep through a whole farm chain.
        if (infections is not null)
            foreach (var tile in infections.Order()) TryBlightFarmAt(GetPosition(tile));
        _nextFarmBlightActionTick = _farmBlight.Count == 0 ? long.MaxValue :
            _farmBlight.Values.Min(blight => Math.Min(blight.Expires, blight.NextSpread));
    }
}
