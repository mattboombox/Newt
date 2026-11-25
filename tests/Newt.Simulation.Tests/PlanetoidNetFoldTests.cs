using System.Numerics;
using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlanetoidNetFoldTests
{
    [Fact]
    public void EveryFaceStartsAtItsNetLocationAndEndsOnTheSphere()
    {
        const int size = 24, width = size * 3;
        for (var face = 0; face < 6; face++)
        {
            var owner = new GridPosition(face % 3 * size + 7, face / 3 * size + 9);
            var net = PlanetoidLayout.TileToNet(owner, size);
            var start = PlanetoidNetFold.Position(owner.X, owner.Y, 3, width, owner, 0);
            Assert.True(Vector3.Distance(start, new Vector3(net.X - size * 1.5f, size * 1.5f - net.Y, size / 2f)) < 0.0001f);
            var end = PlanetoidNetFold.Position(owner.X, owner.Y, 3, width, owner, 1);
            Assert.True(Vector3.Distance(end, PlanetoidGeometry.Position(new(owner.X, 3, owner.Y), width, owner)) < 0.0001f);
        }
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.2f)]
    [InlineData(0.5f)]
    [InlineData(0.7f)]
    public void HingesStayJoinedWhileFacesFold(float progress)
    {
        const int size = 24, width = size * 3;
        var front = new GridPosition(0, 0);
        var right = new GridPosition(size, 0);
        var top = new GridPosition(size, size);
        var frontRight = PlanetoidNetFold.Position(size, 12, 0, width, front, progress);
        var rightLeft = PlanetoidNetFold.Position(size, 12, 0, width, right, progress);
        Assert.True(Vector3.Distance(frontRight, rightLeft) < 0.0001f);
        var frontTop = PlanetoidNetFold.Position(12, 0, 0, width, front, progress);
        var topBottom = PlanetoidNetFold.Position(size + 12, size * 2, 0, width, top, progress);
        Assert.True(Vector3.Distance(frontTop, topBottom) < 0.0001f);
    }
}
