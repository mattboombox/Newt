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

/// <summary>Species traits; energy costs and reserves are derived from the energy cap.</summary>
public readonly record struct CritterNutrition(
    CritterBodySize BodySize,
    CritterFeedingStrategy FeedingStrategy,
    int MaximumEnergy,
    int VillageFeedingThreshold = 0,
    bool CanReproduce = true,
    bool HasMetabolism = true,
    bool HuntsUntilFull = false,
    bool HuntsWhenFull = false)
{
    public const int MetabolismIntervalSeconds = 75;

    // Whole units: round starting energy down and breeding cost up.
    public int InitialEnergy => Math.Max(1, MaximumEnergy / 3);
    public int ReproductionThreshold => MaximumEnergy;
    public int ReproductionCost => (MaximumEnergy + 1) / 2;
    public int MetabolismCost => 1;
    public int MetabolismIntervalTicks => HasMetabolism
        ? MetabolismIntervalSeconds * SimulationWorld.TicksPerSecond : 0;
    public int HungryThreshold => (int)((2L * MaximumEnergy - 1) / 3);

    /// <summary>Strictly below two-thirds of maximum energy, without rounding.</summary>
    public bool IsHungryAt(int energy) => HasMetabolism && 3L * energy < 2L * MaximumEnergy;

    public bool WantsToHuntAt(int energy) => HuntsWhenFull || (HuntsUntilFull
        ? energy < MaximumEnergy : IsHungryAt(energy));

    public bool FeedsFromEnvironment => FeedingStrategy is
        CritterFeedingStrategy.Photosynthetic or
        CritterFeedingStrategy.TerrainOnly or
        CritterFeedingStrategy.TerrainFirst;

    public int FoodEnergy => (int)BodySize;
}

public static class CritterNutritions
{
    private static readonly CritterNutrition Plankton = new(CritterBodySize.Tiny, CritterFeedingStrategy.Photosynthetic, 4);
    private static readonly CritterNutrition Jellyfish = new(CritterBodySize.Small, CritterFeedingStrategy.Hunter, 8,
        HuntsWhenFull: true);
    private static readonly CritterNutrition Trilobite = new(CritterBodySize.Big, CritterFeedingStrategy.TerrainOnly, 7);
    private static readonly CritterNutrition SeaScorpion = new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 10);
    private static readonly CritterNutrition MegaSpider = new(CritterBodySize.Huge, CritterFeedingStrategy.Hunter, 11);
    private static readonly CritterNutrition Nautilus = new(CritterBodySize.Big, CritterFeedingStrategy.TerrainFirst, 7);
    private static readonly CritterNutrition Fish = new(CritterBodySize.Big, CritterFeedingStrategy.TerrainFirst, 7);
    private static readonly CritterNutrition Newt = new(CritterBodySize.Medium, CritterFeedingStrategy.TerrainOnly, 7);
    private static readonly CritterNutrition MegaToad = new(CritterBodySize.Huge, CritterFeedingStrategy.Hunter, 16);
    private static readonly CritterNutrition Crab = new(CritterBodySize.Big, CritterFeedingStrategy.TerrainOnly, 6);
    private static readonly CritterNutrition Squid = new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 10);
    private static readonly CritterNutrition SquidEgg = new(CritterBodySize.Tiny, CritterFeedingStrategy.Hunter, 1,
        CanReproduce: false, HasMetabolism: false);
    private static readonly CritterNutrition Therapsid = new(CritterBodySize.Large, CritterFeedingStrategy.TerrainOnly, 8);
    private static readonly CritterNutrition Monkey = new(CritterBodySize.Big, CritterFeedingStrategy.TerrainOnly, 8);
    private static readonly CritterNutrition Ape = new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 12,
        VillageFeedingThreshold: 5);
    private static readonly CritterNutrition Deer = new(CritterBodySize.Large, CritterFeedingStrategy.TerrainOnly, 8);
    private static readonly CritterNutrition Elk = new(CritterBodySize.Large, CritterFeedingStrategy.TerrainOnly, 11);
    private static readonly CritterNutrition Wolf = new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 11);
    private static readonly CritterNutrition ToothedWhale = new(CritterBodySize.Huge, CritterFeedingStrategy.Hunter, 30,
        HuntsUntilFull: true);

    public static CritterNutrition Get(CritterSpecies species) => species switch
    {
        CritterSpecies.Custodian => new(CritterBodySize.Big, CritterFeedingStrategy.Hunter, 30,
            CanReproduce: false, HasMetabolism: false),
        CritterSpecies.Lich => new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 40,
            CanReproduce: false, HasMetabolism: false),
        CritterSpecies.UndeadWarrior => new(CritterBodySize.Large, CritterFeedingStrategy.Hunter, 6,
            CanReproduce: false, HasMetabolism: false),
        CritterSpecies.Plankton => Plankton,
        CritterSpecies.Jellyfish => Jellyfish,
        CritterSpecies.Worm or CritterSpecies.Trilobite => Trilobite,
        CritterSpecies.SeaScorpion => SeaScorpion,
        CritterSpecies.MegaSpider => MegaSpider,
        CritterSpecies.Nautilus => Nautilus,
        CritterSpecies.Fish => Fish,
        CritterSpecies.Newt or CritterSpecies.Snail => Newt,
        CritterSpecies.MegaToad => MegaToad,
        CritterSpecies.Crab => Crab,
        CritterSpecies.Squid => Squid,
        CritterSpecies.SquidEgg => SquidEgg,
        CritterSpecies.Therapsid => Therapsid,
        CritterSpecies.Monkey => Monkey,
        CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor => Ape with { MaximumEnergy = 80, CanReproduce = false },
        CritterSpecies.Ape => Ape,
        CritterSpecies.ApeSailor => Ape with { MaximumEnergy = 2 * Ape.MaximumEnergy },
        CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard => Ape,
        CritterSpecies.ApeBomber => Ape with { MaximumEnergy = 20, CanReproduce = false },
        CritterSpecies.ApeLudditePriest or CritterSpecies.ApeBishop => Ape with { CanReproduce = false },
        CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen => Ape,
        CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack => Ape,
        CritterSpecies.ApeScholar or CritterSpecies.UndeadApe => Ape with { CanReproduce = false },
        CritterSpecies.Vampire => Ape,
        CritterSpecies.Deer or CritterSpecies.Gazelle => Deer,
        CritterSpecies.MegaSlug => Deer with { MaximumEnergy = 10 },
        CritterSpecies.Elk => Elk,
        CritterSpecies.Wolf or CritterSpecies.Dog => Wolf,
        CritterSpecies.ToothedWhale or CritterSpecies.BaleenWhale => ToothedWhale,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };
}
