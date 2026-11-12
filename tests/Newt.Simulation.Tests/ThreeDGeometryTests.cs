using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ThreeDGeometryTests
{
    [Fact]
    public void PlanetoidCameraCarriesItsHeadingAcrossAFaceSeam()
    {
        const int width = 288, faceSize = 96;
        var start = new System.Numerics.Vector2(144, 0.25f);
        const float yaw = 0.7f;
        var (east, up, south) = PlanetoidGeometry.Frame(start.X, start.Y, width);
        var (end, endYaw) = PlanetoidGeometry.Move(start, new(0, -1), yaw, width);
        Assert.NotEqual(PlanetoidLayout.FaceAt(start.X, start.Y, faceSize),
            PlanetoidLayout.FaceAt(end.X, end.Y, faceSize));
        var (nextEast, nextUp, nextSouth) = PlanetoidGeometry.Frame(end.X, end.Y, width);
        var oldHeading = -east * MathF.Sin(yaw) - south * MathF.Cos(yaw);
        var newHeading = -nextEast * MathF.Sin(endYaw) - nextSouth * MathF.Cos(endYaw);
        var axis = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(up, nextUp));
        var angle = MathF.Acos(Math.Clamp(System.Numerics.Vector3.Dot(up, nextUp), -1f, 1f));
        var expected = System.Numerics.Vector3.Transform(oldHeading,
            System.Numerics.Quaternion.CreateFromAxisAngle(axis, angle));
        Assert.InRange(System.Numerics.Vector3.Distance(expected, newHeading), 0, 0.0001f);
    }

    [Theory]
    [InlineData(12.5f, 6.5f)]
    [InlineData(191.5f, 96.5f)]
    [InlineData(383.5f, 185.5f)]
    public void PlanetoidWorldMapRoundTripsAcrossTheGlobe(float x, float y)
    {
        const int width = 384, height = 192;
        var direction = PlanetoidLayout.MapDirection(x, y, width, height);
        var atlas = PlanetoidLayout.AtlasPosition(direction, width / 4);
        var face = PlanetoidLayout.FaceAt(atlas.X, atlas.Y, width / 4);
        var projected = PlanetoidLayout.MapPosition(
            PlanetoidLayout.Direction(atlas.X, atlas.Y, width / 4, face), width, height);
        Assert.Equal(x, projected.X, precision: 3);
        Assert.Equal(y, projected.Y, precision: 3);
    }

    [Fact]
    public void PlanetoidJoinsItsEquatorialAndPolarFaces()
    {
        const int size = 96, width = size * 3;
        for (var x = 0; x <= size; x++)
        {
            var front = PlanetoidGeometry.Position(new(x, 0, 0), width, new(0, 0));
            var north = PlanetoidGeometry.Position(new(size + x, 0, 2 * size), width, new(size, size));
            Assert.InRange((front - north).Length(), 0, 0.0001f);
        }
        var polarTile = new GridPosition(size + 24, 2 * size - 1);
        Assert.Equal(polarTile, PlanetoidLayout.Offset(new(24, 0), 0, -1, size));
        Assert.Equal(new GridPosition(24, 0), PlanetoidLayout.Offset(polarTile, 0, 1, size));
    }

    [Theory]
    [InlineData(42.5f, 0.5f)]
    [InlineData(144f, 144f)]
    [InlineData(287.5f, 191.5f)]
    public void PlanetoidProjectionPreservesTileCoordinatesAndCameraFrame(float x, float y)
    {
        const int width = 288;
        var position = PlanetoidGeometry.Position(new(x, 7, y), width);
        var local = PlanetoidGeometry.TilePosition(position, width);
        Assert.Equal(x, local.X, precision: 3);
        Assert.Equal(y, local.Z, precision: 3);
        Assert.Equal(7f, local.Y, precision: 3);
        var (east, up, south) = PlanetoidGeometry.Frame(x, y, width);
        Assert.InRange(System.Numerics.Vector3.Distance(System.Numerics.Vector3.Cross(east, up), south), 0, 0.0001f);
    }

    [Theory]
    [InlineData(448, 20.5f, 244.5f)]
    [InlineData(768, 600.25f, 216.25f)]
    public void RingFlightCrossesToTheOppositeSideOnlyWhenReachingTheCeiling(int width, float x, float opposite)
    {
        var ceiling = ThreeDGeometry.RingFlightCeiling(width);
        Assert.Equal(opposite, ThreeDGeometry.RingFlightCrossing(x, width, ceiling - 1, ceiling));
        Assert.Null(ThreeDGeometry.RingFlightCrossing(opposite, width, ceiling, ceiling));
        Assert.Null(ThreeDGeometry.RingFlightCrossing(x, width, ceiling - 2, ceiling - 1));
    }

    [Theory]
    [InlineData(true, false, 24f, 5.533333f)]
    [InlineData(false, true, 16f, 4.2f)]
    [InlineData(false, false, 19.666667f, 4.933333f)]
    public void VerticalBoostSpeedsUpActiveFlightAndSwimming(bool rising, bool sinking, float flight, float swim)
    {
        var result = ThreeDGeometry.FirstPersonFlightHeight(20, 0, null, true, rising, sinking,
            1f / 6, 32, boosted: true);
        var swimming = ThreeDGeometry.FirstPersonSwimmingHeight(5, 0, 6, rising, 1f / 6,
            fastSink: sinking, boosted: true);

        Assert.Equal(flight, result.Height, precision: 4);
        Assert.Equal(swim, swimming, precision: 4);
    }

    [Fact]
    public void NexusPlayerSpawnMovesOutOfTheCenterOpening()
    {
        var world = new SimulationWorld(96, 96, Terrain.Plains) { Body = WorldBody.Nexus };
        var center = new System.Numerics.Vector2(world.Width / 2f, world.Height / 2f);
        Assert.False(NexusLayout.Contains(world, new GridPosition((int)center.X, (int)center.Y)));

        var spawn = ThreeDGeometry.PlayerSpawnPosition(world, center);

        Assert.True(NexusLayout.Contains(world, new GridPosition((int)spawn.X, (int)spawn.Y)));
        Assert.Equal(spawn, ThreeDGeometry.PlayerSpawnPosition(world, spawn));
    }

    [Theory]
    [InlineData(64, 16, -8, -16)]
    [InlineData(64, 16, 0, 64)]
    [InlineData(0, 32, 16, 0)]
    [InlineData(0, 32, 32, 80)]
    [InlineData(0, 32, 96, 80)]
    public void WrappedChunkCopiesIncludePartiallyVisibleColumns(int start, int length, int visibleLeft, int expected)
    {
        Assert.Equal(expected, ThreeDGeometry.FirstVisibleChunkCopy(start, length, visibleLeft, 80));
    }

    [Fact]
    public void NexusPetalsLiftTowardTheirTipsWhileTheHubStaysFlat()
    {
        const float radius = 100f;
        Assert.Equal(0f, ThreeDGeometry.NexusPetalLift(0, 0, radius));
        Assert.Equal(0f, ThreeDGeometry.NexusPetalLift(0, 50, radius));
        var middle = ThreeDGeometry.NexusPetalLift(75, 0, radius);
        Assert.InRange(middle, 1f, 2f);
        Assert.Equal(middle, ThreeDGeometry.NexusPetalLift(0, -75, radius));
        Assert.Equal(8f, ThreeDGeometry.NexusPetalLift(100, 0, radius));
    }

    [Theory]
    [InlineData(96, 0)] // Walking camera.
    [InlineData(396, 28)] // Free camera.
    [InlineData(1320, 600)] // Zoomed-out free camera.
    [InlineData(1800, 28)] // Large ring world.
    public void RenderDistancePresetsReduceRangeWithoutClippingCameraFocus(float maximum, float orbitDistance)
    {
        var max = ThreeDGeometry.RenderDistance(maximum, 0, orbitDistance);
        var threeQuarters = ThreeDGeometry.RenderDistance(maximum, 1, orbitDistance);
        var half = ThreeDGeometry.RenderDistance(maximum, 2, orbitDistance);
        var quarter = ThreeDGeometry.RenderDistance(maximum, 3, orbitDistance);
        Assert.Equal(maximum, max);
        Assert.Equal(maximum * 0.75f, threeQuarters);
        Assert.Equal(maximum * 0.5f, half);
        Assert.Equal(Math.Max(maximum * 0.25f, orbitDistance + 16), quarter);
        Assert.True(quarter > orbitDistance + 14); // Includes terrain's full height range at the focus.
    }

    [Fact]
    public void FullWorldRenderDistanceCoversFlatAndCylindricalMaps()
    {
        var flat = ThreeDGeometry.FullWorldRenderDistance(768, 768, cylindrical: false);
        Assert.True(flat > MathF.Sqrt(2) * 768);
        var cylinder = ThreeDGeometry.FullWorldRenderDistance(448, 672, cylindrical: true);
        Assert.True(cylinder > 672);
        Assert.True(cylinder < ThreeDGeometry.FullWorldRenderDistance(448, 672, cylindrical: false));
        Assert.Equal(flat, ThreeDGeometry.RenderDistance(flat, 0));
        Assert.Equal(cylinder, ThreeDGeometry.RenderDistance(cylinder, 0));
    }

    [Fact]
    public void NexusFreeCameraCanSeeTheWholeWorldWithoutZoomingOut()
    {
        var size = WorldPreset.NexusMega;
        var world = new SimulationWorld(size.Width, size.Height, Terrain.Plains) { Body = WorldBody.Nexus };
        const float closeZoom = 28, distantZoom = 620;
        var nearView = ThreeDGeometry.WorldRenderDistance(world, ThreeDGeometry.FarClipDistance(closeZoom), 0, closeZoom);
        var farView = ThreeDGeometry.WorldRenderDistance(world, ThreeDGeometry.FarClipDistance(distantZoom), 0, distantZoom);

        Assert.True(nearView > MathF.Sqrt(2) * size.Width + closeZoom);
        Assert.True(farView > MathF.Sqrt(2) * size.Width + distantZoom);
        Assert.Equal(nearView, ThreeDGeometry.RenderDistance(nearView, 0, closeZoom));
    }

    [Theory]
    [InlineData(WorldBody.Terrarium, 1280, 642)]
    [InlineData(WorldBody.RingWorld, 1280, 44)]
    [InlineData(WorldBody.RingWorld, 5120, 44)]
    [InlineData(WorldBody.ONeillCylinder, 448, 336)]
    [InlineData(WorldBody.ONeillCylinder, 448, 672)]
    [InlineData(WorldBody.ONeillCylinder, 512, 1344)]
    public void AllCameraRangesUseTheFullMapAtMaximum(WorldBody body, int width, int height)
    {
        var world = new SimulationWorld(width, height, Terrain.Plains) { Body = body };
        foreach (var (minimumRange, cameraDistance) in new[] { (96f, 0f), (96f, 4f), (396f, 28f) })
        {
            var full = ThreeDGeometry.WorldRenderDistance(world, minimumRange, 0, cameraDistance);
            var crossSpan = world.IsRingHabitat ? width / MathF.PI : width;
            Assert.True(full > MathF.Sqrt((float)height * height + crossSpan * crossSpan) + cameraDistance);
            for (var level = 1; level < 4; level++)
                Assert.Equal(full * (4 - level) / 4, ThreeDGeometry.WorldRenderDistance(world, minimumRange, level, cameraDistance));
        }
    }

    [Fact]
    public void ChaseCameraFollowsPlayerHeightAndRotatesBehindLookDirection()
    {
        var target = new System.Numerics.Vector3(10, 5, 10);
        foreach (var look in new[] { System.Numerics.Vector3.UnitX, -System.Numerics.Vector3.UnitZ,
                     System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(1, -1, 1)) })
        {
            var eye = ThreeDGeometry.ThirdPersonCameraPosition(target, look, 4, _ => false);
            Assert.InRange(System.Numerics.Vector3.Distance(eye, target - look * 4), 0, 0.00001f);
            // Sinking/swimming moves the whole rig, without changing follow distance.
            var submerged = ThreeDGeometry.ThirdPersonCameraPosition(target - System.Numerics.Vector3.UnitY * 3,
                look, 4, _ => false);
            Assert.InRange(System.Numerics.Vector3.Distance(submerged, eye - System.Numerics.Vector3.UnitY * 3),
                0, 0.00001f);
        }
    }

    [Fact]
    public void ChaseCameraStopsBeforeWallsGroundAndThinIce()
    {
        var target = new System.Numerics.Vector3(0, 2, 0);
        var wall = ThreeDGeometry.ThirdPersonCameraPosition(target, -System.Numerics.Vector3.UnitZ, 4,
            p => p.Z >= 1.5f);
        Assert.InRange(wall.Z, 1.4f, 1.4999f);
        var ground = ThreeDGeometry.ThirdPersonCameraPosition(target, System.Numerics.Vector3.UnitY, 4,
            p => p.Y < 0.15f);
        Assert.InRange(ground.Y, 0.15f, 0.21f);
        var ice = ThreeDGeometry.ThirdPersonCameraPosition(target, -System.Numerics.Vector3.UnitY, 4,
            p => Math.Abs(p.Y - 3) < 0.15f);
        Assert.InRange(ice.Y, 2.8f, 2.85f);
    }

    [Fact]
    public void ChaseCameraFollowsRingSurfaceAcrossSeam()
    {
        const int width = 1280, height = 44;
        var frame = RingGeometry.Frame(width - 0.5f, width);
        var target = RingGeometry.Position(new System.Numerics.Vector3(width - 0.5f, 5, height / 2f), width, height);
        var eye = ThreeDGeometry.ThirdPersonCameraPosition(target, -frame.East, 4, p =>
            RingGeometry.StripPosition(p, width, height).Y < 0.15f);
        Assert.InRange(System.Numerics.Vector3.Distance(eye, target + frame.East * 4), 0, 0.0001f);
        var strip = RingGeometry.StripPosition(eye, width, height);
        Assert.InRange(strip.X, 0, 4);
        Assert.InRange(strip.Y, 4.9f, 5.1f);
    }

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
    public void SwimmingHeightKeepsTheCameraOnSolidGroundOrBridgeDecks()
    {
        var ground = ThreeDGeometry.RoadHeight(2, 0, shallowsBridge: true);
        var standing = ground + 1.2f;
        Assert.Equal(standing, ThreeDGeometry.FirstPersonSwimmingHeight(standing, ground, null, true, 1));
        Assert.Equal(standing, ThreeDGeometry.FirstPersonSwimmingHeight(standing, ground, null, false, 1));
    }

    [Fact]
    public void FirstPersonTakesOffAndDescendsAtTwoSpeeds()
    {
        const float ground = 2, maximumHeight = 12;
        var takeoff = ThreeDGeometry.FirstPersonFlightHeight(ground + 1.2f, ground, null,
            flying: false, rising: true, fastSink: false, seconds: 1, maximumHeight);
        Assert.True(takeoff.Flying);
        Assert.Equal(9.2f, takeoff.Height, precision: 4);

        var capped = ThreeDGeometry.FirstPersonFlightHeight(takeoff.Height, ground, null,
            flying: true, rising: true, fastSink: false, seconds: 1, maximumHeight);
        Assert.Equal(maximumHeight, capped.Height);
        var sinking = ThreeDGeometry.FirstPersonFlightHeight(capped.Height, ground, null,
            flying: true, rising: false, fastSink: false, seconds: 1, maximumHeight);
        var fastSink = ThreeDGeometry.FirstPersonFlightHeight(capped.Height, ground, null,
            flying: true, rising: false, fastSink: true, seconds: 1, maximumHeight);
        Assert.Equal(10f, sinking.Height);
        Assert.Equal(6f, fastSink.Height);

        var landed = ThreeDGeometry.FirstPersonFlightHeight(fastSink.Height, ground, null,
            flying: true, rising: false, fastSink: true, seconds: 1, maximumHeight);
        Assert.Equal((ground + 1.2f, false), landed);
    }

    [Fact]
    public void FirstPersonCanFlyOutOfOpenWaterAndSinkBackIntoIt()
    {
        const float water = 5, ground = 1, surfaceEye = water + 1.2f * 0.75f;
        var surfaced = ThreeDGeometry.FirstPersonFlightHeight(water, ground, water,
            flying: false, rising: true, fastSink: false, seconds: 2, maximumHeight: 32);
        Assert.Equal((surfaceEye, false), surfaced);
        var airborne = ThreeDGeometry.FirstPersonFlightHeight(surfaced.Height, ground, water,
            flying: false, rising: true, fastSink: false, seconds: 1, maximumHeight: 32);
        Assert.True(airborne.Flying);
        var landed = ThreeDGeometry.FirstPersonFlightHeight(airborne.Height, ground, water,
            flying: true, rising: false, fastSink: true, seconds: 2, maximumHeight: 32);
        Assert.Equal((surfaceEye, false), landed);
        var submerged = ThreeDGeometry.FirstPersonFlightHeight(landed.Height, ground, water,
            flying: false, rising: false, fastSink: true, seconds: 1, maximumHeight: 32);
        Assert.True(submerged.Height < water);
        Assert.False(submerged.Flying);
    }

    [Fact]
    public void FirstPersonCannotTakeOffThroughIce()
    {
        const float iceCeiling = 4.94f;
        var result = ThreeDGeometry.FirstPersonFlightHeight(4.5f, 1, 5,
            flying: false, rising: true, fastSink: false, seconds: 2, maximumHeight: 32, iceCeiling: iceCeiling);
        Assert.Equal((iceCeiling, false), result);
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
        var running = ThreeDGeometry.WalkingStep(0, 1, 0, 0.1f, true).Length();
        var ascent = ThreeDGeometry.FirstPersonFlightHeight(10, 0, null, true, true, false, 0.1f, 32,
            boosted: true);
        Assert.Equal(2.4f, running, precision: 5);
        Assert.Equal(running, ascent.Height - 10, precision: 5);
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
