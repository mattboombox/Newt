namespace Newt.Simulation;

/// <summary>Deterministic water-flow operations over elevation.</summary>
public static class Hydrology
{
    private const float ElevationTolerance = 0.000_001f;
    // A maximum-magnitude crater on the 1280 x 642 presets can contain more
    // than 40,000 bowl tiles. These bounds cover that local feature without
    // permitting an outletless depression to flood an entire maximum-size world.
    private const int DefaultBasinSearchBudget = 1_048_576;
    private const int DefaultLakeTileBudget = 65_536;
    private const int TerminalLakeTileBudget = 128;

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

    /// <summary>Starts a natural or player spring on a hill or mountain.</summary>
    public static SpringResult StartSnowmeltSpring(
        SimulationWorld world,
        GridPosition source,
        SpringOrigin origin = SpringOrigin.Natural)
    {
        ArgumentNullException.ThrowIfNull(world);
        var validSource = origin is SpringOrigin.Player
            ? IsPlayerRiverSource(world, source)
            : IsSnowmeltSource(world, source);
        if (!validSource)
        {
            var invalid = new SpringResult(SpringTermination.InvalidSource, 0, source);
            world.LastCompletedSpring = invalid;
            return invalid;
        }

        return StartSpring(world, source, origin);
    }

    public static bool IsSnowmeltSource(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        return IsUplandRiverSource(world, position, allowHills: true);
    }

    private static bool IsPlayerRiverSource(SimulationWorld world, GridPosition position) =>
        IsUplandRiverSource(world, position, allowHills: true);

    private static bool IsWatershedSource(SimulationWorld world, GridPosition position) =>
        IsUplandRiverSource(world, position, allowHills: true);

    private static bool IsUplandRiverSource(
        SimulationWorld world,
        GridPosition position,
        bool allowHills)
    {
        if (!world.Contains(position) ||
            world.GetTerrain(position) is not Terrain.Mountain &&
                (!allowHills || world.GetTerrain(position) is not Terrain.Hills) ||
            world.GetSurfaceCover(position) is SurfaceCover.Lava)
        {
            return false;
        }

        return true;
    }

