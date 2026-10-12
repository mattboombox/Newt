namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ApeCulturePopulationThreshold = 200;
    internal const int ApeCultureCheckIntervalTicks = 60 * TicksPerSecond;
    private readonly Dictionary<int, ApeCulture> _apeVillageCultures = [];

    private void RecruitApeCultureBombers(ApeWar war, int village)
    {
        if (!_apeVillageCultures.TryGetValue(village, out var culture) || culture is not ApeCulture.Luddite) return;
        var warriors = _apeVillageHomes.Where(pair => pair.Value == village &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) && CanRecruitApe(index) && _species[index] is CritterSpecies.ApeWarrior &&
            (!war.CivilWar || war.DefendingWarriors.Contains(pair.Key)) &&
            (village != war.Attacker || war.AttackingParticipants is null || war.AttackingParticipants.Contains(pair.Key)))
            .Select(pair => pair.Key).Order().ToArray();
        foreach (var id in warriors.Take((warriors.Length + 3) / 4))
        {
            war.Bombers.Add(id);
            ChangeCritterSpecies(_critterIndicesById[id], CritterSpecies.ApeBomber, true, true);
        }
    }

    private void RemoveSpentBomber(CritterId id)
    {
        if (_critterIndicesById.TryGetValue(id.Value, out var index) && _species[index] is CritterSpecies.ApeBomber &&
            _energy[index] == 0 && !TryReanimateApe(index))
            RemoveCritter(id);
    }

    public ApeCulture? GetApeVillageCulture(GridPosition position) =>
        Contains(position) && GetApeStructureVillage(position) is { } village &&
        _apeVillageCultures.TryGetValue(GetIndex(village), out var culture) ? culture : null;

    private ApeCultureDefinition? GetApeCultureDefinition(int village) =>
        _apeVillageCultures.TryGetValue(village, out var culture) ? ApeCultures.Get(culture) : null;

    private bool ApeCultureBlocksTechnology(int village) => GetApeCultureDefinition(village)?.BlocksTechnology is true;

    public bool TryAssignApeCulture(GridPosition position, ApeCulture culture, bool ignorePopulationRequirement = false)
    {
        if (!LifeEnabled || !Contains(position) || GetApeStructureVillage(position) is not { } center)
            return false;
        var village = GetIndex(center);
        if (!IsRoadVillage(village) || _apeWars.ContainsKey(village) || _apeVillageCultures.ContainsKey(village) ||
            (!ignorePopulationRequirement && GetApeVillageResidentCountByTile(village) < ApeCulturePopulationThreshold))
            return false;
        var definition = ApeCultures.Get(culture);
        var libraries = _apeAuxiliaryVillages.Where(pair => pair.Value == village &&
            _apeStructures.GetValueOrDefault(pair.Key) is ApeStructureKind.Library).Select(pair => pair.Key).ToArray();
        var site = definition.BlocksTechnology && libraries.Length > 0 ? libraries[0] :
            FindApeConstructionSite(village, definition.Building);
        if (site < 0 || !IsValidApeAuxiliaryTile(site, definition.Building))
            return false;
        // Cultural roles recruit existing residents, so only the building needs an empty tile.
        _apeVillageCultures[village] = culture;
        if (definition.BlocksTechnology)
        {
            _apeVillageTechnologies.Remove(village);
            foreach (var library in libraries)
                RemoveApeStructureAt(GetPosition(library));
            foreach (var id in _apeVillageHomes.Where(pair => pair.Value == village).Select(pair => pair.Key).ToArray())
            {
                _apeColonistTechnologies.Remove(id);
                if (_critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.ApeScholar)
                    ChangeCritterSpecies(index, CritterSpecies.Ape, true, true);
            }
        }
        BuildApeStructureAt(village, site, definition.Building);
        RecruitApeCultureLeader(village);
        RecruitApeCultureUnits(village);
        _apeVillageMergeCheckNeeded = true;
        return true;
    }

    internal bool TryDevelopApeCulture(int village)
    {
        if (!LifeEnabled || !NaturalEventsEnabled || Tick == 0 || Tick % ApeCultureCheckIntervalTicks != 0 ||
            !IsRoadVillage(village) || _apeWars.ContainsKey(village) || _apeVillageCultures.ContainsKey(village) ||
            GetApeVillageResidentCountByTile(village) < ApeCulturePopulationThreshold || NextInt(100) >= 1)
            return false;
        return TryAssignApeCulture(GetPosition(village), ApeCultures.All[NextInt(ApeCultures.All.Count)].Culture);
    }

    private IEnumerable<int> FindCultureRecruitmentTiles(int building)
    {
        var center = GetPosition(building);
        return Enumerable.Range(0, _terrain.Length).Where(tile => _occupants[tile] < 0 &&
            WrappedManhattanDistance(center, GetPosition(tile)) <= 8 && CanLiveOn(CritterSpecies.Ape, tile) &&
            !_teleporters.Contains(tile) && !_megaSpiderWebFood.ContainsKey(tile) && !_vampireLairs.Contains(tile))
            .OrderBy(tile => WrappedManhattanDistance(center, GetPosition(tile))).ThenBy(tile => tile);
    }

    private void RecruitApeCultureUnits(int village)
    {
        if (GetApeCultureDefinition(village) is not { } definition || _apeWars.ContainsKey(village))
            return;
        var building = _apeAuxiliaryVillages.FirstOrDefault(pair => pair.Value == village &&
            _apeStructures.GetValueOrDefault(pair.Key) == definition.Building, new(-1, -1)).Key;
        if (building < 0) return;
        foreach (var unit in definition.Units)
        {
            var residents = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) && _species[index] == unit.Species)
                .Select(pair => pair.Key).Order().ToArray();
            foreach (var id in residents.Skip(unit.Count))
                ChangeCritterSpecies(_critterIndicesById[id], CritterSpecies.Ape, true, true);
            var count = Math.Min(residents.Length, unit.Count);
            foreach (var id in FindHealthyUnemployedCultureRecruits(village)
                .Take(Math.Max(0, unit.Count - count)).ToArray())
                ChangeCritterSpecies(_critterIndicesById[id], unit.Species, true, true);
        }
    }

    private bool RecruitApeCultureLeader(int village)
    {
        if (GetApeCultureDefinition(village) is not { ReplacesChieftain: true } definition)
            return false;
        if (_apeVillageHomes.Any(pair => pair.Value == village && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            IsCultureLeader(definition, _species[index]))) return true;
        if (definition.Culture == ApeCulture.Monarchy)
        {
            var recruit = FindHealthyUnemployedCultureRecruits(village).FirstOrDefault(-1);
            if (recruit < 0) return false;
            ChangeCritterSpecies(_critterIndicesById[recruit],
                NextInt(2) == 1 ? CritterSpecies.ApeQueen : CritterSpecies.ApeKing, true, true);
            return true;
        }
        var candidate = _apeVillageHomes.Where(pair => pair.Value == village &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) && CanRecruitApe(index) && _species[index] is CritterSpecies.ApeChieftain or CritterSpecies.Ape)
            .OrderBy(pair => _species[_critterIndicesById[pair.Key]] is CritterSpecies.ApeChieftain ? 0 : 1)
            .ThenBy(pair => pair.Key).FirstOrDefault(new KeyValuePair<int, int>(-1, -1));
        var leader = definition.Culture == ApeCulture.Monarchy && NextInt(2) == 1
            ? CritterSpecies.ApeQueen : definition.LeaderSpecies;
        if (candidate.Key >= 0)
            ChangeCritterSpecies(_critterIndicesById[candidate.Key], leader, true, true);
        else
        {
            var building = _apeAuxiliaryVillages.FirstOrDefault(pair => pair.Value == village &&
                _apeStructures.GetValueOrDefault(pair.Key) == definition.Building, new(-1, -1)).Key;
            if (building < 0) return false;
            var tile = FindCultureRecruitmentTiles(building).FirstOrDefault(-1);
            if (tile < 0) return false;
            var id = AddCritter(leader, GetPosition(tile));
            _apeVillageHomes[id.Value] = village;
        }
        return true;
    }

    private IEnumerable<int> FindHealthyUnemployedCultureRecruits(int village) =>
        _apeVillageHomes.Where(pair => pair.Value == village &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            IsUnemployedResidentApe(index) && CanRecruitApe(index))
            .Select(pair => pair.Key).Order();

    private void RemoveApeCulture(int village)
    {
        if (!_apeVillageCultures.Remove(village, out var culture)) return;
        var definition = ApeCultures.Get(culture);
        // Demobilization must not restore cultural roles after their culture has fallen.
        if (_apeWars.TryGetValue(village, out var war))
            foreach (var (id, role) in war.OriginalRoles.ToArray())
            {
                if (!_apeVillageHomes.TryGetValue(id, out var home) || home != village) continue;
                if (definition.ReplacesChieftain && IsCultureLeader(definition, role))
                    war.OriginalRoles[id] = CritterSpecies.ApeChieftain;
                else if (definition.Units.Any(unit => unit.Species == role))
                    war.OriginalRoles[id] = CritterSpecies.Ape;
            }
        foreach (var tile in _apeAuxiliaryVillages.Where(pair => pair.Value == village &&
            _apeStructures.GetValueOrDefault(pair.Key) == definition.Building).Select(pair => pair.Key).ToArray())
            RemoveApeStructureAt(GetPosition(tile));
        foreach (var id in _apeVillageHomes.Where(pair => pair.Value == village).Select(pair => pair.Key).ToArray())
        {
            if (!_critterIndicesById.TryGetValue(id, out var index)) continue;
            if (_species[index] is CritterSpecies.ApeBomber)
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, true, true);
            else if (definition.ReplacesChieftain && IsCultureLeader(definition, _species[index]))
                ChangeCritterSpecies(index, CritterSpecies.ApeChieftain, true, true);
            else if (definition.Units.Any(unit => unit.Species == _species[index]))
                ChangeCritterSpecies(index, CritterSpecies.Ape, true, true);
        }
        _apeVillageMergeCheckNeeded = true;
    }

    private static bool IsCultureLeader(ApeCultureDefinition definition, CritterSpecies species) =>
        species == definition.LeaderSpecies ||
        (definition.Culture == ApeCulture.Monarchy && species is CritterSpecies.ApeQueen);

    private static bool IsApeMilitary(CritterSpecies species) =>
        species is CritterSpecies.ApeWarrior or CritterSpecies.ApeBomber or CritterSpecies.ApeRoyalGuard;

    private bool IsApeCulturalCombatUnit(int village, CritterSpecies species) =>
        GetApeCultureDefinition(village)?.Units.Any(unit => unit.Species == species && unit.Combatant) is true;

    public bool UsesLudditeWarriorAppearance(CritterId id) =>
        UsesApeCultureWarriorAppearance(id, ApeCulture.Luddite);

    public bool UsesMonarchyWarriorAppearance(CritterId id) =>
        UsesApeCultureWarriorAppearance(id, ApeCulture.Monarchy);

    private bool UsesApeCultureWarriorAppearance(CritterId id, ApeCulture culture) =>
        _critterIndicesById.TryGetValue(id.Value, out var index) && CanRecruitApe(index) && _species[index] is CritterSpecies.ApeWarrior &&
        _apeVillageHomes.TryGetValue(id.Value, out var village) &&
        _apeVillageCultures.TryGetValue(village, out var villageCulture) && villageCulture == culture &&
        TryGetApeWar(index, out var war) && (!war.CivilWar || war.DefendingWarriors.Contains(id.Value));
}
