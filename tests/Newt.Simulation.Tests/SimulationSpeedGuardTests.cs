using Newt.Game;

namespace Newt.Simulation.Tests;

public sealed class SimulationSpeedGuardTests
{
    private static readonly TimeSpan Target = TimeSpan.FromSeconds(1d / 60);

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    public void SustainedSlowUpdatesTriggerFallback(double rate)
    {
        var guard = new SimulationSpeedGuard();
        for (var update = 0; update < 3; update++)
        {
            Assert.False(guard.ShouldReduce(rate, TimeSpan.FromMilliseconds(250), Target, false));
        }
        Assert.True(guard.ShouldReduce(rate, TimeSpan.FromMilliseconds(250), Target, false));
    }

    [Theory]
    [InlineData(0.25)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    public void LowerSpeedsAreNeverReduced(double rate)
    {
        var guard = new SimulationSpeedGuard();
        for (var update = 0; update < 20; update++)
        {
            Assert.False(guard.ShouldReduce(rate, TimeSpan.FromSeconds(1), Target, true));
        }
    }

    [Fact]
    public void IsolatedStallDoesNotReduceSpeed()
    {
        var guard = new SimulationSpeedGuard();
        Assert.False(guard.ShouldReduce(32, TimeSpan.FromSeconds(5), Target, true));
        Assert.False(guard.ShouldReduce(32, Target, Target, false));
        for (var update = 0; update < 3; update++)
        {
            Assert.False(guard.ShouldReduce(32, TimeSpan.FromMilliseconds(250), Target, false));
        }
    }

    [Fact]
    public void FrameworkSlowFlagDetectsOverloadEvenWhenIndividualUpdatesAreFast()
    {
        var guard = new SimulationSpeedGuard();
        var reduced = false;
        for (var update = 0; update < 70 && !reduced; update++)
        {
            reduced = guard.ShouldReduce(16, TimeSpan.FromMilliseconds(1), Target, true);
        }
        Assert.True(reduced);
    }

    [Fact]
    public void ResetDiscardsEarlierSlowUpdates()
    {
        var guard = new SimulationSpeedGuard();
        for (var update = 0; update < 3; update++)
        {
            Assert.False(guard.ShouldReduce(32, TimeSpan.FromMilliseconds(250), Target, false));
        }
        guard.Reset();
        Assert.False(guard.ShouldReduce(32, TimeSpan.FromMilliseconds(250), Target, false));
    }
}
