namespace Newt.Simulation;

/// <summary>Builds deterministic, normalized temperature and moisture fields.</summary>
public static class ClimateSystem
{
    public const float GlobalClimateEditStep = 0.05f;
    private const int Unreachable = int.MaxValue;
    internal const float SeaIceThreshold = 0.12f;
    internal const float FreezingThreshold = 0.18f;
    internal const float ColdThreshold = 0.33f;
    internal const float HotThreshold = 0.67f;
    internal const float DryThreshold = 0.33f;
    internal const float WetThreshold = 0.67f;
    private const float RiverMoistureStrength = 0.45f;
    private const float RiverMoistureReach = 6f;
    private const float LakeMoistureStrength = 0.75f;
    private const float LakeMoistureReach = 8f;

    /// <summary>
    /// Rebuilds temperature from latitude, elevation, and broad seeded variation.
    /// This is a static climate normal, not moment-to-moment weather.
    /// </summary>
    public static void RebuildTemperature(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        for (var y = 0; y < world.Height; y++)
        {
            var latitude = Math.Abs((y + 0.5f) / world.Height * 2 - 1);
            var latitudeWarmth = 1 - latitude;
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                var elevationCooling = Math.Max(0, world.GetElevation(position)) * 0.48f;
                var variation = (FractalNoise(world, x, y, world.Seed ^ 0xA0761D6478BD642FUL) - 0.5f) * 0.18f;
                var baseline = world.Body is WorldBody.RingWorld
                    ? GetRingTemperatureNormal(world, x)
                    : 0.06f + latitudeWarmth * 0.98f + SeasonSystem.GetTemperatureOffset(world, y);
                var temperature = baseline - elevationCooling + variation + world.GlobalTemperatureOffset;
                world.SetTemperature(position, temperature);
            }
        }
    }

    /// <summary>
    /// Rebuilds moisture and biome after physical terrain and surface water are known.
    /// </summary>
    public static void RebuildMoistureAndBiomes(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);

        var oceanDistance = CalculateDistances(world, DistanceSource.Saltwater);
        var riverDistance = CalculateDistances(world, DistanceSource.River);
        var lakeDistance = CalculateDistances(world, DistanceSource.Lake);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                var index = y * world.Width + x;
                var oceanInfluence = DistanceInfluence(oceanDistance[index], strength: 0.54f, reach: 11f);
                var riverInfluence = DistanceInfluence(
                    riverDistance[index],
                    RiverMoistureStrength,
                    RiverMoistureReach);
                var lakeInfluence = DistanceInfluence(
                    lakeDistance[index],
                    LakeMoistureStrength,
                    LakeMoistureReach);
                var elevationPenalty = Math.Max(0, world.GetElevation(position)) * 0.18f;
                var variation = (FractalNoise(world, x, y, world.Seed ^ 0xE7037ED1A0B428DBUL) - 0.5f) * 0.28f;
                var engineeredZone = world.Body is WorldBody.RingWorld
                    ? GetRingMoistureNormal(world, x)
                    : 0f;
                var moisture = 0.08f + oceanInfluence + riverInfluence + lakeInfluence + variation - elevationPenalty +
                    engineeredZone + world.GlobalMoistureOffset;
                world.SetMoisture(position, moisture);

                var terrain = world.GetTerrain(position);
                var biome = IsSubmerged(terrain)
                    ? Biome.None
                    : ClassifyBiomeForTerrain(
                        terrain,
                        world.GetTemperature(position),
                        world.GetMoisture(position));
                world.SetBiome(position, FilterBiomeForLife(world, position, biome));
            }
        }
    }

    /// <summary>
    /// Reclassifies biomes after a temperature-only change without rebuilding
    /// the unchanged ocean, river, and lake distance fields.
    /// </summary>
    internal static void RebuildBiomesFromCurrentMoisture(SimulationWorld world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                var terrain = world.GetTerrain(position);
                var biome = IsSubmerged(terrain)
                    ? Biome.None
                    : ClassifyBiomeForTerrain(
                        terrain,
                        world.GetTemperature(position),
                        world.GetMoisture(position));
                world.SetBiome(position, FilterBiomeForLife(world, position, biome));
            }
        }
    }

    internal static void RebuildBiomeAt(SimulationWorld world, GridPosition position)
    {
        var terrain = world.GetTerrain(position);
        var biome = IsSubmerged(terrain)
            ? Biome.None
            : ClassifyBiomeForTerrain(
                terrain,
                world.GetTemperature(position),
                world.GetMoisture(position));
        world.SetBiome(position, FilterBiomeForLife(world, position, biome));
    }

    private static Biome ClassifyBiomeForTerrain(
        Terrain terrain,
        float temperature,
        float moisture)
    {
        var biome = ClassifyBiome(temperature, moisture);
        return biome is Biome.Swamp && terrain is Terrain.Hills or Terrain.Mountain
            ? Biome.Forest
            : biome;
    }

    private static Biome FilterBiomeForLife(
        SimulationWorld world,
        GridPosition position,
        Biome biome)
    {
        // Stone is represented ecologically by the absence of a biome. The
        // surface cover remains only as transient recovery-timer metadata for
        // impacts and cooled lava.
        if (world.GetSurfaceCover(position) is SurfaceCover.Stone)
        {
            return Biome.None;
        }

        if ((world.LifeEnabled && !world.IsLifeRecoveryPending(position)) ||
            biome is Biome.Desert ||
            biome is Biome.Arctic)
        {
            return biome;
        }

        return Biome.None;
    }

    internal static Biome ClassifyBiome(float temperature, float moisture)
    {
        var temperatureBand = ClassifyTemperature(temperature);
        var moistureBand = ClassifyMoisture(moisture);
        if (temperatureBand is TemperatureBand.Freezing)
        {
            return Biome.Arctic;
        }

        if (temperatureBand is TemperatureBand.Cold)
        {
            return moistureBand switch
            {
                MoistureBand.Dry => Biome.Tundra,
                MoistureBand.Wet => Biome.Bog,
                _ => Biome.Taiga,
            };
        }

        if (temperatureBand is TemperatureBand.Temperate)
        {
            return moistureBand switch
            {
                MoistureBand.Dry => Biome.Grassland,
                MoistureBand.Wet => Biome.Swamp,
                _ => Biome.Forest,
            };
        }

        return moistureBand switch
        {
            MoistureBand.Dry => Biome.Desert,
            MoistureBand.Wet => Biome.Jungle,
            _ => Biome.Arid,
        };
    }

    internal static TemperatureBand ClassifyTemperature(float temperature)
    {
        if (temperature < FreezingThreshold)
        {
            return TemperatureBand.Freezing;
        }

        if (temperature < ColdThreshold)
        {
            return TemperatureBand.Cold;
        }

        return temperature < HotThreshold ? TemperatureBand.Temperate : TemperatureBand.Hot;
    }

    internal static MoistureBand ClassifyMoisture(float moisture)
    {
        if (moisture < DryThreshold)
        {
            return MoistureBand.Dry;
        }

        return moisture < WetThreshold ? MoistureBand.Normal : MoistureBand.Wet;
    }

    public static void AdjustGlobalTemperature(SimulationWorld world, float amount)
    {
        ValidateGlobalAdjustment(world, amount);
        world.GlobalTemperatureOffset = Math.Clamp(
            world.GlobalTemperatureOffset + amount,
            SimulationWorld.MinimumGlobalClimateOffset,
            SimulationWorld.MaximumGlobalClimateOffset);
        TerrainClassifier.RebuildAll(world);
    }

    public static void AdjustGlobalMoisture(SimulationWorld world, float amount)
    {
        ValidateGlobalAdjustment(world, amount);
        world.GlobalMoistureOffset = Math.Clamp(
            world.GlobalMoistureOffset + amount,
            SimulationWorld.MinimumGlobalClimateOffset,
            SimulationWorld.MaximumGlobalClimateOffset);
        RebuildMoistureAndBiomes(world);
    }

    private static void ValidateGlobalAdjustment(SimulationWorld world, float amount)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (amount == 0 || !float.IsFinite(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }
    }

    private static int[] CalculateDistances(SimulationWorld world, DistanceSource source)
    {
        var distances = new int[checked(world.Width * world.Height)];
        Array.Fill(distances, Unreachable);
        var frontier = new Queue<int>();

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (!IsSource(world, position, source))
                {
                    continue;
                }

                var index = y * world.Width + x;
                distances[index] = 0;
                frontier.Enqueue(index);
            }
        }

        while (frontier.TryDequeue(out var index))
        {
            var x = index % world.Width;
            var y = index / world.Width;
            TryVisit((x + 1) % world.Width, y);
            TryVisit((x - 1 + world.Width) % world.Width, y);
            if (y > 0)
            {
                TryVisit(x, y - 1);
            }
            if (y + 1 < world.Height)
            {
                TryVisit(x, y + 1);
            }

            void TryVisit(int neighborX, int neighborY)
            {
                var neighborIndex = neighborY * world.Width + neighborX;
                if (distances[neighborIndex] != Unreachable)
                {
                    return;
                }

                distances[neighborIndex] = distances[index] + 1;
                frontier.Enqueue(neighborIndex);
            }
        }

        return distances;
    }

    private static bool IsSource(SimulationWorld world, GridPosition position, DistanceSource source) => source switch
    {
        DistanceSource.Saltwater => world.GetTerrain(position) is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice,
        DistanceSource.River => world.GetSurfaceWater(position) is SurfaceWaterKind.River,
        DistanceSource.Lake => world.GetSurfaceWater(position) is SurfaceWaterKind.FreshwaterLake,
        _ => false,
    };

    private static float DistanceInfluence(int distance, float strength, float reach) =>
        distance == Unreachable ? 0 : strength * MathF.Exp(-distance / reach);

    private static float GetRingTemperatureNormal(SimulationWorld world, int x)
    {
        var phase = (x + 0.5f) / world.Width * MathF.Tau;
        var seedPhase = (world.Seed % 10_000) / 10_000f * MathF.Tau;
        return 0.49f + MathF.Sin(phase * 3 + seedPhase) * 0.30f +
            MathF.Sin(phase * 7 - seedPhase * 0.5f) * 0.08f;
    }

    private static float GetRingMoistureNormal(SimulationWorld world, int x)
    {
        var phase = (x + 0.5f) / world.Width * MathF.Tau;
        var seedPhase = (world.Seed % 8_192) / 8_192f * MathF.Tau;
        return MathF.Sin(phase * 4 + seedPhase) * 0.24f +
            MathF.Sin(phase * 9 - seedPhase) * 0.07f;
    }

    private static bool IsSubmerged(Terrain terrain) => terrain is
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice;

    private static float FractalNoise(SimulationWorld world, int x, int y, ulong seed)
    {
        var broad = ValueNoise(world.Width, x, y, seed, scale: 32);
        var regional = ValueNoise(world.Width, x, y, seed ^ 0x8EBC6AF09C88C6E3UL, scale: 13);
        return broad * 0.68f + regional * 0.32f;
    }

    private static float ValueNoise(int worldWidth, int x, int y, ulong seed, int scale)
    {
        var horizontalCells = Math.Max(1, (int)Math.Ceiling(worldWidth / (double)scale));
        var continuousX = x * horizontalCells / (float)worldWidth;
        var cellX = (int)MathF.Floor(continuousX);
        var cellY = y / scale;
        var fractionX = Smooth(continuousX - cellX);
        var fractionY = Smooth((y % scale) / (float)scale);
        var nextCellX = (cellX + 1) % horizontalCells;

        var northWest = HashToUnit(seed, cellX, cellY);
        var northEast = HashToUnit(seed, nextCellX, cellY);
        var southWest = HashToUnit(seed, cellX, cellY + 1);
        var southEast = HashToUnit(seed, nextCellX, cellY + 1);
        var north = Lerp(northWest, northEast, fractionX);
        var south = Lerp(southWest, southEast, fractionX);
        return Lerp(north, south, fractionY);
    }

    private static float HashToUnit(ulong seed, int x, int y)
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

    private static float Smooth(float value) => value * value * (3 - 2 * value);

    private static float Lerp(float start, float end, float amount) => start + (end - start) * amount;

    private enum DistanceSource : byte
    {
        Saltwater,
        River,
        Lake,
    }
}
