namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private void AdvanceVillageGrowthFeeding()
    {
        var villages = _apeVillageFood.Where(pair =>
                (long)pair.Value * 2 > GetApeVillageFoodCapacityByTile(pair.Key))
            .Select(pair => pair.Key).ToHashSet();
        if (villages.Count == 0) return;
        // Group once rather than scanning every resident for every settlement.
        foreach (var residents in _apeVillageHomes.Where(pair => villages.Contains(pair.Value))
            .GroupBy(pair => pair.Value))
            FeedVillageForGrowth(residents.Key, residents.Select(pair => pair.Key));
    }

    internal void FeedVillageForGrowth(int village) => FeedVillageForGrowth(village,
        _apeVillageHomes.Where(pair => pair.Value == village).Select(pair => pair.Key));

    private void FeedVillageForGrowth(int village, IEnumerable<int> residents)
    {
        if (!_apeStructures.TryGetValue(village, out var structure) || structure is not ApeStructureKind.Village)
            return;
        var capacity = GetApeVillageFoodCapacityByTile(village);
        var food = _apeVillageFood.GetValueOrDefault(village);
        if ((long)food * 2 <= capacity) return;
        var dogsCanGrow = GetVillageDogCount(village) < GetVillageDogLimit(village);
        var candidates = residents.Where(id => _critterIndicesById.TryGetValue(id, out var index) &&
                (IsLivingApe(_species[index]) || _species[index] is CritterSpecies.Dog && dogsCanGrow) &&
                CanSpeciesReproduce(_species[index]) && !_plagues.ContainsKey(id) &&
                !_apeSettlerTargets.ContainsKey(id) && !IsApeFeedingBlocked(index) &&
                CanCritterRemainOnTile(index, GetIndex(_positions[index])))
            .Select(id => _critterIndicesById[id])
            .Select(index => (Index: index, Needed: CritterNutritions.Get(_species[index]).ReproductionThreshold - _energy[index]))
            .Where(candidate => candidate.Needed > 0).OrderBy(candidate => _critterIds[candidate.Index].Value).ToList();
        while ((long)food * 2 > capacity && candidates.Count > 0)
        {
            // No partial meals. A full meal may take stores below half; recheck afterward.
            var affordable = candidates.Where(candidate => candidate.Needed <= food).ToArray();
            if (affordable.Length == 0) break;
            var chosen = affordable[NextInt(affordable.Length)];
            _energy[chosen.Index] += chosen.Needed;
            food -= chosen.Needed;
            candidates.Remove(chosen);
        }
        _apeVillageFood[village] = food;
    }
}
