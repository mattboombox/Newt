namespace Newt.Simulation;

/// <summary>Inputs required to reproduce a generated world.</summary>
public readonly record struct WorldGenerationOptions(
    WorldPreset Preset,
    ulong Seed,
    double LandFraction = 0.38);
