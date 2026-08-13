namespace Newt.Simulation;

/// <summary>Advances a simple axial season cycle and applies it to temperature.</summary>
public static class SeasonSystem
{
    public const int CycleSeconds = 360;
    public const float EquatorialBand = 0.20f;
    public const float PolarTemperatureAmplitude = 0.10f;
    public const int TicksPerYear = CycleSeconds * SimulationWorld.TicksPerSecond;
    // Seasonal temperature changes are gradual; refreshing the whole climate
    // every ten seconds avoids repeated map-wide work without visible stepping.
    private const int ClimateRefreshTicks = 10 * SimulationWorld.TicksPerSecond;

    public static void SetEnabled(SimulationWorld world, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Body is WorldBody.RingWorld)
        {
            enabled = false;
        }
        if (world.SeasonsEnabled == enabled)
        {
            return;
        }

        world.SeasonsEnabled = enabled;
        TerrainClassifier.RebuildAll(world);
    }

    public static Season GetSeason(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        var signedLatitude = GetSignedLatitude(world, position.Y);
        if (!world.SeasonsEnabled || Math.Abs(signedLatitude) <= EquatorialBand)
        {
            return Season.PermanentSummer;
        }

        var phase = GetCyclePhase(world);
        if (signedLatitude < 0)
        {
            phase = (phase + 0.5f) % 1f;
        }

        return phase switch
        {
            < 0.125f or >= 0.875f => Season.Spring,
            < 0.375f => Season.Summer,
            < 0.625f => Season.Fall,
            _ => Season.Winter,
        };
    }

    /// <summary>Returns the signed temperature contribution from the current season.</summary>
    public static float GetTemperatureChange(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return GetTemperatureOffset(world, position.Y);
    }

    /// <summary>Moisture is intentionally not changed by the simple seasonal model.</summary>
    public static float GetMoistureChange(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return 0;
    }

    internal static float GetTemperatureOffset(SimulationWorld world, int y)
    {
        if (!world.SeasonsEnabled)
        {
            return 0;
        }

        var signedLatitude = GetSignedLatitude(world, y);
        var distanceFromEquatorialBand = Math.Max(0, Math.Abs(signedLatitude) - EquatorialBand);
        var influence = distanceFromEquatorialBand / (1 - EquatorialBand);
        influence = influence * influence * (3 - 2 * influence);
        var hemisphere = signedLatitude >= 0 ? 1f : -1f;
        return MathF.Sin(GetCyclePhase(world) * MathF.Tau) * hemisphere *
            influence * PolarTemperatureAmplitude;
    }

    internal static void Advance(SimulationWorld world)
    {
        if (!world.SeasonsEnabled)
        {
            return;
        }

        world.SeasonTick++;
        if (world.SeasonTick % ClimateRefreshTicks == 0)
        {
            TerrainClassifier.RebuildLandforms(world);
            ClimateSystem.RebuildBiomesFromCurrentMoisture(world);
        }
    }

    private static float GetCyclePhase(SimulationWorld world) =>
        (world.SeasonTick % TicksPerYear) / (float)TicksPerYear;

    private static float GetSignedLatitude(SimulationWorld world, int y) =>
        1 - (y + 0.5f) / world.Height * 2;
}
