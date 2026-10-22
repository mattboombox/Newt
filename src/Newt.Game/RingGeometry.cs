using System.Numerics;

namespace Newt.Game;

/// <summary>Embeds a finite-width strip on the inside of a closed cylindrical ring.</summary>
internal static class RingGeometry
{
    internal static float Radius(int circumference) => circumference / MathF.Tau;

    internal static (Vector3 East, Vector3 Up) Frame(float x, int circumference)
    {
        var angle = ThreeDGeometry.WrapCamera(x, circumference) / circumference * MathF.Tau;
        return (new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0),
            new Vector3(-MathF.Sin(angle), MathF.Cos(angle), 0));
    }

    internal static Vector3 Position(Vector3 strip, int circumference, int width)
    {
        var radius = Radius(circumference);
        var (_, up) = Frame(strip.X, circumference);
        return new Vector3(0, radius, strip.Z - width / 2f) - up * (radius - strip.Y);
    }

    internal static Vector3 StripPosition(Vector3 position, int circumference, int width)
    {
        var radius = Radius(circumference);
        var radial = new Vector2(position.X, radius - position.Y);
        var x = MathF.Atan2(radial.X, radial.Y) / MathF.Tau * circumference;
        return new Vector3(ThreeDGeometry.WrapCamera(x, circumference), radius - radial.Length(), position.Z + width / 2f);
    }

    internal static float? TriangleHit(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a;
        var e2 = c - a;
        var p = Vector3.Cross(direction, e2);
        var determinant = Vector3.Dot(e1, p);
        if (Math.Abs(determinant) < 0.000001f) return null;
        var inverse = 1 / determinant;
        var t = origin - a;
        var u = Vector3.Dot(t, p) * inverse;
        if (u < -0.00001f || u > 1.00001f) return null;
        var q = Vector3.Cross(t, e1);
        var v = Vector3.Dot(direction, q) * inverse;
        if (v < -0.00001f || u + v > 1.00001f) return null;
        var distance = Vector3.Dot(e2, q) * inverse;
        return distance >= 0 ? distance : null;
    }
}
