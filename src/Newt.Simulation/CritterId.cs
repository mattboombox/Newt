namespace Newt.Simulation;

/// <summary>A stable handle to a critter. Zero is reserved as an invalid ID.</summary>
public readonly record struct CritterId(int Value)
{
    public bool IsValid => Value > 0;
}
