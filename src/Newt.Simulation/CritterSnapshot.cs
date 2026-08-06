namespace Newt.Simulation;

/// <summary>An immutable view of critter state for rendering and diagnostics.</summary>
public readonly record struct CritterSnapshot(
    CritterId Id,
    CritterSpecies Species,
    GridPosition Position);
