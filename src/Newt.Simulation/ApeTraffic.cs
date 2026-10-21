namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private bool CanMoverDisplace(int mover, int blocker) =>
        CanDisplace(_apeSettlerTargets.ContainsKey(_critterIds[mover].Value)
            ? CritterSpecies.ApeTrader : _species[mover], _species[blocker]);

    private bool CanBeShovedOnto(int blocker, int tile) =>
        IsLivingApe(_species[blocker])
            ? _terrain[tile] is not Terrain.MegaWall
            : CanCritterLiveOn(blocker, tile);

    private void DelayShovedApeMovement(int index)
    {
        if (!IsLivingApe(_species[index]))
            return;
        var penalty = CanCritterRemainOnTile(index, GetIndex(_positions[index]))
            ? 1 : StrandedMovementIntervalMultiplier;
        _nextMovementTicks[index] = Math.Max(_nextMovementTicks[index],
            Tick + GetCritterMovementIntervalTicks(index) * penalty);
    }

    // When there is no free sidestep, the blocker can use the tile the mover vacates.
    // Traders passing other traders use their route-aware exchange instead.
    private bool TrySwapPastMovementBlocker(int mover, int destination,
        IReadOnlySet<GridPosition>? reserved)
    {
        if (!CanSwapPastMovementBlocker(mover, destination, reserved))
            return false;
        var blocker = _occupants[destination];
        var origin = GetIndex(_positions[mover]);

        var from = _positions[mover];
        var to = _positions[blocker];
        _positions[mover] = to;
        _positions[blocker] = from;
        _occupants[destination] = mover;
        _occupants[origin] = blocker;
        _nextMovementTicks[blocker] = Math.Max(_nextMovementTicks[blocker],
            Tick + GetMovementIntervalTicks(_species[blocker]));
        DelayShovedApeMovement(blocker);
        TriggerWolfDenNear(from, to, _species[mover]);
        TriggerWolfDenNear(to, from, _species[blocker]);
        return true;
    }

    private bool CanSwapPastMovementBlocker(int mover, int destination,
        IReadOnlySet<GridPosition>? reserved, int? fromTile = null)
    {
        var blocker = _occupants[destination];
        var origin = fromTile ?? GetIndex(_positions[mover]);
        if ((!_apeSettlerTargets.ContainsKey(_critterIds[mover].Value) &&
                _species[mover] is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor)) ||
            blocker < 0 || blocker == mover ||
            _species[blocker] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ||
            !CanMoverDisplace(mover, blocker) ||
            IsCaughtInMegaSpiderWeb(mover) || IsCaughtInMegaSpiderWeb(blocker) ||
            reserved?.Contains(_positions[mover]) is true || reserved?.Contains(_positions[blocker]) is true ||
            !CanCritterRemainOnTile(mover, destination) || !CanBeShovedOnto(blocker, origin) ||
            (IsApeMilitary(_species[mover]) && AreWarEnemies(mover, blocker)))
            return false;

        return true;
    }
}
