namespace Newt.Simulation;

public readonly record struct SandwormSegment(
    CritterId Worm,
    int Index,
    int Length,
    GridPosition? TowardHead,
    GridPosition? TowardTail);

public sealed partial class SimulationWorld
{
    private const int MealUnitsPerCritter = 2;
    private const int SandwormMealUnitsPerGrowth = 5 * MealUnitsPerCritter;
    private const int SandwormConversionOdds = 200;
    private const int SandwormHuntingLengthAdvantage = 5;
    private const int SandwormSplitLength = 10;
    private const int SandwormDaughterLength = 3;

    private sealed class Sandworm(List<int> tiles, int mealUnits)
    {
        public List<int> Tiles { get; } = tiles;
        public int MealUnits { get; set; } = mealUnits;
    }

    // Only the head occupies a critter slot. Track every segment for hazards
    // and for collisions with other sandworms.
    private readonly Dictionary<int, Sandworm> _sandworms = [];
    private readonly Dictionary<int, int> _sandwormTiles = [];
    private readonly HashSet<int> _sandwormVisited = [];
    private readonly Queue<(int Tile, int FirstStep)> _sandwormSearch = [];

    public bool TryGetSandwormSegment(GridPosition position, out SandwormSegment segment)
    {
        if (_sandwormTiles.Count > 0 && Contains(position))
        {
            var tile = GetIndex(position);
            if (_sandwormTiles.TryGetValue(tile, out var id))
            {
                var tiles = _sandworms[id].Tiles;
                var index = tiles.IndexOf(tile);
                segment = new SandwormSegment(new CritterId(id), index, tiles.Count,
                    index > 0 ? GetPosition(tiles[index - 1]) : null,
                    index + 1 < tiles.Count ? GetPosition(tiles[index + 1]) : null);
                return true;
            }
        }

        segment = default;
        return false;
    }

    private bool TrySpawnSandworm(GridPosition position, out CritterId id)
    {
        id = default;
        if (!IsSandwormHabitat(position)) return false;
        var head = GetIndex(position);
        if (_occupants[head] >= 0 || _sandwormTiles.ContainsKey(head)) return false;

        var tail = FindSandwormSpawnTail(position);
        if (tail < 0) return false;
        id = AddSandworm([head, tail]);
        return true;
    }

    private bool TryConvertDesertWorm(int index)
    {
        var position = _positions[index];
        var head = GetIndex(position);
        if (!IsSandwormHabitat(position) || _sandwormTiles.ContainsKey(head)) return false;
        var tail = FindSandwormSpawnTail(position);
        if (tail < 0 || NextInt(SandwormConversionOdds) != 0) return false;
        ChangeCritterSpecies(index, CritterSpecies.Sandworm, preserveEnergy: true);
        RegisterSandworm(_critterIds[index], [head, tail], mealUnits: 0);
        return true;
    }

    private int FindSandwormSpawnTail(GridPosition position)
    {
        foreach (var direction in CardinalDirections)
        {
            var neighbor = OffsetSandwormTile(position, direction);
            if (neighbor == position || !IsSandwormHabitat(neighbor)) continue;
            var tail = GetIndex(neighbor);
            if (_occupants[tail] >= 0 || _sandwormTiles.ContainsKey(tail)) continue;
            return tail;
        }
        return -1;
    }

    private CritterId AddSandworm(List<int> tiles, int mealUnits = 0)
    {
        var head = tiles[0];
        var id = AddCritterUnchecked(CritterSpecies.Sandworm, GetPosition(head), head);
        RegisterSandworm(id, tiles, mealUnits);
        return id;
    }

    private void RegisterSandworm(CritterId id, List<int> tiles, int mealUnits)
    {
        _sandworms.Add(id.Value, new Sandworm(tiles, mealUnits));
        foreach (var tile in tiles)
            _sandwormTiles.Add(tile, id.Value);
    }

