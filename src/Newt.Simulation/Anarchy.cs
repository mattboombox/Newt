namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int AnarchyCampSize = 11;
    private readonly HashSet<int> _anarchyCampTiles = [];

    /// <summary>Splits the selected village's unemployed apes into full barbarian camps.</summary>
    public int TriggerAnarchy(GridPosition position)
    {
        if (!LifeEnabled || !Contains(position) || GetApeStructureVillage(position) is not { } villagePosition)
            return 0;
        var village = GetIndex(villagePosition);
        if (!IsRoadVillage(village)) return 0;
        var recruits = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is CritterSpecies.Ape && CanRecruitApe(index))
            .Select(pair => pair.Key).ToHashSet();
        var needed = recruits.Count / AnarchyCampSize;
        if (needed == 0) return 0;

        bool Residential(int tile) => _apeStructures.GetValueOrDefault(tile) is ApeStructureKind.ResidentialDistrict &&
            _apeAuxiliaryVillages.GetValueOrDefault(tile, -1) == village;
        bool Suitable(int tile) => tile != village &&
            (Residential(tile) || !HasBlockingApeStructure(tile)) &&
            CanBuildApeStructureOn(tile, ApeStructureKind.Village) && CanLiveOn(CritterSpecies.Ape, tile) &&
            _surfaceCovers[tile] is SurfaceCover.None && !_wolfDenCharges.ContainsKey(tile) &&
            !_megaSpiderWebFood.ContainsKey(tile) && !_teleporters.Contains(tile) &&
            (_occupants[tile] < 0 || recruits.Contains(_critterIds[_occupants[tile]].Value));
        // Residential districts are reusable sites; nearby free ground fills any remaining need.
        var sites = Enumerable.Range(0, _terrain.Length)
            .Where(tile => WrappedManhattanDistance(villagePosition, GetPosition(tile)) <= ApeVillageClaimRadius && Suitable(tile))
            .OrderByDescending(Residential)
            .ThenBy(tile => WrappedManhattanDistance(villagePosition, GetPosition(tile)))
            .ThenBy(tile => tile).Take(needed).ToArray();
        var camps = new List<int>(sites.Length);
        foreach (var site in sites)
        {
            var group = recruits.OrderBy(id => WrappedManhattanDistance(GetPosition(site),
                    _positions[_critterIndicesById[id]]))
                .ThenBy(id => id).Take(AnarchyCampSize).ToArray();
            if (Residential(site)) RemoveApeStructureAt(GetPosition(site));
            SetApeStructure(site, ApeStructureKind.Village);
            _barbarianVillageTiles.Add(site);
            _anarchyCampTiles.Add(site);
            _apeVillageIds[site] = _nextApeVillageId++;
            _apeVillageFood[site] = 0;
            _apeVillagesWithMultipleResidents.Add(site);
            for (var i = 0; i < group.Length; i++)
            {
                var id = group[i];
                var index = _critterIndicesById[id];
                DetachApeFromVillage(id, countedAsResident: true);
                _apeVillageHomes[id] = site;
                var role = i == 0 ? CritterSpecies.ApeChieftain :
                    CanLiveOn(CritterSpecies.ApeSailor, GetIndex(_positions[index])) && IsBarbarianCampCoastal(site)
                        ? CritterSpecies.ApeSailor : CritterSpecies.ApeWarrior;
                ChangeCritterSpecies(index, role, preserveEnergy: true, preserveApeVillage: true);
                recruits.Remove(id);
            }
            camps.Add(site);
        }
        if (camps.Count == 0) return 0;
        var dogs = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) && _species[index] is CritterSpecies.Dog)
            .Select(pair => pair.Key).Order().ToArray();
        for (var i = 0; i < dogs.Length; i++)
        {
            var id = dogs[i];
            DetachApeFromVillage(id, countedAsResident: false);
            _apeVillageHomes[id] = camps[i % camps.Count];
        }
        if (GetApeVillageSustainingPopulation(village) == 0)
        {
            TryStopApeWar(villagePosition);
            RemoveAbandonedApeVillage(village);
        }
        return camps.Count;
    }
}
