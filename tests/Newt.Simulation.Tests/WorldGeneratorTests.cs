using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WorldGeneratorTests
{
    [Fact]
    public void SameOptionsProduceSameTerrain()
    {
        var options = new WorldGenerationOptions(WorldPreset.Micro, Seed: 42);
        var first = WorldGenerator.Generate(options);
        var second = WorldGenerator.Generate(options);

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                var position = new GridPosition(x, y);
                Assert.Equal(first.GetTerrain(position), second.GetTerrain(position));
                Assert.Equal(first.GetTemperature(position), second.GetTemperature(position));
                Assert.Equal(first.GetMoisture(position), second.GetMoisture(position));
                Assert.Equal(first.GetBiome(position), second.GetBiome(position));
                Assert.Equal(first.GetSurfaceCover(position), second.GetSurfaceCover(position));
                Assert.Equal(first.GetSurfaceWater(position), second.GetSurfaceWater(position));
            }
        }

        Assert.Equal(first.ActiveSpringCount, second.ActiveSpringCount);
        Assert.Equal(first.VolcanoCount, second.VolcanoCount);
        for (var index = 0; index < first.VolcanoCount; index++)
        {
            Assert.Equal(first.GetVolcano(index), second.GetVolcano(index));
        }
    }

    [Fact]
    public void GeneratedWorldContainsLandWaterAndCoasts()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Micro, Seed: 73));
        var terrains = CountTerrains(world);

        Assert.True(terrains[Terrain.Plains] + terrains[Terrain.Hills] + terrains[Terrain.Mountain] > 0);
        Assert.True(terrains[Terrain.Ocean] + terrains[Terrain.DeepOcean] > 0);
        Assert.True(terrains[Terrain.Shallows] > 0);
        Assert.True(terrains[Terrain.Beach] > 0);
        Assert.True(world.VolcanoCount > 0);
        Assert.Contains(AllPositions(world), position => world.GetSurfaceCover(position) is SurfaceCover.Lava);
    }

    [Fact]
    public void DefaultGameWorldSeedsItsLargestBelowSeaLevelBasin()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Standard, Seed: 20260806));

        Assert.True(world.GetTerrain(world.OceanSeed) is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice);
        Assert.Equal(
            LargestBelowSeaLevelComponentSize(world),
            BelowSeaLevelComponentSize(world, world.OceanSeed));
    }

    [Fact]
    public void PresetsProduceTheirDocumentedDimensions()
    {
        WorldPreset[] presets = [WorldPreset.Micro, WorldPreset.Standard, WorldPreset.Large, WorldPreset.Ring, WorldPreset.Earth];
        foreach (var preset in presets)
        {
            var world = WorldGenerator.Generate(new WorldGenerationOptions(preset, Seed: 9));
            Assert.Equal(preset.Width, world.Width);
            Assert.Equal(preset.Height, world.Height);
            if (preset != WorldPreset.Earth)
            {
                Assert.InRange(world.ActiveSpringCount, 1, 6);
            }
        }
    }

    [Fact]
    public void EarthPresetUsesRecognizableRealWorldRelief()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Earth, Seed: 9));

        Assert.Equal(240, world.Width);
        Assert.Equal(120, world.Height);
        Assert.Equal(WorldBody.Earth, world.Body);
        Assert.True(world.GetElevation(new GridPosition(174, 39)) > 0.58f); // Himalayas
        Assert.True(world.GetElevation(new GridPosition(20, 60)) < -0.20f); // Pacific
        Assert.True(world.GetElevation(new GridPosition(120, 60)) < 0); // Gulf of Guinea
    }

    [Fact]
    public void RingWorldIsLongWithoutScalingItsLocalFeaturesByFullWidth()
    {
        Assert.Equal(1200, WorldPreset.Ring.Width);
        Assert.Equal(40, WorldPreset.Ring.Height);
        Assert.True(WorldPreset.Ring.Width >= WorldPreset.Large.Width * 4);

        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Ring, Seed: 9));
        Assert.Equal(WorldPreset.Ring.Width, world.Width);
        Assert.Equal(WorldPreset.Ring.Height, world.Height);
        Assert.Equal(WorldBody.RingWorld, world.Body);
        Assert.False(world.SeasonsEnabled);
        Assert.NotEmpty(world.AdditionalOceanSeeds);
        Assert.All(world.AdditionalOceanSeeds, seed =>
            Assert.True(world.GetTerrain(seed) is
                Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice));
        for (var x = 0; x < world.Width; x++)
        {
            var top = new GridPosition(x, 0);
            var bottom = new GridPosition(x, world.Height - 1);
            Assert.Equal(Terrain.RingWorldWall, world.GetTerrain(top));
            Assert.Equal(Terrain.RingWorldWall, world.GetTerrain(bottom));
            Assert.Equal(SimulationWorld.RingWorldWallElevation, world.GetElevation(top));
            Assert.Equal(SimulationWorld.RingWorldWallElevation, world.GetElevation(bottom));
        }
        Assert.True(SimulationWorld.RingWorldWallElevation > SimulationWorld.MaximumGroundElevation);
    }

    [Fact]
    public void RingWorldUsesLongitudinalEngineeredClimateZones()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Ring, Seed: 9));
        var centerY = world.Height / 2;
        var temperatures = Enumerable.Range(0, world.Width)
            .Select(x => world.GetTemperature(new GridPosition(x, centerY)))
            .ToArray();
        var moistures = Enumerable.Range(0, world.Width)
            .Select(x => world.GetMoisture(new GridPosition(x, centerY)))
            .ToArray();

        Assert.True(temperatures.Max() - temperatures.Min() > 0.5f);
        Assert.True(moistures.Max() - moistures.Min() > 0.45f);
        SeasonSystem.SetEnabled(world, true);
        Assert.False(world.SeasonsEnabled);
    }

    [Fact]
    public void ElevationIsStoredRelativeToSeaLevel()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Micro, Seed: 91));
        var elevations = new List<float>();

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                elevations.Add(world.GetElevation(new GridPosition(x, y)));
            }
        }

        Assert.Contains(elevations, elevation => elevation <= 0);
        Assert.Contains(elevations, elevation => elevation > 0);
    }

    [Fact]
    public void NaturalSpringsStartOnNonArcticMountainsBesideArcticMountains()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Micro, Seed: 42));
        var sources = AllPositions(world)
            .Where(position => world.GetSurfaceWater(position) is SurfaceWaterKind.River)
            .ToList();

        Assert.NotEmpty(sources);
        Assert.Equal(sources.Count, world.ActiveSpringCount);
        foreach (var source in sources)
        {
            Assert.Equal(Terrain.Mountain, world.GetTerrain(source));
            Assert.NotEqual(Biome.Arctic, world.GetBiome(source));
            Assert.True(HasAdjacentArcticMountain(world, source));
        }
    }

    private static Dictionary<Terrain, int> CountTerrains(SimulationWorld world)
    {
        var counts = Enum.GetValues<Terrain>().ToDictionary(terrain => terrain, _ => 0);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                counts[world.GetTerrain(new GridPosition(x, y))]++;
            }
        }

        return counts;
    }

    private static int LargestBelowSeaLevelComponentSize(SimulationWorld world)
    {
        var visited = new HashSet<GridPosition>();
        var largest = 0;
        foreach (var position in AllPositions(world))
        {
            if (world.GetElevation(position) <= world.SeaLevel && !visited.Contains(position))
            {
                largest = Math.Max(largest, FloodBelowSeaLevel(world, position, visited));
            }
        }
        return largest;
    }

    private static int BelowSeaLevelComponentSize(SimulationWorld world, GridPosition start) =>
        FloodBelowSeaLevel(world, start, []);

    private static int FloodBelowSeaLevel(
        SimulationWorld world,
        GridPosition start,
        HashSet<GridPosition> visited)
    {
        var queue = new Queue<GridPosition>();
        queue.Enqueue(start);
        visited.Add(start);
        var count = 0;
        ReadOnlySpan<GridPosition> directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
        while (queue.TryDequeue(out var current))
        {
            count++;
            foreach (var direction in directions)
            {
                var y = current.Y + direction.Y;
                if (y < 0 || y >= world.Height)
                {
                    continue;
                }
                var neighbor = new GridPosition((current.X + direction.X + world.Width) % world.Width, y);
                if (world.GetElevation(neighbor) <= world.SeaLevel && visited.Add(neighbor))
                {
                    queue.Enqueue(neighbor);
                }
            }
        }
        return count;
    }

    private static bool HasAdjacentArcticMountain(SimulationWorld world, GridPosition position)
    {
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

                var x = (position.X + offsetX + world.Width) % world.Width;
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

    private static IEnumerable<GridPosition> AllPositions(SimulationWorld world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                yield return new GridPosition(x, y);
            }
        }
    }
}
