namespace Newt.Simulation;

/// <summary>
/// Describes how a species turns food into survival and offspring. Energy is
/// stored as small whole units so lifecycle results remain deterministic.
/// </summary>
public readonly record struct CritterNutrition(
    int InitialEnergy,
    int MaximumEnergy,
    int HungryThreshold,
    int MetabolismIntervalTicks,
    int MetabolismCost,
    int ReproductionThreshold,
    int ReproductionCost,
    int AmbientFeedingIntervalTicks = 0,
    int AmbientFoodEnergy = 0)
{
    public bool HasMetabolism => MetabolismIntervalTicks > 0 && MetabolismCost > 0;

    public bool CanReproduce => ReproductionThreshold > 0 && ReproductionCost > 0;

    public bool FeedsFromEnvironment =>
        AmbientFeedingIntervalTicks > 0 && AmbientFoodEnergy > 0;
}

public static class CritterNutritions
{
    private static readonly CritterNutrition Plankton = new(
        InitialEnergy: 1,
        MaximumEnergy: 4,
        HungryThreshold: 1,
        MetabolismIntervalTicks: 45 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 4,
        ReproductionCost: 3,
        AmbientFeedingIntervalTicks: 15 * SimulationWorld.TicksPerSecond,
        AmbientFoodEnergy: 1);

    private static readonly CritterNutrition Jellyfish = new(
        InitialEnergy: 2,
        MaximumEnergy: 8,
        HungryThreshold: 2,
        MetabolismIntervalTicks: 120 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 8,
        ReproductionCost: 6);

    private static readonly CritterNutrition Worm = new(
        InitialEnergy: 2,
        MaximumEnergy: 5,
        HungryThreshold: 1,
        MetabolismIntervalTicks: 60 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 5,
        ReproductionCost: 4,
        AmbientFeedingIntervalTicks: 22 * SimulationWorld.TicksPerSecond,
        AmbientFoodEnergy: 1);

    private static readonly CritterNutrition Fish = new(
        InitialEnergy: 3,
        MaximumEnergy: 10,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 45 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 10,
        ReproductionCost: 7);

    private static readonly CritterNutrition Newt = new(
        InitialEnergy: 4,
        MaximumEnergy: 10,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 10,
        ReproductionCost: 7,
        AmbientFeedingIntervalTicks: 18 * SimulationWorld.TicksPerSecond,
        AmbientFoodEnergy: 1);

    private static readonly CritterNutrition MegaToad = new(
        InitialEnergy: 5,
        MaximumEnergy: 12,
        HungryThreshold: 4,
        MetabolismIntervalTicks: 70 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 12,
        ReproductionCost: 8);

    public static CritterNutrition Get(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => Plankton,
        CritterSpecies.Jellyfish => Jellyfish,
        CritterSpecies.Worm => Worm,
        CritterSpecies.Fish => Fish,
        CritterSpecies.Newt => Newt,
        CritterSpecies.MegaToad => MegaToad,
        // Crab and ape behavior has not been ported yet. Leaving metabolism
        // disabled keeps adding those species from silently starving.
        CritterSpecies.Crab or CritterSpecies.Ape => default,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };
}
