namespace Newt.Simulation;

/// <summary>A named world size available to the player.</summary>
public readonly record struct WorldPreset(string Name, int Width, int Height)
{
    public static WorldPreset Micro => new("Micro", 80, 48);

    public static WorldPreset Standard => new("Standard", 160, 96);

    public static WorldPreset Large => new("Large", 252, 130);

    public static WorldPreset Ring => new("Ring World", 504, 40);

    public static WorldPreset Earth => new("Earth", 240, 120);
}
