using Microsoft.Xna.Framework;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private readonly CloudPerforations _cloudPerforations3D = new();
    private readonly Dictionary<(int X, int Y, int Face), CloudPatch3D> _cloudPatchLookup3D = [];
    private Vector3? _previousCloudTraveller3D;
    private PlayerView _previousCloudTravellerView3D;

    private void PunchCameraClouds3D(Matrix view)
    {
        var point = _playerView is PlayerView.ThirdPerson
            ? PlayerGhostCenter3D() : Matrix.Invert(view).Translation;
        const float radius = 1.5f;
        // Sweep between frames, but never carve a long tunnel on a teleport or
        // a switch between the player and the detached camera.
        if (_previousCloudTraveller3D is { } previous && _previousCloudTravellerView3D == _playerView &&
            Vector3.DistanceSquared(previous, point) <= 16 * 16)
            PunchCloudSegment3D(previous, point, radius);
        else
            PunchCloudPoint3D(point, radius);
        _previousCloudTraveller3D = point;
        _previousCloudTravellerView3D = _playerView;
    }

    private void PunchCloudSegment3D(Vector3 from, Vector3 to, float radius)
    {
        var distance = Vector3.Distance(from, to);
        var samples = Math.Max(1, (int)MathF.Ceiling(distance / 0.25f));
        for (var i = 0; i <= samples; i++)
            PunchCloudPoint3D(Vector3.Lerp(from, to, i / (float)samples), radius);
    }

    private void PunchCloudPoint3D(Vector3 point, float radius)
    {
        if (_clouds3D.Count == 0) return;
        // Undo exactly the wind transform used to draw the cloud cells. Holes stay
        // attached to the cloud as it drifts, including over wrapped/cube-face seams.
        var circumference = IsPlanetoid3D ? MathHelper.TwoPi * PlanetoidGeometry.Radius(_world.Width) : _world.Width;
        var drift = (float)(_environmentAnimationSeconds * CloudDriftTilesPerSecond % circumference);
        var angle = MathHelper.TwoPi * drift / circumference;
        if (IsPlanetoid3D) point = Vector3.Transform(point, Matrix.CreateRotationY(-angle));
        else if (IsRing3D)
        {
            var axis = new Vector3(0, RingGeometry.Radius(_world.Width), 0);
            point = Vector3.Transform(point - axis, Matrix.CreateRotationZ(-angle)) + axis;
        }
        var local = LocalPoint3D(point);
        var top = CloudBaseHeight3D + CloudThickness3D;
        if (local.Y < top - CloudThickness3D - radius || local.Y > top + radius) return;
        if (!IsPlanetoid3D && !IsRing3D) local.X -= drift;

        var face = -1;
        int x, y;
        if (IsPlanetoid3D)
        {
            var size = PlanetoidLayout.FaceSize(_world.Width);
            x = (int)MathF.Floor(local.X / size * _cloudFaceSize3D);
            y = (int)MathF.Floor(local.Z / size * _cloudFaceSize3D);
            face = PlanetoidLayout.FaceAt(x, y, _cloudFaceSize3D);
        }
        else
        {
            x = ThreeDGeometry.WrapTile((int)MathF.Floor(local.X / _cloudCellWidth3D), _cloudColumns3D);
            y = (int)MathF.Floor((local.Z - CloudWallInset3D) / _cloudCellHeight3D);
            if (y < 0 || y >= _cloudRows3D) return;
        }
        var reach = (int)MathF.Ceiling(radius / Math.Min(_cloudCellWidth3D, _cloudCellHeight3D));
        for (var dy = -reach; dy <= reach; dy++)
        for (var dx = -reach; dx <= reach; dx++)
        {
            if (dx * dx * _cloudCellWidth3D * _cloudCellWidth3D +
                dy * dy * _cloudCellHeight3D * _cloudCellHeight3D > radius * radius) continue;
            var cell = CloudNeighbor3D(x, y, face, dx, dy);
            if (cell.Face < 0 && (cell.Y < 0 || cell.Y >= _cloudRows3D)) continue;
            _cloudPerforations3D.Punch(cell);
        }
    }

    private (int X, int Y, int Face) CloudPatchKey3D(int x, int y, int face)
    {
        if (face >= 0)
        {
            x -= face % 3 * _cloudFaceSize3D;
            y -= face / 3 * _cloudFaceSize3D;
        }
        return (x / CloudPatchCells3D, y / CloudPatchCells3D, face);
    }

    private void InvalidatePuncturedCloudPatches3D()
    {
        foreach (var cell in _cloudPerforations3D.DirtyCells)
        {
            Dirty(cell);
            Dirty(CloudNeighbor3D(cell.X, cell.Y, cell.Face, -1, 0));
            Dirty(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 1, 0));
            Dirty(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 0, -1));
            Dirty(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 0, 1));
        }
        _cloudPerforations3D.DirtyCells.Clear();

        void Dirty((int X, int Y, int Face) cell)
        {
            if (cell.Face < 0 && (cell.Y < 0 || cell.Y >= _cloudRows3D)) return;
            if (_cloudPatchLookup3D.TryGetValue(CloudPatchKey3D(cell.X, cell.Y, cell.Face), out var patch))
                patch.HolesDirty = true;
        }
    }
}
