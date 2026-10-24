using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ThreeDGeometryTests
{
    [Fact]
    public void RoadSidesMeetLowerRoadsAndBridgeDecksWithoutExtendingIntoWater()
    {
        Assert.Equal(2.01f, ThreeDGeometry.RoadSideBottom(2, 1, null, false));
        Assert.Equal(3, ThreeDGeometry.RoadSideBottom(2, 3, null, false));
        var deck = ThreeDGeometry.RoadHeight(2, 0, true);
        Assert.Equal(deck, ThreeDGeometry.RoadSideBottom(2, 1, 0, true));
        Assert.True(deck > ThreeDGeometry.ColumnHeight(0));
        // Equal-height bridge tiles need no vertical strip down to the sea bed.
        Assert.Equal(ThreeDGeometry.RoadHeight(1, 0, true), deck);
    }

    [Fact]
    public void UnderwaterCameraTintFadesAcrossWaterlineAndRemainsBounded()
    {
        Assert.Equal(0, ThreeDGeometry.UnderwaterTintOpacity(6, 5));
        Assert.Equal(0, ThreeDGeometry.UnderwaterTintOpacity(5, 5));
        Assert.Equal(0, ThreeDGeometry.UnderwaterTintOpacity(1, null));
        Assert.InRange(ThreeDGeometry.UnderwaterTintOpacity(4.925f, 5), 0.1399f, 0.1401f);
        Assert.Equal(0.28f, ThreeDGeometry.UnderwaterTintOpacity(4, 5));
        Assert.Equal(0.28f, ThreeDGeometry.UnderwaterTintOpacity(-100, 5));
    }

    [Fact]
    public void FastSinkingTriplesDescentWithoutChangingSwimmingUp()
    {
        var normal = ThreeDGeometry.FirstPersonSwimmingHeight(5, 0, 5, false, 1);
        var fast = ThreeDGeometry.FirstPersonSwimmingHeight(5, 0, 5, false, 1, fastSink: true);
        Assert.InRange(5 - fast, 1.1999f, 1.2001f);
        Assert.InRange((5 - fast) / (5 - normal), 2.999f, 3.001f);
        Assert.Equal(ThreeDGeometry.FirstPersonSwimmingHeight(4, 0, 5, true, 1),
            ThreeDGeometry.FirstPersonSwimmingHeight(4, 0, 5, true, 1, fastSink: true));
        Assert.Equal(1.2f, ThreeDGeometry.FirstPersonSwimmingHeight(1.3f, 0, 5, false, 1, fastSink: true));
    }

    [Fact]
    public void IceEntryPreservesWhichSideOfTheSheetThePlayerApproachesFrom()
    {
        Assert.Equal(ThreeDGeometry.IceEntry.Above,
            ThreeDGeometry.FirstPersonIceEntry(6, 1, 5, false, false));
        Assert.Equal(ThreeDGeometry.IceEntry.Below,
            ThreeDGeometry.FirstPersonIceEntry(4, 1, 5, false, false));
        Assert.Equal(ThreeDGeometry.IceEntry.Blocked,
            ThreeDGeometry.FirstPersonIceEntry(5, 1, 5, false, false));
        // A swimmer cannot pop onto a lower adjacent ice sheet.
        Assert.Equal(ThreeDGeometry.IceEntry.Blocked,
            ThreeDGeometry.FirstPersonIceEntry(4, 1, 3, false, true));
        Assert.Equal(ThreeDGeometry.IceEntry.Blocked,
            ThreeDGeometry.FirstPersonIceEntry(4, 4.9f, 5, false, true));
        Assert.Equal(ThreeDGeometry.IceEntry.Above,
            ThreeDGeometry.FirstPersonIceEntry(6, 1, 7, true, false));
    }

    [Fact]
    public void SwimmingUnderIceStopsAtUndersideAndReleaseSinksAgain()
    {
        const float ceiling = 4.94f;
        var eye = 3f;
        for (var frame = 0; frame < 300; frame++)
        {
            var next = ThreeDGeometry.FirstPersonSwimmingHeight(eye, 1, 5, true, 1f / 60, ceiling: ceiling);
            Assert.InRange(next - eye, 0, 0.0134f);
            Assert.True(next <= ceiling);
            eye = next;
        }
        Assert.Equal(ceiling, eye);
        var sunk = ThreeDGeometry.FirstPersonSwimmingHeight(eye, 1, 5, false, 1, ceiling: ceiling);
        Assert.InRange(eye - sunk, 0.3999f, 0.4001f);
        var emerged = ThreeDGeometry.FirstPersonSwimmingHeight(eye, 1, 5, true, 1);
        Assert.True(emerged > ceiling);
    }

    [Fact]
    public void LowIceCeilingAllowsSwimmingWithoutSnappingToStandingHeight()
    {
        var next = ThreeDGeometry.FirstPersonSwimmingHeight(4.5f, 4, 5, true, 0.1f, ceiling: 4.94f);
        Assert.InRange(next, 4.5799f, 4.5801f);
        Assert.True(next < 4 + 1.2f);
    }

    [Fact]
    public void FirstPersonSinksSlowlyAndSwimsUpToSurfaceLimit()
    {
        const float ground = 1, water = 5, eyeOffset = 1.2f;
        var surfaceEye = water + eyeOffset * 0.75f;
        var sunk = ThreeDGeometry.FirstPersonSwimmingHeight(surfaceEye, ground, water, false, 1);
        Assert.InRange(surfaceEye - sunk, 0.3999f, 0.4001f);
        var risen = ThreeDGeometry.FirstPersonSwimmingHeight(sunk, ground, water, true, 0.1f);
        Assert.InRange(risen - sunk, 0.0799f, 0.0801f);
        for (var frame = 0; frame < 100; frame++)
            risen = ThreeDGeometry.FirstPersonSwimmingHeight(risen, ground, water, true, 0.1f);
        Assert.Equal(surfaceEye, risen);
        for (var frame = 0; frame < 1000; frame++)
            risen = ThreeDGeometry.FirstPersonSwimmingHeight(risen, ground, water, false, 0.1f);
        Assert.Equal(ground + eyeOffset, risen);
    }

    [Fact]
    public void SwimmingIsFrameRateIndependentAndShallowsKeepEyesAboveGround()
    {
        foreach (var rising in new[] { false, true })
        {
            var whole = ThreeDGeometry.FirstPersonSwimmingHeight(4, 0, 5, rising, 1);
            var frames = 4f;
            for (var frame = 0; frame < 60; frame++)
                frames = ThreeDGeometry.FirstPersonSwimmingHeight(frames, 0, 5, rising, 1f / 60);
            Assert.InRange(Math.Abs(whole - frames), 0, 0.0001f);
        }
        Assert.Equal(5.2f, ThreeDGeometry.FirstPersonSwimmingHeight(4, 4, 4.1f, false, 1));
    }

    [Fact]
    public void SpaceDoesNotRaiseCameraOnSolidGroundOrBridgeDecks()
    {
        var ground = ThreeDGeometry.RoadHeight(2, 0, shallowsBridge: true);
        var standing = ground + 1.2f;
        Assert.Equal(standing, ThreeDGeometry.FirstPersonSwimmingHeight(standing, ground, null, true, 1));
        Assert.Equal(standing, ThreeDGeometry.FirstPersonSwimmingHeight(standing, ground, null, false, 1));
    }

    [Theory]
    [InlineData(RiverConnection.North, 0, -1, 1)]
    [InlineData(RiverConnection.NorthEast, 1, -1, 2)]
    [InlineData(RiverConnection.East, 1, 0, 1)]
    [InlineData(RiverConnection.SouthEast, 1, 1, 2)]
    [InlineData(RiverConnection.South, 0, 1, 1)]
    [InlineData(RiverConnection.SouthWest, -1, 1, 2)]
    [InlineData(RiverConnection.West, -1, 0, 1)]
    [InlineData(RiverConnection.NorthWest, -1, -1, 2)]
    public void RiverCascadesFollowCardinalEdgesAndDiagonalCorners(RiverConnection direction, int dx, int dy, int faces)
    {
        var segments = ThreeDGeometry.ConnectionSideSegments(direction).ToArray();
        Assert.Equal(faces, segments.Length);
        Assert.All(segments, segment =>
        {
            Assert.Equal(dx, segment.TargetX);
            Assert.Equal(dy, segment.TargetY);
            Assert.Equal(1, Math.Abs(segment.FaceX) + Math.Abs(segment.FaceY));
            Assert.InRange(segment.Start, 0, 1);
            Assert.InRange(segment.End, 0, 1);
            Assert.InRange(segment.End - segment.Start, 0.3333f, 0.3334f);
            if (faces == 2)
                Assert.True(segment.Start == 0 || segment.End == 1);
        });
        Assert.Empty(ThreeDGeometry.ConnectionSideSegments(RiverConnection.None));
    }

    [Fact]
    public void RiverCascadesStopAtReceivingWaterOrInterveningTerrain()
    {
        Assert.Equal(2, ThreeDGeometry.RiverSideBottom(2, 1, null));
        Assert.Equal(3, ThreeDGeometry.RiverSideBottom(2, 3, null));
        Assert.Equal(ThreeDGeometry.ColumnHeight(0), ThreeDGeometry.RiverSideBottom(2, 1, 0));
    }

    [Theory]
    [InlineData(CritterSpecies.Fish, true)]
    [InlineData(CritterSpecies.ToothedWhale, true)]
    [InlineData(CritterSpecies.BaleenWhale, true)]
    [InlineData(CritterSpecies.Squid, true)]
    [InlineData(CritterSpecies.Plankton, true)]
    [InlineData(CritterSpecies.SeaScorpion, true)]
    [InlineData(CritterSpecies.Ape, false)]
    [InlineData(CritterSpecies.ApeTrader, false)]
    [InlineData(CritterSpecies.ApeWarrior, false)]
    [InlineData(CritterSpecies.Deer, false)]
    public void OceanCrittersLieFlatUnderBridgesWhileLandCrittersStayAbove(CritterSpecies species, bool flat)
    {
        var habitat = CritterHabitats.GetHabitat(species);
        Assert.Equal(flat, ThreeDGeometry.FlattensUnderWaterCover(habitat, false, false, true, 5));
        Assert.False(ThreeDGeometry.FlattensUnderWaterCover(habitat, false, false, false, 0));
    }

    [Fact]
    public void IceKeepsItsClearanceRuleAndSurfaceVesselsStayUpright()
    {
        Assert.True(ThreeDGeometry.FlattensUnderWaterCover(CritterHabitat.OceanDweller, false, true, false, 0.5f));
        Assert.False(ThreeDGeometry.FlattensUnderWaterCover(CritterHabitat.OceanDweller, false, true, false, 2));
        Assert.False(ThreeDGeometry.FlattensUnderWaterCover(CritterHabitat.ShorelineHunter, true, false, true, 0));
    }

    [Theory]
    [InlineData(-0.5f)]
    [InlineData(-0.1f)]
    public void ShallowsBridgeDeckSitsAboveWaterRegardlessOfBedDepth(float bedElevation)
    {
        var bed = ThreeDGeometry.ColumnHeight(bedElevation);
        Assert.Equal(ThreeDGeometry.ColumnHeight(0) + 0.015f,
            ThreeDGeometry.RoadHeight(bed, 0, shallowsBridge: true));
        Assert.Equal(bed + 0.01f, ThreeDGeometry.RoadHeight(bed, 0, shallowsBridge: false));
        Assert.Equal(bed + 0.01f, ThreeDGeometry.RoadHeight(bed, null, shallowsBridge: true));
    }

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
