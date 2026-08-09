namespace Newt.Simulation;

/// <summary>Meteor crater excavation, ejecta, fragmentation, melt, and shockwaves.</summary>
public static class Impacts
{
    private const float MinimumMagnitude = 0f;
    private const float MaximumMagnitude = 1f;

    public static MeteorImpactResult CreateMeteorImpact(
        SimulationWorld world,
        GridPosition center,
        float magnitude)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
        if (!float.IsFinite(magnitude) || magnitude is < MinimumMagnitude or > MaximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        var minimumDimension = Math.Min(world.Width, world.Height);
        var craterRadius = 2 + (int)MathF.Round(minimumDimension * 0.18f * MathF.Pow(magnitude, 1.7f));
        var shockRadius = craterRadius * 3;
        ApplyCrater(world, center, craterRadius, magnitude);

        var fragmentCount = (int)MathF.Round(magnitude * 10f);
        if (magnitude >= 0.2f)
        {
            fragmentCount += world.NextInt(3);
        }
        AddFragments(world, center, craterRadius, shockRadius, magnitude, fragmentCount);

        if (magnitude >= 0.12f)
        {
            var meltFlows = Math.Clamp(1 + (int)MathF.Floor(magnitude * 4), 1, 5);
            Volcanism.StartImpactMelt(
                world,
                center,
                meltFlows,
                Math.Max(3, (int)MathF.Round(craterRadius * (0.45f + magnitude * 0.35f))));
        }

        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
        world.ImpactWaves.Add(new ImpactWaveActivity(
            center,
            shockRadius,
            0.65f + magnitude * 1.35f,
            magnitude));
        return new MeteorImpactResult(center, magnitude, craterRadius, shockRadius, fragmentCount);
    }

    internal static void Advance(SimulationWorld world)
    {
        for (var index = world.ImpactWaves.Count - 1; index >= 0; index--)
        {
            var wave = world.ImpactWaves[index];
            var previousRadius = wave.CurrentRadius;
            wave.CurrentRadius = Math.Min(wave.MaximumRadius, wave.CurrentRadius + wave.Speed);
            ApplyShockAnnulus(world, wave, previousRadius, wave.CurrentRadius);
            if (wave.CurrentRadius >= wave.MaximumRadius)
            {
                world.ImpactWaves.RemoveAt(index);
            }
        }
    }

    public static bool IsOnShockFront(
        SimulationWorld world,
        GridPosition position,
        ImpactWaveSnapshot wave,
        float thickness = 0.85f)
    {
        ArgumentNullException.ThrowIfNull(world);
        var distance = DistortedDistance(world, wave.Center, position);
        return Math.Abs(distance - wave.CurrentRadius) <= thickness;
    }

    private static void ApplyCrater(
        SimulationWorld world,
        GridPosition center,
        int radius,
        float magnitude)
    {
        var reach = Math.Max(radius + 1, (int)MathF.Ceiling(radius * 2.3f));
        var depth = 0.10f + magnitude * 0.55f;
        var rimHeight = 0.04f + magnitude * 0.20f;
        var complex = magnitude >= 0.5f;
        for (var offsetY = -reach; offsetY <= reach; offsetY++)
        {
            var y = center.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (var offsetX = -reach; offsetX <= reach; offsetX++)
            {
                var x = Mod(center.X + offsetX, world.Width);
                var position = new GridPosition(x, y);
                var distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
                var irregularity = 0.92f + HashUnit(world.Seed ^ 0xD6E8FEB86659FD93UL, x, y) * 0.16f;
                var normalized = distance / (radius * irregularity);
                if (normalized > 2.3f)
                {
                    continue;
                }

                var elevationChange = 0f;
                if (normalized <= 0.90f)
                {
                    var bowl = complex
                        ? 1f - SmoothStep(0.28f, 0.92f, normalized)
                        : 1f - normalized * normalized;
                    elevationChange -= depth * Math.Max(0, bowl);
                    if (complex)
                    {
                        elevationChange += MathF.Sin(normalized * MathF.PI * 10f) * 0.012f * magnitude;
                    }

                    if (magnitude >= 0.5f)
                    {
                        var peakRadius = 0.13f + magnitude * 0.11f;
                        if (normalized < peakRadius)
                        {
                            elevationChange += depth * 0.82f * (1 - normalized / peakRadius);
                        }
                    }

                    if (magnitude >= 0.85f)
                    {
                        var ring = 1 - Math.Abs(normalized - 0.48f) / 0.10f;
                        elevationChange += Math.Max(0, ring) * rimHeight * 0.55f;
                    }
                }
                else if (normalized <= 1.22f)
                {
                    var rim = 1 - Math.Abs(normalized - 1.05f) / 0.17f;
                    elevationChange += Math.Max(0, rim) * rimHeight;
                }
                else
                {
                    var ejectaVariation = 0.7f + HashUnit(world.Seed ^ 0xA0761D6478BD642FUL, x, y) * 0.6f;
                    elevationChange += rimHeight * 0.24f * ejectaVariation /
                        MathF.Pow(Math.Max(1, normalized), 3);
                }

                world.SetElevation(position, world.GetElevation(position) + elevationChange);
                if (normalized <= 1.12f)
                {
                    ClearImpactEntities(world, position);
                }

                var ejectaChance = 0.62f / MathF.Max(1, normalized);
                if (normalized <= 1.50f ||
                    HashUnit(world.Seed ^ 0xE7037ED1A0B428DBUL, x, y) < ejectaChance)
                {
                    var recoverySeconds = 18 + (int)MathF.Round(magnitude * 42 + normalized * 5);
                    world.SetSurfaceCover(
                        position,
                        SurfaceCover.Stone,
                        world.Tick + recoverySeconds * SimulationWorld.TicksPerSecond);
                }
            }
        }
    }

