namespace Newt.Game;

internal sealed class RifleShotTiming(float distance)
{
    internal float TravelSeconds { get; } = Math.Clamp(distance / 600f, 0.06f, 0.3f);
    internal float Age { get; private set; }
    internal bool Impacted { get; private set; }
    internal float Progress => Math.Clamp(Age / TravelSeconds, 0, 1);
    internal float Opacity => Math.Clamp(1 - Math.Max(0, Age - TravelSeconds) / 1.6f, 0, 1);
    internal bool Finished => Age >= TravelSeconds + 1.6f;
    internal bool ShockwaveVisible => Impacted && Age - TravelSeconds < 0.6f;
    internal float ShockwaveProgress => Math.Clamp((Age - TravelSeconds) / 0.6f, 0, 1);

    internal bool Advance(float seconds)
    {
        Age += seconds;
        if (Impacted || Age < TravelSeconds) return false;
        Impacted = true;
        return true;
    }
}
