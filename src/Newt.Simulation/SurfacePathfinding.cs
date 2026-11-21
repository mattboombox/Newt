namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, (int Target, bool StopAdjacent, int Current, Queue<int> Steps)> _surfacePaths = [];
    // Use the same edge-crossing rules for route construction and cached steps.
    private bool ArePathNeighbors(int first, int second)
    {
        var position = GetPosition(first);
        foreach (var direction in MovementDirections)
            if (TryGetPathNeighbor(position, direction, out var neighbor) && neighbor == second)
                return true;
        return false;
    }

    private bool TryGetPathNeighbor(GridPosition position, GridPosition direction, out int tile)
    {
        tile = -1;
        var next = OffsetTile(position, direction);
        if (!Contains(next) || next == position) return false;
        if (Body == WorldBody.Planetoid && direction.X != 0 && direction.Y != 0)
        {
            // Cross shared edges; diagonals at three-face corners are ambiguous.
            var size = PlanetoidLayout.FaceSize(Width);
            if (PlanetoidLayout.FaceAt(position.X, position.Y, size) != PlanetoidLayout.FaceAt(next.X, next.Y, size))
                return false;
        }
        tile = GetIndex(next);
        return true;
    }

    private int SurfacePathLowerBound(int from, int to)
    {
        // Each legal step turns by at most pi / faceSize, including diagonals.
        // Great-circle distance therefore supplies an admissible A* estimate.
        var dot = System.Numerics.Vector3.Dot(GetPlanetoidTileDirection(GetPosition(from)),
            GetPlanetoidTileDirection(GetPosition(to)));
        return (int)MathF.Floor(MathF.Acos(Math.Clamp(dot, -1f, 1f)) *
            PlanetoidLayout.FaceSize(Width) / MathF.PI);
    }

    private GridPosition? TryMoveAlongSurfacePath(int index, int target,
        IReadOnlySet<GridPosition>? reserved, bool stopAdjacent = false)
    {
        var id = _critterIds[index].Value;
        var current = GetIndex(_positions[index]);
        stopAdjacent |= _occupants[target] >= 0 && _occupants[target] != index &&
            !CanShoveMovementBlocker(index, target, reserved) &&
            !CanSwapPastMovementBlocker(index, target, reserved);
        if (!_surfacePaths.TryGetValue(id, out var route) || route.Target != target ||
            route.StopAdjacent != stopAdjacent || route.Current != current ||
            !route.Steps.TryPeek(out var step) || !ArePathNeighbors(current, step) ||
            !CanCritterLiveOn(index, step) || reserved?.Contains(GetPosition(step)) is true)
        {
            var path = FindApeSettlerPath(index, current, target,
                reservedPrey: reserved, pathSpecies: _species[index], stopAdjacent: stopAdjacent);
            if (path is null || path.Count == 0)
            {
                _surfacePaths.Remove(id);
                return null;
            }
            route = (target, stopAdjacent, current, path);
        }
        var next = route.Steps.Peek();
        if (_occupants[next] is var ally && ally >= 0 && AreFriendlyMilitary(index, ally) &&
            TrySwapPastMovementBlocker(index, next, reserved))
        {
            _surfacePaths.Remove(id);
            return null;
        }
        if (_occupants[next] >= 0 && !TryShoveMovementBlocker(index, next, reserved))
        {
            TrySwapPastMovementBlocker(index, next, reserved);
            _surfacePaths.Remove(id);
            return null;
        }
        route.Steps.Dequeue();
        route.Current = next;
        _surfacePaths[id] = route;
        MoveCritter(index, next, GetPosition(next));
        return null;
    }
}
