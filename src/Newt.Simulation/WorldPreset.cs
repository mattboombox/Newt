namespace Newt.Simulation;

/// <summary>A named world size available to the player.</summary>
public readonly record struct WorldPreset(string Name, int Width, int Height)
{
    public static WorldPreset Micro => new("Micro", 80, 48);

    public static WorldPreset Small => new("Small", 160, 96);

    public static WorldPreset Standard => new("Standard", 320, 192);

    // Fits a 2560x1440 display above the HUD at the 4px zoom level with margin.
    public static WorldPreset Large => new("Large", 640, 311);

    public static WorldPreset Ring => new("Ring World", 1280, 44);

    public static WorldPreset RingLarge => new("Large", Ring.Width * 2, Ring.Height);

    public static WorldPreset RingMega => new("Mega", Ring.Width * 4, Ring.Height);

    public static WorldPreset ONeillCylinder => new("Standard", 448, 336);

    public static WorldPreset ONeillCylinderLarge => new("Large", ONeillCylinder.Width, ONeillCylinder.Height * 2);

    public static WorldPreset ONeillCylinderMega => new("Mega", 512, ONeillCylinder.Height * 4);

    public static WorldPreset Nexus => new("Nexus", 384, 384);

    public static WorldPreset NexusMega => new("Mega", 768, 768);

    public static WorldPreset PlanetoidMicro => new("Micro", 72, 48);

    public static WorldPreset PlanetoidSmall => new("Small", 144, 96);

    public static WorldPreset PlanetoidStandard => new("Standard", 288, 192);

    public static WorldPreset PlanetoidLarge => new("Large", 576, 384);

    public static WorldPreset PlanetoidHuge => new("Huge", 1152, 768);

    public static WorldPreset Earth => new("Earth", 1280, 642);

    // Stress-test preset: fills a 2560x1440 display above the 156px HUD at 2px per tile.
    public static WorldPreset Huge => new("Huge", 1280, 642);
}
