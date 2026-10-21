namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    /// <summary>Temporary peaceful consolidation rule; disable when diplomacy and war are introduced.</summary>
    public bool ApeVillageMergingEnabled { get; set; } = true;

    private bool _apeVillageMergeCheckNeeded = true;

    internal void MergeAdjacentApeVillages()
    {
        if (!ApeVillageMergingEnabled || !_apeVillageMergeCheckNeeded)
            return;
        _apeVillageMergeCheckNeeded = false;

        var parents = _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village &&
                !_barbarianVillageTiles.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Key);
        if (parents.Count < 2)
            return;

        int Root(int tile)
        {
            while (parents[tile] != tile)
            {
                parents[tile] = parents[parents[tile]];
                tile = parents[tile];
            }
            return tile;
        }

        var owners = new Dictionary<int, int>();
        foreach (var pair in _apeStructures)
        {
            if (pair.Value is ApeStructureKind.Ruin)
                continue;
            var owner = pair.Value is ApeStructureKind.Village
                ? pair.Key : _apeAuxiliaryVillages.GetValueOrDefault(pair.Key, -1);
            if (parents.ContainsKey(owner))
                owners[pair.Key] = owner;
        }

        foreach (var pair in owners)
        {
            var position = GetPosition(pair.Key);
            foreach (var direction in MovementDirections)
            {
                var y = position.Y + direction.Y;
                if (y < 0 || y >= Height || !owners.TryGetValue(
                        GetIndex(new GridPosition(Mod(position.X + direction.X, Width), y)), out var neighbor))
                    continue;
                var first = Root(pair.Value);
                var second = Root(neighbor);
                if (first == second)
                    continue;
                if (_apeWars.ContainsKey(first) || _apeWars.ContainsKey(second))
                    continue;
                if (_apeVillageCultures.ContainsKey(first) && _apeVillageCultures.ContainsKey(second))
                {
                    if (TryStartApeWar(GetPosition(first), GetPosition(second), bloodWar: true))
                        _apeWars[first].MergeOnVictory = true;
                    continue;
                }
                // A lone culture survives peaceful consolidation; two cultures fight for dominance.
                if (!_apeVillageCultures.ContainsKey(first) && _apeVillageCultures.ContainsKey(second))
                    (first, second) = (second, first);
                // Keep the oldest village's identity, independent of tile or dictionary order.
                if (!_apeVillageCultures.ContainsKey(first) && _apeVillageIds[first] > _apeVillageIds[second])
                    (first, second) = (second, first);
                parents[second] = first;
            }
        }

        foreach (var village in parents.Keys.ToArray().OrderBy(tile => _apeVillageIds[tile]))
        {
            var survivor = Root(village);
            if (village != survivor && !_apeWars.ContainsKey(village) && !_apeWars.ContainsKey(survivor))
                MergeApeVillageInto(village, survivor);
        }
        // Conversion cannot introduce another adjacency.
        _apeVillageMergeCheckNeeded = false;
    }

    private void MergeApeVillageInto(int source, int destination)
    {
        RemoveRoadsForVillage(source);
        var food = _apeVillageFood.GetValueOrDefault(source) + _apeVillageFood.GetValueOrDefault(destination);
        var wood = _apeVillageWood.GetValueOrDefault(source) + _apeVillageWood.GetValueOrDefault(destination);
        if (_apeVillageCultures.ContainsKey(source))
        {
            if (_apeVillageCultures.ContainsKey(destination)) RemoveApeCulture(source);
            else if (_apeVillageCultures.Remove(source, out var culture)) _apeVillageCultures[destination] = culture;
        }
        if (_apeVillageTechnologies.Remove(source, out var technologies))
        {
            if (!_apeVillageTechnologies.TryGetValue(destination, out var known))
                _apeVillageTechnologies[destination] = known = [];
            known.UnionWith(technologies);
        }
        if (ApeCultureBlocksTechnology(destination)) _apeVillageTechnologies.Remove(destination);

        foreach (var tile in _apeAuxiliaryVillages.Where(pair => pair.Value == source).Select(pair => pair.Key).ToArray())
            _apeAuxiliaryVillages[tile] = destination;
        // Convert the old center to a three-resident district wherever its terrain permits.
        ConvertMergedBuildingToHousing(source, destination);
        var libraries = _apeAuxiliaryVillages.Where(pair => pair.Value == destination &&
                _apeStructures.GetValueOrDefault(pair.Key) is ApeStructureKind.Library)
            .Select(pair => pair.Key).Order().ToArray();
        foreach (var library in libraries.Skip(1))
            ConvertMergedBuildingToHousing(library, destination);
        if (ApeCultureBlocksTechnology(destination))
            foreach (var library in libraries.Where(tile => _apeStructures.GetValueOrDefault(tile) is ApeStructureKind.Library))
                RemoveApeStructureAt(GetPosition(library));

        foreach (var id in _apeVillageHomes.Where(pair => pair.Value == source).Select(pair => pair.Key).ToArray())
            _apeVillageHomes[id] = destination;
        foreach (var id in _apeVillageTargets.Where(pair => pair.Value == source).Select(pair => pair.Key).ToArray())
            _apeVillageTargets[id] = destination;
        foreach (var pair in _apeSettlerTargets.ToArray())
        {
            if (pair.Value.TargetVillageTile == source)
            {
                // The destination is now an existing district, not a site to found again.
                _apeSettlerTargets.Remove(pair.Key);
                _apeSettlerPaths.Remove(pair.Key);
                _apeVillageTargets.Remove(pair.Key);
                _apeVillageHomes[pair.Key] = destination;
            }
            else if (pair.Value.OriginVillageTile == source)
                _apeSettlerTargets[pair.Key] = (destination, pair.Value.TargetVillageTile);
        }

        var residents = _apeVillageHomes.Where(pair => pair.Value == destination)
            .Select(pair => pair.Key)
            .OrderBy(id => GetApeCultureDefinition(destination) is { ReplacesChieftain: true } culture &&
                _critterIndicesById.TryGetValue(id, out var index) && IsCultureLeader(culture, _species[index]) ? 0 : 1)
            .ThenBy(id => id).ToArray();
        var chiefs = 0;
        var scholars = 0;
        foreach (var id in residents)
        {
            _apeSailorReturnPaths.Remove(id);
            _apeWorkerPaths.Remove(id);
            _apeReproductionStalls.Remove(id);
            _apeVillageSeparationSinceTicks.Remove(id);
            _apeFoodReturnProgress.Remove(id);
            if (!_critterIndicesById.TryGetValue(id, out var index))
                continue;
            if (_species[index] is CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen && ++chiefs > 1)
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, preserveEnergy: true, preserveApeVillage: true);
            else if (_species[index] is CritterSpecies.ApeScholar && (ApeCultureBlocksTechnology(destination) || ++scholars > 2))
                ChangeCritterSpecies(index, CritterSpecies.Ape, preserveEnergy: true, preserveApeVillage: true);
            if (ApeCultureBlocksTechnology(destination)) _apeColonistTechnologies.Remove(id);
        }

        _apeVillageIds.Remove(source);
        _apeVillageFood.Remove(source);
        _apeVillageWood.Remove(source);
        _loneApeSailorSinceTicks.Remove(source);
        _loneApeSailorSinceTicks.Remove(destination);
        _apeVillageFood[destination] = Math.Min(food, GetApeVillageFoodCapacityByTile(destination));
        _apeVillageWood[destination] = Math.Min(wood, GetApeVillageWoodCapacityByTile(destination));
        RecruitApeCultureUnits(destination);
    }

    private void ConvertMergedBuildingToHousing(int tile, int village)
    {
        if (IsValidApeAuxiliaryTile(tile, ApeStructureKind.ResidentialDistrict))
        {
            SetApeStructure(tile, ApeStructureKind.ResidentialDistrict);
            _apeAuxiliaryVillages[tile] = village;
        }
        else
        {
            // Preserve a safe fallback for terrain that no longer supports housing.
            // Normal center removal would also discard residents and stores.
            _apeAuxiliaryVillages.Remove(tile);
            AddApeRuin(tile);
        }
        _apeStructureNextActionTicks.Remove(tile);
        _apeFoodDistrictInactiveSinceTicks.Remove(tile);
    }
}
