using System.Numerics;
using Newt.Simulation;

namespace Newt.Game;

internal static class PlanetoidNetFold
{
    internal static Vector3 Position(float x, float y, float height, int width, GridPosition owner, float progress)
    {
        var size = PlanetoidLayout.FaceSize(width);
        var face = PlanetoidLayout.FaceAt(owner.X, owner.Y, size);
        var u = x - face % 3 * size - size / 2f;
        var v = size / 2f - (y - face / 3 * size);
        var half = size / 2f;
        var fold = Smooth(Math.Clamp(progress / 0.7f, 0, 1));
        var angle = fold * MathF.PI / 2;
        var right = Matrix4x4.CreateRotationY(angle);
        var hinge = new Vector3(half, 0, half);
        var cube = face switch
        {
            1 => hinge + Vector3.Transform(new Vector3(u + half, v, 0), right),
            2 => hinge + Vector3.Transform(new Vector3(size, 0, 0) +
                Vector3.Transform(new Vector3(u + half, v, 0), right), right),
            3 => new Vector3(-half, 0, half) + Vector3.Transform(new Vector3(u - half, v, 0), Matrix4x4.CreateRotationY(-angle)),
            4 => new Vector3(0, half, half) + Vector3.Transform(new Vector3(u, v + half, 0), Matrix4x4.CreateRotationX(-angle)),
            5 => new Vector3(0, -half, half) + Vector3.Transform(new Vector3(u, v - half, 0), Matrix4x4.CreateRotationX(angle)),
            _ => new Vector3(u, v, half),
        };
        var sphere = PlanetoidGeometry.Position(new Vector3(x, height, y), width, owner);
        return Vector3.Lerp(cube, sphere, Smooth(Math.Clamp((progress - 0.7f) / 0.3f, 0, 1)));
    }

    internal static float Smooth(float t) => t * t * (3 - 2 * t);
}
