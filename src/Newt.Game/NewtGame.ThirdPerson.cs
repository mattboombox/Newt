using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private AlphaTestEffect? _ghostEffect3D;
    private CritterSpecies _playerGhostSpecies = CritterSpecies.Ape;
    private const float ThirdPersonDistance = 4f;

    private void RandomizePlayerGhostSprite()
    {
        // Cosmetic randomness never advances the simulation RNG. Avoid an
        // immediate repeat when another loaded critter sprite is available.
        var choices = _critterSprites.Keys.Where(species => species != _playerGhostSpecies).ToArray();
        if (choices.Length > 0)
            _playerGhostSpecies = choices[Random.Shared.Next(choices.Length)];
    }

    private Vector3 PlayerCameraPosition3D()
    {
        var eye = WorldPoint3D(new Vector3(_focus3D.X, _firstPersonEyeHeight, _focus3D.Y));
        if (_playerView is not PlayerView.ThirdPerson) return eye;
        var up = SurfaceUp3D(_focus3D.X);
        var east = IsRing3D ? Framework(RingGeometry.Frame(_focus3D.X, _world.Width).East) : Vector3.UnitX;
        // The walker's eye eases up after a step. Keep the lower chase target
        // above the new ground/deck during that transition too.
        var targetHeight = Math.Max(_firstPersonEyeHeight - 0.45f, WalkingSurfaceHeight(_focus3D) + 0.15f);
        var target = WorldPoint3D(new Vector3(_focus3D.X, targetHeight, _focus3D.Y));
        return Framework(ThreeDGeometry.ThirdPersonCameraPosition(Numerics(target),
            Numerics(CameraLookDirection3D(east, up)), ThirdPersonDistance, point =>
            {
                var local = IsRing3D
                    ? Framework(RingGeometry.StripPosition(point, _world.Width, _world.Height)) : Framework(point);
                var x = (int)MathF.Floor(local.X);
                var y = (int)MathF.Floor(local.Z);
                if (!ContainsTile3D(x, y)) return true;
                var tile = MapTile3D(x, y);
                const float clearance = 0.15f;
                if (local.Y < TileHeight3D(tile) + clearance) return true;
                // Water is transparent; ice and bridge decks obstruct the camera
                // on either side, including while the shared walker swims below.
                if (WaterElevation3D(tile) is not { } water) return false;
                var surface = ThreeDGeometry.ColumnHeight(water);
                return (IsIce3D(tile) || IsShallowsBridge3D(tile)) &&
                    Math.Abs(local.Y - surface) < clearance;
            }));
    }

    private void DrawPlayerGhost3D(Matrix view, Matrix projection)
    {
        if (_playerView is not PlayerView.ThirdPerson ||
            !_critterSprites.TryGetValue(_playerGhostSpecies, out var sprite)) return;
        // This is visual camera state only: it never occupies a simulation tile
        // or participates in collision, hunger, combat, or critter movement.
        var feet = Math.Max(WalkingSurfaceHeight(_focus3D) + 0.025f,
            _firstPersonEyeHeight - FirstPersonEyeOffset + 0.025f);
        var position = WorldPoint3D(new Vector3(_focus3D.X, feet, _focus3D.Y));
        var camera = Matrix.Invert(view);
        var halfSize = WalkingSpriteScale3D * 0.5f;
        var center = position + SurfaceUp3D(_focus3D.X) * halfSize;
        var right = camera.Right * halfSize;
        var up = camera.Up * halfSize;
        // Fade when an obstruction pushes the chase camera into the avatar.
        var opacity = 0.72f * Math.Clamp(Vector3.Distance(camera.Translation, center) / 0.8f, 0, 1);
        var a = new VertexPositionColorTexture(center - right + up, Color.White, new Vector2(0, 0));
        var b = new VertexPositionColorTexture(center + right + up, Color.White, new Vector2(1, 0));
        var c = new VertexPositionColorTexture(center + right - up, Color.White, new Vector2(1, 1));
        var d = new VertexPositionColorTexture(center - right - up, Color.White, new Vector2(0, 1));
        VertexPositionColorTexture[] vertices = [a, b, c, a, c, d];
        _ghostEffect3D ??= new AlphaTestEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
            DiffuseColor = new Vector3(0.2f, 0.5f, 1f),
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 0,
        };
        _ghostEffect3D.Texture = sprite;
        _ghostEffect3D.View = view;
        _ghostEffect3D.Projection = projection;
        _ghostEffect3D.Alpha = opacity;
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        // Water/ice renders afterward and must respect visible ghost pixels in
        // front of it. Discard empty sprite texels before writing depth, keeping
        // the background transparent and allowing closer sheets to cover swimmers.
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
        foreach (var pass in _ghostEffect3D.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
        }
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}
