namespace Newt.Simulation;

public sealed record VillagePopulationSnapshot(int Id, GridPosition Position, bool Barbarian,
    ApeCulture? Culture, int Capacity, IReadOnlyDictionary<CritterSpecies, int> Counts)
{
    public int ApeCount => Counts.Where(pair => pair.Key != CritterSpecies.Dog).Sum(pair => pair.Value);
}

public sealed partial class SimulationWorld
{
    /// <summary>Groups living residents by home village, regardless of their current location.</summary>
    public IReadOnlyList<VillagePopulationSnapshot> GetVillagePopulations()
    {
        var counts = new Dictionary<int, Dictionary<CritterSpecies, int>>();
        foreach (var (tile, structure) in _apeStructures)
            if (structure == ApeStructureKind.Village) counts[tile] = [];
        foreach (var (id, tile) in _apeVillageHomes)
            if (counts.TryGetValue(tile, out var village) && _critterIndicesById.TryGetValue(id, out var index))
                village[_species[index]] = village.GetValueOrDefault(_species[index]) + 1;
        return counts.Select(pair => new VillagePopulationSnapshot(_apeVillageIds[pair.Key],
            GetPosition(pair.Key), _barbarianVillageTiles.Contains(pair.Key),
            GetApeVillageCulture(GetPosition(pair.Key)), GetApeVillagePopulationCapacityByTile(pair.Key), pair.Value))
            .OrderBy(village => village.Id).ToArray();
    }
}
