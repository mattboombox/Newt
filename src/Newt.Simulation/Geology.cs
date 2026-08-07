namespace Newt.Simulation;

/// <summary>Elevation-changing geological operations used by tools and events.</summary>
public static class Geology
{
    /// <summary>
    /// Raises a soft circular region and returns the number of affected tiles.
    /// Horizontal distance follows the world's wrapping geometry.
    /// </summary>
    public static int ApplyRadialUplift(
        SimulationWorld world,
        GridPosition center,
        int radius,
        float strength)
    {
        ValidateStrength(strength);
        return ApplyRadialElevationChange(world, center, radius, strength);
    }

    /// <summary>
    /// Lowers a soft circular region and returns the number of affected tiles.
    /// Horizontal distance follows the world's wrapping geometry.
    /// </summary>
    public static int ApplyRadialLowering(
        SimulationWorld world,
        GridPosition center,
        int radius,
        float strength)
    {
        ValidateStrength(strength);
        return ApplyRadialElevationChange(world, center, radius, -strength);
    }

    private static int ApplyRadialElevationChange(
        SimulationWorld world,
        GridPosition center,
        int radius,
        float strength)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        var affectedTiles = 0;
        var freshwaterTouched = false;
        var radiusSquared = radius * radius;
        for (var offsetY = -radius; offsetY <= radius; offsetY++)
        {
            var y = center.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                var x = Mod(center.X + offsetX, world.Width);
                var position = new GridPosition(x, y);
                var normalizedDistance = distanceSquared / (float)radiusSquared;
                var falloff = 1 - normalizedDistance;
                var elevationChange = strength * falloff * falloff;
                freshwaterTouched |= elevationChange != 0 &&
                    world.GetSurfaceWater(position) is not SurfaceWaterKind.None;
                world.SetElevation(position, world.GetElevation(position) + elevationChange);
                affectedTiles++;
            }
        }

        TerrainClassifier.RebuildLandforms(world);
        if (freshwaterTouched)
        {
            Hydrology.RebuildFreshwater(world);
        }
        else
        {
            ClimateSystem.RebuildMoistureAndBiomes(world);
        }
        return affectedTiles;
    }

    private static void ValidateStrength(float strength)
    {
        if (strength <= 0 || !float.IsFinite(strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength));
        }
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
