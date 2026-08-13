namespace Newt.Simulation;

/// <summary>A named world size available to the player.</summary>
public readonly record struct WorldPreset(string Name, int Width, int Height)
{
    public static WorldPreset Micro => new("Micro", 80, 48);

    public static WorldPreset Standard => new("Standard", 160, 96);

    public static WorldPreset Large => new("Large", 320, 192);

    // Fits a 2560x1440 display above the HUD at the 4px zoom level with margin.
    public static WorldPreset Huge => new("Huge", 640, 311);

    public static WorldPreset Ring => new("Ring World", 1280, 40);

    public static WorldPreset Earth => new("Earth", 1280, 642);

    // Stress-test preset: fills a 2560x1440 display above the 156px HUD at 2px per tile.
    public static WorldPreset Massive => new("Massive", 1280, 642);
}
