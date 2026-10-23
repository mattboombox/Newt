using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool _firstPerson, _firstPersonMouseReady;
    private float _firstPersonEyeHeight;
    private const float FirstPersonEyeOffset = 1.2f;
    private const float FirstPersonRenderDistance = 96f;

    private float WalkingSurfaceHeight(Vector2 position)
    {
        var tile = MapTile3D((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
        var ground = TileHeight3D(tile);
        // Walk on the bed of open water; only solid ice supports the walker at the surface.
        if (IsIce3D(tile) && WaterElevation3D(tile) is { } surface)
            ground = Math.Max(ground, ThreeDGeometry.ColumnHeight(surface) + 0.035f);
        return ground;
    }

    private void EnterFirstPerson()
    {
        _firstPerson = true;
        _firstPersonMouseReady = false;
        _distance3D = 6;
        _pitch3D = 0;
        _inspectedCritterId = default;
        WrapCamera3D();
        if (!ContainsTile3D((int)_focus3D.X, (int)_focus3D.Y))
        {
            var nearest = float.MaxValue;
            var start = _focus3D;
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
            {
                if (!ContainsTile3D(x, y)) continue;
                var point = new Vector2(x + 0.5f, y + 0.5f);
                var distance = Vector2.DistanceSquared(point, _focus3D);
                if (distance < nearest) { nearest = distance; start = point; }
            }
            _focus3D = start;
        }
        _firstPersonEyeHeight = WalkingSurfaceHeight(_focus3D) + FirstPersonEyeOffset;
        IsMouseVisible = false;
        ResetToolRepeat();
    }

    private void HandleFirstPerson(KeyboardState keyboard, MouseState mouse, float seconds)
    {
        if (_setupMenuOpen || _controlsWindowOpen || _populationWindowMode != 0)
        {
            IsMouseVisible = true;
            _firstPersonMouseReady = false;
            return;
        }
        IsMouseVisible = false;
        seconds = Math.Clamp(seconds, 0, 0.1f);
        var centerX = GraphicsDevice.Viewport.Width / 2;
        var centerY = MapViewportHeight / 2;
        if (_firstPersonMouseReady)
        {
            _yaw3D = MathHelper.WrapAngle(_yaw3D - (mouse.X - centerX) * 0.003f);
            _pitch3D = Math.Clamp(_pitch3D + (mouse.Y - centerY) * 0.003f,
                -MathHelper.PiOver2 + 0.02f, MathHelper.PiOver2 - 0.02f);
        }
        Mouse.SetPosition(centerX, centerY);
        _firstPersonMouseReady = true;
        var forward = (keyboard.IsKeyDown(Keys.W) ? 1 : 0) - (keyboard.IsKeyDown(Keys.S) ? 1 : 0);
        var right = (keyboard.IsKeyDown(Keys.D) ? 1 : 0) - (keyboard.IsKeyDown(Keys.A) ? 1 : 0);
        var running = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
        var step = ThreeDGeometry.WalkingStep(_yaw3D, forward, right, seconds, running);
        var previous = _focus3D;
        _focus3D += new Vector2(step.X, step.Y);
        WrapCamera3D();
        if (!ContainsTile3D((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y)))
            _focus3D = previous;
        var playerTile = new GridPosition((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y));
        if (_world.FindTeleporterArrival(playerTile) is { } arrival && ContainsTile3D(arrival.X, arrival.Y))
        {
            _focus3D = new Vector2(arrival.X + 0.5f, arrival.Y + 0.5f);
            _firstPersonEyeHeight = WalkingSurfaceHeight(_focus3D) + FirstPersonEyeOffset;
        }
        var ground = WalkingSurfaceHeight(_focus3D);
        var desired = ground + FirstPersonEyeOffset;
        _firstPersonEyeHeight += (desired - _firstPersonEyeHeight) * (1 - MathF.Exp(-12 * seconds));
        _firstPersonEyeHeight = Math.Max(_firstPersonEyeHeight, ground + 0.15f);
    }

    private (Matrix View, Matrix Projection) FirstPersonMatrices3D()
    {
        var eye = WorldPoint3D(new Vector3(_focus3D.X, _firstPersonEyeHeight, _focus3D.Y));
        var up = SurfaceUp3D(_focus3D.X);
        var east = IsRing3D ? Framework(RingGeometry.Frame(_focus3D.X, _world.Width).East) : Vector3.UnitX;
        var forward = CameraLookDirection3D(east, up);
        var cameraUp = east * (-MathF.Sin(_yaw3D) * MathF.Sin(_pitch3D)) + up * MathF.Cos(_pitch3D) -
            Vector3.UnitZ * (MathF.Cos(_yaw3D) * MathF.Sin(_pitch3D));
        var far = IsRing3D ? Math.Max(396, RingGeometry.Radius(_world.Width) * 2.2f)
            : FirstPersonRenderDistance;
        return (Matrix.CreateLookAt(eye, eye + forward, cameraUp),
            Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Viewport3D().AspectRatio, 0.03f, far));
    }

    private void DrawFirstPersonCrosshair()
    {
        if (!_firstPerson || _setupMenuOpen || _controlsWindowOpen || _populationWindowMode != 0) return;
        var x = GraphicsDevice.Viewport.Width / 2;
        var y = MapViewportHeight / 2;
        _spriteBatch!.Draw(_pixel, new Rectangle(x - 4, y, 9, 1), Color.White * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y - 4, 1, 9), Color.White * 0.8f);
    }
}
