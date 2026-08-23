namespace Newt.Simulation;

/// <summary>Animated volcanic eruptions, lava cooling, and volcano lifecycles.</summary>
public static class Volcanism
{
    private const int FlowStepIntervalTicks = 5;
    private const int TerrainRefreshIntervalTicks = 10 * SimulationWorld.TicksPerSecond;
    private const int LandLavaLifetimeTicks = 9 * SimulationWorld.TicksPerSecond;
    private const int SubmergedLavaLifetimeTicks = 3 * SimulationWorld.TicksPerSecond;
    private const int BaseStoneLifetimeTicks = 45 * SimulationWorld.TicksPerSecond;

    private static readonly GridPosition[] Directions =
    [
        new(1, 0),
        new(1, 1),
        new(0, 1),
        new(-1, 1),
        new(-1, 0),
        new(-1, -1),
        new(0, -1),
        new(1, -1),
    ];

    /// <summary>Places a temporary stone cover on one tile without changing elevation.</summary>
    public static bool PlaceStone(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position) || world.IsOccupied(position) ||
            world.GetTerrain(position) is Terrain.RingWorldWall)
        {
            return false;
        }

        world.SetSurfaceCover(position, SurfaceCover.Stone, world.Tick + BaseStoneLifetimeTicks);
        return true;
    }

    /// <summary>Places one small lava deposit and raises only the clicked tile.</summary>
    public static bool PlaceLava(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position) || world.GetTerrain(position) is Terrain.RingWorldWall ||
            !DepositLava(world, position, 0.03f))
        {
            return false;
        }

        RebuildAfterDeposits(world);
        return true;
    }

    public static bool ClearGeologicalCover(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position) || world.GetSurfaceCover(position) is SurfaceCover.None)
        {
            return false;
        }

        world.SetSurfaceCover(position, SurfaceCover.None, 0);
        return true;
    }

    public static bool SpawnVolcano(
        SimulationWorld world,
        GridPosition position,
        VolcanoState state = VolcanoState.Active)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!world.Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }
        if (world.GetTerrain(position) is Terrain.RingWorldWall || world.HasTeleporter(position) ||
            world.IsOccupied(position) ||
            world.Volcanoes.Any(volcano => volcano.Position == position))
        {
            return false;
        }

        var volcano = new VolcanoActivity(
            position,
            state,
            world.NextInt(Directions.Length),
            world.Tick + RandomRange(world, 2, 5) * SimulationWorld.TicksPerSecond,
            world.Tick + RandomRange(world, 25, 46) * SimulationWorld.TicksPerSecond);
        world.Volcanoes.Add(volcano);
        var changed = RaiseVolcanicMound(world, position);
        changed |= DepositLava(world, position, 0.07f + world.NextUnitFloat() * 0.05f);
        if (changed)
        {
            RebuildAfterDeposits(world);
        }
        return true;
    }

    private static bool RaiseVolcanicMound(SimulationWorld world, GridPosition center)
    {
        const int radius = 2;
        var changed = false;
        for (var offsetY = -radius; offsetY <= radius; offsetY++)
        {
            var y = center.Y + offsetY;
            if (y < 0 || y >= world.Height)
            {
                continue;
            }

            for (var offsetX = -radius; offsetX <= radius; offsetX++)
            {
                var distance = MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
                if (distance > 2.35f)
                {
                    continue;
                }

                var position = new GridPosition(Mod(center.X + offsetX, world.Width), y);
                var targetElevation = distance <= 1.45f
                    ? 0.68f - distance * 0.055f
                    : 0.52f - (distance - 1.45f) * 0.12f;
                if (world.GetElevation(position) >= targetElevation)
                {
                    continue;
                }

                world.SetElevation(position, targetElevation);
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Forces an eruption now; flow lobes continue animating on later ticks.</summary>
    public static bool TriggerEruption(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        var volcano = world.Volcanoes.FirstOrDefault(candidate => candidate.Position == position);
        if (volcano is null || volcano.State is VolcanoState.Extinct)
        {
            return false;
        }

        var changed = BeginEruption(world, volcano);
        volcano.NextEruptionTick = world.Tick + RandomRange(world, 3, 7) * SimulationWorld.TicksPerSecond;
        if (changed)
        {
            RebuildAfterDeposits(world);
        }
        return true;
    }

    internal static void Advance(SimulationWorld world)
    {
        var terrainChanged = AdvanceVolcanoes(world);
        terrainChanged |= AdvanceFlows(world);
        AdvanceSurfaceCovers(world);
        world.VolcanicTerrainRefreshPending |= terrainChanged;
        // Individual flows still animate on schedule, but their global terrain
        // refresh is shared by all volcanoes. Hydrology is rebuilt separately and
        // only when a deposit actually touches freshwater.
        if (world.VolcanicTerrainRefreshPending &&
            world.Tick - world.LastVolcanicTerrainRefreshTick >= TerrainRefreshIntervalTicks)
        {
            RebuildAfterDeposits(world);
        }
    }

    internal static bool StartImpactMelt(
        SimulationWorld world,
        GridPosition origin,
        int flowCount,
        int flowLength)
    {
        var changed = DepositLava(world, origin, 0.008f);
        for (var index = 0; index < flowCount; index++)
        {
            world.LavaFlows.Add(new LavaFlowActivity(
                origin,
                world.NextInt(Directions.Length),
                Math.Max(2, flowLength + world.NextInt(5) - 2),
                world.Tick + FlowStepIntervalTicks));
        }
        return changed;
    }

    internal static void RemoveVolcanoAt(SimulationWorld world, GridPosition position) =>
        world.Volcanoes.RemoveAll(volcano => volcano.Position == position);

    private static bool AdvanceVolcanoes(SimulationWorld world)
    {
        var changed = false;
        var initialCount = world.Volcanoes.Count;
        for (var index = initialCount - 1; index >= 0; index--)
        {
            var volcano = world.Volcanoes[index];
            if (volcano.State is VolcanoState.Active)
            {
                if (world.Tick >= volcano.NextEruptionTick)
                {
                    changed |= BeginEruption(world, volcano);
                    volcano.NextEruptionTick = world.Tick +
                        RandomRange(world, 3, 7) * SimulationWorld.TicksPerSecond;
                }

                if (world.Tick >= volcano.NextStateTick)
                {
                    volcano.State = VolcanoState.Dormant;
                    volcano.NextStateTick = world.Tick +
                        RandomRange(world, 12, 31) * SimulationWorld.TicksPerSecond;
                }
            }
            else if (volcano.State is VolcanoState.Dormant && world.Tick >= volcano.NextStateTick)
            {
                if (world.NextInt(100) < 58)
                {
                    volcano.State = VolcanoState.Active;
                    volcano.NextEruptionTick = world.Tick + RandomRange(world, 1, 4) * SimulationWorld.TicksPerSecond;
                    volcano.NextStateTick = world.Tick + RandomRange(world, 20, 41) * SimulationWorld.TicksPerSecond;
                }
                else
                {
                    MakeExtinct(world, volcano, spawnSuccessor: world.NextInt(100) < 72);
                }
            }
        }

        return changed;
    }

    internal static void MakeExtinct(
        SimulationWorld world,
        VolcanoActivity volcano,
        bool spawnSuccessor)
    {
        volcano.State = VolcanoState.Extinct;
        world.SetElevation(
            volcano.Position,
            Math.Max(world.GetElevation(volcano.Position), 0.60f));
        if (spawnSuccessor)
        {
            SpawnChainVolcano(world, volcano);
        }
        world.Volcanoes.Remove(volcano);
        RebuildAfterDeposits(world);
    }

    private static bool BeginEruption(SimulationWorld world, VolcanoActivity volcano)
    {
        var changed = DepositLava(world, volcano.Position, 0.05f + world.NextUnitFloat() * 0.06f);
        var flowCount = RandomRange(world, 1, 4);
        for (var flow = 0; flow < flowCount; flow++)
        {
            var direction = Mod(volcano.PreferredDirection + RandomRange(world, -2, 3), Directions.Length);
            world.LavaFlows.Add(new LavaFlowActivity(
                volcano.Position,
                direction,
                RandomRange(world, 7, 17),
                world.Tick + FlowStepIntervalTicks));
        }

        var chunks = RandomRange(world, 1, 4);
        for (var chunk = 0; chunk < chunks; chunk++)
        {
            var direction = Directions[world.NextInt(Directions.Length)];
            var distance = RandomRange(world, 3, 9);
            var scatter = Directions[world.NextInt(Directions.Length)];
            var target = GetPosition(
                world,
                volcano.Position.X + direction.X * distance + scatter.X * world.NextInt(2),
                volcano.Position.Y + direction.Y * distance + scatter.Y * world.NextInt(2));
            if (target is not null)
            {
                changed |= DepositLava(world, target.Value, 0.01f + world.NextUnitFloat() * 0.035f);
            }
        }

        if (world.NextInt(100) < 30)
        {
            volcano.PreferredDirection = Mod(
                volcano.PreferredDirection + (world.NextInt(2) == 0 ? -1 : 1),
                Directions.Length);
        }
        return changed;
    }

    private static bool AdvanceFlows(SimulationWorld world)
    {
        var changed = false;
        for (var index = world.LavaFlows.Count - 1; index >= 0; index--)
        {
            var flow = world.LavaFlows[index];
            if (world.Tick < flow.NextStepTick)
            {
                continue;
            }

            var next = ChooseFlowStep(world, flow);
            if (next is null)
            {
                world.LavaFlows.RemoveAt(index);
                continue;
            }

            flow.Position = next.Value.Position;
            flow.Direction = next.Value.Direction;
            flow.RemainingSteps--;
            flow.NextStepTick = world.Tick + FlowStepIntervalTicks;
            changed |= DepositLava(world, flow.Position, 0.012f + world.NextUnitFloat() * 0.035f);
            if (flow.RemainingSteps <= 0)
            {
                world.LavaFlows.RemoveAt(index);
            }
        }

        return changed;
    }

    private static (GridPosition Position, int Direction)? ChooseFlowStep(
        SimulationWorld world,
        LavaFlowActivity flow)
    {
        (GridPosition Position, int Direction)? best = null;
        var bestScore = float.MaxValue;
        var currentElevation = world.GetElevation(flow.Position);
        for (var offset = -2; offset <= 2; offset++)
        {
            var directionIndex = Mod(flow.Direction + offset, Directions.Length);
            var direction = Directions[directionIndex];
            var candidate = GetPosition(world, flow.Position.X + direction.X, flow.Position.Y + direction.Y);
            if (candidate is null)
            {
                continue;
            }

            var turnPenalty = Math.Abs(offset) * 0.035f;
            var randomJitter = world.NextUnitFloat() * 0.08f;
            var score = world.GetElevation(candidate.Value) + turnPenalty + randomJitter;
            if (score < bestScore)
            {
                best = (candidate.Value, directionIndex);
                bestScore = score;
            }
        }

        if (best is not null && world.GetElevation(best.Value.Position) > currentElevation + 0.18f)
        {
            return null;
        }
        return best;
    }

    internal static bool DepositLava(SimulationWorld world, GridPosition position, float elevationGain)
    {
        if (world.IsOccupied(position))
        {
            return false;
        }

        var submerged = IsOcean(world.GetTerrain(position));
        world.VolcanicFreshwaterRefreshPending |=
            world.GetSurfaceWater(position) is not SurfaceWaterKind.None;
        world.SetElevation(position, world.GetElevation(position) + elevationGain);
        world.SetSurfaceCover(
            position,
            SurfaceCover.Lava,
            world.Tick + (submerged ? SubmergedLavaLifetimeTicks : LandLavaLifetimeTicks));
        return true;
    }

    private static void AdvanceSurfaceCovers(SimulationWorld world)
    {
        foreach (var position in world.ActiveSurfaceCovers.ToArray())
        {
            var cover = world.GetSurfaceCover(position);
            var untilTick = world.GetSurfaceCoverUntilTick(position);
            if (cover is SurfaceCover.Stone && HasRiverAtOrBeside(world, position))
            {
                untilTick -= 3;
                world.SetSurfaceCover(position, cover, untilTick);
            }

            if (world.Tick < untilTick)
            {
                continue;
            }

            if (cover is SurfaceCover.Lava)
            {
                var climateFactor = Math.Clamp(
                    1.4f - world.GetMoisture(position) * 0.45f - world.GetTemperature(position) * 0.20f,
                    0.65f,
                    1.4f);
                var stoneLifetime = (int)MathF.Round(BaseStoneLifetimeTicks * climateFactor);
                if (HasRiverAtOrBeside(world, position))
                {
                    stoneLifetime /= 4;
                }
                world.SetSurfaceCover(position, SurfaceCover.Stone, world.Tick + Math.Max(1, stoneLifetime));
            }
            else
            {
                world.SetSurfaceCover(position, SurfaceCover.None, 0);
            }
        }
    }

    private static void SpawnChainVolcano(SimulationWorld world, VolcanoActivity parent)
    {
        for (var attempt = 0; attempt < Directions.Length; attempt++)
        {
            var directionIndex = Mod(parent.PreferredDirection + attempt, Directions.Length);
            var direction = Directions[directionIndex];
            var candidate = GetPosition(
                world,
                parent.Position.X + direction.X,
                parent.Position.Y + direction.Y);
            if (candidate is null || world.Volcanoes.Any(volcano => volcano.Position == candidate.Value))
            {
                continue;
            }

            SpawnVolcano(world, candidate.Value);
            return;
        }
    }

    private static bool HasRiverAtOrBeside(SimulationWorld world, GridPosition position)
    {
        if (world.GetSurfaceWater(position) is SurfaceWaterKind.River)
        {
            return true;
        }

        foreach (var direction in Directions)
        {
            var neighbor = GetPosition(world, position.X + direction.X, position.Y + direction.Y);
            if (neighbor is not null && world.GetSurfaceWater(neighbor.Value) is SurfaceWaterKind.River)
            {
                return true;
            }
        }
        return false;
    }

    private static void RebuildAfterDeposits(SimulationWorld world)
    {
        TerrainClassifier.RebuildLandforms(world);
        if (world.VolcanicFreshwaterRefreshPending)
        {
            Hydrology.RebuildFreshwater(world);
        }
        else
        {
            ClimateSystem.RebuildBiomesFromCurrentMoisture(world);
        }
        world.VolcanicTerrainRefreshPending = false;
        world.VolcanicFreshwaterRefreshPending = false;
        world.LastVolcanicTerrainRefreshTick = world.Tick;
    }

    private static GridPosition? GetPosition(SimulationWorld world, int x, int y)
    {
        if (y < 0 || y >= world.Height)
        {
            return null;
        }
        return new GridPosition(Mod(x, world.Width), y);
    }

    private static int RandomRange(SimulationWorld world, int minimum, int exclusiveMaximum) =>
        minimum + world.NextInt(exclusiveMaximum - minimum);

    private static bool IsOcean(Terrain terrain) => terrain is
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice;

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
