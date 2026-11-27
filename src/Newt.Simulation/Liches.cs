namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _lichArmy = [];
    public const int LichUndeadWarriorLimit = 25;
    private readonly Dictionary<int, HashSet<int>> _lichWarriors = [];

    public int GetLichUndeadWarriorCount(CritterId lich) =>
        _lichWarriors.TryGetValue(lich.Value, out var warriors) ? warriors.Count : 0;

    private void SetLichArmyOwner(int id, int lich)
    {
        RemoveLichArmyOwner(id);
        _lichArmy[id] = lich;
        if (_critterIndicesById.TryGetValue(id, out var index)) TrackLichWarrior(index);
    }

    private void UntrackLichWarrior(int id)
    {
        if (_lichArmy.TryGetValue(id, out var lich) && _lichWarriors.TryGetValue(lich, out var warriors))
        {
            warriors.Remove(id);
            if (warriors.Count == 0) _lichWarriors.Remove(lich);
        }
    }

    private void RemoveLichArmyOwner(int id)
    {
        UntrackLichWarrior(id);
        _lichArmy.Remove(id);
    }

    private void TrackLichWarrior(int index)
    {
        var id = _critterIds[index].Value;
        if (_species[index] is not CritterSpecies.UndeadWarrior || !_lichArmy.TryGetValue(id, out var lich)) return;
        if (!_lichWarriors.TryGetValue(lich, out var warriors)) _lichWarriors[lich] = warriors = [];
        warriors.Add(id);
    }
    internal const int LichEscortSize = 20;
    private void SpawnLichWarriors(CritterId lich, GridPosition origin)
    {
        var remaining = Math.Min(4, LichUndeadWarriorLimit - GetLichUndeadWarriorCount(lich));
        if (remaining <= 0) return;
        // Prefer free space; only replace living Apes when the nearby tiles are full.
        foreach (var replaceApes in new[] { false, true })
        for (var radius = 1; radius <= 3; radius++)
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius ||
                origin.Y + dy < 0 || origin.Y + dy >= Height)
                continue;
            var position = OffsetTile(origin, new(dx, dy));
            var tile = GetIndex(position);
            if (!CanLiveOn(CritterSpecies.UndeadWarrior, tile))
                continue;
            var occupant = _occupants[tile];
            CritterId warrior;
            if (occupant < 0)
                warrior = AddCritter(CritterSpecies.UndeadWarrior, position);
            else if (replaceApes && IsLichPrey(_species[occupant]))
            {
                warrior = _critterIds[occupant];
                ChangeCritterSpecies(occupant, CritterSpecies.UndeadWarrior, preserveEnergy: false);
                _reproductionTruces.Remove(warrior.Value);
            }
            else
                continue;
            SetLichArmyOwner(warrior.Value, lich.Value);
            if (--remaining == 0)
                return;
        }
    }
    private void CollapseLichArmy(int lich)
    {
        RemoveLichNecropolis(lich);
        var army = _lichArmy.Where(pair => pair.Value == lich).Select(pair => pair.Key).ToArray();
        foreach (var id in army)
        {
            RemoveLichArmyOwner(id);
            if (_critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.UndeadWarrior)
                RemoveCritter(new CritterId(id));
        }
        if (_lichWars.TryGetValue(lich, out var war))
            EndApeWar(war);
    }
    internal int GetCritterMovementIntervalTicks(int index)
    {
        var tile = GetIndex(_positions[index]);
        var inWater = _terrain[tile] is Terrain.Shallows or Terrain.Ocean or Terrain.DeepOcean ||
            (_terrain[tile] is not Terrain.Ice && _surfaceWater[tile] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake);
        var interval = GetMovementIntervalTicks(_species[index]) *
            (_species[index] is CritterSpecies.Lich or CritterSpecies.UndeadWarrior && inWater ? 3 : 1);
        if ((IsLivingApe(_species[index]) || _species[index] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor) &&
            _roadConnections.ContainsKey(tile))
            interval = Math.Max(1, interval / 2);
        return interval;
    }
    private static bool IsLichUndead(CritterSpecies species) =>
        species is CritterSpecies.Lich or CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior;

    private static bool IsLichPrey(CritterSpecies species) => IsLivingApe(species) ||
        species is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor;

    private void RestoreUndeadKillEnergy(int index) =>
        _energy[index] = Math.Min(CritterNutritions.Get(_species[index]).MaximumEnergy, _energy[index] + 5);

    private bool TryLichConversion(int killer, int victim)
    {
        if (_species[killer] is not CritterSpecies.Lich || !IsLichPrey(_species[victim]))
            return false;
        var lichId = _critterIds[killer].Value;
        var victimId = _critterIds[victim].Value;
        RecordLichApeKill(killer, victim);
        ChangeCritterSpecies(victim, CritterSpecies.UndeadApe, preserveEnergy: false);
        SetLichArmyOwner(victimId, lichId);
        _plagueImmunityRemainders[victimId] = GetOrCreatePlagueImmunityRemainder(lichId);
        _reproductionTruces.Remove(victimId);
        RestoreUndeadKillEnergy(killer);
        _preyTargets[killer] = -1;
        return true;
    }

    internal CritterId[] GetLichEscort(CritterId lich) =>
        _lichCampaigns.TryGetValue(lich.Value, out var campaign) && campaign.Tile >= 0
            ? campaign.Escorts.Order().Select(id => new CritterId(id)).ToArray() :
        _critterIndicesById.TryGetValue(lich.Value, out var leader) && _species[leader] is CritterSpecies.Lich
            ? _lichArmy.Where(pair => pair.Value == lich.Value &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) && _species[index] is CritterSpecies.UndeadWarrior)
                .OrderBy(pair => pair.Key).Take(LichEscortSize).Select(pair => new CritterId(pair.Key)).ToArray()
            : [];

    private GridPosition? TryMoveUndeadWarrior(int index, IReadOnlySet<GridPosition>? reserved)
    {
        var id = _critterIds[index];
        if (!_lichArmy.TryGetValue(id.Value, out var lichId) ||
            !_critterIndicesById.TryGetValue(lichId, out var chief) ||
            !GetLichEscort(new CritterId(lichId)).Contains(id))
            return TryMoveHunter(index, ApePerceptionRadius, reserved, huntWhenFull: true);
        _preyTargets[index] = -1;
        var prey = FindHunterPrey(index, _species[index], 1, reserved);
        if (prey is not null)
            return prey;
        if (WrappedManhattanDistance(_positions[index], _positions[chief]) > 3)
            TryMoveTowardApeStructure(index, GetIndex(_positions[chief]), reserved);
        return null;
    }
}
