namespace Newt.Simulation;

public sealed record VillagePopulationSnapshot(int Id, GridPosition Position, bool Barbarian,
    ApeCulture? Culture, int Capacity, IReadOnlyDictionary<CritterSpecies, int> Counts, CritterId? Lich = null)
{
    public bool IsNecropolis => Lich is not null;
    public long FoundedTick { get; init; }
    public int ApeCount => Counts.Where(pair => pair.Key != CritterSpecies.Dog).Sum(pair => pair.Value);
}

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, long> _apeVillageFoundedTicks = [];
    public int ApeColonistCount => _apeSettlerTargets.Keys.Count(id =>
        _critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.Ape);

    public GridPosition? GetNecropolisPopulationHome(CritterId id)
    {
        if (!_critterIndicesById.TryGetValue(id.Value, out var index) || !IsLichUndead(_species[index])) return null;
        var lich = _species[index] is CritterSpecies.Lich ? id.Value : _lichArmy.GetValueOrDefault(id.Value, -1);
        return _lichCampaigns.TryGetValue(lich, out var home) && home.Tile >= 0 ? GetPosition(home.Tile) : null;
    }

    // Necropolis IDs use a separate range so chart histories cannot collide with villages.
    public int? GetPopulationSettlementId(GridPosition position) =>
        Contains(position) && _necropolisOwners.TryGetValue(GetIndex(position), out var lich) ? -lich : GetApeVillageId(position);

    /// <summary>Groups village residents and each necropolis's owned undead, wherever they are.</summary>
    public IReadOnlyList<VillagePopulationSnapshot> GetVillagePopulations()
    {
        var counts = new Dictionary<int, Dictionary<CritterSpecies, int>>();
        foreach (var (tile, structure) in _apeStructures)
            if (structure == ApeStructureKind.Village) counts[tile] = [];
        foreach (var (id, tile) in _apeVillageHomes)
            if (counts.TryGetValue(tile, out var village) && _critterIndicesById.TryGetValue(id, out var index) &&
                !IsLichUndead(_species[index]))
                village[_species[index]] = village.GetValueOrDefault(_species[index]) + 1;
        var undead = _necropolisOwners.Values.ToDictionary(lich => lich,
            _ => new Dictionary<CritterSpecies, int> { [CritterSpecies.Lich] = 1 });
        foreach (var (id, lich) in _lichArmy)
            if (undead.TryGetValue(lich, out var army) && _critterIndicesById.TryGetValue(id, out var index) &&
                _species[index] is CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior)
                army[_species[index]] = army.GetValueOrDefault(_species[index]) + 1;
        return counts.Select(pair => new VillagePopulationSnapshot(_apeVillageIds[pair.Key],
            GetPosition(pair.Key), _barbarianVillageTiles.Contains(pair.Key),
            GetApeVillageCulture(GetPosition(pair.Key)), GetApeVillagePopulationCapacityByTile(pair.Key), pair.Value)
                { FoundedTick = _apeVillageFoundedTicks.GetValueOrDefault(pair.Key) })
            .Concat(_necropolisOwners.Select(pair => new VillagePopulationSnapshot(-pair.Value,
                GetPosition(pair.Key), false, null, 0, undead[pair.Value], new CritterId(pair.Value))
                    { FoundedTick = _lichCampaigns[pair.Value].FoundedTick }))
            .OrderBy(village => village.IsNecropolis).ThenBy(village => Math.Abs(village.Id)).ToArray();
    }
}
