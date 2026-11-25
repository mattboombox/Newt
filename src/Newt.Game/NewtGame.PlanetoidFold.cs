using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private const float PlanetoidFoldSeconds = 2.8f;
    private float _planetoidFoldAge = PlanetoidFoldSeconds;
    private Matrix _planetoidFoldStart;
    private Matrix _planetoidFoldEnd;
    private bool _planetoidFoldReversed;
    private BasicEffect? _planetoidFoldEffect;
    private VertexPositionColor[] _planetoidFoldVertices = [];
    private bool FoldingPlanetoid => _view3D && IsPlanetoid3D && _planetoidFoldAge < PlanetoidFoldSeconds;

    private void StartPlanetoidFold(bool reversed = false)
    {
        _planetoidFoldReversed = reversed;
        var (endView, endProjection) = Matrices3D();
        _planetoidFoldEnd = Matrix.Multiply(endView * endProjection,
            1f / Math.Max(1, Matrix.Invert(endView).Translation.Length()));
        var size = PlanetoidFaceSize;
        var center = new Vector3(_cameraX + GraphicsDevice.Viewport.Width / (2f * TileSize) - size * 1.5f,
            size * 1.5f - _cameraY - MapViewportHeight / (2f * TileSize), size / 2f);
        var view = Matrix.CreateLookAt(center + Vector3.UnitZ * size * 4, center, Vector3.Up);
        _planetoidFoldStart = view * Matrix.CreateOrthographic(GraphicsDevice.Viewport.Width / (float)TileSize,
            MapViewportHeight / (float)TileSize, 0.1f, size * 20);
        _planetoidFoldAge = 0;
    }

    private void DrawPlanetoidFold()
    {
        var size = PlanetoidFaceSize;
        var time = Math.Clamp(_planetoidFoldAge / PlanetoidFoldSeconds, 0, 1);
        if (_planetoidFoldReversed) time = 1 - time;
        var progress = Math.Clamp((time - 0.18f) / 0.64f, 0, 1);
        var overview = Matrix.CreateLookAt(new Vector3(0, 0, size * 4), Vector3.Zero, Vector3.Up) *
            Matrix.CreatePerspectiveFieldOfView(ViewFieldOfView3D, Viewport3D().AspectRatio, 0.1f, size * 20);
        overview = Matrix.Multiply(overview, 1f / (size * 4));
        var transform = time < 0.18f
            ? Matrix.Lerp(_planetoidFoldStart, overview, PlanetoidNetFold.Smooth(time / 0.18f))
            : Matrix.Lerp(overview, _planetoidFoldEnd, PlanetoidNetFold.Smooth(Math.Clamp((time - 0.82f) / 0.18f, 0, 1)));
        // Bound animation geometry on very large worlds; each face retains its outline.
        var step = Math.Max(1, (size + 95) / 96);
        var cells = (size + step - 1) / step;
        var count = 6 * cells * cells * 6;
        if (_planetoidFoldVertices.Length != count) _planetoidFoldVertices = new VertexPositionColor[count];
        var index = 0;
        for (var face = 0; face < 6; face++)
        for (var y = 0; y < size; y += step)
        for (var x = 0; x < size; x += step)
        {
            var tile = new GridPosition(face % 3 * size + x, face / 3 * size + y);
            var surface = WaterElevation3D(tile);
            var height = ThreeDGeometry.ColumnHeight(surface ?? _world.GetElevation(tile));
            var temperature = _world.GetTemperatureBand(tile);
            var color = GetTileColor(tile, UnfrozenBedTerrain(tile), _world.GetBiome(tile), temperature);
            if (surface is { } water && water > _world.GetElevation(tile))
                color = IsIce3D(tile) ? GetTerrainColor(Terrain.Ice, temperature)
                    : Color.Lerp(color, WaterSurfaceColor(tile), WaterSurfaceTintAlpha3D / 255f);
            var right = tile.X + Math.Min(step, size - x);
            var bottom = tile.Y + Math.Min(step, size - y);
            var a = Point(tile.X, tile.Y);
            var b = Point(right, tile.Y);
            var c = Point(right, bottom);
            var d = Point(tile.X, bottom);
            _planetoidFoldVertices[index++] = new(a, color);
            _planetoidFoldVertices[index++] = new(b, color);
            _planetoidFoldVertices[index++] = new(c, color);
            _planetoidFoldVertices[index++] = new(a, color);
            _planetoidFoldVertices[index++] = new(c, color);
            _planetoidFoldVertices[index++] = new(d, color);
            Vector3 Point(float px, float py) => Framework(PlanetoidNetFold.Position(px, py, height, _world.Width, tile, progress));
        }
        _planetoidFoldEffect ??= new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        _planetoidFoldEffect.World = Matrix.Identity;
        _planetoidFoldEffect.View = Matrix.Identity;
        _planetoidFoldEffect.Projection = transform;
        var previousViewport = GraphicsDevice.Viewport;
        GraphicsDevice.Viewport = Viewport3D();
        GraphicsDevice.SetVertexBuffer(null);
        GraphicsDevice.BlendState = BlendState.Opaque;
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        foreach (var pass in _planetoidFoldEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _planetoidFoldVertices, 0, index / 3);
        }
        GraphicsDevice.Viewport = previousViewport;
    }
}
