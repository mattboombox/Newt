namespace Newt.Simulation;

public static partial class WorldGenerator
{
    private static SimulationWorld GenerateNexus(WorldGenerationOptions options)
    {
        var preset = options.Preset;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.Plains, options.Seed)
        {
            Body = WorldBody.Nexus,
            SeasonsEnabled = false,
        };
        var center = NexusLayout.Center(world);
        var elevations = GenerateNexusElevations(world, options.Seed);

        // All usable terrain, including the center, contributes to land coverage.
        var petalElevations = new List<double>();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (!NexusLayout.IsWall(world, new(x, y)))
                petalElevations.Add(elevations[y * world.Width + x]);
        var threshold = FindLandThreshold(petalElevations.ToArray(), options.LandFraction);
        var depths = petalElevations.Where(value => value < threshold).Select(value => threshold - value).Order().ToArray();
        var depthScale = depths.Length == 0 ? 1 : Math.Max(1,
            TerrainClassifier.DeepOceanDepthThreshold / Math.Max(0.001, depths[depths.Length / 4]));
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var height = elevations[y * world.Width + x] - threshold;
            if (height < 0) height *= depthScale;
            world.SetElevation(new(x, y), (float)height);
        }
        NexusLayout.ApplyWalls(world);
        SelectOceanSeeds(world, includeLargeSecondaryBasins: true);
        TerrainClassifier.RebuildAll(world);
        world.TryPlaceTeleporter(center);
        for (var arm = 0; arm < NexusLayout.ArmCount; arm++)
            world.TryPlaceTeleporter(NexusLayout.ArmPosition(world, arm));
        SeedNaturalVolcanoes(world, options.Seed);
        StartNaturalSprings(world, options.Seed);
        world.SeedNexusCustodians(preset.Width >= WorldPreset.NexusMega.Width);
        return world;
    }

    internal static double[] GenerateNexusElevations(SimulationWorld world, ulong seed)
    {
        var center = NexusLayout.Center(world);
        var radius = NexusLayout.Radius(world);
        var regions = Enumerable.Range(0, NexusLayout.ArmCount + 1)
            .Select(_ => new List<GridPosition>()).ToArray();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, y);
            if (NexusLayout.IsWall(world, position)) continue;
            regions[NexusLayout.ArmIndex(world, position)].Add(position);
            var dx = x - center.X;
            var dy = y - center.Y;
            if (dx * dx + dy * dy <= radius * radius * 0.52 * 0.52)
                regions[NexusLayout.ArmCount].Add(position);
        }

        var result = new double[checked(world.Width * world.Height)];
        for (var region = 0; region < regions.Length; region++)
        {
            var tiles = regions[region];
            if (tiles.Count == 0) continue;
            var minX = tiles.Min(tile => tile.X);
            var minY = tiles.Min(tile => tile.Y);
            var width = tiles.Max(tile => tile.X) - minX + 1;
            var height = tiles.Max(tile => tile.Y) - minY + 1;
            // Each petal has its own random stream, noise, and local feature scale.
            var regionSeed = seed ^ unchecked(0x9E3779B97F4A7C15UL * (ulong)(region + 1));
            var random = new GeneratorRandom(regionSeed);
            var field = new double[width * height];
            var masses = Math.Clamp(field.Length / 10_000 + 2, 2, 24);
            for (var mass = 0; mass < masses; mass++)
                AddContinentalMass(field, width, height, ref random, 1.0);
            var foundation = (double[])field.Clone();
            var chains = Math.Clamp(field.Length / 7_000 + 2, 2, 24);
            for (var chain = 0; chain < chains; chain++)
                AddVolcanicChain(field, foundation, width, height, ref random);
            AddElevationNoise(field, width, height, regionSeed);

            foreach (var tile in tiles)
            {
                var value = field[(tile.Y - minY) * width + tile.X - minX];
                var index = tile.Y * world.Width + tile.X;
                if (region < NexusLayout.ArmCount)
                    result[index] = value;
                else
                {
                    // Petal boundaries meet inside the shared hub. Keep that hub
                    // smooth, then transition to independent terrain beyond the gaps.
                    var dx = tile.X - center.X;
                    var dy = tile.Y - center.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy) / radius;
                    var blend = Smooth(Math.Clamp((distance - 0.36) / 0.16, 0, 1));
                    result[index] = Lerp(value, result[index], blend);
                }
            }
        }
        return result;
    }
}
