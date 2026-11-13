using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool _view3D = true;
    private int _renderDistanceLevel3D; // Four distance levels, shared by the regular 3D camera modes.
    private int _megaCylinderRenderDistanceLevel3D;
    private int _megaCylinderFirstPersonDistanceLevel3D = 1;
    private bool _drawing3DAtlas;
    // Tint submerged objects while blending unchanged water tiles back to their 2D palette.
    private const int WaterSurfaceTintAlpha3D = 64;
    private const int WhaleFallCutout3D = -2;
    private const float WalkingSpriteScale3D = 0.8f;
    private bool _dayNightCycleEnabled = true;
    private static readonly (float Phase, Color Color)[] SkyCycle3D =
    [
        (0f, Color.SkyBlue),
        (0.20f, Color.SkyBlue),
        (0.32f, new Color(255, 166, 92)), // Sunset gold.
        (0.40f, new Color(203, 91, 130)), // Rose dusk.
        (0.48f, new Color(49, 36, 82)),
        (0.55f, Color.Black),
        (0.72f, Color.Black),
        (0.80f, new Color(64, 49, 103)),
        (0.88f, new Color(244, 151, 163)), // Dawn pink.
        (0.94f, new Color(255, 207, 151)), // Morning peach.
        (1f, Color.SkyBlue),
    ];

    private Color GetSkyColor3D()
    {
        if (!_dayNightCycleEnabled) return Color.SkyBlue;
        // Use simulation ticks so the sky follows pause/speed and works without seasons.
        var phase = SkyPhase3D();
        for (var i = 1; i < SkyCycle3D.Length; i++)
        {
            var (end, to) = SkyCycle3D[i];
            if (phase > end) continue;
            var (start, from) = SkyCycle3D[i - 1];
            var blend = MathHelper.SmoothStep(0, 1, (phase - start) / (end - start));
            return Color.Lerp(from, to, blend);
        }
        return Color.SkyBlue;
    }

    private float SkyPhase3D()
    {
        const long cycleTicks = 2L * SeasonSystem.TicksPerYear;
        return (_world.Tick % cycleTicks) / (float)cycleTicks;
    }

    private void DrawSun3D(Matrix view, Matrix projection)
    {
        var sun = ThreeDGeometry.SunDirection(_dayNightCycleEnabled ? SkyPhase3D() : 0.14f);
        // Cross the ring's width from north to south rather than following its circumference.
        var (east, up, south) = SurfaceFrame3D(_focus3D.X, _focus3D.Y);
        var sunriseDirection = IsRing3D ? -south : east;
        var direction = sunriseDirection * sun.X + up * sun.Y;
        // A direction (w = 0) keeps the sun infinitely distant as the camera moves.
        var clip = Vector4.Transform(new Vector4(direction, 0), view * projection);
        if (clip.W <= 0) return;
        var viewport = GraphicsDevice.Viewport;
        var x = (clip.X / clip.W + 1) * viewport.Width * 0.5f;
        var y = (1 - clip.Y / clip.W) * viewport.Height * 0.5f;
        var pixelSize = Math.Max(2, viewport.Height / 300);
        const int radius = 7;
        var extent = (radius + 1) * pixelSize;
        if (x < -extent || x > viewport.Width + extent || y < -extent || y > viewport.Height + extent) return;
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp, depthStencilState: DepthStencilState.None);
        for (var row = -radius; row <= radius; row++)
        {
            var halfWidth = (int)MathF.Floor(MathF.Sqrt(radius * radius - row * row));
            _spriteBatch.Draw(_pixel, new Rectangle(
                (int)x - halfWidth * pixelSize - pixelSize / 2,
                (int)y + row * pixelSize - pixelSize / 2,
                (halfWidth * 2 + 1) * pixelSize, pixelSize), Color.White);
        }
        _spriteBatch.End();
    }
    private Vector2 _focus3D;
    private float _yaw3D = MathHelper.PiOver4, _pitch3D = 0.85f, _distance3D = 28;
    private BasicEffect? _terrainEffect3D;
    private AlphaTestEffect? _cutoutEffect3D;
    private readonly List<RenderTarget2D> _atlasPages3D = [];
    private readonly List<VertexPositionColor> _terrainVertices3D = [];
    private readonly List<(Vector3 Position, GridPosition Tile, int Critter, bool Ground)> _cutouts3D = [];
    private const int AtlasSize3D = 2048, AtlasColumns3D = AtlasSize3D / 32;
    private const int AtlasCapacity3D = AtlasColumns3D * AtlasColumns3D;
    private float MaximumDistance3D => IsRing3D
        ? Math.Max(6, RingGeometry.Radius(_world.Width) - 15)
        : IsPlanetoid3D ? Math.Max(28, PlanetoidGeometry.Radius(_world.Width) * 3)
        : ThreeDGeometry.MaximumZoomDistance(MapViewportHeight, ZoomLevels[0]);

    private void ToggleWalkingCamera()
    {
        if (!_view3D) return;
        if (!IsWalkingCamera)
        {
            EnterFirstPerson();
            return;
        }

        _playerView = PlayerView.FreeCamera;
        _firstPersonMouseReady = false;
        _distance3D = Math.Clamp(28, 6, MaximumDistance3D);
        _pitch3D = 0.85f;
        IsMouseVisible = true;
        ResetToolRepeat();
    }

    private void Toggle3DView()
    {
        if (IsWalkingCamera) ToggleWalkingCamera();
        _view3D = !_view3D;
        ResetToolRepeat();
        if (_view3D)
        {
            _yaw3D = 0;
            _pitch3D = MathHelper.PiOver2;
            _focus3D = new Vector2(
                _cameraX + (GraphicsDevice.Viewport.Width / 2f - MapOffsetX) / TileSize,
                _cameraY + (MapViewportHeight / 2f - MapOffsetY) / TileSize);
            if (IsPlanetoid3D)
            {
                var tile = PlanetoidTileAtMapCenter((int)_focus3D.X, (int)_focus3D.Y);
                _focus3D = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
            }
            var distance = IsPlanetoid3D ? PlanetoidGeometry.Radius(_world.Width) * 2
                : ThreeDGeometry.OverheadDistance(MapViewportHeight, TileSize);
            _distance3D = Math.Clamp(distance,
                6, MaximumDistance3D);
            WrapCamera3D();
        }
        else
        {
            Match2DCameraTo3D();
        }
    }

    private void TogglePlayerPerspective()
    {
        if (!IsWalkingCamera) return;
        // Perspective changes keep the same walker. Never re-enter or reset its
        // position, height, ice state, heading, or future character attributes.
        if (_playerView is PlayerView.FirstPerson) RandomizePlayerGhostSprite();
        _playerView = _playerView is PlayerView.FirstPerson ? PlayerView.ThirdPerson : PlayerView.FirstPerson;
        _firstPersonMouseReady = false;
    }

    private void Match2DCameraTo3D()
    {
        if (HasCurvedSurface3D) { Match2DCameraToCurved3D(); return; }
        var (view, projection) = Matrices3D();
        var viewport = Viewport3D();
        var centerX = viewport.Width / 2;
        var centerY = viewport.Height / 2;
        var tile = PickTile3D(centerX, centerY) ?? MapTile3D((int)_focus3D.X, (int)_focus3D.Y);
        var height = IsIce3D(tile) && WaterElevation3D(tile) is { } surface
            ? ThreeDGeometry.ColumnHeight(surface) + 0.035f : TileHeight3D(tile);
        var near = viewport.Unproject(new Vector3(centerX, centerY, 0), projection, view, Matrix.Identity);
        var far = viewport.Unproject(new Vector3(centerX, centerY, 1), projection, view, Matrix.Identity);
        var direction = Vector3.Normalize(far - near);
        var center = new Vector3(_focus3D.X, height, _focus3D.Y);
        // Use the point actually under the screen center, even when the camera is tilted.
        if (Math.Abs(direction.Y) > 0.00001f)
        {
            var distance = (height - near.Y) / direction.Y;
            if (distance >= 0) center = near + direction * distance;
        }
        var projected = viewport.Project(center, projection, view, Matrix.Identity);
        var east = viewport.Project(center + Vector3.UnitX, projection, view, Matrix.Identity) - projected;
        var south = viewport.Project(center + Vector3.UnitZ, projection, view, Matrix.Identity) - projected;
        // Match projected tile area to approximate both dimensions of an oblique view.
        var pixelsPerTile = MathF.Sqrt(Math.Abs(east.X * south.Y - east.Y * south.X));
        _zoomIndex = ThreeDGeometry.NearestZoomIndex(pixelsPerTile, ZoomLevels);
        _cameraX = ConstrainCameraX((int)MathF.Round(center.X -
            (viewport.Width / 2f - MapOffsetX) / TileSize));
        _cameraY = Math.Clamp((int)MathF.Round(center.Z -
            (viewport.Height / 2f - MapOffsetY) / TileSize),
            0, Math.Max(0, _world.Height - viewport.Height / TileSize));
    }

    private void HandleCamera3D(KeyboardState keyboard, MouseState mouse, float seconds)
    {
        if (IsWalkingCamera)
        {
            HandleFirstPerson(keyboard, mouse, seconds);
            return;
        }
        seconds = Math.Min(seconds, 0.1f);
        var rotation = seconds * 1.5f;
        if (keyboard.IsKeyDown(Keys.Left)) _yaw3D -= rotation;
        if (keyboard.IsKeyDown(Keys.Right)) _yaw3D += rotation;
        if (keyboard.IsKeyDown(Keys.Up)) _pitch3D += rotation;
        if (keyboard.IsKeyDown(Keys.Down)) _pitch3D -= rotation;
        if (mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Pressed)
        {
            _yaw3D -= (mouse.X - _previousMouse.X) * 0.006f;
            _pitch3D += (mouse.Y - _previousMouse.Y) * 0.006f;
        }
        _pitch3D = Math.Clamp(_pitch3D, -MathHelper.Pi / 3, MathHelper.PiOver2);
        _yaw3D = MathHelper.WrapAngle(_yaw3D);
        var forward = new Vector2(-MathF.Sin(_yaw3D), -MathF.Cos(_yaw3D));
        var right = new Vector2(MathF.Cos(_yaw3D), -MathF.Sin(_yaw3D));
        var movement = Vector2.Zero;
        if (keyboard.IsKeyDown(Keys.W)) movement += forward;
        if (keyboard.IsKeyDown(Keys.S)) movement -= forward;
        if (keyboard.IsKeyDown(Keys.A)) movement -= right;
        if (keyboard.IsKeyDown(Keys.D)) movement += right;
        if (movement.LengthSquared() > 0) movement.Normalize();
        var speed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 2.5f : 1;
        MoveCameraOnMap3D(movement * seconds * _distance3D * 0.65f * speed);
        _distance3D = Math.Clamp(_distance3D * MathF.Pow(0.85f,
            (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120f), 6, MaximumDistance3D);
        WrapCamera3D();
    }

    private void WrapCamera3D()
    {
        // Rebase east/west while keeping the north/south edges finite.
        _focus3D.X = _world.Body == WorldBody.Nexus
            ? Math.Clamp(_focus3D.X, 0, _world.Width - 1)
            : ThreeDGeometry.WrapCamera(_focus3D.X, _world.Width);
        _focus3D.Y = IsPlanetoid3D ? Math.Clamp(_focus3D.Y, 0, _world.Height - 0.001f)
            : Math.Clamp(_focus3D.Y, 0, _world.Height - 1);
    }

    private void MoveCameraOnMap3D(Vector2 step)
    {
        if (!IsPlanetoid3D)
        {
            _focus3D += step;
            return;
        }
        var (atlas, yaw) = PlanetoidGeometry.Move(new(_focus3D.X, _focus3D.Y), new(step.X, step.Y), _yaw3D, _world.Width);
        _focus3D = new Vector2(atlas.X, atlas.Y);
        _yaw3D = yaw;
    }

    private Vector3 EyeOffset3D()
    {
        // Looking above the horizon rotates the view without putting the eye underground.
        var orbitPitch = Math.Max(_pitch3D, 0.25f);
        return new Vector3(MathF.Sin(_yaw3D) * MathF.Cos(orbitPitch) * _distance3D,
            MathF.Sin(orbitPitch) * _distance3D,
            MathF.Cos(_yaw3D) * MathF.Cos(orbitPitch) * _distance3D);
    }

    private Vector3 CameraLookDirection3D(Vector3 east, Vector3 up, Vector3 south)
    {
        var local = ThreeDGeometry.LookDirection(_yaw3D, _pitch3D);
        return east * local.X + up * local.Y + south * local.Z;
    }

    private Viewport Viewport3D() => new(0, 0, GraphicsDevice.Viewport.Width, MapViewportHeight);

    private int RenderDistanceLevel3D => !IsMegaCylinder3D ? _renderDistanceLevel3D :
        _playerView is PlayerView.FirstPerson ? _megaCylinderFirstPersonDistanceLevel3D :
        _megaCylinderRenderDistanceLevel3D;

    private float RenderDistance3D(float minimumRange, float cameraDistance = 0) =>
        ThreeDGeometry.WorldRenderDistance(_world, minimumRange, RenderDistanceLevel3D, cameraDistance);

    private void CycleRenderDistance3D()
    {
        var next = (RenderDistanceLevel3D + 1) % ThreeDGeometry.RenderDistanceLevels;
        if (!IsMegaCylinder3D)
            _renderDistanceLevel3D = next;
        else if (_playerView is PlayerView.FirstPerson)
            _megaCylinderFirstPersonDistanceLevel3D = next;
        else
            _megaCylinderRenderDistanceLevel3D = next;
    }

    private (Matrix View, Matrix Projection) Matrices3D()
    {
        if (IsWalkingCamera) return FirstPersonMatrices3D();
        if (IsRing3D || IsPlanetoid3D) return CurvedOrbitMatrices3D();
        var tile = MapTile3D((int)_focus3D.X, (int)_focus3D.Y);
        var targetHeight = ContainsTile3D((int)_focus3D.X, (int)_focus3D.Y)
            ? Math.Max(4.5f, TileHeight3D(tile)) : 4.5f;
        var target = WorldPoint3D(new Vector3(_focus3D.X, targetHeight, _focus3D.Y));
        var eye = target + EyeOffset3D();
        var localEye = LocalPoint3D(eye);
        var eyeX = (int)MathF.Floor(localEye.X);
        var eyeY = (int)MathF.Floor(localEye.Z);
        if (_world.Body is not WorldBody.Nexus || ContainsTile3D(eyeX, eyeY))
        {
            localEye.Y = Math.Max(localEye.Y, TileHeight3D(MapTile3D(eyeX, eyeY)) + 1.5f);
            eye = WorldPoint3D(localEye);
        }
        var cameraDistance = Vector3.Distance(eye, target);
        if (_pitch3D < 0.25f) target = eye + CameraLookDirection3D(Vector3.UnitX, Vector3.Up, Vector3.UnitZ) * _distance3D;
        var cameraUp = new Vector3(-MathF.Sin(_yaw3D) * MathF.Sin(_pitch3D),
            MathF.Cos(_pitch3D), -MathF.Cos(_yaw3D) * MathF.Sin(_pitch3D));
        var far = ThreeDGeometry.FarClipDistance(_distance3D);
        return (Matrix.CreateLookAt(eye, target, cameraUp),
            Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Viewport3D().AspectRatio, 0.1f,
                RenderDistance3D(far, cameraDistance)));
    }

    private float TileHeight3D(GridPosition tile) =>
        ThreeDGeometry.ColumnHeight(_world.GetElevation(tile));

    private GridPosition MapTile3D(int x, int y) => new(
        _world.Body == WorldBody.Nexus ? Math.Clamp(x, 0, _world.Width - 1)
            : ThreeDGeometry.WrapTile(x, _world.Width), Math.Clamp(y, 0, _world.Height - 1));

    private bool ContainsTile3D(int x, int y) =>
        y >= 0 && y < _world.Height &&
        (_world.Body != WorldBody.Nexus || x >= 0 && x < _world.Width &&
            NexusLayout.Contains(_world, new GridPosition(x, y)));

    private float? WaterElevation3D(GridPosition tile) => ThreeDGeometry.WaterElevation(
        _world.GetTerrain(tile), _world.GetSurfaceWater(tile), _world.HasOceans,
        _world.SeaLevel, _world.GetWaterSurfaceElevation(tile));

    private bool IsIce3D(GridPosition tile) => ThreeDGeometry.IsIce(
        _world.GetTerrain(tile), _world.GetSurfaceWater(tile), _world.GetBiome(tile));

    private bool IsSurfaceSailor3D(CritterSnapshot critter) => ThreeDGeometry.IsSurfaceSailor(
        critter.Species, critter.IsColonist, _world.IsApePirate(critter.Id), critter.IsVeteranWarrior);

    private bool ShouldFlattenUnderWaterCover3D(CritterSnapshot critter) =>
        WaterElevation3D(critter.Position) is { } waterSurface &&
        ThreeDGeometry.FlattensUnderWaterCover(CritterHabitats.GetHabitat(critter.Species),
            IsSurfaceSailor3D(critter), IsIce3D(critter.Position), _world.HasRoadBridge(critter.Position),
            ThreeDGeometry.ColumnHeight(waterSurface) - (TileHeight3D(critter.Position) + 0.025f) - 0.02f);

    private Terrain UnfrozenBedTerrain(GridPosition tile) =>
        IsIce3D(tile) && WaterElevation3D(tile) is { } water
            ? water - _world.GetElevation(tile) > 0.20f ? Terrain.DeepOcean : Terrain.Ocean
            : _world.GetTerrain(tile);

    private GridPosition? PickTile3D(int x, int y)
    {
        var viewport = Viewport3D();
        if (x < 0 || x >= viewport.Width || y < 0 || y >= viewport.Height) return null;
        var (view, projection) = Matrices3D();
        var near = viewport.Unproject(new Vector3(x, y, 0), projection, view, Matrix.Identity);
        var far = viewport.Unproject(new Vector3(x, y, 1), projection, view, Matrix.Identity);
        var ray = new Ray(near, Vector3.Normalize(far - near));
        GridPosition? picked = null;
        var nearest = float.MaxValue;
        EnsureTerrainChunks3D();
        var visibleTiles = VisibleBounds3D(new BoundingFrustum(view * projection));
        foreach (var chunk in GetChunkViews3D(visibleTiles))
            if (ray.Intersects(chunk.Bounds) is { } entry && entry < nearest)
                PickWithin(chunk.Tiles);
        return picked;

        void PickWithin(Rectangle bounds)
        {
            for (var ty = bounds.Top; ty < bounds.Bottom; ty++)
            for (var tx = bounds.Left; tx < bounds.Right; tx++)
            {
                if (!ContainsTile3D(tx, ty)) continue;
                var tile = MapTile3D(tx, ty);
                var height = IsIce3D(tile) && WaterElevation3D(tile) is { } surface
                    ? ThreeDGeometry.ColumnHeight(surface) + 0.035f : TileHeight3D(tile);
                var distance = HasCurvedSurface3D ? CurvedColumnHit3D(ray, tx, ty, height)
                    : ThreeDGeometry.IntersectColumn(ray.Position.X, ray.Position.Y, ray.Position.Z,
                        ray.Direction.X, ray.Direction.Y, ray.Direction.Z, tx, ty, height);
                if (distance is { } hit && hit < nearest)
                {
                    var projected = viewport.Project(ray.Position + ray.Direction * hit, projection, view, Matrix.Identity);
                    if (projected.Z >= 0 && projected.Z <= 1) { nearest = hit; picked = tile; }
                }
            }
        }
    }

    private Rectangle VisibleBounds3D(BoundingFrustum frustum)
    {
        // Closed surfaces use their world-space chunk bounds for visibility.
        if (IsRing3D || IsPlanetoid3D) return new Rectangle(0, 0, _world.Width, _world.Height);
        // Intersect the view frustum with the terrain's vertical range. This avoids
        // scanning a huge square of invisible tiles at the larger zoom distances.
        var corners = frustum.GetCorners();
        var minimum = new Vector2(float.MaxValue);
        var maximum = new Vector2(float.MinValue);
        var maximumHeight = _world.Body is WorldBody.Nexus
            ? 14f + ThreeDGeometry.NexusMaximumPetalLift(NexusLayout.Radius(_world)) : 14f;
        void Include(Vector3 point)
        {
            var p = new Vector2(point.X, point.Z);
            minimum = Vector2.Min(minimum, p);
            maximum = Vector2.Max(maximum, p);
        }
        foreach (var corner in corners)
            if (corner.Y >= 0 && corner.Y <= maximumHeight) Include(corner);
        for (var i = 0; i < 4; i++)
        {
            Clip(corners[i], corners[(i + 1) % 4]);
            Clip(corners[i + 4], corners[(i + 1) % 4 + 4]);
            Clip(corners[i], corners[i + 4]);
        }
        void Clip(Vector3 a, Vector3 b)
        {
            if (Math.Abs(a.Y - b.Y) < 0.000001f) return;
            foreach (var plane in new[] { 0f, maximumHeight })
            {
                var t = (plane - a.Y) / (b.Y - a.Y);
                if (t >= 0 && t <= 1) Include(Vector3.Lerp(a, b, t));
            }
        }
        if (minimum.X == float.MaxValue) return Rectangle.Empty;
        var left = (int)MathF.Floor(minimum.X) - 2;
        var right = (int)MathF.Ceiling(maximum.X) + 2;
        if (_world.Body == WorldBody.Nexus)
        {
            left = Math.Clamp(left, 0, _world.Width);
            right = Math.Clamp(right, left, _world.Width);
        }
        else
        {
            // Show each longitude once when zoomed out, shifting the displayed seam
            // with the camera as in 2D. Close views retain continuous edge crossing.
            (left, right) = ThreeDGeometry.SingleMapSpan(left, right, _focus3D.X, _world.Width);
        }
        var top = Math.Clamp((int)MathF.Floor(minimum.Y) - 2, 0, _world.Height);
        var bottom = Math.Clamp((int)MathF.Ceiling(maximum.Y) + 2, top, _world.Height);
        return new Rectangle(left, top, right - left,
            bottom - top);
    }

    private void DrawWorld3D()
    {
        WrapCamera3D();
        _terrainEffect3D ??= new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        _cutoutEffect3D ??= new AlphaTestEffect(GraphicsDevice)
        { VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 100 };
        _terrainVertices3D.Clear();
        _cutouts3D.Clear();
        var (view, projection) = Matrices3D();
        var frustum = new BoundingFrustum(view * projection);
        var bounds = VisibleBounds3D(frustum);
        var hovered = ScreenToWorld(Mouse.GetState().X, Mouse.GetState().Y);
        PrepareTerrainChunks3D(frustum, bounds);
        foreach (var chunk in _visibleChunks3D)
            CollectTerrainDetails3D(chunk.Tiles, frustum, hovered);
        for (var i = 0; i < _world.CritterCount; i++)
        {
            var critter = _world.GetCritter(i);
            var tile = critter.Position;
            if (!ContainsTile3D(tile.X, tile.Y) || tile.Y < bounds.Top || tile.Y >= bounds.Bottom) continue;
            var height = TileHeight3D(tile);
            if (_world.HasRoadBridge(tile) &&
                ThreeDGeometry.StandsOnIce(CritterHabitats.GetHabitat(critter.Species)))
                height = ThreeDGeometry.RoadHeight(height, WaterElevation3D(tile), shallowsBridge: true);
            if (IsIce3D(tile) && WaterElevation3D(tile) is { } surface &&
                ThreeDGeometry.StandsOnIce(CritterHabitats.GetHabitat(critter.Species)))
                height = Math.Max(height, ThreeDGeometry.ColumnHeight(surface) + 0.035f);
            if (WaterElevation3D(tile) is { } waterSurface &&
                IsSurfaceSailor3D(critter))
                height = Math.Max(height, ThreeDGeometry.ColumnHeight(waterSurface) + 0.035f);
            var spriteHeight = height + 0.025f;
            if (!IsIce3D(tile) && !_world.HasRoadBridge(tile) && IsSurfaceSailor3D(critter) &&
                CritterHabitats.GetHabitat(critter.Species) is not CritterHabitat.Flier &&
                WaterElevation3D(tile) is { } waterline)
            {
                // Sink the bottom of surface vessels into open water to give them a waterline.
                spriteHeight = Math.Max(TileHeight3D(tile) + 0.025f,
                    ThreeDGeometry.ColumnHeight(waterline) - 0.12f);
            }
            if (critter.Species is CritterSpecies.Plankton && WaterElevation3D(tile) is { } planktonSurface)
            {
                // Open water sits at +0.005; ice spans +0.005 to +0.035.
                // Float above open water, or immediately below the ice's underside.
                spriteHeight = Math.Max(TileHeight3D(tile) + 0.001f,
                    ThreeDGeometry.ColumnHeight(planktonSurface) + (IsIce3D(tile) ? 0f : 0.015f));
            }
            if (ShouldFlattenUnderWaterCover3D(critter))
                spriteHeight = ThreeDGeometry.ColumnHeight(WaterElevation3D(tile)!.Value);
            var firstX = _world.Body == WorldBody.Nexus ? tile.X
                : ThreeDGeometry.FirstVisibleCopy(tile.X, bounds.Left, _world.Width);
            for (var x = firstX; x < bounds.Right; x += _world.Width)
            {
                if (x < bounds.Left) continue;
                var position = new Vector3(x + 0.5f, spriteHeight, tile.Y + 0.5f);
                if (frustum.Contains(new BoundingSphere(WorldPoint3D(position), 2)) == ContainmentType.Disjoint) continue;
                _cutouts3D.Add((position, tile, i, false));
            }
        }
        // Render the existing artwork into reusable atlas pages before drawing any depth geometry.
        // Wrapped copies share artwork, keeping atlas memory bounded by real objects.
        var artwork = _cutouts3D.DistinctBy(item => (item.Tile, item.Critter, item.Ground)).ToArray();
        var artworkSlots = artwork.Select((item, slot) => (item, slot))
            .ToDictionary(entry => (entry.item.Tile, entry.item.Critter, entry.item.Ground), entry => entry.slot);
        var pages = (artwork.Length + AtlasCapacity3D - 1) / AtlasCapacity3D;
        while (_atlasPages3D.Count < pages)
            _atlasPages3D.Add(new RenderTarget2D(GraphicsDevice, AtlasSize3D, AtlasSize3D));
        _drawing3DAtlas = true;
        try
        {
            for (var page = 0; page < pages; page++)
            {
                GraphicsDevice.SetRenderTarget(_atlasPages3D[page]);
                GraphicsDevice.Clear(Color.Transparent);
                _spriteBatch!.Begin(samplerState: SamplerState.PointClamp);
                var end = Math.Min(artwork.Length, (page + 1) * AtlasCapacity3D);
                for (var i = page * AtlasCapacity3D; i < end; i++)
                {
                    var item = artwork[i];
                    var slot = i % AtlasCapacity3D;
                    var sx = slot % AtlasColumns3D;
                    var sy = slot / AtlasColumns3D;
                    if (item.Critter >= 0) DrawCritter(_world.GetCritter(item.Critter), sx, sy);
                    else if (item.Critter == WhaleFallCutout3D) DrawWhaleFall(sx, sy, item.Tile);
                    else if (item.Ground)
                    {
                        var water = _world.GetSurfaceWater(item.Tile);
                        if (water is not (SurfaceWaterKind.None or SurfaceWaterKind.FreshwaterLake))
                            DrawSurfaceWater(sx, sy, item.Tile, water);
                        DrawVillageRoad(sx, sy, item.Tile);
                        DrawImpactWave(sx, sy, item.Tile);
                    }
                    else
                    {
                        DrawApeStructure(sx, sy, item.Tile);
                        DrawWolfDen(sx, sy, item.Tile);
                        DrawMegaSpiderWeb(sx, sy, item.Tile);
                        DrawTeleporter(sx, sy, item.Tile);
                        DrawCustodianFabricator(sx, sy, item.Tile);
                        DrawVolcanoVent(sx, sy, item.Tile);
                    }
                }
                _spriteBatch.End();
            }
        }
        finally { _drawing3DAtlas = false; GraphicsDevice.SetRenderTarget(null); }
        var originalViewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = Viewport3D();
        GraphicsDevice.Clear(GetSkyColor3D());
        DrawSun3D(view, projection);
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _terrainEffect3D.View = view;
        _terrainEffect3D.Projection = projection;
        DrawTerrainChunks3D(static chunk => chunk.Terrain);
        var vertices = _terrainVertices3D.ToArray();
        if (vertices.Length > 0)
        foreach (var pass in _terrainEffect3D.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length / 3);
        }
        _cutoutEffect3D.View = view;
        _cutoutEffect3D.Projection = projection;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        var cameraWorld = Matrix.Invert(view);
        // Share the camera's orientation across all sprites. Per-sprite look-at
        // directions cause radial rotation and foreshortening in overhead views.
        var right = cameraWorld.Right * 0.5f;
        var up = cameraWorld.Up * 0.5f;
        for (var page = 0; page < pages; page++)
        {
            var quads = new List<VertexPositionColorTexture>();
            for (var i = 0; i < _cutouts3D.Count; i++)
            {
                var item = _cutouts3D[i];
                var artworkSlot = artworkSlots[(item.Tile, item.Critter, item.Ground)];
                if (artworkSlot / AtlasCapacity3D != page) continue;
                var slot = artworkSlot % AtlasCapacity3D;
                var u = (slot % AtlasColumns3D) / (float)AtlasColumns3D;
                var v = (slot / AtlasColumns3D) / (float)AtlasColumns3D;
                var step = 1f / AtlasColumns3D;
                var p = _cutouts3D[i].Position;
                // Screen-aligned sprites stay flat and upright even directly overhead.
                var scale = IsWalkingCamera ? WalkingSpriteScale3D : 1f;
                var underWaterCover = item.Critter >= 0 && ShouldFlattenUnderWaterCover3D(_world.GetCritter(item.Critter));
                var center = WorldPoint3D(p) + SurfaceUp3D(p.X, p.Z) * (0.5f * scale);
                var spriteRight = right * scale;
                var spriteUp = up * scale;
                var a = new VertexPositionColorTexture(center - spriteRight + spriteUp, Color.White, new Vector2(u, v));
                var b = new VertexPositionColorTexture(center + spriteRight + spriteUp, Color.White, new Vector2(u + step, v));
                var c = new VertexPositionColorTexture(center + spriteRight - spriteUp, Color.White, new Vector2(u + step, v + step));
                var d = new VertexPositionColorTexture(center - spriteRight - spriteUp, Color.White, new Vector2(u, v + step));
                // Aquatic critters lie beneath bridge decks and low ice sheets.
                var flat = underWaterCover || item.Ground || (item.Critter >= 0
                    ? _world.GetCritter(item.Critter).Species is CritterSpecies.Plankton or CritterSpecies.Trilobite
                    : _world.GetApeStructure(item.Tile) is ApeStructureKind.Farm or ApeStructureKind.BlightedFarm or
                        ApeStructureKind.RicePaddy or ApeStructureKind.Aquaculture ||
                        _world.GetVolcanoState(item.Tile) is { } volcanoState && volcanoState != VolcanoState.Extinct);
                if (flat)
                {
                    var halfSize = IsWalkingCamera && item.Critter >= 0 ? WalkingSpriteScale3D * 0.5f : 0.5f;
                    a.Position = WorldPoint3D(p + new Vector3(-halfSize, 0, -halfSize), item.Tile);
                    b.Position = WorldPoint3D(p + new Vector3(halfSize, 0, -halfSize), item.Tile);
                    c.Position = WorldPoint3D(p + new Vector3(halfSize, 0, halfSize), item.Tile);
                    d.Position = WorldPoint3D(p + new Vector3(-halfSize, 0, halfSize), item.Tile);
                }
                quads.AddRange([a, b, c, a, c, d]);
            }
            _cutoutEffect3D.Texture = _atlasPages3D[page];
            var data = quads.ToArray();
            foreach (var pass in _cutoutEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3);
            }
        }
        DrawPlayerGhost3D(view, projection);
        DrawWaterSurfaces3D();
        GraphicsDevice.Viewport = originalViewport;
    }

    private Color TerrainColumnColor3D(GridPosition tile) =>
        _world.GetSurfaceWater(tile) is SurfaceWaterKind.FreshwaterLake
            ? GetLakeColor(_world.GetWaterDepth(tile), frozen: false)
            : GetTileColor(tile, UnfrozenBedTerrain(tile), _world.GetBiome(tile), _world.GetTemperatureBand(tile));

    private void CollectTerrainDetails3D(Rectangle bounds, BoundingFrustum frustum, GridPosition? hovered)
    {
        var hasImpactWave = _world.ActiveImpactWaveCount > 0;
        for (var y = bounds.Top; y < bounds.Bottom; y++)
        for (var x = bounds.Left; x < bounds.Right; x++)
        {
            if (!ContainsTile3D(x, y)) continue;
            var tile = MapTile3D(x, y);
            var water = _world.GetSurfaceWater(tile);
            var river = water is not (SurfaceWaterKind.None or SurfaceWaterKind.FreshwaterLake);
            var roads = _world.GetRoadConnections(tile);
            var whaleFall = _world.GetWhaleFallNutrition(tile) > 0;
            var hasGroundArt = river || roads != RiverConnection.None || hasImpactWave;
            var hasStructure = _world.GetApeStructure(tile) is not null || _world.HasVampireLair(tile) ||
                _world.GetWolfDenCharges(tile) is not null || _world.GetMegaSpiderWebFood(tile) is not null ||
                _world.HasTeleporter(tile) || _world.IsCustodianFabricator(tile) ||
                _world.GetVolcanoState(tile) is { } state && state != VolcanoState.Extinct;
            var drawColumn = _world.GetSurfaceCover(tile) is SurfaceCover.Lava;
            if (!drawColumn && hovered != tile && !whaleFall && !hasGroundArt && !hasStructure) continue;

            var height = TileHeight3D(tile);
            var waterElevation = WaterElevation3D(tile);
            var top = Math.Max(height, ThreeDGeometry.ColumnHeight(waterElevation ?? _world.GetElevation(tile)));
            if (!frustum.Intersects(TileBounds3D(x, y, top + 2))) continue;
            if (drawColumn || hovered == tile)
            {
                var color = TerrainColumnColor3D(tile);
                if (hovered == tile) color = Color.Lerp(color, Color.White, 0.45f);
                AddColumn3D(new GridPosition(x, y), drawColumn ? height : height + 0.003f, color,
                    topOnly: !drawColumn);
                if (drawColumn)
                    AddWaterTile3D(tile, x, y, _dynamicWater3D, _dynamicIce3D);
            }

            // Carcasses stay on the bed, including beneath ice and bridge decks.
            if (whaleFall)
                _cutouts3D.Add((new Vector3(x + 0.5f, height + 0.012f, y + 0.5f), tile, WhaleFallCutout3D, true));
            if (river)
                AddConnectionSides3D(x, y, height + 0.01f, _world.GetRiverConnections(tile), GetSurfaceWaterColor(water));
            if (roads != RiverConnection.None)
                AddConnectionSides3D(x, y,
                    ThreeDGeometry.RoadHeight(height, waterElevation, _world.HasRoadBridge(tile)),
                    roads, GetRoadColor(tile), road: true);
            if (hasGroundArt)
                _cutouts3D.Add((new Vector3(x + 0.5f,
                    ThreeDGeometry.RoadHeight(height, waterElevation, _world.HasRoadBridge(tile)),
                    y + 0.5f), tile, -1, true));
            if (hasStructure)
                _cutouts3D.Add((new Vector3(x + 0.5f, IsIce3D(tile) ? top + 0.05f : height + 0.015f, y + 0.5f),
                    tile, -1, false));
        }
    }

    private void DrawWaterSurfaces3D()
    {
        // Composite after opaque terrain and cutouts, tinting submerged objects without
        // hiding them or blocking subsequent depth tests with transparent fragments.
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        DrawTerrainChunks3D(static chunk => chunk.Ice);
        DrawUnbufferedWater3D(_dynamicIce3D);
        DrawTerrainChunks3D(static chunk => chunk.Water);
        DrawUnbufferedWater3D(_dynamicWater3D);
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void DrawUnbufferedWater3D(List<VertexPositionColor> vertices)
    {
        if (vertices.Count == 0) return;
        CurveWaterVertices3D(vertices);
        var data = vertices.ToArray();
        foreach (var pass in _terrainEffect3D!.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3);
        }
    }

    private void AddWaterTile3D(GridPosition tile, int x, int y,
        List<VertexPositionColor> vertices, List<VertexPositionColor> iceVertices)
    {
        // Lakes use their own spill elevation, including on worlds without oceans.
        // Pick one surface per tile so lakes below sea level do not get two sheets.
        var surface = WaterElevation3D(tile);
        if (surface is null || _world.GetElevation(tile) >= surface.Value) return;
        var ice = IsIce3D(tile);
        var height = ThreeDGeometry.ColumnHeight(surface.Value) + (ice ? 0.035f : 0.005f);
        // Matching the bed's palette avoids shifting its final color:
        // target * alpha + target * (1 - alpha) = target.
        var waterColor = _world.GetSurfaceWater(tile) is SurfaceWaterKind.FreshwaterLake
            ? GetLakeColor(_world.GetWaterDepth(tile), frozen: false)
            : GetTileColor(tile, UnfrozenBedTerrain(tile), _world.GetBiome(tile), _world.GetTemperatureBand(tile));
        var color = ice ? new Color(GetTerrainColor(Terrain.Ice, _world.GetBiome(tile),
            _world.GetTemperatureBand(tile), _world.GetElevation(tile)), 210)
            : new Color(waterColor, WaterSurfaceTintAlpha3D);
        var waterStart = vertices.Count;
        var iceStart = iceVertices.Count;
        var a = new VertexPositionColor(new Vector3(x, height, y), color);
        var b = new VertexPositionColor(new Vector3(x + 1, height, y), color);
        var c = new VertexPositionColor(new Vector3(x + 1, height, y + 1), color);
        var d = new VertexPositionColor(new Vector3(x, height, y + 1), color);
        (ice ? iceVertices : vertices).AddRange([a, b, c, a, c, d]);
        if (ice)
        {
            // A thin slab, rather than a white column down to the lake/ocean bed.
            void Edge(Vector3 p, Vector3 q)
            {
                var lowerP = p - Vector3.Up * 0.03f;
                var lowerQ = q - Vector3.Up * 0.03f;
                var shade = new Color(new Vector4(color.ToVector3() * 0.8f, 0.3f));
                iceVertices.AddRange([new(p, shade), new(q, shade), new(lowerQ, shade),
                    new(p, shade), new(lowerQ, shade), new(lowerP, shade)]);
            }
            Edge(a.Position, b.Position);
            Edge(b.Position, c.Position);
            Edge(c.Position, d.Position);
            Edge(d.Position, a.Position);
        }
        if (IsPlanetoid3D)
        {
            var surfaceVertices = ice ? iceVertices : vertices;
            for (var index = ice ? iceStart : waterStart; index < surfaceVertices.Count; index++)
            {
                var vertex = surfaceVertices[index];
                vertex.Position = WorldPoint3D(vertex.Position, tile);
                surfaceVertices[index] = vertex;
            }
        }
    }

    private void AddConnectionSides3D(int x, int y, float topHeight, RiverConnection connections, Color color,
        bool road = false)
    {
        var offset = road ? 0.003f : 0.002f;
        foreach (var segment in ThreeDGeometry.ConnectionSideSegments(connections))
        {
            if (!ContainsTile3D(x + segment.TargetX, y + segment.TargetY) ||
                !ContainsTile3D(x + segment.FaceX, y + segment.FaceY)) continue;
            var target = MapTile3D(x + segment.TargetX, y + segment.TargetY);
            var adjacent = MapTile3D(x + segment.FaceX, y + segment.FaceY);
            // Stop at the receiving surface and clip against the adjacent column.
            var bottom = road
                ? ThreeDGeometry.RoadSideBottom(TileHeight3D(target), TileHeight3D(adjacent),
                    WaterElevation3D(target), _world.HasRoadBridge(target))
                : ThreeDGeometry.RiverSideBottom(TileHeight3D(target), TileHeight3D(adjacent), WaterElevation3D(target));
            if (bottom >= topHeight - (road ? offset : 0.012f)) continue;
            Vector3 a, b;
            if (segment.FaceX != 0)
            {
                var edge = x + (segment.FaceX > 0 ? 1 + offset : -offset);
                a = new(edge, bottom, y + segment.Start);
                b = new(edge, bottom, y + segment.End);
            }
            else
            {
                var edge = y + (segment.FaceY > 0 ? 1 + offset : -offset);
                a = new(x + segment.Start, bottom, edge);
                b = new(x + segment.End, bottom, edge);
            }
            var c = b with { Y = topHeight };
            var d = a with { Y = topHeight };
            var owner = new GridPosition(x, y);
            a = WorldPoint3D(a, owner);
            b = WorldPoint3D(b, owner);
            c = WorldPoint3D(c, owner);
            d = WorldPoint3D(d, owner);
            _terrainVertices3D.AddRange([new(a, color), new(b, color), new(c, color),
                new(a, color), new(c, color), new(d, color)]);
        }
    }

    private void AddColumn3D(GridPosition tile, float height, Color color, bool topOnly = false)
    {
        float x = tile.X, z = tile.Y;
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color shade)
        {
            a = WorldPoint3D(a, tile);
            b = WorldPoint3D(b, tile);
            c = WorldPoint3D(c, tile);
            d = WorldPoint3D(d, tile);
            _terrainVertices3D.AddRange([new(a, shade), new(b, shade), new(c, shade),
                new(a, shade), new(c, shade), new(d, shade)]);
        }
        Face(new(x, height, z), new(x + 1, height, z), new(x + 1, height, z + 1), new(x, height, z + 1), color);
        if (topOnly) return;
        if (IsRing3D)
            Face(new(x, 0, z), new(x, 0, z + 1), new(x + 1, 0, z + 1), new(x + 1, 0, z),
                new Color(color.ToVector3() * 0.45f));
        void Side(int nx, int ny, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float shade)
        {
            if (IsPlanetoid3D)
            {
                var neighbor = PlanetoidLayout.Offset(tile, nx - tile.X, ny - tile.Y, PlanetoidLayout.FaceSize(_world.Width));
                nx = neighbor.X;
                ny = neighbor.Y;
            }
            if (!ContainsTile3D(nx, ny) || TileHeight3D(MapTile3D(nx, ny)) < height)
                Face(a, b, c, d, new Color(color.ToVector3() * shade));
        }
        Side(tile.X, tile.Y - 1, new(x, 0, z), new(x + 1, 0, z), new(x + 1, height, z), new(x, height, z), 0.68f);
        Side(tile.X, tile.Y + 1, new(x, 0, z + 1), new(x + 1, 0, z + 1), new(x + 1, height, z + 1), new(x, height, z + 1), 0.8f);
        Side(tile.X - 1, tile.Y, new(x, 0, z), new(x, 0, z + 1), new(x, height, z + 1), new(x, height, z), 0.6f);
        Side(tile.X + 1, tile.Y, new(x + 1, 0, z), new(x + 1, 0, z + 1), new(x + 1, height, z + 1), new(x + 1, height, z), 0.75f);
    }

    protected override void UnloadContent()
    {
        ReleaseTerrainChunks3D();
        _whaleFallSprite?.Dispose();
        foreach (var page in _atlasPages3D) page.Dispose();
        _terrainEffect3D?.Dispose();
        _cutoutEffect3D?.Dispose();
        _ghostEffect3D?.Dispose();
        base.UnloadContent();
    }
}
