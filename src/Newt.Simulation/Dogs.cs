namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int DogRecruitmentChancePercent = 25;

    private bool IsReservedVillageDog(int index)
    {
        if (_species[index] is not CritterSpecies.Dog ||
            !_apeVillageHomes.TryGetValue(_critterIds[index].Value, out var village))
            return false;

        // Stable IDs preserve age order even when removal compacts the critter arrays.
        var olderDogs = 0;
        foreach (var pair in _apeVillageHomes)
            if (pair.Value == village && pair.Key < _critterIds[index].Value &&
                _critterIndicesById.TryGetValue(pair.Key, out var other) &&
                _species[other] is CritterSpecies.Dog && ++olderDogs >= 2)
                return false;
        return true;
    }

    internal GridPosition? TryMoveDog(int index, IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        if (!IsReservedVillageDog(index))
            return TryMoveHunter(index, ApeDefenderPerceptionRadius, reservedPrey, huntWhenFull: true);

        _preyTargets[index] = -1;
        if (TryFleePredators(index, LandPreyFleeRadius))
            return null;
        return TryMove(index, reservedPrey, allowPredation: false);
    }

    public int GetApeVillageDogCount(GridPosition village) => GetVillageDogCount(GetIndex(village));

    public int GetApeVillageDogCapacity(GridPosition village) => GetVillageDogLimit(GetIndex(village));

    private int GetVillageDogCount(int village) => _apeVillageHomes.Count(pair =>
        pair.Value == village && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
        _species[index] is CritterSpecies.Dog);

    private int GetVillageDogLimit(int village) =>
        _barbarianVillageTiles.Contains(village)
            ? Math.Max(5, GetApeVillageResidentCountByTile(village) / 5)
            : Math.Max(2, GetApeVillageResidentCountByTile(village) / 10);

    internal void TryRecruitDogFromWolfKill(int killerIndex)
    {
        if (!IsLivingApe(_species[killerIndex]) ||
            !_apeVillageHomes.TryGetValue(_critterIds[killerIndex].Value, out var village) ||
            GetVillageDogCount(village) >= GetVillageDogLimit(village) ||
            NextInt(100) >= DogRecruitmentChancePercent)
            return;
        var reservedBirthTiles = new HashSet<GridPosition>();
        var position = FindBirthPosition(killerIndex, CritterSpecies.Dog, reservedBirthTiles) ??
            FindVillageDogBirthPosition(village, reservedBirthTiles);
        if (position is not { } spawn || !TryAddCritter(CritterSpecies.Dog, spawn))
            return;
        _apeVillageHomes[_critterIds[_occupants[GetIndex(spawn)]].Value] = village;
    }

    private GridPosition? FindVillageDogBirthPosition(int village, IReadOnlySet<GridPosition> reservedBirthTiles) =>
        _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is not CritterSpecies.Dog)
            .Select(pair => _critterIndicesById[pair.Key])
            .Order()
            .Select(index => FindBirthPosition(index, CritterSpecies.Dog, reservedBirthTiles))
            .FirstOrDefault(position => position is not null);
}
