namespace Newt.Simulation;

/// <summary>Fixed geometry and engineered climates of the ten-petal Nexus.</summary>
public static class NexusLayout
{
    public const int ArmCount = 10;
    private static readonly (float Temperature, float Moisture)[] Climates =
    [
        (0.06f, 0.5f), (0.26f, 0.12f), (0.26f, 0.5f), (0.26f, 0.9f),
        (0.52f, 0.12f), (0.52f, 0.5f), (0.52f, 0.9f),
        (0.84f, 0.12f), (0.84f, 0.5f), (0.84f, 0.9f),
    ];

    public static GridPosition Center(SimulationWorld world) => new(world.Width / 2, world.Height / 2);
    public static float Radius(SimulationWorld world) => Math.Min(world.Width, world.Height) * 0.46f;

    public static GridPosition ArmPosition(SimulationWorld world, int arm, float distance = 0.72f)
    {
        var angle = arm * MathF.Tau / ArmCount - MathF.PI / 2;
        var center = Center(world);
        return new(center.X + (int)MathF.Round(MathF.Cos(angle) * Radius(world) * distance),
            center.Y + (int)MathF.Round(MathF.Sin(angle) * Radius(world) * distance));
    }

    public static bool Contains(SimulationWorld world, GridPosition position)
    {
        if (!world.Contains(position)) return false;
        var center = Center(world);
        var dx = position.X - center.X;
        var dy = position.Y - center.Y;
        var angle = MathF.Atan2(dy, dx) + MathF.PI / 2;
        var edge = Radius(world) * (0.68f + 0.32f * MathF.Cos(ArmCount * angle));
        return dx * dx + dy * dy <= edge * edge;
    }

    internal static bool IsWall(SimulationWorld world, GridPosition position)
    {
        if (!Contains(world, position)) return true;
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
            if (!Contains(world, new(position.X + dx, position.Y + dy))) return true;
        return false;
    }

    internal static (float Temperature, float Moisture) Climate(SimulationWorld world, GridPosition position)
    {
        var center = Center(world);
        var dx = position.X - center.X;
        var dy = position.Y - center.Y;
        var angle = MathF.Atan2(dy, dx) + MathF.PI / 2;
        var sector = angle * ArmCount / MathF.Tau + 0.5f;
        var cell = (int)MathF.Floor(sector);
        var fraction = sector - MathF.Floor(sector);
        var arm = (cell % ArmCount + ArmCount) % ArmCount;
        var temperature = Climates[arm].Temperature;
        // Blend a narrow band on each side of a petal boundary, including the angular seam.
        const float bleed = 0.12f;
        if (fraction < bleed)
        {
            var previous = Climates[(arm + ArmCount - 1) % ArmCount].Temperature;
            var blend = Smooth(0.5f + fraction / (2 * bleed));
            temperature = previous + (temperature - previous) * blend;
        }
        else if (fraction > 1 - bleed)
        {
            var next = Climates[(arm + 1) % ArmCount].Temperature;
            temperature += (next - temperature) * Smooth((fraction - (1 - bleed)) / (2 * bleed));
        }
        var radius = MathF.Sqrt(dx * dx + dy * dy) / Radius(world);
        temperature = 0.52f + (temperature - 0.52f) * Smooth(Math.Clamp((radius - 0.12f) / 0.20f, 0, 1));
        return (temperature, radius < 0.26f ? 0.5f : Climates[arm].Moisture);
    }

    internal static int ArmIndex(SimulationWorld world, GridPosition position)
    {
        var center = Center(world);
        var angle = MathF.Atan2(position.Y - center.Y, position.X - center.X) + MathF.PI / 2;
        var arm = (int)MathF.Floor(angle * ArmCount / MathF.Tau + 0.5f);
        return (arm % ArmCount + ArmCount) % ArmCount;
    }

    private static float Smooth(float amount) => amount * amount * (3 - 2 * amount);

    internal static void ApplyWalls(SimulationWorld world)
    {
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            if (IsWall(world, new(x, y)))
                world.SetStructuralTerrain(new(x, y), Terrain.MegaWall, SimulationWorld.MegaWallElevation);
    }
}
