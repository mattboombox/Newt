namespace Newt.Simulation;

/// <summary>A persistent source used to rebuild derived rivers and lakes.</summary>
internal readonly record struct SpringSource(GridPosition Position, SpringOrigin Origin);
