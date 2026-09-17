namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int VillageFeastCheckIntervalTicks = 60 * TicksPerSecond;
    public const int VillageFeastChancePercent = 1;
    private readonly HashSet<int> _villageFeasts = [];

    public bool IsVillageFeasting(GridPosition position) =>
        Contains(position) && _villageFeasts.Contains(GetIndex(position));

    private bool CanHoldVillageFeast(int tile) =>
        _apeStructures.TryGetValue(tile, out var structure) && structure is ApeStructureKind.Village &&
        !_barbarianVillageTiles.Contains(tile) && _apeVillageFood.GetValueOrDefault(tile) > 0;

    public bool TryStartVillageFeast(GridPosition position)
    {
        if (!Contains(position))
            return false;
        var tile = GetIndex(position);
        if (!CanHoldVillageFeast(tile) || !_villageFeasts.Add(tile))
            return false;
        FeedVillageFeast(tile);
        return true;
    }

    internal bool TryStartNaturalVillageFeast(int tile) =>
        NaturalEventsEnabled && CanHoldVillageFeast(tile) && !_villageFeasts.Contains(tile) &&
        NextInt(100) < VillageFeastChancePercent && TryStartVillageFeast(GetPosition(tile));

    private void AdvanceVillageFeasts()
    {
        foreach (var tile in _villageFeasts.ToArray())
            FeedVillageFeast(tile);
        if (!NaturalEventsEnabled || Tick == 0 || Tick % VillageFeastCheckIntervalTicks != 0)
            return;
        foreach (var (tile, kind) in _apeStructures)
            if (kind is ApeStructureKind.Village)
                TryStartNaturalVillageFeast(tile);
    }

    private void FeedVillageFeast(int tile)
    {
        if (!CanHoldVillageFeast(tile))
        {
            _villageFeasts.Remove(tile);
            return;
        }
        var candidates = _apeVillageHomes.Where(pair => pair.Value == tile &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            IsLivingApe(_species[index]) && CanSpeciesReproduce(_species[index]) &&
            !_plagues.ContainsKey(pair.Key) && !_apeSettlerTargets.ContainsKey(pair.Key) &&
            !IsApeFeedingBlocked(index) &&
            _energy[index] < CritterNutritions.Get(_species[index]).ReproductionThreshold)
            .Select(pair => pair.Key).Order().ToList();
        var food = _apeVillageFood[tile];
        while (food > 0 && candidates.Count > 0)
        {
            var choice = NextInt(candidates.Count);
            var index = _critterIndicesById[candidates[choice]];
            candidates.RemoveAt(choice);
            var amount = Math.Min(food, CritterNutritions.Get(_species[index]).ReproductionThreshold - _energy[index]);
            _energy[index] += amount;
            food -= amount;
        }
        _apeVillageFood[tile] = food;
        // If everyone is ready, resume after births open up appetites instead of wasting food.
        if (food == 0)
            _villageFeasts.Remove(tile);
    }
}
