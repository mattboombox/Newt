using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class NexusTests
{
    [Fact]
    public void PetalTerrainIsDeterministicAndVariesBetweenArmsAndSeeds()
    {
        var world = new SimulationWorld(192, 192, Terrain.Plains) { Body = WorldBody.Nexus };
        var first = WorldGenerator.GenerateNexusElevations(world, 731);
        Assert.Equal(first, WorldGenerator.GenerateNexusElevations(world, 731));
        var second = WorldGenerator.GenerateNexusElevations(world, 19);
        var profiles = new List<double[]>();
        for (var arm = 0; arm < NexusLayout.ArmCount; arm++)
        {
            var profile = new List<double>();
            for (var step = 55; step <= 90; step++)
            {
                var tile = NexusLayout.ArmPosition(world, arm, step / 100f);
                Assert.Equal(arm, NexusLayout.ArmIndex(world, tile));
                var index = tile.Y * world.Width + tile.X;
                profile.Add(first[index]);
                Assert.NotEqual(first[index], second[index]);
            }
            Assert.True(profile.Max() - profile.Min() > 0.01);
            Assert.All(profiles, previous => Assert.False(previous.SequenceEqual(profile)));
            profiles.Add(profile.ToArray());
        }
    }

    [Fact]
    public void IndependentPetalsStillRespectRequestedLandCoverage()
    {
        double LandCoverage(double fraction)
        {
            var world = WorldGenerator.Generate(new(new("Test Nexus", 192, 192), 731,
                LandFraction: fraction, MapType: WorldMapType.Nexus));
            var elevations = new List<float>();
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                if (!NexusLayout.IsWall(world, new(x, y)))
                    elevations.Add(world.GetElevation(new(x, y)));
            return elevations.Count(value => value >= 0) / (double)elevations.Count;
        }
        Assert.InRange(LandCoverage(0.3), 0.27, 0.33);
        Assert.InRange(LandCoverage(0.7), 0.67, 0.73);
    }

    [Fact]
    public void StructuralWallsPreserveCustodiansAndRemoveOtherOccupants()
    {
        var world = new SimulationWorld(20, 20, Terrain.Plains) { Body = WorldBody.Nexus };
        var apeTile = NexusLayout.Center(world);
        var custodianTile = new GridPosition(apeTile.X, apeTile.Y - 1);
        var ape = world.AddCritter(CritterSpecies.Ape, apeTile);
        var custodian = world.AddCritter(CritterSpecies.Custodian, custodianTile);
        world.SetStructuralTerrain(new(0, 0), Terrain.MegaWall, SimulationWorld.MegaWallElevation);
        Assert.Equal(2, world.CritterCount);
        world.SetStructuralTerrain(apeTile, Terrain.MegaWall, SimulationWorld.MegaWallElevation);
        world.SetStructuralTerrain(custodianTile, Terrain.MegaWall, SimulationWorld.MegaWallElevation);
        Assert.False(world.TryGetCritter(ape, out _));
        Assert.True(world.TryGetCritter(custodian, out var survivor));
        Assert.Equal(custodianTile, survivor.Position);
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Ape));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Custodian));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void FreshwaterChangesNexusMoistureAndBiome(SurfaceWaterKind water)
    {
        var world = new SimulationWorld(384, 384, Terrain.Plains, seed: 731) { Body = WorldBody.Nexus };
        var target = NexusLayout.ArmPosition(world, 8);
        world.SetTemperature(target, 0.84f);
        world.SetElevation(target, 0.1f);
        ClimateSystem.RebuildMoistureAndBiomes(world);
        var before = world.GetMoisture(target);
        Assert.Equal(Biome.Desert, world.GetBiome(target));
        world.SetSurfaceWater(target, water);
        ClimateSystem.RebuildMoistureAndBiomes(world);
        Assert.True(world.GetMoisture(target) > before + 0.4f);
        Assert.NotEqual(Biome.Desert, world.GetBiome(target));
        world.SetSurfaceWater(target, SurfaceWaterKind.None);
        ClimateSystem.RebuildMoistureAndBiomes(world);
        Assert.Equal(before, world.GetMoisture(target), 5);
        Assert.Equal(Biome.Desert, world.GetBiome(target));
    }

    [Fact]
    public void TemperatureBlendsAtPetalBoundariesIncludingAngularSeam()
    {
        var world = new SimulationWorld(384, 384, Terrain.Plains) { Body = WorldBody.Nexus };
        var center = NexusLayout.Center(world);
        GridPosition Boundary(float degrees)
        {
            var angle = degrees * MathF.PI / 180 - MathF.PI / 2;
            var radius = NexusLayout.Radius(world) * 0.33f;
            return new(center.X + (int)MathF.Round(MathF.Cos(angle) * radius),
                center.Y + (int)MathF.Round(MathF.Sin(angle) * radius));
        }
        Assert.InRange(NexusLayout.Climate(world, Boundary(18)).Temperature, 0.12f, 0.20f);
        Assert.InRange(NexusLayout.Climate(world, Boundary(342)).Temperature, 0.30f, 0.60f);
        Assert.Equal(0.06f, NexusLayout.Climate(world, NexusLayout.ArmPosition(world, 0)).Temperature, 4);
        Assert.Equal(0.84f, NexusLayout.Climate(world, NexusLayout.ArmPosition(world, 9)).Temperature, 4);
    }

    [Fact]
    public void CenterElevationVariesWithSeedInsteadOfUsingAFixedOceanBowl()
    {
        SimulationWorld Generate(ulong seed) => WorldGenerator.Generate(new(new("Test Nexus", 96, 96),
            seed, MapType: WorldMapType.Nexus));
        var first = Generate(19);
        var second = Generate(731);
        Assert.NotEqual(first.GetElevation(NexusLayout.Center(first)), second.GetElevation(NexusLayout.Center(second)));
        Assert.True(first.HasTeleporter(NexusLayout.Center(first)));
        Assert.True(second.HasTeleporter(NexusLayout.Center(second)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NexusHasTenClimatePetalsCentralPortalAndOppositeFabricators(bool mega)
    {
        var world = WorldGenerator.Generate(new(mega ? WorldPreset.NexusMega : WorldPreset.Nexus,
            731, MapType: WorldMapType.Nexus));
        Assert.Equal(WorldBody.Nexus, world.Body);
        Assert.False(world.SeasonsEnabled);
        SeasonSystem.SetEnabled(world, true);
        Assert.False(world.SeasonsEnabled);
        var center = NexusLayout.Center(world);
        Assert.True(world.HasTeleporter(center));
        var expected = new[] { TemperatureBand.Freezing, TemperatureBand.Cold, TemperatureBand.Cold, TemperatureBand.Cold,
            TemperatureBand.Temperate, TemperatureBand.Temperate, TemperatureBand.Temperate,
            TemperatureBand.Hot, TemperatureBand.Hot, TemperatureBand.Hot };
        for (var arm = 0; arm < NexusLayout.ArmCount; arm++)
        {
            var position = NexusLayout.ArmPosition(world, arm);
            Assert.True(world.HasTeleporter(position));
            Assert.Equal(expected[arm], world.GetTemperatureBand(position));
        }
        Assert.Equal(11, world.TeleporterCount);
        var terrainKinds = new HashSet<Terrain>();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (NexusLayout.Contains(world, new(x, y))) terrainKinds.Add(world.GetTerrain(new(x, y)));
        Assert.Contains(Terrain.Hills, terrainKinds);
        Assert.Contains(Terrain.Mountain, terrainKinds);
        Assert.Contains(Terrain.Ocean, terrainKinds);
        Assert.Contains(Terrain.Shallows, terrainKinds);
        Assert.Equal(mega ? 2 : 1, world.CustodianFabricators.Count);
        Assert.Equal(mega ? 10 : 5, world.GetCritterCount(CritterSpecies.Custodian));
        Assert.All(world.CustodianFabricators, position =>
        {
            Assert.True(NexusLayout.Contains(world, position));
            Assert.Equal(Terrain.MegaWall, world.GetTerrain(position));
        });
        if (mega)
        {
            Assert.Equal(world.CustodianFabricators[0].X, world.CustodianFabricators[1].X);
            Assert.Equal(2 * center.Y, world.CustodianFabricators.Sum(position => position.Y));
        }
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (NexusLayout.IsWall(world, new(x, y)))
                Assert.Equal(Terrain.MegaWall, world.GetTerrain(new(x, y)));
        Assert.False(world.TrySpawnCritter(CritterSpecies.Ape, new(0, 0)));
        Assert.False(world.TryAddCritter(CritterSpecies.Custodian, new(0, 0)));
    }

    [Fact]
    public void NexusWallsSurviveClimateRebuildAndPortalRemovalIsRepairable()
    {
        var world = WorldGenerator.Generate(new(new("Test Nexus", 96, 96), 19, MapType: WorldMapType.Nexus));
        var wall = world.CustodianFabricator!.Value;
        world.SetElevation(wall, -0.5f);
        TerrainClassifier.RebuildAll(world);
        Assert.Equal(Terrain.MegaWall, world.GetTerrain(wall));
        Assert.Equal(SimulationWorld.MegaWallElevation, world.GetElevation(wall));
        var portal = NexusLayout.ArmPosition(world, 3);
        Assert.True(world.RemoveTeleporterAt(portal));
        Assert.True(world.TryPlaceTeleporter(portal));
    }
}
