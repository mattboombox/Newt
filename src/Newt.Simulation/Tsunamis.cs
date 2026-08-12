namespace Newt.Simulation;

/// <summary>Ocean-originating erosion waves.</summary>
public static class Tsunamis
{
    public static bool Create(SimulationWorld world, GridPosition center, float magnitude)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(center))
        {
            throw new ArgumentOutOfRangeException(nameof(center));
        }
        if (!float.IsFinite(magnitude) || magnitude is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(magnitude));
        }
        if (world.GetTerrain(center) is not
            (Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice))
        {
            return false;
        }

        var maximumRadius = 5 + (int)MathF.Round(
            Math.Min(world.Width, world.Height) * (0.08f + magnitude * 0.28f));
        world.ImpactWaves.Add(new ImpactWaveActivity(
            center,
            maximumRadius,
            0.8f + magnitude * 1.4f,
            magnitude,
            WaveKind.Tsunami));
        return true;
    }
}
