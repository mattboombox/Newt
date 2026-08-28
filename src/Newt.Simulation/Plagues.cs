namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int PlagueSpreadIntervalTicks = TicksPerSecond;
    public const int PlagueDrainIntervalTicks = 10 * TicksPerSecond;
    public const int VillagePlaguePopulationThreshold = 200;
    public const int VillagePlagueCheckIntervalTicks = 60 * TicksPerSecond;
    public const int VillagePlagueChancePercent = 1;

    // Stable IDs keep sparse infections valid when the critter arrays compact.
    private readonly Dictionary<int, (PlagueKind Kind, long InfectedTick, long DrainTick)> _plagues = [];

    /// <summary>Living apes and sailors infected by either strain; excludes undead.</summary>
    public int SickApeCount => _plagues.Count;

    /// <summary>Infects a living ape on the selected tile; IDs divisible by five resist both strains.</summary>
    public bool TryInfectApeAt(GridPosition position, PlagueKind kind)
    {
        if (kind is not (PlagueKind.Plague or PlagueKind.Zombie) || !Contains(position))
        {
            return false;
        }
        var index = _occupants[GetIndex(position)];
        if (index < 0 || !IsLivingApe(_species[index]) || _critterIds[index].Value % 5 == 0)
        {
            return false;
        }
        var id = _critterIds[index].Value;
        if (_plagues.TryGetValue(id, out var infection))
        {
            // Zombie plague can supersede ordinary plague without resetting its damage clock.
            if (kind is not PlagueKind.Zombie || infection.Kind is PlagueKind.Zombie)
            {
                return false;
            }
            _plagues[id] = (kind, Tick, infection.DrainTick);
        }
        else
        {
            _plagues[id] = (kind, Tick, Tick + PlagueDrainIntervalTicks);
        }
        return true;
    }

    private static bool IsLivingApe(CritterSpecies species) =>
        species is CritterSpecies.Ape or CritterSpecies.ApeSailor;

    internal void AdvanceVillagePlagueOutbreaks()
    {
        if (!NaturalEventsEnabled || Tick == 0 ||
            Tick % VillagePlagueCheckIntervalTicks != 0 ||
            _apeVillageHomes.Count < VillagePlaguePopulationThreshold)
        {
            return;
        }

        foreach (var (tile, structure) in _apeStructures)
        {
            if (structure is ApeStructureKind.Village)
            {
                TryStartVillagePlague(tile);
            }
        }
    }

    internal bool TryStartVillagePlague(int villageTile)
    {
        if (!NaturalEventsEnabled ||
            !_apeStructures.TryGetValue(villageTile, out var structure) ||
            structure is not ApeStructureKind.Village ||
            GetApeVillageResidentCountByTile(villageTile) < VillagePlaguePopulationThreshold)
        {
            return false;
        }

        // Do not seed another outbreak while either strain is circulating here.
        foreach (var infectedId in _plagues.Keys)
        {
            if (_apeVillageHomes.TryGetValue(infectedId, out var home) && home == villageTile)
            {
                return false;
            }
        }
        if (NextInt(100) >= VillagePlagueChancePercent)
        {
            return false;
        }

        var selected = -1;
        var candidates = 0;
        foreach (var (id, home) in _apeVillageHomes)
        {
            if (home == villageTile && id % 5 != 0 &&
                _critterIndicesById.TryGetValue(id, out var index) && IsLivingApe(_species[index]) &&
                NextInt(++candidates) == 0)
            {
                selected = index;
            }
        }
        return selected >= 0 && TryInfectApeAt(_positions[selected], PlagueKind.Plague);
    }

    private PlagueKind GetPlague(int index) =>
        _species[index] is CritterSpecies.UndeadApe ? PlagueKind.Zombie :
        _plagues.TryGetValue(_critterIds[index].Value, out var infection) ? infection.Kind : PlagueKind.None;

    private void SpreadPlagues()
    {
        if (Tick % PlagueSpreadIntervalTicks != 0 ||
            (_plagues.Count == 0 && GetCritterCount(CritterSpecies.UndeadApe) == 0))
        {
            return;
        }
        for (var index = 0; index < _count; index++)
        {
            var kind = GetPlague(index);
            if (kind is PlagueKind.None ||
                (_plagues.TryGetValue(_critterIds[index].Value, out var infection) &&
                    infection.InfectedTick >= Tick))
            {
                continue;
            }
            // Eight grid lookups per contagious ape, never a population search.
            foreach (var direction in MovementDirections)
            {
                var position = _positions[index];
                TryInfectApeAt(new GridPosition(
                    Mod(position.X + direction.X, Width), position.Y + direction.Y), kind);
            }
        }
    }

    private void DrainPlagueEnergy(int index)
    {
        if (_plagues.Count == 0)
        {
            return;
        }
        var id = _critterIds[index].Value;
        if (!_plagues.TryGetValue(id, out var infection) || Tick < infection.DrainTick)
        {
            return;
        }
        _plagues[id] = (infection.Kind, infection.InfectedTick, Tick + PlagueDrainIntervalTicks);
        _energy[index] = Math.Max(0, _energy[index] - 1);
        _damageFlashUntilTicks[index] = Tick + CombatDamageFlashTicks;
    }

    private bool TryReanimateApe(int index)
    {
        if (!IsLivingApe(_species[index]) || GetPlague(index) is not PlagueKind.Zombie)
        {
            return false;
        }
        ChangeCritterSpecies(index, CritterSpecies.UndeadApe, preserveEnergy: false);
        _reproductionTruces.Remove(_critterIds[index].Value);
        return true;
    }
}