    private void RemoveSandworm(int id)
    {
        if (!_sandworms.Remove(id, out var worm)) return;
        foreach (var tile in worm.Tiles)
        {
            _sandwormTiles.Remove(tile);
            DepositWhaleFall(tile);
        }
    }

    internal void KillSandwormAt(GridPosition position)
    {
        if (_sandwormTiles.TryGetValue(GetIndex(position), out var id))
            RemoveCritter(new CritterId(id));
    }

    private GridPosition OffsetSandwormTile(GridPosition tile, GridPosition direction) =>
        Body is WorldBody.Nexus
            ? new GridPosition(tile.X + direction.X, tile.Y + direction.Y)
            : OffsetTile(tile, direction);

    private bool IsSandwormHabitat(GridPosition position)
    {
        if (!Contains(position) || Body is WorldBody.Nexus && !NexusLayout.Contains(this, position))
            return false;
        var tile = GetIndex(position);
        return _biomes[tile] is Biome.Desert &&
            _terrain[tile] is not (Terrain.Shallows or Terrain.Ocean or Terrain.DeepOcean or Terrain.Ice or
                Terrain.Mountain or Terrain.MegaWall) &&
            _surfaceWater[tile] is not SurfaceWaterKind.FreshwaterLake && _surfaceCovers[tile] is not SurfaceCover.Lava;
    }

    private void AdvanceSandworms()
    {
        // Commit snake movement after ordinary encounters, since swallowing
        // prey can compact the critter arrays. Daughters act on a later tick.
        if (_sandworms.Count == 0) return;
        foreach (var id in _sandworms.Keys.ToArray())
        {
            if (!_critterIndicesById.TryGetValue(id, out var index))
                continue;
            var worm = _sandworms[id];
            if (worm.Tiles.Exists(tile => !IsSandwormHabitat(GetPosition(tile))))
            {
                RemoveCritterAtIndex(index);
                continue;
            }
            if (Tick < _nextMovementTicks[index]) continue;
            _nextMovementTicks[index] = Tick + GetMovementIntervalTicks(CritterSpecies.Sandworm);
            var step = FindSandwormStep(worm);
            if (step < 0)
            {
                var tail = GetPosition(worm.Tiles[^1]);
                if (IsSandwormHabitat(tail) &&
                    CardinalDirections.Any(direction => SandwormPathTile(worm, tail, direction, worm.Tiles[0]) >= 0))
                {
                    worm.Tiles.Reverse();
                    MoveSandwormHead(id, worm, worm.Tiles[0]);
                }
                continue;
            }

            MoveSandwormHead(id, worm, step);
            if (worm.MealUnits >= SandwormMealUnitsPerGrowth && step != worm.Tiles[^1])
            {
                worm.MealUnits -= SandwormMealUnitsPerGrowth;
            }
            else
            {
                _sandwormTiles.Remove(worm.Tiles[^1]);
                worm.Tiles.RemoveAt(worm.Tiles.Count - 1);
            }
            worm.Tiles.Insert(0, step);
            _sandwormTiles.Add(step, id);
            if (worm.Tiles.Count == SandwormSplitLength)
                SplitSandworm(worm);
        }
    }

    private void MoveSandwormHead(int id, Sandworm worm, int tile)
    {
        var preyId = SandwormPreyAt(worm, tile);
        if (preyId != 0)
        {
            // One unit per segment gives a five-segment prey exactly 2.5 meals.
            worm.MealUnits += _sandworms[preyId].Tiles.Count;
            RemoveCritter(new CritterId(preyId));
        }
        var prey = _occupants[tile];
        if (prey >= 0)
        {
            RemoveCritterAtIndex(prey);
            worm.MealUnits += MealUnitsPerCritter;
        }
        MoveCritter(_critterIndicesById[id], tile, GetPosition(tile), activateTeleporter: false);
    }

