using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ShipwreckTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Tile = new(3, 3);

    [Theory]
    [InlineData(CritterSpecies.BaleenWhale)]
    [InlineData(CritterSpecies.ToothedWhale)]
    public void WhaleFallReplacesWreckAndPreventsLaterWrecks(CritterSpecies whale)
    {
        var world = new SimulationWorld(10, 10, Terrain.Ocean);
        world.AddCritter(CritterSpecies.ApeSailor, Tile);
        world.RemoveCritterAt(Tile);
        Assert.Equal(ShipwreckKind.Ship, world.GetShipwreck(Tile));
        world.AddCritter(whale, Tile);
        world.RemoveCritterAt(Tile);
        Assert.Null(world.GetShipwreck(Tile));
        Assert.Equal(10, world.GetWhaleFallNutrition(Tile));
        world.AddCritter(CritterSpecies.ApeSailor, Tile);
        world.RemoveCritterAt(Tile);
        Assert.Null(world.GetShipwreck(Tile));
        Assert.Equal(10, world.GetWhaleFallNutrition(Tile));
        world.AddCritter(whale, Tile);
        world.RemoveCritterAt(Tile);
        Assert.Equal(20, world.GetWhaleFallNutrition(Tile));
        Assert.Null(world.GetShipwreck(Tile));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeRoyalGuard)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeKing)]
    [InlineData(CritterSpecies.ApeQueen)]
    [InlineData(CritterSpecies.ApeBomber)]
    public void BoatRolesLeaveFlatWreckStateInOceanAndLake(CritterSpecies species)
    {
        foreach (var lake in new[] { false, true })
        {
            var world = new SimulationWorld(10, 10, Terrain.Plains);
            Assert.True(world.TrySpawnCritter(species, Tile));
            if (lake) world.SetSurfaceWater(Tile, SurfaceWaterKind.FreshwaterLake);
            else world.SetTerrain(Tile, Terrain.Ocean);
            Assert.True(world.RemoveCritterAt(Tile));
            Assert.Equal(ShipwreckKind.Ship, world.GetShipwreck(Tile));
            typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, SimulationWorld.ApeRuinDecayTicks - 1);
            Assert.Equal(ShipwreckKind.Ship, world.GetShipwreck(Tile));
            typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, SimulationWorld.ApeRuinDecayTicks);
            typeof(SimulationWorld).GetMethod("AdvanceShipwrecks", Private)!.Invoke(world, null);
            Assert.Null(world.GetShipwreck(Tile));
        }
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.Vampire)]
    [InlineData(CritterSpecies.Dog)]
    [InlineData(CritterSpecies.Custodian)]
    public void NonBoatCrittersDoNotLeaveWrecks(CritterSpecies species)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        world.AddCritter(species, Tile);
        world.SetTerrain(Tile, Terrain.Ocean);
        world.RemoveCritterAt(Tile);
        Assert.Null(world.GetShipwreck(Tile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SailorOnLandOrRiverDoesNotLeaveWreck(bool river)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        world.TrySpawnCritter(CritterSpecies.ApeSailor, Tile);
        if (river) world.SetSurfaceWater(Tile, SurfaceWaterKind.River);
        world.RemoveCritterAt(Tile);
        Assert.Null(world.GetShipwreck(Tile));
    }

    [Fact]
    public void RiverOverlayOnCoastalTerrainDoesNotLeaveWreck()
    {
        var world = new SimulationWorld(10, 10, Terrain.Shallows);
        world.AddCritter(CritterSpecies.ApeSailor, Tile);
        world.SetSurfaceWater(Tile, SurfaceWaterKind.River);
        world.RemoveCritterAt(Tile);
        Assert.Null(world.GetShipwreck(Tile));
    }

    [Fact]
    public void BarbarianBoatLeavesPirateWreckBeforeVillageMembershipIsRemoved()
    {
        var world = new SimulationWorld(30, 30, Terrain.Plains);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var camp = new GridPosition(15, 15);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var ape = world.AddCritter(CritterSpecies.ApeWarrior, Tile);
        Assert.True(world.TryAssignApeToVillage(ape, camp));
        world.SetTerrain(Tile, Terrain.Ocean);
        world.RemoveCritterAt(Tile);
        Assert.Equal(ShipwreckKind.Pirate, world.GetShipwreck(Tile));
    }

    [Fact]
    public void ColonistLeavesWreckAndStarvationUsesTheSameDeathPath()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        var colonist = world.AddCritter(CritterSpecies.Ape, Tile);
        var routes = (Dictionary<int, (int OriginVillageTile, int TargetVillageTile)>)typeof(SimulationWorld)
            .GetField("_apeSettlerTargets", Private)!.GetValue(world)!;
        routes[colonist.Value] = (0, 99);
        world.SetSurfaceWater(Tile, SurfaceWaterKind.FreshwaterLake);
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energy[0] = 0;
        typeof(SimulationWorld).GetMethod("AdvanceCritterLifecycles", Private)!.Invoke(world, null);
        Assert.Equal(0, world.CritterCount);
        Assert.Equal(ShipwreckKind.Ship, world.GetShipwreck(Tile));
    }
}
