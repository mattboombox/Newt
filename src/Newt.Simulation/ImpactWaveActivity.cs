namespace Newt.Simulation;

internal sealed class ImpactWaveActivity(
    GridPosition center,
    float maximumRadius,
    float speed,
    float magnitude,
    WaveKind kind = WaveKind.Impact)
{
    public GridPosition Center { get; } = center;

    public float CurrentRadius { get; set; }

    public float MaximumRadius { get; } = maximumRadius;

    public float Speed { get; } = speed;

    public float Magnitude { get; } = magnitude;

    public WaveKind Kind { get; } = kind;

    internal List<(GridPosition Position, float Distance)>? PlanetoidFront { get; set; }

    internal int NextPlanetoidTile { get; set; }
    internal HashSet<GridPosition>? LimitedTiles { get; set; }
}
