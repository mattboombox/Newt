namespace Newt.Simulation;

/// <summary>Meteor crater excavation, ejecta, fragmentation, melt, and shockwaves.</summary>
public static class Impacts
{
    private const float MinimumMagnitude = 0f;
    private const float MaximumMagnitude = 1f;

    /// <summary>A single-tile rifle strike without fragments, lava, or a shockwave.</summary>
    public static MeteorImpactResult CreateRifleImpact(SimulationWorld world, GridPosition center)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center)) throw new ArgumentOutOfRangeException(nameof(center));
        world.SetElevation(center, world.GetElevation(center) - 0.04f);
        ClearImpactEntities(world, center);
        world.SetSurfaceCover(center, SurfaceCover.Stone, world.Tick + 18 * SimulationWorld.TicksPerSecond);
        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
        return new MeteorImpactResult(center, 0, 0, 0, 0);
    }

    /// <summary>A missile blast confined to the target's immediate surface neighborhood.</summary>
    public static MeteorImpactResult CreateMissileImpact(SimulationWorld world, GridPosition center) =>
        CreateMeteorImpact(world, center, 0, 2f, rifleSized: true);

    public static MeteorImpactResult CreateMeteorImpact(
        SimulationWorld world,
        GridPosition center,
        float magnitude) => CreateMeteorImpact(world, center, magnitude, MaximumMagnitude);

    /// <summary>Player tool impacts can reach twice the normal maximum crater radius.</summary>
    public static MeteorImpactResult CreateToolMeteorImpact(
        SimulationWorld world,
        GridPosition center,
        float magnitude) => CreateMeteorImpact(world, center, magnitude, 2f);

    private static MeteorImpactResult CreateMeteorImpact(
        SimulationWorld world,
        GridPosition center,
        float magnitude,
        float maximumMagnitude,
        bool rifleSized = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
        if (!float.IsFinite(magnitude) || magnitude < MinimumMagnitude || magnitude > maximumMagnitude)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }

        var requestedMagnitude = magnitude;
        var sizeMultiplier = Math.Max(1f, magnitude);
        magnitude = Math.Min(magnitude, MaximumMagnitude);
        var minimumDimension = Math.Min(world.Width, world.Height);
        var craterRadius = 2 + (int)MathF.Round(minimumDimension * 0.18f * MathF.Pow(magnitude, 1.7f));
        craterRadius = (int)MathF.Round(craterRadius * sizeMultiplier);
        if (rifleSized) craterRadius = 1;
        var shockRadius = rifleSized ? 1 : craterRadius * 3;
        ApplyCrater(world, center, craterRadius, magnitude, rifleSized);

        var fragmentCount = (int)MathF.Round(magnitude * 10f);
        if (magnitude >= 0.2f)
        {
            fragmentCount += world.NextInt(3);
        }
        AddFragments(world, center, craterRadius, shockRadius, magnitude, fragmentCount);

        if (magnitude >= 0.12f)
        {
            var meltFlows = Math.Clamp(1 + (int)MathF.Floor(magnitude * 4), 1, 5);
            // Extended tool impacts spread melt across a larger basin as well as farther outward.
            meltFlows = (int)MathF.Round(meltFlows * sizeMultiplier);
            Volcanism.StartImpactMelt(
                world,
                center,
                meltFlows,
                Math.Max(3, (int)MathF.Round(craterRadius * (0.45f + magnitude * 0.35f))));
        }

        TerrainClassifier.RebuildLandforms(world);
        Hydrology.RebuildFreshwater(world);
        var wave = new ImpactWaveActivity(
            center,
            shockRadius,
            (0.65f + magnitude * 1.35f) * (rifleSized ? 0.16f : 1),
            magnitude);
        if (rifleSized)
        {
            wave.LimitedTiles = [];
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var tile = world.OffsetTile(center, new(dx, dy));
                if (world.Contains(tile)) wave.LimitedTiles.Add(tile);
            }
        }
        world.ImpactWaves.Add(wave);
        return new MeteorImpactResult(center, requestedMagnitude, craterRadius, shockRadius, fragmentCount);
    }

    internal static void Advance(SimulationWorld world)
    {
        for (var index = world.ImpactWaves.Count - 1; index >= 0; index--)
        {
            var wave = world.ImpactWaves[index];
            var previousRadius = wave.CurrentRadius;
            wave.CurrentRadius = Math.Min(wave.MaximumRadius, wave.CurrentRadius + wave.Speed);
            if (wave.Kind is WaveKind.Tsunami)
            {
                ApplyTsunamiAnnulus(world, wave, previousRadius, wave.CurrentRadius);
            }
            else
            {
                ApplyShockAnnulus(world, wave, previousRadius, wave.CurrentRadius);
            }
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
        if (wave.Kind is WaveKind.Impact && wave.MaximumRadius <= 1)
        {
            var nearby = false;
            for (var dy = -1; dy <= 1 && !nearby; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (world.OffsetTile(wave.Center, new(dx, dy)) == position) { nearby = true; break; }
            if (!nearby) return false;
        }
        var distance = DistortedDistance(world, wave.Center, position);
        return Math.Abs(distance - wave.CurrentRadius) <= thickness;
    }

    private static void ApplyCrater(
        SimulationWorld world,
        GridPosition center,
        int radius,
        float magnitude,
        bool rifleSized = false)
    {
        var reach = rifleSized ? 1 : Math.Max(radius + 1, (int)MathF.Ceiling(radius * 2.3f));
        var depth = 0.10f + magnitude * 0.55f;
        var rimHeight = 0.04f + magnitude * 0.20f;
        var complex = magnitude >= 0.5f;
        var planetary = world.Body is WorldBody.Planetoid && !rifleSized;
        var originDirection = planetary ? world.GetPlanetoidTileDirection(center) : default;
        var unitsPerRadian = planetary ? PlanetoidWaveUnitsPerRadian(world) : 0;
        // Atlas neighbors need not be physical neighbors. Visit each spherical
        // tile once and measure the crater along the surface across face seams.
        for (var offsetY = planetary ? -center.Y : -reach;
            offsetY <= (planetary ? world.Height - 1 - center.Y : reach); offsetY++)
        {
            var y = center.Y + offsetY;
            if (!rifleSized && (y < 0 || y >= world.Height))
            {
                continue;
            }

            for (var offsetX = planetary ? -center.X : -reach;
                offsetX <= (planetary ? world.Width - 1 - center.X : reach); offsetX++)
            {
                var x = Mod(center.X + offsetX, world.Width);
                var position = rifleSized ? world.OffsetTile(center, new(offsetX, offsetY)) : new GridPosition(x, y);
                if (!world.Contains(position)) continue;
                var distance = planetary
                    ? position == center ? 0 : MathF.Acos(Math.Clamp(System.Numerics.Vector3.Dot(
                        originDirection, world.GetPlanetoidTileDirection(position)), -1f, 1f)) * unitsPerRadian
                    : MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
                var irregularity = 0.92f + HashUnit(world.Seed ^ 0xD6E8FEB86659FD93UL, position.X, position.Y) * 0.16f;
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
            if (world.Body is not WorldBody.Planetoid && (y < 0 || y >= world.Height))
            {
                continue;
            }

            var x = Mod(center.X + direction.X * distance + perpendicular.X * lateral, world.Width);
            var fragmentCenter = world.Body is WorldBody.Planetoid
                ? OffsetOnPlanetoid(world, center, direction.X * distance + perpendicular.X * lateral,
                    direction.Y * distance + perpendicular.Y * lateral)
                : new GridPosition(x, y);
            var fragmentRadius = Math.Max(1, (int)MathF.Round(
                craterRadius * (0.10f + world.NextUnitFloat() * 0.13f)));
            var fragmentMagnitude = Math.Clamp(
                0.04f + magnitude * (0.08f + world.NextUnitFloat() * 0.12f),
                0.04f,
                0.28f);
            ApplyCrater(world, fragmentCenter, fragmentRadius, fragmentMagnitude);
        }
    }

    private static GridPosition OffsetOnPlanetoid(SimulationWorld world, GridPosition center, int dx, int dy)
    {
        var size = PlanetoidLayout.FaceSize(world.Width);
        var origin = world.GetPlanetoidTileDirection(center);
        var faceEast = PlanetoidLayout.East(PlanetoidLayout.FaceAt(center.X, center.Y, size));
        var east = System.Numerics.Vector3.Normalize(faceEast - origin * System.Numerics.Vector3.Dot(faceEast, origin));
        var south = System.Numerics.Vector3.Cross(east, origin);
        var tangent = east * dx + south * dy;
        var length = tangent.Length();
        if (length == 0) return center;
        var angle = length / PlanetoidWaveUnitsPerRadian(world);
        var direction = origin * MathF.Cos(angle) + tangent / length * MathF.Sin(angle);
        var atlas = PlanetoidLayout.AtlasPosition(direction, size);
        return new((int)atlas.X, (int)atlas.Y);
    }

    private static void ApplyShockAnnulus(
        SimulationWorld world,
        ImpactWaveActivity wave,
        float previousRadius,
        float currentRadius)
    {
        foreach (var (position, distance) in WaveAnnulus(world, wave, previousRadius, currentRadius))
        {
            var power = 0.25f + wave.Magnitude * 0.75f;
            var normalized = distance / wave.MaximumRadius;
            var strength = power * Math.Max(0, 1 - normalized * normalized);
            if (strength >= 0.16f)
                RemoveCrittersHitByImpact(world, position);
        }
    }

    private static void ApplyTsunamiAnnulus(
        SimulationWorld world,
        ImpactWaveActivity wave,
        float previousRadius,
        float currentRadius)
    {
        var changed = false;
        foreach (var (position, distance) in WaveAnnulus(world, wave, previousRadius, currentRadius))
        {
            if (world.GetTerrain(position) is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice)
                continue;

            var remainingStrength = Math.Max(0, 1 - distance / wave.MaximumRadius);
            var erosion = (0.012f + wave.Magnitude * 0.055f) * remainingStrength;
            if (erosion <= 0)
                continue;

            world.SetElevation(position, world.GetElevation(position) - erosion);
            RemoveCrittersHitByImpact(world, position);
            world.RemoveWolfDenAt(position);
            world.RemoveMegaSpiderWebAt(position);
            world.RemoveTeleporterAt(position);
            changed = true;
        }

        if (changed)
        {
            TerrainClassifier.RebuildLandforms(world);
            Hydrology.RebuildFreshwater(world);
        }
    }

    private static IEnumerable<(GridPosition Position, float Distance)> WaveAnnulus(
        SimulationWorld world, ImpactWaveActivity wave, float previousRadius, float currentRadius)
    {
        if (wave.LimitedTiles is { } tiles)
        {
            foreach (var tile in tiles)
            {
                var distance = DistortedDistance(world, wave.Center, tile);
                if (distance > previousRadius && distance <= currentRadius) yield return (tile, distance);
            }
            yield break;
        }
        if (world.Body is WorldBody.Planetoid)
        {
            var front = wave.PlanetoidFront ??= BuildPlanetoidFront(world, wave);
            while (wave.NextPlanetoidTile < front.Count && front[wave.NextPlanetoidTile].Distance <= currentRadius)
                yield return front[wave.NextPlanetoidTile++];
            yield break;
        }

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
                if (distance > previousRadius && distance <= currentRadius)
                    yield return (position, distance);
            }
        }
    }

    private static List<(GridPosition Position, float Distance)> BuildPlanetoidFront(
        SimulationWorld world, ImpactWaveActivity wave)
    {
        // Prepare the front once, then process only newly reached tiles on each tick.
        var front = new List<(GridPosition Position, float Distance)>();
        var origin = world.GetPlanetoidTileDirection(wave.Center);
        var tileUnitsPerRadian = PlanetoidWaveUnitsPerRadian(world);
        var minimumDot = MathF.Cos(Math.Min(MathF.PI, wave.MaximumRadius / tileUnitsPerRadian));
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, y);
            if (position == wave.Center)
                continue;
            var dot = System.Numerics.Vector3.Dot(origin, world.GetPlanetoidTileDirection(position));
            if (dot >= minimumDot)
                front.Add((position, MathF.Acos(Math.Clamp(dot, -1f, 1f)) * tileUnitsPerRadian));
        }
        front.Sort(static (first, second) => first.Distance.CompareTo(second.Distance));
        return front;
    }

    private static void ClearImpactEntities(SimulationWorld world, GridPosition position)
    {
        RemoveCrittersHitByImpact(world, position);
        world.RemoveWolfDenAt(position);
        world.RemoveMegaSpiderWebAt(position);
        world.RemoveTeleporterAt(position);
        Volcanism.RemoveVolcanoAt(world, position);
    }

    private static void RemoveCrittersHitByImpact(SimulationWorld world, GridPosition position)
    {
        world.KillSandwormAt(position);
        world.RemoveCritterAt(position);
    }

    private static float DistortedDistance(
        SimulationWorld world,
        GridPosition center,
        GridPosition position)
    {
        if (world.Body is WorldBody.Planetoid)
        {
            if (position == center)
                return 0f;
            var dot = System.Numerics.Vector3.Dot(
                world.GetPlanetoidTileDirection(center), world.GetPlanetoidTileDirection(position));
            return MathF.Acos(Math.Clamp(dot, -1f, 1f)) * PlanetoidWaveUnitsPerRadian(world);
        }

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

    private static float PlanetoidWaveUnitsPerRadian(SimulationWorld world) =>
        PlanetoidLayout.FaceSize(world.Width) / (MathF.PI / 2);

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
