namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ApeUniqueBuildingRebuildDelayTicks = 60 * TicksPerSecond;
    private static readonly ApeStructureKind[] RecoverableApeBuildings =
        [ApeStructureKind.Market, ApeStructureKind.Library];
    private readonly Dictionary<(int Village, ApeStructureKind Kind), (long Due, int VillageId)> _apeBuildingRebuilds = [];

    private void ScheduleApeBuildingReplacement(int village, ApeStructureKind kind)
    {
        if (!IsRoadVillage(village) || !RecoverableApeBuildings.Contains(kind)) return;
        if (kind is ApeStructureKind.Library && ApeCultureBlocksTechnology(village)) return;
        _apeBuildingRebuilds[(village, kind)] = (Tick + ApeUniqueBuildingRebuildDelayTicks,
            _apeVillageIds[village]);
    }

    private void AdvanceApeBuildingReplacements(int village)
    {
        foreach (var kind in RecoverableApeBuildings)
        {
            var key = (village, kind);
            if (!_apeBuildingRebuilds.TryGetValue(key, out var pending)) continue;
            if (_apeVillageIds.GetValueOrDefault(village) != pending.VillageId || HasApeStructure(village, kind) ||
                kind is ApeStructureKind.Library && ApeCultureBlocksTechnology(village))
            {
                _apeBuildingRebuilds.Remove(key);
                continue;
            }
            if (Tick < pending.Due) continue;
            var rebuilt = TryPurchaseApeStructure(village, kind);
            if (rebuilt) _apeBuildingRebuilds.Remove(key);
        }
    }

    private bool IsSavingForApeBuildingReplacement(int village)
    {
        foreach (var kind in new[] { ApeStructureKind.Market, ApeStructureKind.Library })
            if (_apeBuildingRebuilds.TryGetValue((village, kind), out var pending) && Tick >= pending.Due &&
                !HasApeStructure(village, kind) && IsApeBuildingTechnologyUnlocked(village, kind) &&
                (kind is not ApeStructureKind.Market || CanBuildApeMarket(GetPosition(village))) &&
                GetApeConstructionCandidates(village, kind).Count > 0) return true;
        return false;
    }
}
