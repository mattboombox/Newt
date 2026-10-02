namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _apeWorkplaces = [];

    private int GetApeWorkplace(int workerId, CritterSpecies species)
    {
        if (!_apeWorkplaces.TryGetValue(workerId, out var tile))
            return -1;
        return species switch
        {
            CritterSpecies.ApeFarmer when
                _apeFarmers.GetValueOrDefault(tile, -1) == workerId && HasApeFarmer(tile) => tile,
            CritterSpecies.ApeLumberjack when
                _apeLumberjacks.GetValueOrDefault(tile, -1) == workerId && HasApeLumberjack(tile) => tile,
            _ => -1,
        };
    }

    private void DetachApeWorker(int workerId)
    {
        if (!_apeWorkplaces.Remove(workerId, out var tile))
            return;
        if (_apeFarmers.GetValueOrDefault(tile, -1) == workerId)
            _apeFarmers.Remove(tile);
        if (_apeLumberjacks.GetValueOrDefault(tile, -1) == workerId)
            _apeLumberjacks.Remove(tile);
    }
}
