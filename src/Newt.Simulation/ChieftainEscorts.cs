namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ChieftainEscortSize = 4;
    private const int ChieftainEscortDistance = 2;
    private readonly Dictionary<int, (int Target, long Until)> _chieftainAttackTargets = [];

    private void RememberChieftainTarget(int chief, int target)
    {
        if (_species[chief] is CritterSpecies.ApeChieftain)
            _chieftainAttackTargets[_critterIds[chief].Value] =
                (_critterIds[target].Value, Tick + 2 * GetMovementIntervalTicks(CritterSpecies.ApeChieftain));
    }

    private bool IsChieftainEscortTarget(int index, int target) =>
        _species[index] is (CritterSpecies.ApeWarrior or CritterSpecies.Dog) &&
        _apeVillageHomes.TryGetValue(_critterIds[index].Value, out var village) &&
        TryGetVillageChieftain(village, out var chief) &&
        _chieftainAttackTargets.TryGetValue(_critterIds[chief].Value, out var attack) &&
        Tick < attack.Until && attack.Target == _critterIds[target].Value &&
        GetChieftainEscort(GetPosition(village)).Contains(_critterIds[index]) &&
        CanEatInCurrentContext(chief, target);

    internal CritterId[] GetChieftainEscort(GridPosition village)
    {
        var tile = GetIndex(village);
        if (!TryGetVillageChieftain(tile, out _))
            return [];
        var warriors = new Queue<int>();
        var dogs = new Queue<int>();
        foreach (var pair in _apeVillageHomes.Where(pair => pair.Value == tile).OrderBy(pair => pair.Key))
        {
            if (!_critterIndicesById.TryGetValue(pair.Key, out var index))
                continue;
            if (_species[index] is CritterSpecies.ApeWarrior)
                warriors.Enqueue(pair.Key);
            else if (_species[index] is CritterSpecies.Dog && !IsReservedVillageDog(index))
                dogs.Enqueue(pair.Key);
        }
        return warriors.Take(ChieftainEscortSize).Concat(dogs.Take(2))
            .Select(id => new CritterId(id)).ToArray();
    }

    private bool TryGetVillageChieftain(int village, out int chiefIndex)
    {
        chiefIndex = -1;
        var chiefId = int.MaxValue;
        foreach (var pair in _apeVillageHomes)
            if (pair.Value == village && pair.Key < chiefId &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is CritterSpecies.ApeChieftain)
            {
                chiefId = pair.Key;
                chiefIndex = index;
            }
        return chiefIndex >= 0;
    }

    internal bool TryFollowVillageChieftain(int index, IReadOnlySet<GridPosition>? reservedPrey,
        out GridPosition? prey)
    {
        prey = null;
        if (_species[index] is not (CritterSpecies.ApeWarrior or CritterSpecies.Dog) ||
            !_apeVillageHomes.TryGetValue(_critterIds[index].Value, out var village) ||
            !TryGetVillageChieftain(village, out var chief) ||
            !GetChieftainEscort(GetPosition(village)).Contains(_critterIds[index]))
            return false;

        _preyTargets[index] = -1;
        if (_chieftainAttackTargets.TryGetValue(_critterIds[chief].Value, out var attack) &&
            Tick < attack.Until && _critterIndicesById.TryGetValue(attack.Target, out var target) &&
            CanPursuePrey(index, target))
        {
            var position = _positions[target];
            _preyTargets[index] = GetIndex(position);
            if (MovementDirections.Any(direction => new GridPosition(
                Mod(_positions[index].X + direction.X, Width), _positions[index].Y + direction.Y) == position))
            {
                if (reservedPrey?.Contains(position) is not true)
                    prey = position;
            }
            else
                TryMoveTowardApeStructure(index, GetIndex(position), reservedPrey);
            return true;
        }
        // Fight adjacent enemies, but do not abandon the chief to chase distant prey.
        prey = FindHunterPrey(index, _species[index], 1, reservedPrey);
        if (prey is not null)
            return true;
        if (IsBarbarianApe(index) && _species[index] is CritterSpecies.ApeWarrior && TryFeedApeFoliage(index))
            return true;
        if (WrappedManhattanDistance(_positions[index], _positions[chief]) > ChieftainEscortDistance)
            TryMoveTowardApeStructure(index, GetIndex(_positions[chief]), reservedPrey);
        return true;
    }
}
