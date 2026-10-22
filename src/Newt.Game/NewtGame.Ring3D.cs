using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool IsRing3D => _world.Body == WorldBody.RingWorld;
    private Vector3[] _ringUpFrames3D = [];
    private BoundingBox[] _ringBounds3D = [];
    private int _ringBoundsWidth3D, _ringBoundsHeight3D;

    private static System.Numerics.Vector3 Numerics(Vector3 v) => new(v.X, v.Y, v.Z);
    private static Vector3 Framework(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    private Vector3 WorldPoint3D(Vector3 point)
    {
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

    private Vector3 SurfaceUp3D(float x) => IsRing3D
        ? Framework(RingGeometry.Frame(x, _world.Width).Up) : Vector3.Up;

    private BoundingBox TileBounds3D(int x, int y, float height)
    {
        if (!IsRing3D) return new BoundingBox(new Vector3(x, 0, y), new Vector3(x + 1, height, y + 1));
        if (height == 14)
        {
            if (_ringBoundsWidth3D != _world.Width || _ringBoundsHeight3D != _world.Height)
            {
                _ringBoundsWidth3D = _world.Width;
                _ringBoundsHeight3D = _world.Height;
                _ringBounds3D = new BoundingBox[_world.Width * _world.Height];
                for (var row = 0; row < _world.Height; row++)
                for (var column = 0; column < _world.Width; column++)
                    _ringBounds3D[row * _world.Width + column] = CurvedTileBounds3D(column, row, 14);
            }
            return _ringBounds3D[y * _world.Width + x];
        }
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
            var p = WorldPoint3D(new Vector3(x + dx, height * dh, y + dz));
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }

    private (Matrix View, Matrix Projection) RingMatrices3D()
    {
        var tile = MapTile3D((int)_focus3D.X, (int)_focus3D.Y);
        var target = WorldPoint3D(new Vector3(_focus3D.X, Math.Max(4.5f, TileHeight3D(tile)), _focus3D.Y));
        var frame = RingGeometry.Frame(_focus3D.X, _world.Width);
        var east = Framework(frame.East);
        var up = Framework(frame.Up);
        var offset = EyeOffset3D();
        var eye = target + east * offset.X + up * offset.Y + Vector3.UnitZ * offset.Z;
        // Keep the camera above the local terrain as the rail curves underneath it.
        var stripEye = RingGeometry.StripPosition(Numerics(eye), _world.Width, _world.Height);
        var eyeTile = MapTile3D((int)stripEye.X, (int)MathF.Floor(stripEye.Z));
        if (stripEye.Y < TileHeight3D(eyeTile) + 1.5f)
        {
            stripEye.Y = TileHeight3D(eyeTile) + 1.5f;
            eye = WorldPoint3D(Framework(stripEye));
        }
        if (_pitch3D < 0.25f) target = eye + CameraLookDirection3D(east, up) * _distance3D;
        var cameraUp = east * (-MathF.Sin(_yaw3D) * MathF.Sin(_pitch3D)) +
            up * MathF.Cos(_pitch3D) - Vector3.UnitZ * (MathF.Cos(_yaw3D) * MathF.Sin(_pitch3D));
        return (Matrix.CreateLookAt(eye, target, cameraUp),
            Matrix.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Viewport3D().AspectRatio, 0.1f,
                Math.Max(ThreeDGeometry.FarClipDistance(_distance3D), RingGeometry.Radius(_world.Width) * 2.2f)));
    }

    private float? RingColumnHit3D(Ray ray, int x, int y, float height)
    {
        if (ray.Intersects(TileBounds3D(x, y, 14)) is null) return null;
        var a = WorldPoint3D(new Vector3(x, height, y));
        var b = WorldPoint3D(new Vector3(x + 1, height, y));
        var c = WorldPoint3D(new Vector3(x + 1, height, y + 1));
        var d = WorldPoint3D(new Vector3(x, height, y + 1));
        var e = WorldPoint3D(new Vector3(x, 0, y));
        var f = WorldPoint3D(new Vector3(x + 1, 0, y));
        var g = WorldPoint3D(new Vector3(x + 1, 0, y + 1));
        var h = WorldPoint3D(new Vector3(x, 0, y + 1));
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
        if (!IsRing3D) return;
        for (var i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            v.Position = WorldPoint3D(v.Position);
            vertices[i] = v;
        }
    }

    private void Match2DCameraToRing3D()
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
        _cameraX = ConstrainCameraX((int)MathF.Round(local.X - (viewport.Width / 2f - MapOffsetX) / TileSize));
        _cameraY = Math.Clamp((int)MathF.Round(local.Z - (viewport.Height / 2f - MapOffsetY) / TileSize),
            0, Math.Max(0, _world.Height - viewport.Height / TileSize));
    }
}
