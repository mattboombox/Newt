namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int DogRecruitmentChancePercent = 1;

    public int GetApeVillageDogCount(GridPosition village) => GetVillageDogCount(GetIndex(village));

    private int GetVillageDogCount(int village) => _apeVillageHomes.Count(pair =>
        pair.Value == village && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
        _species[index] is CritterSpecies.Dog);

    private int GetVillageDogLimit(int village) => GetApeVillageResidentCountByTile(village) / 10;

    internal void TryRecruitDogFromWolfKill(int killerIndex)
    {
        if (!IsLivingApe(_species[killerIndex]) ||
            !_apeVillageHomes.TryGetValue(_critterIds[killerIndex].Value, out var village) ||
            GetVillageDogCount(village) >= GetVillageDogLimit(village) ||
            NextInt(100) >= DogRecruitmentChancePercent)
            return;
        var position = FindBirthPosition(killerIndex, CritterSpecies.Dog, new HashSet<GridPosition>());
        if (position is not { } spawn || !TryAddCritter(CritterSpecies.Dog, spawn))
            return;
        _apeVillageHomes[_critterIds[_occupants[GetIndex(spawn)]].Value] = village;
    }
}
