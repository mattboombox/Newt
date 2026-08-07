namespace Newt.Simulation;

/// <summary>Deterministic water-flow operations over elevation.</summary>
public static class Hydrology
{
    private const float ElevationTolerance = 0.000_001f;
    private const int DefaultBasinSearchBudget = 4_096;
    private const int DefaultLakeTileBudget = 2_048;

    private static readonly GridPosition[] FlowDirections =
    [
        new(0, -1),
        new(1, -1),
        new(1, 0),
        new(1, 1),
        new(0, 1),
        new(-1, 1),
        new(-1, 0),
        new(-1, -1),
    ];

    private static readonly GridPosition[] BasinDirections =
    [
        new(0, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 0),
    ];

    /// <summary>Starts a spring whose channel will extend by one tile per simulation tick.</summary>
    public static SpringResult StartSpring(
        SimulationWorld world,
        GridPosition source,
        int maximumLength = 2_048)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        if (!world.Contains(source) || IsOcean(world.GetTerrain(source)) || world.GetElevation(source) <= 0)
        {
            var invalid = new SpringResult(SpringTermination.InvalidSource, 0, source);
            world.LastCompletedSpring = invalid;
            return invalid;
        }

        if (world.GetSurfaceWater(source) is not SurfaceWaterKind.None)
        {
            var existing = new SpringResult(SpringTermination.ReachedWatercourse, 0, source);
            world.LastCompletedSpring = existing;
            return existing;
        }

