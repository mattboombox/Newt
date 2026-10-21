using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTrafficTests
{
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
    [InlineData(CritterSpecies.ApeWarrior, CritterSpecies.ApeWarrior)]
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
    public void SwapPushesLandCivilianIntoOceanWithPenaltyAndPreservesSpecies()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.SetTerrain(new(4, 5), Terrain.Ocean);
        world.SetTerrain(new(5, 5), Terrain.Shallows);
        world.AddCritter(CritterSpecies.ApeTraderSailor, new(4, 5));
        world.AddCritter(CritterSpecies.ApeFarmer, new(5, 5));
        Assert.True(Swap(world));
        Assert.Equal(new GridPosition(4, 5), world.GetCritter(1).Position);
        Assert.Equal(CritterSpecies.ApeFarmer, world.GetCritter(1).Species);
        var schedules = (long[])typeof(SimulationWorld).GetField("_nextMovementTicks",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var delay = 4 * SimulationWorld.GetMovementIntervalTicks(CritterSpecies.ApeFarmer);
        Assert.True(schedules[1] >= world.Tick + delay);
        for (var tick = 0; tick < delay; tick++)
            world.AdvanceOneTick();
        Assert.Equal(CritterSpecies.ApeFarmer, world.GetCritter(1).Species);
        Assert.NotEqual(Terrain.Ocean, world.GetTerrain(world.GetCritter(1).Position));
    }

    [Fact]
    public void SwapNeverPushesApeOntoRingWall()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains);
        world.SetTerrain(new(4, 5), Terrain.MegaWall);
        Assert.True(world.TrySpawnCritter(CritterSpecies.ApeTrader, new(4, 5)));
        world.AddCritter(CritterSpecies.Ape, new(5, 5));
        Assert.False(Swap(world));
    }

    private static bool Swap(SimulationWorld world, IReadOnlySet<GridPosition>? reserved = null) =>
        (bool)typeof(SimulationWorld).GetMethod("TrySwapPastMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, reserved])!;
}
