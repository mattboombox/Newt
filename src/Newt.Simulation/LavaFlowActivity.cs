namespace Newt.Simulation;

internal sealed class LavaFlowActivity(
    GridPosition position,
    int direction,
    int remainingSteps,
    long nextStepTick)
{
    public GridPosition Position { get; set; } = position;

    public int Direction { get; set; } = direction;

    public int RemainingSteps { get; set; } = remainingSteps;

    public long NextStepTick { get; set; } = nextStepTick;
}
