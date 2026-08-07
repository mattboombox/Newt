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
    private readonly float[] _elevation;
    private readonly float[] _temperature;
    private readonly float[] _moisture;
    private readonly Biome[] _biomes;
    private readonly SurfaceWaterKind[] _surfaceWater;
    private readonly float[] _waterSurfaceElevations;
    private readonly RiverConnection[] _riverConnections;
    private readonly List<ActiveSpring> _activeSprings = [];
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
        Seed = seed == 0 ? 1 : seed;
        _terrain = new Terrain[checked(width * height)];
        Array.Fill(_terrain, defaultTerrain);
        _elevation = new float[_terrain.Length];
        _temperature = new float[_terrain.Length];
        _moisture = new float[_terrain.Length];
        _biomes = new Biome[_terrain.Length];
        _surfaceWater = new SurfaceWaterKind[_terrain.Length];
        _waterSurfaceElevations = new float[_terrain.Length];
        Array.Fill(_waterSurfaceElevations, float.NaN);
        _riverConnections = new RiverConnection[_terrain.Length];
        _occupants = new int[_terrain.Length];
        Array.Fill(_occupants, -1);
        _species = new CritterSpecies[_terrain.Length];
        _positions = new GridPosition[_terrain.Length];
        _nextMovementTicks = new long[_terrain.Length];
        _randomState = Seed;
    }

    public int Width { get; }

    public int Height { get; }

    public ulong Seed { get; }

    public long Tick { get; private set; }

    public int CritterCount => _count;

    public int ActiveSpringCount => _activeSprings.Count;

    public SpringResult? LastCompletedSpring { get; internal set; }

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    public Terrain GetTerrain(GridPosition position) => _terrain[GetIndex(position)];

    /// <summary>Returns signed elevation relative to sea level at zero.</summary>
    public float GetElevation(GridPosition position) => _elevation[GetIndex(position)];

    /// <summary>Returns normalized temperature, from zero (freezing) to one (hot).</summary>
    public float GetTemperature(GridPosition position) => _temperature[GetIndex(position)];

    public TemperatureBand GetTemperatureBand(GridPosition position) =>
        ClimateSystem.ClassifyTemperature(GetTemperature(position));

    /// <summary>Returns normalized moisture, from zero (arid) to one (saturated).</summary>
    public float GetMoisture(GridPosition position) => _moisture[GetIndex(position)];

    public Biome GetBiome(GridPosition position) => _biomes[GetIndex(position)];

    public SurfaceWaterKind GetSurfaceWater(GridPosition position) =>
        _surfaceWater[GetIndex(position)];

    public float? GetWaterSurfaceElevation(GridPosition position)
    {
        var elevation = _waterSurfaceElevations[GetIndex(position)];
        return float.IsNaN(elevation) ? null : elevation;
    }

    public float GetWaterDepth(GridPosition position)
    {
        var surface = GetWaterSurfaceElevation(position);
        return surface is null ? 0 : Math.Max(0, surface.Value - GetElevation(position));
    }

    public RiverConnection GetRiverConnections(GridPosition position) =>
        _riverConnections[GetIndex(position)];

    public bool IsOccupied(GridPosition position) => _occupants[GetIndex(position)] >= 0;

    public void SetTerrain(GridPosition position, Terrain terrain) =>
        _terrain[GetIndex(position)] = terrain;

    internal void SetElevation(GridPosition position, float elevation) =>
        _elevation[GetIndex(position)] = elevation;

    internal void SetTemperature(GridPosition position, float temperature) =>
        _temperature[GetIndex(position)] = Math.Clamp(temperature, 0, 1);

    internal void SetMoisture(GridPosition position, float moisture) =>
        _moisture[GetIndex(position)] = Math.Clamp(moisture, 0, 1);

    internal void SetBiome(GridPosition position, Biome biome) =>
        _biomes[GetIndex(position)] = biome;

    internal void SetSurfaceWater(GridPosition position, SurfaceWaterKind water) =>
        _surfaceWater[GetIndex(position)] = water;

    internal void SetWaterSurfaceElevation(GridPosition position, float? elevation) =>
        _waterSurfaceElevations[GetIndex(position)] = elevation ?? float.NaN;

    internal void AddRiverConnection(GridPosition position, RiverConnection connection) =>
        _riverConnections[GetIndex(position)] |= connection;

    internal List<ActiveSpring> ActiveSprings => _activeSprings;

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
        Hydrology.AdvanceSprings(this);
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
        CritterSpecies.Plankton => terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows,
        CritterSpecies.Crab => terrain is Terrain.Shallows or Terrain.Beach,
        CritterSpecies.Ape => terrain is Terrain.Plains or Terrain.Beach,
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
