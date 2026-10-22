using System.Numerics;
using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RingGeometryTests
{
    [Theory]
    [InlineData(1280)]
    [InlineData(2560)]
    [InlineData(5120)]
    public void RingClosesExactlyAtEastWestSeam(int circumference)
    {
        var first = RingGeometry.Position(new Vector3(0, 5, 22), circumference, 44);
        var last = RingGeometry.Position(new Vector3(circumference, 5, 22), circumference, 44);
        Assert.Equal(first, last);
        Assert.Equal(RingGeometry.Frame(0, circumference), RingGeometry.Frame(circumference, circumference));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(320)]
    [InlineData(640)]
    [InlineData(960)]
    [InlineData(1279)]
    public void TerrainElevationPointsInwardAndRailFrameFollowsSurface(int x)
    {
        var basePoint = RingGeometry.Position(new Vector3(x, 0, 22), 1280, 44);
        var elevated = RingGeometry.Position(new Vector3(x, 5, 22), 1280, 44);
        var (east, up) = RingGeometry.Frame(x, 1280);
        Assert.InRange(Vector3.Distance(elevated - basePoint, up * 5), 0, 0.0001f);
        Assert.InRange(Math.Abs(Vector3.Dot(east, up)), 0, 0.00001f);
        Assert.InRange(Vector3.Distance(east, Vector3.Cross(up, Vector3.UnitZ)), 0, 0.00001f);
        Assert.InRange(east.Length(), 0.99999f, 1.00001f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(319.5f)]
    [InlineData(641.25f)]
    [InlineData(1279.5f)]
    public void WorldCoordinatesRoundTripToMapCoordinates(float x)
    {
        var map = new Vector3(x, 4.25f, 20.5f);
        var roundTrip = RingGeometry.StripPosition(RingGeometry.Position(map, 1280, 44), 1280, 44);
        Assert.InRange(Vector3.Distance(map, roundTrip), 0, 0.0003f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(319)]
    [InlineData(640)]
    [InlineData(1279)]
    public void RayPicksCurvedTileIncludingSeamAndFarArc(int x)
    {
        var a = RingGeometry.Position(new Vector3(x, 5, 20), 1280, 44);
        var b = RingGeometry.Position(new Vector3(x + 1, 5, 20), 1280, 44);
        var c = RingGeometry.Position(new Vector3(x + 1, 5, 21), 1280, 44);
        var d = RingGeometry.Position(new Vector3(x, 5, 21), 1280, 44);
        var center = (a + b + c + d) / 4;
        var normal = RingGeometry.Frame(x + 0.5f, 1280).Up;
        var origin = center + normal * 10;
        var hit = RingGeometry.TriangleHit(origin, -normal, a, b, c) ??
            RingGeometry.TriangleHit(origin, -normal, a, c, d);
        Assert.NotNull(hit);
        Assert.InRange(hit.Value, 9.999f, 10.001f);
        Assert.Null(RingGeometry.TriangleHit(origin, normal, a, b, c));
    }

    [Fact]
    public void ExistingSizesProvideLongRingRelativeToStripWidth()
    {
        Assert.True(RingGeometry.Radius(WorldPreset.Ring.Width) > WorldPreset.Ring.Height * 4);
        Assert.True(RingGeometry.Radius(WorldPreset.RingMega.Width) > WorldPreset.RingMega.Height * 8);
    }
}
