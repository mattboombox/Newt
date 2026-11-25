using Newt.Game;

namespace Newt.Simulation.Tests;

public sealed class PlanetoidCameraLimitsTests
{
    [Theory]
    [InlineData(96, 20)]
    [InlineData(96, 96)]
    [InlineData(96, 640)]
    [InlineData(72, 360)]
    public void EveryMapPositionCanBeCenteredEvenWhenViewportExceedsMap(int length, int visible)
    {
        for (var tile = 0; tile < length; tile++)
        {
            var camera = tile - visible / 2;
            Assert.Equal(camera, PlanetoidCameraLimits.Constrain(camera, length, visible));
        }
        Assert.Equal(-visible / 2, PlanetoidCameraLimits.Constrain(int.MinValue, length, visible));
        Assert.Equal(length - 1 - visible / 2,
            PlanetoidCameraLimits.Constrain(int.MaxValue, length, visible));
    }
}
