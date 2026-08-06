namespace Newt.Simulation;

/// <summary>
/// Owns deterministic world state. It has no dependency on MonoGame, wall-clock
/// time, rendering, or input, so the same seed and commands produce the same run.
/// </summary>
public sealed class SimulationWorld
{
    public const int TicksPerSecond = 20;

    private static readonly GridPosition[] Directions =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    private readonly Terrain[] _terrain;
    private readonly int[] _occupants;
    private readonly CritterSpecies[] _species;
    private readonly GridPosition[] _positions;
    private readonly long[] _nextMovementTicks;
    private ulong _randomState;
    private int _count;

    public SimulationWorld(int width, int height, Terrain defaultTerrain, ulong seed = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        _terrain = new Terrain[checked(width * height)];
        Array.Fill(_terrain, defaultTerrain);
        _occupants = new int[_terrain.Length];
        Array.Fill(_occupants, -1);
        _species = new CritterSpecies[_terrain.Length];
        _positions = new GridPosition[_terrain.Length];
        _nextMovementTicks = new long[_terrain.Length];
        _randomState = seed == 0 ? 1 : seed;
    }

    public int Width { get; }

    public int Height { get; }

    public long Tick { get; private set; }

    public int CritterCount => _count;

    public Terrain GetTerrain(GridPosition position) => _terrain[GetIndex(position)];

    public void SetTerrain(GridPosition position, Terrain terrain) =>
        _terrain[GetIndex(position)] = terrain;

    public CritterId AddCritter(CritterSpecies species, GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (_occupants[tileIndex] >= 0)
        {
            throw new InvalidOperationException($"Tile {position} is already occupied.");
        }

        if (!CanLiveOn(species, _terrain[tileIndex]))
        {
            throw new InvalidOperationException($"{species} cannot live on {_terrain[tileIndex]}.");
        }

        var index = _count++;
        _species[index] = species;
        _positions[index] = position;
        _nextMovementTicks[index] = Tick + GetMovementIntervalTicks(species);
        _occupants[tileIndex] = index;
        return new CritterId(index + 1);
    }

    public CritterSnapshot GetCritter(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new CritterSnapshot(new CritterId(index + 1), _species[index], _positions[index]);
    }

    /// <summary>Advances exactly one fixed simulation tick.</summary>
    public void AdvanceOneTick()
    {
        Tick++;
        for (var index = 0; index < _count; index++)
        {
            if (Tick < _nextMovementTicks[index])
            {
                continue;
            }

            _nextMovementTicks[index] += GetMovementIntervalTicks(_species[index]);
            TryMove(index);
        }
    }

    private void TryMove(int critterIndex)
    {
        var startDirection = NextInt(Directions.Length);
        var current = _positions[critterIndex];
        for (var offset = 0; offset < Directions.Length; offset++)
        {
            var direction = Directions[(startDirection + offset) % Directions.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var destinationIndex = GetIndex(candidate);
            if (_occupants[destinationIndex] >= 0 ||
                !CanLiveOn(_species[critterIndex], _terrain[destinationIndex]))
            {
                continue;
            }

            _occupants[GetIndex(current)] = -1;
            _occupants[destinationIndex] = critterIndex;
            _positions[critterIndex] = candidate;
            return;
        }
    }

    private int GetIndex(GridPosition position)
    {
        if (position.X < 0 || position.X >= Width || position.Y < 0 || position.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return position.Y * Width + position.X;
    }

    private static bool CanLiveOn(CritterSpecies species, Terrain terrain) => species switch
    {
        CritterSpecies.Plankton => terrain is Terrain.Ocean or Terrain.Shallows,
        CritterSpecies.Crab => terrain is Terrain.Shallows,
        CritterSpecies.Ape => terrain is Terrain.Grass or Terrain.Shallows,
        _ => false,
    };

    private static int GetMovementIntervalTicks(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => 5 * TicksPerSecond,
        CritterSpecies.Crab => 6,
        CritterSpecies.Ape => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    private int NextInt(int exclusiveMaximum)
    {
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 7;
        _randomState ^= _randomState << 17;
        return (int)(_randomState % (uint)exclusiveMaximum);
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
