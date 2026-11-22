namespace Newt.Game;

internal sealed class RifleShotTiming(float distance, bool shockwave = false, bool instant = false, float fadeSeconds = 1.6f)
{
    internal float TravelSeconds { get; } = instant ? 0 : Math.Clamp(distance / 600f, 0.06f, 0.3f);
    internal float Age { get; private set; }
    internal bool Impacted { get; private set; }
    internal float Progress => TravelSeconds <= 0 ? 1 : Math.Clamp(Age / TravelSeconds, 0, 1);
    internal float Opacity => Math.Clamp(1 - Math.Max(0, Age - TravelSeconds) / fadeSeconds, 0, 1);
    internal bool Finished => Age >= TravelSeconds + fadeSeconds;
    internal bool ShockwaveVisible => shockwave && Impacted && Age - TravelSeconds < 0.6f;
    internal float ShockwaveProgress => Math.Clamp((Age - TravelSeconds) / 0.6f, 0, 1);

    internal bool Advance(float seconds)
    {
        Age += seconds;
        if (Impacted || Age < TravelSeconds) return false;
        Impacted = true;
        return true;
    }
}
