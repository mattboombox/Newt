namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ApeLibraryPopulationThreshold = 200;
    internal const int ApeResearchIntervalTicks = 30 * TicksPerSecond;
    internal const int ApeResearchChancePerScholar = 5;
    private readonly Dictionary<int, HashSet<ApeTechnology>> _apeVillageTechnologies = [];
    private readonly Dictionary<int, HashSet<ApeTechnology>> _apeColonistTechnologies = [];

    private bool ApeKnowsTechnology(int apeIndex, ApeTechnology technology) => apeIndex >= 0 &&
        ((_apeColonistTechnologies.TryGetValue(_critterIds[apeIndex].Value, out var known) && known.Contains(technology)) ||
            (_apeVillageHomes.TryGetValue(_critterIds[apeIndex].Value, out var village) && HasApeTechnology(village, technology)));

    private void RememberApeColonistTechnologies(int apeId, int villageTile) =>
        _apeColonistTechnologies[apeId] = _apeVillageTechnologies.TryGetValue(villageTile, out var known)
            ? new HashSet<ApeTechnology>(known) : [];

    private void InheritApeColonistTechnologies(int apeId, int villageTile)
    {
        if (_apeColonistTechnologies.Remove(apeId, out var known))
            _apeVillageTechnologies[villageTile] = known;
    }

    public IReadOnlyList<ApeTechnology> GetApeVillageTechnologies(GridPosition position) =>
        _apeVillageTechnologies.TryGetValue(GetIndex(position), out var known)
            ? known.OrderBy(technology => technology).ToArray() : Array.Empty<ApeTechnology>();

    internal bool HasApeTechnology(int villageTile, ApeTechnology technology) =>
        _apeVillageTechnologies.TryGetValue(villageTile, out var known) && known.Contains(technology);

    internal bool TryResearchApeTechnology(int villageTile, ApeTechnology technology)
    {
        var definition = ApeTechnologies.All.SingleOrDefault(item => item.Technology == technology);
        if (!_apeStructures.TryGetValue(villageTile, out var structure) || structure is not ApeStructureKind.Village ||
            definition is null || !definition.Prerequisites.All(required => HasApeTechnology(villageTile, required)))
            return false;
        if (!_apeVillageTechnologies.TryGetValue(villageTile, out var known))
            _apeVillageTechnologies[villageTile] = known = [];
        return known.Add(technology);
    }

    private bool IsApeBuildingTechnologyUnlocked(int villageTile, ApeStructureKind kind) => kind switch
    {
        ApeStructureKind.NavalDistrict => HasApeTechnology(villageTile, ApeTechnology.Sailing),
        ApeStructureKind.Aquaculture => HasApeTechnology(villageTile, ApeTechnology.Aquaculture),
        _ => true,
    };

    public int GetApeVillageScholarCount(GridPosition position) => GetApeScholarCount(GetIndex(position));

    private int GetApeScholarCount(int villageTile) => _apeVillageHomes.Count(pair =>
        pair.Value == villageTile && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
        _species[index] is CritterSpecies.ApeScholar);

    private void AdvanceApeLibrary(int villageTile, int libraryTile)
    {
        if (!_apeStructureNextActionTicks.TryGetValue(libraryTile, out var nextTick))
            nextTick = Tick + ApeResearchIntervalTicks;
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[libraryTile] = nextTick;
            return;
        }
        _apeStructureNextActionTicks[libraryTile] = Tick + ApeResearchIntervalTicks;
        TryRecruitApeScholar(villageTile, libraryTile);
        var scholars = Math.Min(2, GetApeScholarCount(villageTile));
        if (scholars == 0)
            return;
        var eligible = ApeTechnologies.All.Where(definition =>
            !HasApeTechnology(villageTile, definition.Technology) &&
            definition.Prerequisites.All(required => HasApeTechnology(villageTile, required))).ToArray();
        if (eligible.Length > 0 && NextInt(100) < scholars * ApeResearchChancePerScholar)
            TryResearchApeTechnology(villageTile, eligible[NextInt(eligible.Length)].Technology);
    }

    private void TryRecruitApeScholar(int villageTile, int libraryTile)
    {
        if (GetApeScholarCount(villageTile) >= 2 || _occupants[libraryTile] >= 0)
            return;
        var civilians = _apeVillageHomes.Where(pair => pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            _species[index] is CritterSpecies.Ape && !_apeCarriedFood.ContainsKey(pair.Key) &&
            !_plagues.ContainsKey(pair.Key)).Select(pair => pair.Key).ToArray();
        if (civilians.Length <= 1)
            return;
        var library = GetPosition(libraryTile);
        var recruitId = civilians.OrderBy(id => WrappedManhattanDistance(
            _positions[_critterIndicesById[id]], library)).ThenBy(id => id).First();
        var recruitIndex = _critterIndicesById[recruitId];
        ChangeCritterSpecies(recruitIndex, CritterSpecies.ApeScholar,
            preserveEnergy: true, preserveApeVillage: true);
        MoveCritter(recruitIndex, libraryTile, library);
    }

    private GridPosition? TryMoveApeScholar(int index, IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (_apeVillageHomes.TryGetValue(_critterIds[index].Value, out var villageTile))
        {
            var library = _apeAuxiliaryVillages.FirstOrDefault(pair => pair.Value == villageTile &&
                _apeStructures.GetValueOrDefault(pair.Key) is ApeStructureKind.Library, new(-1, -1)).Key;
            if (library >= 0 && WrappedManhattanDistance(_positions[index], GetPosition(library)) > 1)
                return TryMoveTowardApeStructure(index, library, reservedPrey);
        }
        return TryMove(index, reservedPrey);
    }
}
