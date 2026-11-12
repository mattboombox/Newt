using System.Numerics;

namespace Newt.Simulation;

/// <summary>Six square faces in a three-by-two map atlas, projected onto a sphere.</summary>
public static class PlanetoidLayout
{
    private static readonly (Vector3 Normal, Vector3 East, Vector3 South)[] Faces =
    [
        (Vector3.UnitZ, Vector3.UnitX, -Vector3.UnitY),
        (Vector3.UnitX, -Vector3.UnitZ, -Vector3.UnitY),
        (-Vector3.UnitZ, -Vector3.UnitX, -Vector3.UnitY),
        (-Vector3.UnitX, Vector3.UnitZ, -Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (-Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ),
    ];

    public static int FaceSize(int width) => width / 3;

    public static int FaceAt(float x, float y, int size) =>
        Math.Clamp((int)(x / size), 0, 2) + 3 * Math.Clamp((int)(y / size), 0, 1);

    public static Vector3 Direction(float x, float y, int size, int face)
    {
        var (normal, east, south) = Faces[face];
        return Vector3.Normalize(normal + east * FaceCoordinate(x / size - face % 3) +
            south * FaceCoordinate(y / size - face / 3));
    }

    private static float FaceCoordinate(float fraction) => MathF.Tan((fraction - 0.5f) * (MathF.PI / 2));

    public static Vector3 East(int face) => Faces[face].East;

    public static Vector3 MapDirection(float x, float y, int width, int height)
    {
        var (sinLongitude, cosLongitude) = MathF.SinCos((x / width - 0.5f) * MathF.Tau);
        var (sinLatitude, cosLatitude) = MathF.SinCos((0.5f - y / height) * MathF.PI);
        return new(sinLongitude * cosLatitude, sinLatitude, cosLongitude * cosLatitude);
    }

    public static Vector2 MapPosition(Vector3 direction, int width, int height)
    {
        var longitude = MathF.Atan2(direction.X, direction.Z) / MathF.Tau + 0.5f;
        var latitude = MathF.Asin(Math.Clamp(direction.Y, -1, 1)) / MathF.PI;
        return new((longitude % 1) * width, Math.Clamp((0.5f - latitude) * height, 0, height - 0.001f));
    }

    public static Vector2 AtlasPosition(Vector3 direction, int size)
    {
        var abs = Vector3.Abs(direction);
        var face = abs.X >= abs.Y && abs.X >= abs.Z ? direction.X >= 0 ? 1 : 3
            : abs.Y >= abs.Z ? direction.Y >= 0 ? 4 : 5 : direction.Z >= 0 ? 0 : 2;
        var (normal, east, south) = Faces[face];
        var scale = Vector3.Dot(direction, normal);
        if (scale == 0) return new Vector2(size / 2f);
        return new Vector2(Coordinate(east) + face % 3 * size, Coordinate(south) + face / 3 * size);

        float Coordinate(Vector3 axis) => Math.Clamp(
            (MathF.Atan(Vector3.Dot(direction, axis) / scale) * (2 / MathF.PI) + 0.5f) * size,
            0, size - 0.001f);
    }

    public static GridPosition Offset(GridPosition position, int dx, int dy, int size)
    {
        var face = FaceAt(position.X, position.Y, size);
        var x = position.X + dx;
        var y = position.Y + dy;
        if (x >= face % 3 * size && x < (face % 3 + 1) * size &&
            y >= face / 3 * size && y < (face / 3 + 1) * size)
            return new(x, y);
        var atlas = AtlasPosition(Direction(x + 0.5f, y + 0.5f, size, face), size);
        return new((int)atlas.X, (int)atlas.Y);
    }
}
