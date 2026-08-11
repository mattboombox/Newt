namespace Newt.Simulation;

/// <summary>Creates deterministic, horizontally wrapping terrarium worlds.</summary>
public static class WorldGenerator
{
    private static readonly GridPosition[] ChainDirections =
    [
        new(1, 0), new(1, 1), new(0, 1), new(-1, 1),
        new(-1, 0), new(-1, -1), new(0, -1), new(1, -1),
    ];

    public static SimulationWorld Generate(WorldGenerationOptions options)
    {
        if (options.LandFraction is < 0.15 or > 0.75)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Land fraction must be between 0.15 and 0.75.");
        }

        if (options.Preset == WorldPreset.Earth)
        {
            return GenerateEarth(options);
        }

        if (options.Preset == WorldPreset.Mars)
        {
            return GenerateMars(options);
        }

        if (options.Preset == WorldPreset.Moon)
        {
            return GenerateMoon(options);
        }

        var preset = options.Preset;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed);
        var elevation = new double[checked(preset.Width * preset.Height)];
        var random = new GeneratorRandom(options.Seed);
        var area = preset.Width * preset.Height;
        var volcanicChains = Math.Max(5, area / 1_800);

        for (var chain = 0; chain < volcanicChains; chain++)
        {
            AddVolcanicChain(elevation, preset.Width, preset.Height, ref random);
        }

        var threshold = FindLandThreshold(elevation, options.LandFraction);
        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                var position = new GridPosition(x, y);
                var height = elevation[y * preset.Width + x] - threshold;
                world.SetElevation(position, (float)height);
            }
        }

        CarveCentralOcean(world);

        TerrainClassifier.RebuildAll(world);
        SeedNaturalVolcanoes(world, options.Seed);
        StartNaturalSprings(world, options.Seed);
        return world;
    }

    private static SimulationWorld GenerateMars(WorldGenerationOptions options)
    {
        var preset = WorldPreset.Mars;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.Plains, options.Seed)
        {
            Body = WorldBody.Mars,
            HasOceans = false,
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            "Newt.Simulation.MarsElevation") ??
            throw new InvalidOperationException("The embedded Mars elevation grid is missing.");
        using var reader = new BinaryReader(stream);

        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), reader.ReadInt16() / 10_000f);
            }
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static SimulationWorld GenerateMoon(WorldGenerationOptions options)
    {
        var preset = WorldPreset.Moon;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.Plains, options.Seed)
        {
            Body = WorldBody.Moon,
            HasOceans = false,
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            "Newt.Simulation.MoonElevation") ??
            throw new InvalidOperationException("The embedded Moon elevation grid is missing.");
        using var reader = new BinaryReader(stream);

        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), reader.ReadInt16() / 6_000f);
            }
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static SimulationWorld GenerateEarth(WorldGenerationOptions options)
    {
        var preset = WorldPreset.Earth;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed)
        {
            Body = WorldBody.Earth,
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            "Newt.Simulation.EarthElevation") ??
            throw new InvalidOperationException("The embedded Earth elevation grid is missing.");
        using var reader = new BinaryReader(stream);

        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                var meters = reader.ReadInt16();
                var elevation = meters >= 0 ? meters / 5_000f : meters / 10_000f;
                world.SetElevation(new GridPosition(x, y), elevation);
            }
        }

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static void SeedNaturalVolcanoes(SimulationWorld world, ulong seed)
    {
        var candidates = new List<GridPosition>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (world.GetTerrain(position) is Terrain.Mountain && !world.IsOccupied(position))
                {
                    candidates.Add(position);
                }
            }
        }

        var random = new GeneratorRandom(seed ^ 0x94D049BB133111EBUL);
        Shuffle(candidates, ref random);
        var targetCount = Math.Clamp(world.Width * world.Height / 12_000 + 1, 1, 5);
        var minimumDistance = Math.Max(8, Math.Min(world.Width, world.Height) / 5);
        var selected = new List<GridPosition>(targetCount);
        foreach (var candidate in candidates)
        {
            if (selected.Any(existing => WrappedDistanceSquared(world, existing, candidate) <
                minimumDistance * minimumDistance))
            {
                continue;
            }

            if (Volcanism.SpawnVolcano(world, candidate))
            {
                selected.Add(candidate);
                if (selected.Count >= targetCount)
                {
                    break;
                }
            }
        }
    }

    private static void StartNaturalSprings(SimulationWorld world, ulong seed)
    {
        var candidates = new List<GridPosition>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (IsSnowmeltSource(world, position))
                {
                    candidates.Add(position);
                }
            }
        }

        var random = new GeneratorRandom(seed ^ 0xD1B54A32D192ED03UL);
        Shuffle(candidates, ref random);
        var targetCount = Math.Clamp(world.Width * world.Height / 5_000 + 1, 2, 6);
        var minimumDistance = Math.Max(6, Math.Min(world.Width, world.Height) / 8);
        var selected = new List<GridPosition>(targetCount);

        foreach (var candidate in candidates)
        {
            if (selected.Any(source => WrappedDistanceSquared(world, source, candidate) <
                minimumDistance * minimumDistance))
            {
                continue;
            }

            if (Hydrology.StartSpring(world, candidate).Termination is SpringTermination.Flowing)
            {
                selected.Add(candidate);
                if (selected.Count >= targetCount)
                {
                    break;
                }
            }
        }
    }

    private static bool IsSnowmeltSource(SimulationWorld world, GridPosition position)
    {
        if (world.GetTerrain(position) is not Terrain.Mountain ||
            world.GetSurfaceCover(position) is not SurfaceCover.None ||
            world.GetBiome(position) is Biome.Arctic)
        {
            return false;
        }

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var y = position.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var x = Mod(position.X + offsetX, world.Width);
                var neighbor = new GridPosition(x, y);
                if (world.GetTerrain(neighbor) is Terrain.Mountain &&
                    world.GetBiome(neighbor) is Biome.Arctic)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int WrappedDistanceSquared(
        SimulationWorld world,
        GridPosition first,
        GridPosition second)
    {
        var distanceX = Math.Abs(first.X - second.X);
        distanceX = Math.Min(distanceX, world.Width - distanceX);
        var distanceY = first.Y - second.Y;
        return distanceX * distanceX + distanceY * distanceY;
    }

    private static void Shuffle<T>(IList<T> values, ref GeneratorRandom random)
    {
        for (var index = values.Count - 1; index > 0; index--)
        {
            var replacement = random.NextInt(index + 1);
            (values[index], values[replacement]) = (values[replacement], values[index]);
        }
    }

    private static void AddVolcanicChain(
        double[] elevation,
        int width,
        int height,
        ref GeneratorRandom random)
    {
        var x = random.NextInt(width);
        var y = random.NextInt(height);
        var directionIndex = random.NextInt(ChainDirections.Length);
        var length = random.NextInt(8) + 4;
        for (var step = 0; step < length; step++)
        {
            // Very wide worlds need more shields, not horizontally stretched shields.
            // The height cap preserves the existing proportions of ordinary presets.
            var maximumRadiusX = Math.Min(
                Math.Max(3, width * 0.055),
                Math.Max(3, height * 0.11));
            var radiusX = random.NextDouble(
                Math.Min(Math.Max(2, width * 0.018), maximumRadiusX),
                maximumRadiusX);
            var radiusY = random.NextDouble(Math.Max(2, height * 0.025), Math.Max(3, height * 0.075));
            var strength = random.NextDouble(0.28, 0.72);
            AddVolcanicShield(elevation, width, height, x, y, radiusX, radiusY, strength);

            var roll = random.NextInt(100);
            if (roll < 18)
            {
                directionIndex = Mod(directionIndex - 1, ChainDirections.Length);
            }
            else if (roll >= 82)
            {
                directionIndex = Mod(directionIndex + 1, ChainDirections.Length);
            }

            var direction = ChainDirections[directionIndex];
            x = Mod(x + direction.X * (random.NextInt(3) + 1), width);
            y += direction.Y * (random.NextInt(2) + 1);
            if (y < 0 || y >= height)
            {
                break;
            }
        }
    }

    private static void AddVolcanicShield(
        double[] elevation,
        int width,
        int height,
        int centerX,
        int centerY,
        double radiusX,
        double radiusY,
        double strength)
    {
        for (var y = 0; y < height; y++)
        {
            var normalizedY = (y - centerY) / radiusY;
            for (var x = 0; x < width; x++)
            {
                var distanceX = Math.Abs(x - centerX);
                distanceX = Math.Min(distanceX, width - distanceX);
                var normalizedX = distanceX / radiusX;
                var distanceSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distanceSquared >= 1)
                {
                    continue;
                }

                var influence = 1 - distanceSquared;
                elevation[y * width + x] += influence * influence * strength;
            }
        }
    }

    private static void CarveCentralOcean(SimulationWorld world)
    {
        var center = world.OceanSeed;
        var radiusY = Math.Max(2, world.Height / 18);
        var radiusX = Math.Min(
            Math.Max(2, world.Width / 25),
            Math.Max(2, world.Height / 9));
        for (var offsetY = -radiusY; offsetY <= radiusY; offsetY++)
        {
            var y = center.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (var offsetX = -radiusX; offsetX <= radiusX; offsetX++)
            {
                var distance = offsetX * offsetX / (float)(radiusX * radiusX) +
                    offsetY * offsetY / (float)(radiusY * radiusY);
                if (distance > 1)
                {
                    continue;
                }

                var position = new GridPosition(Mod(center.X + offsetX, world.Width), y);
                var target = -0.06f - (1 - distance) * 0.28f;
                world.SetElevation(position, Math.Min(world.GetElevation(position), target));
            }
        }
    }

    private static double FindLandThreshold(double[] elevation, double landFraction)
    {
        var ordered = (double[])elevation.Clone();
        Array.Sort(ordered);
        var index = (int)Math.Clamp(
            Math.Floor(ordered.Length * (1 - landFraction)),
            0,
            ordered.Length - 1);
        return ordered[index];
    }

    private struct GeneratorRandom
    {
        private ulong _state;

        public GeneratorRandom(ulong seed) => _state = seed == 0 ? 1 : seed;

        public int NextInt(int exclusiveMaximum)
        {
            Advance();
            return (int)(_state % (uint)exclusiveMaximum);
        }

        public double NextDouble(double minimum, double maximum)
        {
            Advance();
            var unit = (_state >> 11) * (1.0 / (1UL << 53));
            return minimum + (maximum - minimum) * unit;
        }

        private void Advance()
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
        }
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
