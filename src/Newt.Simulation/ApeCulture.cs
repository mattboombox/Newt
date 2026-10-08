namespace Newt.Simulation;

public enum ApeCulture : byte
{
    Luddite,
    Monarchy,
}

public sealed record ApeCulturalUnit(CritterSpecies Species, int Count, bool Combatant = false);

public sealed record ApeCultureDefinition(
    ApeCulture Culture,
    string Name,
    ApeStructureKind Building,
    IReadOnlyList<ApeCulturalUnit> Units,
    CritterSpecies LeaderSpecies,
    bool ReplacesChieftain = true,
    bool BlocksTechnology = false);

public static class ApeCultures
{
    public static IReadOnlyList<ApeCultureDefinition> All { get; } = Array.AsReadOnly(new[]
    {
        new ApeCultureDefinition(ApeCulture.Luddite, "Luddite", ApeStructureKind.LudditeTemple,
            [new(CritterSpecies.ApeLudditePriest, 3)], CritterSpecies.ApeChieftain,
            ReplacesChieftain: false, BlocksTechnology: true),
        new ApeCultureDefinition(ApeCulture.Monarchy, "Monarchy", ApeStructureKind.Castle,
            [new(CritterSpecies.ApeBishop, 1), new(CritterSpecies.ApeRoyalGuard, 4, Combatant: true)],
            CritterSpecies.ApeKing),
    });

    public static ApeCultureDefinition Get(ApeCulture culture) => All.Single(definition => definition.Culture == culture);
}
