namespace Newt.Game;

/// <summary>Detects sustained host overload without changing simulation rules.</summary>
internal sealed class SimulationSpeedGuard
{
    internal const double FallbackRate = 8;
    internal const int MinimumCritterCount = 5_000;
    private bool _enabled = true;
    private double _slowSeconds;

    internal bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            Reset();
        }
    }

    internal bool ShouldReduce(
        double rate,
        int critterCount,
        TimeSpan updateDuration,
        TimeSpan targetDuration,
        bool runningSlowly)
    {
        if (!Enabled || rate <= FallbackRate || critterCount < MinimumCritterCount ||
            (!runningSlowly && updateDuration <= targetDuration * 1.25))
        {
            Reset();
            return false;
        }

        // A single stall must not count as multiple slow updates. The framework's
        // slow flag also catches rendering pressure and fixed-step catch-up loops.
        _slowSeconds += Math.Min(0.25,
            Math.Max(updateDuration.TotalSeconds, targetDuration.TotalSeconds));
        if (_slowSeconds < 1)
        {
            return false;
        }

        Reset();
        return true;
    }

    internal void Reset() => _slowSeconds = 0;
}