    private static void AddFragments(
        SimulationWorld world,
        GridPosition center,
        int craterRadius,
        int shockRadius,
        float magnitude,
        int fragmentCount)
    {
        if (fragmentCount <= 0)
        {
            return;
        }

        var directionIndex = world.NextInt(8);
        ReadOnlySpan<GridPosition> directions =
        [
            new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
            new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
        ];
        var direction = directions[directionIndex];
        var perpendicular = new GridPosition(-direction.Y, direction.X);
        for (var index = 0; index < fragmentCount; index++)
        {
            var distance = craterRadius * 2 + world.NextInt(Math.Max(1, shockRadius - craterRadius * 2 + 1));
            var lateral = world.NextInt(craterRadius * 2 + 1) - craterRadius;
            var y = center.Y + direction.Y * distance + perpendicular.Y * lateral;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            var x = Mod(center.X + direction.X * distance + perpendicular.X * lateral, world.Width);
            var fragmentRadius = Math.Max(1, (int)MathF.Round(
                craterRadius * (0.10f + world.NextUnitFloat() * 0.13f)));
            var fragmentMagnitude = Math.Clamp(
                0.04f + magnitude * (0.08f + world.NextUnitFloat() * 0.12f),
                0.04f,
                0.28f);
            ApplyCrater(world, new GridPosition(x, y), fragmentRadius, fragmentMagnitude);
        }
    }

    private static void ApplyShockAnnulus(
        SimulationWorld world,
        ImpactWaveActivity wave,
        float previousRadius,
        float currentRadius)
    {
        var reach = (int)MathF.Ceiling(currentRadius * 1.12f + 1);
        for (var offsetY = -reach; offsetY <= reach; offsetY++)
        {
            var y = wave.Center.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }
            for (var offsetX = -reach; offsetX <= reach; offsetX++)
            {
                var position = new GridPosition(Mod(wave.Center.X + offsetX, world.Width), y);
                var distance = DistortedDistance(world, wave.Center, position);
                if (distance <= previousRadius || distance > currentRadius)
                {
                    continue;
                }

                var power = 0.25f + wave.Magnitude * 0.75f;
                var normalized = distance / wave.MaximumRadius;
                var strength = power * Math.Max(0, 1 - normalized * normalized);
                if (strength >= 0.16f)
                {
                    world.RemoveCritterAt(position);
                }
            }
        }
    }

    private static void ClearImpactEntities(SimulationWorld world, GridPosition position)
    {
        world.RemoveCritterAt(position);
        Volcanism.RemoveVolcanoAt(world, position);
    }

    private static float DistortedDistance(
        SimulationWorld world,
        GridPosition center,
        GridPosition position)
    {
        var deltaX = Math.Abs(position.X - center.X);
        deltaX = Math.Min(deltaX, world.Width - deltaX);
        var deltaY = position.Y - center.Y;
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var distortion = 0.92f + HashUnit(
            world.Seed ^ 0x8EBC6AF09C88C6E3UL,
            position.X - center.X,
            position.Y - center.Y) * 0.16f;
        return distance / distortion;
    }

    private static float HashUnit(ulong seed, int x, int y)
    {
        var value = seed;
        value ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
        value ^= (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (value >> 40) * (1f / (1 << 24));
    }

    private static float SmoothStep(float minimum, float maximum, float value)
    {
        var amount = Math.Clamp((value - minimum) / (maximum - minimum), 0, 1);
        return amount * amount * (3 - 2 * amount);
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
