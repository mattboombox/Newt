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
    public void LargeDisconnectedBelowSeaLevelBasinBecomesASecondaryOcean()
    {
        var world = new SimulationWorld(24, 20, Terrain.Plains, seed: 84);
        world.HasOceans = true;
        foreach (var position in AllPositions(world))
        {
            world.SetElevation(position, 0.5f);
        }

        for (var y = 2; y < 12; y++)
        {
            for (var x = 1; x < 11; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.2f);
            }
        }
        for (var y = 4; y < 12; y++)
        {
            for (var x = 14; x < 22; x++)
            {
                world.SetElevation(new GridPosition(x, y), -0.1f);
            }
        }

        WorldGenerator.SelectOceanSeeds(world, includeLargeSecondaryBasins: true);
        TerrainClassifier.RebuildAll(world);

        Assert.Single(world.AdditionalOceanSeeds);
        Assert.True(world.GetTerrain(world.AdditionalOceanSeeds[0]) is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice);
    }

    [Fact]
    public void PresetsProduceTheirDocumentedDimensions()
    {
        WorldPreset[] presets =
            [WorldPreset.Micro, WorldPreset.Small, WorldPreset.Standard, WorldPreset.Large,
                WorldPreset.Huge, WorldPreset.Ring, WorldPreset.Earth];
        foreach (var preset in presets)
        {
            var world = WorldGenerator.Generate(new WorldGenerationOptions(preset, Seed: 9));
            Assert.Equal(preset.Width, world.Width);
            Assert.Equal(preset.Height, world.Height);
            if (preset != WorldPreset.Earth)
            {
                Assert.InRange(world.ActiveSpringCount, 1, 24);
            }
        }
    }

    [Fact]
    public void RenamedPresetsUseTheirDocumentedNamesAndDimensions()
    {
        Assert.Equal(new WorldPreset("Small", 160, 96), WorldPreset.Small);
        Assert.Equal(new WorldPreset("Standard", 320, 192), WorldPreset.Standard);
        Assert.Equal(new WorldPreset("Large", 640, 311), WorldPreset.Large);
        Assert.Equal(new WorldPreset("Huge", 1280, 642), WorldPreset.Huge);

        Assert.Equal(320, WorldPreset.Standard.Width);
        Assert.Equal(192, WorldPreset.Standard.Height);
        Assert.Equal(640, WorldPreset.Large.Width);
        Assert.Equal(311, WorldPreset.Large.Height);
        Assert.Equal(2560, WorldPreset.Large.Width * 4);
        Assert.True(WorldPreset.Large.Height * 4 < 1440 - 156);
    }

    [Fact]
    public void HugePresetFillsA1440pViewportAtFarthestZoom()
    {
        Assert.Equal(1280, WorldPreset.Huge.Width);
        Assert.Equal(642, WorldPreset.Huge.Height);
        Assert.Equal(2560, WorldPreset.Huge.Width * 2);
        Assert.Equal(1440 - 156, WorldPreset.Huge.Height * 2);
    }

    [Fact]
    public void EarthPresetUsesRecognizableRealWorldRelief()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Earth, Seed: 9));

        Assert.Equal(1280, world.Width);
        Assert.Equal(642, world.Height);
        Assert.Equal(WorldBody.Earth, world.Body);
        Assert.True(world.GetElevation(new GridPosition(928, 209)) > 0.58f); // Himalayas
        Assert.True(world.GetElevation(new GridPosition(107, 321)) < -0.20f); // Pacific
        Assert.True(world.GetElevation(new GridPosition(640, 321)) < 0); // Gulf of Guinea
        Assert.True(world.GetTerrain(new GridPosition(693, 192)) is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows); // Mediterranean
        Assert.NotEmpty(world.SpringSources);
        Assert.All(world.SpringSources, source =>
        {
            Assert.Equal(SpringOrigin.Natural, source.Origin);
            Assert.True(world.GetTerrain(source.Position) is Terrain.Hills or Terrain.Mountain);
            Assert.Equal(SurfaceWaterKind.River, world.GetSurfaceWater(source.Position));
        });
    }

    [Fact]
    public void EarthMapCanBeGeneratedAtLargeSizeFromTheSameReliefResource()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Large,
            Seed: 9,
            MapType: WorldMapType.Earth));

        Assert.Equal(640, world.Width);
        Assert.Equal(311, world.Height);
        Assert.Equal(WorldBody.Earth, world.Body);
        Assert.Contains(AllPositions(world), position => world.GetElevation(position) > 0.58f);
        Assert.Contains(AllPositions(world), position => world.GetElevation(position) < -0.20f);
    }

    [Fact]
    public void ProceduralMapShapesAreDeterministicAndDistinct()
    {
        var continents = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Continents));
        var pangaea = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Pangaea));
        var archipelago = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Archipelago));
        var secondArchipelago = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Archipelago));

        Assert.Contains(AllPositions(continents), position =>
            continents.GetElevation(position) != pangaea.GetElevation(position));
        Assert.Contains(AllPositions(continents), position =>
            continents.GetElevation(position) != archipelago.GetElevation(position));
        Assert.All(AllPositions(archipelago), position =>
            Assert.Equal(archipelago.GetElevation(position), secondArchipelago.GetElevation(position)));
    }

    [Fact]
    public void ExtremeMapShapesHaveClearlyDifferentLandConnectivity()
    {
        var pangaea = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Pangaea));
        var archipelago = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.Archipelago));
        var pangaeaLand = FindAboveSeaLevelComponents(pangaea).OrderByDescending(component => component.Count).ToList();
        var archipelagoLand = FindAboveSeaLevelComponents(archipelago).OrderByDescending(component => component.Count).ToList();

        var pangaeaTotal = pangaeaLand.Sum(component => component.Count);
        Assert.True(pangaeaLand[0].Count >= pangaeaTotal * 0.70,
            $"Largest Pangaea component {pangaeaLand[0].Count} / {pangaeaTotal}; components {pangaeaLand.Count}");
        Assert.True(archipelagoLand.Count >= 10);
        Assert.True(archipelagoLand[0].Count <= archipelagoLand.Sum(component => component.Count) * 0.45);
    }

    [Fact]
    public void WaterWorldHasNoExposedLandAndVariedDepth()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(
            WorldPreset.Standard, 42, MapType: WorldMapType.AllOcean));
        var elevations = AllPositions(world).Select(world.GetElevation).ToArray();

        Assert.All(elevations, elevation => Assert.True(elevation < world.SeaLevel));
        Assert.True(elevations.Max() - elevations.Min() > 0.15f);
        Assert.DoesNotContain(AllPositions(world), position => world.GetTerrain(position) is
            Terrain.Plains or Terrain.Hills or Terrain.Mountain or Terrain.Beach or
            Terrain.Lowlands or Terrain.Canyon or Terrain.Trench);
        Assert.Equal(0, world.VolcanoCount);
        Assert.Equal(0, world.ActiveSpringCount);
    }

    [Fact]
    public void RingWorldIsLongWithoutScalingItsLocalFeaturesByFullWidth()
    {
        Assert.Equal(1280, WorldPreset.Ring.Width);
        Assert.Equal(40, WorldPreset.Ring.Height);
        Assert.Equal(WorldPreset.Ring.Width, WorldPreset.Huge.Width);

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
        Assert.Equal(4, world.TeleporterCount);
        for (var index = 0; index < 4; index++)
        {
            var x = world.Width * (index * 2 + 1) / 8;
            Assert.True(world.HasTeleporter(new GridPosition(x, world.Height / 2)));
        }
        Assert.True(SimulationWorld.RingWorldWallElevation > SimulationWorld.MaximumGroundElevation);
    }

    [Fact]
    public void RingWorldWallsDoNotCreateShallowsAlongTheirEdges()
    {
        var world = new SimulationWorld(8, 5, Terrain.DeepOcean, seed: 9)
        {
            Body = WorldBody.RingWorld,
            OceanSeed = new GridPosition(0, 2),
        };
        foreach (var position in AllPositions(world))
        {
            world.SetElevation(position, -0.1f);
        }

        TerrainClassifier.RebuildAll(world);

        for (var x = 0; x < world.Width; x++)
        {
            Assert.NotEqual(Terrain.Shallows, world.GetTerrain(new GridPosition(x, 1)));
            Assert.NotEqual(Terrain.Shallows, world.GetTerrain(new GridPosition(x, world.Height - 2)));
        }
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
    public void SmallGeneratedIslandsAreNotEntirelyMountainSummits()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Micro, Seed: seed));
            foreach (var island in FindAboveSeaLevelComponents(world).Where(component => component.Count <= 20))
            {
                Assert.Contains(island, position => world.GetElevation(position) <= 0.58f);
            }
        }
    }

    [Fact]
    public void NaturalSpringsStartOnHillsOrMountains()
    {
        var world = WorldGenerator.Generate(new WorldGenerationOptions(WorldPreset.Micro, Seed: 42));
        var sources = AllPositions(world)
            .Where(position => world.GetSurfaceWater(position) is SurfaceWaterKind.River)
            .ToList();

        Assert.NotEmpty(sources);
        Assert.Equal(sources.Count, world.ActiveSpringCount);
        foreach (var source in sources)
        {
            Assert.True(world.GetTerrain(source) is Terrain.Hills or Terrain.Mountain);
        }
    }

    [Theory]
    [InlineData(80, 48, 6)]
    [InlineData(160, 96, 14)]
    [InlineData(320, 192, 24)]
    [InlineData(640, 311, 24)]
    [InlineData(1280, 642, 24)]
    public void NaturalRiverTargetsAreDoubledForEveryMapSize(
        int width,
        int height,
        int expectedCount)
    {
        Assert.Equal(expectedCount, WorldGenerator.GetNaturalSpringTargetCount(width, height));
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

    private static IEnumerable<List<GridPosition>> FindAboveSeaLevelComponents(SimulationWorld world)
    {
        var visited = new HashSet<GridPosition>();
        GridPosition[] directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
        foreach (var start in AllPositions(world))
        {
            if (world.GetElevation(start) <= world.SeaLevel || !visited.Add(start))
            {
                continue;
            }

            var component = new List<GridPosition>();
            var queue = new Queue<GridPosition>();
            queue.Enqueue(start);
            while (queue.TryDequeue(out var current))
            {
                component.Add(current);
                foreach (var direction in directions)
                {
                    var y = current.Y + direction.Y;
                    if (y < 0 || y >= world.Height)
                    {
                        continue;
                    }

                    var neighbor = new GridPosition(
                        (current.X + direction.X + world.Width) % world.Width,
                        y);
                    if (world.GetElevation(neighbor) > world.SeaLevel && visited.Add(neighbor))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }

            yield return component;
        }
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
