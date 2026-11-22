using System.Numerics;
using Newt.Game;

namespace Newt.Simulation.Tests;

public sealed class CloudPerforationTests
{
    [Fact]
    public void CloudHolePersistsAcrossWeatherStepsThenRecovers()
    {
        var holes = new CloudPerforations();
        var cell = (12, 8, -1);
        holes.Punch(cell);
        Assert.Contains(cell, holes.DirtyCells);
        holes.DirtyCells.Clear();
        holes.Advance(3);
        Assert.True(holes.Contains(cell));
        Assert.Empty(holes.DirtyCells);
        holes.Advance((float)CloudPerforations.RecoverySeconds - 3);
        Assert.False(holes.Contains(cell));
        Assert.Contains(cell, holes.DirtyCells);
    }

    [Fact]
    public void RepeatedShotsKeepAHoleOpenWithoutRepeatedlyRebuildingTheSameCells()
    {
        var holes = new CloudPerforations();
        var cell = (4, 6, 2);
        holes.Punch(cell);
        holes.DirtyCells.Clear();
        holes.Advance(10);
        holes.Punch(cell);
        Assert.Empty(holes.DirtyCells);
        holes.Advance(3);
        Assert.True(holes.Contains(cell));
        holes.Advance(9);
        Assert.False(holes.Contains(cell));
    }

    [Fact]
    public void PlanetFacesHaveIndependentHolesAndResetClearsDamage()
    {
        var holes = new CloudPerforations();
        holes.Punch((0, 0, 0));
        Assert.False(holes.Contains((0, 0, 1)));
        holes.Punch((0, 0, 1));
        holes.Clear();
        Assert.False(holes.Contains((0, 0, 0)));
        Assert.False(holes.Contains((0, 0, 1)));
        Assert.Empty(holes.DirtyCells);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MissileAndRocketCollisionSamplesCrossThinCloudsWithoutStopping(bool guided)
    {
        var holes = new CloudPerforations();
        var flight = new GuidedMissileFlight(new(0, 10, 0), -Vector3.UnitY, Vector3.Zero, guided: guided);
        var hit = flight.Advance(2, point =>
        {
            if (point.Y >= 5 && point.Y <= 5.18f) holes.Punch((0, 0, -1));
            return point.Y <= 0;
        });
        Assert.True(holes.Contains((0, 0, -1)));
        Assert.True(hit);
        Assert.True(flight.Arrived);
        Assert.True(flight.Position.Y <= 0);
    }
}
