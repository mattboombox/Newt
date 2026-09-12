namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int DogRecruitmentChancePercent = 25;

    public int GetApeVillageDogCount(GridPosition village) => GetVillageDogCount(GetIndex(village));

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
