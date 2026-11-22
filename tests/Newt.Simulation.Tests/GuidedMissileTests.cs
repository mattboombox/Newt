using System.Numerics;
using Newt.Game;

namespace Newt.Simulation.Tests;

public sealed class GuidedMissileTests
{
    [Fact]
    public void RocketPodFliesStraightAtCruiseSpeedAndIgnoresTargetChanges()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitX, new(30, 0, 0), guided: false);
        flight.Advance(0.1f, _ => false);
        Assert.InRange(flight.Position.X, 2.3999f, 2.4001f);
        Assert.Equal(0, flight.Position.Y);
        Assert.Equal(0, flight.Position.Z);
        flight.Target = new(-10, 30, 20);
        flight.Advance(1, _ => false);
        Assert.InRange(flight.Position.X, 26.399f, 26.401f);
        Assert.Equal(0, flight.Position.Y);
        Assert.Equal(0, flight.Position.Z);
        Assert.False(flight.Arrived);
    }

    [Fact]
    public void RocketPodDetonatesOnceOnAnInterveningSurface()
    {
        var flight = new GuidedMissileFlight(new(0, 2, 0), Vector3.UnitX, new(100, 2, 0), guided: false);
        Assert.True(flight.Advance(1, p => p.X >= 6.2f));
        Assert.InRange(flight.Position.X, 6.2f, 6.251f);
        Assert.NotEqual(flight.Target, flight.Position);
        Assert.False(flight.Advance(1, _ => true));
    }

    [Fact]
    public void RocketsThatLeaveTheWorldExpireWithoutAnImpact()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitX, new(0, 1000, 0), guided: false);
        Assert.False(flight.Advance(21, _ => false));
        Assert.True(flight.Expired);
        Assert.False(flight.Arrived);
        var end = flight.Position;
        Assert.False(flight.Advance(1, _ => true));
        Assert.Equal(end, flight.Position);
    }

    [Fact]
    public void MissileLaunchesUpAndOutBeforeCurvingToItsTarget()
    {
        var target = new Vector3(0, 0, -40);
        var flight = new GuidedMissileFlight(Vector3.Zero, new(1, 2, -0.5f), target);
        Assert.False(flight.Advance(GuidedMissileFlight.LaunchSeconds));
        Assert.True(flight.Position.Y > 3);
        Assert.True(flight.Position.X > 1);
        Assert.False(flight.Arrived);
        var hits = FlyToArrival(flight);
        Assert.Equal(1, hits);
        Assert.InRange(Vector3.Distance(target, flight.Position), 0, 0.15f);
        Assert.True(flight.Age > new RifleShotTiming(40).TravelSeconds);
        Assert.False(flight.Advance(1));
    }

    [Fact]
    public void MissileCanBeRedirectedWhileInFlightWithoutTeleporting()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitY, new(0, 0, -60));
        flight.Advance(0.8f);
        var before = flight.Position;
        flight.Target = new(35, 0, 20);
        flight.Advance(1f / 60);
        Assert.InRange(Vector3.Distance(before, flight.Position), 0, 0.41f);
        Assert.False(flight.Arrived);
        Assert.Equal(1, FlyToArrival(flight));
        Assert.InRange(Vector3.Distance(flight.Target, flight.Position), 0, 0.15f);
    }

    [Fact]
    public void LongFlightsKeepABoundedCurvedTrail()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitY, new(0, 0, -1000));
        flight.Advance(10);
        Assert.False(flight.Arrived);
        Assert.InRange(flight.Trail.Count, 2, GuidedMissileFlight.MaximumTrailPoints);
        Assert.NotEqual(Vector3.Zero, flight.Trail[0]);
    }

    [Fact]
    public void FlightDoesNotDependOnRenderFrameRate()
    {
        var slow = new GuidedMissileFlight(Vector3.Zero, new(1, 2, 0), new(40, 0, -30));
        var fast = new GuidedMissileFlight(Vector3.Zero, new(1, 2, 0), new(40, 0, -30));
        for (var i = 0; i < 30; i++) slow.Advance(1f / 30);
        for (var i = 0; i < 144; i++) fast.Advance(1f / 144);
        Assert.InRange(Vector3.Distance(slow.Position, fast.Position), 0, 0.5f);
    }

    [Fact]
    public void PlanetaryGuidanceAllowsTerrainCollisionBeforeTheTarget()
    {
        const float radius = 30;
        var target = new Vector3(radius + 0.1f, 0, 0);
        var flight = new GuidedMissileFlight(new(0, 0, radius + 1), new(1, 0, 2), target, radius);
        for (var i = 0; i < 1200 && !flight.Arrived; i++)
        {
            flight.Advance(1f / 60, point => point.Length() <= radius + 0.5f);
            Assert.True(flight.Position.Length() >= radius);
        }
        Assert.True(flight.Arrived);
        Assert.True(flight.Position.Length() <= radius + 0.5f);
        Assert.NotEqual(target, flight.Position);
    }

    [Fact]
    public void CruiseSpeedStaysConstantNearTheTarget()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitX, new(1000, 0, 0));
        flight.Advance(0.5f, _ => false);
        var before = flight.Position;
        flight.Advance(1f / 120, _ => false);
        var farStep = Vector3.Distance(before, flight.Position);
        flight.Target = flight.Position + Vector3.UnitX * 2;
        before = flight.Position;
        flight.Advance(1f / 120, _ => false);
        var nearStep = Vector3.Distance(before, flight.Position);
        Assert.InRange(farStep, 0.1999f, 0.2001f);
        Assert.InRange(nearStep, 0.1999f, 0.2001f);
    }

    [Fact]
    public void MissileCanOvershootAimWithoutSnappingOrDetonating()
    {
        var flight = new GuidedMissileFlight(Vector3.Zero, Vector3.UnitX, new(100, 0, 0));
        flight.Advance(0.5f, _ => false);
        flight.Target = flight.Position + new Vector3(0, 0.1f, 0);
        Assert.False(flight.Advance(1f / 60, _ => false));
        Assert.False(flight.Arrived);
        Assert.True(flight.Position.X > flight.Target.X);
        Assert.NotEqual(flight.Target, flight.Position);
    }

    [Fact]
    public void SweptCollisionHitsThinObstacleDuringLaunchAndDetonatesOnlyOnce()
    {
        var flight = new GuidedMissileFlight(new(0, 2, 0), Vector3.UnitX, new(100, 2, 0));
        Assert.True(flight.Advance(1, p => p.X >= 3 && p.X <= 3.06f));
        Assert.True(flight.Arrived);
        Assert.InRange(flight.Position.X, 3, 3.06f);
        Assert.True(flight.Age < GuidedMissileFlight.LaunchSeconds);
        Assert.NotEqual(flight.Target, flight.Position);
        var hit = flight.Position;
        Assert.False(flight.Advance(1, _ => true));
        Assert.Equal(hit, flight.Position);
    }

    private static int FlyToArrival(GuidedMissileFlight flight)
    {
        var hits = 0;
        for (var i = 0; i < 1200 && !flight.Arrived; i++)
            if (flight.Advance(1f / 60)) hits++;
        Assert.True(flight.Arrived);
        return hits;
    }
}
