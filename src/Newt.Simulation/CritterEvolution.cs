namespace Newt.Simulation;

/// <summary>The primitive critter evolution tree and reproduction mutations.</summary>
public static class CritterEvolution
{
    public const int ChanceStepsPerPercent = 2;
    public const int MaximumChanceSteps = 100 * ChanceStepsPerPercent;
    public const int DefaultChanceSteps = 1;

    internal static int GetOffspringEvolutionChanceSteps(
        CritterSpecies parentSpecies,
        int baseChanceSteps) =>
        parentSpecies is CritterSpecies.Therapsid
            ? Math.Min(MaximumChanceSteps, baseChanceSteps * 2)
            : baseChanceSteps;

    public static bool TryGetEvolvedSpecies(
        CritterSpecies species,
        out CritterSpecies evolvedSpecies)
        => TryGetEvolvedSpecies(species, branchIndex: 0, out evolvedSpecies);

    public static int GetEvolvedSpeciesCount(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => 3,
        CritterSpecies.Worm => 2,
        CritterSpecies.Trilobite => 2,
        CritterSpecies.Nautilus => 1,
        CritterSpecies.Fish => 1,
        CritterSpecies.Newt => 2,
        CritterSpecies.Therapsid => 4,
        CritterSpecies.Monkey => 1,
        CritterSpecies.Deer => 2,
        CritterSpecies.ToothedWhale => 1,
        _ => 0,
    };

    public static bool TryGetEvolvedSpecies(
        CritterSpecies species,
        int branchIndex,
        out CritterSpecies evolvedSpecies)
    {
        evolvedSpecies = (species, branchIndex) switch
        {
            (CritterSpecies.Plankton, 0) => CritterSpecies.Jellyfish,
            (CritterSpecies.Plankton, 1) => CritterSpecies.Worm,
            (CritterSpecies.Plankton, 2) => CritterSpecies.Trilobite,
            (CritterSpecies.Worm, 0) => CritterSpecies.Fish,
            (CritterSpecies.Worm, 1) => CritterSpecies.Nautilus,
            (CritterSpecies.Trilobite, 0) => CritterSpecies.Crab,
            (CritterSpecies.Trilobite, 1) => CritterSpecies.SeaScorpion,
            (CritterSpecies.Nautilus, 0) => CritterSpecies.Squid,
            (CritterSpecies.Fish, 0) => CritterSpecies.Newt,
            (CritterSpecies.Newt, 0) => CritterSpecies.MegaToad,
            (CritterSpecies.Newt, 1) => CritterSpecies.Therapsid,
            (CritterSpecies.Therapsid, 0) => CritterSpecies.Monkey,
            (CritterSpecies.Therapsid, 1) => CritterSpecies.Deer,
            (CritterSpecies.Therapsid, 2) => CritterSpecies.Wolf,
            (CritterSpecies.Therapsid, 3) => CritterSpecies.ToothedWhale,
            (CritterSpecies.Monkey, 0) => CritterSpecies.Ape,
            (CritterSpecies.Deer, 0) => CritterSpecies.Elk,
            (CritterSpecies.Deer, 1) => CritterSpecies.Gazelle,
            (CritterSpecies.ToothedWhale, 0) => CritterSpecies.BaleenWhale,
            _ => species,
        };
        return evolvedSpecies != species;
    }

    public static bool TryGetDevolvedSpecies(
        CritterSpecies species,
        out CritterSpecies devolvedSpecies)
    {
        devolvedSpecies = species switch
        {
            CritterSpecies.Jellyfish => CritterSpecies.Plankton,
            CritterSpecies.Worm => CritterSpecies.Plankton,
            CritterSpecies.Trilobite => CritterSpecies.Plankton,
            CritterSpecies.Fish => CritterSpecies.Worm,
            CritterSpecies.Nautilus => CritterSpecies.Worm,
            CritterSpecies.Crab => CritterSpecies.Trilobite,
            CritterSpecies.SeaScorpion => CritterSpecies.Trilobite,
            CritterSpecies.Squid => CritterSpecies.Nautilus,
            CritterSpecies.Newt => CritterSpecies.Fish,
            CritterSpecies.MegaToad => CritterSpecies.Newt,
            CritterSpecies.Therapsid => CritterSpecies.Newt,
            CritterSpecies.Monkey => CritterSpecies.Therapsid,
            CritterSpecies.Ape => CritterSpecies.Monkey,
            CritterSpecies.Deer => CritterSpecies.Therapsid,
            CritterSpecies.Elk => CritterSpecies.Deer,
            CritterSpecies.Gazelle => CritterSpecies.Deer,
            CritterSpecies.Wolf => CritterSpecies.Therapsid,
            CritterSpecies.ToothedWhale => CritterSpecies.Therapsid,
            CritterSpecies.BaleenWhale => CritterSpecies.ToothedWhale,
            _ => species,
        };
        return devolvedSpecies != species;
    }

    internal static CritterSpecies ChooseOffspring(
        CritterSpecies parentSpecies,
        int roll,
        int evolutionChanceSteps,
        int branchIndex = 0)
    {
        if (roll is < 0 or >= MaximumChanceSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(roll));
        }
        if (evolutionChanceSteps is < 0 or > MaximumChanceSteps)
        {
            throw new ArgumentOutOfRangeException(nameof(evolutionChanceSteps));
        }

        return roll < evolutionChanceSteps &&
            TryGetEvolvedSpecies(parentSpecies, branchIndex, out var evolvedSpecies)
                ? evolvedSpecies
                : parentSpecies;
    }
}
