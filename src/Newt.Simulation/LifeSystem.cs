namespace Newt.Simulation;

/// <summary>Controls whether a world supports critters and living biomes.</summary>
public static class LifeSystem
{
    private const int MinimumRecoverySeconds = 18;
    private const int RecoveryWindowSeconds = 28;

    public static void SetEnabled(SimulationWorld world, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(world);

        if (enabled)
        {
            if (!world.LifeEnabled)
            {
                ScheduleBarrenTerrainRecovery(world);
            }
            world.LifeEnabled = true;
            ClimateSystem.RebuildMoistureAndBiomes(world);
            world.EnablePlanktonRecovery();
            return;
        }

        world.LifeEnabled = false;
        world.ClearLifeRecovery();
        world.DisablePlanktonRecovery();
        while (world.CritterCount > 0)
        {
            world.RemoveCritterAt(world.GetCritter(world.CritterCount - 1).Position);
        }
        ClimateSystem.RebuildMoistureAndBiomes(world);
    }

    public static bool IsBarrenStoneTerrain(Terrain terrain) => terrain is
        Terrain.Plains or Terrain.Hills or Terrain.Lowlands or
        Terrain.Canyon or Terrain.Trench;

    /// <summary>
    /// A biome-less ordinary land tile is stone. Biome.None retains its literal
    /// meaning on water, beaches, mountains, ice sheets, and artificial walls.
    /// </summary>
    public static bool IsStoneBiome(SimulationWorld world, GridPosition position)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.GetBiome(position) is Biome.None &&
            IsBarrenStoneTerrain(world.GetTerrain(position));
    }

    internal static void Advance(SimulationWorld world)
    {
        var recovery = world.LifeRecoveryTiles;
        while (world.LifeRecoveryIndex < recovery.Count &&
            recovery[world.LifeRecoveryIndex].UntilTick <= world.Tick)
        {
            var tile = recovery[world.LifeRecoveryIndex++];
            if (!world.IsLifeRecoveryPending(tile.Position))
            {
                continue;
            }

            world.ClearLifeRecoveryAt(tile.Position);
            ClimateSystem.RebuildBiomeAt(world, tile.Position);
        }

        if (world.LifeRecoveryIndex >= recovery.Count && recovery.Count > 0)
        {
            recovery.Clear();
            world.LifeRecoveryIndex = 0;
        }
    }

    private static void ScheduleBarrenTerrainRecovery(SimulationWorld world)
    {
        world.ClearLifeRecovery();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (world.GetBiome(position) is not Biome.None ||
                    !IsBarrenStoneTerrain(world.GetTerrain(position)))
                {
                    continue;
                }

                var seconds = MinimumRecoverySeconds + GetRecoveryOffset(world.Seed, position);
                var untilTick = world.Tick + seconds * SimulationWorld.TicksPerSecond;
                world.SetLifeRecovery(position, untilTick);
                world.LifeRecoveryTiles.Add(new LifeRecoveryTile(position, untilTick));
            }
        }

        world.LifeRecoveryTiles.Sort(static (left, right) =>
            left.UntilTick.CompareTo(right.UntilTick));
    }

    private static int GetRecoveryOffset(ulong seed, GridPosition position)
    {
        var value = seed;
        value ^= (ulong)(uint)position.X * 0x9E3779B185EBCA87UL;
        value ^= (ulong)(uint)position.Y * 0xC2B2AE3D27D4EB4FUL;
        value ^= value >> 30;
        value *= 0xBF58476D1CE4E5B9UL;
        value ^= value >> 27;
        value *= 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (int)(value % RecoveryWindowSeconds);
    }
}
