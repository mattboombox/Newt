namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, (int Residents, int Sustaining)> _apePopulationCounts = [];
    private readonly Dictionary<int, (int Home, CritterSpecies Species)> _apePopulationMembers = [];
    private long _apePopulationCountsTick = -1;
    private int _apePopulationHomeCount = -1;

    private (int Residents, int Sustaining) GetCachedApePopulation(int village)
    {
        if (_apePopulationCountsTick != Tick || _apePopulationHomeCount != _apeVillageHomes.Count)
        {
            _apePopulationCounts.Clear();
            _apePopulationMembers.Clear();
            foreach (var (id, home) in _apeVillageHomes)
                if (_critterIndicesById.TryGetValue(id, out var index))
                    AddPopulationMember(id, home, _species[index]);
            _apePopulationCountsTick = Tick;
            _apePopulationHomeCount = _apeVillageHomes.Count;
        }
        return _apePopulationCounts.GetValueOrDefault(village);
    }

    private static (int Residents, int Sustaining) PopulationContribution(CritterSpecies species) =>
        (species is CritterSpecies.Dog ? 0 : 1,
            species is CritterSpecies.Dog or CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ? 0 : 1);

    private void AddPopulationMember(int id, int home, CritterSpecies species)
    {
        var current = _apePopulationCounts.GetValueOrDefault(home);
        var contribution = PopulationContribution(species);
        _apePopulationCounts[home] = (current.Residents + contribution.Residents, current.Sustaining + contribution.Sustaining);
        _apePopulationMembers[id] = (home, species);
    }

    private void RefreshApePopulationMember(int id)
    {
        if (_apePopulationCountsTick != Tick) return;
        if (_apePopulationMembers.Remove(id, out var old))
        {
            var current = _apePopulationCounts.GetValueOrDefault(old.Home);
            var contribution = PopulationContribution(old.Species);
            _apePopulationCounts[old.Home] = (current.Residents - contribution.Residents, current.Sustaining - contribution.Sustaining);
        }
        if (_apeVillageHomes.TryGetValue(id, out var home) && _critterIndicesById.TryGetValue(id, out var index))
            AddPopulationMember(id, home, _species[index]);
        _apePopulationHomeCount = _apeVillageHomes.Count;
    }

    private void SetApeVillageHome(int id, int home)
    {
        if (_apePopulationHomeCount != _apeVillageHomes.Count) _apePopulationCountsTick = -1;
        _apeVillageHomes[id] = home;
        RefreshApePopulationMember(id);
    }

    private bool RemoveApeVillageHome(int id, out int home)
    {
        if (_apePopulationHomeCount != _apeVillageHomes.Count) _apePopulationCountsTick = -1;
        if (!_apeVillageHomes.Remove(id, out home)) return false;
        RefreshApePopulationMember(id);
        return true;
    }

    private bool RemoveApeVillageHome(int id) => RemoveApeVillageHome(id, out _);
}
