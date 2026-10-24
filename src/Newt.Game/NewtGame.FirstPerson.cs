using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool _firstPerson, _firstPersonMouseReady;
    private float _firstPersonEyeHeight;
    private bool _firstPersonUnderIce;
    private const float FirstPersonEyeOffset = 1.2f;
    private const float FirstPersonRenderDistance = 96f;

    private float WalkingSurfaceHeight(Vector2 position)
    {
        var tile = MapTile3D((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
        var ground = TileHeight3D(tile);
        // Solid ice and bridge decks support the walker above the water.
        if (!_firstPersonUnderIce && IsIce3D(tile) && WaterElevation3D(tile) is { } surface)
            ground = Math.Max(ground, ThreeDGeometry.ColumnHeight(surface) + 0.035f);
        if (IsShallowsBridge3D(tile))
            ground = ThreeDGeometry.RoadHeight(ground, WaterElevation3D(tile), shallowsBridge: true);
        return ground;
    }

    private float? FirstPersonWaterSurface(Vector2 position)
    {
        var tile = MapTile3D((int)MathF.Floor(position.X), (int)MathF.Floor(position.Y));
        if ((IsIce3D(tile) && !_firstPersonUnderIce) || IsShallowsBridge3D(tile) || WaterElevation3D(tile) is not { } water)
            return null;
        var surface = ThreeDGeometry.ColumnHeight(water);
        return surface > TileHeight3D(tile) ? surface : null;
    }

    private float InitialFirstPersonEyeHeight(Vector2 position)
    {
        _firstPersonUnderIce = false;
        return Math.Max(WalkingSurfaceHeight(position) + FirstPersonEyeOffset,
            FirstPersonWaterSurface(position) is { } water ? water + FirstPersonEyeOffset * 0.75f : float.MinValue);
    }

    private void EnterFirstPerson()
    {
        _firstPerson = true;
        // Entering first person explicitly resumes camera control. Population
        // charts remain informational; the help panel releases mouse capture.
        _controlsWindowOpen = false;
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
        _firstPersonEyeHeight = InitialFirstPersonEyeHeight(_focus3D);
        IsMouseVisible = false;
        ResetToolRepeat();
    }

    private void HandleFirstPerson(KeyboardState keyboard, MouseState mouse, float seconds)
    {
        if (_setupMenuOpen || _controlsWindowOpen)
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
        var previousTile = MapTile3D((int)MathF.Floor(previous.X), (int)MathF.Floor(previous.Y));
        var walkingOnIce = IsIce3D(previousTile) && !_firstPersonUnderIce &&
            WaterElevation3D(previousTile) is { } previousWater &&
            _firstPersonEyeHeight >= ThreeDGeometry.ColumnHeight(previousWater) + 0.035f;
        _focus3D += new Vector2(step.X, step.Y);
        WrapCamera3D();
        if (!ContainsTile3D((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y)))
            _focus3D = previous;
        var nextTile = MapTile3D((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y));
        if (IsIce3D(nextTile) && WaterElevation3D(nextTile) is { } iceWater)
        {
            var entry = ThreeDGeometry.FirstPersonIceEntry(_firstPersonEyeHeight, TileHeight3D(nextTile),
                ThreeDGeometry.ColumnHeight(iceWater), walkingOnIce, _firstPersonUnderIce);
            if (entry is ThreeDGeometry.IceEntry.Blocked)
                _focus3D = previous;
            else
                _firstPersonUnderIce = entry is ThreeDGeometry.IceEntry.Below;
        }
        else
            _firstPersonUnderIce = false;
        var playerTile = new GridPosition((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y));
        if (_world.FindTeleporterArrival(playerTile) is { } arrival && ContainsTile3D(arrival.X, arrival.Y))
        {
            _focus3D = new Vector2(arrival.X + 0.5f, arrival.Y + 0.5f);
            _firstPersonEyeHeight = InitialFirstPersonEyeHeight(_focus3D);
        }
        var ground = WalkingSurfaceHeight(_focus3D);
        var currentTile = MapTile3D((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y));
        float? ceiling = _firstPersonUnderIce && WaterElevation3D(currentTile) is { } iceSurface
            ? ThreeDGeometry.ColumnHeight(iceSurface) - 0.06f : null;
        _firstPersonEyeHeight = ThreeDGeometry.FirstPersonSwimmingHeight(_firstPersonEyeHeight, ground,
            FirstPersonWaterSurface(_focus3D), keyboard.IsKeyDown(Keys.Space), seconds, FirstPersonEyeOffset, ceiling,
            fastSink: keyboard.IsKeyDown(Keys.LeftControl));
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

    private void DrawFirstPersonUnderwaterTint()
    {
        if (!_firstPerson || _spriteBatch is null || _pixel is null) return;
        var tile = MapTile3D((int)MathF.Floor(_focus3D.X), (int)MathF.Floor(_focus3D.Y));
        var elevation = WaterElevation3D(tile);
        var opacity = ThreeDGeometry.UnderwaterTintOpacity(_firstPersonEyeHeight,
            elevation is { } water ? ThreeDGeometry.ColumnHeight(water) : null);
        if (opacity <= 0) return;
        // Tint the world view, including underwater objects; draw the interface afterward.
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, MapViewportHeight),
            new Color(20, 85, 125) * opacity);
    }

    private void DrawFirstPersonCrosshair()
    {
        if (!_firstPerson || _setupMenuOpen || _controlsWindowOpen) return;
        var x = GraphicsDevice.Viewport.Width / 2;
        var y = MapViewportHeight / 2;
        _spriteBatch!.Draw(_pixel, new Rectangle(x - 4, y, 9, 1), Color.White * 0.8f);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y - 4, 1, 9), Color.White * 0.8f);
    }
}
