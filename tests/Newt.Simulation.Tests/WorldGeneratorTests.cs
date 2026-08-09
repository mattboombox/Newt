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
    public void DefaultGameWorldStartsWithOceanAtItsCenterSeed()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Standard, Seed: 20260806));

        Assert.Equal(new GridPosition(world.Width / 2, world.Height / 2), world.OceanSeed);
        Assert.True(world.GetTerrain(world.OceanSeed) is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice);
    }

    [Fact]
    public void PresetsProduceTheirDocumentedDimensions()
    {
        WorldPreset[] presets = [WorldPreset.Micro, WorldPreset.Standard, WorldPreset.Large, WorldPreset.Ring];
        foreach (var preset in presets)
        {
            var world = WorldGenerator.Generate(new WorldGenerationOptions(preset, Seed: 9));
            Assert.Equal(preset.Width, world.Width);
            Assert.Equal(preset.Height, world.Height);
            Assert.InRange(world.ActiveSpringCount, 1, 6);
        }
    }

    [Fact]
    public void RingWorldIsLongWithoutScalingItsLocalFeaturesByFullWidth()
    {
        Assert.Equal(504, WorldPreset.Ring.Width);
        Assert.Equal(40, WorldPreset.Ring.Height);
        Assert.True(WorldPreset.Ring.Width >= WorldPreset.Large.Width * 2);

        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Ring, Seed: 9));
        Assert.Equal(WorldPreset.Ring.Width, world.Width);
        Assert.Equal(WorldPreset.Ring.Height, world.Height);
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
