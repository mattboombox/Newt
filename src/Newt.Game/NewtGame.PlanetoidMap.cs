using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private int PlanetoidFaceSize => PlanetoidLayout.FaceSize(_world.Width);
    private int MapWidth2D => IsPlanetoid3D ? PlanetoidFaceSize * 4 : _world.Width;
    private int MapHeight2D => IsPlanetoid3D ? PlanetoidFaceSize * 3 : _world.Height;
    private bool WrapsMapX => !IsPlanetoid3D && _world.Body is not WorldBody.Nexus;

    private GridPosition? MapTile2D(int x, int y) => IsPlanetoid3D
        ? PlanetoidLayout.TileFromNet(new(x, y), PlanetoidFaceSize)
        : new GridPosition(Mod(x, _world.Width), y);

    private GridPosition MapLocation2D(GridPosition tile) =>
        IsPlanetoid3D ? PlanetoidLayout.TileToNet(tile, PlanetoidFaceSize) : tile;

    private GridPosition PlanetoidTileAtMapCenter(int x, int y)
    {
        x = Math.Clamp(x, 0, MapWidth2D - 1);
        y = Math.Clamp(y, 0, MapHeight2D - 1);
        if (x < PlanetoidFaceSize || x >= 2 * PlanetoidFaceSize)
            y = Math.Clamp(y, PlanetoidFaceSize, 2 * PlanetoidFaceSize - 1);
        return PlanetoidLayout.TileFromNet(new(x, y), PlanetoidFaceSize)!.Value;
    }
}
