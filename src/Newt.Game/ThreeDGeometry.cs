using Newt.Simulation;

namespace Newt.Game;

/// <summary>Shared finite-map geometry, independent of the graphics device.</summary>
internal static class ThreeDGeometry
{
    internal static float ColumnHeight(float elevation) => 0.25f + (elevation + 1) * 4;

    // Match the vertical world span of the 2D view with the 45-degree 3D lens.
    internal static float OverheadDistance(int viewportHeight, int pixelsPerTile) =>
        viewportHeight / (2 * MathF.Tan(MathF.PI / 8) * pixelsPerTile);

    internal static float MaximumZoomDistance(int viewportHeight, int minimumPixelsPerTile) =>
        Math.Max(180, OverheadDistance(viewportHeight, minimumPixelsPerTile));

    internal static float FarClipDistance(float cameraDistance) => Math.Max(180, cameraDistance) * 2.2f;

    internal static System.Numerics.Vector3 LookDirection(float yaw, float pitch) => new(
        -MathF.Sin(yaw) * MathF.Cos(pitch), -MathF.Sin(pitch), -MathF.Cos(yaw) * MathF.Cos(pitch));

    internal static System.Numerics.Vector2 WalkingStep(float yaw, float forward, float right, float seconds, bool running)
    {
        var movement = new System.Numerics.Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw)) * forward +
            new System.Numerics.Vector2(MathF.Cos(yaw), -MathF.Sin(yaw)) * right;
        if (movement.LengthSquared() > 1) movement = System.Numerics.Vector2.Normalize(movement);
        return movement * Math.Clamp(seconds, 0, 0.1f) * (running ? 8.75f : 3.5f);
    }

    internal static int NearestZoomIndex(float pixelsPerTile, ReadOnlySpan<int> levels)
    {
        var best = 0;
        var error = float.MaxValue;
        for (var i = 0; i < levels.Length; i++)
        {
            var difference = Math.Abs(MathF.Log(levels[i] / Math.Max(0.001f, pixelsPerTile)));
            if (difference < error) { error = difference; best = i; }
        }
        return best;
    }

    internal static float WrapCamera(float focus, int size) => (focus % size + size) % size;

    internal static int WrapTile(int coordinate, int size) => (coordinate % size + size) % size;

    internal static int FirstVisibleCopy(int tile, int minimum, int size) =>
        tile + (int)Math.Ceiling((minimum - tile) / (double)size) * size;

    internal static (int Left, int Right) SingleMapSpan(int left, int right, float focus, int width)
    {
        if (right - left <= width) return (left, right);
        var start = Math.Clamp((int)MathF.Floor(focus) - width / 2, left, right - width);
        return (start, start + width);
    }

    internal static bool IsIce(Terrain terrain, SurfaceWaterKind water, Biome biome) =>
        terrain == Terrain.Ice || water == SurfaceWaterKind.FreshwaterLake && biome == Biome.Arctic;

    internal static float? WaterElevation(Terrain terrain, SurfaceWaterKind water,
        bool hasOceans, float seaLevel, float? lakeLevel) =>
        water == SurfaceWaterKind.FreshwaterLake ? lakeLevel : hasOceans && terrain is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice ? seaLevel : null;

    internal static bool StandsOnIce(CritterHabitat habitat) => habitat is
        CritterHabitat.LandDweller or CritterHabitat.Flier or CritterHabitat.LandAndWaterDweller;

    internal static bool IsSurfaceSailor(CritterSpecies species, bool colonist, bool pirate, bool veteran) =>
        colonist || pirate || veteran || species is CritterSpecies.Custodian or CritterSpecies.ApeSailor or
            CritterSpecies.ApeTraderSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
            CritterSpecies.ApeBomber or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or CritterSpecies.ApeRoyalGuard;

    internal static (System.Numerics.Vector3 Right, System.Numerics.Vector3 Up) BillboardBasis(
        System.Numerics.Vector3 towardCamera, System.Numerics.Vector3 fallbackRight)
    {
        var normal = towardCamera.LengthSquared() > 0.000001f
            ? System.Numerics.Vector3.Normalize(towardCamera) : System.Numerics.Vector3.UnitZ;
        var right = System.Numerics.Vector3.Cross(System.Numerics.Vector3.UnitY, normal);
        if (right.LengthSquared() < 0.000001f) right = fallbackRight;
        right = System.Numerics.Vector3.Normalize(right);
        return (right, System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(normal, right)));
    }

    // Returns the first visible face, including sides of taller columns.
    internal static float? IntersectColumn(float ox, float oy, float oz,
        float dx, float dy, float dz, int x, int y, float height)
    {
        var near = 0f;
        var far = float.PositiveInfinity;
        bool Slab(float origin, float direction, float minimum, float maximum)
        {
            if (Math.Abs(direction) < 0.000001f) return origin >= minimum && origin <= maximum;
            var a = (minimum - origin) / direction;
            var b = (maximum - origin) / direction;
            near = Math.Max(near, Math.Min(a, b));
            far = Math.Min(far, Math.Max(a, b));
            return near <= far;
        }
        return Slab(ox, dx, x, x + 1) && Slab(oy, dy, 0, height) && Slab(oz, dz, y, y + 1)
            ? near : null;
    }
}
