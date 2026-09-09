namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _apeLumberjacks = [];
    private readonly Dictionary<int, long> _apeLumberjackRecruitmentTicks = [];

    private bool HasApeLumberjack(int campTile) =>
        _apeLumberjacks.TryGetValue(campTile, out var id) &&
        _critterIndicesById.TryGetValue(id, out var index) &&
        _species[index] is CritterSpecies.ApeLumberjack &&
        _apeAuxiliaryVillages.TryGetValue(campTile, out var village) &&
        _apeVillageHomes.TryGetValue(id, out var home) && home == village;

    private void AdvanceApeLumberjackRecruitment(int villageTile, int campTile)
    {
        if (HasApeLumberjack(campTile))
            return;
        _apeLumberjacks.Remove(campTile);
        if (!_apeLumberjackRecruitmentTicks.TryGetValue(campTile, out var due))
        {
            _apeLumberjackRecruitmentTicks[campTile] = Tick + 30 * TicksPerSecond;
            return;
        }
        if (Tick < due)
            return;
        _apeLumberjackRecruitmentTicks[campTile] = Tick + 30 * TicksPerSecond;
        var recruitId = _apeVillageHomes.Where(pair => pair.Value == villageTile &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is CritterSpecies.Ape && !_plagues.ContainsKey(pair.Key) &&
                !_apeSettlerTargets.ContainsKey(pair.Key) && !_apeCarriedFood.ContainsKey(pair.Key))
            .OrderBy(pair => WrappedManhattanDistance(
                _positions[_critterIndicesById[pair.Key]], GetPosition(campTile)))
            .ThenBy(pair => pair.Key).Select(pair => pair.Key).FirstOrDefault();
        if (recruitId == 0)
            return;
        ChangeCritterSpecies(_critterIndicesById[recruitId], CritterSpecies.ApeLumberjack,
            preserveEnergy: true, preserveApeVillage: true);
        _apeLumberjacks[campTile] = recruitId;
        _apeLumberjackRecruitmentTicks.Remove(campTile);
    }

    private void RemoveApeLumberStaff(int campTile)
    {
        _apeLumberjackRecruitmentTicks.Remove(campTile);
        if (_apeLumberjacks.Remove(campTile, out var id) &&
            _critterIndicesById.TryGetValue(id, out var index) &&
            _species[index] is CritterSpecies.ApeLumberjack)
            ChangeCritterSpecies(index, CritterSpecies.Ape,
                preserveEnergy: true, preserveApeVillage: true);
    }
}
