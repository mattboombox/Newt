namespace Newt.Simulation;

/// <summary>
/// Ecological interpretation of a tile's temperature and moisture. Terrain
/// remains the physical surface beneath the biome.
/// </summary>
public enum Biome : byte
{
    None,
    Arctic,
    Tundra,
    Taiga,
    Bog,
    Grassland,
    Forest,
    Swamp,
    Desert,
    Arid,
    Jungle,
}
