using System.Numerics;
using Newt.Simulation;

namespace Newt.Game;

internal static class PlanetoidGeometry
{
    internal static float Radius(int width) => PlanetoidLayout.FaceSize(width) * 2f / MathF.PI;

    internal static (Vector3 East, Vector3 Up, Vector3 South) Frame(float x, float y, int width)
    {
        var size = PlanetoidLayout.FaceSize(width);
        var face = PlanetoidLayout.FaceAt(x, y, size);
        var up = PlanetoidLayout.Direction(x, y, size, face);
        var axis = PlanetoidLayout.East(face);
        var east = Vector3.Normalize(axis - up * Vector3.Dot(axis, up));
        return (east, up, Vector3.Cross(east, up));
    }

    internal static Vector3 Position(Vector3 tile, int width, GridPosition? owner = null)
    {
        var size = PlanetoidLayout.FaceSize(width);
        var face = PlanetoidLayout.FaceAt(owner?.X ?? tile.X, owner?.Y ?? tile.Z, size);
        return PlanetoidLayout.Direction(tile.X, tile.Z, size, face) * (Radius(width) + tile.Y);
    }

    internal static Vector3 TilePosition(Vector3 point, int width)
    {
        var atlas = PlanetoidLayout.AtlasPosition(point, PlanetoidLayout.FaceSize(width));
        return new(atlas.X, point.Length() - Radius(width), atlas.Y);
    }

    internal static (Vector2 Position, float Yaw) Move(Vector2 position, Vector2 step, float yaw, int width)
    {
        if (step == Vector2.Zero) return (position, yaw);
        var (east, up, south) = Frame(position.X, position.Y, width);
        // Transport the horizontal heading independently of pitch. A vertical
        // look direction has no horizontal component to recover yaw from.
        var heading = -east * MathF.Sin(yaw) - south * MathF.Cos(yaw);
        var tangent = Vector3.Normalize(east * step.X + south * step.Y);
        var rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(up, tangent)),
            step.Length() / Radius(width));
        var atlas = PlanetoidLayout.AtlasPosition(Vector3.Transform(up, rotation), PlanetoidLayout.FaceSize(width));
        var (nextEast, _, nextSouth) = Frame(atlas.X, atlas.Y, width);
        heading = Vector3.Transform(heading, rotation);
        return (atlas, MathF.Atan2(-Vector3.Dot(heading, nextEast), -Vector3.Dot(heading, nextSouth)));
    }
}
