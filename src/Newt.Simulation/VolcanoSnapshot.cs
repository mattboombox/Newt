namespace Newt.Simulation;

/// <summary>An immutable view of a volcano for rendering and inspection.</summary>
public readonly record struct VolcanoSnapshot(GridPosition Position, VolcanoState State);
