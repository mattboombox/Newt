namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ChieftainEscortSize = 4;
    private const int ChieftainEscortDistance = 2;

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
        // Alternate roles so available dogs can join an established warrior escort.
        var escort = new List<CritterId>(ChieftainEscortSize);
        while (escort.Count < ChieftainEscortSize && (warriors.Count > 0 || dogs.Count > 0))
        {
            if (warriors.TryDequeue(out var warrior))
                escort.Add(new CritterId(warrior));
            if (escort.Count < ChieftainEscortSize && dogs.TryDequeue(out var dog))
                escort.Add(new CritterId(dog));
        }
        return escort.ToArray();
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
