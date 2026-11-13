using System.Numerics;

namespace Newt.Simulation;

public static partial class WorldGenerator
{
    private static SimulationWorld GeneratePlanetaryRelief(WorldGenerationOptions options)
    {
        const int sourceWidth = 2560, sourceHeight = 1280;
        var moon = options.MapType is WorldMapType.Moon;
        var resource = moon ? "MoonElevation" : "MarsElevation";
        var world = new SimulationWorld(options.Preset.Width, options.Preset.Height,
            Terrain.DeepOcean, options.Seed)
        {
            Body = ShapeBody(options.Shape, moon ? WorldBody.Moon : WorldBody.Mars),
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            $"Newt.Simulation.{resource}") ??
            throw new InvalidOperationException($"The embedded {resource} grid is missing.");
        using var reader = new BinaryReader(stream);
        var source = new short[sourceWidth * sourceHeight];
        for (var index = 0; index < source.Length; index++)
            source[index] = reader.ReadInt16();

        var metersPerUnit = moon ? 6_000f : 10_000f;
        ApplyPlanetaryRelief(world, source, sourceWidth, sourceHeight, metersPerUnit, metersPerUnit);

        if (world.IsRingHabitat || world.Body is WorldBody.Nexus or WorldBody.Planetoid)
            FinishGeneratedTerrain(world, includeLargeSecondaryBasins: true, seedVolcanoes: true);
        else
        {
            SelectOceanSeeds(world, includeLargeSecondaryBasins: true);
            TerrainClassifier.RebuildAll(world);
            StartNaturalSprings(world, options.Seed);
        }
        return world;
    }

    private static void ApplyPlanetaryRelief(SimulationWorld world, short[] source, int sourceWidth,
        int sourceHeight, float landMetersPerUnit, float seaMetersPerUnit)
    {
        if (world.Body == WorldBody.Planetoid)
        {
            var faceSize = PlanetoidLayout.FaceSize(world.Width);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
            {
                var (sourceX, sourceY) = PlanetoidSourcePosition(TileDirection(x, y, faceSize), sourceWidth, sourceHeight);
                var meters = source[(int)sourceY * sourceWidth + (int)sourceX];
                world.SetElevation(new(x, y), meters / (meters >= 0 ? landMetersPerUnit : seaMetersPerUnit));
            }
            return;
        }
        var scale = (double)sourceWidth / world.Width;
        // Curved worlds keep the source aspect ratio. A narrow ring takes the equatorial band;
        // taller shapes continue the terrain beyond the poles with a seeded elevation field.
        var curved = world.IsRingHabitat || world.Body == WorldBody.Nexus;
        var reliefHeight = curved ? sourceHeight / scale : world.Height;
        var reliefTop = (world.Height - reliefHeight) / 2;
        var extension = reliefTop > 0
            ? GenerateProceduralElevations(world.Width, world.Height, WorldMapType.Continents, world.Seed)
            : null;
        var extensionSeaLevel = extension is null ? 0 : FindLandThreshold(extension, 0.38);
        var blendWidth = Math.Max(1, Math.Min(reliefTop, world.Width / 8.0));

        for (var y = 0; y < world.Height; y++)
        {
            var reliefY = y + 0.5 - reliefTop;
            var sourceY = Math.Clamp((int)(reliefY * sourceHeight / reliefHeight), 0, sourceHeight - 1);
            var beyondRelief = Math.Max(-reliefY, reliefY - reliefHeight);
            var blend = Smooth(Math.Clamp(beyondRelief / blendWidth, 0, 1));
            // Repeating a polar edge row carries peaks into long ridges. Fade the
            // mountain relief faster than the underlying land and coastlines.
            var peakFade = Smooth(Math.Clamp(beyondRelief / (blendWidth / 4), 0, 1));
            for (var x = 0; x < world.Width; x++)
            {
                var sourceX = Math.Min(sourceWidth - 1, (int)((x + 0.5) * scale));
                var meters = source[sourceY * sourceWidth + sourceX];
                var elevation = meters / (meters >= 0 ? landMetersPerUnit : seaMetersPerUnit);
                if (extension is not null && blend > 0)
                {
                    var peak = Math.Max(0, elevation - TerrainClassifier.MountainElevationThreshold);
                    elevation -= (float)(peak * peakFade);
                    elevation = (float)Lerp(elevation, extension[y * world.Width + x] - extensionSeaLevel, blend);
                }
                world.SetElevation(new(x, y), elevation);
            }
        }

        if (extension is not null && (world.Body is WorldBody.ONeillCylinder or WorldBody.Nexus))
        {
            // Balance each blended polar extension independently. The outermost rows become walls.
            var polarRows = (int)Math.Ceiling(reliefTop - 0.5);
            DeepenPolarOcean(world, 1, polarRows);
            DeepenPolarOcean(world, world.Height - polarRows, world.Height - 1);
        }
    }

