using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTrafficTests
{
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
    public void SwapDoesNotPushLandCivilianIntoOcean()
    {
        var world = new SimulationWorld(10, 10, Terrain.Plains, seed: 1);
        world.SetTerrain(new(4, 5), Terrain.Ocean);
        world.SetTerrain(new(5, 5), Terrain.Shallows);
        world.AddCritter(CritterSpecies.ApeTraderSailor, new(4, 5));
        world.AddCritter(CritterSpecies.ApeFarmer, new(5, 5));
        Assert.False(Swap(world));
    }

    private static bool Swap(SimulationWorld world, IReadOnlySet<GridPosition>? reserved = null) =>
        (bool)typeof(SimulationWorld).GetMethod("TrySwapPastMovementBlocker",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, [0, 55, reserved])!;
}
