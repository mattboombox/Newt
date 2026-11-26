namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int LargeVillageManagementPopulation = 64;
    private readonly Dictionary<int, long> _apeVillageManagementTicks = [];
    private readonly Dictionary<(int Village, ApeStructureKind Kind), int> _apeBuildingCounts = [];
    private long _apeBuildingCountsTick = -1;
    private int _apeBuildingStructureCount = -1, _apeBuildingAuxiliaryCount = -1;

    private bool IsVillageManagementDue(int village, int population)
    {
        if (population < LargeVillageManagementPopulation) return true;
        if (_apeVillageManagementTicks.TryGetValue(village, out var next) && Tick < next) return false;
        // Assign each settlement a stable phase so management work is spread over a second.
        var phase = village % TicksPerSecond;
        var delay = (phase - Tick % TicksPerSecond + TicksPerSecond) % TicksPerSecond;
        _apeVillageManagementTicks[village] = Tick + (delay == 0 ? TicksPerSecond : delay);
        return true;
    }

    private void InvalidateApeBuildingCounts() => _apeBuildingCountsTick = -1;

    private int GetCachedApeBuildingCount(int village, ApeStructureKind kind)
    {
        if (_apeBuildingCountsTick != Tick || _apeBuildingStructureCount != _apeStructures.Count ||
            _apeBuildingAuxiliaryCount != _apeAuxiliaryVillages.Count)
        {
            _apeBuildingCounts.Clear();
            foreach (var (tile, home) in _apeAuxiliaryVillages)
                if (_apeStructures.TryGetValue(tile, out var structure))
                {
                    var key = (home, structure);
                    _apeBuildingCounts[key] = _apeBuildingCounts.GetValueOrDefault(key) + 1;
                }
            _apeBuildingCountsTick = Tick;
            _apeBuildingStructureCount = _apeStructures.Count;
            _apeBuildingAuxiliaryCount = _apeAuxiliaryVillages.Count;
        }
        return _apeBuildingCounts.GetValueOrDefault((village, kind));
    }
}
