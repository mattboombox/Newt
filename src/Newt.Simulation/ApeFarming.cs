namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _apeFarmers = [];
    private readonly Dictionary<int, long> _apeFarmerRecruitmentTicks = [];
    private readonly Dictionary<int, long> _apeFoodExpansionTicks = [];
    private readonly HashSet<int> _apeVillagesWithMultipleResidents = [];

    private void TryBuildEmergencyApeFarm(int village)
    {
        if (_apeVillageFood.GetValueOrDefault(village) >= 10 ||
            Tick < _apeFoodExpansionTicks.GetValueOrDefault(village))
            return;
        var available = _apeVillageHomes.Count(pair => pair.Value == village &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            _species[index] is CritterSpecies.Ape && !_plagues.ContainsKey(pair.Key) &&
            !_apeSettlerTargets.ContainsKey(pair.Key) && !_apeCarriedFood.ContainsKey(pair.Key));
        var unstaffed = _apeAuxiliaryVillages.Count(pair => pair.Value == village &&
            _apeStructures.TryGetValue(pair.Key, out var kind) && IsApeFoodDistrict(kind) &&
            IsApeFoodDistrictActive(pair.Key, kind) && !HasApeFarmer(pair.Key));
        if (available > 0 && unstaffed == 0)
            TryBuildApeFoodDistrict(village);
    }

    private bool HasApeFarmer(int farmTile) =>
        _apeFarmers.TryGetValue(farmTile, out var id) &&
        _critterIndicesById.TryGetValue(id, out var index) &&
        _species[index] is CritterSpecies.ApeFarmer &&
        _apeAuxiliaryVillages.TryGetValue(farmTile, out var village) &&
        _apeVillageHomes.TryGetValue(id, out var home) && home == village;

    private void TryAssignLoneApeFarmer(int village)
    {
        foreach (var (id, home) in _apeVillageHomes)
        {
            if (home != village || !_critterIndicesById.TryGetValue(id, out var index) ||
                !IsLivingApe(_species[index]) || !CanRecruitApe(index) || _apeSettlerTargets.ContainsKey(id))
            {
                continue;
            }

            if (_apeWorkplaces.TryGetValue(id, out var workplace) && HasApeFarmer(workplace) &&
                _apeStructures.TryGetValue(workplace, out var kind) && IsApeFoodDistrictActive(workplace, kind))
            {
                return;
            }

            var position = _positions[index];
            if (!CanLiveOn(CritterSpecies.ApeFarmer, GetIndex(position)))
                return;

            var farm = FindActiveFarm(position);
            if (farm < 0 && TryBuildApeFoodDistrict(village))
                farm = FindActiveFarm(position);
            if (farm >= 0)
                AssignApeFarmer(id, farm);
            return;
        }

        int FindActiveFarm(GridPosition position) =>
            _apeAuxiliaryVillages.Where(pair => pair.Value == village &&
                _apeStructures.TryGetValue(pair.Key, out var kind) && IsApeFoodDistrict(kind) &&
                IsApeFoodDistrictActive(pair.Key, kind))
            .OrderBy(pair => WrappedManhattanDistance(position, GetPosition(pair.Key)))
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .FirstOrDefault(-1);
    }

    private void AdvanceApeFarmerRecruitment(int villageTile, int farmTile, ApeStructureKind structure)
    {
        if (!IsApeFoodDistrictActive(farmTile, structure))
            return;
        if (HasApeFarmer(farmTile))
            return;
        if (_apeFarmers.Remove(farmTile, out var previousId) &&
            _apeWorkplaces.GetValueOrDefault(previousId, -1) == farmTile)
            _apeWorkplaces.Remove(previousId);
        if (!_apeFarmerRecruitmentTicks.TryGetValue(farmTile, out var due))
        {
            _apeFarmerRecruitmentTicks[farmTile] = Tick + 30 * TicksPerSecond;
            return;
        }
        if (Tick < due)
            return;
        _apeFarmerRecruitmentTicks[farmTile] = Tick + 30 * TicksPerSecond;
        var recruitId = _apeVillageHomes.Where(pair => pair.Value == villageTile &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is CritterSpecies.Ape && !_plagues.ContainsKey(pair.Key) &&
                !_apeSettlerTargets.ContainsKey(pair.Key) && !_apeCarriedFood.ContainsKey(pair.Key))
            .OrderBy(pair => WrappedManhattanDistance(
                _positions[_critterIndicesById[pair.Key]], GetPosition(farmTile)))
            .ThenBy(pair => pair.Key).Select(pair => pair.Key).FirstOrDefault();
        if (recruitId == 0)
            return;
        AssignApeFarmer(recruitId, farmTile);
    }

    private void AssignApeFarmer(int recruitId, int farmTile)
    {
        var index = _critterIndicesById[recruitId];
        if (_species[index] is CritterSpecies.ApeFarmer)
            DetachApeWorker(recruitId);
        else
            ChangeCritterSpecies(index, CritterSpecies.ApeFarmer, preserveEnergy: true, preserveApeVillage: true);
        _apeFarmers[farmTile] = recruitId;
        _apeWorkplaces[recruitId] = farmTile;
        _apeFarmerRecruitmentTicks.Remove(farmTile);
    }

    private void RemoveApeFarmStaff(int farmTile)
    {
        _apeFarmerRecruitmentTicks.Remove(farmTile);
        if (!_apeFarmers.Remove(farmTile, out var id))
            return;
        if (_apeWorkplaces.GetValueOrDefault(id, -1) == farmTile)
            _apeWorkplaces.Remove(id);
        if (_critterIndicesById.TryGetValue(id, out var index) &&
            _species[index] is CritterSpecies.ApeFarmer)
            ChangeCritterSpecies(index, CritterSpecies.Ape,
                preserveEnergy: true, preserveApeVillage: true);
    }
}
