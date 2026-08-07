namespace Newt.Simulation;

/// <summary>Derives physical terrain from elevation and the climate fields.</summary>
public static class TerrainClassifier
{
    /// <summary>Rebuilds climate, terrain, coastlines, and biome surfaces.</summary>
    public static void RebuildAll(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ClimateSystem.RebuildTemperature(world);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                world.SetTerrain(position, ClassifyLandform(
                    world.GetElevation(position),
                    world.GetTemperature(position)));
            }
        }

        AddCoasts(world);
        ClimateSystem.RebuildMoistureAndBiomes(world);
    }

    private static Terrain ClassifyLandform(float elevation, float temperature)
    {
        if (elevation < -0.20)
        {
            return Terrain.DeepOcean;
        }

        if (elevation <= 0)
        {
            return temperature < ClimateSystem.SeaIceThreshold ? Terrain.Ice : Terrain.Ocean;
        }

        if (elevation > 0.58)
        {
            return Terrain.Mountain;
        }

        if (elevation > 0.34)
        {
            return Terrain.Hills;
        }

        return Terrain.Plains;
    }

    private static void AddCoasts(SimulationWorld world)
    {
        var replacements = new List<(GridPosition Position, Terrain Terrain)>();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                var terrain = world.GetTerrain(position);
                if (terrain is Terrain.Ocean or Terrain.DeepOcean && HasAdjacentLand(world, position))
                {
                    replacements.Add((position, Terrain.Shallows));
                }
                else if (IsLand(terrain) && HasAdjacentWater(world, position))
                {
                    replacements.Add((position, Terrain.Beach));
                }
            }
        }

        foreach (var replacement in replacements)
        {
            world.SetTerrain(replacement.Position, replacement.Terrain);
        }
    }

    private static bool HasAdjacentLand(SimulationWorld world, GridPosition position) =>
        HasAdjacent(world, position, IsLand);

    private static bool HasAdjacentWater(SimulationWorld world, GridPosition position) =>
        HasAdjacent(world, position, terrain => terrain is Terrain.Ocean or Terrain.DeepOcean or Terrain.Ice);

    private static bool HasAdjacent(SimulationWorld world, GridPosition position, Func<Terrain, bool> predicate)
    {
        ReadOnlySpan<GridPosition> directions = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];
        foreach (var direction in directions)
        {
            var y = position.Y + direction.Y;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            var x = (position.X + direction.X + world.Width) % world.Width;
            if (predicate(world.GetTerrain(new GridPosition(x, y))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLand(Terrain terrain) => terrain is
        Terrain.Plains or Terrain.Hills or Terrain.Mountain or Terrain.Beach;
}
