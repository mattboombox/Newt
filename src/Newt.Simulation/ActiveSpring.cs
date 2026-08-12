namespace Newt.Simulation;

/// <summary>Mutable state for a spring that is still extending downhill.</summary>
internal sealed class ActiveSpring(GridPosition source, int maximumLength)
{
    public GridPosition Current { get; set; } = source;

    public int MaximumLength { get; } = maximumLength;

    public HashSet<GridPosition> Visited { get; } = [source];

    public Queue<GridPosition> PlannedRoute { get; } = new();

    public HashSet<GridPosition> UpstreamLake { get; } = [];
}
