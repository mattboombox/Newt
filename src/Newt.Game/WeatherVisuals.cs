using Newt.Simulation;

namespace Newt.Game;

internal enum PrecipitationKind { None, Rain, Snow }
internal enum CloudKind { None, White, Moist }

internal static class WeatherVisuals
{
    internal const float RainCloudDensity = 0.28f;

    // A bank must cover roughly 865 square world tiles before producing precipitation.
    internal const int RainCloudCellCount = 2048;

    internal static bool HasCloud(float density, float shape) =>
        density > 0.11f && Math.Abs(shape) > 0.10f;

    internal static bool CloudBankCanRain(uint weatherSeed) => (Hash(weatherSeed) & 1) == 0;

    internal static CloudKind CloudBank(int cellCount, bool rainEligible = true) => cellCount <= 0 ? CloudKind.None
        : rainEligible && cellCount >= RainCloudCellCount ? CloudKind.Moist : CloudKind.White;

    internal const double CloudColorFadeSeconds = 2;

    internal static float CloudWetness(float from, bool wet, double secondsSinceChange)
    {
        var t = (float)Math.Clamp(secondsSinceChange / CloudColorFadeSeconds, 0, 1);
        t = t * t * (3 - 2 * t);
        return from + ((wet ? 1f : 0f) - from) * t;
    }
    internal static (float CellSize, float Opacity, bool Detailed) SnowAppearance(float worldUnitsPerPixel)
    {
        // Let nearby flakes visibly grow, up to a nine-pixel-wide cross. Distant
        // flakes retain their physical size and fade instead of filling pixels.
        var cellSize = Math.Min(0.018f, worldUnitsPerPixel * 3);
        var projectedSize = cellSize / worldUnitsPerPixel;
        return (cellSize, Math.Clamp((projectedSize - 0.08f) / 0.52f, 0, 1), projectedSize >= 0.75f);
    }

    internal static uint Hash(uint value)
    {
        value = (value ^ (value >> 16)) * 0x7feb352du;
        value = (value ^ (value >> 15)) * 0x846ca68bu;
        return value ^ (value >> 16);
    }

    internal static uint CellSeed(int x, int y, int face, int copy = 0) =>
        Hash(unchecked((uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^
            (uint)(face + 1) * 0xc2b2ae35u ^ (uint)copy * 0x27d4eb2fu));

    internal static float RandomFraction(uint seed) => (Hash(seed) >> 8) / 16777216f;

    internal static double DropLifetime(PrecipitationKind kind) => kind == PrecipitationKind.Snow ? 1 / 0.12 : 1 / 0.92;

    internal static double PrewarmedDropBirth(double seconds, PrecipitationKind kind, uint seed, int channel)
    {
        var lifetime = DropLifetime(kind);
        var phase = (seconds / lifetime + RandomFraction(seed) +
            (channel + RandomFraction(seed + (uint)(channel * 17 + 1))) / 6.0) % 1;
        return seconds - phase * lifetime;
    }

    internal static bool IsDropAlive(double seconds, double bornAt, PrecipitationKind kind) =>
        seconds >= bornAt && seconds - bornAt < DropLifetime(kind);

    internal static PrecipitationKind Precipitation(float density, MoistureBand moisture, TemperatureBand temperature) =>
        density < RainCloudDensity || moisture == MoistureBand.Dry ? PrecipitationKind.None :
        temperature == TemperatureBand.Freezing ? PrecipitationKind.Snow : PrecipitationKind.Rain;

    // Shared with the river waterfall droplets, including the short splash phase.
    internal static float WaterfallDropAge(double seconds, double phase, int drop) =>
        (float)((seconds * 0.92 + phase + drop / 6.0) % 1);

    internal static float WaterfallFall(float age)
    {
        var progress = Math.Clamp(age / 0.73f, 0, 1);
        return progress * progress;
    }

    internal static float SnowFall(double seconds, double phase, int drop) =>
        (float)((seconds * 0.12 + phase + drop / 6.0) % 1);
}
