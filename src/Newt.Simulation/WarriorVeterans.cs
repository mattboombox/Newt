namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _warriorCombatKills = [];
    private const int WarriorVeteranKillThreshold = 5;

    private bool IsVeteranWarrior(int index) => _species[index] is CritterSpecies.ApeWarrior &&
        _warriorCombatKills.GetValueOrDefault(_critterIds[index].Value) >= WarriorVeteranKillThreshold;

    private void RecordWarriorCombatKill(int index)
    {
        if (_species[index] is CritterSpecies.ApeWarrior)
        {
            var id = _critterIds[index].Value;
            _warriorCombatKills[id] = _warriorCombatKills.GetValueOrDefault(id) + 1;
        }
    }
}
