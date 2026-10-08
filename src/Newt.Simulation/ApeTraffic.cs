namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    // When there is no free sidestep, the blocker can use the tile the mover vacates.
    // Traders passing other traders use their route-aware exchange instead.
    private bool TrySwapPastMovementBlocker(int mover, int destination,
        IReadOnlySet<GridPosition>? reserved)
    {
        var blocker = _occupants[destination];
        var origin = GetIndex(_positions[mover]);
        if (_species[mover] is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor) ||
            blocker < 0 || blocker == mover ||
            _species[blocker] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ||
            !CanDisplace(_species[mover], _species[blocker]) ||
            IsCaughtInMegaSpiderWeb(mover) || IsCaughtInMegaSpiderWeb(blocker) ||
            reserved?.Contains(_positions[mover]) is true || reserved?.Contains(_positions[blocker]) is true ||
            !CanCritterLiveOn(mover, destination) || !CanCritterLiveOn(blocker, origin) ||
            (IsApeMilitary(_species[mover]) && AreWarEnemies(mover, blocker)))
            return false;

        var from = _positions[mover];
        var to = _positions[blocker];
        _positions[mover] = to;
        _positions[blocker] = from;
        _occupants[destination] = mover;
        _occupants[origin] = blocker;
        _nextMovementTicks[blocker] = Math.Max(_nextMovementTicks[blocker],
            Tick + GetMovementIntervalTicks(_species[blocker]));
        TriggerWolfDenNear(from, to, _species[mover]);
        TriggerWolfDenNear(to, from, _species[blocker]);
        return true;
    }
}
