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

        var preset = options.Preset;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed);
        var elevation = new double[checked(preset.Width * preset.Height)];
        var random = new GeneratorRandom(options.Seed);
        var area = preset.Width * preset.Height;
        var continentalMasses = Math.Clamp(area / 10_000 + 2, 2, 5);
        var volcanicChains = Math.Clamp(area / 7_000 + 2, 2, 6);

        for (var continent = 0; continent < continentalMasses; continent++)
        {
            AddContinentalMass(elevation, preset.Width, preset.Height, ref random);
        }

        for (var chain = 0; chain < volcanicChains; chain++)
        {
            AddVolcanicChain(elevation, preset.Width, preset.Height, ref random);
        }

        AddElevationNoise(elevation, preset.Width, preset.Height, options.Seed);

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

        SelectMainOceanSeed(world);

        TerrainClassifier.RebuildAll(world);
        SeedNaturalVolcanoes(world, options.Seed);
        StartNaturalSprings(world, options.Seed);
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
                if (Hydrology.IsSnowmeltSource(world, position))
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

            if (Hydrology.StartSnowmeltSpring(world, candidate).Termination is SpringTermination.Flowing)
            {
                selected.Add(candidate);
                if (selected.Count >= targetCount)
                {
                    break;
                }
            }
        }
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
                Math.Max(2.5, width * 0.038),
                Math.Max(2.5, height * 0.078));
            var radiusX = random.NextDouble(
                Math.Min(Math.Max(1.75, width * 0.012), maximumRadiusX),
                maximumRadiusX);
            var radiusY = random.NextDouble(Math.Max(1.75, height * 0.018), Math.Max(2.5, height * 0.052));
            var strength = random.NextDouble(0.42, 0.86);
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

    private static void AddContinentalMass(
        double[] elevation,
        int width,
        int height,
        ref GeneratorRandom random)
    {
        var centerX = random.NextInt(width);
        var centerY = random.NextInt(height);
        var maximumRadiusX = Math.Min(width * 0.30, height * 1.35);
        var baseRadiusX = random.NextDouble(
            Math.Min(width * 0.14, maximumRadiusX),
            Math.Max(Math.Min(width * 0.14, maximumRadiusX) + 0.01, maximumRadiusX));
        var baseRadiusY = random.NextDouble(height * 0.14, height * 0.28);
        var lobes = random.NextInt(4) + 4;

        AddVolcanicShield(
            elevation,
            width,
            height,
            centerX,
            centerY,
            baseRadiusX,
            baseRadiusY,
            random.NextDouble(0.38, 0.58));

        for (var lobe = 0; lobe < lobes; lobe++)
        {
            var direction = ChainDirections[random.NextInt(ChainDirections.Length)];
            var offsetX = direction.X * (int)random.NextDouble(baseRadiusX * 0.18, baseRadiusX * 0.72);
            var offsetY = direction.Y * (int)random.NextDouble(baseRadiusY * 0.18, baseRadiusY * 0.72);
            AddVolcanicShield(
                elevation,
                width,
                height,
                Mod(centerX + offsetX, width),
                Math.Clamp(centerY + offsetY, 0, height - 1),
                random.NextDouble(baseRadiusX * 0.38, baseRadiusX * 0.78),
                random.NextDouble(baseRadiusY * 0.38, baseRadiusY * 0.82),
                random.NextDouble(0.24, 0.48));
        }
    }

    private static void AddElevationNoise(
        double[] elevation,
        int width,
        int height,
        ulong seed)
    {
        var scales = new (int CellSize, double Amplitude, ulong Salt)[]
        {
            (Math.Max(8, Math.Min(width, height) / 3), 0.18, 0x9E3779B185EBCA87UL),
            (Math.Max(5, Math.Min(width, height) / 7), 0.095, 0xC2B2AE3D27D4EB4FUL),
            (Math.Max(3, Math.Min(width, height) / 15), 0.045, 0xD6E8FEB86659FD93UL),
        };

        foreach (var (cellSize, amplitude, salt) in scales)
        {
            var cellsX = Math.Max(2, (int)Math.Ceiling(width / (double)cellSize));
            var cellsY = Math.Max(2, (int)Math.Ceiling(height / (double)cellSize));
            for (var y = 0; y < height; y++)
            {
                var sampleY = y / (double)height * cellsY;
                var y0 = Math.Min((int)Math.Floor(sampleY), cellsY - 1);
                var y1 = Math.Min(y0 + 1, cellsY - 1);
                var blendY = Smooth(sampleY - Math.Floor(sampleY));
                for (var x = 0; x < width; x++)
                {
                    var sampleX = x / (double)width * cellsX;
                    var x0 = (int)Math.Floor(sampleX) % cellsX;
                    var x1 = (x0 + 1) % cellsX;
                    var blendX = Smooth(sampleX - Math.Floor(sampleX));
                    var north = Lerp(
                        HashNoise(seed ^ salt, x0, y0),
                        HashNoise(seed ^ salt, x1, y0),
                        blendX);
                    var south = Lerp(
                        HashNoise(seed ^ salt, x0, y1),
                        HashNoise(seed ^ salt, x1, y1),
                        blendX);
                    elevation[y * width + x] += (Lerp(north, south, blendY) - 0.5) * 2 * amplitude;
                }
            }
        }
    }

    private static double HashNoise(ulong seed, int x, int y)
    {
        var value = seed;
        value ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
        value ^= (ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (value >> 11) * (1.0 / (1UL << 53));
    }

    private static double Smooth(double value) => value * value * (3 - 2 * value);

    private static double Lerp(double first, double second, double amount) =>
        first + (second - first) * amount;

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

    private static void SelectMainOceanSeed(SimulationWorld world)
    {
        var visited = new bool[checked(world.Width * world.Height)];
        var largestSize = 0;
        var selected = world.OceanSeed;
        var directions = new GridPosition[]
        {
            new(1, 0), new(-1, 0), new(0, 1), new(0, -1),
        };

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var start = new GridPosition(x, y);
                var startIndex = y * world.Width + x;
                if (visited[startIndex] || world.GetElevation(start) > world.SeaLevel)
                {
                    continue;
                }

                var queue = new Queue<GridPosition>();
                queue.Enqueue(start);
                visited[startIndex] = true;
                var size = 0;
                var deepest = start;
                var deepestElevation = world.GetElevation(start);

                while (queue.TryDequeue(out var current))
                {
                    size++;
                    var elevation = world.GetElevation(current);
                    if (elevation < deepestElevation)
                    {
                        deepest = current;
                        deepestElevation = elevation;
                    }

                    foreach (var direction in directions)
                    {
                        var neighborY = current.Y + direction.Y;
                        if (neighborY < 0 || neighborY >= world.Height)
                        {
                            continue;
                        }

                        var neighborX = Mod(current.X + direction.X, world.Width);
                        var neighbor = new GridPosition(neighborX, neighborY);
                        var neighborIndex = neighborY * world.Width + neighborX;
                        if (visited[neighborIndex] ||
                            world.GetElevation(neighbor) > world.SeaLevel)
                        {
                            continue;
                        }

                        visited[neighborIndex] = true;
                        queue.Enqueue(neighbor);
                    }
                }

                if (size > largestSize)
                {
                    largestSize = size;
                    selected = deepest;
                }
            }
        }

        world.OceanSeed = selected;
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
