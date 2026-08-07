namespace Newt.Simulation;

/// <summary>The result of filling one closed elevation basin.</summary>
public readonly record struct LakeFillResult(
    bool Created,
    int LakeTileCount,
    float SurfaceElevation,
    GridPosition? Outlet,
    GridPosition? OutletConnection);