        world.SetSurfaceWater(source, SurfaceWaterKind.River);
        world.SetWaterSurfaceElevation(source, null);
        world.ActiveSprings.Add(new ActiveSpring(source, maximumLength));
        return new SpringResult(SpringTermination.Flowing, 1, source);
    }

    /// <summary>Convenience operation that completes a spring immediately for generation and tests.</summary>
    public static SpringResult TraceSpring(
        SimulationWorld world,
        GridPosition source,
        int maximumLength = 2_048)
    {
        var started = StartSpring(world, source, maximumLength);
        if (started.Termination is not SpringTermination.Flowing)
        {
            return started;
        }

        while (world.ActiveSpringCount > 0)
        {
            AdvanceSprings(world);
        }

        return world.LastCompletedSpring ?? started;
    }

    internal static void AdvanceSprings(SimulationWorld world)
    {
        var climateChanged = false;
        for (var index = world.ActiveSprings.Count - 1; index >= 0; index--)
        {
            var spring = world.ActiveSprings[index];
            var result = AdvanceSpring(world, spring);
            if (result is null)
            {
                continue;
            }

            world.LastCompletedSpring = result;
            world.ActiveSprings.RemoveAt(index);
            climateChanged = true;
        }

        // Rebuild once after all springs completing on this tick. A growing
        // channel does not pay the world-scale climate cost on every river tile.
        if (climateChanged)
        {
            TerrainClassifier.RebuildAll(world);
        }
    }

    /// <summary>
    /// Fills the sink to the lowest reachable spill elevation within explicit
    /// search and lake-area budgets.
    /// </summary>
    public static LakeFillResult FillBasin(
        SimulationWorld world,
        GridPosition sink,
        int searchBudget = DefaultBasinSearchBudget,
        int lakeTileBudget = DefaultLakeTileBudget)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(sink) || IsOcean(world.GetTerrain(sink)))
        {
            return default;
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(searchBudget);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lakeTileBudget);

        var frontier = new PriorityQueue<GridPosition, float>();
        var bestSpill = new Dictionary<GridPosition, float> { [sink] = world.GetElevation(sink) };
        var previous = new Dictionary<GridPosition, GridPosition>();
        frontier.Enqueue(sink, world.GetElevation(sink));
        GridPosition? exit = null;
        var examined = 0;

        while (frontier.TryDequeue(out var current, out var currentSpill) && examined++ < searchBudget)
        {
            if (currentSpill > bestSpill[current] + ElevationTolerance)
            {
                continue;
            }

            foreach (var direction in FlowDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null)
                {
                    continue;
                }

                if (IsOcean(world.GetTerrain(neighbor.Value)))
                {
                    exit = current;
                    frontier.Clear();
                    break;
                }

                var candidateSpill = Math.Max(currentSpill, world.GetElevation(neighbor.Value));
                if (bestSpill.TryGetValue(neighbor.Value, out var knownSpill) &&
                    candidateSpill >= knownSpill - ElevationTolerance)
                {
                    continue;
                }

                bestSpill[neighbor.Value] = candidateSpill;
                previous[neighbor.Value] = current;
                frontier.Enqueue(neighbor.Value, candidateSpill);
            }
        }

        if (exit is null)
        {
            return CreateTerminalLake(world, sink);
        }

        var spillElevation = bestSpill[exit.Value];
        var path = ReconstructPath(previous, sink, exit.Value);
        var outletIndex = path.FindIndex(position =>
            world.GetElevation(position) >= spillElevation - ElevationTolerance);
        if (outletIndex < 0)
        {
            outletIndex = path.Count - 1;
        }

        var outlet = path[outletIndex];
        var outletConnection = outletIndex > 0 ? path[outletIndex - 1] : sink;
        var lakeTiles = FindLakeTiles(world, sink, spillElevation, lakeTileBudget);
        if (lakeTiles is null || lakeTiles.Count == 0)
        {
            return CreateTerminalLake(world, sink);
        }

        foreach (var position in lakeTiles)
        {
            world.SetSurfaceWater(position, SurfaceWaterKind.FreshwaterLake);
            world.SetWaterSurfaceElevation(position, spillElevation);
        }

        return new LakeFillResult(true, lakeTiles.Count, spillElevation, outlet, outletConnection);
    }

    private static SpringResult? AdvanceSpring(SimulationWorld world, ActiveSpring spring)
    {
        var current = spring.Current;
        foreach (var direction in FlowDirections)
        {
            var neighbor = GetNeighbor(world, current, direction);
            if (neighbor is not null && IsOcean(world.GetTerrain(neighbor.Value)))
            {
                Connect(world, current, neighbor.Value);
                return Completed(SpringTermination.ReachedOcean, spring);
            }
        }

        GridPosition? lowest = null;
        var lowestElevation = world.GetElevation(current);
        foreach (var direction in FlowDirections)
        {
            var candidate = GetNeighbor(world, current, direction);
            if (candidate is null || spring.Visited.Contains(candidate.Value))
            {
                continue;
            }

            if (world.GetSurfaceWater(candidate.Value) is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake)
            {
                Connect(world, current, candidate.Value);
                return Completed(SpringTermination.ReachedWatercourse, spring);
            }

            var elevation = world.GetElevation(candidate.Value);
            if (elevation < lowestElevation)
            {
                lowestElevation = elevation;
                lowest = candidate;
            }
        }

        if (lowest is null)
        {
            var lake = FillBasin(world, current);
            return Completed(
                lake.Created ? SpringTermination.FormedLake : SpringTermination.Basin,
                spring);
        }

        Connect(world, current, lowest.Value);
        world.SetSurfaceWater(lowest.Value, SurfaceWaterKind.River);
        world.SetWaterSurfaceElevation(lowest.Value, null);
        spring.Current = lowest.Value;
        spring.Visited.Add(lowest.Value);
        if (spring.Visited.Count >= spring.MaximumLength)
        {
            return Completed(SpringTermination.MaximumLength, spring);
        }

        return null;
    }

    private static SpringResult Completed(SpringTermination termination, ActiveSpring spring) =>
        new(termination, spring.Visited.Count, spring.Current);

    private static LakeFillResult CreateTerminalLake(SimulationWorld world, GridPosition sink)
    {
        var surface = world.GetElevation(sink) + 0.001f;
        world.SetSurfaceWater(sink, SurfaceWaterKind.FreshwaterLake);
        world.SetWaterSurfaceElevation(sink, surface);
        return new LakeFillResult(true, 1, surface, null, null);
    }

    private static List<GridPosition> ReconstructPath(
        Dictionary<GridPosition, GridPosition> previous,
        GridPosition start,
        GridPosition end)
    {
        var path = new List<GridPosition> { end };
        var current = end;
        while (current != start && previous.TryGetValue(current, out var prior))
        {
            current = prior;
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    private static List<GridPosition>? FindLakeTiles(
        SimulationWorld world,
        GridPosition sink,
        float spillElevation,
        int tileBudget)
    {
        var lake = new List<GridPosition>();
        var visited = new HashSet<GridPosition> { sink };
        var queue = new Queue<GridPosition>();
        queue.Enqueue(sink);

        while (queue.TryDequeue(out var current))
        {
            if (lake.Count >= tileBudget)
            {
                return null;
            }

            if (current != sink && world.GetElevation(current) >= spillElevation - ElevationTolerance)
            {
                continue;
            }

            lake.Add(current);
            foreach (var direction in BasinDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null || visited.Contains(neighbor.Value) ||
                    IsOcean(world.GetTerrain(neighbor.Value)))
                {
                    continue;
                }

                visited.Add(neighbor.Value);
                queue.Enqueue(neighbor.Value);
            }
        }

        return lake;
    }

    private static void Connect(SimulationWorld world, GridPosition from, GridPosition to)
    {
        var deltaX = to.X - from.X;
        if (Math.Abs(deltaX) > 1)
        {
            deltaX = deltaX > 0 ? -1 : 1;
        }
        var deltaY = Math.Sign(to.Y - from.Y);
        deltaX = Math.Sign(deltaX);

        world.AddRiverConnection(from, ToConnection(deltaX, deltaY));
        world.AddRiverConnection(to, ToConnection(-deltaX, -deltaY));
    }

    private static RiverConnection ToConnection(int deltaX, int deltaY) => (deltaX, deltaY) switch
    {
        (0, -1) => RiverConnection.North,
        (1, -1) => RiverConnection.NorthEast,
        (1, 0) => RiverConnection.East,
        (1, 1) => RiverConnection.SouthEast,
        (0, 1) => RiverConnection.South,
        (-1, 1) => RiverConnection.SouthWest,
        (-1, 0) => RiverConnection.West,
        (-1, -1) => RiverConnection.NorthWest,
        _ => RiverConnection.None,
    };

    private static GridPosition? GetNeighbor(
        SimulationWorld world,
        GridPosition position,
        GridPosition direction)
    {
        var y = position.Y + direction.Y;
        if (y < 0 || y >= world.Height)
        {
            return null;
        }

        var x = Mod(position.X + direction.X, world.Width);
        return new GridPosition(x, y);
    }

    private static bool IsOcean(Terrain terrain) => terrain is
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice;

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
