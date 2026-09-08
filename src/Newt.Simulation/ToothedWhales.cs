namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ToothedWhalePursuitTicks = 60 * TicksPerSecond;
    internal const int ToothedWhaleTrackingRadius = 14;
    private readonly Dictionary<int, (int PreyId, long UntilTick)> _toothedWhaleHunts = [];

    private GridPosition? TryMoveToothedWhale(int whaleIndex, IReadOnlySet<GridPosition>? reservedPrey)
    {
        var whaleId = _critterIds[whaleIndex].Value;
        var target = FindToothedWhalePrey(whaleIndex, reservedPrey);
        return TryMoveHunter(whaleIndex, ToothedWhalePerceptionRadius, reservedPrey,
            huntWhenFull: _toothedWhaleHunts.ContainsKey(whaleId), preferredTarget: target);
    }

    internal GridPosition? FindToothedWhalePrey(int whaleIndex, IReadOnlySet<GridPosition>? reservedPrey)
    {
        var whaleId = _critterIds[whaleIndex].Value;
        if (_toothedWhaleHunts.TryGetValue(whaleId, out var hunt) && Tick < hunt.UntilTick &&
            _critterIndicesById.TryGetValue(hunt.PreyId, out var preyIndex) &&
            CanPursuePrey(whaleIndex, preyIndex) &&
            CanLiveOn(CritterSpecies.ToothedWhale, GetIndex(_positions[preyIndex])) &&
            WrappedManhattanDistance(_positions[whaleIndex], _positions[preyIndex]) <= ToothedWhaleTrackingRadius &&
            IsPreyPursuitAllowedAtDistance(CritterSpecies.ToothedWhale, _species[preyIndex],
                WrappedManhattanDistance(_positions[whaleIndex], _positions[preyIndex])))
        {
            // Another hunter reserving this prey for one tick must not erase the whale's memory.
            return reservedPrey?.Contains(_positions[preyIndex]) is true ? null : _positions[preyIndex];
        }

        _toothedWhaleHunts.Remove(whaleId);
        if (_energy[whaleIndex] >= CritterNutritions.Get(CritterSpecies.ToothedWhale).MaximumEnergy)
            return null;
        var target = FindHunterPrey(whaleIndex, CritterSpecies.ToothedWhale,
            ToothedWhalePerceptionRadius, reservedPrey);
        if (target is { } position)
        {
            var prey = _occupants[GetIndex(position)];
            _toothedWhaleHunts[whaleId] = (_critterIds[prey].Value, Tick + ToothedWhalePursuitTicks);
        }
        return target;
    }
}
