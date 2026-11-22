using Microsoft.Xna.Framework;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private static readonly (string Name, Color Color, double Interval)[] LaserModes =
    [
        ("Red", new Color(255, 35, 35), 0.70),
        ("Blue", new Color(45, 100, 255), 0.45),
        ("Ultraviolet", new Color(180, 55, 255), 0.28),
        ("X-ray", new Color(65, 255, 85), 0.16),
        ("Gamma ray", new Color(255, 140, 25), 0.09),
    ];
    private int _laserPowerIndex;
    private double _laserCooldownRemaining;

    private void ChangeLaserPower(int direction)
    {
        _laserPowerIndex = Mod(_laserPowerIndex + direction, LaserModes.Length);
        _laserCooldownRemaining = Math.Min(_laserCooldownRemaining, LaserModes[_laserPowerIndex].Interval);
    }

    private void FireLaser(GridPosition tile)
    {
        if (_paused) return;
        var mode = LaserModes[_laserPowerIndex];
        var target = ProjectileTarget3D(tile);
        var origin = target;
        var map = MapLocation2D(tile);
        var mapTarget = new Vector2(map.X + 0.5f, map.Y + 0.5f);
        var mapOrigin = new Vector2(_cameraX + (GraphicsDevice.Viewport.Width / 2f - MapOffsetX) / TileSize,
            _cameraY + (MapViewportHeight / 2f - MapOffsetY) / TileSize);
        if (WrapsMapX) mapTarget.X += MathF.Round((mapOrigin.X - mapTarget.X) / MapWidth2D) * MapWidth2D;
        if (_view3D)
        {
            var camera = Matrix.Invert(Matrices3D().View);
            origin = _playerView is PlayerView.ThirdPerson ? PlayerGhostCenter3D()
                : camera.Translation + camera.Forward * 0.12f + camera.Right * 0.075f - camera.Up * 0.06f;
            PunchCloudSegment3D(origin, target, 0.45f + _laserPowerIndex * 0.1f);
        }
        // Keep one live beam while aiming; melting pulses have their own power-dependent cadence.
        _rifleShots.RemoveAll(shot => shot.BeamColor is not null);
        var timing = new RifleShotTiming(0, instant: true, fadeSeconds: 0.12f);
        timing.Advance(0); // Its instant effect is applied below, never by the rifle impact path.
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget, timing) { BeamColor = mode.Color });
        if (_laserCooldownRemaining > 0.0001) return;
        _laserCooldownRemaining = mode.Interval;
        Volcanism.MeltWithLaser(_world, tile, _laserPowerIndex);
    }
}
