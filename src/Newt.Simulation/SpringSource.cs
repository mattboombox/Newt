namespace Newt.Simulation;

/// <summary>A persistent source from which freshwater can be redrawn.</summary>
internal readonly record struct SpringSource(GridPosition Position, int MaximumLength);
