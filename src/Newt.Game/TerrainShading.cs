using Newt.Simulation;

namespace Newt.Game;

/// <summary>Small brightness changes that preserve the existing biome palette.</summary>
internal static class TerrainShading
{
    internal static float GetBrightness(Terrain terrain, float elevation, float seaLevel)
    {
        if (terrain is Terrain.Mountain or Terrain.RingWorldWall)
        {
            return 1f;
        }

        if (terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice)
        {
            var depth = Math.Clamp((seaLevel - elevation) / 0.5f, 0f, 1f);
            return 1f + 0.03f * (1f - 2f * depth);
        }

        // A continuous scale avoids adding shading seams at landform boundaries.
        var height = Math.Clamp(elevation / TerrainClassifier.MountainElevationThreshold, -1f, 1f);
        return 1f + 0.10f * height;
    }
}
