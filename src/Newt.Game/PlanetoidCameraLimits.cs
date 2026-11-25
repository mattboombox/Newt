namespace Newt.Game;

internal static class PlanetoidCameraLimits
{
    internal static int Constrain(int position, int mapLength, int visibleTiles)
    {
        var halfViewport = Math.Max(1, visibleTiles) / 2;
        return Math.Clamp(position, -halfViewport, mapLength - 1 - halfViewport);
    }
}
