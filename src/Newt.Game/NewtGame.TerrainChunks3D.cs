using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private const int MeshChunkSize3D = 32;
    private readonly List<TerrainChunk3D> _terrainChunks3D = [];
    private readonly List<ChunkView3D> _visibleChunks3D = [];
    private readonly List<VertexPositionColor> _dynamicWater3D = [];
    private readonly List<VertexPositionColor> _dynamicIce3D = [];
    private SimulationWorld? _chunkWorld3D;
    private (float SeaLevel, bool HasOceans) _chunkWaterSettings3D;

    private bool IsMegaCylinder3D => _preset == WorldPreset.ONeillCylinderMega &&
        _world.Body is WorldBody.ONeillCylinder;

    private sealed record ChunkMesh3D(VertexBuffer Buffer, int[] ColumnStarts);

    private sealed class TerrainChunk3D(Rectangle tiles, BoundingBox bounds)
    {
        public Rectangle Tiles { get; } = tiles;
        public BoundingBox Bounds { get; } = bounds;
        public bool Dirty { get; set; } = true;
        public bool HasPlayableTiles { get; set; } = true;
        public ChunkMesh3D? Terrain { get; set; }
        public ChunkMesh3D? Water { get; set; }
        public ChunkMesh3D? Ice { get; set; }

        public void DisposeBuffers()
        {
            Terrain?.Buffer.Dispose();
            Water?.Buffer.Dispose();
            Ice?.Buffer.Dispose();
        }
    }

    private readonly record struct ChunkView3D(TerrainChunk3D Chunk, Rectangle Tiles, BoundingBox Bounds, int OffsetX);

    private void EnsureTerrainChunks3D()
    {
        if (_chunkWorld3D == _world) return;
        ReleaseTerrainChunks3D();
        _chunkWorld3D = _world;
        _chunkWorld3D.TerrainGeometryChanged += InvalidateTerrainChunks3D;
        for (var y = 0; y < _world.Height; y += MeshChunkSize3D)
        for (var x = 0; x < _world.Width; x += MeshChunkSize3D)
        {
            var tiles = new Rectangle(x, y, Math.Min(MeshChunkSize3D, _world.Width - x),
                Math.Min(MeshChunkSize3D, _world.Height - y));
            var bounds = ChunkBounds3D(tiles);
            _terrainChunks3D.Add(new TerrainChunk3D(tiles, bounds));
        }
    }

    private BoundingBox ChunkBounds3D(Rectangle tiles)
    {
        if (IsPlanetoid3D)
        {
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            var size = PlanetoidLayout.FaceSize(_world.Width);
            for (var face = 0; face < 6; face++)
            {
                var faceTiles = new Rectangle(face % 3 * size, face / 3 * size, size, size);
                var region = Rectangle.Intersect(tiles, faceTiles);
                if (region.Width == 0 || region.Height == 0) continue;
                // Project the rectangle's edges and any face-center axes it crosses.
                var middleX = Math.Clamp(faceTiles.Center.X, region.Left, region.Right);
                var middleY = Math.Clamp(faceTiles.Center.Y, region.Top, region.Bottom);
                var owner = new GridPosition(region.Left, region.Top);
                foreach (var x in new[] { region.Left, middleX, region.Right })
                foreach (var y in new[] { region.Top, middleY, region.Bottom })
                {
                    var low = WorldPoint3D(new Vector3(x, 0, y), owner);
                    var high = WorldPoint3D(new Vector3(x, 14, y), owner);
                    min = Vector3.Min(min, Vector3.Min(low, high));
                    max = Vector3.Max(max, Vector3.Max(low, high));
                }
            }
            return new BoundingBox(min, max);
        }
        if (!IsRing3D)
        {
            var height = _world.Body is WorldBody.Nexus
                ? 14f + ThreeDGeometry.NexusMaximumPetalLift(NexusLayout.Radius(_world)) : 14f;
            return new BoundingBox(new Vector3(tiles.Left, 0, tiles.Top),
                new Vector3(tiles.Right, height, tiles.Bottom));
        }

        // Sample every column around the curved strip, including any angular extrema.
        var bounds = CurvedTileBounds3D(tiles.Left, tiles.Top, 14);
        for (var x = tiles.Left + 1; x < tiles.Right; x++)
            bounds = BoundingBox.CreateMerged(bounds, CurvedTileBounds3D(x, tiles.Top, 14));
        bounds.Max.Z += tiles.Height - 1;
        return bounds;
    }

    private IEnumerable<ChunkView3D> GetChunkViews3D(Rectangle visibleTiles)
    {
        var wraps = !HasCurvedSurface3D;
        foreach (var chunk in _terrainChunks3D)
        {
            var tiles = chunk.Tiles;
            if (!chunk.HasPlayableTiles || tiles.Top >= visibleTiles.Bottom || tiles.Bottom <= visibleTiles.Top)
                continue;
            if (!wraps)
            {
                var clipped = Rectangle.Intersect(tiles, visibleTiles);
                if (!clipped.IsEmpty)
                    yield return new ChunkView3D(chunk, clipped, chunk.Bounds, 0);
                continue;
            }

            var start = ThreeDGeometry.FirstVisibleChunkCopy(tiles.Left, tiles.Width, visibleTiles.Left, _world.Width);
            for (var x = start; x < visibleTiles.Right; x += _world.Width)
            {
                var copy = new Rectangle(x, tiles.Top, tiles.Width, tiles.Height);
                var clipped = Rectangle.Intersect(copy, visibleTiles);
                if (clipped.IsEmpty) continue;
                var offset = x - tiles.Left;
                var translation = new Vector3(offset, 0, 0);
                yield return new ChunkView3D(chunk, clipped,
                    new BoundingBox(chunk.Bounds.Min + translation, chunk.Bounds.Max + translation), offset);
            }
        }
    }

    private void ReleaseTerrainChunks3D()
    {
        if (_chunkWorld3D is not null)
            _chunkWorld3D.TerrainGeometryChanged -= InvalidateTerrainChunks3D;
        _chunkWorld3D = null;
        foreach (var chunk in _terrainChunks3D)
            chunk.DisposeBuffers();
        _terrainChunks3D.Clear();
        _visibleChunks3D.Clear();
    }

    private void InvalidateTerrainChunks3D(GridPosition? changed)
    {
        if (changed is not { } tile)
        {
            foreach (var chunk in _terrainChunks3D)
                chunk.Dirty = true;
            return;
        }

        // A column's sides depend on neighboring heights, including across a wrapping seam.
        var columns = (_world.Width + MeshChunkSize3D - 1) / MeshChunkSize3D;
        var row = tile.Y / MeshChunkSize3D * columns;
        var column = tile.X / MeshChunkSize3D;
        _terrainChunks3D[row + column].Dirty = true;
        if (tile.X > 0 || _world.Body is not WorldBody.Nexus)
            _terrainChunks3D[row + ThreeDGeometry.WrapTile(tile.X - 1, _world.Width) / MeshChunkSize3D].Dirty = true;
        if (tile.X + 1 < _world.Width || _world.Body is not WorldBody.Nexus)
            _terrainChunks3D[row + ThreeDGeometry.WrapTile(tile.X + 1, _world.Width) / MeshChunkSize3D].Dirty = true;
        if (tile.Y > 0)
            _terrainChunks3D[(tile.Y - 1) / MeshChunkSize3D * columns + column].Dirty = true;
        if (tile.Y + 1 < _world.Height)
            _terrainChunks3D[(tile.Y + 1) / MeshChunkSize3D * columns + column].Dirty = true;
        if (IsPlanetoid3D)
        {
            var size = PlanetoidLayout.FaceSize(_world.Width);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var neighbor = PlanetoidLayout.Offset(tile, dx, dy, size);
                _terrainChunks3D[neighbor.Y / MeshChunkSize3D * columns + neighbor.X / MeshChunkSize3D].Dirty = true;
            }
        }
    }

    private void PrepareTerrainChunks3D(BoundingFrustum frustum, Rectangle visibleTiles)
    {
        EnsureTerrainChunks3D();
        var waterSettings = (_world.SeaLevel, _world.HasOceans);
        if (_chunkWaterSettings3D != waterSettings)
        {
            _chunkWaterSettings3D = waterSettings;
            InvalidateTerrainChunks3D(null);
        }

        _visibleChunks3D.Clear();
        _dynamicWater3D.Clear();
        _dynamicIce3D.Clear();
        var water = new List<VertexPositionColor>();
        var ice = new List<VertexPositionColor>();
        GraphicsDevice.SetVertexBuffer(null);
        foreach (var view in GetChunkViews3D(visibleTiles))
        {
            if (!frustum.Intersects(view.Bounds)) continue;
            if (view.Chunk.Dirty)
                BuildTerrainChunk3D(view.Chunk, water, ice);
            if (view.Chunk.HasPlayableTiles)
                _visibleChunks3D.Add(view);
        }
        _terrainVertices3D.Clear();
    }

    private void BuildTerrainChunk3D(TerrainChunk3D chunk, List<VertexPositionColor> water,
        List<VertexPositionColor> ice)
    {
        _terrainVertices3D.Clear();
        water.Clear();
        ice.Clear();
        var tiles = chunk.Tiles;
        var terrainStarts = new int[tiles.Width + 1];
        var waterStarts = new int[tiles.Width + 1];
        var iceStarts = new int[tiles.Width + 1];
        var hasPlayableTiles = false;
        for (var x = tiles.Left; x < tiles.Right; x++)
        {
            for (var y = tiles.Top; y < tiles.Bottom; y++)
            {
                if (!ContainsTile3D(x, y)) continue;
                hasPlayableTiles = true;
                var tile = new GridPosition(x, y);
                // Lava pulses over time and is drawn by the dynamic pass.
                if (_world.GetSurfaceCover(tile) is SurfaceCover.Lava) continue;
                AddColumn3D(tile, TileHeight3D(tile), TerrainColumnColor3D(tile));
                AddWaterTile3D(tile, x, y, water, ice);
            }
            var nextColumn = x - tiles.Left + 1;
            terrainStarts[nextColumn] = _terrainVertices3D.Count;
            waterStarts[nextColumn] = water.Count;
            iceStarts[nextColumn] = ice.Count;
        }
        CurveWaterVertices3D(water);
        CurveWaterVertices3D(ice);
        chunk.DisposeBuffers();
        chunk.Terrain = UploadChunk3D(_terrainVertices3D, terrainStarts);
        chunk.Water = UploadChunk3D(water, waterStarts);
        chunk.Ice = UploadChunk3D(ice, iceStarts);
        chunk.HasPlayableTiles = hasPlayableTiles;
        chunk.Dirty = false;
    }

    private ChunkMesh3D? UploadChunk3D(List<VertexPositionColor> vertices, int[] columnStarts)
    {
        if (vertices.Count == 0) return null;
        var buffer = new VertexBuffer(GraphicsDevice, VertexPositionColor.VertexDeclaration,
            vertices.Count, BufferUsage.WriteOnly);
        buffer.SetData(vertices.ToArray());
        return new ChunkMesh3D(buffer, columnStarts);
    }

    private void DrawTerrainChunks3D(Func<TerrainChunk3D, ChunkMesh3D?> select)
    {
        var effect = _terrainEffect3D!;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            int? appliedOffset = null;
            foreach (var view in _visibleChunks3D)
            {
                if (select(view.Chunk) is not { } mesh) continue;
                // At the planar wrap seam, draw only the columns belonging to this copy.
                var left = view.Tiles.Left - view.OffsetX - view.Chunk.Tiles.Left;
                var firstVertex = mesh.ColumnStarts[left];
                var vertexCount = mesh.ColumnStarts[left + view.Tiles.Width] - firstVertex;
                if (vertexCount == 0) continue;
                if (appliedOffset != view.OffsetX)
                {
                    effect.World = Matrix.CreateTranslation(view.OffsetX, 0, 0);
                    pass.Apply();
                    appliedOffset = view.OffsetX;
                }
                GraphicsDevice.SetVertexBuffer(mesh.Buffer);
                GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, firstVertex, vertexCount / 3);
            }
        }
        effect.World = Matrix.Identity;
        GraphicsDevice.SetVertexBuffer(null);
    }
}
