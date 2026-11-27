namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private bool AreFriendlyMilitary(int first, int second) =>
        IsApeMilitary(_species[first]) && IsApeMilitary(_species[second]) &&
        !AreWarEnemies(first, second) && !AreRivalApeFactions(first, second);

    private bool CanMoverDisplace(int mover, int blocker) =>
        CanDisplace(_apeSettlerTargets.ContainsKey(_critterIds[mover].Value)
            ? CritterSpecies.ApeTrader : _species[mover], _species[blocker]) || AreFriendlyMilitary(mover, blocker);

    private bool CanBeShovedOnto(int blocker, int tile) =>
        CanOccupyMegaWall(_species[blocker], tile) &&
        (IsLivingApe(_species[blocker]) || CanCritterLiveOn(blocker, tile));

    private void DelayShovedApeMovement(int index)
    {
        if (!IsLivingApe(_species[index]))
            return;
        var penalty = CanCritterRemainOnTile(index, GetIndex(_positions[index]))
            ? 1 : StrandedMovementIntervalMultiplier;
        _nextMovementTicks[index] = Math.Max(_nextMovementTicks[index],
            Tick + GetCritterMovementIntervalTicks(index) * penalty);
    }

    // Prefer a safe exchange with a civilian from the same village over a sidestep.
    // The checks are local; no vacancy search or worker path rebuild is needed here.
    private bool CanPreferApeSwap(int mover, int destination, IReadOnlySet<GridPosition>? reserved)
    {
        var blocker = _occupants[destination];
        return blocker >= 0 &&
            _species[blocker] is (CritterSpecies.Ape or CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack or
                CritterSpecies.ApeScholar or CritterSpecies.ApeSailor or CritterSpecies.ApeLudditePriest or CritterSpecies.ApeBishop) &&
            _apeVillageHomes.TryGetValue(_critterIds[mover].Value, out var home) &&
            _apeVillageHomes.TryGetValue(_critterIds[blocker].Value, out var otherHome) && home == otherHome &&
            !AreWarEnemies(mover, blocker) && !AreRivalApeFactions(mover, blocker) &&
            CanCritterRemainOnTile(blocker, GetIndex(_positions[mover])) &&
            CanSwapPastMovementBlocker(mover, destination, reserved);
    }

    // Otherwise, when there is no free sidestep, use the existing fallback exchange.
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
                _species[mover] is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or
                    CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor)) ||
            blocker < 0 || blocker == mover ||
            (fromTile is null && !ArePathNeighbors(origin, destination)) ||
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
