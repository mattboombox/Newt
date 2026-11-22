using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private const float CloudDriftTilesPerSecond = 0.8f;
    private const float CloudWallClearance3D = 0.25f;
    private const float CloudThickness3D = 0.18f;

    private const float NexusCloudElevation3D = SimulationWorld.MaximumSeaLevel + 0.10f;
    private const float NexusCloudWallGap3D = 0.02f;
    private const float CloudCellSize3D = 0.65f;
    private const int CloudPatchCells3D = 64;
    private const int CloudNoiseStride3D = 4;
    private const int CloudWeatherStepSeconds3D = 3;
    private const double CloudRebuildBudgetMs3D = 4;

    private static readonly Color CloudTop3D = new(255, 255, 255, 90);
    private static readonly Color CloudBottom3D = new(145, 145, 145, 90);
    private static readonly Color CloudNorth3D = new(173, 173, 173, 90);
    private static readonly Color CloudEast3D = new(191, 191, 191, 90);
    private static readonly Color CloudSouth3D = new(204, 204, 204, 90);
    private static readonly Color CloudWest3D = new(153, 153, 153, 90);

    [Flags]
    private enum CloudEdges3D { None = 0, North = 1, East = 2, South = 4, West = 8 }

    private readonly record struct NexusCloudCell3D(int X, int Y, CloudEdges3D Edges);

    private sealed record CloudFade3D(float From, bool Wet, double ChangedAt, uint WeatherSeed)
    {
        public float At(double seconds) => WeatherVisuals.CloudWetness(From, Wet, seconds - ChangedAt);
    }
    private readonly record struct CloudFaceShade3D(int Vertex, int X, int Y, Color White);

    private sealed class CloudPatch3D(Rectangle cells, int face)
    {
        public Rectangle Cells { get; } = cells;
        public int Face { get; } = face;
        public long WeatherStep { get; set; } = -1;
        public VertexPositionColor[] Vertices { get; set; } = [];
        public CloudFaceShade3D[] Shades { get; set; } = [];
        public double ShadedAt { get; set; } = -1;
        public NexusCloudCell3D[] NexusCells { get; set; } = [];
        public DynamicVertexBuffer? Buffer { get; set; }
        public long UploadedWeatherStep { get; set; } = -1;
        public bool HolesDirty { get; set; }
    }

    private readonly List<CloudPatch3D> _clouds3D = [];
    private readonly Dictionary<(int X, int Y, int Face), float> _cloudNoiseSamples3D = [];
    private readonly Dictionary<(int X, int Y, int Face), float> _cloudMoistureSamples3D = [];
    private readonly List<VertexPositionColor> _cloudMeshScratch3D = [];
    private readonly List<NexusCloudCell3D> _nexusCloudCellsScratch3D = [];
    private readonly List<CloudFaceShade3D> _cloudFaceShadesScratch3D = [];
    private VertexPositionColor[] _cloudClippedVertices3D = [];
    private bool[] _nexusCloudFootprint3D = [];
    private DepthStencilState? _cloudDepthStencil3D;
    private BlendState? _cloudDepthOnlyBlend3D;
    private int _cloudColumns3D;
    private int _cloudRows3D;
    private int _cloudFaceSize3D;
    private float _cloudCellWidth3D;
    private float _cloudCellHeight3D;
    private float _cloudNoiseScale3D;
    private long _cloudWeatherStep3D = -1;
    private readonly Dictionary<(int X, int Y, int Face), int> _cloudBankSizes3D = [];
    private readonly List<(int X, int Y, int Face)> _cloudBankScratch3D = [];
    private readonly Dictionary<uint, int> _cloudBankLineagesScratch3D = [];

    private Dictionary<(int X, int Y, int Face), CloudFade3D> _cloudFades3D = [];
    private Dictionary<(int X, int Y, int Face), CloudFade3D> _previousCloudFades3D = [];
    private double _cloudColorTime3D;

    private void ResetClouds3D()
    {
        _weatherDrops3D.Clear();
        _weatherStreams3D.Clear();
        _nextWeatherCleanup3D = 0;
        _weatherSourceStep3D = -1;
        _weatherSourceCells3D.Clear();
        ReleaseCloudPatches3D();
        _cloudPerforations3D.Clear();
        _cloudNoiseSamples3D.Clear();
        _cloudMoistureSamples3D.Clear();
        _cloudBankSizes3D.Clear();
        _cloudWeatherStep3D = -1;
        _cloudFades3D.Clear();
        _previousCloudFades3D.Clear();
        if (_world.Body is WorldBody.Nexus)
        {
            _nexusCloudFootprint3D = new bool[_world.Width * _world.Height];
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
            {
                var tile = new GridPosition(x, y);
                _nexusCloudFootprint3D[y * _world.Width + x] =
                    NexusLayout.Contains(_world, tile) && _world.GetTerrain(tile) is not Terrain.MegaWall;
            }
        }
        if (IsPlanetoid3D)
        {
            var size = PlanetoidLayout.FaceSize(_world.Width);
            // Match the sample spacing even when neighboring cube edges run in opposite directions.
            _cloudFaceSize3D = (int)MathF.Ceiling(size / (CloudCellSize3D * CloudNoiseStride3D)) *
                CloudNoiseStride3D;
            _cloudCellWidth3D = _cloudCellHeight3D = size / (float)_cloudFaceSize3D;
            for (var face = 0; face < 6; face++)
                AddPatches(new Rectangle(face % 3 * _cloudFaceSize3D, face / 3 * _cloudFaceSize3D,
                    _cloudFaceSize3D, _cloudFaceSize3D), face);
            _cloudNoiseScale3D = Math.Clamp(size / 4f, 13f, 65f);
        }
        else
        {
            _cloudColumns3D = (int)MathF.Ceiling(_world.Width / CloudCellSize3D);
            var height = _world.Height - 2 * CloudWallInset3D;
            _cloudRows3D = (int)MathF.Ceiling(height / CloudCellSize3D);
            if (_cloudRows3D <= 0) return;
            _cloudCellWidth3D = _world.Width / (float)_cloudColumns3D;
            _cloudCellHeight3D = height / _cloudRows3D;
            AddPatches(new Rectangle(0, 0, _cloudColumns3D, _cloudRows3D), -1);
            _cloudNoiseScale3D = Math.Clamp(MathF.Sqrt(_world.Width * height) / 8f, 13f, 65f);
        }

        void AddPatches(Rectangle region, int face)
        {
            for (var y = region.Top; y < region.Bottom; y += CloudPatchCells3D)
            for (var x = region.Left; x < region.Right; x += CloudPatchCells3D)
            {
                var patch = new CloudPatch3D(new Rectangle(x, y, Math.Min(CloudPatchCells3D, region.Right - x),
                    Math.Min(CloudPatchCells3D, region.Bottom - y)), face);
                _clouds3D.Add(patch);
                _cloudPatchLookup3D[CloudPatchKey3D(x, y, face)] = patch;
            }
        }
    }

    private void ReleaseCloudPatches3D()
    {
        foreach (var patch in _clouds3D)
            patch.Buffer?.Dispose();
        _clouds3D.Clear();
        _cloudPatchLookup3D.Clear();
        _nexusCloudFootprint3D = [];
    }

    // The outermost tile on each side of a ring belongs to the Mega Wall.
    private float CloudWallInset3D => IsRing3D ? 1f : 0f;
    private float CloudBaseHeight3D => ThreeDGeometry.ColumnHeight(_world.Body is WorldBody.Nexus
        ? NexusCloudElevation3D : SimulationWorld.MegaWallElevation) -
        (_world.Body is WorldBody.Nexus ? 0 : CloudWallClearance3D) - CloudThickness3D;

    private float CloudDensity3D(int x, int y, int face) => CloudField3D(x, y, face, moisture: false);
    private float CloudMoisture3D(int x, int y, int face) => CloudField3D(x, y, face, moisture: true);

    private float CloudField3D(int x, int y, int face, bool moisture)
    {
        var faceX = face < 0 ? 0 : face % 3 * _cloudFaceSize3D;
        var faceY = face < 0 ? 0 : face / 3 * _cloudFaceSize3D;
        var x0 = faceX + (x - faceX) / CloudNoiseStride3D * CloudNoiseStride3D;
        var y0 = faceY + (y - faceY) / CloudNoiseStride3D * CloudNoiseStride3D;
        var x1 = Math.Min(x0 + CloudNoiseStride3D, face < 0 ? _cloudColumns3D : faceX + _cloudFaceSize3D);
        var y1 = Math.Min(y0 + CloudNoiseStride3D, face < 0 ? _cloudRows3D : faceY + _cloudFaceSize3D);
        var tx = x1 == x0 ? 0 : (x + 0.5f - x0) / (x1 - x0);
        var ty = y1 == y0 ? 0 : (y + 0.5f - y0) / (y1 - y0);
        return MathHelper.Lerp(
            MathHelper.Lerp(Sample(x0, y0), Sample(x1, y0), tx),
            MathHelper.Lerp(Sample(x0, y1), Sample(x1, y1), tx), ty);

        float Sample(int sx, int sy)
        {
            var density = CloudNoiseSample3D(sx, sy, face);
            if (face < 0 && sx == _cloudColumns3D) sx = 0;
            return moisture ? _cloudMoistureSamples3D[(sx, sy, face)] : density;
        }
    }

    private float CloudNoiseSample3D(int x, int y, int face)
    {
        if (face < 0 && x == _cloudColumns3D) x = 0;
        var key = (x, y, face);
        if (_cloudNoiseSamples3D.TryGetValue(key, out var cached)) return cached;

        Vector3 point;
        if (face >= 0)
        {
            point = Framework(PlanetoidLayout.Direction(x, y, _cloudFaceSize3D, face)) *
                PlanetoidGeometry.Radius(_world.Width);
        }
        else
        {
            var (sin, cos) = MathF.SinCos(MathHelper.TwoPi * x / _cloudColumns3D);
            var radius = _world.Width / MathHelper.TwoPi;
            point = new Vector3(cos * radius, sin * radius, y * _cloudCellHeight3D + CloudWallInset3D);
        }

        // Move through the shared noise field slowly so the drifting cover can form and break up.
        var weatherTime = _cloudWeatherStep3D * CloudWeatherStepSeconds3D;
        point = point / _cloudNoiseScale3D + weatherTime * new Vector3(0.003f, 0.008f, 0.005f);
        var warp = new Vector3(CloudValueNoise3D(point + new Vector3(17)),
            CloudValueNoise3D(point + new Vector3(41)), CloudValueNoise3D(point + new Vector3(67)));
        point += warp * 0.45f;
        var density = 0.60f * CloudValueNoise3D(point) +
            0.29f * CloudValueNoise3D(point * 2.1f + new Vector3(13)) +
            0.11f * CloudValueNoise3D(point * 4.3f + new Vector3(29));
        _cloudNoiseSamples3D.Add(key, density);
        _cloudMoistureSamples3D.Add(key, CloudValueNoise3D(point * 0.8f + new Vector3(113, 47, 89)));
        return density;
    }

    private float CloudValueNoise3D(Vector3 point)
    {
        var x = (int)MathF.Floor(point.X);
        var y = (int)MathF.Floor(point.Y);
        var z = (int)MathF.Floor(point.Z);
        var tx = Fade(point.X - x);
        var ty = Fade(point.Y - y);
        var tz = Fade(point.Z - z);
        return MathHelper.Lerp(Plane(z), Plane(z + 1), tz);

        static float Fade(float t) => t * t * (3 - 2 * t);

        float Plane(int layer) => MathHelper.Lerp(
            MathHelper.Lerp(Hash(x, y, layer), Hash(x + 1, y, layer), tx),
            MathHelper.Lerp(Hash(x, y + 1, layer), Hash(x + 1, y + 1, layer), tx), ty);

        float Hash(int ix, int iy, int iz)
        {
            unchecked
            {
                var hash = _seed ^ ((ulong)(uint)ix * 0x9E3779B185EBCA87UL) ^
                    ((ulong)(uint)iy * 0xC2B2AE3D27D4EB4FUL) ^ ((ulong)(uint)iz * 0xA24BAED4963EE407UL);
                hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
                hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
                return ((hash ^ (hash >> 31)) >> 40) * (2f / (1 << 24)) - 1f;
            }
        }
    }

    private (int X, int Y, int Face) CloudNeighbor3D(int x, int y, int face, int dx, int dy)
    {
        if (face < 0) return (ThreeDGeometry.WrapTile(x + dx, _cloudColumns3D), y + dy, -1);
        var tile = PlanetoidLayout.Offset(new GridPosition(x, y), dx, dy, _cloudFaceSize3D);
        return (tile.X, tile.Y, PlanetoidLayout.FaceAt(tile.X, tile.Y, _cloudFaceSize3D));
    }

    private int CloudBankSize3D(int x, int y, int face)
    {
        var start = (X: x, Y: y, Face: face);
        if (_cloudBankSizes3D.TryGetValue(start, out var size)) return size;
        _cloudBankScratch3D.Clear();
        Visit(start);
        // Flood across mesh patches, wrapped map edges, and cube-face seams.
        for (var i = 0; i < _cloudBankScratch3D.Count; i++)
        {
            var cell = _cloudBankScratch3D[i];
            Visit(CloudNeighbor3D(cell.X, cell.Y, cell.Face, -1, 0));
            Visit(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 1, 0));
            Visit(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 0, -1));
            Visit(CloudNeighbor3D(cell.X, cell.Y, cell.Face, 0, 1));
        }
        size = _cloudBankScratch3D.Count;
        _cloudBankLineagesScratch3D.Clear();
        var anchor = start;
        uint dominantSeed = 0;
        var dominantCount = 0;
        // Keep the previous bank's fade when its state is unchanged. New/split/merged
        // banks inherit one uniform color from their overlapping cells.
        CloudFade3D? inherited = null;
        var overlap = 0;
        double totalWetness = 0;
        var uniform = true;
        foreach (var cell in _cloudBankScratch3D)
        {
            if (cell.Face < anchor.Face || cell.Face == anchor.Face &&
                (cell.Y < anchor.Y || cell.Y == anchor.Y && cell.X < anchor.X)) anchor = cell;
            if (!_previousCloudFades3D.TryGetValue(cell, out var previous)) continue;
            _cloudBankLineagesScratch3D.TryGetValue(previous.WeatherSeed, out var count);
            _cloudBankLineagesScratch3D[previous.WeatherSeed] = ++count;
            if (count > dominantCount)
            {
                dominantSeed = previous.WeatherSeed;
                dominantCount = count;
            }
            if (inherited is null) inherited = previous;
            else if (!ReferenceEquals(inherited, previous)) uniform = false;
            totalWetness += previous.At(_cloudColorTime3D);
            overlap++;
        }
        // Preserve weather identity as banks grow, split, or merge rather than
        // rerolling rain eligibility at each three-second weather sample.
        var weatherSeed = dominantCount > 0 ? dominantSeed
            : WeatherVisuals.CellSeed(anchor.X, anchor.Y, anchor.Face) ^ (uint)_seed ^ (uint)(_seed >> 32);
        var wet = WeatherVisuals.CloudBank(size, WeatherVisuals.CloudBankCanRain(weatherSeed)) is CloudKind.Moist;
        var fade = uniform && inherited is not null && inherited.Wet == wet ? inherited
            : new CloudFade3D(overlap > 0 ? (float)(totalWetness / overlap) : 0, wet, _cloudColorTime3D, weatherSeed);
        foreach (var cell in _cloudBankScratch3D)
        {
            _cloudBankSizes3D[cell] = size;
            _cloudFades3D[cell] = fade;
        }
        return size;

        void Visit((int X, int Y, int Face) cell)
        {
            if (cell.Face < 0 && (cell.Y < 0 || cell.Y >= _cloudRows3D)) return;
            if (!_cloudBankSizes3D.TryAdd(cell, 0)) return;
            if (WeatherVisuals.HasCloud(CloudDensity3D(cell.X, cell.Y, cell.Face),
                CloudMoisture3D(cell.X, cell.Y, cell.Face))) _cloudBankScratch3D.Add(cell);
        }
    }

    private float CloudWetness3D(int x, int y, int face)
    {
        CloudBankSize3D(x, y, face);
        return _cloudFades3D.TryGetValue((x, y, face), out var fade) ? fade.At(_cloudColorTime3D) : 0;
    }

    private CloudKind CloudKind3D(int x, int y, int face, int dx = 0, int dy = 0)
    {
        var cell = CloudNeighbor3D(x, y, face, dx, dy);
        if (cell.Face < 0 && (cell.Y < 0 || cell.Y >= _cloudRows3D)) return CloudKind.None;
        if (_cloudPerforations3D.Contains(cell)) return CloudKind.None;
        var size = CloudBankSize3D(cell.X, cell.Y, cell.Face);
        return WeatherVisuals.CloudBank(size, _cloudFades3D.TryGetValue(cell, out var fade) && fade.Wet);
    }
    private void BuildCloudPatch3D(CloudPatch3D patch, float height, bool isNexus)
    {
        _cloudMeshScratch3D.Clear();
        _nexusCloudCellsScratch3D.Clear();
        _cloudFaceShadesScratch3D.Clear();
        var cells = patch.Cells;
        var low = height - CloudThickness3D;
        // Cache the one-cell border too, so shared sides are emitted only at exposed edges.
        var stride = cells.Width + 2;
        Span<CloudKind> occupied = stackalloc CloudKind[stride * (cells.Height + 2)];
        for (var y = -1; y <= cells.Height; y++)
        for (var x = -1; x <= cells.Width; x++)
        {
            if ((x < 0 || x == cells.Width) && (y < 0 || y == cells.Height)) continue;
            var localX = Math.Clamp(x, 0, cells.Width - 1);
            var localY = Math.Clamp(y, 0, cells.Height - 1);
            occupied[(y + 1) * stride + x + 1] = CloudKind3D(cells.X + localX, cells.Y + localY,
                patch.Face, x - localX, y - localY);
        }

        for (var y = 0; y < cells.Height; y++)
        for (var x = 0; x < cells.Width; x++)
        {
            var index = (y + 1) * stride + x + 1;
            var kind = occupied[index];
            if (kind is CloudKind.None) continue;
            var left = x + cells.X;
            var top = y + cells.Y;

            var edges = CloudEdges3D.None;
            if (occupied[index - stride] is CloudKind.None) edges |= CloudEdges3D.North;
            if (occupied[index + 1] is CloudKind.None) edges |= CloudEdges3D.East;
            if (occupied[index + stride] is CloudKind.None) edges |= CloudEdges3D.South;
            if (occupied[index - 1] is CloudKind.None) edges |= CloudEdges3D.West;
            if (isNexus)
            {
                _nexusCloudCellsScratch3D.Add(new(left, top, edges));
                continue;
            }

            var a = CloudPatchPoint3D(left, top, height, patch.Face);
            var b = CloudPatchPoint3D(left + 1, top, height, patch.Face);
            var c = CloudPatchPoint3D(left + 1, top + 1, height, patch.Face);
            var d = CloudPatchPoint3D(left, top + 1, height, patch.Face);
            var e = CloudPatchPoint3D(left, top, low, patch.Face);
            var f = CloudPatchPoint3D(left + 1, top, low, patch.Face);
            var g = CloudPatchPoint3D(left + 1, top + 1, low, patch.Face);
            var h = CloudPatchPoint3D(left, top + 1, low, patch.Face);
            var firstVertex = _cloudMeshScratch3D.Count;
            AddCloudSlab3D(a, b, c, d, e, f, g, h, edges);
            for (var vertex = firstVertex; vertex < _cloudMeshScratch3D.Count; vertex += 6)
                _cloudFaceShadesScratch3D.Add(new(vertex, left, top, _cloudMeshScratch3D[vertex].Color));
        }

        if (isNexus)
            patch.NexusCells = _nexusCloudCellsScratch3D.ToArray();
        else
            patch.Vertices = _cloudMeshScratch3D.ToArray();
        patch.Shades = _cloudFaceShadesScratch3D.ToArray();
        patch.ShadedAt = -1;
        patch.WeatherStep = _cloudWeatherStep3D;
        patch.UploadedWeatherStep = -1;
        patch.HolesDirty = false;
    }

    private Vector3 CloudPatchPoint3D(float x, float y, float height, int face)
    {
        if (face >= 0)
            return Framework(PlanetoidLayout.Direction(x, y, _cloudFaceSize3D, face)) *
                (PlanetoidGeometry.Radius(_world.Width) + height);
        var point = new Vector3(x * _cloudCellWidth3D, height, y * _cloudCellHeight3D + CloudWallInset3D);
        return IsRing3D ? WorldPoint3D(point) : point;
    }

    private void DrawClouds3D(double elapsedSeconds, BoundingFrustum frustum, Rectangle bounds, float daylight,
        Matrix view, Matrix projection)
    {
        _cloudColorTime3D = elapsedSeconds;
        InvalidatePuncturedCloudPatches3D();
        _precipitationEmitters3D.Clear();
        var isNexus = _world.Body is WorldBody.Nexus;
        var curved = IsPlanetoid3D || IsRing3D;
        var weatherStep = (long)(elapsedSeconds / CloudWeatherStepSeconds3D);
        if (weatherStep != _cloudWeatherStep3D)
        {
            _cloudWeatherStep3D = weatherStep;
            _cloudNoiseSamples3D.Clear();
            _cloudMoistureSamples3D.Clear();
            _cloudBankSizes3D.Clear();
            (_previousCloudFades3D, _cloudFades3D) = (_cloudFades3D, _previousCloudFades3D);
            _cloudFades3D.Clear();
        }

        var height = CloudBaseHeight3D + CloudThickness3D;
        var circumference = IsPlanetoid3D ? MathHelper.TwoPi * PlanetoidGeometry.Radius(_world.Width) : _world.Width;
        var drift = (float)(elapsedSeconds * CloudDriftTilesPerSecond % circumference);
        var angle = MathHelper.TwoPi * drift / circumference;
        var rotation = Matrix.Identity;
        if (IsPlanetoid3D)
            rotation = Matrix.CreateRotationY(angle);
        else if (IsRing3D)
        {
            var axis = new Vector3(0, RingGeometry.Radius(_world.Width), 0);
            rotation = Matrix.CreateTranslation(-axis) * Matrix.CreateRotationZ(angle) * Matrix.CreateTranslation(axis);
        }
        CollectPrecipitationEmitters3D(height - CloudThickness3D, rotation, drift, view);

        // First find the nearest cloud surface without writing color. The color pass
        // then blends that surface exactly once, independent of patch or face order.
        _cloudDepthOnlyBlend3D ??= new BlendState { ColorWriteChannels = ColorWriteChannels.None };
        _cloudDepthStencil3D ??= new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false,
            DepthBufferFunction = CompareFunction.Equal,
            StencilEnable = true,
            StencilMask = 1,
            StencilWriteMask = 1,
            ReferenceStencil = 1,
            StencilFunction = CompareFunction.NotEqual,
            StencilPass = StencilOperation.Replace
        };
        GraphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        var effect = TerrainSceneEffect3D;
        if (effect is BasicEffect basic)
            basic.DiffuseColor = new Vector3(daylight * daylight);
        else
            effect.Parameters["CloudLighting"].SetValue(1f);

        var cameraTileX = (int)LocalPoint3D(Matrix.Invert(view).Translation).X;
        var clipLeft = isNexus ? 0 : bounds.IsEmpty ? cameraTileX - 48 : bounds.Left;
        var clipRight = isNexus ? _world.Width : bounds.IsEmpty ? cameraTileX + 48 : bounds.Right;
        var cloudRebuildTimeMs = 0.0;
        var depthOnly = true;
        for (var cloudPass = 0; cloudPass < 2; cloudPass++)
        {
        depthOnly = cloudPass == 0;
        GraphicsDevice.BlendState = depthOnly ? _cloudDepthOnlyBlend3D : BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = depthOnly ? DepthStencilState.Default : _cloudDepthStencil3D;
        foreach (var patch in _clouds3D)
        {
            var cells = patch.Cells;
            var midpoint = CloudPatchPoint3D(cells.X + cells.Width / 2f, cells.Y + cells.Height / 2f, height, patch.Face);
            var radius = MathF.Sqrt(MathF.Pow(cells.Width * _cloudCellWidth3D / 2, 2) +
                MathF.Pow(cells.Height * _cloudCellHeight3D / 2, 2));
            if (curved)
            {
                if (IsPlanetoid3D) radius *= 1.15f * (1 + height / PlanetoidGeometry.Radius(_world.Width));
                if (frustum.Intersects(new BoundingSphere(Vector3.Transform(midpoint, rotation), radius + 1)))
                    DrawPatch(patch, rotation);
                continue;
            }

            var left = cells.Left * _cloudCellWidth3D;
            var right = cells.Right * _cloudCellWidth3D;
            var firstCopy = (int)MathF.Floor((clipLeft - right - drift) / _world.Width) + 1;
            for (var copy = firstCopy; left + drift + copy * _world.Width < clipRight; copy++)
            {
                var shift = drift + copy * _world.Width;
                var center = midpoint + new Vector3(shift, 0, 0);
                if (isNexus) center.Y += NexusLift3D(center.X, center.Z);
                if (!frustum.Intersects(new BoundingSphere(center, radius * (isNexus ? 2 : 1) + 1))) continue;
                if (isNexus || left + shift < clipLeft || right + shift > clipRight)
                    DrawPatch(patch, Matrix.Identity, shift);
                else
                    DrawPatch(patch, Matrix.CreateTranslation(shift, 0, 0));
            }
        }
        }

        SetTerrainWorld3D(effect, Matrix.Identity);
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        DrawPrecipitation3D(elapsedSeconds, height - CloudThickness3D, daylight, view, projection);

        void DrawPatch(CloudPatch3D patch, Matrix transform, float? clippedShift = null)
        {
            // Keep drawing the last mesh while visible patches catch up with the new weather sample.
            if (depthOnly && (patch.HolesDirty ||
                patch.WeatherStep != _cloudWeatherStep3D && cloudRebuildTimeMs < CloudRebuildBudgetMs3D))
            {
                var start = Stopwatch.GetTimestamp();
                BuildCloudPatch3D(patch, height, isNexus);
                cloudRebuildTimeMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            // Refresh color every frame independently of the three-second geometry samples.
            if (patch.ShadedAt != elapsedSeconds)
            {
                var changed = false;
                foreach (var shade in patch.Shades)
                {
                    var wetness = CloudWetness3D(shade.X, shade.Y, patch.Face);
                    var color = Color.Lerp(shade.White, new Color(52, 63, 80, 210), 0.82f * wetness);
                    if (patch.Vertices[shade.Vertex].Color == color) continue;
                    for (var vertex = shade.Vertex; vertex < shade.Vertex + 6; vertex++)
                        patch.Vertices[vertex].Color = color;
                    changed = true;
                }
                if (changed) patch.UploadedWeatherStep = -1;
                patch.ShadedAt = elapsedSeconds;
            }
            var vertices = patch.Vertices;
            var count = vertices.Length;
            if (clippedShift is { } nexusShift && isNexus)
            {
                count = ClipNexusCloudPatch3D(patch.NexusCells, nexusShift, height);
                vertices = _cloudClippedVertices3D;
            }
            else if (count == 0) return;
            else if (clippedShift is { } shift)
            {
                if (_cloudClippedVertices3D.Length < count) _cloudClippedVertices3D = new VertexPositionColor[count];
                count = 0;
                for (var i = 0; i < vertices.Length; i += 6)
                {
                    var x0 = vertices[i].Position.X;
                    var x1 = vertices[i + 1].Position.X;
                    var x2 = vertices[i + 2].Position.X;
                    var x3 = vertices[i + 5].Position.X;
                    if (Math.Max(Math.Max(x0, x1), Math.Max(x2, x3)) + shift < clipLeft ||
                        Math.Min(Math.Min(x0, x1), Math.Min(x2, x3)) + shift > clipRight)
                        continue;

                    for (var j = i; j < i + 6; j++)
                    {
                        var vertex = vertices[j];
                        vertex.Position.X = Math.Clamp(vertex.Position.X + shift, clipLeft, clipRight);
                        _cloudClippedVertices3D[count++] = vertex;
                    }
                }
                if (count == 0) return;
                vertices = _cloudClippedVertices3D;
            }
            else
            {
                if (patch.UploadedWeatherStep != patch.WeatherStep)
                {
                    GraphicsDevice.SetVertexBuffer(null);
                    if (patch.Buffer is null || patch.Buffer.VertexCount < count)
                    {
                        patch.Buffer?.Dispose();
                        patch.Buffer = new DynamicVertexBuffer(GraphicsDevice, typeof(VertexPositionColor),
                            count, BufferUsage.WriteOnly);
                    }
                    patch.Buffer.SetData(vertices, 0, count, SetDataOptions.Discard);
                    patch.UploadedWeatherStep = patch.WeatherStep;
                }
                GraphicsDevice.SetVertexBuffer(patch.Buffer);
            }

            if (count == 0) return;
            SetTerrainWorld3D(effect, transform);
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                if (clippedShift.HasValue)
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, count / 3);
                else
                    GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, count / 3);
            }
        }
    }

    private int ClipNexusCloudPatch3D(NexusCloudCell3D[] cells, float shift, float height)
    {
        _cloudMeshScratch3D.Clear();
        foreach (var cell in cells)
        {
            var left = Math.Max(0, cell.X * _cloudCellWidth3D + shift);
            var right = Math.Min(_world.Width, (cell.X + 1) * _cloudCellWidth3D + shift);
            if (left >= right) continue;
            var top = Math.Max(0, cell.Y * _cloudCellHeight3D);
            var bottom = Math.Min(_world.Height, (cell.Y + 1) * _cloudCellHeight3D);
            if (top >= bottom) continue;

            var firstX = (int)MathF.Floor(left);
            var lastX = (int)MathF.Ceiling(right) - 1;
            var firstY = (int)MathF.Floor(top);
            var lastY = (int)MathF.Ceiling(bottom) - 1;
            // Cells touch at most four map tiles. Use one slab in the interior.
            if (left > firstX && right < lastX + 1 && top > firstY && bottom < lastY + 1 &&
                Inside(firstX, firstY) && Inside(lastX, firstY) && Inside(firstX, lastY) && Inside(lastX, lastY))
            {
                AddSlab(left, top, right, bottom, cell.Edges, CloudWetness3D(cell.X, cell.Y, -1));
                continue;
            }

            for (var y = firstY; y <= lastY; y++)
            for (var x = firstX; x <= lastX; x++)
            {
                if (!Inside(x, y)) continue;
                var west = Math.Max(x, left);
                var east = Math.Min(x + 1, right);
                var north = Math.Max(y, top);
                var south = Math.Min(y + 1, bottom);
                var edges = CloudEdges3D.None;
                if (north == top) edges |= cell.Edges & CloudEdges3D.North;
                if (east == right) edges |= cell.Edges & CloudEdges3D.East;
                if (south == bottom) edges |= cell.Edges & CloudEdges3D.South;
                if (west == left) edges |= cell.Edges & CloudEdges3D.West;
                // Pull cut faces off the wall plane so the two meshes cannot overlap.
                if (north == y && !Inside(x, y - 1))
                {
                    north += NexusCloudWallGap3D;
                    edges |= CloudEdges3D.North;
                }
                if (east == x + 1 && !Inside(x + 1, y))
                {
                    east -= NexusCloudWallGap3D;
                    edges |= CloudEdges3D.East;
                }
                if (south == y + 1 && !Inside(x, y + 1))
                {
                    south -= NexusCloudWallGap3D;
                    edges |= CloudEdges3D.South;
                }
                if (west == x && !Inside(x - 1, y))
                {
                    west += NexusCloudWallGap3D;
                    edges |= CloudEdges3D.West;
                }
                if (west < east && north < south)
                    AddSlab(west, north, east, south, edges, CloudWetness3D(cell.X, cell.Y, -1));
            }
        }
        var count = _cloudMeshScratch3D.Count;
        if (_cloudClippedVertices3D.Length < count)
            Array.Resize(ref _cloudClippedVertices3D, Math.Max(count, _cloudClippedVertices3D.Length * 2));
        _cloudMeshScratch3D.CopyTo(_cloudClippedVertices3D);
        return count;

        bool Inside(int x, int y) => (uint)x < (uint)_world.Width && (uint)y < (uint)_world.Height &&
            _nexusCloudFootprint3D[y * _world.Width + x];

        void AddSlab(float left, float top, float right, float bottom, CloudEdges3D edges, float wetness)
        {
            var a = PointAtTop(left, top);
            var b = PointAtTop(right, top);
            var c = PointAtTop(right, bottom);
            var d = PointAtTop(left, bottom);
            var e = PointAtBottom(left, top);
            var f = PointAtBottom(right, top);
            var g = PointAtBottom(right, bottom);
            var h = PointAtBottom(left, bottom);
            AddCloudSlab3D(a, b, c, d, e, f, g, h, edges, wetness);

            Vector3 PointAtTop(float x, float z) => new(x,
                height + NexusLift3D(x, z), z);
            Vector3 PointAtBottom(float x, float z) => new(x, height - CloudThickness3D + NexusLift3D(x, z), z);
        }
    }

    private void AddCloudSlab3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 e, Vector3 f, Vector3 g, Vector3 h, CloudEdges3D edges, float wetness = 0)
    {
        Color Shade(Color color) => Color.Lerp(color, new Color(52, 63, 80, 210), 0.82f * wetness);
        AddCloudFace3D(a, b, c, d, Shade(CloudTop3D));
        AddCloudFace3D(e, h, g, f, Shade(CloudBottom3D));
        if ((edges & CloudEdges3D.North) != 0) AddCloudFace3D(e, f, b, a, Shade(CloudNorth3D));
        if ((edges & CloudEdges3D.East) != 0) AddCloudFace3D(f, g, c, b, Shade(CloudEast3D));
        if ((edges & CloudEdges3D.South) != 0) AddCloudFace3D(g, h, d, c, Shade(CloudSouth3D));
        if ((edges & CloudEdges3D.West) != 0) AddCloudFace3D(h, e, a, d, Shade(CloudWest3D));
    }

    private void AddCloudFace3D(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        _cloudMeshScratch3D.Add(new(a, color));
        _cloudMeshScratch3D.Add(new(b, color));
        _cloudMeshScratch3D.Add(new(c, color));
        _cloudMeshScratch3D.Add(new(a, color));
        _cloudMeshScratch3D.Add(new(c, color));
        _cloudMeshScratch3D.Add(new(d, color));
    }
}
