using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ImpactTests
{
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
