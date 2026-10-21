namespace Newt.Game;

/// <summary>Selects maximum speed below the population limits and caps speed above them.</summary>
internal sealed class SimulationSpeedGuard
{
    internal const double FallbackRate = 1;
    internal const double MaximumRate = 32;
    internal const int MinimumCritterCount = 20_000;
    internal const int MinimumNonBarbarianApeCount = 250;

    internal bool Enabled { get; set; } = true;
    internal bool AutomaticallyReduced { get; private set; }

    internal double GetRate(double rate, int critterCount, int nonBarbarianApeCount)
    {
        var aboveLimit = critterCount >= MinimumCritterCount ||
            nonBarbarianApeCount >= MinimumNonBarbarianApeCount;
        if (!Enabled)
        {
            Reset();
            return rate;
        }
        AutomaticallyReduced = aboveLimit && rate >= FallbackRate;
        return aboveLimit ? Math.Min(rate, FallbackRate) : MaximumRate;
    }

    internal void Reset()
    {
        AutomaticallyReduced = false;
    }
}
