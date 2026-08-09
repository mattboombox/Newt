namespace Newt.Simulation;

public readonly record struct MeteorImpactResult(
    GridPosition Center,
    float Magnitude,
    int CraterRadius,
    int ShockRadius,
    int FragmentCount);
