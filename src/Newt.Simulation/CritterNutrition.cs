namespace Newt.Simulation;

public enum CritterBodySize : byte
{
    Tiny = 1,
    Small = 2,
    Medium = 3,
    Big = 4,
    Large = 5,
    Huge = 8,
}

public enum CritterFeedingStrategy : byte
{
    Photosynthetic,
    TerrainOnly,
    TerrainFirst,
    Hunter,
}

/// <summary>
/// Describes how a species turns food into survival and offspring. Energy is
/// stored as small whole units so lifecycle results remain deterministic.
/// </summary>
public readonly record struct CritterNutrition(
    CritterBodySize BodySize,
    CritterFeedingStrategy FeedingStrategy,
    int InitialEnergy,
    int MaximumEnergy,
    int HungryThreshold,
    int MetabolismIntervalTicks,
    int MetabolismCost,
    int ReproductionThreshold,
    int ReproductionCost)
{
    public bool HasMetabolism => MetabolismIntervalTicks > 0 && MetabolismCost > 0;

    public bool CanReproduce => ReproductionThreshold > 0 && ReproductionCost > 0;

    public bool FeedsFromEnvironment => FeedingStrategy is
        CritterFeedingStrategy.Photosynthetic or
        CritterFeedingStrategy.TerrainOnly or
        CritterFeedingStrategy.TerrainFirst;

    public int FoodEnergy => (int)BodySize;
}

public static class CritterNutritions
{
    private static readonly CritterNutrition Plankton = new(
        BodySize: CritterBodySize.Tiny,
        FeedingStrategy: CritterFeedingStrategy.Photosynthetic,
        InitialEnergy: 1,
        MaximumEnergy: 4,
        HungryThreshold: 1,
        MetabolismIntervalTicks: 45 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 4,
        ReproductionCost: 2);

    private static readonly CritterNutrition Jellyfish = new(
        BodySize: CritterBodySize.Small,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 2,
        MaximumEnergy: 8,
        HungryThreshold: 2,
        MetabolismIntervalTicks: 120 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 8,
        ReproductionCost: 6);

    private static readonly CritterNutrition Worm = new(
        BodySize: CritterBodySize.Medium,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 2,
        MaximumEnergy: 5,
        HungryThreshold: 1,
        MetabolismIntervalTicks: 60 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 5,
        ReproductionCost: 3);

    private static readonly CritterNutrition Trilobite = new(
        BodySize: CritterBodySize.Big,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 2,
        MaximumEnergy: 5,
        HungryThreshold: 1,
        MetabolismIntervalTicks: 60 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 5,
        ReproductionCost: 3);

    private static readonly CritterNutrition SeaScorpion = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 4,
        MaximumEnergy: 10,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 90 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 10,
        ReproductionCost: 7);

    private static readonly CritterNutrition Nautilus = new(
        BodySize: CritterBodySize.Big,
        FeedingStrategy: CritterFeedingStrategy.TerrainFirst,
        InitialEnergy: 3,
        MaximumEnergy: 7,
        HungryThreshold: 2,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 7,
        ReproductionCost: 5);

    private static readonly CritterNutrition Fish = new(
        BodySize: CritterBodySize.Big,
        FeedingStrategy: CritterFeedingStrategy.TerrainFirst,
        InitialEnergy: 3,
        MaximumEnergy: 7,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 45 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 7,
        ReproductionCost: 5);

    private static readonly CritterNutrition Newt = new(
        BodySize: CritterBodySize.Medium,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 4,
        MaximumEnergy: 7,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 7,
        ReproductionCost: 5);

    private static readonly CritterNutrition MegaToad = new(
        BodySize: CritterBodySize.Huge,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 5,
        MaximumEnergy: 16,
        HungryThreshold: 4,
        MetabolismIntervalTicks: 70 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 14,
        ReproductionCost: 9);

    private static readonly CritterNutrition Crab = new(
        BodySize: CritterBodySize.Big,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 3,
        MaximumEnergy: 6,
        HungryThreshold: 2,
        MetabolismIntervalTicks: 45 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 5,
        ReproductionCost: 3);

    private static readonly CritterNutrition Squid = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 4,
        MaximumEnergy: 10,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 10,
        ReproductionCost: 7);

    private static readonly CritterNutrition SquidEgg = new(
        BodySize: CritterBodySize.Tiny,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 1,
        MaximumEnergy: 1,
        HungryThreshold: 0,
        MetabolismIntervalTicks: 0,
        MetabolismCost: 0,
        ReproductionThreshold: 0,
        ReproductionCost: 0);

    private static readonly CritterNutrition Therapsid = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.TerrainFirst,
        InitialEnergy: 5,
        MaximumEnergy: 12,
        HungryThreshold: 4,
        MetabolismIntervalTicks: 70 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 12,
        ReproductionCost: 8);

    private static readonly CritterNutrition Monkey = new(
        BodySize: CritterBodySize.Big,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 4,
        MaximumEnergy: 9,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 8,
        ReproductionCost: 5);

    private static readonly CritterNutrition Ape = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 6,
        MaximumEnergy: 14,
        HungryThreshold: 5,
        MetabolismIntervalTicks: 70 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 12,
        ReproductionCost: 8);

    private static readonly CritterNutrition Deer = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 4,
        MaximumEnergy: 8,
        HungryThreshold: 3,
        MetabolismIntervalTicks: 75 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 8,
        ReproductionCost: 5);

    private static readonly CritterNutrition Elk = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.TerrainOnly,
        InitialEnergy: 5,
        MaximumEnergy: 11,
        HungryThreshold: 4,
        MetabolismIntervalTicks: 80 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 11,
        ReproductionCost: 8);

    private static readonly CritterNutrition Wolf = new(
        BodySize: CritterBodySize.Large,
        FeedingStrategy: CritterFeedingStrategy.Hunter,
        InitialEnergy: 5,
        MaximumEnergy: 11,
        HungryThreshold: 4,
        MetabolismIntervalTicks: 60 * SimulationWorld.TicksPerSecond,
        MetabolismCost: 1,
        ReproductionThreshold: 11,
        ReproductionCost: 8);

    public static CritterNutrition Get(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => Plankton,
        CritterSpecies.Jellyfish => Jellyfish,
        CritterSpecies.Worm => Worm,
        CritterSpecies.Trilobite => Trilobite,
        CritterSpecies.SeaScorpion => SeaScorpion,
        CritterSpecies.Nautilus => Nautilus,
        CritterSpecies.Fish => Fish,
        CritterSpecies.Newt => Newt,
        CritterSpecies.MegaToad => MegaToad,
        CritterSpecies.Crab => Crab,
        CritterSpecies.Squid => Squid,
        CritterSpecies.SquidEgg => SquidEgg,
        CritterSpecies.Therapsid => Therapsid,
        CritterSpecies.Monkey => Monkey,
        CritterSpecies.Ape => Ape,
        CritterSpecies.ApeSailor => Ape,
        CritterSpecies.Deer => Deer,
        CritterSpecies.Elk => Elk,
        CritterSpecies.Gazelle => Deer,
        CritterSpecies.Wolf => Wolf,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };
}
