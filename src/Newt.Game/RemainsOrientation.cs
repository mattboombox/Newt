using Newt.Simulation;

namespace Newt.Game;

internal static class RemainsOrientation
{
    internal static int QuarterTurns(ulong seed, GridPosition tile, bool shipwreck)
    {
        unchecked
        {
            var hash = seed ^ ((ulong)(uint)tile.X * 0x9E3779B185EBCA87UL) ^
                ((ulong)(uint)tile.Y * 0xC2B2AE3D27D4EB4FUL) ^
                (shipwreck ? 0xA24BAED4963EE407UL : 0xD1B54A32D192ED03UL);
            hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
            hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
            return (int)((hash ^ (hash >> 31)) & 3);
        }
    }
}
