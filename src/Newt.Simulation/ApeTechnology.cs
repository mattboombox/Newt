namespace Newt.Simulation;

public enum ApeTechnology : byte
{
    Sailing,
    Aquaculture,
}

public sealed record ApeTechnologyDefinition(
    ApeTechnology Technology,
    string Name,
    IReadOnlyList<ApeTechnology> Prerequisites);

public static class ApeTechnologies
{
    public static IReadOnlyList<ApeTechnologyDefinition> All { get; } =
        Array.AsReadOnly(new[]
        {
            new ApeTechnologyDefinition(ApeTechnology.Sailing, "Sailing", Array.Empty<ApeTechnology>()),
            new ApeTechnologyDefinition(ApeTechnology.Aquaculture, "Aquaculture", Array.Empty<ApeTechnology>()),
        });
}