    /// <summary>Starts a spring whose channel will extend by one tile per simulation tick.</summary>
    public static SpringResult StartSpring(
        SimulationWorld world,
        GridPosition source,
        SpringOrigin origin = SpringOrigin.Natural)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!IsValidSource(world, source))
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
        world.RegisterSpringSource(source, origin);
        world.ActiveSprings.Add(new ActiveSpring(source));
        return new SpringResult(SpringTermination.Flowing, 1, source);
    }

    /// <summary>Convenience operation that completes a spring immediately for generation and tests.</summary>
    public static SpringResult TraceSpring(
        SimulationWorld world,
        GridPosition source,
        SpringOrigin origin = SpringOrigin.Natural)
    {
        var started = StartSpring(world, source, origin);
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

    /// <summary>
    /// Instantly redraws all freshwater from its persistent sources after landforms change.
    /// </summary>
    public static void RebuildFreshwater(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var sources = world.SpringSources.ToArray();
        world.ClearFreshwater();
        world.LastCompletedSpring = null;

        foreach (var source in sources)
        {
            if (!IsValidSource(world, source.Position) ||
                world.GetSurfaceWater(source.Position) is not SurfaceWaterKind.None)
            {
                continue;
            }

            world.SetSurfaceWater(source.Position, SurfaceWaterKind.River);
            var spring = new ActiveSpring(source.Position);
            SpringResult? result;
            do
            {
                result = AdvanceSpring(world, spring);
            }
            while (result is null);

            world.LastCompletedSpring = result;
        }

        ClimateSystem.RebuildMoistureAndBiomes(world);
    }

    /// <summary>
    /// Dries one natural watershed and starts a replacement from a different
    /// hill or mountain. If none exists yet, seeds one without removing player rivers.
    /// </summary>
    public static bool ShiftNaturalWatershed(SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var naturalSources = world.SpringSources
            .Where(source => source.Origin is SpringOrigin.Natural)
            .ToArray();
        if (naturalSources.Length == 0)
        {
            var newSource = SelectNaturalSpringReplacement(world, removedSource: null);
            return newSource is not null &&
                StartSpring(world, newSource.Value, SpringOrigin.Natural).Termination is
                    SpringTermination.Flowing;
        }

        var removed = naturalSources[world.NextInt(naturalSources.Length)];
        if (!world.RemoveNaturalSpringSource(removed.Position))
        {
            return false;
        }

        RebuildFreshwater(world);
        var replacement = SelectNaturalSpringReplacement(world, removed.Position);
        if (replacement is not null &&
            StartSpring(world, replacement.Value, SpringOrigin.Natural).Termination is
                SpringTermination.Flowing)
        {
            return true;
        }

        // A shift is atomic: restore the old source if a new watershed cannot
        // actually be started after the remaining rivers have been rebuilt.
        world.RegisterSpringSource(removed.Position, SpringOrigin.Natural);
        RebuildFreshwater(world);
        return false;
    }

    private static GridPosition? SelectNaturalSpringReplacement(
        SimulationWorld world,
        GridPosition? removedSource)
    {
        GridPosition? selected = null;
        var candidateCount = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (position == removedSource ||
                    !IsWatershedSource(world, position) ||
                    world.GetSurfaceWater(position) is not SurfaceWaterKind.None ||
                    world.IsOccupied(position))
                {
                    continue;
                }

                candidateCount++;
                if (world.NextInt(candidateCount) == 0)
                {
                    selected = position;
                }
            }
        }

        return selected;
    }

    /// <summary>Removes the connected river and lake system at a freshwater tile.</summary>
    public static bool RemoveFreshwaterAt(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position) ||
            world.GetSurfaceWater(position) is SurfaceWaterKind.None)
        {
            return false;
        }

        var connectedWater = FindConnectedFreshwater(world, position);
        world.RemoveSpringSources(connectedWater);
        RebuildFreshwater(world);
        return true;
    }

    internal static void AdvanceSprings(SimulationWorld world)
    {
        var lakeClimateChanged = false;
        var springCompleted = false;
        for (var index = world.ActiveSprings.Count - 1; index >= 0; index--)
        {
            var spring = world.ActiveSprings[index];
            var result = AdvanceSpring(world, spring);
            if (spring.ClimateRefreshPending)
            {
                spring.ClimateRefreshPending = false;
                lakeClimateChanged = true;
            }
            if (result is null)
            {
                continue;
            }

            world.LastCompletedSpring = result;
            world.ActiveSprings.RemoveAt(index);
            springCompleted = true;
        }

        // Rebuild once after all springs completing on this tick. A growing
        // channel does not pay the world-scale climate cost on every river tile.
        if (springCompleted)
        {
            TerrainClassifier.RebuildAll(world);
        }
        else if (lakeClimateChanged)
        {
            ClimateSystem.RebuildMoistureAndBiomes(world);
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
        => FillBasin(world, sink, searchBudget, lakeTileBudget, blocked: null);

    private static LakeFillResult FillBasin(
        SimulationWorld world,
        GridPosition sink,
        int searchBudget,
        int lakeTileBudget,
        IReadOnlySet<GridPosition>? blocked)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(sink) || IsOcean(world.GetTerrain(sink)))
        {
            return default;
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(searchBudget);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lakeTileBudget);

        var tileCount = checked(world.Width * world.Height);
        var sinkIndex = ToIndex(world, sink);
        var frontier = new PriorityQueue<int, (float Spill, int EstimatedTotal, int Steps)>();
        var bestSpill = new float[tileCount];
        var bestSteps = new int[tileCount];
        var previous = new int[tileCount];
        var settled = new bool[tileCount];
        Array.Fill(bestSpill, float.PositiveInfinity);
        Array.Fill(bestSteps, int.MaxValue);
        Array.Fill(previous, -1);
        bestSpill[sinkIndex] = world.GetElevation(sink);
        bestSteps[sinkIndex] = 0;
        frontier.Enqueue(
            sinkIndex,
            (bestSpill[sinkIndex], EstimateOceanRouteLength(world, sink, 0), 0));
        var exitIndex = -1;
        var localExitIndex = -1;
        var examined = 0;

        while (frontier.TryDequeue(out var currentIndex, out var priority) &&
            examined++ < Math.Min(searchBudget, tileCount))
        {
            var currentSpill = priority.Spill;
            var currentSteps = priority.Steps;
            if (currentSpill > bestSpill[currentIndex] + ElevationTolerance ||
                Math.Abs(currentSpill - bestSpill[currentIndex]) <= ElevationTolerance &&
                currentSteps != bestSteps[currentIndex])
            {
                continue;
            }
            var current = FromIndex(world, currentIndex);
            settled[currentIndex] = true;

            foreach (var direction in FlowDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null)
                {
                    continue;
                }

                if (neighbor.Value != sink && blocked?.Contains(neighbor.Value) is true)
                {
                    continue;
                }

                if (IsOcean(world.GetTerrain(neighbor.Value)))
                {
                    exitIndex = currentIndex;
                    frontier.Clear();
                    break;
                }

                var neighborIndex = ToIndex(world, neighbor.Value);
                if (localExitIndex < 0 && currentIndex != sinkIndex &&
                    world.GetElevation(current) >= currentSpill - ElevationTolerance &&
                    !settled[neighborIndex] &&
                    world.GetElevation(neighbor.Value) < currentSpill - ElevationTolerance)
                {
                    // Preserve a local outlet for oceanless test/sandbox worlds.
                    // Ocean-bearing worlds continue searching so crater
                    // roughness cannot masquerade as the true enclosing rim.
                    localExitIndex = currentIndex;
                }

                var candidateSpill = Math.Max(currentSpill, world.GetElevation(neighbor.Value));
                var candidateSteps = currentSteps + 1;
                var improvesSpill = candidateSpill < bestSpill[neighborIndex] - ElevationTolerance;
                var tiesWithShorterRoute =
                    Math.Abs(candidateSpill - bestSpill[neighborIndex]) <= ElevationTolerance &&
                    candidateSteps < bestSteps[neighborIndex];
                if (!improvesSpill && !tiesWithShorterRoute)
                {
                    continue;
                }

                bestSpill[neighborIndex] = candidateSpill;
                bestSteps[neighborIndex] = candidateSteps;
                previous[neighborIndex] = currentIndex;
                frontier.Enqueue(
                    neighborIndex,
                    (candidateSpill,
                        EstimateOceanRouteLength(world, neighbor.Value, candidateSteps),
                        candidateSteps));
            }
        }

        if (exitIndex < 0)
        {
            exitIndex = localExitIndex;
        }

        if (exitIndex < 0)
        {
            return CreateTerminalLake(world, sink, Math.Min(lakeTileBudget, TerminalLakeTileBudget));
        }

        var spillElevation = bestSpill[exitIndex];
        var path = ReconstructPath(world, previous, sinkIndex, exitIndex);
        var lakeTiles = FindLakeTiles(world, sink, spillElevation, lakeTileBudget, blocked);
        if (lakeTiles is null || lakeTiles.Count == 0)
        {
            return CreateTerminalLake(world, sink, Math.Min(lakeTileBudget, TerminalLakeTileBudget));
        }

        foreach (var position in lakeTiles)
        {
            world.SetSurfaceWater(position, SurfaceWaterKind.FreshwaterLake);
            world.SetWaterSurfaceElevation(position, spillElevation);
        }

        var lakeSet = lakeTiles.ToHashSet();
        var outletIndex = path.FindIndex(position => !lakeSet.Contains(position));
        if (outletIndex < 0)
        {
            return new LakeFillResult(true, lakeTiles.Count, spillElevation, null, null);
        }

        var outlet = path[outletIndex];
        var outletConnection = outletIndex > 0 ? path[outletIndex - 1] : sink;
        return new LakeFillResult(
            true,
            lakeTiles.Count,
            spillElevation,
            outlet,
            outletConnection,
            path.Skip(outletIndex).ToArray());
    }

    private static SpringResult? AdvanceSpring(SimulationWorld world, ActiveSpring spring)
    {
        var current = spring.Current;
        if (spring.PlannedRoute.TryPeek(out var nextPlanned) &&
            world.GetElevation(nextPlanned) < world.GetElevation(current) - ElevationTolerance)
        {
            // The minimax route is only needed to cross the lake's rim or a flat
            // spillway. Once it descends again, normal routing must take over so
            // a later depression can form its own lake instead of being bridged.
            spring.PlannedRoute.Clear();
        }

        if (spring.PlannedRoute.TryDequeue(out var planned))
        {
            if (IsOcean(world.GetTerrain(planned)))
            {
                Connect(world, current, planned);
                return Completed(SpringTermination.ReachedOcean, spring);
            }

            if (world.GetSurfaceWater(planned) is
                SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake)
            {
                Connect(world, current, planned);
                return Completed(SpringTermination.ReachedWatercourse, spring);
            }

            Connect(world, current, planned);
            world.SetSurfaceWater(planned, SurfaceWaterKind.River);
            world.SetWaterSurfaceElevation(planned, null);
            spring.Current = planned;
            spring.Visited.Add(planned);
            return null;
        }

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
            if (candidate is null || spring.Visited.Contains(candidate.Value) ||
                spring.UpstreamLake.Contains(candidate.Value))
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
            if (lake.Outlet is not null &&
                (spring.Visited.Contains(lake.Outlet.Value) ||
                    spring.UpstreamLake.Contains(lake.Outlet.Value)))
            {
                HashSet<GridPosition> blocked = [.. spring.Visited, .. spring.UpstreamLake];
                blocked.Remove(current);
                lake = FillBasin(
                    world,
                    current,
                    DefaultBasinSearchBudget,
                    DefaultLakeTileBudget,
                    blocked);
            }

            if (lake.Created)
            {
                // A filled lake affects climate immediately even when its
                // overflow river still has a long route remaining. The caller
                // coalesces all lakes formed on this tick into one rebuild.
                spring.ClimateRefreshPending = true;
            }

            if (!lake.Created || lake.Outlet is null || lake.OutletConnection is null ||
                lake.OverflowPath is null || lake.OverflowPath.Count == 0 ||
                spring.Visited.Contains(lake.Outlet.Value) ||
                spring.UpstreamLake.Contains(lake.Outlet.Value))
            {
                return Completed(
                    SpringTermination.FormedLake,
                    spring);
            }

            CaptureUpstreamLake(world, current, spring.UpstreamLake);
            Connect(world, lake.OutletConnection.Value, lake.Outlet.Value);
            world.SetSurfaceWater(lake.Outlet.Value, SurfaceWaterKind.River);
            world.SetWaterSurfaceElevation(lake.Outlet.Value, null);
            spring.Current = lake.Outlet.Value;
            spring.Visited.Add(lake.Outlet.Value);
            foreach (var position in lake.OverflowPath.Skip(1))
            {
                spring.PlannedRoute.Enqueue(position);
            }

            return null;
        }

        Connect(world, current, lowest.Value);
        world.SetSurfaceWater(lowest.Value, SurfaceWaterKind.River);
        world.SetWaterSurfaceElevation(lowest.Value, null);
        spring.Current = lowest.Value;
        spring.Visited.Add(lowest.Value);
        return null;
    }

    private static void CaptureUpstreamLake(
        SimulationWorld world,
        GridPosition start,
        HashSet<GridPosition> destination)
    {
        if (world.GetSurfaceWater(start) is not SurfaceWaterKind.FreshwaterLake)
        {
            return;
        }

        var queue = new Queue<GridPosition>();
        queue.Enqueue(start);
        destination.Add(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var direction in BasinDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is not null &&
                    world.GetSurfaceWater(neighbor.Value) is SurfaceWaterKind.FreshwaterLake &&
                    destination.Add(neighbor.Value))
                {
                    queue.Enqueue(neighbor.Value);
                }
            }
        }
    }

    private static HashSet<GridPosition> FindConnectedFreshwater(
        SimulationWorld world,
        GridPosition start)
    {
        var connected = new HashSet<GridPosition> { start };
        var frontier = new Queue<GridPosition>();
        frontier.Enqueue(start);

        while (frontier.TryDequeue(out var current))
        {
            var currentWater = world.GetSurfaceWater(current);
            var riverConnections = world.GetRiverConnections(current);
            foreach (var direction in FlowDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null || connected.Contains(neighbor.Value) ||
                    world.GetSurfaceWater(neighbor.Value) is SurfaceWaterKind.None)
                {
                    continue;
                }

                var followsChannel = (riverConnections &
                    ToConnection(direction.X, direction.Y)) != 0;
                var joinsLake = currentWater is SurfaceWaterKind.FreshwaterLake &&
                    world.GetSurfaceWater(neighbor.Value) is SurfaceWaterKind.FreshwaterLake &&
                    (direction.X == 0 || direction.Y == 0);
                if (!followsChannel && !joinsLake)
                {
                    continue;
                }

                connected.Add(neighbor.Value);
                frontier.Enqueue(neighbor.Value);
            }
        }

        return connected;
    }

    private static SpringResult Completed(SpringTermination termination, ActiveSpring spring) =>
        new(termination, spring.Visited.Count, spring.Current);

    private static LakeFillResult CreateTerminalLake(
        SimulationWorld world,
        GridPosition sink,
        int tileBudget)
    {
        var surface = FindTerminalLakeSurface(world, sink, tileBudget);
        var lakeTiles = FindBoundedTerminalLakeTiles(world, sink, surface, tileBudget);
        foreach (var position in lakeTiles)
        {
            world.SetSurfaceWater(position, SurfaceWaterKind.FreshwaterLake);
            world.SetWaterSurfaceElevation(position, surface);
        }

        return new LakeFillResult(true, lakeTiles.Count, surface, null, null);
    }

    private static float FindTerminalLakeSurface(
        SimulationWorld world,
        GridPosition sink,
        int tileBudget)
    {
        var frontier = new PriorityQueue<GridPosition, float>();
        var bestSpill = new Dictionary<GridPosition, float> { [sink] = world.GetElevation(sink) };
        frontier.Enqueue(sink, world.GetElevation(sink));
        var surface = world.GetElevation(sink);
        var examined = 0;

        while (frontier.TryDequeue(out var current, out var currentSpill) && examined < tileBudget)
        {
            if (currentSpill > bestSpill[current] + ElevationTolerance)
            {
                continue;
            }

            surface = currentSpill;
            examined++;
            foreach (var direction in BasinDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null || IsOcean(world.GetTerrain(neighbor.Value)))
                {
                    continue;
                }

                var candidateSpill = Math.Max(currentSpill, world.GetElevation(neighbor.Value));
                if (bestSpill.TryGetValue(neighbor.Value, out var knownSpill) &&
                    candidateSpill >= knownSpill - ElevationTolerance)
                {
                    continue;
                }

                bestSpill[neighbor.Value] = candidateSpill;
                frontier.Enqueue(neighbor.Value, candidateSpill);
            }
        }

        var enclosedTiles = FindBoundedTerminalLakeTiles(world, sink, surface, tileBudget);
        return enclosedTiles.Count <= 1
            ? surface + ElevationTolerance * 2
            : surface;
    }

    private static List<GridPosition> FindBoundedTerminalLakeTiles(
        SimulationWorld world,
        GridPosition sink,
        float surfaceElevation,
        int tileBudget)
    {
        var lake = new List<GridPosition>(tileBudget);
        var visited = new HashSet<GridPosition> { sink };
        var queue = new Queue<GridPosition>();
        queue.Enqueue(sink);

        while (queue.TryDequeue(out var current) && lake.Count < tileBudget)
        {
            if (current != sink && world.GetElevation(current) >= surfaceElevation - ElevationTolerance)
            {
                continue;
            }

            lake.Add(current);
            foreach (var direction in BasinDirections)
            {
                var neighbor = GetNeighbor(world, current, direction);
                if (neighbor is null || !visited.Add(neighbor.Value) ||
                    IsOcean(world.GetTerrain(neighbor.Value)))
                {
                    continue;
                }

                queue.Enqueue(neighbor.Value);
            }
        }

        return lake;
    }

    private static List<GridPosition> ReconstructPath(
        SimulationWorld world,
        int[] previous,
        int startIndex,
        int endIndex)
    {
        var path = new List<GridPosition> { FromIndex(world, endIndex) };
        var currentIndex = endIndex;
        while (currentIndex != startIndex && previous[currentIndex] >= 0)
        {
            currentIndex = previous[currentIndex];
            path.Add(FromIndex(world, currentIndex));
        }
        path.Reverse();
        return path;
    }

    private static int ToIndex(SimulationWorld world, GridPosition position) =>
        position.Y * world.Width + position.X;

    private static GridPosition FromIndex(SimulationWorld world, int index) =>
        new(index % world.Width, index / world.Width);

    private static int EstimateOceanRouteLength(
        SimulationWorld world,
        GridPosition position,
        int steps)
    {
        var horizontal = Math.Abs(position.X - world.OceanSeed.X);
        horizontal = Math.Min(horizontal, world.Width - horizontal);
        var vertical = Math.Abs(position.Y - world.OceanSeed.Y);
        // Eight-direction movement reaches a target in Chebyshev distance.
        return steps + Math.Max(horizontal, vertical);
    }

    private static List<GridPosition>? FindLakeTiles(
        SimulationWorld world,
        GridPosition sink,
        float spillElevation,
        int tileBudget,
        IReadOnlySet<GridPosition>? blocked)
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
                    neighbor.Value != sink && blocked?.Contains(neighbor.Value) is true ||
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

    private static bool IsValidSource(SimulationWorld world, GridPosition source) =>
        world.Contains(source) && !IsOcean(world.GetTerrain(source)) && world.GetElevation(source) > 0;
}
