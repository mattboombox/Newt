namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, int> _lichArmy = [];
    internal const int LichEscortSize = 20;
    private void SpawnLichWarriors(CritterId lich, GridPosition origin)
    {
        var remaining = 4;
        // Prefer free space; only replace living Apes when the nearby tiles are full.
        foreach (var replaceApes in new[] { false, true })
        for (var radius = 1; radius <= 3; radius++)
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius ||
                origin.Y + dy < 0 || origin.Y + dy >= Height)
                continue;
            var position = new GridPosition(Mod(origin.X + dx, Width), origin.Y + dy);
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
            _lichArmy[warrior.Value] = lich.Value;
            if (--remaining == 0)
                return;
        }
    }
    private void CollapseLichArmy(int lich)
    {
        var army = _lichArmy.Where(pair => pair.Value == lich).Select(pair => pair.Key).ToArray();
        foreach (var id in army)
        {
            _lichArmy.Remove(id);
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
        return GetMovementIntervalTicks(_species[index]) *
            (_species[index] is CritterSpecies.Lich or CritterSpecies.UndeadWarrior && inWater ? 3 : 1);
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
        ChangeCritterSpecies(victim, CritterSpecies.UndeadApe, preserveEnergy: false);
        _lichArmy[victimId] = lichId;
        _reproductionTruces.Remove(victimId);
        RestoreUndeadKillEnergy(killer);
        _preyTargets[killer] = -1;
        return true;
    }

    internal CritterId[] GetLichEscort(CritterId lich) =>
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
