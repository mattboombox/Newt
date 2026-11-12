using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private GridPosition[] _planetoidMapTiles = [];
    private GridPosition[] _planetoidTileLocations = [];
    private int _planetoidMapCacheWidth;

    private int MapWidth2D => IsPlanetoid3D ? PlanetoidLayout.FaceSize(_world.Width) * 4 : _world.Width;

    private GridPosition MapTile2D(int x, int y)
    {
        if (!IsPlanetoid3D) return new(Mod(x, _world.Width), y);
        EnsurePlanetoidMapProjection();
        return _planetoidMapTiles[y * MapWidth2D + Mod(x, MapWidth2D)];
    }

    private GridPosition MapLocation2D(GridPosition tile)
    {
        if (!IsPlanetoid3D) return tile;
        EnsurePlanetoidMapProjection();
        return _planetoidTileLocations[tile.Y * _world.Width + tile.X];
    }

    private void EnsurePlanetoidMapProjection()
    {
        if (_planetoidMapCacheWidth == _world.Width) return;
        var size = PlanetoidLayout.FaceSize(_world.Width);
        var width = MapWidth2D;
        var height = _world.Height;
        _planetoidMapTiles = new GridPosition[width * height];
        _planetoidTileLocations = new GridPosition[_world.Width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var direction = PlanetoidLayout.MapDirection(x + 0.5f, y + 0.5f, width, height);
                var tile = PlanetoidLayout.AtlasPosition(direction, size);
                _planetoidMapTiles[y * width + x] = new((int)tile.X, (int)tile.Y);
            }
            for (var x = 0; x < _world.Width; x++)
            {
                var direction = PlanetoidLayout.Direction(x + 0.5f, y + 0.5f, size, PlanetoidLayout.FaceAt(x, y, size));
                var location = PlanetoidLayout.MapPosition(direction, width, height);
                _planetoidTileLocations[y * _world.Width + x] = new((int)location.X, (int)location.Y);
            }
        }
        _planetoidMapCacheWidth = _world.Width;
    }
}
