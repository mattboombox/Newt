using Newt.Simulation;

namespace Newt.Game;

/// <summary>Population-panel totals, shared by live rows and history samples.</summary>
internal static class PopulationCounts
{
    internal enum VillageSort { Age, Population }

    internal static IReadOnlyList<VillagePopulationSnapshot> SortVillages(
        IEnumerable<VillagePopulationSnapshot> villages, VillageSort sort, bool reverse)
    {
        var ordered = sort == VillageSort.Population
            ? reverse ? villages.OrderBy(village => village.ApeCount) : villages.OrderByDescending(village => village.ApeCount)
            : reverse ? villages.OrderByDescending(village => village.FoundedTick) : villages.OrderBy(village => village.FoundedTick);
        return ordered.ThenBy(village => village.IsNecropolis).ThenBy(village => Math.Abs(village.Id)).ToArray();
    }
    internal enum Selection { Species, CondensedApes, Barbarians, BarbarianChiefs, SickApes, Colonists }

    private static CritterSpecies ChartSpecies(CritterSpecies species) =>
        species == CritterSpecies.ApeTraderSailor ? CritterSpecies.ApeTrader : species;

    private static CritterSpecies VillageRowSpecies(CritterSpecies species, bool barbarian) =>
        barbarian && IsCondensedApe(species) && species != CritterSpecies.ApeChieftain
            ? CritterSpecies.ApeWarrior : ChartSpecies(species);

    internal static Dictionary<CritterSpecies, int> VillageRows(VillagePopulationSnapshot village)
    {
        var rows = new Dictionary<CritterSpecies, int>();
        foreach (var (species, count) in village.Counts)
        {
            if (count <= 0) continue;
            var key = VillageRowSpecies(species, village.Barbarian);
            rows[key] = rows.GetValueOrDefault(key) + count;
        }
        return rows;
    }

    internal static CritterSnapshot? FindCritter(SimulationWorld world, CritterSpecies species,
        Selection selection = Selection.Species, GridPosition? village = null, CritterId previous = default)
    {
        CritterSnapshot? match = null;
        CritterSnapshot? fallback = null;
        CritterSnapshot? previousMatch = null;
        CritterSnapshot? previousFallback = null;
        var matchCount = 0;
        var fallbackCount = 0;
        for (var index = 0; index < world.CritterCount; index++)
        {
            var critter = world.GetCritter(index);
            if (village is { } home)
            {
                var populationHome = critter.Species is CritterSpecies.Lich or CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior
                    ? world.GetNecropolisPopulationHome(critter.Id) : world.GetApeHomeVillage(critter.Id);
                if (populationHome != home) continue;
                if (VillageRowSpecies(critter.Species, world.IsBarbarianApe(critter.Id)) == species)
                    Sample(critter, ref match, ref previousMatch, ref matchCount);
                else if (IsCondensedApe(critter.Species))
                    Sample(critter, ref fallback, ref previousFallback, ref fallbackCount);
                continue;
            }
            var barbarian = world.IsBarbarianApe(critter.Id);
            var matches = selection switch
            {
                Selection.CondensedApes => IsCondensedApe(critter.Species),
                Selection.Barbarians => barbarian && IsApe(critter.Species) && critter.Species != CritterSpecies.ApeChieftain,
                Selection.BarbarianChiefs => barbarian && critter.Species == CritterSpecies.ApeChieftain,
                Selection.SickApes => IsCondensedApe(critter.Species) && critter.Plague != PlagueKind.None,
                Selection.Colonists => critter.Species == CritterSpecies.Ape && critter.IsColonist,
                _ => ChartSpecies(critter.Species) == ChartSpecies(species) && (!IsApe(species) || !barbarian) &&
                    !(critter.Species == CritterSpecies.Ape && critter.IsColonist),
            };
            if (matches) Sample(critter, ref match, ref previousMatch, ref matchCount);
        }
        return match ?? previousMatch ?? fallback ?? previousFallback;

        // Reservoir sampling keeps each eligible critter equally likely without
        // allocating a list or consuming the simulation's random sequence.
        void Sample(CritterSnapshot critter, ref CritterSnapshot? chosen,
            ref CritterSnapshot? repeated, ref int count)
        {
            if (critter.Id == previous)
            {
                repeated = critter;
                return;
            }
            if (Random.Shared.Next(++count) == 0) chosen = critter;
        }
    }

    private static readonly CritterSpecies[] ApeSpecies = Enum.GetValues<CritterSpecies>().Where(IsApe).ToArray();

    internal static bool IsApe(CritterSpecies species) => species is
        CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or
        CritterSpecies.ApeScholar or CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack or
        CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or
        CritterSpecies.ApeBishop or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeLudditePriest or
        CritterSpecies.ApeBomber or CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor or
        CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior;

    // Species totals already include all factions, pirates, colonists, veterans,
    // and sick individuals. Adding faction or sickness subtotals would count twice.
    internal static int ApeTotal(SimulationWorld world) => ApeSpecies.Sum(world.GetCritterCount);

    internal static bool IsCondensedApe(CritterSpecies species) =>
        IsApe(species) && species is not (CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior);

    internal static int CondensedApeTotal(SimulationWorld world) =>
        ApeSpecies.Where(IsCondensedApe).Sum(world.GetCritterCount);
    internal static int BarbarianApeTotal(IReadOnlyList<int> barbarians) =>
        ApeSpecies.Sum(species => barbarians[(int)species]);

    // Barbarians and colonists get separate detailed rows; camp dogs remain in Dogs.
    internal static int DetailedSpeciesTotal(SimulationWorld world, CritterSpecies species,
        IReadOnlyList<int> barbarians) =>
        species == CritterSpecies.ApeTraderSailor ? 0 :
        world.GetCritterCount(species) - (IsApe(species) ? barbarians[(int)species] : 0) -
        (species == CritterSpecies.Ape ? world.ApeColonistCount : 0) +
        (species == CritterSpecies.ApeTrader
            ? world.GetCritterCount(CritterSpecies.ApeTraderSailor) - barbarians[(int)CritterSpecies.ApeTraderSailor] : 0);
}
