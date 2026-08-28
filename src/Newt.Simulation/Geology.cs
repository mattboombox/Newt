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

    /// <summary>Moves or creates the primary saltwater source without replacing additional sources.</summary>
    public static void MoveOceanSeed(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        world.OceanSeed = position;
        world.SetAdditionalOceanSeeds(world.AdditionalOceanSeeds);
        world.HasOceans = true;
        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
    }

    /// <summary>Adds a distinct ocean source at or below sea level, preserving existing sources.</summary>
    public static bool TryAddOceanSeed(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.TryRegisterOceanSeed(position))
        {
            return false;
        }
        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
        return true;
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

    /// <summary>
    /// Blends heights toward their local 3-by-3 average with a soft circular falloff.
    /// All averages use the original heights; returns the number of changed tiles.
    /// </summary>
    public static int ApplyRadialSmoothing(
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
        if (!float.IsFinite(strength) || strength is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(strength));
        }
        if (strength == 0)
        {
            return 0;
        }

        var changes = new List<(GridPosition Position, float Elevation)>();
        var radiusSquared = (double)radius * radius;
        // Visit each wrapped column only once, even when the brush spans the world.
        var firstX = -Math.Min(radius, world.Width / 2);
        var lastX = Math.Min(radius, (world.Width - 1) / 2);
        var firstY = -Math.Min(radius, center.Y);
        var lastY = Math.Min(radius, world.Height - 1 - center.Y);
        for (var dy = firstY; dy <= lastY; dy++)
        {
            for (var dx = firstX; dx <= lastX; dx++)
            {
                var distanceSquared = (double)dx * dx + (double)dy * dy;
                if (distanceSquared >= radiusSquared)
                {
                    continue;
                }
                var position = new GridPosition(Mod(center.X + dx, world.Width), center.Y + dy);
                if (world.GetTerrain(position) is Terrain.RingWorldWall)
                {
                    continue;
                }

                double sum = 0;
                var samples = 0;
                for (var ny = Math.Max(0, position.Y - 1); ny <= Math.Min(world.Height - 1, position.Y + 1); ny++)
                {
                    for (var nx = -Math.Min(1, world.Width / 2); nx <= Math.Min(1, (world.Width - 1) / 2); nx++)
                    {
                        var neighbor = new GridPosition(Mod(position.X + nx, world.Width), ny);
                        if (world.GetTerrain(neighbor) is Terrain.RingWorldWall)
                        {
                            continue;
                        }
                        sum += world.GetElevation(neighbor);
                        samples++;
                    }
                }

                var original = world.GetElevation(position);
                var falloff = 1 - distanceSquared / radiusSquared;
                var smoothed = (float)(original + (sum / samples - original) * strength * falloff * falloff);
                if (smoothed != original)
                {
                    changes.Add((position, smoothed));
                }
            }
        }

        foreach (var change in changes)
        {
            world.SetElevation(change.Position, change.Elevation);
        }
        if (changes.Count > 0)
        {
            TerrainClassifier.RebuildLandforms(world);
            Hydrology.RebuildFreshwater(world);
        }
        return changes.Count;
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
