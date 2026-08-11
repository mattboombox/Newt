namespace Newt.Simulation;

/// <summary>
/// Owns deterministic world state. It has no dependency on MonoGame, wall-clock
/// time, rendering, or input, so the same seed and commands produce the same run.
/// </summary>
public sealed class SimulationWorld
{
    public const int TicksPerSecond = 20;
    public const float MinimumGroundElevation = -1f;
    public const float MaximumGroundElevation = 2f;
    public const float MinimumSeaLevel = -1f;
    public const float MaximumSeaLevel = 1f;
    public const float MinimumGlobalClimateOffset = -1f;
    public const float MaximumGlobalClimateOffset = 1f;

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
    private readonly SurfaceCover[] _surfaceCovers;
    private readonly long[] _surfaceCoverUntilTicks;
    private readonly HashSet<GridPosition> _activeSurfaceCovers = [];
    private readonly SurfaceWaterKind[] _surfaceWater;
    private readonly float[] _waterSurfaceElevations;
    private readonly RiverConnection[] _riverConnections;
    private readonly List<ActiveSpring> _activeSprings = [];
    private readonly List<SpringSource> _springSources = [];
    private readonly List<VolcanoActivity> _volcanoes = [];
    private readonly List<LavaFlowActivity> _lavaFlows = [];
    private readonly List<ImpactWaveActivity> _impactWaves = [];
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
        _surfaceCovers = new SurfaceCover[_terrain.Length];
        _surfaceCoverUntilTicks = new long[_terrain.Length];
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
        OceanSeed = new GridPosition(width / 2, height / 2);
    }

    public int Width { get; }

    public int Height { get; }

    public ulong Seed { get; }

    public long Tick { get; private set; }

    public long SeasonTick { get; internal set; }

    public long Year => SeasonTick / SeasonSystem.TicksPerYear;

    /// <summary>The absolute elevation of the globally connected ocean surface.</summary>
    public float SeaLevel { get; internal set; }

    /// <summary>The single source from which globally connected saltwater spreads.</summary>
    public GridPosition OceanSeed { get; internal set; }

    public float GlobalTemperatureOffset { get; internal set; }

    public float GlobalMoistureOffset { get; internal set; }

    public bool SeasonsEnabled { get; internal set; } = true;

    public int CritterCount => _count;

    public int ActiveSpringCount => _activeSprings.Count;

    public int VolcanoCount => _volcanoes.Count;

    public int ActiveLavaFlowCount => _lavaFlows.Count;

    public int ActiveImpactWaveCount => _impactWaves.Count;

    public SpringResult? LastCompletedSpring { get; internal set; }

    public bool Contains(GridPosition position) =>
        position.X >= 0 && position.X < Width && position.Y >= 0 && position.Y < Height;

    public Terrain GetTerrain(GridPosition position) => _terrain[GetIndex(position)];

    /// <summary>Returns absolute ground elevation. Compare with <see cref="SeaLevel"/> for water depth.</summary>
    public float GetElevation(GridPosition position) => _elevation[GetIndex(position)];

    /// <summary>Returns normalized temperature, from zero (freezing) to one (hot).</summary>
    public float GetTemperature(GridPosition position) => _temperature[GetIndex(position)];

    public TemperatureBand GetTemperatureBand(GridPosition position) =>
        ClimateSystem.ClassifyTemperature(GetTemperature(position));

    /// <summary>Returns normalized moisture, from zero (arid) to one (saturated).</summary>
    public float GetMoisture(GridPosition position) => _moisture[GetIndex(position)];

    public MoistureBand GetMoistureBand(GridPosition position) =>
        ClimateSystem.ClassifyMoisture(GetMoisture(position));

    public Biome GetBiome(GridPosition position) => _biomes[GetIndex(position)];

    public SurfaceCover GetSurfaceCover(GridPosition position) => _surfaceCovers[GetIndex(position)];

    public VolcanoSnapshot GetVolcano(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _volcanoes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var volcano = _volcanoes[index];
        return new VolcanoSnapshot(volcano.Position, volcano.State);
    }

    public VolcanoState? GetVolcanoState(GridPosition position)
    {
        var volcano = _volcanoes.FirstOrDefault(candidate => candidate.Position == position);
        return volcano?.State;
    }

    public ImpactWaveSnapshot GetImpactWave(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _impactWaves.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var wave = _impactWaves[index];
        return new ImpactWaveSnapshot(
            wave.Center,
            wave.CurrentRadius,
            wave.MaximumRadius,
            wave.Magnitude);
    }

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
        _elevation[GetIndex(position)] = Math.Clamp(
            elevation,
            MinimumGroundElevation,
            MaximumGroundElevation);

    internal void SetTemperature(GridPosition position, float temperature) =>
        _temperature[GetIndex(position)] = Math.Clamp(temperature, 0, 1);

    internal void SetMoisture(GridPosition position, float moisture) =>
        _moisture[GetIndex(position)] = Math.Clamp(moisture, 0, 1);

    internal void SetBiome(GridPosition position, Biome biome) =>
        _biomes[GetIndex(position)] = biome;

    internal long GetSurfaceCoverUntilTick(GridPosition position) =>
        _surfaceCoverUntilTicks[GetIndex(position)];

    internal void SetSurfaceCover(GridPosition position, SurfaceCover cover, long untilTick)
    {
        var index = GetIndex(position);
        _surfaceCovers[index] = cover;
        _surfaceCoverUntilTicks[index] = untilTick;
        if (cover is SurfaceCover.None)
        {
            _activeSurfaceCovers.Remove(position);
        }
        else
        {
            _activeSurfaceCovers.Add(position);
        }
    }

    internal void SetSurfaceWater(GridPosition position, SurfaceWaterKind water) =>
        _surfaceWater[GetIndex(position)] = water;

    internal void SetWaterSurfaceElevation(GridPosition position, float? elevation) =>
        _waterSurfaceElevations[GetIndex(position)] = elevation ?? float.NaN;

    internal void AddRiverConnection(GridPosition position, RiverConnection connection) =>
        _riverConnections[GetIndex(position)] |= connection;

    internal List<ActiveSpring> ActiveSprings => _activeSprings;

    internal IReadOnlyList<SpringSource> SpringSources => _springSources;

    internal HashSet<GridPosition> ActiveSurfaceCovers => _activeSurfaceCovers;

    internal List<VolcanoActivity> Volcanoes => _volcanoes;

    internal List<LavaFlowActivity> LavaFlows => _lavaFlows;

    internal List<ImpactWaveActivity> ImpactWaves => _impactWaves;

    internal void RegisterSpringSource(GridPosition position, int maximumLength)
    {
        if (_springSources.All(source => source.Position != position))
        {
            _springSources.Add(new SpringSource(position, maximumLength));
        }
    }

    internal void RemoveSpringSources(IReadOnlySet<GridPosition> positions) =>
        _springSources.RemoveAll(source => positions.Contains(source.Position));

    internal void ClearFreshwater()
    {
        Array.Clear(_surfaceWater);
        Array.Fill(_waterSurfaceElevations, float.NaN);
        Array.Clear(_riverConnections);
        _activeSprings.Clear();
    }

    public CritterId AddCritter(CritterSpecies species, GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (_occupants[tileIndex] >= 0)
        {
            throw new InvalidOperationException($"Tile {position} is already occupied.");
        }

        if (_surfaceCovers[tileIndex] is not SurfaceCover.None ||
            !CanLiveOn(species, _terrain[tileIndex]))
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
        SeasonSystem.Advance(this);
        Impacts.Advance(this);
        Volcanism.Advance(this);
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
                _surfaceCovers[destinationIndex] is not SurfaceCover.None ||
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

    internal bool RemoveCritterAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        var critterIndex = _occupants[tileIndex];
        if (critterIndex < 0)
        {
            return false;
        }

        var lastIndex = _count - 1;
        _occupants[tileIndex] = -1;
        if (critterIndex != lastIndex)
        {
            _species[critterIndex] = _species[lastIndex];
            _positions[critterIndex] = _positions[lastIndex];
            _nextMovementTicks[critterIndex] = _nextMovementTicks[lastIndex];
            _occupants[GetIndex(_positions[critterIndex])] = critterIndex;
        }

        _count--;
        return true;
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

    internal int NextInt(int exclusiveMaximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
        _randomState ^= _randomState << 13;
        _randomState ^= _randomState >> 7;
        _randomState ^= _randomState << 17;
        return (int)(_randomState % (uint)exclusiveMaximum);
    }

    internal float NextUnitFloat() => NextInt(1 << 24) * (1f / (1 << 24));

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
