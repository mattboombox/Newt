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
            Body = moon ? WorldBody.Moon : WorldBody.Mars,
        };
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream(
            $"Newt.Simulation.{resource}") ??
            throw new InvalidOperationException($"The embedded {resource} grid is missing.");
        using var reader = new BinaryReader(stream);
        var source = new short[sourceWidth * sourceHeight];
        for (var index = 0; index < source.Length; index++)
            source[index] = reader.ReadInt16();

        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            // Match Earth's cell-centered relief sampling at every supported size.
            var sourceX = Math.Min(sourceWidth - 1, (int)((x + 0.5) * sourceWidth / world.Width));
            var sourceY = Math.Min(sourceHeight - 1, (int)((y + 0.5) * sourceHeight / world.Height));
            var meters = source[sourceY * sourceWidth + sourceX];
            world.SetElevation(new(x, y), meters / (moon ? 6_000f : 10_000f));
        }

        SelectOceanSeeds(world, includeLargeSecondaryBasins: true);
        TerrainClassifier.RebuildAll(world);
        StartNaturalSprings(world, options.Seed);
        return world;
    }
}
