using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ThreeDGeometryTests
{
    [Theory]
    [InlineData(0.88f, 1f, 0f)]
    [InlineData(0.14f, 0f, 1f)]
    [InlineData(0.40f, -1f, 0f)]
    public void SunTravelsFromEastThroughNoonToWest(float phase, float east, float up)
    {
        var direction = ThreeDGeometry.SunDirection(phase);
        Assert.InRange(Math.Abs(direction.X - east), 0, 0.0001f);
        Assert.InRange(Math.Abs(direction.Y - up), 0, 0.0001f);
    }

    [Theory]
    [InlineData(0.48f)]
    [InlineData(0.55f)]
    [InlineData(0.72f)]
    [InlineData(0.80f)]
    public void SunIsBelowHorizonDuringNight(float phase) =>
        Assert.True(ThreeDGeometry.SunDirection(phase).Y < 0);

    [Fact]
    public void SunOrbitWrapsContinuouslyAtCycleBoundary()
    {
        Assert.InRange(System.Numerics.Vector2.Distance(
            ThreeDGeometry.SunDirection(0), ThreeDGeometry.SunDirection(1)), 0, 0.00001f);
        Assert.InRange(System.Numerics.Vector2.Distance(
            ThreeDGeometry.SunDirection(0.99999f), ThreeDGeometry.SunDirection(0)), 0, 0.001f);
    }

    [Fact]
    public void WalkingIsSmoothAndIndependentOfFrameRate()
    {
        var whole = System.Numerics.Vector2.Zero;
        var halves = System.Numerics.Vector2.Zero;
        for (var i = 0; i < 60; i++) whole += ThreeDGeometry.WalkingStep(0, 1, 0, 1f / 60, false);
        for (var i = 0; i < 120; i++) halves += ThreeDGeometry.WalkingStep(0, 1, 0, 1f / 120, false);
        Assert.InRange(System.Numerics.Vector2.Distance(whole, halves), 0, 0.0001f);
        Assert.InRange(whole.Length(), 3.4999f, 3.5001f);
    }

    [Fact]
    public void DiagonalWalkingDoesNotMoveFasterAndShiftSpeedsUp()
    {
        var forward = ThreeDGeometry.WalkingStep(0, 1, 0, 0.1f, false);
        var diagonal = ThreeDGeometry.WalkingStep(0, 1, 1, 0.1f, false);
        Assert.Equal(forward.Length(), diagonal.Length(), precision: 5);
        Assert.Equal(forward.Length() * 2.5f, ThreeDGeometry.WalkingStep(0, 1, 0, 0.1f, true).Length(), precision: 5);
    }

    [Fact]
    public void WalkingUsesTheLookHeadingAndStopsWithoutWasd()
    {
        var turned = ThreeDGeometry.WalkingStep(MathF.PI / 2, 1, 0, 0.1f, false);
        Assert.True(turned.X < -0.34f);
        Assert.InRange(Math.Abs(turned.Y), 0, 0.0001f);
        Assert.Equal(System.Numerics.Vector2.Zero, ThreeDGeometry.WalkingStep(1, 0, 0, 0.1f, false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1.57f)]
    [InlineData(-2.5f)]
    public void CameraCanLookAboveHorizonAtEveryHeading(float yaw)
    {
        var upwards = ThreeDGeometry.LookDirection(yaw, -MathF.PI / 3);
        Assert.True(upwards.Y > 0.86f);
        Assert.InRange(upwards.Length(), 0.99999f, 1.00001f);
        Assert.Equal(0, ThreeDGeometry.LookDirection(yaw, 0).Y);
        Assert.True(ThreeDGeometry.LookDirection(yaw, 0.25f).Y < 0);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(28)]
    [InlineData(180)]
    [InlineData(600)]
    public void CloseZoomRetainsLongViewDistanceAndWideZoomStillFits(float cameraDistance)
    {
        var farClip = ThreeDGeometry.FarClipDistance(cameraDistance);
        Assert.True(farClip >= 396);
        Assert.True(farClip > cameraDistance * 2);
    }

    [Fact]
    public void ZoomedOutMapShowsEachLongitudeOnlyOnce()
    {
        var (left, right) = ThreeDGeometry.SingleMapSpan(-500, 500, 5, 80);
        Assert.Equal(80, right - left);
        var longitudes = Enumerable.Range(left, right - left)
            .Select(x => ThreeDGeometry.WrapTile(x, 80)).ToArray();
        Assert.Equal(80, longitudes.Distinct().Count());
        Assert.Equal(-35, left);
    }

    [Fact]
    public void CloseViewsKeepTheirSeamlessWrappingSpan()
    {
        Assert.Equal((-5, 15), ThreeDGeometry.SingleMapSpan(-5, 15, 5, 80));
    }

    [Fact]
    public void WideViewShiftsWithCameraAndStaysWithinVisibleBounds()
    {
        Assert.Equal((-30, 50), ThreeDGeometry.SingleMapSpan(-500, 500, 10, 80));
        Assert.Equal((0, 80), ThreeDGeometry.SingleMapSpan(0, 100, 5, 80));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeSailor, false, false, false, true)]
    [InlineData(CritterSpecies.ApeTraderSailor, false, false, false, true)]
    [InlineData(CritterSpecies.ApeWarrior, false, false, false, true)]
    [InlineData(CritterSpecies.ApeChieftain, false, false, false, true)]
    [InlineData(CritterSpecies.ApeRoyalGuard, false, false, false, true)]
    [InlineData(CritterSpecies.ApeKing, false, false, false, true)]
    [InlineData(CritterSpecies.ApeQueen, false, false, false, true)]
    [InlineData(CritterSpecies.ApeBomber, false, false, false, true)]
    [InlineData(CritterSpecies.Vampire, true, false, false, true)]
    [InlineData(CritterSpecies.ApeFarmer, true, false, false, true)]
    [InlineData(CritterSpecies.Ape, false, true, false, true)]
    [InlineData(CritterSpecies.Ape, false, false, true, true)]
    [InlineData(CritterSpecies.Fish, false, false, false, false)]
    [InlineData(CritterSpecies.ToothedWhale, false, false, false, false)]
    [InlineData(CritterSpecies.Vampire, false, false, false, false)]
    public void AllSailorVariantsFloatWhileAquaticCrittersRemainAtBed(CritterSpecies species,
        bool colonist, bool pirate, bool veteran, bool expected) =>
        Assert.Equal(expected, ThreeDGeometry.IsSurfaceSailor(species, colonist, pirate, veteran));

    [Theory]
    [InlineData(0.5f, 0)]
    [InlineData(1f, 0)]
    [InlineData(7.8f, 3)]
    [InlineData(14f, 4)]
    [InlineData(31f, 6)]
    [InlineData(200f, 10)]
    public void ReturningTo2DSelectsNearestScaleAndClampsToSupportedZoom(float pixels, int expected)
    {
        int[] levels = [1, 2, 4, 8, 16, 24, 32, 48, 64, 96, 128];
        Assert.Equal(expected, ThreeDGeometry.NearestZoomIndex(pixels, levels));
    }

    [Fact]
    public void EverySupported2DZoomSurvivesAnOverheadRoundTrip()
    {
        int[] levels = [1, 2, 4, 8, 16, 24, 32, 48, 64, 96, 128];
        for (var i = 0; i < levels.Length; i++)
            Assert.Equal(i, ThreeDGeometry.NearestZoomIndex(levels[i], levels));
    }

    [Theory]
    [InlineData(484, 1)]
    [InlineData(640, 1)]
    [InlineData(1284, 1)]
    [InlineData(484, 4)]
    public void Entering3DPreserves2DVisibleSpanWithoutHittingZoomLimit(int viewportHeight, int tileSize)
    {
        var requested = ThreeDGeometry.OverheadDistance(viewportHeight, tileSize);
        var distance = Math.Clamp(requested, 6, ThreeDGeometry.MaximumZoomDistance(viewportHeight, 1));
        var projectedPixelsPerTile = viewportHeight / (2 * distance * MathF.Tan(MathF.PI / 8));
        Assert.Equal((float)tileSize, projectedPixelsPerTile, precision: 4);
        Assert.True(ThreeDGeometry.MaximumZoomDistance(viewportHeight, 1) >= requested);
    }

    [Theory]
    [InlineData(-1, 79)]
    [InlineData(80, 0)]
    [InlineData(161, 1)]
    public void RepeatedMapTilesResolveToOriginalTile(int coordinate, int expected) =>
        Assert.Equal(expected, ThreeDGeometry.WrapTile(coordinate, 80));

    [Theory]
    [InlineData(79, -5, -1)]
    [InlineData(0, 75, 80)]
    [InlineData(10, -170, -150)]
    public void CritterCopiesContinueAcrossVisibleSeams(int tile, int minimum, int expected) =>
        Assert.Equal(expected, ThreeDGeometry.FirstVisibleCopy(tile, minimum, 80));

    [Theory]
    [InlineData(Terrain.Ice, SurfaceWaterKind.None, Biome.None, true)]
    [InlineData(Terrain.Plains, SurfaceWaterKind.FreshwaterLake, Biome.Arctic, true)]
    [InlineData(Terrain.Plains, SurfaceWaterKind.FreshwaterLake, Biome.Grassland, false)]
    [InlineData(Terrain.Plains, SurfaceWaterKind.None, Biome.Arctic, false)]
    public void FrozenOceansAndLakesReceiveIceSheets(Terrain terrain, SurfaceWaterKind water, Biome biome, bool expected) =>
        Assert.Equal(expected, ThreeDGeometry.IsIce(terrain, water, biome));

    [Fact]
    public void LakeSurfaceUsesItsOwnLevelWithoutAnOcean()
    {
        Assert.Equal(0.6f, ThreeDGeometry.WaterElevation(Terrain.Plains,
            SurfaceWaterKind.FreshwaterLake, false, 0, 0.6f));
        Assert.Equal(0.6f, ThreeDGeometry.WaterElevation(Terrain.Ice,
            SurfaceWaterKind.FreshwaterLake, true, 0, 0.6f));
        Assert.Null(ThreeDGeometry.WaterElevation(Terrain.Canyon, SurfaceWaterKind.None, true, 0, null));
        Assert.Equal(0f, ThreeDGeometry.WaterElevation(Terrain.Ice, SurfaceWaterKind.None, true, 0, null));
    }

    [Theory]
    [InlineData(CritterSpecies.Deer, true)]
    [InlineData(CritterSpecies.Ape, true)]
    [InlineData(CritterSpecies.Fish, false)]
    [InlineData(CritterSpecies.ToothedWhale, false)]
    public void LandCrittersStandOnIceAndAquaticCrittersRemainUnderneath(CritterSpecies species, bool expected) =>
        Assert.Equal(expected, ThreeDGeometry.StandsOnIce(CritterHabitats.GetHabitat(species)));

    [Theory]
    [InlineData(-1, 0.25f)]
    [InlineData(0, 4.25f)]
    [InlineData(2, 12.25f)]
    public void ElevationProducesPositiveColumns(float elevation, float expected) =>
        Assert.Equal(expected, ThreeDGeometry.ColumnHeight(elevation));

    [Theory]
    [InlineData(-1, 79)]
    [InlineData(80, 0)]
    [InlineData(161.5f, 1.5f)]
    [InlineData(-161.5f, 78.5f)]
    public void CameraWrapsAcrossEitherEdge(float focus, float expected) =>
        Assert.Equal(expected, ThreeDGeometry.WrapCamera(focus, 80));

    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(0.00001f, 10, 0)]
    [InlineData(3, 5, 7)]
    [InlineData(-3, 0, -7)]
    public void SpriteFacesEyeIncludingDirectlyOverhead(float x, float y, float z)
    {
        var towardEye = System.Numerics.Vector3.Normalize(new(x, y, z));
        var (right, up) = ThreeDGeometry.BillboardBasis(towardEye, System.Numerics.Vector3.UnitX);
        Assert.InRange(System.Numerics.Vector3.Dot(right, up), -0.0001f, 0.0001f);
        Assert.InRange(right.Length(), 0.9999f, 1.0001f);
        Assert.InRange(up.Length(), 0.9999f, 1.0001f);
        var normal = System.Numerics.Vector3.Cross(right, up);
        Assert.InRange(System.Numerics.Vector3.Dot(normal, towardEye), 0.9999f, 1.0001f);
    }
    [Fact]
    public void PickingHitsTopAndSideOfElevatedTile()
    {
        Assert.Equal(6f, ThreeDGeometry.IntersectColumn(2.5f, 10, 3.5f, 0, -1, 0, 2, 3, 4));
        Assert.Equal(2f, ThreeDGeometry.IntersectColumn(0, 2, 3.5f, 1, 0, 0, 2, 3, 4));
    }

    [Fact]
    public void PickingRejectsParallelMissesAndTilesBehindCamera()
    {
        Assert.Null(ThreeDGeometry.IntersectColumn(-1, 10, 3.5f, 0, -1, 0, 2, 3, 4));
        Assert.Null(ThreeDGeometry.IntersectColumn(0, 2, 3.5f, -1, 0, 0, 2, 3, 4));
    }

    [Fact]
    public void TallColumnOccludesTileBehindIt()
    {
        var foreground = ThreeDGeometry.IntersectColumn(0, 3, 0.5f, 1, -0.25f, 0, 2, 0, 4);
        var background = ThreeDGeometry.IntersectColumn(0, 3, 0.5f, 1, -0.25f, 0, 8, 0, 2);
        Assert.NotNull(foreground);
        Assert.NotNull(background);
        Assert.True(foreground < background);
    }
}
