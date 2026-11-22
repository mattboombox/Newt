using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private sealed record RifleShot(GridPosition Tile, bool ThreeD, Vector3 Origin, Vector3 Target,
        Vector2 MapOrigin, Vector2 MapTarget, RifleShotTiming Timing)
    {
        public GuidedMissileFlight? Missile { get; init; }
        public bool FollowCursor { get; init; }
        public float? MeteorMagnitude { get; init; }
        public Color? BeamColor { get; init; }
        public GridPosition ImpactTile { get; set; } = Tile;
    }
    private readonly List<RifleShot> _rifleShots = [];
    private readonly VertexPositionColor[] _rifleLineVertices = new VertexPositionColor[2];
    private readonly VertexPositionColor[] _rifleGlowVertices = new VertexPositionColor[6];
    private SoundEffect? _rifleSound;
    private readonly List<SoundEffect> _missileSounds = [];
    private double _missileCooldownRemaining;
    private double _rocketPodCooldownRemaining;
    private int _missileLaunchSequence;
    private bool _guidedMissileFollowsCursor = true;
    private readonly VertexPositionColor[] _missileTrailVertices = new VertexPositionColor[GuidedMissileFlight.MaximumTrailPoints + 1];
    private BasicEffect? _projectileEffect3D;
    private DepthStencilState? _rifleTerrainMask3D;
    private DepthStencilState? _rifleCloudOverlay3D;
    private BlendState? _rifleDepthOnlyBlend3D;
    private const double RifleCooldownSeconds = 1;
    private double _rifleCooldownRemaining;
    private bool HasVisibleImpactWave => _world.ActiveImpactWaveCount > 0 ||
        _rifleShots.Any(shot => shot.Timing.ShockwaveVisible);

    private bool TryGetVisibleImpactWave(GridPosition tile, out ImpactWaveSnapshot wave, bool impactOnly = false)
    {
        foreach (var shot in _rifleShots)
        {
            if (!shot.Timing.ShockwaveVisible) continue;
            wave = new(shot.ImpactTile, shot.Timing.ShockwaveProgress, 1, 0, WaveKind.Impact);
            if (Impacts.IsOnShockFront(_world, tile, wave)) return true;
        }
        for (var i = 0; i < _world.ActiveImpactWaveCount; i++)
        {
            wave = _world.GetImpactWave(i);
            if (impactOnly && wave.Kind is not WaveKind.Impact) continue;
            var center = wave.Center;
            if (wave.Kind is WaveKind.Impact && wave.MaximumRadius <= 1 &&
                _rifleShots.Any(shot => shot.Timing.Impacted && shot.ImpactTile == center)) continue;
            if (Impacts.IsOnShockFront(_world, tile, wave)) return true;
        }
        wave = default;
        return false;
    }

    private void LoadRifleSound()
    {
        try
        {
            using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Content", "Sounds", "anti-materiel-rifle.wav"));
            _rifleSound = SoundEffect.FromStream(stream);
            foreach (var name in new[] { "missile-launch.wav", "missile-launch2.wav" })
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Content", "Sounds", name);
                if (!File.Exists(path)) continue;
                using var missileStream = File.OpenRead(path);
                _missileSounds.Add(SoundEffect.FromStream(missileStream));
            }
        }
        catch (NoAudioHardwareException) { }
    }

    private void FireAntiMaterielRifle(GridPosition tile)
    {
        if (_paused || _rifleCooldownRemaining > 0) return;
        _rifleCooldownRemaining = RifleCooldownSeconds;
        var height = Math.Max(TileHeight3D(tile),
            ThreeDGeometry.ColumnHeight(WaterElevation3D(tile) ?? _world.GetElevation(tile))) + 0.035f;
        var target = WorldPoint3D(new(tile.X + 0.5f, height, tile.Y + 0.5f), tile);
        var origin = target;
        var mapLocation = MapLocation2D(tile);
        var mapTarget = new Vector2(mapLocation.X + 0.5f, mapLocation.Y + 0.5f);
        var mapOrigin = new Vector2(_cameraX + (GraphicsDevice.Viewport.Width / 2f - MapOffsetX) / TileSize,
            _cameraY + (MapViewportHeight / 2f - MapOffsetY) / TileSize);
        if (WrapsMapX)
            mapTarget.X += MathF.Round((mapOrigin.X - mapTarget.X) / MapWidth2D) * MapWidth2D;
        if (_view3D)
        {
            var camera = Matrix.Invert(Matrices3D().View);
            // A slight muzzle offset makes the camera-fired tracer visible even at the crosshair.
            origin = _playerView is PlayerView.ThirdPerson
                ? PlayerGhostCenter3D()
                : camera.Translation + camera.Forward * 0.12f + camera.Right * 0.075f - camera.Up * 0.06f;
        }
        var distance = _view3D ? Vector3.Distance(origin, target) : Vector2.Distance(mapOrigin, mapTarget);
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget, new(distance)));
        try { _rifleSound?.Play(0.8f, 0, 0); }
        catch (NoAudioHardwareException) { }
    }

    private Vector3 ProjectileTarget3D(GridPosition tile)
    {
        var height = Math.Max(TileHeight3D(tile),
            ThreeDGeometry.ColumnHeight(WaterElevation3D(tile) ?? _world.GetElevation(tile))) + 0.035f;
        return WorldPoint3D(new(tile.X + 0.5f, height, tile.Y + 0.5f), tile);
    }

    private void FireMissile(GridPosition tile, bool rocketPod = false)
    {
        if (_paused) return;
        if (rocketPod)
        {
            if (_rocketPodCooldownRemaining > 0.0001) return;
            _rocketPodCooldownRemaining = GuidedMissileFlight.RocketPodFireIntervalSeconds;
        }
        else
        {
            if (_missileCooldownRemaining > 0.0001) return;
            _missileCooldownRemaining = GuidedMissileFlight.FireIntervalSeconds;
        }
        var mapOrigin = new Vector2(_cameraX + (GraphicsDevice.Viewport.Width / 2f - MapOffsetX) / TileSize,
            _cameraY + (MapViewportHeight / 2f - MapOffsetY) / TileSize);
        var map = MapLocation2D(tile);
        var mapTarget = new Vector2(map.X + 0.5f, map.Y + 0.5f);
        if (WrapsMapX) mapTarget.X += MathF.Round((mapOrigin.X - mapTarget.X) / MapWidth2D) * MapWidth2D;
        var side = _missileLaunchSequence++ % 2 == 0 ? 1f : -1f;
        // Cosmetic variation separates repeated trails without changing the simulation RNG.
        var launchOffset = side * (0.25f + Random.Shared.NextSingle() * 0.10f);
        var outward = side * (0.7f + Random.Shared.NextSingle() * 0.2f);
        var rise = 1.2f + Random.Shared.NextSingle() * 0.2f;
        var launchTile = MapTile2D((int)MathF.Floor(mapOrigin.X), (int)MathF.Floor(mapOrigin.Y));
        var launchHeight = launchTile is { } start && _world.Contains(start) ? ProjectileSurfaceHeight(start) : ProjectileSurfaceHeight(tile);
        var origin = new Vector3(mapOrigin + new Vector2(launchOffset, 0), launchHeight + 0.8f);
        var target = new Vector3(mapTarget, ProjectileSurfaceHeight(tile) + 0.035f);
        var launch = new Vector3(outward, -0.5f, rise);
        if (_view3D)
        {
            var camera = Matrix.Invert(Matrices3D().View);
            origin = _playerView is PlayerView.ThirdPerson ? PlayerGhostCenter3D()
                : camera.Translation + camera.Forward * 0.12f + camera.Right * 0.075f - camera.Up * 0.06f;
            origin += camera.Right * launchOffset;
            target = ProjectileTarget3D(tile);
            var local = LocalPoint3D(origin);
            launch = SurfaceUp3D(local.X, local.Z) * rise + camera.Right * outward + camera.Forward * 0.5f;
        }
        if (rocketPod) launch = target - origin;
        var flight = new GuidedMissileFlight(Numerics(origin), Numerics(launch), Numerics(target),
            _view3D && IsPlanetoid3D ? PlanetoidGeometry.Radius(_world.Width) : 0, guided: !rocketPod);
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget, new(0, shockwave: true))
            { Missile = flight, FollowCursor = !rocketPod && _guidedMissileFollowsCursor });
        try
        {
            if (_missileSounds.Count > 0)
                _missileSounds[Random.Shared.Next(_missileSounds.Count)]
                    .Play(0.8f, Random.Shared.NextSingle() * 0.16f - 0.08f, 0);
        }
        catch (NoAudioHardwareException) { }
    }

    private float ProjectileSurfaceHeight(GridPosition tile) => Math.Max(TileHeight3D(tile),
        ThreeDGeometry.ColumnHeight(WaterElevation3D(tile) ?? _world.GetElevation(tile)));

    private bool MissileHitsSurface(Vector3 position, bool threeD, out GridPosition tile)
    {
        tile = default;
        float altitude;
        if (threeD)
        {
            var local = LocalPoint3D(position);
            var x = (int)MathF.Floor(local.X);
            var y = (int)MathF.Floor(local.Z);
            if (!ContainsTile3D(x, y)) return false;
            tile = MapTile3D(x, y);
            altitude = local.Y;
        }
        else
        {
            if (MapTile2D((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y)) is not { } mapTile ||
                !_world.Contains(mapTile)) return false;
            tile = mapTile;
            altitude = position.Z;
        }
        return altitude <= ProjectileSurfaceHeight(tile) + 0.015f;
    }

    private void AdvanceRifleShots(float seconds)
    {
        _cloudPerforations3D.Advance(seconds);
        _rifleCooldownRemaining = Math.Max(0, _rifleCooldownRemaining - seconds);
        _missileCooldownRemaining = Math.Max(0, _missileCooldownRemaining - seconds);
        _rocketPodCooldownRemaining = Math.Max(0, _rocketPodCooldownRemaining - seconds);
        _laserCooldownRemaining = Math.Max(0, _laserCooldownRemaining - seconds);
        var mouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
        var hovered = CurrentTool is WorldTool.GuidedMissiles && !IsPointerOverWarCharts(mouse)
            ? ScreenToWorld(mouse.X, mouse.Y) : null;
        for (var i = _rifleShots.Count - 1; i >= 0; i--)
        {
            var shot = _rifleShots[i];
            if (shot.Missile is { Arrived: false } missile)
            {
                if (missile.Guided && shot.FollowCursor && shot.ThreeD == _view3D && hovered is { } tile)
                {
                    shot.ImpactTile = tile;
                    if (shot.ThreeD) missile.Target = Numerics(ProjectileTarget3D(tile));
                    else
                    {
                        var map = MapLocation2D(tile);
                        var target = new Vector3(map.X + 0.5f, map.Y + 0.5f, ProjectileSurfaceHeight(tile) + 0.035f);
                        if (WrapsMapX) target.X += MathF.Round((missile.Position.X - target.X) / MapWidth2D) * MapWidth2D;
                        missile.Target = Numerics(target);
                    }
                }
                if (missile.Advance(seconds, position =>
                {
                    if (shot.ThreeD) PunchCloudPoint3D(Framework(position), 0.9f);
                    if (!MissileHitsSurface(Framework(position), shot.ThreeD, out var hitTile)) return false;
                    shot.ImpactTile = hitTile;
                    return true;
                }))
                {
                    shot.Timing.Advance(shot.Timing.TravelSeconds);
                    Impacts.CreateMissileImpact(_world, shot.ImpactTile);
                }
            }
            else
            {
                var previousHead = Vector3.Lerp(shot.Origin, shot.Target, shot.Timing.Progress);
                if (shot.Timing.Advance(seconds))
                {
                    if (shot.MeteorMagnitude is { } magnitude)
                        Impacts.CreateToolMeteorImpact(_world, shot.ImpactTile, magnitude);
                    else Impacts.CreateRifleImpact(_world, shot.ImpactTile);
                }
                if (shot.ThreeD && shot.Missile is null && shot.Timing.Progress > 0)
                {
                    var head = Vector3.Lerp(shot.Origin, shot.Target, shot.Timing.Progress);
                    if (head != previousHead) PunchCloudSegment3D(previousHead, head,
                        shot.MeteorMagnitude is { } magnitude ? 1.2f + magnitude * 0.5f : 0.45f);
                }
            }
            if (shot.Timing.Finished || shot.Missile is { Expired: true }) _rifleShots.RemoveAt(i);
        }
    }

    private void DrawRifleShots3D(Matrix view, Matrix projection, bool depthMaskOnly = false)
    {
        if (!_rifleShots.Any(shot => shot.ThreeD)) return;
        // Emissive projectiles must not change the terrain/water lighting state.
        _projectileEffect3D ??= new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        // Mark rifle pixels against terrain before the translucent player and clouds write depth.
        // Bit 2 survives the cloud pass, which uses only stencil bit 1.
        _rifleTerrainMask3D ??= new DepthStencilState
        {
            DepthBufferEnable = true, DepthBufferWriteEnable = false,
            StencilEnable = true, StencilMask = 2, StencilWriteMask = 2,
            ReferenceStencil = 2, StencilFunction = CompareFunction.Always,
            StencilPass = StencilOperation.Replace
        };
        _rifleCloudOverlay3D ??= new DepthStencilState
        {
            DepthBufferEnable = false, DepthBufferWriteEnable = false,
            StencilEnable = true, StencilMask = 2, StencilWriteMask = 0,
            ReferenceStencil = 2, StencilFunction = CompareFunction.Equal,
            StencilPass = StencilOperation.Keep
        };
        _rifleDepthOnlyBlend3D ??= new BlendState { ColorWriteChannels = ColorWriteChannels.None };
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.BlendState = depthMaskOnly ? _rifleDepthOnlyBlend3D : BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = depthMaskOnly ? _rifleTerrainMask3D : _rifleCloudOverlay3D;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _projectileEffect3D!.World = Matrix.Identity;
        _projectileEffect3D.View = view;
        _projectileEffect3D.Projection = projection;
        _projectileEffect3D.DiffuseColor = Vector3.One;
        var camera = Matrix.Invert(view);
        foreach (var shot in _rifleShots)
        {
            if (!shot.ThreeD) continue;
            if (shot.MeteorMagnitude is not null)
                DrawMeteorSmoke3D(shot, view, projection, camera);
            var color = new Color(shot.BeamColor ?? new Color(155, 155, 155), (int)(220 * shot.Timing.Opacity));
            _rifleLineVertices[0] = new(shot.Origin, color);
            var head = shot.Missile is { } missile ? Framework(missile.Position) : Vector3.Lerp(shot.Origin, shot.Target, shot.Timing.Progress);
            _rifleLineVertices[1] = new(head, color);
            foreach (var pass in _projectileEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                if (shot.Missile is { } trail)
                {
                    for (var i = 0; i < trail.Trail.Count; i++)
                        _missileTrailVertices[i] = new(Framework(trail.Trail[i]), color);
                    _missileTrailVertices[trail.Trail.Count] = new(head, color);
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineStrip, _missileTrailVertices, 0, trail.Trail.Count);
                }
                else GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _rifleLineVertices, 0, 1);
            }
            var clip = Vector4.Transform(new Vector4(head, 1), view * projection);
            if (clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W) continue;
            var pixelSize = 2 * clip.W / (GraphicsDevice.Viewport.Height * projection.M22);
            var halfSize = shot.MeteorMagnitude is { } meteorMagnitude
                ? Math.Max(0.12f + meteorMagnitude * 0.12f, pixelSize * 2)
                : Math.Clamp(0.035f, pixelSize * 1.5f, pixelSize * 3);
            GraphicsDevice.BlendState = depthMaskOnly ? _rifleDepthOnlyBlend3D : BlendState.Additive;
            Glow(2.5f, (shot.BeamColor ?? new Color(255, 70, 4)) * (shot.Timing.Opacity * 0.10f));
            Glow(1.6f, (shot.BeamColor ?? new Color(255, 100, 8)) * (shot.Timing.Opacity * 0.25f));
            Glow(1, (shot.BeamColor ?? new Color(255, 130, 20)) * shot.Timing.Opacity);
            Glow(0.4f, (shot.BeamColor is { } beam ? Color.Lerp(beam, Color.White, 0.65f) : new Color(255, 221, 126)) * shot.Timing.Opacity);
            GraphicsDevice.BlendState = depthMaskOnly ? _rifleDepthOnlyBlend3D : BlendState.NonPremultiplied;

            void Glow(float scale, Color shade)
            {
                var right = camera.Right * (halfSize * scale);
                var up = camera.Up * (halfSize * scale);
                _rifleGlowVertices[0] = new(head - right - up, shade);
                _rifleGlowVertices[1] = new(head + right - up, shade);
                _rifleGlowVertices[2] = new(head + right + up, shade);
                _rifleGlowVertices[3] = new(head - right - up, shade);
                _rifleGlowVertices[4] = new(head + right + up, shade);
                _rifleGlowVertices[5] = new(head - right + up, shade);
                foreach (var pass in _projectileEffect3D.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _rifleGlowVertices, 0, 2);
                }
            }
        }
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }

    private void DrawRifleShots2D()
    {
        foreach (var shot in _rifleShots)
        {
            if (shot.ThreeD) continue;
            if (shot.MeteorMagnitude is not null) DrawMeteorSmoke2D(shot);
            var from = new Vector2(MapOffsetX + (shot.MapOrigin.X - _cameraX) * TileSize,
                MapOffsetY + (shot.MapOrigin.Y - _cameraY) * TileSize);
            var head = shot.Missile is { } missile ? new Vector2(missile.Position.X, missile.Position.Y)
                : Vector2.Lerp(shot.MapOrigin, shot.MapTarget, shot.Timing.Progress);
            var to = new Vector2(MapOffsetX + (head.X - _cameraX) * TileSize,
                MapOffsetY + (head.Y - _cameraY) * TileSize);
            if (shot.Missile is { } trail)
            {
                for (var i = 1; i < trail.Trail.Count; i++)
                    Segment(MapPoint(trail.Trail[i - 1]), MapPoint(trail.Trail[i]));
                Segment(MapPoint(trail.Trail[^1]), to);
            }
            else Segment(from, to);

            Vector2 MapPoint(System.Numerics.Vector3 point) => new(
                MapOffsetX + (point.X - _cameraX) * TileSize,
                MapOffsetY + (point.Y - _cameraY) * TileSize);
            void Segment(Vector2 start, Vector2 end)
            {
                var delta = end - start;
                _spriteBatch!.Draw(_pixel, start, null, (shot.BeamColor ?? new Color(155, 155, 155)) * shot.Timing.Opacity,
                    MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), 1.5f), SpriteEffects.None, 0);
            }
            var x = (int)MathF.Round(to.X);
            var y = (int)MathF.Round(to.Y);
            _spriteBatch!.Draw(_pixel, new Rectangle(x - 6, y - 6, 12, 12), (shot.BeamColor ?? new Color(255, 70, 4)) * (shot.Timing.Opacity * 0.10f));
            _spriteBatch!.Draw(_pixel, new Rectangle(x - 4, y - 4, 8, 8), (shot.BeamColor ?? new Color(255, 100, 8)) * (shot.Timing.Opacity * 0.25f));
            _spriteBatch!.Draw(_pixel, new Rectangle(x - 2, y - 2, 4, 4), (shot.BeamColor ?? new Color(255, 130, 20)) * shot.Timing.Opacity);
            _spriteBatch!.Draw(_pixel, new Rectangle(x - 1, y - 1, 2, 2), (shot.BeamColor is { } beam ? Color.Lerp(beam, Color.White, 0.65f) : new Color(255, 221, 126)) * shot.Timing.Opacity);
        }
    }
}
