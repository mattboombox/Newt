using Newt.Simulation;

namespace Newt.Game;

/// <summary>Population-panel totals, shared by live rows and history samples.</summary>
internal static class PopulationCounts
{
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

    internal static int BarbarianApeTotal(IReadOnlyList<int> barbarians) =>
        ApeSpecies.Sum(species => barbarians[(int)species]);

    // Ape barbarians get separate detailed rows; their dogs remain in Dogs.
    internal static int DetailedSpeciesTotal(SimulationWorld world, CritterSpecies species,
        IReadOnlyList<int> barbarians) =>
        world.GetCritterCount(species) - (IsApe(species) ? barbarians[(int)species] : 0);
}
