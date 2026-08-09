namespace Newt.Simulation;

/// <summary>Derives physical terrain from elevation and the climate fields.</summary>
public static class TerrainClassifier
{
    /// <summary>Rebuilds climate, terrain, coastlines, and biome surfaces.</summary>
    public static void RebuildAll(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        RebuildLandforms(world);
        ClimateSystem.RebuildMoistureAndBiomes(world);
    }

    /// <summary>Rebuilds physical terrain without using the current freshwater layout.</summary>
    internal static void RebuildLandforms(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        ClimateSystem.RebuildTemperature(world);

        var ocean = FindOceanConnectedTiles(world);

        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                var index = y * world.Width + x;
                world.SetTerrain(position, ocean[index]
                    ? ClassifyOcean(world.SeaLevel - world.GetElevation(position), world.GetTemperature(position))
                    : ClassifyDryLand(world.GetElevation(position)));
            }
        }

        AddCoasts(world);
    }

    private static bool[] FindOceanConnectedTiles(SimulationWorld world)
    {
        var connected = new bool[checked(world.Width * world.Height)];
        var frontier = new Queue<GridPosition>();

        TryVisit(world.OceanSeed.X, world.OceanSeed.Y);

        while (frontier.TryDequeue(out var current))
        {
            TryVisit((current.X + 1) % world.Width, current.Y);
            TryVisit((current.X - 1 + world.Width) % world.Width, current.Y);
            if (current.Y > 0)
            {
                TryVisit(current.X, current.Y - 1);
            }
            if (current.Y + 1 < world.Height)
            {
                TryVisit(current.X, current.Y + 1);
            }
        }

        return connected;

        void TryVisit(int x, int y)
        {
            var index = y * world.Width + x;
            if (connected[index] || world.GetElevation(new GridPosition(x, y)) > world.SeaLevel)
            {
                return;
            }

            connected[index] = true;
            frontier.Enqueue(new GridPosition(x, y));
        }
    }

    private static Terrain ClassifyOcean(float depth, float temperature)
    {
        if (depth > 0.20f)
        {
            return Terrain.DeepOcean;
        }

        return temperature < ClimateSystem.SeaIceThreshold ? Terrain.Ice : Terrain.Ocean;
    }

    private static Terrain ClassifyDryLand(float elevation)
    {
        if (elevation < -0.45f)
        {
            return Terrain.Trench;
        }

        if (elevation < -0.20f)
        {
            return Terrain.Canyon;
        }

        if (elevation <= 0)
        {
            return Terrain.Lowlands;
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
        Terrain.Plains or Terrain.Hills or Terrain.Mountain or Terrain.Beach or
        Terrain.Lowlands or Terrain.Canyon or Terrain.Trench;
}
