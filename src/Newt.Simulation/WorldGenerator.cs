namespace Newt.Simulation;

/// <summary>Creates deterministic, horizontally wrapping terrarium worlds.</summary>
public static class WorldGenerator
{
    public static SimulationWorld Generate(WorldGenerationOptions options)
    {
        if (options.LandFraction is < 0.15 or > 0.75)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Land fraction must be between 0.15 and 0.75.");
        }

        var preset = options.Preset;
        var world = new SimulationWorld(preset.Width, preset.Height, Terrain.DeepOcean, options.Seed);
        var elevation = new double[checked(preset.Width * preset.Height)];
        var random = new GeneratorRandom(options.Seed);
        var area = preset.Width * preset.Height;
        var landMasses = Math.Max(5, area / 1_350);

        for (var mass = 0; mass < landMasses; mass++)
        {
            var centerX = random.NextInt(preset.Width);
            var centerY = random.NextInt(preset.Height);
            var radiusX = random.NextDouble(preset.Width * 0.045, preset.Width * 0.16);
            var radiusY = random.NextDouble(preset.Height * 0.08, preset.Height * 0.24);
            var strength = random.NextDouble(0.65, 1.15);
            AddLandMass(elevation, preset.Width, preset.Height, centerX, centerY, radiusX, radiusY, strength);
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

        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static void AddLandMass(
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
}
