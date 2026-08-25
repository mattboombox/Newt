namespace Newt.Simulation;

/// <summary>Creates deterministic, horizontally wrapping terrarium worlds.</summary>
public static class WorldGenerator
{
    private const int EarthSourceWidth = 1280;
    private const int EarthSourceHeight = 642;
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

        if (options.MapType is WorldMapType.Earth || options.Preset == WorldPreset.Earth)
        {
            return GenerateEarth(options);
        }

        var preset = options.Preset;
        var isRingWorld = options.MapType is WorldMapType.RingWorld || preset == WorldPreset.Ring;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed)
        {
            Body = isRingWorld ? WorldBody.RingWorld : WorldBody.Terrarium,
            SeasonsEnabled = !isRingWorld,
        };
        var elevation = new double[checked(preset.Width * preset.Height)];
        var random = new GeneratorRandom(options.Seed);
        if (options.MapType is WorldMapType.AllOcean)
        {
            AddElevationNoise(elevation, preset.Width, preset.Height, options.Seed);
            for (var y = 0; y < preset.Height; y++)
            {
                for (var x = 0; x < preset.Width; x++)
                {
                    var position = new GridPosition(x, y);
                    // Keep every tile submerged while retaining readable ocean
                    // basins, ridges, and temperature-driven polar sea ice.
                    world.SetElevation(position, (float)Math.Clamp(
                        elevation[y * preset.Width + x] - 0.42,
                        -0.85,
                        -0.04));
                }
            }

            SelectOceanSeeds(world, includeLargeSecondaryBasins: false);
            TerrainClassifier.RebuildAll(world);
            return world;
        }

        var area = preset.Width * preset.Height;
        // Extra map area buys more local features instead of scaling up a small
        // fixed set of continents and ranges.
        var defaultMasses = Math.Clamp(area / 10_000 + 2, 2, 24);
        var continentalMasses = options.MapType switch
        {
            WorldMapType.Pangaea => 0,
            WorldMapType.Archipelago => Math.Min(72, defaultMasses * 3),
            _ => defaultMasses,
        };
        var volcanicChains = options.MapType switch
        {
            WorldMapType.Pangaea => Math.Clamp(area / 15_000 + 3, 3, 18),
            WorldMapType.Archipelago => Math.Min(40, Math.Max(3, continentalMasses / 2)),
            _ => Math.Clamp(area / 7_000 + 2, 2, 24),
        };
        var continentScale = options.MapType switch
        {
            WorldMapType.Archipelago => 0.28,
            _ => 1.0,
        };

        if (options.MapType is WorldMapType.Pangaea)
        {
            AddPangaea(elevation, preset.Width, preset.Height, ref random);
        }

        for (var continent = 0; continent < continentalMasses; continent++)
        {
            AddContinentalMass(elevation, preset.Width, preset.Height, ref random, continentScale);
        }

        var continentalFoundation = (double[])elevation.Clone();
        for (var chain = 0; chain < volcanicChains; chain++)
        {
            AddVolcanicChain(
                elevation,
                continentalFoundation,
                preset.Width,
                preset.Height,
                ref random);
        }

        AddElevationNoise(elevation, preset.Width, preset.Height, options.Seed);

        var landFraction = options.MapType switch
        {
            WorldMapType.Pangaea when options.LandFraction == 0.38 => 0.48,
            WorldMapType.Archipelago when options.LandFraction == 0.38 => 0.22,
            _ => options.LandFraction,
        };
        var threshold = FindLandThreshold(elevation, landFraction);
        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                var position = new GridPosition(x, y);
                var height = elevation[y * preset.Width + x] - threshold;
                world.SetElevation(position, (float)height);
            }
        }

        TerrainClassifier.ApplyRingWorldWalls(world);
        SelectOceanSeeds(world, includeLargeSecondaryBasins: true);

        TerrainClassifier.RebuildAll(world);
        SeedRingWorldTeleporters(world);
        SeedNaturalVolcanoes(world, options.Seed);
        StartNaturalSprings(world, options.Seed);
        return world;
    }

    private static SimulationWorld GenerateEarth(WorldGenerationOptions options)
    {
        var preset = options.Preset == WorldPreset.Earth ? WorldPreset.Huge : options.Preset;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed)
        {
            Body = WorldBody.Earth,
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            "Newt.Simulation.EarthElevation") ??
            throw new InvalidOperationException("The embedded Earth elevation grid is missing.");
        using var reader = new BinaryReader(stream);
        var source = new short[EarthSourceWidth * EarthSourceHeight];
        for (var index = 0; index < source.Length; index++)
        {
            source[index] = reader.ReadInt16();
        }

        for (var y = 0; y < preset.Height; y++)
        {
            for (var x = 0; x < preset.Width; x++)
            {
                var sourceX = Math.Min(EarthSourceWidth - 1,
                    (int)((x + 0.5) * EarthSourceWidth / preset.Width));
                var sourceY = Math.Min(EarthSourceHeight - 1,
                    (int)((y + 0.5) * EarthSourceHeight / preset.Height));
                var meters = source[sourceY * EarthSourceWidth + sourceX];
                var elevation = meters >= 0 ? meters / 5_000f : meters / 10_000f;
                world.SetElevation(new GridPosition(x, y), elevation);
            }
        }

        OpenEarthSeaPassage(world, westLongitude: -6.1, eastLongitude: -4.7, latitude: 35.9);
        TerrainClassifier.RebuildAll(world);
        StartNaturalSprings(world, options.Seed);
        return world;
    }

    /// <summary>
    /// Keeps a real narrow sea passage open when its width falls below one map
    /// cell and nearest-cell relief sampling would otherwise select both shores.
    /// </summary>
    private static void OpenEarthSeaPassage(
        SimulationWorld world,
        double westLongitude,
        double eastLongitude,
        double latitude)
    {
        var startX = LongitudeToWorldX(world, westLongitude);
        var endX = LongitudeToWorldX(world, eastLongitude);
        var centerY = LatitudeToWorldY(world, latitude);
        var halfWidth = world.Height >= 500 ? 1 : 0;
        for (var x = startX; x <= endX; x++)
        {
            for (var offsetY = -halfWidth; offsetY <= halfWidth; offsetY++)
            {
                var y = Math.Clamp(centerY + offsetY, 0, world.Height - 1);
                world.SetElevation(new GridPosition(x, y), -0.02f);
            }
        }
    }

    private static int LongitudeToWorldX(SimulationWorld world, double longitude) =>
        Math.Clamp((int)Math.Floor((longitude + 180) / 360 * world.Width), 0, world.Width - 1);

    private static int LatitudeToWorldY(SimulationWorld world, double latitude) =>
        Math.Clamp((int)Math.Floor((90 - latitude) / 180 * world.Height), 0, world.Height - 1);

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

    private static void SeedRingWorldTeleporters(SimulationWorld world)
    {
        if (world.Body is not WorldBody.RingWorld)
        {
            return;
        }

        const int teleporterCount = 4;
        var y = world.Height / 2;
        for (var index = 0; index < teleporterCount; index++)
        {
            var x = world.Width * (index * 2 + 1) / (teleporterCount * 2);
            world.TryPlaceTeleporter(new GridPosition(x, y));
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
        var targetCount = Math.Clamp(world.Width * world.Height / 2_500 + 1, 3, 12);
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
        double[] continentalFoundation,
        int width,
        int height,
        ref GeneratorRandom random)
    {
        var x = 0;
        var y = 0;
        var foundFoundation = false;
        for (var attempt = 0; attempt < 128; attempt++)
        {
            x = random.NextInt(width);
            y = random.NextInt(height);
            if (continentalFoundation[y * width + x] >= 0.12)
            {
                foundFoundation = true;
                break;
            }
        }

        if (!foundFoundation)
        {
            return;
        }

        var directionIndex = random.NextInt(ChainDirections.Length);
        var length = random.NextInt(8) + 4;
        for (var step = 0; step < length; step++)
        {
            if (continentalFoundation[y * width + x] < 0.04)
            {
                break;
            }

            // Very wide worlds need more shields, not horizontally stretched shields.
            // The height cap preserves the existing proportions of ordinary presets.
            var featureWidth = Math.Min(width, WorldPreset.Small.Width);
            var featureHeight = Math.Min(height, WorldPreset.Small.Height);
            var maximumRadiusX = Math.Min(
                Math.Max(2.5, featureWidth * 0.038),
                Math.Max(2.5, featureHeight * 0.078));
            var radiusX = random.NextDouble(
                Math.Min(Math.Max(1.75, featureWidth * 0.012), maximumRadiusX),
                maximumRadiusX);
            var radiusY = random.NextDouble(
                Math.Max(1.75, featureHeight * 0.018),
                Math.Max(2.5, featureHeight * 0.052));
            var strength = random.NextDouble(0.42, 0.86);
            AddVolcanicShield(
                elevation,
                width,
                height,
                x,
                y,
                radiusX,
                radiusY,
                strength,
                summitBoost: strength * 0.55,
                foundation: continentalFoundation);

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
        ref GeneratorRandom random,
        double scale)
    {
        var centerX = random.NextInt(width);
        var centerY = random.NextInt(height);
        var featureWidth = Math.Min(width, WorldPreset.Small.Width);
        var featureHeight = Math.Min(height, WorldPreset.Small.Height);
        var maximumRadiusX = Math.Min(featureWidth * 0.30, featureHeight * 1.35) * scale;
        var baseRadiusX = random.NextDouble(
            Math.Min(featureWidth * 0.14 * scale, maximumRadiusX),
            Math.Max(Math.Min(featureWidth * 0.14 * scale, maximumRadiusX) + 0.01, maximumRadiusX));
        var baseRadiusY = random.NextDouble(featureHeight * 0.14 * scale, featureHeight * 0.28 * scale);
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

    private static void AddPangaea(
        double[] elevation,
        int width,
        int height,
        ref GeneratorRandom random)
    {
        var centerX = width / 2 + random.NextInt(Math.Max(1, width / 10)) - width / 20;
        var centerY = height / 2 + random.NextInt(Math.Max(1, height / 10)) - height / 20;
        var radiusX = width * 0.31;
        var radiusY = height * 0.39;
        AddVolcanicShield(elevation, width, height, centerX, centerY, radiusX, radiusY, 0.72);

        // Large overlapping shoulders keep the supercontinent connected while
        // giving it deep gulfs, peninsulas, and an irregular outline.
        for (var lobe = 0; lobe < 14; lobe++)
        {
            var direction = ChainDirections[random.NextInt(ChainDirections.Length)];
            var offsetX = direction.X * (int)random.NextDouble(radiusX * 0.18, radiusX * 0.62);
            var offsetY = direction.Y * (int)random.NextDouble(radiusY * 0.18, radiusY * 0.62);
            AddVolcanicShield(
                elevation,
                width,
                height,
                Mod(centerX + offsetX, width),
                Math.Clamp(centerY + offsetY, 0, height - 1),
                random.NextDouble(radiusX * 0.20, radiusX * 0.42),
                random.NextDouble(radiusY * 0.18, radiusY * 0.40),
                random.NextDouble(0.30, 0.55));
        }
    }

    private static void AddElevationNoise(
        double[] elevation,
        int width,
        int height,
        ulong seed)
    {
        var featureSpan = Math.Min(Math.Min(width, height), WorldPreset.Small.Height);
        var scales = new (int CellSize, double Amplitude, ulong Salt)[]
        {
            (Math.Max(8, featureSpan / 3), 0.18, 0x9E3779B185EBCA87UL),
            (Math.Max(5, featureSpan / 7), 0.095, 0xC2B2AE3D27D4EB4FUL),
            (Math.Max(3, featureSpan / 15), 0.060, 0xD6E8FEB86659FD93UL),
            (2, 0.035, 0xA24BAED4963EE407UL),
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
        double strength,
        double summitBoost = 0,
        double[]? foundation = null)
    {
        var extentX = (int)Math.Ceiling(radiusX);
        var minimumY = Math.Max(0, centerY - (int)Math.Ceiling(radiusY));
        var maximumY = Math.Min(height - 1, centerY + (int)Math.Ceiling(radiusY));
        for (var y = minimumY; y <= maximumY; y++)
        {
            var normalizedY = (y - centerY) / radiusY;
            for (var offsetX = -extentX; offsetX <= extentX; offsetX++)
            {
                var x = Mod(centerX + offsetX, width);
                var normalizedX = Math.Abs(offsetX) / radiusX;
                var distanceSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distanceSquared >= 1)
                {
                    continue;
                }

                var index = y * width + x;
                if (foundation is not null && foundation[index] < 0.015)
                {
                    continue;
                }

                var influence = 1 - distanceSquared;
                var broadShield = influence * influence * strength;
                var summit = Math.Pow(influence, 6) * summitBoost;
                elevation[index] += broadShield + summit;
            }
        }
    }

    internal static void SelectOceanSeeds(
        SimulationWorld world,
        bool includeLargeSecondaryBasins)
    {
        var visited = new bool[checked(world.Width * world.Height)];
        var basins = new List<(int Size, GridPosition Deepest)>();
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

                basins.Add((size, deepest));
            }
        }

        var ordered = basins.OrderByDescending(basin => basin.Size).ToArray();
        if (ordered.Length == 0)
        {
            world.SetAdditionalOceanSeeds([]);
            return;
        }

        world.OceanSeed = ordered[0].Deepest;
        var minimumSecondaryBasinSize = Math.Max(64, world.Width * world.Height / 100);
        world.SetAdditionalOceanSeeds(includeLargeSecondaryBasins
            ? ordered.Skip(1)
                .Where(basin => basin.Size >= minimumSecondaryBasinSize)
                .Select(basin => basin.Deepest)
            : []);
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
