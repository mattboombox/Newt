using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WeatherVisualTests
{
    [Fact]
    public void NewlyLoadedRainAndSnowAlreadyFillTheirFallColumnAtAnyWorldTime()
    {
        foreach (var kind in new[] { PrecipitationKind.Rain, PrecipitationKind.Snow })
        foreach (var seconds in new[] { 0.0, 100.0, 10000.0 })
        for (uint seed = 0; seed < 12; seed++)
        {
            var lifetime = WeatherVisuals.DropLifetime(kind);
            var phases = Enumerable.Range(0, 6).Select(channel =>
            {
                var bornAt = WeatherVisuals.PrewarmedDropBirth(seconds, kind, seed, channel);
                Assert.True(WeatherVisuals.IsDropAlive(seconds, bornAt, kind));
                Assert.True(bornAt + lifetime > seconds);
                return (seconds - bornAt) / lifetime;
            }).ToArray();
            Assert.True(phases.Min() < 0.4, "New regions need drops near the cloud.");
            Assert.True(phases.Max() > 0.6, "New regions need drops already near the ground.");
            Assert.Equal(6, phases.Distinct().Count());
        }
    }

    [Fact]
    public void WhiteAndMoistCloudBanksHaveAClearGapAndKeepTheirOwnColorAtDifferentDensities()
    {
        foreach (var density in new[] { 0.15f, 0.3f, 0.6f })
        {
            Assert.Equal(CloudKind.White, WeatherVisuals.CloudBank(density, -0.4f));
            Assert.Equal(CloudKind.Moist, WeatherVisuals.CloudBank(density, 0.4f));
            foreach (var gap in new[] { -0.1f, -0.05f, 0, 0.05f, 0.1f })
                Assert.Equal(CloudKind.None, WeatherVisuals.CloudBank(density, gap));
        }
        Assert.Equal(CloudKind.None, WeatherVisuals.CloudBank(0, 0.8f));
    }

    [Theory]
    [InlineData(0.2f, MoistureBand.Wet, TemperatureBand.Temperate, PrecipitationKind.None)]
    [InlineData(0.4f, MoistureBand.Dry, TemperatureBand.Hot, PrecipitationKind.None)]
    [InlineData(0.4f, MoistureBand.Dry, TemperatureBand.Freezing, PrecipitationKind.None)]
    [InlineData(0.4f, MoistureBand.Normal, TemperatureBand.Cold, PrecipitationKind.Rain)]
    [InlineData(0.4f, MoistureBand.Wet, TemperatureBand.Temperate, PrecipitationKind.Rain)]
    [InlineData(0.4f, MoistureBand.Normal, TemperatureBand.Freezing, PrecipitationKind.Snow)]
    public void WeatherChangesWithCloudDensityAndTheTileBelow(float density, MoistureBand moisture,
        TemperatureBand temperature, object expected) =>
        Assert.Equal(Assert.IsType<PrecipitationKind>(expected), WeatherVisuals.Precipitation(density, moisture, temperature));

    [Fact]
    public void DropsRemainInFlightAfterEmissionStopsAndExpireAfterLanding()
    {
        Assert.Equal(PrecipitationKind.None,
            WeatherVisuals.Precipitation(0.4f, MoistureBand.Dry, TemperatureBand.Temperate));
        // Stopping new emission must not cancel the earlier wet-ground droplet.
        Assert.True(WeatherVisuals.IsDropAlive(10.6, 10, PrecipitationKind.Rain));
        Assert.True(WeatherVisuals.IsDropAlive(11, 10, PrecipitationKind.Rain));
        Assert.False(WeatherVisuals.IsDropAlive(11.1, 10, PrecipitationKind.Rain));
        Assert.True(WeatherVisuals.IsDropAlive(18, 10, PrecipitationKind.Snow));
        Assert.False(WeatherVisuals.IsDropAlive(18.4, 10, PrecipitationKind.Snow));
    }

    [Fact]
    public void CloudCellJitterBreaksAlignedRowsAndStaysInsideItsCell()
    {
        var offsets = Enumerable.Range(0, 32)
            .Select(y => WeatherVisuals.RandomFraction(WeatherVisuals.CellSeed(12, y, 0))).ToArray();
        Assert.All(offsets, value => Assert.InRange(value, 0, 1));
        Assert.Equal(offsets.Length, offsets.Distinct().Count());
        Assert.True(offsets.Min() < 0.15f && offsets.Max() > 0.85f);
    }

    [Fact]
    public void SnowShrinksAndFadesWithDistanceInsteadOfGrowingToFillPixels()
    {
        // Perspective pixels span more world units as camera distance increases.
        var previousPixels = float.MaxValue;
        var previousOpacity = 1f;
        foreach (var unitsPerPixel in new[] { 0.003f, 0.01f, 0.02f, 0.04f, 0.1f, 0.3f })
        {
            var appearance = WeatherVisuals.SnowAppearance(unitsPerPixel);
            var pixels = appearance.CellSize / unitsPerPixel;
            Assert.InRange(appearance.CellSize, 0, 0.018f);
            Assert.InRange(pixels, 0, previousPixels);
            Assert.InRange(appearance.Opacity, 0, previousOpacity);
            Assert.True(pixels <= 3); // Nearby crosses remain at most nine pixels wide.
            previousPixels = pixels;
            previousOpacity = appearance.Opacity;
        }
        Assert.True(WeatherVisuals.SnowAppearance(0.01f).Detailed);
        Assert.False(WeatherVisuals.SnowAppearance(0.04f).Detailed);
        Assert.Equal(0, WeatherVisuals.SnowAppearance(0.3f).Opacity);
        var close = WeatherVisuals.SnowAppearance(0.003f).CellSize / 0.003f;
        var middle = WeatherVisuals.SnowAppearance(0.01f).CellSize / 0.01f;
        var far = WeatherVisuals.SnowAppearance(0.02f).CellSize / 0.02f;
        Assert.True(close > middle && middle > far, "Flakes must visibly grow as the camera approaches.");
    }

    [Fact]
    public void PlayerFallHasFixedTerminalSpeedAndIsIndependentOfFrameRate()
    {
        const float time = 1;
        var whole = ThreeDGeometry.AdvancePlayerFall(0, time);
        Assert.Equal(16.8f, whole.Distance, precision: 4);
        var terminal = ThreeDGeometry.PlayerTerminalFallSpeed;
        Assert.Equal(terminal, whole.Velocity, precision: 4);
        var continued = ThreeDGeometry.AdvancePlayerFall(whole.Velocity, time);
        Assert.Equal(terminal, continued.Distance, precision: 4);
        Assert.Equal(terminal, continued.Velocity, precision: 4);
        var velocity = 0f;
        var distance = 0f;
        for (var frame = 0; frame < 60; frame++)
        {
            var fall = ThreeDGeometry.AdvancePlayerFall(velocity, time / 60);
            velocity = fall.Velocity;
            distance += fall.Distance;
        }
        Assert.Equal(whole.Distance, distance, precision: 4);
        Assert.Equal(whole.Velocity, velocity, precision: 4);
    }

    [Fact]
    public void PlayerDescentIgnoresBoostAndClampsAtLandingIncludingLedgeFalls()
    {
        var normal = ThreeDGeometry.FirstPersonFlightHeight(10, 0, null, false, false, false,
            0.1f, 32, fallingDistance: 2);
        var boosted = ThreeDGeometry.FirstPersonFlightHeight(10, 0, null, false, false, true,
            0.1f, 32, boosted: true, fallingDistance: 2);
        Assert.Equal((8f, true), normal);
        Assert.Equal(normal, boosted);
        var landing = ThreeDGeometry.FirstPersonFlightHeight(2, 0, null, true, false, false,
            0.1f, 32, fallingDistance: 10);
        Assert.Equal((1.2f, false), landing);
    }

    [Fact]
    public void SnowFallsSlowlyWhileRainUsesTheWaterfallCycle()
    {
        var snow = WeatherVisuals.SnowFall(1, 0, 0);
        var rain = WeatherVisuals.WaterfallDropAge(1, 0, 0);
        Assert.InRange(snow, 0.1f, 0.15f);
        Assert.True(rain > 7 * snow);
        Assert.Equal(0, WeatherVisuals.WaterfallFall(0));
        Assert.Equal(1, WeatherVisuals.WaterfallFall(0.73f));
        Assert.True(WeatherVisuals.WaterfallFall(0.5f) - WeatherVisuals.WaterfallFall(0.4f) >
            WeatherVisuals.WaterfallFall(0.2f) - WeatherVisuals.WaterfallFall(0.1f));
    }
}
