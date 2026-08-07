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
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
        if (strength <= 0 || !float.IsFinite(strength))
        {
            throw new ArgumentOutOfRangeException(nameof(strength));
        }

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
                var uplift = strength * falloff * falloff;
                world.SetElevation(position, world.GetElevation(position) + uplift);
                affectedTiles++;
            }
        }

        TerrainClassifier.RebuildAll(world);
        return affectedTiles;
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