    private static Vector3 TileDirection(int x, int y, int size) =>
        PlanetoidLayout.Direction(x + 0.5f, y + 0.5f, size, PlanetoidLayout.FaceAt(x, y, size));

    private static (double X, double Y) PlanetoidSourcePosition(Vector3 direction, int width, int height)
    {
        var longitude = Math.Atan2(direction.X, direction.Z) / Math.Tau + 0.5;
        var latitude = 0.5 - Math.Asin(Math.Clamp(direction.Y, -1, 1)) / Math.PI;
        return (Math.Min(width - 1e-6, longitude * width), Math.Min(height - 1e-6, latitude * height));
    }

    private static double[] GeneratePlanetoidElevations(SimulationWorld world, WorldMapType mapType)
    {
        var sourceWidth = world.Width;
        var sourceHeight = sourceWidth / 2;
        var source = GenerateProceduralElevations(sourceWidth, sourceHeight, mapType, world.Seed);
        var rowMeans = new double[sourceHeight];
        for (var y = 0; y < sourceHeight; y++)
        {
            var total = 0.0;
            foreach (var value in source.AsSpan(y * sourceWidth, sourceWidth))
                total += value;
            rowMeans[y] = total / sourceWidth;
        }

        var elevation = new double[world.Width * world.Height];
        var size = PlanetoidLayout.FaceSize(world.Width);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var direction = TileDirection(x, y, size);
            var (sx, sy) = PlanetoidSourcePosition(direction, sourceWidth, sourceHeight);
            sx -= 0.5;
            sy = Math.Clamp(sy - 0.5, 0, sourceHeight - 1);
            var west = (int)Math.Floor(sx);
            var blendX = sx - west;
            west = Mod(west, sourceWidth);
            var east = (west + 1) % sourceWidth;
            var north = (int)sy;
            var south = Math.Min(north + 1, sourceHeight - 1);
            var blendY = sy - north;
            var projected = Lerp(Lerp(source[north * sourceWidth + west], source[north * sourceWidth + east], blendX),
                Lerp(source[south * sourceWidth + west], source[south * sourceWidth + east], blendX), blendY);

            // Longitudes converge near the poles. Fade the stretched bands into
            // the local mean with detail sampled directly on the sphere.
            var polarBlend = Smooth(Math.Clamp((Math.Abs(direction.Y) - 0.72) / 0.28, 0, 1));
            if (polarBlend > 0)
            {
                var polar = Lerp(rowMeans[north], rowMeans[south], blendY) + SphericalElevationNoise(direction, size, world.Seed);
                projected = Lerp(projected, polar, polarBlend);
            }
            elevation[y * world.Width + x] = projected;
        }
        return elevation;
    }

    private static double SphericalElevationNoise(Vector3 direction, int faceSize, ulong seed) =>
        0.24 * GlobeNoise(direction * Math.Max(2f, faceSize / 20f), seed) +
        0.10 * GlobeNoise(direction * Math.Max(4f, faceSize / 6f), seed ^ 0xC2B2AE3D27D4EB4FUL);

    internal static double GlobeNoise(Vector3 point, ulong seed)
    {
        var x = (int)MathF.Floor(point.X);
        var y = (int)MathF.Floor(point.Y);
        var z = (int)MathF.Floor(point.Z);
        var blendX = Smooth(point.X - x);
        var blendY = Smooth(point.Y - y);
        var blendZ = Smooth(point.Z - z);
        return Lerp(Slice(z), Slice(z + 1), blendZ) * 2 - 1;

        double Slice(int plane)
        {
            var planeSeed = seed ^ unchecked((ulong)(uint)plane * 0xA24BAED4963EE407UL);
            return Lerp(Lerp(HashNoise(planeSeed, x, y), HashNoise(planeSeed, x + 1, y), blendX),
                Lerp(HashNoise(planeSeed, x, y + 1), HashNoise(planeSeed, x + 1, y + 1), blendX), blendY);
        }
    }

    private static void DeepenPolarOcean(SimulationWorld world, int startY, int endY)
    {
        if (startY >= endY) return;
        var depths = new List<double>();
        var nexus = world.Body == WorldBody.Nexus;
        for (var y = startY; y < endY; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, y);
            if (nexus && NexusLayout.IsWall(world, position)) continue;
            var depth = world.SeaLevel - world.GetElevation(position);
            if (depth > 0) depths.Add(depth);
        }

        var depthScale = DeepOceanScale(depths);
        if (depthScale <= 1) return;
        for (var y = startY; y < endY; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, y);
            var depth = world.SeaLevel - world.GetElevation(position);
            if (depth > 0)
                world.SetElevation(position, world.SeaLevel - (float)(depth * depthScale));
        }
    }
}
