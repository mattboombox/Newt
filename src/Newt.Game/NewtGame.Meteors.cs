using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private const int MeteorSmokePuffCount = 48;

    private static Vector3 MeteorPosition3D(RifleShot shot, float progress) =>
        Vector3.Lerp(shot.Origin, shot.Target, progress) + shot.ArcOffset * (4 * progress * (1 - progress));
    private static Vector2 MeteorPosition2D(RifleShot shot, float progress) =>
        Vector2.Lerp(shot.MapOrigin, shot.MapTarget, progress) + shot.MapArcOffset * (4 * progress * (1 - progress));

    private void LaunchMeteorFragment(Newt.Simulation.GridPosition source, Newt.Simulation.GridPosition tile, int radius, float magnitude)
    {
        var origin = ProjectileTarget3D(source);
        var target = ProjectileTarget3D(tile);
        var startMap = MapLocation2D(source);
        var endMap = MapLocation2D(tile);
        var mapOrigin = new Vector2(startMap.X + 0.5f, startMap.Y + 0.5f);
        var mapTarget = new Vector2(endMap.X + 0.5f, endMap.Y + 0.5f);
        if (WrapsMapX) mapTarget.X += MathF.Round((mapOrigin.X - mapTarget.X) / MapWidth2D) * MapWidth2D;
        var (_, up, _) = SurfaceFrame3D(source.X + 0.5f, source.Y + 0.5f);
        var distance = Vector3.Distance(origin, target);
        var flight = Math.Clamp(0.45f + Vector2.Distance(mapOrigin, mapTarget) / 35f, 0.5f, 2.5f);
        var mapDelta = mapTarget - mapOrigin;
        var bend = mapDelta.LengthSquared() > 0 ? Vector2.Normalize(new(-mapDelta.Y, mapDelta.X)) : Vector2.UnitY;
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget,
            new(distance, travelSeconds: flight))
        {
            MeteorMagnitude = magnitude, FragmentRadius = radius,
            ArcOffset = up * Math.Max(2, distance * 0.4f),
            MapArcOffset = bend * Math.Max(1, mapDelta.Length() * 0.22f)
        });
    }

    private void LaunchMeteor(Newt.Simulation.GridPosition tile, float magnitude)
    {
        if (_paused) return;
        var target = ProjectileTarget3D(tile);
        var (east, up, south) = SurfaceFrame3D(tile.X + 0.5f, tile.Y + 0.5f);
        var angle = Random.Shared.NextSingle() * MathHelper.TwoPi;
        var height = 30 + magnitude * 10;
        var sideways = height * (0.3f + Random.Shared.NextSingle() * 0.8f);
        var origin = target + up * height + (east * MathF.Cos(angle) + south * MathF.Sin(angle)) * sideways;
        var map = MapLocation2D(tile);
        var mapTarget = new Vector2(map.X + 0.5f, map.Y + 0.5f);
        if (WrapsMapX)
        {
            var cameraX = _cameraX + (GraphicsDevice.Viewport.Width / 2f - MapOffsetX) / TileSize;
            mapTarget.X += MathF.Round((cameraX - mapTarget.X) / MapWidth2D) * MapWidth2D;
        }
        var mapOrigin = mapTarget + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * height;
        var distance = _view3D ? Vector3.Distance(origin, target) : Vector2.Distance(mapOrigin, mapTarget);
        _rifleShots.Add(new(tile, _view3D, origin, target, mapOrigin, mapTarget, new(distance))
            { MeteorMagnitude = magnitude });
    }

    private static bool MeteorSmokePuff(RifleShot shot, int index, out float fraction,
        out float age, out float size, out float opacity)
    {
        fraction = index / (float)MeteorSmokePuffCount;
        age = shot.Timing.Age - fraction * shot.Timing.TravelSeconds;
        size = 0.20f + (shot.MeteorMagnitude ?? 0) * 0.08f + Math.Max(0, age) * 0.6f;
        opacity = Math.Clamp(1 - age / 1.6f, 0, 1) * 0.48f;
        return age >= 0 && opacity > 0 && fraction <= shot.Timing.Progress;
    }

    private void DrawMeteorSmoke3D(RifleShot shot, Matrix view, Matrix projection, Matrix camera)
    {
        var (east, up, south) = SurfaceFrame3D(shot.ImpactTile.X + 0.5f, shot.ImpactTile.Y + 0.5f);
        var viewProjection = view * projection;
        for (var puff = 0; puff <= MeteorSmokePuffCount; puff += shot.FragmentRadius is null ? 1 : 3)
        {
            if (!MeteorSmokePuff(shot, puff, out var fraction, out var age, out var size, out var opacity)) continue;
            var center = MeteorPosition3D(shot, fraction) +
                up * (age * 0.22f) + (east * MathF.Sin(puff * 2.4f) + south * MathF.Cos(puff * 1.7f)) * (age * 0.16f);
            var clip = Vector4.Transform(new Vector4(center, 1), viewProjection);
            if (clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W) continue;
            var right = camera.Right * size;
            var vertical = camera.Up * size;
            var color = new Color(58 + puff % 4 * 6, 62 + puff % 4 * 6, 66 + puff % 4 * 6, (int)(255 * opacity));
            _rifleGlowVertices[0] = new(center - right - vertical, color);
            _rifleGlowVertices[1] = new(center + right - vertical, color);
            _rifleGlowVertices[2] = new(center + right + vertical, color);
            _rifleGlowVertices[3] = new(center - right - vertical, color);
            _rifleGlowVertices[4] = new(center + right + vertical, color);
            _rifleGlowVertices[5] = new(center - right + vertical, color);
            foreach (var pass in _projectileEffect3D!.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _rifleGlowVertices, 0, 2);
            }
        }
    }

    private void DrawMeteorSmoke2D(RifleShot shot)
    {
        for (var puff = 0; puff <= MeteorSmokePuffCount; puff += shot.FragmentRadius is null ? 1 : 3)
        {
            if (!MeteorSmokePuff(shot, puff, out var fraction, out var age, out var size, out var opacity)) continue;
            var center = MeteorPosition2D(shot, fraction) +
                new Vector2(MathF.Sin(puff * 2.4f), -1) * (age * 0.22f);
            var pixel = new Vector2(MapOffsetX + (center.X - _cameraX) * TileSize,
                MapOffsetY + (center.Y - _cameraY) * TileSize);
            _spriteBatch!.Draw(_pixel, pixel, null, new Color(58, 62, 66) * opacity,
                puff * 2.4f, new Vector2(0.5f), new Vector2(Math.Max(2, size * TileSize * 2)), SpriteEffects.None, 0);
        }
    }
}
