using Newt.Simulation;

namespace Newt.Game;

/// <summary>Shared finite-map geometry, independent of the graphics device.</summary>
internal static class ThreeDGeometry
{
    internal static float ColumnHeight(float elevation) => 0.25f + (elevation + 1) * 4;

    internal static float UnderwaterTintOpacity(float eyeHeight, float? waterHeight) =>
        waterHeight is { } surface ? Math.Clamp((surface - eyeHeight) / 0.15f, 0, 1) * 0.28f : 0;

    internal static float FirstPersonSwimmingHeight(float current, float ground, float? waterSurface,
        bool rising, float seconds, float eyeOffset = 1.2f, float? ceiling = null, bool fastSink = false)
    {
        var standing = ground + eyeOffset;
        if (waterSurface is not { } water)
            return Math.Max(current + (standing - current) * (1 - MathF.Exp(-12 * seconds)), ground + 0.15f);
        var target = rising ? Math.Max(standing, water + eyeOffset * 0.75f) : standing;
        if (ceiling is { } limit) target = Math.Min(target, limit);
        var speed = rising ? 0.8f : fastSink ? 1.2f : 0.4f;
        var distance = speed * seconds;
        var floor = ceiling is null ? standing : ground + 0.15f;
        var result = Math.Max(floor, current + Math.Clamp(target - current, -distance, distance));
        return ceiling is { } top ? Math.Min(result, top) : result;
    }

    internal enum IceEntry { Above, Below, Blocked }

    internal static IceEntry FirstPersonIceEntry(float eye, float bed, float surface,
        bool walkingOnIce, bool alreadyUnderIce)
    {
        if (!alreadyUnderIce && (walkingOnIce || eye >= surface + 0.035f))
            return IceEntry.Above;
        // Leave clearance for the camera's near plane below the slab's underside.
        var ceiling = surface - 0.06f;
        return eye <= ceiling && bed + 0.15f <= ceiling ? IceEntry.Below : IceEntry.Blocked;
    }

    internal readonly record struct ConnectionSideSegment(
        int FaceX, int FaceY, int TargetX, int TargetY, float Start, float End);

    private static readonly (RiverConnection Flag, int X, int Y)[] ConnectionDirections =
    [
        (RiverConnection.North, 0, -1), (RiverConnection.NorthEast, 1, -1),
        (RiverConnection.East, 1, 0), (RiverConnection.SouthEast, 1, 1),
        (RiverConnection.South, 0, 1), (RiverConnection.SouthWest, -1, 1),
        (RiverConnection.West, -1, 0), (RiverConnection.NorthWest, -1, -1),
    ];

    internal static float RiverSideBottom(float targetHeight, float adjacentHeight, float? waterElevation) =>
        Math.Max(Math.Max(targetHeight, adjacentHeight),
            waterElevation is { } water ? ColumnHeight(water) : float.MinValue);

    internal static IEnumerable<ConnectionSideSegment> ConnectionSideSegments(RiverConnection connections)
    {
        const float width = 1f / 3;
        foreach (var (flag, dx, dy) in ConnectionDirections)
        {
            if ((connections & flag) == 0) continue;
            if (dx == 0 || dy == 0)
                yield return new(dx, dy, dx, dy, 0.5f - width / 2, 0.5f + width / 2);
            else
            {
                // Diagonal top artwork reaches a corner along both adjoining faces.
                yield return new(dx, 0, dx, dy, dy < 0 ? 0 : 1 - width, dy < 0 ? width : 1);
                yield return new(0, dy, dx, dy, dx < 0 ? 0 : 1 - width, dx < 0 ? width : 1);
            }
        }
    }

    internal static float RoadHeight(float bedHeight, float? waterElevation, bool shallowsBridge) =>
        shallowsBridge && waterElevation is { } water
            ? Math.Max(bedHeight + 0.01f, ColumnHeight(water) + 0.015f)
            : bedHeight + 0.01f;

    internal static float RoadSideBottom(float targetHeight, float adjacentHeight,
        float? waterElevation, bool targetIsBridge) =>
        Math.Max(RoadHeight(targetHeight, waterElevation, targetIsBridge), adjacentHeight);

    internal static System.Numerics.Vector2 SunDirection(float skyPhase)
    {
        // Dawn at .88, noon at .14, sunset at .40, matching the sky palette.
        var daylight = (skyPhase + 0.12f) % 1f;
        var angle = daylight <= 0.52f
            ? daylight / 0.52f * MathF.PI
            : MathF.PI + (daylight - 0.52f) / 0.48f * MathF.PI;
        return new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

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

    internal static bool FlattensUnderWaterCover(CritterHabitat habitat, bool surfaceSailor,
        bool ice, bool bridge, float clearance) =>
        !surfaceSailor && !StandsOnIce(habitat) && (bridge || ice && clearance < 1f);

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
