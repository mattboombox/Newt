namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _warriorCombatKills = [];
    private const int WarriorVeteranKillThreshold = 5;

    private bool CanEarnWarriorVeterancy(int index) =>
        IsApeMilitary(_species[index]) ||
        (IsBarbarianApe(index) && _species[index] is (CritterSpecies.Ape or CritterSpecies.ApeSailor));

    private bool IsVeteranWarrior(int index) => CanEarnWarriorVeterancy(index) &&
        _warriorCombatKills.GetValueOrDefault(_critterIds[index].Value) >= WarriorVeteranKillThreshold;

    private void RecordWarriorCombatKill(int index)
    {
        if (CanEarnWarriorVeterancy(index))
        {
            var id = _critterIds[index].Value;
            _warriorCombatKills[id] = _warriorCombatKills.GetValueOrDefault(id) + 1;
            if (_species[index] is not CritterSpecies.ApeBomber && _warriorCombatKills[id] == WarriorVeteranKillThreshold)
                _energy[index] = CritterNutritions.Get(_species[index]).MaximumEnergy;
        }
    }

    private void PrioritizeVillageVeterans(int village)
    {
        var warriors = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is CritterSpecies.ApeWarrior)
            .Select(pair => pair.Key).Order().ToArray();
        var veterans = warriors.Count(id => IsVeteranWarrior(_critterIndicesById[id]));
        if (veterans == 0) return;
        var capacity = GetApeWarriorLimitForMilitaryDistricts(
            GetApeStructureCount(village, ApeStructureKind.MilitaryDistrict));
        var excess = warriors.Length - Math.Max(capacity, veterans);
        foreach (var id in warriors.Where(id => !IsVeteranWarrior(_critterIndicesById[id])).Take(Math.Max(0, excess)))
        {
            var index = _critterIndicesById[id];
            if (_returningWarSailors.ContainsKey(id))
                _returningWarSailors[id] = CritterSpecies.Ape;
            else
                ChangeCritterSpecies(index, CritterSpecies.Ape, preserveEnergy: true, preserveApeVillage: true);
        }
    }
}
