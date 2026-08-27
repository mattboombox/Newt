namespace Newt.Simulation;

/// <summary>The result of filling a basin, or adding OceanSeed instead of creating an oversized lake.</summary>
public readonly record struct LakeFillResult(
    bool Created,
    int LakeTileCount,
    float SurfaceElevation,
    GridPosition? Outlet,
    GridPosition? OutletConnection,
    IReadOnlyList<GridPosition>? OverflowPath = null,
    GridPosition? OceanSeed = null);
