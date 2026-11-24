namespace Newt.Simulation;

public sealed record ApeWarArmyStatus(int Fighters, int StartingFighters, int StopAt,
    ApeCulture? Culture, bool Undead = false, bool PopulationTarget = false);

public sealed record ApeWarStatus(ApeWarArmyStatus Attacking, ApeWarArmyStatus Defending);

public sealed record ActiveApeWar(long Id, string Attacker, string Defender, string WarType, ApeWarStatus Armies)
{
    public GridPosition? AttackerPosition { get; init; }
    public GridPosition? DefenderPosition { get; init; }
}
