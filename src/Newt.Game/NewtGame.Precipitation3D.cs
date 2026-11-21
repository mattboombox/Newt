using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private readonly List<(Vector3 Source, uint Seed)> _precipitationEmitters3D = [];
    private readonly List<VertexPositionColor> _precipitationVertices3D = [];
    private VertexPositionColor[] _precipitationBuffer3D = [];
    private sealed class WeatherStream3D
    {
        public double LastSeen;
        public PrecipitationKind Kind;
        public double[] NextBirth = new double[6];
        public uint Sequence;
    }
    private readonly record struct WeatherDrop3D(Vector3 Source, Vector3 Ground, Vector3 Up, Vector3 East,
        PrecipitationKind Kind, double BornAt, uint Seed);
    private readonly Dictionary<uint, WeatherStream3D> _weatherStreams3D = [];
    private readonly List<WeatherDrop3D> _weatherDrops3D = [];
    private readonly List<uint> _expiredWeatherStreams3D = [];
    private double _nextWeatherCleanup3D;
    private readonly record struct WeatherSourceCell3D(int X, int Y, int Face, int Copy);
    private readonly List<WeatherSourceCell3D> _weatherSourceCells3D = [];
    private readonly PriorityQueue<WeatherSourceCell3D, (float Distance, uint Tie)> _weatherSourceCandidates3D = new();
    private long _weatherSourceStep3D = -1;
    private Vector3 _weatherSourceAnchor3D;
    private float _weatherSourceDrift3D;

    private void CollectPrecipitationEmitters3D(float height, Matrix rotation, float drift, Matrix view)
    {
        const float range = 48;
        const int maximumSources = 1600;
        var localCamera = LocalPoint3D(Matrix.Invert(view).Translation);
        var anchor = WorldPoint3D(new(localCamera.X, height, localCamera.Z));
        var curved = IsPlanetoid3D || IsRing3D;
        // Refresh a region around the camera's position, never its viewing direction.
        // Rain sources do not depend on cloud meshes, their rebuild budget, or culling.
        if (_weatherSourceStep3D != _cloudWeatherStep3D || Vector3.DistanceSquared(anchor, _weatherSourceAnchor3D) > 16 ||
            Math.Abs(drift - _weatherSourceDrift3D) > 4)
        {
            _weatherSourceStep3D = _cloudWeatherStep3D;
            _weatherSourceAnchor3D = anchor;
            _weatherSourceDrift3D = drift;
            _weatherSourceCandidates3D.Clear();
            foreach (var patch in _clouds3D)
            {
                var cells = patch.Cells;
                var midpoint = CloudPatchPoint3D(cells.X + cells.Width / 2f, cells.Y + cells.Height / 2f, height, patch.Face);
                var radius = MathF.Sqrt(MathF.Pow(cells.Width * _cloudCellWidth3D / 2, 2) +
                    MathF.Pow(cells.Height * _cloudCellHeight3D / 2, 2));
                if (curved)
                {
                    if (IsPlanetoid3D) radius *= 1.15f * (1 + height / PlanetoidGeometry.Radius(_world.Width));
                    if (Vector3.Distance(anchor, Vector3.Transform(midpoint, rotation)) <= radius + range)
                        AddPatch(patch, 0);
                }
                else
                {
                    var left = cells.Left * _cloudCellWidth3D;
                    var right = cells.Right * _cloudCellWidth3D;
                    var firstCopy = (int)MathF.Floor((anchor.X - range - right - drift) / _world.Width) + 1;
                    for (var copy = firstCopy; left + drift + copy * _world.Width < anchor.X + range; copy++)
                    {
                        var center = midpoint + new Vector3(drift + copy * _world.Width, 0, 0);
                        if (_world.Body is WorldBody.Nexus) center.Y += NexusLift3D(center.X, center.Z);
                        if (Vector3.Distance(anchor, center) <= radius * (_world.Body is WorldBody.Nexus ? 2 : 1) + range)
                            AddPatch(patch, copy);
                    }
                }
            }
            _weatherSourceCells3D.Clear();
            while (_weatherSourceCandidates3D.TryDequeue(out var cell, out _)) _weatherSourceCells3D.Add(cell);
        }
        foreach (var cell in _weatherSourceCells3D)
            _precipitationEmitters3D.Add((Source(cell), WeatherVisuals.CellSeed(cell.X, cell.Y, cell.Face, cell.Copy)));

        void AddPatch(CloudPatch3D patch, int copy)
        {
            for (var y = patch.Cells.Top; y < patch.Cells.Bottom; y++)
            for (var x = patch.Cells.Left; x < patch.Cells.Right; x++)
            {
                var seed = WeatherVisuals.CellSeed(x, y, patch.Face);
                if (seed % 4 != 0) continue;
                var cell = new WeatherSourceCell3D(x, y, patch.Face, copy);
                var source = Source(cell);
                var distance = Vector3.DistanceSquared(source, anchor);
                if (distance > range * range) continue;
                if (_world.Body is WorldBody.Nexus)
                {
                    var local = LocalPoint3D(source);
                    var tileX = (int)MathF.Floor(local.X);
                    var tileY = (int)MathF.Floor(local.Z);
                    if ((uint)tileX >= _world.Width || (uint)tileY >= _world.Height ||
                        !_nexusCloudFootprint3D[tileY * _world.Width + tileX]) continue;
                }
                if (_weatherSourceCandidates3D.Count >= maximumSources &&
                    _weatherSourceCandidates3D.TryPeek(out _, out var farthest) && distance >= -farthest.Distance) continue;
                if (CloudKind3D(x, y, patch.Face) is not CloudKind.Moist) continue;
                _weatherSourceCandidates3D.Enqueue(cell, (-distance, seed));
                if (_weatherSourceCandidates3D.Count > maximumSources) _weatherSourceCandidates3D.Dequeue();
            }
        }

        Vector3 Source(WeatherSourceCell3D cell)
        {
            var seed = WeatherVisuals.CellSeed(cell.X, cell.Y, cell.Face);
            var source = CloudPatchPoint3D(cell.X + 0.12f + 0.76f * WeatherVisuals.RandomFraction(seed),
                cell.Y + 0.12f + 0.76f * WeatherVisuals.RandomFraction(seed + 1), height, cell.Face);
            if (curved) return Vector3.Transform(source, rotation);
            source.X += drift + cell.Copy * _world.Width;
            if (_world.Body is WorldBody.Nexus) source.Y += NexusLift3D(source.X, source.Z);
            return source;
        }
    }

    private void DrawPrecipitation3D(double seconds, float cloudHeight, float daylight, Matrix view, Matrix projection)
    {
        _precipitationVertices3D.Clear();
        var camera = Matrix.Invert(view);
        var viewProjection = view * projection;
        // Clouds only spawn drops. Existing drops keep their trajectory when a cloud
        // thins, drifts over dry ground, changes temperature, or leaves the view.
        for (var i = _weatherDrops3D.Count - 1; i >= 0; i--)
            if (!WeatherVisuals.IsDropAlive(seconds, _weatherDrops3D[i].BornAt, _weatherDrops3D[i].Kind))
            {
                _weatherDrops3D[i] = _weatherDrops3D[^1];
                _weatherDrops3D.RemoveAt(_weatherDrops3D.Count - 1);
            }
        foreach (var (source, seed) in _precipitationEmitters3D)
        {
            var local = LocalPoint3D(source);
            if (_world.Body is WorldBody.Nexus &&
                (local.X < 0 || local.X >= _world.Width || local.Z < 0 || local.Z >= _world.Height)) continue;
            if (local.Z < 0 || local.Z >= _world.Height) continue;
            var tile = MapTile3D((int)MathF.Floor(local.X), (int)MathF.Floor(local.Z));
            if (_world.Body is WorldBody.Nexus && !_nexusCloudFootprint3D[tile.Y * _world.Width + tile.X]) continue;
            var kind = WeatherVisuals.Precipitation(WeatherVisuals.RainCloudDensity,
                _world.GetMoistureBand(tile), _world.GetTemperatureBand(tile));
            if (kind is PrecipitationKind.None) continue;
            var surfaceHeight = Math.Max(TileHeight3D(tile),
                ThreeDGeometry.ColumnHeight(WaterElevation3D(tile) ?? _world.GetElevation(tile))) + 0.025f;
            if (surfaceHeight >= cloudHeight) continue;
            // Use the tile below the drifting cloud, including its rotated cube face.
            var ground = WorldPoint3D(new(local.X, surfaceHeight, local.Z), tile);
            var frame = SurfaceFrame3D(local.X, local.Z);
            if (!_weatherStreams3D.TryGetValue(seed, out var stream))
            {
                stream = new WeatherStream3D { LastSeen = double.NegativeInfinity };
                _weatherStreams3D.Add(seed, stream);
            }
            var lifetime = WeatherVisuals.DropLifetime(kind);
            if (seconds - stream.LastSeen > 0.3 || stream.Kind != kind)
            {
                var prewarm = seconds - stream.LastSeen >= lifetime;
                for (var drop = 0; drop < 6; drop++)
                {
                    if (prewarm)
                    {
                        // Populate the entire fall column when loading an ongoing weather
                        // region, rather than waiting a full snow lifetime to fill it.
                        var bornAt = WeatherVisuals.PrewarmedDropBirth(seconds, kind, seed, drop);
                        var previousSource = PastWeatherSource3D(source, seconds - bornAt);
                        SpawnDrop(previousSource, WeatherVisuals.Hash(seed + ++stream.Sequence), bornAt);
                        stream.NextBirth[drop] = bornAt + lifetime;
                    }
                    else stream.NextBirth[drop] = seconds + WeatherVisuals.RandomFraction(seed + (uint)(drop * 17)) * lifetime;
                }
            }
            stream.LastSeen = seconds;
            stream.Kind = kind;
            for (var drop = 0; drop < 6; drop++)
            {
                if (stream.NextBirth[drop] > seconds || _weatherDrops3D.Count >= 12000) continue;
                var dropSeed = WeatherVisuals.Hash(seed + ++stream.Sequence);
                SpawnDrop(source, dropSeed, seconds);
                stream.NextBirth[drop] = seconds + lifetime * (0.8 + 0.4 * WeatherVisuals.RandomFraction(dropSeed + 2));
            }

            void SpawnDrop(Vector3 emissionSource, uint dropSeed, double bornAt)
            {
                if (_weatherDrops3D.Count >= 12000) return;
                var emissionGround = ground;
                var emissionFrame = frame;
                if (bornAt < seconds)
                {
                    var pastLocal = LocalPoint3D(emissionSource);
                    if (!ContainsTile3D((int)MathF.Floor(pastLocal.X), (int)MathF.Floor(pastLocal.Z))) return;
                    var pastTile = MapTile3D((int)MathF.Floor(pastLocal.X), (int)MathF.Floor(pastLocal.Z));
                    if (_world.GetMoistureBand(pastTile) is MoistureBand.Dry ||
                        _world.Body is WorldBody.Nexus && !_nexusCloudFootprint3D[pastTile.Y * _world.Width + pastTile.X]) return;
                    var pastHeight = Math.Max(TileHeight3D(pastTile),
                        ThreeDGeometry.ColumnHeight(WaterElevation3D(pastTile) ?? _world.GetElevation(pastTile))) + 0.025f;
                    if (pastHeight >= cloudHeight) return;
                    emissionGround = WorldPoint3D(new(pastLocal.X, pastHeight, pastLocal.Z), pastTile);
                    emissionFrame = SurfaceFrame3D(pastLocal.X, pastLocal.Z);
                }
                var offset = emissionFrame.East * ((WeatherVisuals.RandomFraction(dropSeed) - 0.5f) * 0.65f) +
                    emissionFrame.South * ((WeatherVisuals.RandomFraction(dropSeed + 1) - 0.5f) * 0.65f);
                _weatherDrops3D.Add(new(emissionSource + offset, emissionGround + offset,
                    emissionFrame.Up, emissionFrame.East, kind, bornAt, dropSeed));
            }
        }
        if (seconds >= _nextWeatherCleanup3D)
        {
            _nextWeatherCleanup3D = seconds + 3;
            _expiredWeatherStreams3D.Clear();
            foreach (var pair in _weatherStreams3D)
                if (seconds - pair.Value.LastSeen > 12) _expiredWeatherStreams3D.Add(pair.Key);
            foreach (var seed in _expiredWeatherStreams3D) _weatherStreams3D.Remove(seed);
        }
        foreach (var particle in _weatherDrops3D)
        {
                var snow = particle.Kind is PrecipitationKind.Snow;
                var elapsed = seconds - particle.BornAt;
                var age = WeatherVisuals.WaterfallDropAge(elapsed, 0, 0);
                var fall = snow ? WeatherVisuals.SnowFall(elapsed, 0, 0) : WeatherVisuals.WaterfallFall(age);
                var center = Vector3.Lerp(particle.Source, particle.Ground, fall);
                if (snow) center += particle.East * (MathF.Sin((float)elapsed * 0.7f + particle.Seed % 31) * 0.09f);
                var clip = Vector4.Transform(new Vector4(center, 1), viewProjection);
                if (clip.W <= 0 || Math.Abs(clip.X) > clip.W || Math.Abs(clip.Y) > clip.W || clip.Z < 0 || clip.Z > clip.W)
                    continue;
                var pixelSize = 2 * clip.W / (GraphicsDevice.Viewport.Height * projection.M22);
                if (snow)
                {
                    var appearance = WeatherVisuals.SnowAppearance(pixelSize);
                    if (appearance.Opacity <= 0) continue;
                    var right = camera.Right * appearance.CellSize;
                    var up = camera.Up * appearance.CellSize;
                    var color = new Color(239, 246, 255, (int)(230 * appearance.Opacity));
                    Pixel(center, right, up, 0, 0, color);
                    var shape = appearance.Detailed ? particle.Seed % 3 : 0;
                    if (shape == 1)
                    {
                        Pixel(center, right, up, -1, 0, color);
                        Pixel(center, right, up, 1, 0, color);
                        Pixel(center, right, up, 0, -1, color);
                        Pixel(center, right, up, 0, 1, color);
                    }
                    else if (shape == 2)
                    {
                        Pixel(center, right, up, -1, -1, color);
                        Pixel(center, right, up, 1, -1, color);
                        Pixel(center, right, up, -1, 1, color);
                        Pixel(center, right, up, 1, 1, color);
                    }
                }
                else
                {
                    // The same accelerating fall and brief splash as river waterfalls.
                    var splash = age >= 0.73f;
                    var bounce = Math.Clamp((age - 0.73f) / 0.27f, 0, 1);
                    if (splash) center += particle.Up * (MathF.Sin(bounce * MathHelper.Pi) * 0.10f);
                    var right = camera.Right * Math.Max(pixelSize * 0.42f, 0.012f);
                    var up = splash ? camera.Up * Math.Max(pixelSize * 0.42f, 0.018f)
                        : particle.Up * 0.075f;
                    var color = splash ? new Color(215, 240, 251, (int)(180 * (1 - bounce)))
                        : new Color(161, 211, 239, 195);
                    Quad(center - right - up, center + right - up, center + right + up, center - right + up, color);
                }
        }
        if (_precipitationVertices3D.Count == 0) return;
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.BlendState = BlendState.NonPremultiplied;
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _terrainEffect3D!.World = Matrix.Identity;
        _terrainEffect3D.View = view;
        _terrainEffect3D.Projection = projection;
        _terrainEffect3D.DiffuseColor = new Vector3(Math.Max(0.3f, daylight));
        if (_precipitationBuffer3D.Length < _precipitationVertices3D.Count)
            Array.Resize(ref _precipitationBuffer3D, Math.Max(_precipitationVertices3D.Count, _precipitationBuffer3D.Length * 2));
        _precipitationVertices3D.CopyTo(_precipitationBuffer3D);
        foreach (var pass in _terrainEffect3D.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _precipitationBuffer3D, 0,
                _precipitationVertices3D.Count / 3);
        }
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;

        void Pixel(Vector3 center, Vector3 right, Vector3 up, int x, int y, Color color)
        {
            center += right * x + up * y;
            right *= 0.5f;
            up *= 0.5f;
            Quad(center - right - up, center + right - up, center + right + up, center - right + up, color);
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            _precipitationVertices3D.Add(new(a, color));
            _precipitationVertices3D.Add(new(b, color));
            _precipitationVertices3D.Add(new(c, color));
            _precipitationVertices3D.Add(new(a, color));
            _precipitationVertices3D.Add(new(c, color));
            _precipitationVertices3D.Add(new(d, color));
        }
    }

    private Vector3 PastWeatherSource3D(Vector3 source, double age)
    {
        var drift = (float)(age * CloudDriftTilesPerSecond);
        if (IsPlanetoid3D)
            return Vector3.Transform(source, Matrix.CreateRotationY(-drift / PlanetoidGeometry.Radius(_world.Width)));
        if (IsRing3D)
        {
            var axis = new Vector3(0, RingGeometry.Radius(_world.Width), 0);
            return Vector3.Transform(source - axis, Matrix.CreateRotationZ(-MathHelper.TwoPi * drift / _world.Width)) + axis;
        }
        if (_world.Body is WorldBody.Nexus) source.Y -= NexusLift3D(source.X, source.Z);
        source.X -= drift;
        if (_world.Body is WorldBody.Nexus) source.Y += NexusLift3D(source.X, source.Z);
        return source;
    }
}
