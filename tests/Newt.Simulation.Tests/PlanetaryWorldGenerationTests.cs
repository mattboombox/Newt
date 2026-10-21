using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlanetaryWorldGenerationTests
{
    public static IEnumerable<object[]> PlanetSizes()
    {
        foreach (var type in new[] { WorldMapType.Moon, WorldMapType.Mars })
        foreach (var preset in new[] { WorldPreset.Micro, WorldPreset.Small, WorldPreset.Standard,
            WorldPreset.Large, WorldPreset.Huge })
            yield return [type, preset];
    }

    [Theory]
    [MemberData(nameof(PlanetSizes))]
    public void PlanetaryReliefSupportsEveryEarthSizeWithNormalOceans(WorldMapType type, WorldPreset preset)
    {
        var world = WorldGenerator.Generate(new(preset, 9, MapType: type));
        Assert.Equal(preset.Width, world.Width);
        Assert.Equal(preset.Height, world.Height);
        Assert.Equal(type is WorldMapType.Moon ? WorldBody.Moon : WorldBody.Mars, world.Body);
        Assert.True(world.HasOceans);
        Assert.True(world.SeasonsEnabled);
        Assert.Equal(0L, world.SeasonTick);
        Assert.True(world.GetElevation(world.OceanSeed) <= world.SeaLevel);
        Assert.True(world.GetTerrain(world.OceanSeed) is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice);
        Assert.All(world.AdditionalOceanSeeds, seed => Assert.True(world.GetElevation(seed) <= world.SeaLevel));
        var land = false;
        var ocean = false;
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, y);
            land |= world.GetElevation(position) > world.SeaLevel;
            ocean |= world.GetTerrain(position) is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows;
        }
        Assert.True(land);
        Assert.True(ocean);
    }

    [Theory]
    [InlineData(WorldMapType.Moon)]
    [InlineData(WorldMapType.Mars)]
    public void PlanetaryMapsStartWithNeutralPolarSeasonsAndAdvanceNormally(WorldMapType type)
    {
        var world = WorldGenerator.Generate(new(WorldPreset.Micro, 9, MapType: type));
        var north = new GridPosition(world.Width / 2, 0);
        var south = new GridPosition(world.Width / 2, world.Height - 1);
        Assert.Equal(0f, SeasonSystem.GetTemperatureChange(world, north));
        Assert.Equal(0f, SeasonSystem.GetTemperatureChange(world, south));
        Assert.True(world.SeasonsEnabled);
        SeasonSystem.Advance(world);
        Assert.Equal(1L, world.SeasonTick);
        world.SeasonTick = SeasonSystem.TicksPerYear / 4;
        Assert.True(SeasonSystem.GetTemperatureChange(world, north) > 0);
        Assert.True(SeasonSystem.GetTemperatureChange(world, south) < 0);
        SeasonSystem.SetEnabled(world, false);
        Assert.Equal(0f, SeasonSystem.GetTemperatureChange(world, north));
        Assert.Equal(0f, SeasonSystem.GetTemperatureChange(world, south));
    }

    [Fact]
    public void EarthKeepsSeasonsEnabledByDefault()
    {
        var world = WorldGenerator.Generate(new(WorldPreset.Micro, 9, MapType: WorldMapType.Earth));
        Assert.True(world.SeasonsEnabled);
    }

    [Theory]
    [InlineData(WorldMapType.Moon, "MoonElevation", 6000f)]
    [InlineData(WorldMapType.Mars, "MarsElevation", 10000f)]
    public void PlanetaryMapsKeepOriginalReliefAndChooseDeepestOceanSeed(WorldMapType type, string resource, float scale)
    {
        var world = WorldGenerator.Generate(new(WorldPreset.Standard, 19, MapType: type));
        using var stream = typeof(WorldGenerator).Assembly.GetManifestResourceStream($"Newt.Simulation.{resource}")!;
        using var reader = new BinaryReader(stream);
        Assert.Equal(2560L * 1280 * sizeof(short), stream.Length);
        var source = new short[2560 * 1280];
        for (var index = 0; index < source.Length; index++) source[index] = reader.ReadInt16();
        var matches = 0;
        var compared = 0;
        var distinct = 0;
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
        {
            var sourceX = (int)((x + 0.5) * 2560 / world.Width);
            var sourceY = (int)((y + 0.5) * 1280 / world.Height);
            var expected = source[sourceY * 2560 + sourceX] / scale;
            if (Math.Abs(world.GetElevation(new(x, y)) - Math.Clamp(expected,
                SimulationWorld.MinimumGroundElevation, SimulationWorld.MaximumGroundElevation)) < 0.00001f)
                matches++;
            if (x > 0 && expected > SimulationWorld.MinimumGroundElevation &&
                expected < SimulationWorld.MaximumGroundElevation)
            {
                compared++;
                if (world.GetElevation(new(x, y)) != world.GetElevation(new(x - 1, y))) distinct++;
            }
        }
        // Natural springs can raise a small number of source tiles.
        Assert.True(matches > world.Width * world.Height * 0.99);
        // The old 240-column grid duplicated about a quarter of Standard's
        // adjacent columns. Real source detail should resolve those steps.
        Assert.True(distinct > compared * 0.90, $"Distinct neighboring elevations: {distinct}/{compared}");
        var visited = new HashSet<GridPosition> { world.OceanSeed };
        var pending = new Queue<GridPosition>();
        pending.Enqueue(world.OceanSeed);
        while (pending.TryDequeue(out var position))
        {
            Assert.True(world.GetElevation(position) >= world.GetElevation(world.OceanSeed));
            foreach (var offset in new GridPosition[] { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) })
            {
                var neighbor = new GridPosition((position.X + offset.X + world.Width) % world.Width,
                    position.Y + offset.Y);
                if (world.Contains(neighbor) && world.GetElevation(neighbor) <= world.SeaLevel && visited.Add(neighbor))
                    pending.Enqueue(neighbor);
            }
        }
    }
}
