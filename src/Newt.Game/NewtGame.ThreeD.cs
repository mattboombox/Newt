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
    // Tint submerged objects and let the bed color show through the water.
    private const int WaterSurfaceTintAlpha3D = 64;
    private const int WhaleFallCutout3D = -2;
    private const int WaterSurfaceArtCutout3D = -3;
    private const int SandwormCutout3D = -4;
    private const float SpriteScale3D = ThreeDGeometry.CritterSpriteScale;
    private const float StructureTileLift3D = 0.015f;
    private const float WalkingSpriteScale3D = 0.8f;
    private bool _dayNightCycleEnabled = true;
    private static readonly (Vector3 Direction, bool Cross)[] Stars3D = CreateStars3D();
    private const int TwilightGrid3D = 32;
    private readonly VertexPositionColor[] _twilightVertices3D = new VertexPositionColor[TwilightGrid3D * TwilightGrid3D * 6];
    private readonly Color[] _twilightColors3D = new Color[(TwilightGrid3D + 1) * (TwilightGrid3D + 1)];
    private static readonly Color DaySkyBlue3D = new(65, 138, 204);
    private const float StarVisibilityBrightness3D = 0.55f;
    // This palette is only the overhead sky. It deepens from day blue to black after sunset and
    // climbs back to day blue after sunrise, and it never reverses direction, so the dark side of
    // the sky darkens (and the stars fade in) exactly once per evening. All of the warm sunrise and
    // sunset color lives in TwilightColor3D, around the sun and along the horizon.
    // Phases match ThreeDGeometry.SunDirection: noon .14, sunset .40, dawn .88.
    private static readonly (float Phase, Color Color)[] SkyCycle3D =
    [
        (0f, DaySkyBlue3D),
        (0.30f, DaySkyBlue3D),
        (0.36f, new Color(56, 116, 186)),
        (0.40f, new Color(44, 78, 150)),  // Sunset: deep blue overhead.
        (0.45f, new Color(25, 38, 100)),
        (0.50f, new Color(10, 14, 52)),
        (0.56f, new Color(2, 3, 14)),
        (0.60f, Color.Black),
        (0.70f, Color.Black),
        (0.78f, new Color(8, 11, 44)),
        (0.84f, new Color(26, 36, 96)),
        (0.88f, new Color(52, 82, 150)),  // Sunrise: dim blue overhead.
        (0.93f, new Color(60, 112, 180)),
        (0.97f, DaySkyBlue3D),
        (1f, DaySkyBlue3D),
    ];

    private Color GetDayNightSkyColor3D(float phase)
    {
        if (!_dayNightCycleEnabled) return DaySkyBlue3D;
        // Use simulation ticks so the sky follows pause/speed and works without seasons.
        for (var i = 1; i < SkyCycle3D.Length; i++)
        {
            var (end, to) = SkyCycle3D[i];
            if (phase > end) continue;
            var (start, from) = SkyCycle3D[i - 1];
            var blend = MathHelper.SmoothStep(0, 1, (phase - start) / (end - start));
            return Color.Lerp(from, to, blend);
        }
        return DaySkyBlue3D;
    }

    private float SkyPhase3D()
    {
        const int cycleTicks = DayNightCycleSeconds3D * SimulationWorld.TicksPerSecond;
        var tickFraction = _accumulator / SimulationStep;
        return (float)((_world.Tick % cycleTicks + tickFraction) / cycleTicks);
    }

    private static (Vector3 Direction, bool Cross)[] CreateStars3D()
    {
        var random = new Random(7349);
        var stars = new (Vector3 Direction, bool Cross)[640];
        for (var i = 0; i < stars.Length; i++)
        {
            var y = 2 * random.NextSingle() - 1;
            var azimuth = MathHelper.TwoPi * random.NextSingle();
            var radius = MathF.Sqrt(1 - y * y);
            stars[i] = (new Vector3(radius * MathF.Cos(azimuth), y, radius * MathF.Sin(azimuth)),
                random.Next(5) == 0);
        }
        return stars;
    }

    private void DrawStars3D(Matrix viewProjection, Color sky, Vector3 sun, Vector3 up, float twilight, float dawn)
    {
        if (twilight == 0 && StarOpacity3D(sky) <= 0)
            return;

        var viewport = GraphicsDevice.Viewport;
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp, depthStencilState: DepthStencilState.None);
        foreach (var (direction, cross) in Stars3D)
        {
            var clip = Vector4.Transform(new Vector4(direction, 0), viewProjection);
            if (clip.W <= 0 || Math.Abs(clip.X) >= clip.W || Math.Abs(clip.Y) >= clip.W)
                continue;
            var height = Vector3.Dot(direction, up);
            var localSky = TwilightColor3D(sky, Vector3.Dot(direction, sun), height, twilight, dawn);
            var opacity = StarOpacity3D(localSky);
            if (opacity <= 0)
                continue;
            var color = Color.White * opacity;
            var x = (int)((clip.X / clip.W + 1) * viewport.Width * 0.5f);
            var y = (int)((1 - clip.Y / clip.W) * viewport.Height * 0.5f);
            if (cross)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y, 3, 1), color);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y - 1, 1, 1), color);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + 1, 1, 1), color);
            }
            else
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, 1), color);
        }
        _spriteBatch.End();
    }

    private (Color Sky, Vector3 Sun, float Dawn) SkyAtCamera3D(Vector3 eye)
    {
        var phase = _dayNightCycleEnabled ? SkyPhase3D() : 0.14f;
        if (IsPlanetoid3D && _dayNightCycleEnabled)
        {
            var angle = MathHelper.TwoPi * phase;
            var sun = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
            var localUp = Vector3.Normalize(eye);
            var sunsetward = new Vector3(MathF.Sin(angle), 0, -MathF.Cos(angle));
            var elevation = Vector3.Dot(localUp, sun);
            var dawn = GetDayNightSkyColor3D(ThreeDGeometry.PlanetoidSkyPhase(elevation, afternoon: false));
            var dusk = GetDayNightSkyColor3D(ThreeDGeometry.PlanetoidSkyPhase(elevation, afternoon: true));
            var blend = MathHelper.SmoothStep(0, 1, 0.5f + 2.5f * Vector3.Dot(localUp, sunsetward));
            return (Color.Lerp(dawn, dusk, blend), sun, 1 - blend);
        }

        var localSun = ThreeDGeometry.SunDirection(phase);
        // Cross the ring's width from north to south rather than following its circumference.
        var (east, up, south) = SurfaceFrame3D(_focus3D.X, _focus3D.Y);
        var sunriseDirection = IsRing3D ? -south : east;
        var dawnTint = phase >= 0.72f ? 1f : MathHelper.SmoothStep(1, 0, phase / 0.05f);
        return (GetDayNightSkyColor3D(phase), sunriseDirection * localSun.X + up * localSun.Y, dawnTint);
    }

    private static float StarOpacity3D(Color sky)
    {
        var brightness = Math.Max(sky.R, Math.Max(sky.G, sky.B)) / 255f;
        return 1 - MathHelper.SmoothStep(0, 1, brightness / StarVisibilityBrightness3D);
    }

    private float TwilightStrength3D(float sunHeight, float altitudeFade)
    {
        if (!_dayNightCycleEnabled || altitudeFade >= 1)
            return 0;
        return (1 - altitudeFade) * (sunHeight < 0
            ? MathHelper.SmoothStep(0, 1, 1 + sunHeight / 0.42f)
            : MathHelper.SmoothStep(0, 1, 1 - sunHeight / 0.55f));
    }

    // Each ramp runs from the cool upper twilight to the hot band beside the sun.
    private static readonly Vector3[] SunsetRamp3D =
    [
        new(0.30f, 0.22f, 0.52f), // Violet.
        new(0.80f, 0.33f, 0.50f), // Rose.
        new(1.00f, 0.52f, 0.24f), // Orange.
        new(1.00f, 0.78f, 0.42f), // Gold.
    ];
    private static readonly Vector3[] SunriseRamp3D =
    [
        new(0.40f, 0.34f, 0.66f), // Lavender.
        new(0.94f, 0.50f, 0.60f), // Pink.
        new(1.00f, 0.62f, 0.44f), // Peach.
        new(1.00f, 0.84f, 0.58f), // Pale gold.
    ];
    private static readonly Vector3 SunsetCore3D = new(1.00f, 0.93f, 0.74f);
    private static readonly Vector3 SunriseCore3D = new(1.00f, 0.92f, 0.84f);
    private static readonly Vector3 BeltOfVenus3D = new(0.86f, 0.52f, 0.62f);
    private static readonly Vector3 EarthShadowTint3D = new(0.62f, 0.66f, 0.90f);

    private static Vector3 SampleRamp3D(Vector3[] ramp, float position)
    {
        var scaled = Math.Clamp(position, 0f, 1f) * (ramp.Length - 1);
        var index = Math.Min((int)scaled, ramp.Length - 2);
        return Vector3.Lerp(ramp[index], ramp[index + 1], scaled - index);
    }

    // alignment: dot(ray, sun). height: dot(ray, up), i.e. the sine of the ray's elevation.
    private static Color TwilightColor3D(Color sky, float alignment, float height, float twilight, float dawn)
    {
        if (twilight <= 0) return sky;
        var strength = Math.Clamp(twilight, 0, 1);
        dawn = Math.Clamp(dawn, 0, 1);

        // 0 on the far side of the sky, 1 looking straight at the sun.
        var toward = MathHelper.SmoothStep(0, 1, Math.Clamp((alignment + 0.2f) / 1.2f, 0, 1));
        // 1 along the horizon, fading out by roughly 40 degrees up.
        var lowness = 1 - MathHelper.SmoothStep(0, 1, Math.Clamp(height / 0.7f, 0, 1));
        // A high sun keeps its warmth in a tight patch; a sinking sun spreads it across the sky.
        var spread = MathF.Pow(toward, MathHelper.Lerp(2.8f, 1f, strength));
        var heat = spread * (0.35f + 0.65f * lowness);

        // Sunward and low is gold or peach, rising through orange and rose to violet. The wash
        // is a blend over the sky, never a fixed dark color, so it cannot undercut the palette.
        var ramp = Vector3.Lerp(SampleRamp3D(SunsetRamp3D, heat), SampleRamp3D(SunriseRamp3D, heat), dawn);
        var wash = strength * (0.12f + 0.88f * MathF.Pow(heat, 0.85f));
        var color = Vector3.Lerp(sky.ToVector3(), ramp, Math.Clamp(wash, 0, 1));

        // A soft halo and a bright core hugging the sun.
        var sunward = MathF.Max(alignment, 0);
        var halo = 0.35f * MathF.Pow(sunward, 6) + 0.65f * MathF.Pow(sunward, 48);
        color = Vector3.Lerp(color, Vector3.Lerp(SunsetCore3D, SunriseCore3D, dawn),
            Math.Clamp(halo * strength, 0, 1));

        // Opposite the sun: a pink "belt of Venus" riding above a bluish Earth-shadow band.
        var away = MathHelper.SmoothStep(0, 1, Math.Clamp((0.25f - alignment) / 0.8f, 0, 1));
        var deep = strength * strength * away;
        if (deep > 0)
        {
            var shadow = 1 - MathHelper.SmoothStep(0, 1, Math.Clamp(height / 0.1f, 0, 1));
            color = Vector3.Lerp(color, color * EarthShadowTint3D, 0.55f * deep * shadow);
            var belt = MathF.Exp(-MathF.Pow((height - 0.14f) / 0.11f, 2));
            color = Vector3.Lerp(color, BeltOfVenus3D, 0.4f * deep * belt);
        }
        return new Color(color);
    }

    private static Color SunColor3D(float sunHeight, float altitudeFade)
    {
        // Fade out the horizon tint by the altitude where stars are visible even at noon.
        var noonBrightness = Color.Lerp(DaySkyBlue3D, Color.Black, altitudeFade).B / 255f;
        var atmosphere = MathHelper.SmoothStep(0, 1, (noonBrightness - StarVisibilityBrightness3D) /
            (DaySkyBlue3D.B / 255f - StarVisibilityBrightness3D));
        var horizon = 1 - MathHelper.SmoothStep(0, 1, sunHeight / 0.35f);
        return Color.Lerp(Color.White, new Color(255, 190, 120), 0.9f * horizon * atmosphere);
    }

    private void DrawTwilight3D(Matrix view, Matrix projection, Color sky, Vector3 sun, Vector3 up,
        float twilight, float dawn)
    {
        if (twilight == 0)
            return;

        var camera = Matrix.Invert(view);
        const int corners = TwilightGrid3D + 1;
        // Shade each grid corner once; neighboring cells share it.
        for (var row = 0; row < corners; row++)
            for (var column = 0; column < corners; column++)
            {
                var x = 2f * column / TwilightGrid3D - 1;
                var y = 2f * row / TwilightGrid3D - 1;
                var ray = Vector3.Normalize(Vector3.TransformNormal(
                    new Vector3(x / projection.M11, y / projection.M22, -1), camera));
                _twilightColors3D[row * corners + column] = TwilightColor3D(
                    sky, Vector3.Dot(ray, sun), Vector3.Dot(ray, up), twilight, dawn);
            }

        VertexPositionColor Vertex(int column, int row) => new(
            new Vector3(2f * column / TwilightGrid3D - 1, 2f * row / TwilightGrid3D - 1, 0.5f),
            _twilightColors3D[row * corners + column]);

        for (var row = 0; row < TwilightGrid3D; row++)
            for (var column = 0; column < TwilightGrid3D; column++)
            {
                var index = (row * TwilightGrid3D + column) * 6;
                var bottomLeft = Vertex(column, row);
                var bottomRight = Vertex(column + 1, row);
                var topLeft = Vertex(column, row + 1);
                var topRight = Vertex(column + 1, row + 1);
                _twilightVertices3D[index] = bottomLeft;
                _twilightVertices3D[index + 1] = bottomRight;
                _twilightVertices3D[index + 2] = topLeft;
                _twilightVertices3D[index + 3] = topLeft;
                _twilightVertices3D[index + 4] = bottomRight;
                _twilightVertices3D[index + 5] = topRight;
            }

        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.None;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _terrainEffect3D!.View = Matrix.Identity;
        _terrainEffect3D.Projection = Matrix.Identity;
        foreach (var pass in _terrainEffect3D.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _twilightVertices3D, 0, _twilightVertices3D.Length / 3);
        }
    }

    private void DrawSun3D(Matrix viewProjection, Vector3 direction, Color? tint = null)
    {
        // A direction (w = 0) keeps the sun infinitely distant as the camera moves.
        var clip = Vector4.Transform(new Vector4(direction, 0), viewProjection);
        if (clip.W <= 0) return;
        var viewport = GraphicsDevice.Viewport;
        var x = (clip.X / clip.W + 1) * viewport.Width * 0.5f;
        var y = (1 - clip.Y / clip.W) * viewport.Height * 0.5f;
        var pixelSize = Math.Max(2, viewport.Height / 300);
        const int radius = 7, haloRadius = 15;
        var extent = (haloRadius + 1) * pixelSize;
        if (x < -extent || x > viewport.Width + extent || y < -extent || y > viewport.Height + extent) return;
        var color = tint ?? Color.White;
        _spriteBatch!.Begin(samplerState: SamplerState.PointClamp, depthStencilState: DepthStencilState.None);
        // Stepped, pixel-art glow rings behind the disc.
        DrawDisc(haloRadius, color * 0.07f);
        DrawDisc(11, color * 0.12f);
        DrawDisc(9, color * 0.22f);
        DrawDisc(radius, color);
        _spriteBatch.End();

        void DrawDisc(int discRadius, Color fill)
        {
            for (var row = -discRadius; row <= discRadius; row++)
            {
                var halfWidth = (int)MathF.Floor(MathF.Sqrt(discRadius * discRadius - row * row));
                _spriteBatch!.Draw(_pixel, new Rectangle(
                    (int)x - halfWidth * pixelSize - pixelSize / 2,
                    (int)y + row * pixelSize - pixelSize / 2,
                    (halfWidth * 2 + 1) * pixelSize, pixelSize), fill);
            }
        }
    }
    private Vector2 _focus3D;
    private float _yaw3D = MathHelper.PiOver4, _pitch3D = 0.85f, _distance3D = 28;
    private BasicEffect? _terrainEffect3D;
    private Effect? _planetoidDaylightEffect3D;
    private const int DayNightCycleSeconds3D = 240;
    // Minimum night brightness and the solar elevations where the transition starts and ends.
    private static readonly Vector3 PlanetoidDaylightRamp3D = new(0.24f, -0.10f, 0.22f);
    private bool UsePlanetoidDaylight3D => IsPlanetoid3D && _dayNightCycleEnabled;
    private Effect TerrainSceneEffect3D => UsePlanetoidDaylight3D ? _planetoidDaylightEffect3D! : _terrainEffect3D!;
    private AlphaTestEffect? _cutoutEffect3D;

    private static void SetTerrainWorld3D(Effect effect, Matrix world)
    {
        if (effect is BasicEffect basic)
            basic.World = world;
        else
            effect.Parameters["World"].SetValue(world);
    }

    private float PlanetoidTileBrightness3D(GridPosition tile, Vector3 sun)
    {
        var size = PlanetoidLayout.FaceSize(_world.Width);
        var face = PlanetoidLayout.FaceAt(tile.X, tile.Y, size);
        var direction = Framework(PlanetoidLayout.Direction(tile.X + 0.5f, tile.Y + 0.5f, size, face));
        var ramp = PlanetoidDaylightRamp3D;
        return MathHelper.SmoothStep(ramp.X, 1, (Vector3.Dot(direction, sun) - ramp.Y) / (ramp.Z - ramp.Y));
    }
    private readonly List<RenderTarget2D> _atlasPages3D = [];
    private readonly List<VertexPositionColor> _terrainVertices3D = [];
    private readonly List<VertexPositionColor> _lavaVertices3D = [];
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
        var speed = keyboard.IsKeyDown(Keys.LeftShift) ? 2.5f : 1;
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

    private void DrawWorld3D(double elapsedSeconds)
    {
        WrapCamera3D();
        _terrainEffect3D ??= new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        _cutoutEffect3D ??= new AlphaTestEffect(GraphicsDevice)
        { VertexColorEnabled = true, AlphaFunction = CompareFunction.Greater, ReferenceAlpha = 100 };
        _terrainVertices3D.Clear();
        _lavaVertices3D.Clear();
        _cutouts3D.Clear();
        var (view, projection) = Matrices3D();
        var frustum = new BoundingFrustum(view * projection);
        var bounds = VisibleBounds3D(frustum);
        var hovered = ScreenToWorld(Mouse.GetState().X, Mouse.GetState().Y);
        PrepareTerrainChunks3D(frustum, bounds);
        foreach (var chunk in _visibleChunks3D)
            CollectTerrainDetails3D(chunk.Tiles, frustum, hovered, elapsedSeconds);
        for (var i = 0; i < _world.CritterCount; i++)
        {
            var critter = _world.GetCritter(i);
            if (critter.Species is CritterSpecies.Sandworm) continue;
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
        var eye = Matrix.Invert(view).Translation;
        var (dayNightSky, sunDirection, dawn) = SkyAtCamera3D(eye);
        var skyUp = IsPlanetoid3D ? Vector3.Normalize(eye) : SurfaceUp3D(_focus3D.X, _focus3D.Y);
        var sunHeight = Vector3.Dot(sunDirection, skyUp);
        // Use solar elevation so the altitude fade to black does not darken daytime terrain.
        var terrainBrightness = _dayNightCycleEnabled && !UsePlanetoidDaylight3D
            ? MathHelper.SmoothStep(0.34f, 1f, (sunHeight + 0.28f) / 0.50f)
            : 1f;
        var terrainTint = new Vector3(terrainBrightness);
        var spriteTint = new Color(terrainTint);
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
                    if (item.Critter >= 0)
                        DrawCritter(_world.GetCritter(item.Critter), sx, sy, UsePlanetoidDaylight3D
                            ? PlanetoidTileBrightness3D(item.Tile, sunDirection) : terrainBrightness);
                    else if (item.Critter == WhaleFallCutout3D) DrawWhaleFall(sx, sy, item.Tile);
                    else if (item.Critter == WaterSurfaceArtCutout3D)
                        DrawWaterSurfaceArt(sx, sy, item.Tile, elapsedSeconds);
                    else if (item.Critter == SandwormCutout3D)
                        DrawSandwormSegment(sx, sy, item.Tile);
                    else if (item.Ground)
                    {
                        var water = _world.GetSurfaceWater(item.Tile);
                        if (water is not (SurfaceWaterKind.None or SurfaceWaterKind.FreshwaterLake))
                            DrawSurfaceWater(sx, sy, item.Tile, water, elapsedSeconds);
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
        var localEye = LocalPoint3D(eye);
        var eyeHeight = _playerView is PlayerView.FirstPerson
            ? _firstPersonEyeHeight : localEye.Y;
        var outsideRing = IsRing3D && _playerView is PlayerView.FreeCamera &&
            (localEye.Z < 0 || localEye.Z > _world.Height || localEye.Y < 0);
        var skyBlend = outsideRing ? 1f : ThreeDGeometry.SkyBlackBlend(eyeHeight, _world.Body);
        var sky = Color.Lerp(dayNightSky, Color.Black, skyBlend);
        GraphicsDevice.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, sky, 1f, 0);
        var twilight = TwilightStrength3D(sunHeight, skyBlend);
        _terrainEffect3D.DiffuseColor = Vector3.One;
        DrawTwilight3D(view, projection, sky, sunDirection, skyUp, twilight, dawn);
        DrawStars3D(view * projection, sky, sunDirection, skyUp, twilight, dawn);
        DrawSun3D(view * projection, sunDirection, SunColor3D(sunHeight, skyBlend));
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _terrainEffect3D.View = view;
        _terrainEffect3D.Projection = projection;
        _terrainEffect3D.DiffuseColor = terrainTint;
        if (UsePlanetoidDaylight3D)
        {
            _planetoidDaylightEffect3D ??= Content.Load<Effect>("PlanetoidDaylight");
            var parameters = _planetoidDaylightEffect3D.Parameters;
            parameters["ViewProjection"].SetValue(view * projection);
            parameters["SunDirection"].SetValue(sunDirection);
            parameters["DaylightRamp"].SetValue(PlanetoidDaylightRamp3D);
            parameters["CloudLighting"].SetValue(0f);
        }
        DrawTerrainChunks3D(static chunk => chunk.Terrain);
        DrawUnbufferedSurface3D(_terrainVertices3D, TerrainSceneEffect3D);
        // Lava emits light even on the night side. Draw it at bed height before water.
        _terrainEffect3D.DiffuseColor = Vector3.One;
        DrawUnbufferedSurface3D(_lavaVertices3D, _terrainEffect3D);
        _terrainEffect3D.DiffuseColor = terrainTint;
        _cutoutEffect3D.View = view;
        _cutoutEffect3D.Projection = projection;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        var cameraWorld = Matrix.Invert(view);
        // Share the camera's orientation across all sprites. Per-sprite look-at
        // directions cause radial rotation and foreshortening in overhead views.
        var right = cameraWorld.Right * 0.5f;
        var up = cameraWorld.Up * 0.5f;
        var surfaceArt = new List<(int Page, List<VertexPositionColorTexture> Quads)>();
        var translucentCritters = new List<(int Page, List<VertexPositionColorTexture> Quads)>();
        for (var page = 0; page < pages; page++)
        {
            var quads = new List<VertexPositionColorTexture>();
            List<VertexPositionColorTexture>? surfaceQuads = null;
            List<VertexPositionColorTexture>? translucentQuads = null;
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
                var critter = item.Critter >= 0 ? _world.GetCritter(item.Critter) : (CritterSnapshot?)null;
                if (critter is { } offsetCritter)
                {
                    var offset = ThreeDGeometry.CritterOffset(offsetCritter.Id);
                    p += new Vector3(offset.X, 0, offset.Z);
                }
                var structure = critter is null && !item.Ground ? _world.GetApeStructure(item.Tile) : null;
                var farm = structure is ApeStructureKind.Farm or ApeStructureKind.BlightedFarm or
                    ApeStructureKind.RicePaddy or ApeStructureKind.Aquaculture;
                // Screen-aligned sprites stay flat and upright even directly overhead.
                var spriteScale = (!item.Ground && !farm) || item.Critter == WhaleFallCutout3D ? SpriteScale3D : 1f;
                var scale = spriteScale * (IsWalkingCamera ? WalkingSpriteScale3D : 1f);
                var underWaterCover = critter is { } occupant && ShouldFlattenUnderWaterCover3D(occupant);
                var spriteRight = right * scale;
                var spriteUp = up * scale;
                var center = WorldPoint3D(p) + SurfaceUp3D(p.X, p.Z) * (0.5f * scale);
                if (critter is null && !item.Ground && !farm)
                {
                    // Centered building art has padding below the visible base in its atlas cell.
                    // Anchor that base, rather than the quad's bottom edge, to the tile.
                    var visibleHeight = _world.IsCustodianFabricator(item.Tile) ||
                        _world.GetWolfDenCharges(item.Tile) is not null ? 1f
                        : structure is not null || _world.HasVampireLair(item.Tile) || _world.HasTeleporter(item.Tile)
                            ? 0.75f : 0f;
                    if (visibleHeight > 0f)
                    {
                        var basePosition = p with { Y = p.Y - StructureTileLift3D };
                        center = WorldPoint3D(basePosition) + spriteUp * visibleHeight;
                    }
                }
                var tint = item.Critter >= 0 ? Color.White : UsePlanetoidDaylight3D
                    ? new Color(new Vector3(PlanetoidTileBrightness3D(item.Tile, sunDirection))) : spriteTint;
                var a = new VertexPositionColorTexture(center - spriteRight + spriteUp, tint, new Vector2(u, v));
                var b = new VertexPositionColorTexture(center + spriteRight + spriteUp, tint, new Vector2(u + step, v));
                var c = new VertexPositionColorTexture(center + spriteRight - spriteUp, tint, new Vector2(u + step, v + step));
                var d = new VertexPositionColorTexture(center - spriteRight - spriteUp, tint, new Vector2(u, v + step));
                // Aquatic critters lie beneath bridge decks and low ice sheets.
                var flat = underWaterCover || item.Ground || (critter is { } spriteCritter
                    ? spriteCritter.Species is
                        CritterSpecies.Plankton or CritterSpecies.Trilobite or CritterSpecies.SeaScorpion
                    : farm ||
                        _world.GetVolcanoState(item.Tile) is { } volcanoState && volcanoState != VolcanoState.Extinct);
                if (flat)
                {
                    var halfSize = spriteScale * 0.5f;
                    if (IsWalkingCamera && item.Critter >= 0)
                        halfSize *= WalkingSpriteScale3D;
                    a.Position = WorldPoint3D(p + new Vector3(-halfSize, 0, -halfSize), item.Tile);
                    b.Position = WorldPoint3D(p + new Vector3(halfSize, 0, -halfSize), item.Tile);
                    c.Position = WorldPoint3D(p + new Vector3(halfSize, 0, halfSize), item.Tile);
                    d.Position = WorldPoint3D(p + new Vector3(-halfSize, 0, halfSize), item.Tile);
                }
                var target = item.Critter == WaterSurfaceArtCutout3D ? surfaceQuads ??= []
                    : critter is { Species: CritterSpecies.Jellyfish, IsDamageFlashing: false }
                        ? translucentQuads ??= [] : quads;
                target.AddRange([a, b, c, a, c, d]);
            }
            if (surfaceQuads is not null)
                surfaceArt.Add((page, surfaceQuads));
            if (translucentQuads is not null)
                translucentCritters.Add((page, translucentQuads));
            DrawCutoutPage(page, quads);
        }
        DrawPlayerGhost3D(view, projection);
        DrawBlendedCutouts(translucentCritters);
        DrawWaterSurfaces3D();
        DrawBlendedCutouts(surfaceArt);
        DrawClouds3D(elapsedSeconds, frustum, bounds, terrainBrightness);
        GraphicsDevice.Viewport = originalViewport;

        void DrawBlendedCutouts(List<(int Page, List<VertexPositionColorTexture> Quads)> cutouts)
        {
            if (cutouts.Count == 0) return;
            GraphicsDevice.BlendState = BlendState.AlphaBlend;
            GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _cutoutEffect3D.ReferenceAlpha = 0;
            foreach (var (page, quads) in cutouts)
                DrawCutoutPage(page, quads);
            _cutoutEffect3D.ReferenceAlpha = 100;
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        void DrawCutoutPage(int page, List<VertexPositionColorTexture> quads)
        {
            if (quads.Count == 0) return;
            _cutoutEffect3D.Texture = _atlasPages3D[page];
            var data = quads.ToArray();
            foreach (var pass in _cutoutEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3);
            }
        }
    }

    private Color TerrainColumnColor3D(GridPosition tile) =>
        GetBedColor(tile, UnfrozenBedTerrain(tile), _world.GetBiome(tile), _world.GetTemperatureBand(tile));

    private void CollectTerrainDetails3D(
        Rectangle bounds, BoundingFrustum frustum, GridPosition? hovered, double elapsedSeconds)
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
                var hasWaterSurfaceArt = !river &&
                    (GetRiverMouths(tile) != RiverConnection.None || BeachwardDirection(tile) != Vector2.Zero);
                var hasSandworm = _world.TryGetSandwormSegment(tile, out _);
                if (!drawColumn && hovered != tile && !whaleFall && !hasGroundArt && !hasStructure &&
                    !hasWaterSurfaceArt && !hasSandworm)
                    continue;

                var height = TileHeight3D(tile);
                var waterElevation = WaterElevation3D(tile);
                var top = Math.Max(height, ThreeDGeometry.ColumnHeight(waterElevation ?? _world.GetElevation(tile)));
                if (!frustum.Intersects(TileBounds3D(x, y, top + 2))) continue;
                if (drawColumn || hovered == tile)
                {
                    var color = TerrainColumnColor3D(tile);
                    if (!drawColumn && hovered == tile) color = Color.Lerp(color, Color.White, 0.45f);
                    var owner = new GridPosition(x, y);
                    AddColumn3D(owner, drawColumn ? height : height + 0.003f, color,
                        topOnly: !drawColumn);
                    if (drawColumn)
                    {
                        var lava = GetLavaColor(tile);
                        if (hovered == tile) lava = Color.Lerp(lava, Color.White, 0.45f);
                        AddColumn3D(owner, height + 0.003f, lava, topOnly: true, vertices: _lavaVertices3D);
                        AddWaterTile3D(tile, x, y, _dynamicWater3D, _dynamicIce3D);
                    }
                }

                // Carcasses stay on the bed, including beneath ice and bridge decks.
                if (whaleFall)
                    _cutouts3D.Add((new Vector3(x + 0.5f, height + 0.012f, y + 0.5f), tile, WhaleFallCutout3D, true));
                if (hasWaterSurfaceArt)
                    _cutouts3D.Add((new Vector3(x + 0.5f, top + 0.012f, y + 0.5f), tile, WaterSurfaceArtCutout3D, true));
                if (hasSandworm)
                    _cutouts3D.Add((new Vector3(x + 0.5f, top + 0.03f, y + 0.5f), tile, SandwormCutout3D, true));
                if (river)
                    AddConnectionSides3D(x, y, height + 0.01f, _world.GetRiverConnections(tile),
                        GetSurfaceWaterColor(water), outflows: _world.GetRiverOutflows(tile), elapsedSeconds: elapsedSeconds);
                if (roads != RiverConnection.None)
                    AddConnectionSides3D(x, y,
                        ThreeDGeometry.RoadHeight(height, waterElevation, _world.HasRoadBridge(tile)),
                        roads, GetRoadColor(tile), road: true);
                if (hasGroundArt)
                    _cutouts3D.Add((new Vector3(x + 0.5f,
                        ThreeDGeometry.RoadHeight(height, waterElevation, _world.HasRoadBridge(tile)),
                        y + 0.5f), tile, -1, true));
                if (hasStructure)
                    _cutouts3D.Add((new Vector3(x + 0.5f,
                        (IsIce3D(tile) ? top + 0.035f : height) + StructureTileLift3D, y + 0.5f),
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
        DrawUnbufferedSurface3D(vertices, TerrainSceneEffect3D);
    }

    private void DrawUnbufferedSurface3D(List<VertexPositionColor> vertices, Effect effect)
    {
        if (vertices.Count == 0) return;
        var data = vertices.ToArray();
        foreach (var pass in effect.CurrentTechnique.Passes)
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
        var color = ice ? new Color(GetTerrainColor(Terrain.Ice, _world.GetTemperatureBand(tile)), 210)
            : new Color(WaterSurfaceColor(tile), WaterSurfaceTintAlpha3D);
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
        bool road = false, RiverConnection outflows = RiverConnection.None, double elapsedSeconds = 0)
    {
        var offset = road ? 0.003f : 0.002f;
        var owner = new GridPosition(x, y);
        foreach (var segment in ThreeDGeometry.ConnectionSideSegments(connections))
        {
            if (Neighbor(segment.TargetX, segment.TargetY) is not { } target ||
                Neighbor(segment.FaceX, segment.FaceY) is not { } adjacent) continue;
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
            if ((outflows & segment.Connection) != 0 && topHeight - bottom >= 0.14f)
            {
                var normal = new Vector3(segment.FaceX, 0, segment.FaceY);
                AddWaterfallSpray3D(owner, a, b, normal, topHeight - bottom, color, elapsedSeconds);
            }
            a = WorldPoint3D(a, owner);
            b = WorldPoint3D(b, owner);
            c = WorldPoint3D(c, owner);
            d = WorldPoint3D(d, owner);
            _terrainVertices3D.AddRange([new(a, color), new(b, color), new(c, color),
                new(a, color), new(c, color), new(d, color)]);
        }

        GridPosition? Neighbor(int dx, int dy) => IsPlanetoid3D
            ? PlanetoidLayout.Offset(owner, dx, dy, PlanetoidLayout.FaceSize(_world.Width))
            : ContainsTile3D(x + dx, y + dy) ? MapTile3D(x + dx, y + dy) : null;
    }

    private void AddWaterfallSpray3D(GridPosition tile, Vector3 left, Vector3 right, Vector3 outward,
        float dropHeight, Color water, double elapsedSeconds)
    {
        var across = Vector3.Normalize(right - left);
        var phase = (tile.X * 0.37 + tile.Y * 0.23) % 1;
        var mist = new Color(215, 240, 251);
        for (var drop = 0; drop < 6; drop++)
        {
            var age = (float)((elapsedSeconds * 0.92 + phase + drop / 6.0) % 1);
            var center = Vector3.Lerp(left, right, 0.19f + drop % 3 * 0.31f) + outward * 0.008f;
            var falling = age < 0.73f;
            if (falling)
            {
                var fall = age / 0.73f;
                center.Y += dropHeight * (1f - fall * fall);
            }
            else
            {
                // Droplets rebound at the foot of the falls and spread away from the face.
                var bounce = (age - 0.73f) / 0.27f;
                center += outward * (bounce * 0.22f) + across * ((drop % 3 - 1) * bounce * 0.09f);
                center.Y += Math.Min(dropHeight * 0.5f, 0.22f) * (4f * bounce * (1f - bounce));
            }

            var bright = falling ? 0.8f : (1f - age) / 0.27f * 0.8f;
            var halfWidth = falling ? 0.024f : 0.034f;
            var halfHeight = falling ? Math.Min(0.065f, dropHeight * 0.12f) : 0.034f;
            var lower = center - Vector3.Up * halfHeight;
            var upper = center + Vector3.Up * halfHeight;
            if (falling)
            {
                lower.Y = Math.Max(lower.Y, left.Y);
                upper.Y = Math.Min(upper.Y, left.Y + dropHeight);
            }
            var shade = Color.Lerp(water, mist, bright);
            var a = WorldPoint3D(lower - across * halfWidth, tile);
            var b = WorldPoint3D(lower + across * halfWidth, tile);
            var c = WorldPoint3D(upper + across * halfWidth, tile);
            var d = WorldPoint3D(upper - across * halfWidth, tile);
            _terrainVertices3D.AddRange([new(a, shade), new(b, shade), new(c, shade),
                new(a, shade), new(c, shade), new(d, shade)]);
        }
    }

    private void AddColumn3D(GridPosition tile, float height, Color color, bool topOnly = false,
        List<VertexPositionColor>? vertices = null)
    {
        vertices ??= _terrainVertices3D;
        float x = tile.X, z = tile.Y;
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color shade)
        {
            a = WorldPoint3D(a, tile);
            b = WorldPoint3D(b, tile);
            c = WorldPoint3D(c, tile);
            d = WorldPoint3D(d, tile);
            vertices.AddRange([new(a, shade), new(b, shade), new(c, shade),
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
        _jellyfishGlowSprite?.Dispose();
        foreach (var page in _atlasPages3D) page.Dispose();
        _terrainEffect3D?.Dispose();
        _cutoutEffect3D?.Dispose();
        _ghostEffect3D?.Dispose();
        ReleaseCloudPatches3D();
        _cloudDepthStencil3D?.Dispose();
        base.UnloadContent();
    }
}
