namespace Newt.Simulation;

/// <summary>The observable result of tracing one spring downhill.</summary>
public readonly record struct SpringResult(
    SpringTermination Termination,
    int RiverTileCount,
    GridPosition FinalPosition);
