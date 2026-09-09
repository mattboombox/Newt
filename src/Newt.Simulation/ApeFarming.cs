namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _apeFarmers = [];
    private readonly Dictionary<int, long> _apeFarmerRecruitmentTicks = [];

    private bool HasApeFarmer(int farmTile) =>
        _apeFarmers.TryGetValue(farmTile, out var id) &&
        _critterIndicesById.TryGetValue(id, out var index) &&
        _species[index] is CritterSpecies.ApeFarmer &&
        _apeAuxiliaryVillages.TryGetValue(farmTile, out var village) &&
        _apeVillageHomes.TryGetValue(id, out var home) && home == village;

    private void AdvanceApeFarmerRecruitment(int villageTile, int farmTile, ApeStructureKind structure)
    {
        if (!IsApeFoodDistrictActive(farmTile, structure))
            return;
        if (HasApeFarmer(farmTile))
            return;
        _apeFarmers.Remove(farmTile);
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
        ChangeCritterSpecies(_critterIndicesById[recruitId], CritterSpecies.ApeFarmer,
            preserveEnergy: true, preserveApeVillage: true);
        _apeFarmers[farmTile] = recruitId;
        _apeFarmerRecruitmentTicks.Remove(farmTile);
    }

    private void RemoveApeFarmStaff(int farmTile)
    {
        _apeFarmerRecruitmentTicks.Remove(farmTile);
        if (_apeFarmers.Remove(farmTile, out var id) &&
            _critterIndicesById.TryGetValue(id, out var index) &&
            _species[index] is CritterSpecies.ApeFarmer)
            ChangeCritterSpecies(index, CritterSpecies.Ape,
                preserveEnergy: true, preserveApeVillage: true);
    }
}
