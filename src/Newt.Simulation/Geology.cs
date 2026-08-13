namespace Newt.Simulation;

/// <summary>Elevation-changing geological operations used by tools and events.</summary>
public static class Geology
{
    public const float SeaLevelEditStep = 0.01f;

    /// <summary>Moves the global ocean surface and rebuilds ocean connectivity and freshwater.</summary>
    public static void ChangeSeaLevel(SimulationWorld world, float amount)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (amount == 0 || !float.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        world.SeaLevel = Math.Clamp(
            world.SeaLevel + amount,
            SimulationWorld.MinimumSeaLevel,
            SimulationWorld.MaximumSeaLevel);
        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
    }

    /// <summary>Moves or creates the world's single saltwater source and rebuilds water connectivity.</summary>
    public static void MoveOceanSeed(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        world.OceanSeed = position;
        world.HasOceans = true;
        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
    }

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
                world.SetElevation(position, world.GetElevation(position) + elevationChange);
                affectedTiles++;
            }
        }

        TerrainClassifier.RebuildLandforms(world);
        // A changed sill can connect a distant freshwater basin to the ocean
        // even when the brush never touches the lake itself. Always retrace the
        // persistent springs so freshwater cannot remain layered over saltwater.
        Hydrology.RebuildFreshwater(world);
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
