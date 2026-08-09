namespace Newt.Simulation;

internal sealed class VolcanoActivity(
    GridPosition position,
    VolcanoState state,
    int preferredDirection,
    long nextEruptionTick,
    long nextStateTick)
{
    public GridPosition Position { get; set; } = position;

    public VolcanoState State { get; set; } = state;

    public int PreferredDirection { get; set; } = preferredDirection;

    public long NextEruptionTick { get; set; } = nextEruptionTick;

    public long NextStateTick { get; set; } = nextStateTick;
}
