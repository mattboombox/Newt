using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTrafficTests
{
    [Theory]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeRoyalGuard)]
    [InlineData(CritterSpecies.ApeBomber)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeKing)]
    [InlineData(CritterSpecies.ApeQueen)]
    [InlineData(CritterSpecies.ApeTrader)]
    public void FriendlyApeTrafficSwapsWithFarmerEvenWhenShoveSpaceIsAvailable(CritterSpecies moverSpecies)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        var mover = world.AddCritter(moverSpecies, new(4, 5));
        var farmer = world.AddCritter(CritterSpecies.ApeFarmer, new(5, 5));
        SetHomes(world, mover, farmer, sameVillage: true);

        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryShoveMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, null])!);
        // Movement callers finish the step after clearing the blocker. That must
        // preserve the exchanged civilian rather than clearing its new tile.
        typeof(SimulationWorld).GetMethod("MoveCritter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [0, 55, new GridPosition(5, 5), false]);

        Assert.True(world.TryGetCritterAt(new(5, 5), out var advanced));
        Assert.Equal(mover, advanced.Id);
        Assert.True(world.TryGetCritterAt(new(4, 5), out var displaced));
        Assert.Equal(farmer, displaced.Id);
        Assert.Equal(2, world.CritterCount);
    }

    [Fact]
    public void PreferredSwapWorksWithoutAnyEmptyNeighbor()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains);
        var mover = world.AddCritter(CritterSpecies.ApeWarrior, new(0, 0));
        var farmer = world.AddCritter(CritterSpecies.ApeFarmer, new(1, 0));
        world.AddCritter(CritterSpecies.Ape, new(2, 0));
        SetHomes(world, mover, farmer, sameVillage: true);
        Assert.True((bool)typeof(SimulationWorld).GetMethod("CanShoveMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 1, null])!);
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryShoveMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 1, null])!);
        Assert.Equal(new GridPosition(0, 0), world.GetCritter(1).Position);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void PreferredSwapRejectsDifferentVillagesReservedTilesAndUnsafeTerrain(
        bool sameVillage, bool reserved, bool unsafeOrigin)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        var mover = world.AddCritter(CritterSpecies.ApeWarrior, new(4, 5));
        var farmer = world.AddCritter(CritterSpecies.ApeFarmer, new(5, 5));
        SetHomes(world, mover, farmer, sameVillage);
        if (unsafeOrigin) world.SetTerrain(new(4, 5), Terrain.Mountain);
        IReadOnlySet<GridPosition>? reservations = reserved ? new HashSet<GridPosition> { new(5, 5) } : null;
        Assert.False((bool)typeof(SimulationWorld).GetMethod("CanPreferApeSwap",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, reservations])!);
        Assert.Equal(new GridPosition(4, 5), world.GetCritter(0).Position);
        Assert.Equal(new GridPosition(5, 5), world.GetCritter(1).Position);
    }

    private static void SetHomes(SimulationWorld world, CritterId mover, CritterId civilian, bool sameVillage)
    {
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        homes[mover.Value] = 11;
        homes[civilian.Value] = sameVillage ? 11 : 22;
    }

    [Theory]
    [InlineData(Terrain.Ocean, true)]
    [InlineData(Terrain.Mountain, true)]
    [InlineData(Terrain.MegaWall, false)]
    public void ShoveCanUseUnsuitableTerrainExceptRingWalls(Terrain surrounding, bool expected)
    {
        var world = new SimulationWorld(10, 10, surrounding);
        world.SetTerrain(new(4, 5), Terrain.Plains);
        world.SetTerrain(new(5, 5), Terrain.Plains);
        world.AddCritter(CritterSpecies.ApeTrader, new(4, 5));
        var ape = world.AddCritter(CritterSpecies.Ape, new(5, 5));
        var shoved = (bool)typeof(SimulationWorld).GetMethod("TryShoveCritter",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, null])!;
        Assert.Equal(expected, shoved);
        Assert.True(world.TryGetCritter(ape, out var displaced));
        Assert.Equal(CritterSpecies.Ape, displaced.Species);
        Assert.Equal(expected ? surrounding : Terrain.Plains, world.GetTerrain(displaced.Position));
    }

    [Fact]
    public void TraderCannotShoveFishOntoAFloodedMegaWall()
    {
        var world = new SimulationWorld(10, 10, Terrain.MegaWall);
        var origin = new GridPosition(4, 5);
        var blocker = new GridPosition(5, 5);
        var floodedWall = new GridPosition(6, 5);
        world.SetTerrain(origin, Terrain.Plains);
        world.SetTerrain(blocker, Terrain.Shallows);
        world.SetTerrain(floodedWall, Terrain.Plains);
        world.SetSurfaceWater(floodedWall, SurfaceWaterKind.River);
        SetLegacyWall(world, floodedWall);
        world.AddCritter(CritterSpecies.ApeTrader, origin);
        var fish = world.AddCritter(CritterSpecies.Fish, blocker);

        var shoved = (bool)typeof(SimulationWorld).GetMethod("TryShoveCritter",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, null])!;

        Assert.False(shoved);
        Assert.True(world.TryGetCritter(fish, out var displaced));
        Assert.Equal(blocker, displaced.Position);
    }

    [Fact]
    public void PlanktonShoveChainCannotCrossAnOccupiedMegaWall()
    {
        var world = new SimulationWorld(6, 1, Terrain.Ocean);
        world.AddCritter(CritterSpecies.Fish, new(0, 0));
        var blocker = world.AddCritter(CritterSpecies.Plankton, new(1, 0));
        var wall = new GridPosition(2, 0);
        world.AddCritter(CritterSpecies.Plankton, wall);
        SetLegacyWall(world, wall);

        var shoved = (bool)typeof(SimulationWorld).GetMethod("TryShovePlanktonChain",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 1, null])!;

        Assert.False(shoved);
        Assert.True(world.TryGetCritter(blocker, out var plankton));
        Assert.Equal(new GridPosition(1, 0), plankton.Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeFarmer)]
    [InlineData(CritterSpecies.ApeLumberjack)]
    [InlineData(CritterSpecies.ApeScholar)]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void WarriorsCanShoveCivilianApes(CritterSpecies civilian)
        => Assert.True(SimulationWorld.CanDisplace(CritterSpecies.ApeWarrior, civilian));

    [Theory]
    [InlineData(CritterSpecies.ApeWarrior, CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.ApeChieftain)]
    public void MilitaryBlockersCannotBeSwapped(CritterSpecies mover, CritterSpecies blocker)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.AddCritter(mover, new(4, 5));
        world.AddCritter(blocker, new(5, 5));
        Assert.False(Swap(world));
        Assert.Equal(new GridPosition(4, 5), world.GetCritter(0).Position);
        Assert.Equal(new GridPosition(5, 5), world.GetCritter(1).Position);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SwapRespectsBothReservedTiles(bool origin)
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.AddCritter(CritterSpecies.ApeTrader, new(4, 5));
        world.AddCritter(CritterSpecies.ApeFarmer, new(5, 5));
        Assert.False(Swap(world, new HashSet<GridPosition> { new(origin ? 4 : 5, 5) }));
    }

    [Fact]
    public void SwapPushesLandCivilianIntoOceanWhereExposureCanKillIt()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.SetTerrain(new(4, 5), Terrain.Ocean);
        world.SetTerrain(new(5, 5), Terrain.Shallows);
        world.AddCritter(CritterSpecies.ApeTraderSailor, new(4, 5));
        var farmer = world.AddCritter(CritterSpecies.ApeLumberjack, new(5, 5));
        Assert.True(Swap(world));
        Assert.Equal(new GridPosition(4, 5), world.GetCritter(1).Position);
        Assert.Equal(CritterSpecies.ApeLumberjack, world.GetCritter(1).Species);
        var schedules = (long[])typeof(SimulationWorld).GetField("_nextMovementTicks",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var delay = 4 * SimulationWorld.GetMovementIntervalTicks(CritterSpecies.ApeLumberjack);
        Assert.True(schedules[1] >= world.Tick + delay);
        for (var tick = 0; tick < delay; tick++)
            world.AdvanceOneTick();
        Assert.False(world.TryGetCritter(farmer, out _));
    }

    [Fact]
    public void SwapNeverPushesApeOntoRingWall()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        world.AddCritter(CritterSpecies.ApeTrader, new(4, 5));
        SetLegacyWall(world, new(4, 5));
        world.AddCritter(CritterSpecies.Ape, new(5, 5));
        Assert.False(Swap(world));
    }

    private static void SetLegacyWall(SimulationWorld world, GridPosition tile)
    {
        var terrain = (Terrain[])typeof(SimulationWorld).GetField("_terrain",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        terrain[tile.Y * world.Width + tile.X] = Terrain.MegaWall;
    }

    [Fact]
    public void OnlyCustodiansCanSpawnOnMegaWalls()
    {
        var world = new SimulationWorld(3, 3, Terrain.MegaWall);
        var wall = new GridPosition(1, 1);

        foreach (var species in Enum.GetValues<CritterSpecies>().Where(species => species is not CritterSpecies.Custodian))
        {
            Assert.False(world.TrySpawnCritter(species, wall));
            Assert.Throws<InvalidOperationException>(() => world.AddCritter(species, wall));
        }

        Assert.True(world.TrySpawnCritter(CritterSpecies.Custodian, wall));
    }

    [Fact]
    public void StrandedNewtCannotRecoverAcrossMegaWalls()
    {
        var world = new SimulationWorld(5, 3, Terrain.MegaWall);
        var start = new GridPosition(2, 0);
        world.SetTerrain(start, Terrain.Mountain);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Newt, start));

        typeof(SimulationWorld).GetMethod("TryMoveStranded", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [0]);

        Assert.Equal(start, world.GetCritter(0).Position);
    }

    private static bool Swap(SimulationWorld world, IReadOnlySet<GridPosition>? reserved = null) =>
        (bool)typeof(SimulationWorld).GetMethod("TrySwapPastMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, reserved])!;
}
