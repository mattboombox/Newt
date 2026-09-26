namespace Newt.Simulation;

/// <summary>An immutable view of critter state for rendering and diagnostics.</summary>
public readonly record struct CritterSnapshot(
    CritterId Id,
    CritterSpecies Species,
    GridPosition Position,
    int Energy,
    int MaximumEnergy,
    bool IsHungry,
    bool CanReproduce,
    bool IsDamageFlashing,
    PlagueKind Plague = PlagueKind.None,
    bool IsColonist = false,
    GridPosition? ColonistDestination = null,
    bool IsVeteranWarrior = false,
    int CombatKills = 0)
{
    public bool IsPlagueImmune => Species is CritterSpecies.ApeSailor or CritterSpecies.Vampire;
}
