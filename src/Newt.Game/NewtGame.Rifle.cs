using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private sealed record RifleShot(GridPosition Tile, bool ThreeD, Vector3 Origin, Vector3 Target,
        Vector2 MapOrigin, Vector2 MapTarget, RifleShotTiming Timing);
    private readonly List<RifleShot> _rifleShots = [];
    private readonly VertexPositionColor[] _rifleLineVertices = new VertexPositionColor[2];
    private readonly VertexPositionColor[] _rifleGlowVertices = new VertexPositionColor[6];
    private SoundEffect? _rifleSound;
    private DepthStencilState? _rifleTerrainMask3D;
    private DepthStencilState? _rifleCloudOverlay3D;
    private BlendState? _rifleDepthOnlyBlend3D;
    private const double RifleCooldownSeconds = 1;
    private double _rifleCooldownRemaining;
    private bool HasVisibleImpactWave => _world.ActiveImpactWaveCount > 0 ||
        _rifleShots.Any(shot => shot.Timing.ShockwaveVisible);

    private bool TryGetVisibleImpactWave(GridPosition tile, out ImpactWaveSnapshot wave)
    {
        foreach (var shot in _rifleShots)
        {
            if (!shot.Timing.ShockwaveVisible) continue;
            wave = new(shot.Tile, shot.Timing.ShockwaveProgress, 1, 0, WaveKind.Impact);
            if (Impacts.IsOnShockFront(_world, tile, wave)) return true;
        }
        for (var i = 0; i < _world.ActiveImpactWaveCount; i++)
        {
            wave = _world.GetImpactWave(i);
            var center = wave.Center;
            if (wave.Kind is WaveKind.Impact && wave.MaximumRadius <= 1 &&
                _rifleShots.Any(shot => shot.Timing.Impacted && shot.Tile == center)) continue;
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
        }
        catch (NoAudioHardwareException) { }
    }

    private void FireAntiMaterielRifle(GridPosition tile)
    {
        if (_rifleCooldownRemaining > 0) return;
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
            origin = camera.Translation + camera.Forward * 0.12f + camera.Right * 0.075f - camera.Up * 0.06f;
        }
        var distance = _view3D ? Vector3.Distance(origin, target) : Vector2.Distance(mapOrigin, mapTarget);
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget, new(distance)));
        try { _rifleSound?.Play(0.8f, 0, 0); }
        catch (NoAudioHardwareException) { }
    }

    private void AdvanceRifleShots(float seconds)
    {
        _rifleCooldownRemaining = Math.Max(0, _rifleCooldownRemaining - seconds);
        for (var i = _rifleShots.Count - 1; i >= 0; i--)
        {
            var shot = _rifleShots[i];
            if (shot.Timing.Advance(seconds)) Impacts.CreateRifleImpact(_world, shot.Tile);
            if (shot.Timing.Finished) _rifleShots.RemoveAt(i);
        }
    }

    private void DrawRifleShots3D(Matrix view, Matrix projection, bool depthMaskOnly = false)
    {
        if (!_rifleShots.Any(shot => shot.ThreeD)) return;
        // Mark rifle pixels against terrain before clouds write their own depth.
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
        _terrainEffect3D!.World = Matrix.Identity;
        _terrainEffect3D.View = view;
        _terrainEffect3D.Projection = projection;
        _terrainEffect3D.DiffuseColor = Vector3.One;
        var camera = Matrix.Invert(view);
        foreach (var shot in _rifleShots)
        {
            if (!shot.ThreeD) continue;
            var color = new Color(155, 155, 155, (int)(220 * shot.Timing.Opacity));
            _rifleLineVertices[0] = new(shot.Origin, color);
            var head = Vector3.Lerp(shot.Origin, shot.Target, shot.Timing.Progress);
            _rifleLineVertices[1] = new(head, color);
            foreach (var pass in _terrainEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _rifleLineVertices, 0, 1);
            }
            var clip = Vector4.Transform(new Vector4(head, 1), view * projection);
            if (clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W) continue;
            var pixelSize = 2 * clip.W / (GraphicsDevice.Viewport.Height * projection.M22);
            var halfSize = Math.Clamp(0.035f, pixelSize * 1.5f, pixelSize * 3);
            GraphicsDevice.BlendState = depthMaskOnly ? _rifleDepthOnlyBlend3D : BlendState.Additive;
            Glow(2.5f, new Color(255, 70, 4) * (shot.Timing.Opacity * 0.10f));
            Glow(1.6f, new Color(255, 100, 8) * (shot.Timing.Opacity * 0.25f));
            Glow(1, new Color(255, 130, 20) * shot.Timing.Opacity);
            Glow(0.4f, new Color(255, 221, 126) * shot.Timing.Opacity);
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
                foreach (var pass in _terrainEffect3D.CurrentTechnique.Passes)
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
            var from = new Vector2(MapOffsetX + (shot.MapOrigin.X - _cameraX) * TileSize,
                MapOffsetY + (shot.MapOrigin.Y - _cameraY) * TileSize);
            var head = Vector2.Lerp(shot.MapOrigin, shot.MapTarget, shot.Timing.Progress);
            var to = new Vector2(MapOffsetX + (head.X - _cameraX) * TileSize,
                MapOffsetY + (head.Y - _cameraY) * TileSize);
            var delta = to - from;
            _spriteBatch!.Draw(_pixel, from, null, new Color(155, 155, 155) * shot.Timing.Opacity,
                MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), 1.5f), SpriteEffects.None, 0);
            var x = (int)MathF.Round(to.X);
            var y = (int)MathF.Round(to.Y);
            _spriteBatch.Draw(_pixel, new Rectangle(x - 6, y - 6, 12, 12), new Color(255, 70, 4) * (shot.Timing.Opacity * 0.10f));
            _spriteBatch.Draw(_pixel, new Rectangle(x - 4, y - 4, 8, 8), new Color(255, 100, 8) * (shot.Timing.Opacity * 0.25f));
            _spriteBatch.Draw(_pixel, new Rectangle(x - 2, y - 2, 4, 4), new Color(255, 130, 20) * shot.Timing.Opacity);
            _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 1, 2, 2), new Color(255, 221, 126) * shot.Timing.Opacity);
        }
    }
}
