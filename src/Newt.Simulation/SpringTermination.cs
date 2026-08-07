namespace Newt.Simulation;

/// <summary>Explains why a spring trace stopped.</summary>
public enum SpringTermination : byte
{
    Flowing,
    InvalidSource,
    ReachedOcean,
    ReachedWatercourse,
    FormedLake,
    Basin,
    MaximumLength,
}
