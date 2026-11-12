using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool IsRing3D => _world.IsRingHabitat;
    private bool IsPlanetoid3D => _world.Body is WorldBody.Planetoid;
    private bool HasCurvedSurface3D => IsRing3D || IsPlanetoid3D || _world.Body is WorldBody.Nexus;
    private Vector3[] _ringUpFrames3D = [];

    private static System.Numerics.Vector3 Numerics(Vector3 v) => new(v.X, v.Y, v.Z);
    private static Vector3 Framework(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private Vector3 WorldPoint3D(Vector3 point, GridPosition? owner = null)
    {
        if (IsPlanetoid3D)
            return Framework(PlanetoidGeometry.Position(Numerics(point), _world.Width, owner));
        if (_world.Body is WorldBody.Nexus)
            return new Vector3(point.X, point.Y + NexusLift3D(point.X, point.Z), point.Z);
        if (!IsRing3D) return point;
        if (_ringUpFrames3D.Length != _world.Width * 2)
        {
            _ringUpFrames3D = new Vector3[_world.Width * 2];
            for (var i = 0; i < _ringUpFrames3D.Length; i++)
                _ringUpFrames3D[i] = Framework(RingGeometry.Frame(i / 2f, _world.Width).Up);
        }
        var halfTile = (int)MathF.Round(point.X * 2);
        var up = Math.Abs(point.X * 2 - halfTile) < 0.00001f
            ? _ringUpFrames3D[ThreeDGeometry.WrapTile(halfTile, _ringUpFrames3D.Length)]
            : Framework(RingGeometry.Frame(point.X, _world.Width).Up);
        var radius = RingGeometry.Radius(_world.Width);
        return new Vector3(0, radius, point.Z - _world.Height / 2f) - up * (radius - point.Y);
    }

    private float NexusLift3D(float x, float z)
    {
        var center = NexusLayout.Center(_world);
        return ThreeDGeometry.NexusPetalLift(x - center.X, z - center.Y, NexusLayout.Radius(_world));
    }

    private Vector3 LocalPoint3D(Vector3 point)
    {
        if (IsPlanetoid3D)
            return Framework(PlanetoidGeometry.TilePosition(Numerics(point), _world.Width));
        if (IsRing3D)
            return Framework(RingGeometry.StripPosition(Numerics(point), _world.Width, _world.Height));
        if (_world.Body is WorldBody.Nexus)
            point.Y -= NexusLift3D(point.X, point.Z);
        return point;
    }

    private Vector3 SurfaceUp3D(float x, float y) => SurfaceFrame3D(x, y).Up;

    private (Vector3 East, Vector3 Up, Vector3 South) SurfaceFrame3D(float x, float y)
    {
        if (IsPlanetoid3D)
        {
            var (east, up, south) = PlanetoidGeometry.Frame(x, y, _world.Width);
            return (Framework(east), Framework(up), Framework(south));
        }
        if (IsRing3D)
        {
            var (east, up) = RingGeometry.Frame(x, _world.Width);
            return (Framework(east), Framework(up), Vector3.UnitZ);
        }
        return (Vector3.UnitX, Vector3.Up, Vector3.UnitZ);
    }

    private BoundingBox TileBounds3D(int x, int y, float height)
    {
        if (_world.Body is WorldBody.Nexus)
        {
            var center = NexusLayout.Center(_world);
            var nearX = x < center.X ? x + 1 : x;
            var nearY = y < center.Y ? y + 1 : y;
            var farX = x < center.X ? x : x + 1;
            var farY = y < center.Y ? y : y + 1;
            return new BoundingBox(new Vector3(x, NexusLift3D(nearX, nearY), y),
                new Vector3(x + 1, NexusLift3D(farX, farY) + height, y + 1));
        }
        if (!IsRing3D && !IsPlanetoid3D)
            return new BoundingBox(new Vector3(x, 0, y), new Vector3(x + 1, height, y + 1));
        return CurvedTileBounds3D(x, y, height);
    }

    private BoundingBox CurvedTileBounds3D(int x, int y, float height)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var dx = 0; dx <= 1; dx++)
        for (var dz = 0; dz <= 1; dz++)
        for (var dh = 0; dh <= 1; dh++)
        {
            var p = WorldPoint3D(new Vector3(x + dx, height * dh, y + dz), new GridPosition(x, y));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }

    private (Matrix View, Matrix Projection) CurvedOrbitMatrices3D()
    {
        var tile = MapTile3D((int)_focus3D.X, (int)_focus3D.Y);
        var target = WorldPoint3D(new Vector3(_focus3D.X, Math.Max(4.5f, TileHeight3D(tile)), _focus3D.Y));
        var (east, up, south) = SurfaceFrame3D(_focus3D.X, _focus3D.Y);
        var offset = EyeOffset3D();
        var eye = target + east * offset.X + up * offset.Y + south * offset.Z;
        // Keep the camera above the local terrain as the surface curves underneath it.
        var stripEye = LocalPoint3D(eye);
        var eyeTile = MapTile3D((int)stripEye.X, (int)MathF.Floor(stripEye.Z));
        if ((!IsPlanetoid3D || ContainsTile3D(eyeTile.X, (int)MathF.Floor(stripEye.Z))) &&
            stripEye.Y < TileHeight3D(eyeTile) + 1.5f)
        {
            stripEye.Y = TileHeight3D(eyeTile) + 1.5f;
            eye = WorldPoint3D(stripEye);
        }
        var cameraDistance = Vector3.Distance(eye, target);
        if (_pitch3D < 0.25f) target = eye + CameraLookDirection3D(east, up, south) * _distance3D;
        var cameraUp = east * (-MathF.Sin(_yaw3D) * MathF.Sin(_pitch3D)) +
            up * MathF.Cos(_pitch3D) - south * (MathF.Cos(_yaw3D) * MathF.Sin(_pitch3D));
        return (Matrix.CreateLookAt(eye, target, cameraUp),
            Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Viewport3D().AspectRatio, 0.1f,
                RenderDistance3D(Math.Max(ThreeDGeometry.FarClipDistance(_distance3D),
                    RingGeometry.Radius(_world.Width) * 2.2f), cameraDistance)));
    }

    private float? CurvedColumnHit3D(Ray ray, int x, int y, float height)
    {
        if (ray.Intersects(TileBounds3D(x, y, 14)) is null) return null;
        var tile = new GridPosition(x, y);
        var a = WorldPoint3D(new Vector3(x, height, y), tile);
        var b = WorldPoint3D(new Vector3(x + 1, height, y), tile);
        var c = WorldPoint3D(new Vector3(x + 1, height, y + 1), tile);
        var d = WorldPoint3D(new Vector3(x, height, y + 1), tile);
        var e = WorldPoint3D(new Vector3(x, 0, y), tile);
        var f = WorldPoint3D(new Vector3(x + 1, 0, y), tile);
        var g = WorldPoint3D(new Vector3(x + 1, 0, y + 1), tile);
        var h = WorldPoint3D(new Vector3(x, 0, y + 1), tile);
        float? closest = null;
        void Triangle(Vector3 p, Vector3 q, Vector3 r)
        {
            var hit = RingGeometry.TriangleHit(Numerics(ray.Position), Numerics(ray.Direction), Numerics(p), Numerics(q), Numerics(r));
            if (hit is { } distance && (closest is null || distance < closest)) closest = distance;
        }
        void Face(Vector3 p, Vector3 q, Vector3 r, Vector3 s)
        { Triangle(p, q, r); Triangle(p, r, s); }
        Face(a, b, c, d);
        Face(e, f, b, a);
        Face(f, g, c, b);
        Face(g, h, d, c);
        Face(h, e, a, d);
        Face(e, h, g, f);
        return closest;
    }

    private void CurveWaterVertices3D(List<VertexPositionColor> vertices)
    {
        if (!HasCurvedSurface3D || IsPlanetoid3D) return;
        for (var i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            v.Position = WorldPoint3D(v.Position);
            vertices[i] = v;
        }
    }

    private void Match2DCameraToCurved3D()
    {
        var viewport = Viewport3D();
        var (view, projection) = Matrices3D();
        var tile = PickTile3D(viewport.Width / 2, viewport.Height / 2) ??
            MapTile3D((int)_focus3D.X, (int)_focus3D.Y);
        var height = IsIce3D(tile) && WaterElevation3D(tile) is { } surface
            ? ThreeDGeometry.ColumnHeight(surface) + 0.035f : TileHeight3D(tile);
        var local = new Vector3(tile.X + 0.5f, height, tile.Y + 0.5f);
        var point = viewport.Project(WorldPoint3D(local), projection, view, Matrix.Identity);
        var east = viewport.Project(WorldPoint3D(local + Vector3.UnitX), projection, view, Matrix.Identity) - point;
        var south = viewport.Project(WorldPoint3D(local + Vector3.UnitZ), projection, view, Matrix.Identity) - point;
        _zoomIndex = ThreeDGeometry.NearestZoomIndex(MathF.Sqrt(Math.Abs(east.X * south.Y - east.Y * south.X)), ZoomLevels);
        var location = MapLocation2D(tile);
        _cameraX = ConstrainCameraX((int)MathF.Round(location.X + 0.5f - (viewport.Width / 2f - MapOffsetX) / TileSize));
        _cameraY = Math.Clamp((int)MathF.Round(location.Y + 0.5f - (viewport.Height / 2f - MapOffsetY) / TileSize),
            0, Math.Max(0, _world.Height - viewport.Height / TileSize));
    }
}
