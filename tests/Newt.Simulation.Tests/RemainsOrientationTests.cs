using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RemainsOrientationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemainsHaveStableVariedQuarterTurnOrientations(bool shipwreck)
    {
        var seen = new HashSet<int>();
        for (var x = 0; x < 128; x++)
        {
            var tile = new GridPosition(x, 7);
            var turns = RemainsOrientation.QuarterTurns(17, tile, shipwreck);
            Assert.InRange(turns, 0, 3);
            Assert.Equal(turns, RemainsOrientation.QuarterTurns(17, tile, shipwreck));
            seen.Add(turns);
        }
        Assert.Equal(4, seen.Count);
    }
}
