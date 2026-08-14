namespace Newt.Simulation;

/// <summary>Deterministic, rare terrain-forming events driven by simulation time.</summary>
public static class NaturalEvents
{
    private const int MinimumIntervalMinutes = 6;
    private const int IntervalVariationMinutes = 10;

    public static void SetEnabled(SimulationWorld world, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(world);
        world.NaturalEventsEnabled = enabled;
        if (enabled && world.NextNaturalEventTick <= world.Tick)
        {
            ScheduleNext(world);
        }
    }

    internal static void Advance(SimulationWorld world)
    {
        if (!world.NaturalEventsEnabled || world.Tick < world.NextNaturalEventTick)
        {
            return;
        }

        SpawnRandom(world);
        ScheduleNext(world);
    }

    internal static bool SpawnRandom(SimulationWorld world)
    {
        var position = new GridPosition(world.NextInt(world.Width), world.NextInt(world.Height));
        var eventType = world.NextInt(3);
        if (eventType == 0)
        {
            var magnitude = 0.08f + world.NextUnitFloat() * 0.57f;
            Impacts.CreateMeteorImpact(world, position, magnitude);
            return true;
        }

        if (eventType == 1)
        {
            return Volcanism.SpawnVolcano(world, position);
        }

        return Hydrology.ShiftNaturalWatershed(world) || Volcanism.SpawnVolcano(world, position);
    }

    private static void ScheduleNext(SimulationWorld world)
    {
        var minutes = MinimumIntervalMinutes + world.NextInt(IntervalVariationMinutes + 1);
        world.NextNaturalEventTick = world.Tick + minutes * 60L * SimulationWorld.TicksPerSecond;
    }
}
