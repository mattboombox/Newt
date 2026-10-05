using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SimulationSpeedGuardTests
{
    [Fact]
    public void AutoSpeedIsEnabledByDefault() => Assert.True(new SimulationSpeedGuard().Enabled);

    [Theory]
    [InlineData(15_000, 0)]
    [InlineData(100, 250)]
    [InlineData(15_000, 250)]
    public void EitherPopulationLimitImmediatelyEngagesSlowdown(int critters, int apes)
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(1, guard.GetRate(32, critters, apes));
        Assert.True(guard.AutomaticallyReduced);
        Assert.Equal(1, guard.GetRate(1, critters, apes));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    public void MaximumSpeedReturnsOnlyWhenBothPopulationsDropBelowLimits(double rate)
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(1, guard.GetRate(rate, 15_000, 250));
        Assert.Equal(1, guard.GetRate(1, 14_999, 250));
        Assert.Equal(1, guard.GetRate(1, 15_000, 249));
        Assert.Equal(32, guard.GetRate(1, 14_999, 249));
        Assert.False(guard.AutomaticallyReduced);
        Assert.Equal(32, guard.GetRate(32, 14_999, 249));
    }

    [Theory]
    [InlineData(1)]
    public void AutoSpeedSelectsNormalSpeedAboveLimitEvenFromLowerManualRate(double rate)
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(1, guard.GetRate(rate, 20_000, 500));
        Assert.True(guard.AutomaticallyReduced);
    }

    [Theory]
    [InlineData(0.125)]
    [InlineData(0.25)]
    [InlineData(0.5)]
    public void PopulationSlowdownNeverIncreasesAnAlreadySlowRate(double rate)
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(rate, guard.GetRate(rate, 15_000, 0));
        Assert.Equal(rate, guard.GetRate(rate, 0, 250));
        Assert.False(guard.AutomaticallyReduced);
        Assert.Equal(32, guard.GetRate(rate, 14_999, 249));
        guard.Enabled = false;
        Assert.Equal(rate, guard.GetRate(32, 14_999, 249));
    }

    [Fact]
    public void DisablingGuardRestoresPreviousRate()
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(1, guard.GetRate(32, 15_000, 0));
        guard.Enabled = false;
        Assert.Equal(32, guard.GetRate(1, 15_000, 0));
        Assert.False(guard.AutomaticallyReduced);
    }

    [Fact]
    public void ManualSpeedResetDiscardsPreviousRate()
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(1, guard.GetRate(32, 15_000, 0));
        guard.Reset();
        Assert.Equal(1, guard.GetRate(8, 15_000, 0));
        Assert.Equal(32, guard.GetRate(1, 14_999, 0));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(8)]
    public void EnablingAutoSpeedImmediatelyMaxesSpeedBelowLimits(double rate)
    {
        var guard = new SimulationSpeedGuard();
        Assert.Equal(32, guard.GetRate(rate, 14_999, 249));
        Assert.False(guard.AutomaticallyReduced);
        guard.Enabled = false;
        Assert.Equal(rate, guard.GetRate(32, 14_999, 249));
        Assert.Equal(rate, guard.GetRate(rate, 14_999, 249));
    }

    [Fact]
    public void ApeCountIncludesRolesAndTradersButExcludesBarbariansAndOtherAnimals()
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains, seed: 17);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnBarbarianApeVillage(new(15, 10)));
        Assert.Equal(0, world.NonBarbarianApeCount);
        var species = new[] { CritterSpecies.Ape, CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack,
            CritterSpecies.ApeWarrior, CritterSpecies.ApeChieftain, CritterSpecies.ApeScholar,
            CritterSpecies.ApeSailor, CritterSpecies.ApeTrader, CritterSpecies.ApeTraderSailor };
        for (var i = 0; i < species.Length; i++)
        {
            var position = new GridPosition(i, 0);
            world.SetTerrain(position, Terrain.Shallows);
            world.AddCritter(species[i], position);
        }
        world.AddCritter(CritterSpecies.Deer, new(0, 1));
        Assert.Equal(species.Length, world.NonBarbarianApeCount);
        world.RemoveCritterAt(new(0, 0));
        Assert.Equal(species.Length - 1, world.NonBarbarianApeCount);
    }
}
