using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ImpactTests
{
    [Fact]
    public void MeteorCratersCrossEveryPlanetoidFaceEdgeWithoutHittingDistantAtlasNeighbors()
    {
        const int size = 24;
        GridPosition[] directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];
        for (var face = 0; face < 6; face++)
        for (var edge = 0; edge < 4; edge++)
        {
            var world = new SimulationWorld(3 * size, 2 * size, Terrain.Plains)
                { Body = WorldBody.Planetoid, HasOceans = false };
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetElevation(new(x, y), 0.3f);
            var local = edge switch
            {
                0 => new GridPosition(size / 2, 0),
                1 => new GridPosition(size - 1, size / 2),
                2 => new GridPosition(size / 2, size - 1),
                _ => new GridPosition(0, size / 2),
            };
            var center = new GridPosition(face % 3 * size + local.X, face / 3 * size + local.Y);
            var direction = directions[edge];
            var neighbor = PlanetoidLayout.Offset(center, direction.X, direction.Y, size);
            Impacts.CreateMeteorImpact(world, center, 0f);
            Assert.True(world.GetElevation(neighbor) < 0.3f, $"Crater did not cross face {face}, edge {edge}.");
            Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(neighbor));
            var originDirection = world.GetPlanetoidTileDirection(center);
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
            {
                var tile = new GridPosition(x, y);
                var angle = MathF.Acos(Math.Clamp(System.Numerics.Vector3.Dot(originDirection,
                    world.GetPlanetoidTileDirection(tile)), -1f, 1f));
                if (angle * size / (MathF.PI / 2) <= 6) continue;
                Assert.Equal(0.3f, world.GetElevation(tile));
                Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(tile));
            }
        }
    }

    [Fact]
    public void ToolMeteorAtTwoDoublesMaximumNormalCraterAndShockRadius()
    {
        var center = new GridPosition(30, 30);
        var normal = Impacts.CreateMeteorImpact(CreateLandWorld(), center, 1f);
        var extended = Impacts.CreateToolMeteorImpact(CreateLandWorld(), center, 2f);
        Assert.Equal(2f, extended.Magnitude);
        Assert.Equal(normal.CraterRadius * 2, extended.CraterRadius);
        Assert.Equal(normal.ShockRadius * 2, extended.ShockRadius);
    }

    [Fact]
    public void ExtendedToolMeteorAddsMoreImpactMeltFlows()
    {
        var normal = CreateLandWorld();
        var extended = CreateLandWorld();
        var center = new GridPosition(30, 30);
        Impacts.CreateMeteorImpact(normal, center, 1f);
        Impacts.CreateToolMeteorImpact(extended, center, 2f);
        Assert.Equal(5, normal.ActiveLavaFlowCount);
        Assert.Equal(10, extended.ActiveLavaFlowCount);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    [InlineData(1f)]
    public void ToolMeteorPreservesNormalScale(float magnitude)
    {
        var center = new GridPosition(30, 30);
        Assert.Equal(Impacts.CreateMeteorImpact(CreateLandWorld(), center, magnitude),
            Impacts.CreateToolMeteorImpact(CreateLandWorld(), center, magnitude));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(2.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ToolMeteorRejectsMagnitudesOutsideExtendedScale(float magnitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Impacts.CreateToolMeteorImpact(CreateLandWorld(), new(30, 30), magnitude));
    }

    [Fact]
    public void MeteorExcavatesBowlAndRaisesStoneRim()
    {
        var world = CreateLandWorld();
        var center = new GridPosition(30, 30);
        var result = Impacts.CreateMeteorImpact(world, center, 0.5f);
        var rim = new GridPosition(center.X + result.CraterRadius, center.Y);

        Assert.True(world.GetElevation(center) < 0.2f);
        Assert.True(world.GetElevation(rim) > 0.2f);
        Assert.True(world.GetSurfaceCover(center) is SurfaceCover.Stone or SurfaceCover.Lava);
        Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(rim));
    }

    [Fact]
    public void MagnitudeControlsCraterShockAndFragmentScale()
    {
        var small = Impacts.CreateMeteorImpact(CreateLandWorld(), new GridPosition(30, 30), 0f);
        var catastrophic = Impacts.CreateMeteorImpact(CreateLandWorld(), new GridPosition(30, 30), 1f);

        Assert.True(catastrophic.CraterRadius > small.CraterRadius);
        Assert.True(catastrophic.ShockRadius > small.ShockRadius);
        Assert.True(catastrophic.FragmentCount > small.FragmentCount);
    }

    [Fact]
    public void LargeComplexCraterHasCentralPeakAboveFloor()
    {
        var world = CreateLandWorld();
        var center = new GridPosition(30, 30);
        var result = Impacts.CreateMeteorImpact(world, center, 0.8f);
        var floor = new GridPosition(center.X + Math.Max(1, result.CraterRadius / 2), center.Y);

        Assert.True(world.GetElevation(center) > world.GetElevation(floor));
    }

    [Fact]
    public void ImpactMeltUsesAnimatedLavaFlows()
    {
        var world = CreateLandWorld();

        Impacts.CreateMeteorImpact(world, new GridPosition(30, 30), 0.4f);

        Assert.True(world.ActiveLavaFlowCount > 0);
        Assert.True(CountCover(world, SurfaceCover.Lava) > 0);
    }

    [Fact]
    public void MeteorDestroysWolfDenAndStoredCharges()
    {
        var world = CreateLandWorld();
        var center = new GridPosition(30, 30);
        Assert.True(world.AddWolfDenCharge(center));
        Assert.True(world.AddWolfDenCharge(center));

        Impacts.CreateMeteorImpact(world, center, 0f);

        Assert.Null(world.GetWolfDenCharges(center));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt, Terrain.Plains)]
    [InlineData(CritterSpecies.Plankton, Terrain.Ocean)]
    [InlineData(CritterSpecies.Jellyfish, Terrain.Ocean)]
    [InlineData(CritterSpecies.ApeSailor, Terrain.Ocean)]
    public void MeteorImmediatelyRemovesCritterAtImpactTile(
        CritterSpecies species,
        Terrain terrain)
    {
        var world = new SimulationWorld(9, 9, terrain, seed: 731);
        var center = new GridPosition(4, 4);
        world.AddCritter(species, center);

        Impacts.CreateMeteorImpact(world, center, 0f);

        Assert.Equal(0, world.CritterCount);
        Assert.False(world.IsOccupied(center));
    }

    [Fact]
    public void ShockwaveExpandsBeforeRemovingDistantCritter()
    {
        var world = CreateLandWorld();
        var center = new GridPosition(30, 30);
        var critterPosition = new GridPosition(40, 30);
        world.AddCritter(CritterSpecies.Monkey, critterPosition);

        Impacts.CreateMeteorImpact(world, center, 0.5f);
        Assert.Equal(1, world.CritterCount);

        for (var tick = 0; tick < 20 && world.ActiveImpactWaveCount > 0; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.CritterCount);
    }

    [Fact]
    public void PlanetoidWavesCrossPhysicalFaceSeams()
    {
        AssertPlanetoidWaveCrossesSeam(WaveKind.Impact);
        AssertPlanetoidWaveCrossesSeam(WaveKind.Tsunami);
    }

    private static void AssertPlanetoidWaveCrossesSeam(WaveKind kind)
    {
        const int size = 18;
        var world = new SimulationWorld(3 * size, 2 * size, Terrain.Plains) { Body = WorldBody.Planetoid };
        var center = new GridPosition(size / 2, size - 1);
        var acrossSeam = PlanetoidLayout.Offset(center, 0, 1, size);
        var acrossAtlasEdge = new GridPosition(center.X, center.Y + 1);
        Assert.NotEqual(acrossAtlasEdge, acrossSeam);
        world.SetElevation(acrossSeam, 0.3f);
        if (kind is WaveKind.Tsunami)
        {
            world.OceanSeed = center;
            world.SetElevation(center, -0.3f);
            world.SetTerrain(center, Terrain.Ocean);
            Assert.True(Tsunamis.Create(world, center, 0f));
        }
        else
        {
            Impacts.CreateMeteorImpact(world, center, 0f);
        }

        // Isolate wave propagation from the initial crater, which now correctly
        // excavates and clears this adjacent tile across the seam.
        world.SetSurfaceCover(acrossSeam, SurfaceCover.None, 0);
        var critter = world.AddCritter(CritterSpecies.Monkey, acrossSeam);
        var firstFront = world.GetImpactWave(0) with { CurrentRadius = 1f };
        Assert.True(Impacts.IsOnShockFront(world, acrossSeam, firstFront));
        Assert.False(Impacts.IsOnShockFront(world, acrossAtlasEdge, firstFront));
        Assert.True(world.TryGetCritter(critter, out _));
        for (var tick = 0; tick < 3; tick++)
            Impacts.Advance(world);
        Assert.False(world.TryGetCritter(critter, out _));
        if (kind is WaveKind.Tsunami)
            Assert.True(world.GetElevation(acrossSeam) < 0.3f);
    }

    [Fact]
    public void ShockwaveDoesNotStripDistantLandToStone()
    {
        var world = CreateLandWorld();
        var center = new GridPosition(30, 30);
        var result = Impacts.CreateMeteorImpact(world, center, 0.5f);
        var distant = new GridPosition(center.X + (int)MathF.Ceiling(result.CraterRadius * 2.6f), center.Y);

        while (world.ActiveImpactWaveCount > 0)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(distant));
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(2f)]
    public void MagnitudeMustStayBetweenZeroAndOne(float magnitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Impacts.CreateMeteorImpact(CreateLandWorld(), new GridPosition(30, 30), magnitude));
    }

    private static SimulationWorld CreateLandWorld()
    {
        var world = new SimulationWorld(61, 61, Terrain.Plains, seed: 73);
        world.OceanSeed = new GridPosition(0, 0);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.2f);
            }
        }
        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static int CountCover(SimulationWorld world, SurfaceCover cover)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetSurfaceCover(new GridPosition(x, y)) == cover)
                {
                    count++;
                }
            }
        }
        return count;
    }
}