    private void SplitSandworm(Sandworm worm)
    {
        var tiles = worm.Tiles;
        // Keep both daughters contiguous. If a passing critter is on the
        // preferred second head tile, look for a clear spot along the tail.
        var secondHead = tiles.Count - SandwormDaughterLength;
        for (var candidate = secondHead; candidate >= SandwormDaughterLength; candidate--)
        {
            if (_occupants[tiles[candidate]] >= 0) continue;
            secondHead = candidate;
            break;
        }

        var daughter = tiles.GetRange(secondHead, SandwormDaughterLength);
        var prey = _occupants[daughter[0]];
        if (prey >= 0) RemoveCritterAtIndex(prey);
        for (var index = SandwormDaughterLength; index < tiles.Count; index++)
            _sandwormTiles.Remove(tiles[index]);
        tiles.RemoveRange(SandwormDaughterLength, tiles.Count - SandwormDaughterLength);
        worm.MealUnits = 0;
        AddSandworm(daughter, mealUnits: prey >= 0 ? MealUnitsPerCritter : 0);
    }

    private int FindSandwormStep(Sandworm worm)
    {
        var tiles = worm.Tiles;
        var head = GetPosition(tiles[0]);
        var tail = tiles[^1];
        Span<int> steps = stackalloc int[CardinalDirections.Length];
        var count = 0;
        var reverse = -1;
        foreach (var direction in CardinalDirections)
        {
            var next = SandwormPathTile(worm, head, direction, tail);
            if (next < 0 || steps[..count].Contains(next)) continue;
            if (next == tiles[1])
            {
                // A two-segment worm can step onto its vacating tail to turn
                // back down a narrow passage without flipping its trail.
                reverse = next;
                continue;
            }
            steps[count++] = next;
        }
        var patrolCount = count;
        if (reverse >= 0) steps[count++] = reverse;
        if (count == 0) return -1;

        // Breadth-first search finds the nearest prey reachable through this
        // desert, even across wrapping or planetoid face boundaries.
        if (HasSandwormPrey(worm))
        {
            _sandwormVisited.Clear();
            _sandwormSearch.Clear();
            foreach (var step in steps[..count])
            {
                _sandwormVisited.Add(step);
                _sandwormSearch.Enqueue((step, step));
            }
            while (_sandwormSearch.TryDequeue(out var entry))
            {
                if (_occupants[entry.Tile] >= 0 || SandwormPreyAt(worm, entry.Tile) != 0)
                    return entry.FirstStep;
                var position = GetPosition(entry.Tile);
                foreach (var direction in CardinalDirections)
                {
                    var next = SandwormPathTile(worm, position, direction, tail);
                    if (next >= 0 && _sandwormVisited.Add(next))
                        _sandwormSearch.Enqueue((next, entry.FirstStep));
                }
            }
        }
        return steps[NextInt(Math.Max(1, patrolCount))];
    }

    private bool HasSandwormPrey(Sandworm hunter)
    {
        if (_sandworms.Values.Any(prey => CanHuntSandworm(hunter, prey)))
            return true;
        for (var index = 0; index < _count; index++)
            if (_species[index] is not CritterSpecies.Sandworm && IsSandwormHabitat(_positions[index]))
                return true;
        return false;
    }

    private static bool CanHuntSandworm(Sandworm hunter, Sandworm prey) =>
        hunter.Tiles.Count - prey.Tiles.Count >= SandwormHuntingLengthAdvantage;

    private int SandwormPreyAt(Sandworm hunter, int tile) =>
        _sandwormTiles.TryGetValue(tile, out var id) && CanHuntSandworm(hunter, _sandworms[id]) ? id : 0;

    private int SandwormPathTile(Sandworm hunter, GridPosition position, GridPosition direction, int openTail)
    {
        var neighbor = OffsetSandwormTile(position, direction);
        if (!IsSandwormHabitat(neighbor)) return -1;
        var tile = GetIndex(neighbor);
        return tile == openTail || !_sandwormTiles.ContainsKey(tile) || SandwormPreyAt(hunter, tile) != 0 ? tile : -1;
    }
}
