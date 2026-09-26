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
    private readonly Dictionary<int, int> _plagueImmunityRemainders = [];

    /// <summary>Infected living apes and specialists; excludes undead and vampires.</summary>
    public int SickApeCount => _plagues.Count;

    /// <summary>Seeds an outbreak with a random immune ID remainder, protecting about one ape in five.</summary>
    public bool TryInfectApeAt(GridPosition position, PlagueKind kind) =>
        TryInfectApeAt(position, kind, NextInt(5));

    internal bool TryInfectApeAt(GridPosition position, PlagueKind kind, int immuneRemainder)
    {
        if (kind is not (PlagueKind.Plague or PlagueKind.Zombie or PlagueKind.Vampire) || !Contains(position))
        {
            return false;
        }
        var index = _occupants[GetIndex(position)];
        if (index < 0 || !IsLivingApe(_species[index]) ||
            _species[index] is CritterSpecies.ApeSailor || _critterIds[index].Value % 5 == immuneRemainder)
        {
            return false;
        }
        var id = _critterIds[index].Value;
        if (_species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeScholar or CritterSpecies.ApeChieftain)
        {
            ChangeCritterSpecies(
                index,
                CritterSpecies.Ape,
                preserveEnergy: true,
                preserveApeVillage: true);
        }
        if (_plagues.TryGetValue(id, out var infection))
        {
            // Stronger strains supersede ordinary plague without resetting its damage clock.
            if (infection.Kind is PlagueKind.Vampire || kind == infection.Kind || kind is PlagueKind.Plague)
            {
                return false;
            }
            _plagues[id] = (kind, Tick, infection.DrainTick);
        }
        else
        {
            _plagues[id] = (kind, Tick, Tick + PlagueDrainIntervalTicks);
        }
        _plagueImmunityRemainders[id] = immuneRemainder;
        return true;
    }

    private static bool IsLivingApe(CritterSpecies species) =>
        species is CritterSpecies.Ape or CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or
            CritterSpecies.ApeScholar or CritterSpecies.ApeChieftain;

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
        var immuneRemainder = NextInt(5);
        foreach (var (id, home) in _apeVillageHomes)
        {
            if (home == villageTile && id % 5 != immuneRemainder &&
                _critterIndicesById.TryGetValue(id, out var index) && IsLivingApe(_species[index]) &&
                _species[index] is not CritterSpecies.ApeSailor &&
                NextInt(++candidates) == 0)
            {
                selected = index;
            }
        }
        return selected >= 0 && TryInfectApeAt(_positions[selected], PlagueKind.Plague, immuneRemainder);
    }

    private int GetPlagueImmunityRemainder(int index)
    {
        var id = _critterIds[index].Value;
        if (!_plagueImmunityRemainders.TryGetValue(id, out var remainder))
            _plagueImmunityRemainders[id] = remainder = NextInt(5);
        return remainder;
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
            if (kind is PlagueKind.None or PlagueKind.Vampire ||
                (_plagues.TryGetValue(_critterIds[index].Value, out var infection) &&
                    infection.InfectedTick >= Tick))
            {
                continue;
            }
            // Eight grid lookups per contagious ape, never a population search.
            var immuneRemainder = GetPlagueImmunityRemainder(index);
            foreach (var direction in MovementDirections)
            {
                var position = _positions[index];
                TryInfectApeAt(new GridPosition(
                    Mod(position.X + direction.X, Width), position.Y + direction.Y), kind, immuneRemainder);
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
        if (!IsLivingApe(_species[index]) || GetPlague(index) is not (PlagueKind.Zombie or PlagueKind.Vampire))
        {
            return false;
        }
        var risen = GetPlague(index) is PlagueKind.Vampire ? CritterSpecies.Vampire : CritterSpecies.UndeadApe;
        ChangeCritterSpecies(index, risen, preserveEnergy: false);
        _reproductionTruces.Remove(_critterIds[index].Value);
        return true;
    }
}
