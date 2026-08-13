using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SimulationWorldTests
{
    [Fact]
    public void PlanktonRecoverySeedsOneDeepOceanCritterAndRestoresExtinction()
    {
        var world = new SimulationWorld(12, 8, Terrain.DeepOcean, seed: 91);

        Assert.True(world.EnablePlanktonRecovery());
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        var first = world.GetCritter(0).Position;
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(first));

        Assert.True(world.RemoveCritterAt(first));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));

        world.AdvanceOneTick();

        Assert.Equal(1, world.CritterCount);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(world.GetCritter(0).Position));
    }

    [Fact]
    public void SameSeedProducesSameMovement()
    {
        var first = CreateOceanWorld(42);
        var second = CreateOceanWorld(42);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
        }

        Assert.Equal(first.GetCritter(0), second.GetCritter(0));
    }

    [Fact]
    public void OccupiedTileRejectsAnotherCritter()
    {
        var world = CreateOceanWorld(1);

        Assert.Throws<InvalidOperationException>(() =>
            world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2)));
    }

    [Fact]
    public void PlanktonUsesItsFiveSecondMovementInterval()
    {
        var world = CreateOceanWorld(7);
        var initialPosition = world.GetCritter(0).Position;

        for (var tick = 0; tick < 99; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(initialPosition, world.GetCritter(0).Position);
        world.AdvanceOneTick();
        Assert.NotEqual(initialPosition, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(Terrain.DeepOcean)]
    [InlineData(Terrain.Ocean)]
    [InlineData(Terrain.Shallows)]
    public void OceanDwellerPresetAllowsSaltwaterTerrain(Terrain terrain)
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.OceanDweller,
            terrain,
            SurfaceWaterKind.None));
    }

    [Theory]
    [InlineData(Terrain.Shallows)]
    [InlineData(Terrain.Trench)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Plains)]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Ice)]
    public void LandDwellerPresetAllowsConfiguredTerrainAndRivers(Terrain terrain)
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.None));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.River));
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.FreshwaterLake));
    }

    [Fact]
    public void FreshwaterDwellerPresetRequiresRiverOrLake()
    {
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.None));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.River));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.FreshwaterLake));
    }

    [Theory]
    [InlineData(CritterHabitat.OceanDweller, Terrain.Ocean)]
    [InlineData(CritterHabitat.LandDweller, Terrain.Plains)]
    [InlineData(CritterHabitat.FreshwaterDweller, Terrain.Plains)]
    public void LavaBlocksAllNonFlierHabitats(CritterHabitat habitat, Terrain terrain)
    {
        var water = habitat is CritterHabitat.FreshwaterDweller
            ? SurfaceWaterKind.River
            : SurfaceWaterKind.None;

        Assert.False(CritterHabitats.CanOccupy(
            habitat,
            terrain,
            water,
            Biome.None,
            SurfaceCover.Lava));
    }

    [Fact]
    public void FliersIgnoreLavaAndAllTerrainExceptSnowyMountains()
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Trench,
            SurfaceWaterKind.FreshwaterLake,
            Biome.None,
            SurfaceCover.Lava));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Mountain,
            SurfaceWaterKind.None,
            Biome.Desert,
            SurfaceCover.Lava));
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Mountain,
            SurfaceWaterKind.None,
            Biome.Arctic,
            SurfaceCover.None));
    }

    private static SimulationWorld CreateOceanWorld(ulong seed)
    {
        var world = new SimulationWorld(8, 8, Terrain.Ocean, seed);
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2));
        return world;
    }
}
