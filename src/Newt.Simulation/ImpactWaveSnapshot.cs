namespace Newt.Simulation;

public readonly record struct ImpactWaveSnapshot(
    GridPosition Center,
    float CurrentRadius,
    float MaximumRadius,
    float Magnitude);
