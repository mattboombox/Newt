namespace Newt.Simulation;

/// <summary>The primitive critter evolution tree and reproduction mutations.</summary>
public static class CritterEvolution
{
    public const int ChanceStepsPerPercent = 2;
    public const int MaximumChanceSteps = 100 * ChanceStepsPerPercent;
    public const int DefaultChanceSteps = 5 * ChanceStepsPerPercent;

    public static bool TryGetEvolvedSpecies(
        CritterSpecies species,
        out CritterSpecies evolvedSpecies)
        => TryGetEvolvedSpecies(species, branchIndex: 0, out evolvedSpecies);

    public static int GetEvolvedSpeciesCount(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => 2,
        CritterSpecies.Worm => 1,
        CritterSpecies.Fish => 1,
        CritterSpecies.Newt => 1,
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
            (CritterSpecies.Worm, 0) => CritterSpecies.Fish,
            (CritterSpecies.Fish, 0) => CritterSpecies.Newt,
            (CritterSpecies.Newt, 0) => CritterSpecies.MegaToad,
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
            CritterSpecies.Fish => CritterSpecies.Worm,
            CritterSpecies.Newt => CritterSpecies.Fish,
            CritterSpecies.MegaToad => CritterSpecies.Newt,
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
