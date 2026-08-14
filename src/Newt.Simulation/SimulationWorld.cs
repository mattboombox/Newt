namespace Newt.Simulation;

/// <summary>
/// Owns deterministic world state. It has no dependency on MonoGame, wall-clock
/// time, rendering, or input, so the same seed and commands produce the same run.
/// </summary>
public sealed class SimulationWorld
{
    private const int FishPerceptionRadius = 6;
    private const int MegaToadPerceptionRadius = 3;
    private const int NewtFreshwaterPerceptionRadius = 8;
    public const int TicksPerSecond = 20;
    public const float MinimumGroundElevation = -1f;
    public const float MaximumGroundElevation = 2f;
    public const float RingWorldWallElevation = 2.15f;
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
    private readonly long[] _lifeRecoveryUntilTicks;
    private readonly List<LifeRecoveryTile> _lifeRecoveryTiles = [];
    private readonly SurfaceWaterKind[] _surfaceWater;
    private readonly float[] _waterSurfaceElevations;
    private readonly RiverConnection[] _riverConnections;
    private readonly List<ActiveSpring> _activeSprings = [];
    private readonly List<SpringSource> _springSources = [];
    private readonly List<VolcanoActivity> _volcanoes = [];
    private readonly List<LavaFlowActivity> _lavaFlows = [];
    private readonly List<ImpactWaveActivity> _impactWaves = [];
    private readonly List<GridPosition> _additionalOceanSeeds = [];
    private readonly int[] _occupants;
    private readonly CritterId[] _critterIds;
    private readonly Dictionary<int, int> _critterIndicesById = [];
    private readonly Dictionary<int, Queue<GridPosition>> _newtMigrationPaths = [];
    private readonly HashSet<int> _newtFreshwaterSearchCompleted = [];
    private readonly CritterSpecies[] _species;
    private readonly GridPosition[] _positions;
    private readonly long[] _nextMovementTicks;
    private readonly int[] _energy;
    private readonly long[] _nextMetabolismTicks;
    private readonly long[] _nextAmbientFeedingTicks;
    private readonly int[] _preyTargets;
    private readonly int[] _speciesCounts = new int[Enum.GetValues<CritterSpecies>().Length];
    private ulong _randomState;
    private int _nextCritterId = 1;
    private long _freshwaterNavigationRevision;
    private long _newtNavigationRevision = -1;
    private int[]? _newtNavigationNext;
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
        _lifeRecoveryUntilTicks = new long[_terrain.Length];
        _surfaceWater = new SurfaceWaterKind[_terrain.Length];
        _waterSurfaceElevations = new float[_terrain.Length];
        Array.Fill(_waterSurfaceElevations, float.NaN);
        _riverConnections = new RiverConnection[_terrain.Length];
        _occupants = new int[_terrain.Length];
        Array.Fill(_occupants, -1);
        _critterIds = new CritterId[_terrain.Length];
        _species = new CritterSpecies[_terrain.Length];
        _positions = new GridPosition[_terrain.Length];
        _nextMovementTicks = new long[_terrain.Length];
        _energy = new int[_terrain.Length];
        _nextMetabolismTicks = new long[_terrain.Length];
        _nextAmbientFeedingTicks = new long[_terrain.Length];
        _preyTargets = new int[_terrain.Length];
        Array.Fill(_preyTargets, -1);
        _randomState = Seed;
        OceanSeed = new GridPosition(width / 2, height / 2);
    }

    public int Width { get; }

    public int Height { get; }

    public ulong Seed { get; }

    public WorldBody Body { get; internal set; }

    public bool HasOceans { get; internal set; } = true;

    public long Tick { get; private set; }

    public long SeasonTick { get; internal set; }

    public long Year => SeasonTick / SeasonSystem.TicksPerYear;

    /// <summary>The absolute elevation of the globally connected ocean surface.</summary>
    public float SeaLevel { get; internal set; }

    /// <summary>The single source from which globally connected saltwater spreads.</summary>
    public GridPosition OceanSeed { get; internal set; }

    public IReadOnlyList<GridPosition> AdditionalOceanSeeds => _additionalOceanSeeds;

    public float GlobalTemperatureOffset { get; internal set; }

    public float GlobalMoistureOffset { get; internal set; }

    public bool SeasonsEnabled { get; internal set; } = true;

    public bool NaturalEventsEnabled { get; internal set; } = true;

    public bool LifeEnabled { get; internal set; } = true;

    public int EvolutionChanceSteps { get; private set; } = CritterEvolution.DefaultChanceSteps;

    public float EvolutionChancePercent =>
        EvolutionChanceSteps / (float)CritterEvolution.ChanceStepsPerPercent;

    internal long NextNaturalEventTick { get; set; } = 6 * 60 * TicksPerSecond;

    public int CritterCount => _count;

    internal int NewtNavigationBuildCount { get; private set; }

    public int GetCritterCount(CritterSpecies species) => _speciesCounts[(int)species];

    public bool PlanktonRecoveryEnabled { get; private set; }

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
            wave.Magnitude,
            wave.Kind);
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

    public void SetTerrain(GridPosition position, Terrain terrain)
    {
        var index = GetIndex(position);
        if (_terrain[index] != terrain)
        {
            _terrain[index] = terrain;
            _freshwaterNavigationRevision++;
        }
    }

    internal void SetElevation(GridPosition position, float elevation) =>
        _elevation[GetIndex(position)] = Math.Clamp(
            elevation,
            MinimumGroundElevation,
            MaximumGroundElevation);

    internal void SetStructuralTerrain(
        GridPosition position,
        Terrain terrain,
        float elevation)
    {
        var index = GetIndex(position);
        _terrain[index] = terrain;
        _elevation[index] = elevation;
        _surfaceCovers[index] = SurfaceCover.None;
        _surfaceCoverUntilTicks[index] = 0;
        _activeSurfaceCovers.Remove(position);
        RemoveCritterAt(position);
        _freshwaterNavigationRevision++;
    }

    internal void SetTemperature(GridPosition position, float temperature) =>
        _temperature[GetIndex(position)] = Math.Clamp(temperature, 0, 1);

    internal void SetMoisture(GridPosition position, float moisture) =>
        _moisture[GetIndex(position)] = Math.Clamp(moisture, 0, 1);

    internal void SetBiome(GridPosition position, Biome biome)
    {
        var index = GetIndex(position);
        var previousBiome = _biomes[index];
        if (previousBiome == biome)
        {
            return;
        }

        _biomes[index] = biome;
        if (_surfaceWater[index] is SurfaceWaterKind.FreshwaterLake &&
            (previousBiome is Biome.Arctic) != (biome is Biome.Arctic))
        {
            _freshwaterNavigationRevision++;
        }
    }

    internal long GetSurfaceCoverUntilTick(GridPosition position) =>
        _surfaceCoverUntilTicks[GetIndex(position)];

    internal void SetSurfaceCover(GridPosition position, SurfaceCover cover, long untilTick)
    {
        var index = GetIndex(position);
        var previousCover = _surfaceCovers[index];
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

        if (cover is SurfaceCover.Stone)
        {
            _biomes[index] = Biome.None;
        }
        else if (cover is SurfaceCover.None && previousCover is SurfaceCover.Stone)
        {
            ClimateSystem.RebuildBiomeAt(this, position);
        }
        if (cover != previousCover)
        {
            _freshwaterNavigationRevision++;
        }
    }

    internal void SetSurfaceWater(GridPosition position, SurfaceWaterKind water)
    {
        var index = GetIndex(position);
        if (_surfaceWater[index] != water)
        {
            _surfaceWater[index] = water;
            _freshwaterNavigationRevision++;
        }
    }

    internal void SetWaterSurfaceElevation(GridPosition position, float? elevation) =>
        _waterSurfaceElevations[GetIndex(position)] = elevation ?? float.NaN;

    internal void AddRiverConnection(GridPosition position, RiverConnection connection) =>
        _riverConnections[GetIndex(position)] |= connection;

    internal List<ActiveSpring> ActiveSprings => _activeSprings;

    internal IReadOnlyList<SpringSource> SpringSources => _springSources;

    internal HashSet<GridPosition> ActiveSurfaceCovers => _activeSurfaceCovers;

    internal List<LifeRecoveryTile> LifeRecoveryTiles => _lifeRecoveryTiles;

    internal int LifeRecoveryIndex { get; set; }

    internal bool IsLifeRecoveryPending(GridPosition position) =>
        _lifeRecoveryUntilTicks[GetIndex(position)] > 0;

    internal void SetLifeRecovery(GridPosition position, long untilTick) =>
        _lifeRecoveryUntilTicks[GetIndex(position)] = untilTick;

    internal void ClearLifeRecoveryAt(GridPosition position) =>
        _lifeRecoveryUntilTicks[GetIndex(position)] = 0;

    internal void ClearLifeRecovery()
    {
        Array.Clear(_lifeRecoveryUntilTicks);
        _lifeRecoveryTiles.Clear();
        LifeRecoveryIndex = 0;
    }

    internal List<VolcanoActivity> Volcanoes => _volcanoes;

    internal List<LavaFlowActivity> LavaFlows => _lavaFlows;

    internal bool VolcanicTerrainRefreshPending { get; set; }

    internal bool VolcanicFreshwaterRefreshPending { get; set; }

    internal long LastVolcanicTerrainRefreshTick { get; set; }

    internal List<ImpactWaveActivity> ImpactWaves => _impactWaves;

    internal void SetAdditionalOceanSeeds(IEnumerable<GridPosition> seeds)
    {
        _additionalOceanSeeds.Clear();
        _additionalOceanSeeds.AddRange(seeds.Where(seed => seed != OceanSeed));
    }

    internal void RegisterSpringSource(GridPosition position, SpringOrigin origin)
    {
        var index = _springSources.FindIndex(source => source.Position == position);
        if (index < 0)
        {
            _springSources.Add(new SpringSource(position, origin));
        }
        else if (origin is SpringOrigin.Player)
        {
            // Player ownership is permanent and cannot be downgraded by a
            // later natural event or freshwater rebuild.
            _springSources[index] = new SpringSource(position, SpringOrigin.Player);
        }
    }

    internal void RemoveSpringSources(IReadOnlySet<GridPosition> positions) =>
        _springSources.RemoveAll(source => positions.Contains(source.Position));

    internal bool RemoveNaturalSpringSource(GridPosition position) =>
        _springSources.RemoveAll(source =>
            source.Position == position && source.Origin is SpringOrigin.Natural) > 0;

    internal void ClearFreshwater()
    {
        Array.Clear(_surfaceWater);
        Array.Fill(_waterSurfaceElevations, float.NaN);
        Array.Clear(_riverConnections);
        _activeSprings.Clear();
        _freshwaterNavigationRevision++;
    }

    public CritterId AddCritter(CritterSpecies species, GridPosition position)
    {
        if (!LifeEnabled)
        {
            throw new InvalidOperationException("Life is disabled in this world.");
        }
        var tileIndex = GetIndex(position);
        if (_occupants[tileIndex] >= 0)
        {
            throw new InvalidOperationException($"Tile {position} is already occupied.");
        }

        if (!CanLiveOn(species, tileIndex))
        {
            throw new InvalidOperationException($"{species} cannot live on {_terrain[tileIndex]}.");
        }

        var index = _count++;
        var id = new CritterId(_nextCritterId++);
        _critterIds[index] = id;
        _critterIndicesById.Add(id.Value, index);
        _species[index] = species;
        _positions[index] = position;
        _nextMovementTicks[index] = GetFirstMovementTick(species);
        var nutrition = CritterNutritions.Get(species);
        _energy[index] = nutrition.InitialEnergy;
        _nextMetabolismTicks[index] = nutrition.HasMetabolism
            ? Tick + nutrition.MetabolismIntervalTicks
            : long.MaxValue;
        _nextAmbientFeedingTicks[index] = nutrition.FeedsFromEnvironment
            ? Tick + nutrition.AmbientFeedingIntervalTicks
            : long.MaxValue;
        _preyTargets[index] = -1;
        _newtMigrationPaths.Remove(id.Value);
        _newtFreshwaterSearchCompleted.Remove(id.Value);
        _occupants[tileIndex] = index;
        _speciesCounts[(int)species]++;
        return id;
    }

    public bool TryAddCritter(CritterSpecies species, GridPosition position)
    {
        if (!LifeEnabled || !Contains(position))
        {
            return false;
        }

        var index = GetIndex(position);
        if (_occupants[index] >= 0 || !CanLiveOn(species, index))
        {
            return false;
        }

        AddCritter(species, position);
        return true;
    }

    public void AdjustEvolutionChance(int stepDelta)
    {
        EvolutionChanceSteps = Math.Clamp(
            EvolutionChanceSteps + stepDelta,
            0,
            CritterEvolution.MaximumChanceSteps);
    }

    public bool TryEvolveCritterAt(GridPosition position) =>
        TryChangeCritterSpecies(position, evolve: true);

    public bool TryDevolveCritterAt(GridPosition position) =>
        TryChangeCritterSpecies(position, evolve: false);

    /// <summary>Seeds one plankton and restores one after global extinction.</summary>
    public bool EnablePlanktonRecovery()
    {
        if (!LifeEnabled)
        {
            return false;
        }
        PlanktonRecoveryEnabled = true;
        return EnsurePlanktonPopulation();
    }

    internal void DisablePlanktonRecovery() => PlanktonRecoveryEnabled = false;

    public CritterSnapshot GetCritter(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var nutrition = CritterNutritions.Get(_species[index]);
        return new CritterSnapshot(
            _critterIds[index],
            _species[index],
            _positions[index],
            _energy[index],
            nutrition.MaximumEnergy,
            nutrition.HasMetabolism && _energy[index] <= nutrition.HungryThreshold,
            nutrition.CanReproduce && _energy[index] >= nutrition.ReproductionThreshold);
    }

    public bool TryGetCritter(CritterId id, out CritterSnapshot critter)
    {
        if (id.IsValid && _critterIndicesById.TryGetValue(id.Value, out var index))
        {
            critter = GetCritter(index);
            return true;
        }

        critter = default;
        return false;
    }

    public bool TryGetCritterAt(GridPosition position, out CritterSnapshot critter)
    {
        if (Contains(position))
        {
            var index = _occupants[GetIndex(position)];
            if (index >= 0)
            {
                critter = GetCritter(index);
                return true;
            }
        }

        critter = default;
        return false;
    }

    /// <summary>Advances exactly one fixed simulation tick.</summary>
    public void AdvanceOneTick()
    {
        Tick++;
        SeasonSystem.Advance(this);
        NaturalEvents.Advance(this);
        Impacts.Advance(this);
        Volcanism.Advance(this);
        Hydrology.AdvanceSprings(this);
        LifeSystem.Advance(this);
        AdvanceCritterLifecycles();
        if (PlanktonRecoveryEnabled && GetCritterCount(CritterSpecies.Plankton) == 0)
        {
            EnsurePlanktonPopulation();
        }
        AdvanceCritterMovements();
    }

    private void AdvanceCritterMovements()
    {
        List<(GridPosition Predator, GridPosition Prey)>? predations = null;
        HashSet<GridPosition>? reservedPrey = null;
        for (var index = 0; index < _count; index++)
        {
            if (reservedPrey?.Contains(_positions[index]) is true)
            {
                continue;
            }
            if (Tick < _nextMovementTicks[index])
            {
                continue;
            }

            _nextMovementTicks[index] += GetMovementIntervalTicks(_species[index]);
            var prey = _species[index] switch
            {
                CritterSpecies.Fish => TryMoveHunter(index, FishPerceptionRadius, reservedPrey),
                CritterSpecies.MegaToad => TryMoveHunter(index, MegaToadPerceptionRadius, reservedPrey),
                CritterSpecies.Newt => TryMoveNewt(index),
                CritterSpecies.Worm => TryMoveWorm(index),
                _ => TryMove(index, reservedPrey),
            };
            if (prey is not null)
            {
                reservedPrey ??= [];
                reservedPrey.Add(prey.Value);
                (predations ??= []).Add((_positions[index], prey.Value));
            }
        }

        if (predations is null)
        {
            return;
        }

        foreach (var predation in predations)
        {
            CommitPredation(predation.Predator, predation.Prey);
        }
    }

    private void AdvanceCritterLifecycles()
    {
        List<GridPosition>? deaths = null;
        List<(CritterSpecies Species, GridPosition Position)>? births = null;
        HashSet<GridPosition>? reservedBirthTiles = null;

        // Births and deaths are committed after every existing critter has had
        // one lifecycle turn. A newborn therefore never acts on its birth tick.
        for (var index = 0; index < _count; index++)
        {
            var species = _species[index];
            var nutrition = CritterNutritions.Get(species);
            if (!CanLiveOn(species, GetIndex(_positions[index])))
            {
                (deaths ??= []).Add(_positions[index]);
                continue;
            }

            if (nutrition.FeedsFromEnvironment && Tick >= _nextAmbientFeedingTicks[index])
            {
                AdvanceSchedule(
                    ref _nextAmbientFeedingTicks[index],
                    nutrition.AmbientFeedingIntervalTicks);
                if (CanFeedFromEnvironment(species, GetIndex(_positions[index])))
                {
                    _energy[index] = Math.Min(
                        nutrition.MaximumEnergy,
                        _energy[index] + nutrition.AmbientFoodEnergy);
                }
            }

            if (nutrition.HasMetabolism && Tick >= _nextMetabolismTicks[index])
            {
                AdvanceSchedule(ref _nextMetabolismTicks[index], nutrition.MetabolismIntervalTicks);
                _energy[index] = Math.Max(0, _energy[index] - nutrition.MetabolismCost);
                if (_energy[index] == 0)
                {
                    (deaths ??= []).Add(_positions[index]);
                    continue;
                }
            }

            if (!nutrition.CanReproduce || _energy[index] < nutrition.ReproductionThreshold)
            {
                continue;
            }

            reservedBirthTiles ??= [];
            var offspringSpecies = ChooseOffspringSpecies(species);
            var birthPosition = FindBirthPosition(index, offspringSpecies, reservedBirthTiles);
            if (birthPosition is null)
            {
                continue;
            }

            reservedBirthTiles.Add(birthPosition.Value);
            (births ??= []).Add((offspringSpecies, birthPosition.Value));
            _energy[index] -= nutrition.ReproductionCost;
        }

        if (deaths is not null)
        {
            foreach (var position in deaths)
            {
                RemoveCritterAt(position);
            }
        }

        if (births is not null)
        {
            foreach (var birth in births)
            {
                TryAddCritter(birth.Species, birth.Position);
            }
        }
    }

    internal CritterSpecies ChooseOffspringSpecies(CritterSpecies parentSpecies)
    {
        var branchCount = CritterEvolution.GetEvolvedSpeciesCount(parentSpecies);
        var branchIndex = branchCount > 0 ? NextInt(branchCount) : 0;
        return CritterEvolution.ChooseOffspring(
            parentSpecies,
            NextInt(CritterEvolution.MaximumChanceSteps),
            EvolutionChanceSteps,
            branchIndex);
    }

    private bool TryChangeCritterSpecies(GridPosition position, bool evolve)
    {
        if (!Contains(position))
        {
            return false;
        }

        var tileIndex = GetIndex(position);
        var critterIndex = _occupants[tileIndex];
        if (critterIndex < 0)
        {
            return false;
        }

        var currentSpecies = _species[critterIndex];
        var branchCount = CritterEvolution.GetEvolvedSpeciesCount(currentSpecies);
        var changed = evolve
            ? CritterEvolution.TryGetEvolvedSpecies(
                currentSpecies,
                branchCount > 0 ? NextInt(branchCount) : 0,
                out var targetSpecies)
            : CritterEvolution.TryGetDevolvedSpecies(currentSpecies, out targetSpecies);
        if (!changed || !CanLiveOn(targetSpecies, tileIndex))
        {
            return false;
        }

        _speciesCounts[(int)currentSpecies]--;
        _speciesCounts[(int)targetSpecies]++;
        _species[critterIndex] = targetSpecies;
        var critterId = _critterIds[critterIndex].Value;
        _newtMigrationPaths.Remove(critterId);
        _newtFreshwaterSearchCompleted.Remove(critterId);
        var nutrition = CritterNutritions.Get(targetSpecies);
        _energy[critterIndex] = nutrition.MaximumEnergy > 0
            ? Math.Clamp(_energy[critterIndex], 1, nutrition.MaximumEnergy)
            : _energy[critterIndex];
        _nextMovementTicks[critterIndex] = GetFirstMovementTick(targetSpecies);
        _nextMetabolismTicks[critterIndex] = nutrition.HasMetabolism
            ? Tick + nutrition.MetabolismIntervalTicks
            : long.MaxValue;
        _nextAmbientFeedingTicks[critterIndex] = nutrition.FeedsFromEnvironment
            ? Tick + nutrition.AmbientFeedingIntervalTicks
            : long.MaxValue;
        return true;
    }

    private GridPosition? FindBirthPosition(
        int parentIndex,
        CritterSpecies offspringSpecies,
        IReadOnlySet<GridPosition> reservedBirthTiles)
    {
        var startDirection = NextInt(Directions.Length);
        var parentPosition = _positions[parentIndex];
        for (var offset = 0; offset < Directions.Length; offset++)
        {
            var direction = Directions[(startDirection + offset) % Directions.Length];
            var candidate = new GridPosition(
                Mod(parentPosition.X + direction.X, Width),
                parentPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height || reservedBirthTiles.Contains(candidate))
            {
                continue;
            }

            var tileIndex = GetIndex(candidate);
            if (_occupants[tileIndex] < 0 && CanLiveOn(offspringSpecies, tileIndex))
            {
                return candidate;
            }
        }

        return null;
    }

    private void AdvanceSchedule(ref long nextTick, int intervalTicks)
    {
        do
        {
            nextTick += intervalTicks;
        }
        while (nextTick <= Tick);
    }

    private GridPosition? TryMove(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
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
            if (_occupants[destinationIndex] >= 0)
            {
                var preyIndex = _occupants[destinationIndex];
                if (CanEat(_species[critterIndex], _species[preyIndex]) &&
                    reservedPrey?.Contains(candidate) is not true)
                {
                    return candidate;
                }
                continue;
            }

            if (!CanLiveOn(_species[critterIndex], destinationIndex))
            {
                continue;
            }

            _occupants[GetIndex(current)] = -1;
            _occupants[destinationIndex] = critterIndex;
            _positions[critterIndex] = candidate;
            return null;
        }

        return null;
    }

    private GridPosition? TryMoveWorm(int critterIndex)
    {
        var current = _positions[critterIndex];
        if (_terrain[GetIndex(current)] is not Terrain.Shallows)
        {
            var startDirection = NextInt(Directions.Length);
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
                if (_terrain[destinationIndex] is Terrain.Shallows &&
                    _occupants[destinationIndex] < 0 &&
                    CanLiveOn(CritterSpecies.Worm, destinationIndex))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
            }
        }

        return TryMove(critterIndex);
    }

    private GridPosition? TryMoveHunter(
        int critterIndex,
        int perceptionRadius,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var predatorSpecies = _species[critterIndex];
        var target = GetValidHunterTarget(critterIndex, predatorSpecies, reservedPrey) ??
            FindHunterPrey(critterIndex, predatorSpecies, perceptionRadius, reservedPrey);
        if (target is null)
        {
            _preyTargets[critterIndex] = -1;
            return TryMove(critterIndex, reservedPrey);
        }

        _preyTargets[critterIndex] = GetIndex(target.Value);
        var current = _positions[critterIndex];
        var currentDistance = WrappedManhattanDistance(current, target.Value);
        var bestDistance = currentDistance;
        GridPosition? best = null;
        var startDirection = NextInt(Directions.Length);
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
            if (_occupants[destinationIndex] >= 0)
            {
                if (candidate == target && CanLiveOn(predatorSpecies, destinationIndex) &&
                    reservedPrey?.Contains(candidate) is not true)
                {
                    return candidate;
                }
                continue;
            }
            if (!CanLiveOn(predatorSpecies, destinationIndex))
            {
                continue;
            }

            var distance = WrappedManhattanDistance(candidate, target.Value);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best is null)
        {
            return TryMove(critterIndex, reservedPrey);
        }

        MoveCritter(critterIndex, GetIndex(best.Value), best.Value);
        return null;
    }

    private GridPosition? TryMoveNewt(int critterIndex)
    {
        var nutrition = CritterNutritions.Get(CritterSpecies.Newt);
        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            IsAtOrAdjacentToNewtFood(GetIndex(_positions[critterIndex])))
        {
            // A hungry Newt that has reached the shoreline waits for its next
            // feeding interval instead of wandering out of feeding range.
            return null;
        }

        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            TryApproachNearbyNewtFood(critterIndex))
        {
            return null;
        }

        var critterId = _critterIds[critterIndex].Value;
        if (!_newtFreshwaterSearchCompleted.Contains(critterId))
        {
            PlanNewtFreshwaterMigration(critterIndex);
        }

        if (!_newtMigrationPaths.TryGetValue(critterId, out var path) || path.Count == 0)
        {
            return TryWanderNewt(critterIndex);
        }

        var next = path.Peek();
        var destinationIndex = GetIndex(next);
        if (!IsNewtTransitTile(destinationIndex))
        {
            _newtMigrationPaths.Remove(critterId);
            return TryWanderNewt(critterIndex);
        }

        // Preserve the route and wait when another critter temporarily blocks it.
        if (_occupants[destinationIndex] >= 0)
        {
            return null;
        }

        path.Dequeue();
        MoveCritter(critterIndex, destinationIndex, next);
        if (path.Count == 0)
        {
            _newtMigrationPaths.Remove(critterId);
        }
        return null;
    }

    private bool TryApproachNearbyNewtFood(int critterIndex)
    {
        var current = _positions[critterIndex];
        GridPosition? target = null;
        var bestDistance = int.MaxValue;
        var ties = 0;
        for (var offsetY = -NewtFreshwaterPerceptionRadius;
            offsetY <= NewtFreshwaterPerceptionRadius;
            offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var horizontalReach = NewtFreshwaterPerceptionRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                var candidate = new GridPosition(Mod(current.X + offsetX, Width), y);
                if (!IsNewtFeedingTile(GetIndex(candidate)))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    target = candidate;
                    ties = 1;
                }
                else if (distance == bestDistance && NextInt(++ties) == 0)
                {
                    target = candidate;
                }
            }
        }

        if (target is null)
        {
            return false;
        }

        GridPosition? best = null;
        bestDistance = WrappedManhattanDistance(current, target.Value);
        var startDirection = NextInt(Directions.Length);
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

            var candidateIndex = GetIndex(candidate);
            if (_occupants[candidateIndex] >= 0 || !IsNewtTransitTile(candidateIndex) ||
                WrappedManhattanDistance(candidate, target.Value) >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = WrappedManhattanDistance(candidate, target.Value);
        }

        if (best is not null)
        {
            MoveCritter(critterIndex, GetIndex(best.Value), best.Value);
        }

        // Detecting food is enough to suppress random wandering when the best
        // step is temporarily blocked.
        return true;
    }

    private void PlanNewtFreshwaterMigration(int critterIndex)
    {
        var critterId = _critterIds[critterIndex].Value;
        _newtFreshwaterSearchCompleted.Add(critterId);
        var currentIndex = GetIndex(_positions[critterIndex]);
        if (IsNewtFeedingTile(currentIndex))
        {
            return;
        }

        EnsureNewtNavigationField();
        if (_newtNavigationNext is null || _newtNavigationNext[currentIndex] < 0)
        {
            return;
        }

        var path = new Queue<GridPosition>();
        var cursor = currentIndex;
        for (var step = 0; step < _terrain.Length && !IsNewtFeedingTile(cursor); step++)
        {
            var next = _newtNavigationNext[cursor];
            if (next < 0 || next == cursor)
            {
                break;
            }

            path.Enqueue(new GridPosition(next % Width, next / Width));
            cursor = next;
        }

        if (path.Count > 0 && IsNewtFeedingTile(cursor))
        {
            _newtMigrationPaths[critterId] = path;
        }
    }

    private void EnsureNewtNavigationField()
    {
        if (_newtNavigationNext is not null &&
            _newtNavigationRevision == _freshwaterNavigationRevision)
        {
            return;
        }

        var navigationNext = new int[_terrain.Length];
        Array.Fill(navigationNext, -1);
        var frontier = new Queue<int>();
        for (var index = 0; index < _terrain.Length; index++)
        {
            if (IsNewtFeedingTile(index) && IsNewtTransitTile(index))
            {
                navigationNext[index] = index;
                frontier.Enqueue(index);
            }
        }

        while (frontier.Count > 0)
        {
            var currentIndex = frontier.Dequeue();
            var current = new GridPosition(currentIndex % Width, currentIndex / Width);
            foreach (var direction in Directions)
            {
                var y = current.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                var neighborIndex = y * Width + Mod(current.X + direction.X, Width);
                if (navigationNext[neighborIndex] >= 0 || !IsNewtTransitTile(neighborIndex))
                {
                    continue;
                }

                navigationNext[neighborIndex] = currentIndex;
                frontier.Enqueue(neighborIndex);
            }
        }

        _newtNavigationNext = navigationNext;
        _newtNavigationRevision = _freshwaterNavigationRevision;
        NewtNavigationBuildCount++;
    }

    private GridPosition? TryWanderNewt(int critterIndex)
    {
        var current = _positions[critterIndex];
        var startDirection = NextInt(Directions.Length);
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
            if (_occupants[destinationIndex] < 0 && IsNewtOrdinaryTile(destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return null;
            }
        }

        return null;
    }

    private GridPosition? GetValidHunterTarget(
        int critterIndex,
        CritterSpecies predatorSpecies,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var targetIndex = _preyTargets[critterIndex];
        if (targetIndex < 0)
        {
            return null;
        }

        var occupant = _occupants[targetIndex];
        var target = new GridPosition(targetIndex % Width, targetIndex / Width);
        return occupant >= 0 &&
            CanEat(predatorSpecies, _species[occupant]) &&
            CanLiveOn(predatorSpecies, targetIndex) &&
            reservedPrey?.Contains(target) is not true
                ? target
                : null;
    }

    private GridPosition? FindHunterPrey(
        int critterIndex,
        CritterSpecies predatorSpecies,
        int perceptionRadius,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = _positions[critterIndex];
        GridPosition? selected = null;
        var bestDistance = int.MaxValue;
        var ties = 0;
        for (var offsetY = -perceptionRadius; offsetY <= perceptionRadius; offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var horizontalReach = perceptionRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(current.X + offsetX, Width), y);
                if (reservedPrey?.Contains(candidate) is true)
                {
                    continue;
                }
                var candidateIndex = GetIndex(candidate);
                var occupant = _occupants[candidateIndex];
                if (occupant < 0 || !CanEat(predatorSpecies, _species[occupant]) ||
                    !CanLiveOn(predatorSpecies, candidateIndex))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    selected = candidate;
                    ties = 1;
                }
                else if (distance == bestDistance && NextInt(++ties) == 0)
                {
                    selected = candidate;
                }
            }
        }

        return selected;
    }

    private void CommitPredation(GridPosition predatorPosition, GridPosition preyPosition)
    {
        var predatorIndex = _occupants[GetIndex(predatorPosition)];
        var preyIndex = _occupants[GetIndex(preyPosition)];
        if (predatorIndex < 0 || preyIndex < 0 ||
            !CanEat(_species[predatorIndex], _species[preyIndex]))
        {
            return;
        }

        var predatorSpecies = _species[predatorIndex];
        var preySpecies = _species[preyIndex];
        RemoveCritterAt(preyPosition);
        // Removing the prey may compact the predator to another array index,
        // so resolve it again through its still-occupied source tile.
        predatorIndex = _occupants[GetIndex(predatorPosition)];
        var destinationIndex = GetIndex(preyPosition);
        _occupants[GetIndex(predatorPosition)] = -1;
        _occupants[destinationIndex] = predatorIndex;
        _positions[predatorIndex] = preyPosition;
        _preyTargets[predatorIndex] = -1;
        var nutrition = CritterNutritions.Get(predatorSpecies);
        var foodEnergy = preySpecies is CritterSpecies.Worm ? 2 : 1;
        _energy[predatorIndex] = Math.Min(
            nutrition.MaximumEnergy,
            _energy[predatorIndex] + foodEnergy);
    }

    private static bool CanEat(CritterSpecies predator, CritterSpecies prey) => predator switch
    {
        CritterSpecies.Jellyfish or CritterSpecies.Fish =>
            prey is CritterSpecies.Plankton or CritterSpecies.Worm,
        CritterSpecies.MegaToad =>
            prey is CritterSpecies.Worm or CritterSpecies.Fish or CritterSpecies.Newt,
        _ => false,
    };

    private bool CanFeedFromEnvironment(CritterSpecies species, int tileIndex) => species switch
    {
        CritterSpecies.Worm => _terrain[tileIndex] is Terrain.Shallows,
        CritterSpecies.Newt => IsAtOrAdjacentToNewtFood(tileIndex),
        _ => true,
    };

    private int WrappedManhattanDistance(GridPosition first, GridPosition second)
    {
        var horizontal = Math.Abs(first.X - second.X);
        horizontal = Math.Min(horizontal, Width - horizontal);
        return horizontal + Math.Abs(first.Y - second.Y);
    }

    private void MoveCritter(int critterIndex, int destinationIndex, GridPosition destination)
    {
        _occupants[GetIndex(_positions[critterIndex])] = -1;
        _occupants[destinationIndex] = critterIndex;
        _positions[critterIndex] = destination;
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
        var removedId = _critterIds[critterIndex];
        _critterIndicesById.Remove(removedId.Value);
        _newtMigrationPaths.Remove(removedId.Value);
        _newtFreshwaterSearchCompleted.Remove(removedId.Value);
        _speciesCounts[(int)_species[critterIndex]]--;
        _occupants[tileIndex] = -1;
        if (critterIndex != lastIndex)
        {
            _critterIds[critterIndex] = _critterIds[lastIndex];
            _critterIndicesById[_critterIds[critterIndex].Value] = critterIndex;
            _species[critterIndex] = _species[lastIndex];
            _positions[critterIndex] = _positions[lastIndex];
            _nextMovementTicks[critterIndex] = _nextMovementTicks[lastIndex];
            _energy[critterIndex] = _energy[lastIndex];
            _nextMetabolismTicks[critterIndex] = _nextMetabolismTicks[lastIndex];
            _nextAmbientFeedingTicks[critterIndex] = _nextAmbientFeedingTicks[lastIndex];
            _preyTargets[critterIndex] = _preyTargets[lastIndex];
            _occupants[GetIndex(_positions[critterIndex])] = critterIndex;
        }

        _count--;
        return true;
    }

    private bool EnsurePlanktonPopulation()
    {
        if (GetCritterCount(CritterSpecies.Plankton) > 0)
        {
            return false;
        }

        GridPosition? selected = null;
        var candidateCount = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var position = new GridPosition(x, y);
                var index = y * Width + x;
                if (_terrain[index] is not Terrain.DeepOcean || _occupants[index] >= 0 ||
                    _surfaceCovers[index] is not SurfaceCover.None)
                {
                    continue;
                }

                candidateCount++;
                if (NextInt(candidateCount) == 0)
                {
                    selected = position;
                }
            }
        }

        return selected is not null && TryAddCritter(CritterSpecies.Plankton, selected.Value);
    }

    private int GetIndex(GridPosition position)
    {
        if (position.X < 0 || position.X >= Width || position.Y < 0 || position.Y >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return position.Y * Width + position.X;
    }

    private bool CanLiveOn(CritterSpecies species, int tileIndex) =>
        species is CritterSpecies.Newt
            ? IsNewtTransitTile(tileIndex)
            : species is CritterSpecies.MegaToad
                ? IsMegaToadTile(tileIndex)
                : CritterHabitats.CanOccupy(
                    CritterHabitats.GetHabitat(species),
                    _terrain[tileIndex],
                    _surfaceWater[tileIndex],
                    _biomes[tileIndex],
                    _surfaceCovers[tileIndex]);

    private bool IsMegaToadTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (_terrain[tileIndex] is Terrain.Shallows or Terrain.Beach or Terrain.Plains or
            Terrain.Hills or Terrain.Lowlands or Terrain.Canyon or Terrain.Trench ||
            IsFreshwaterMountain(tileIndex));

    private bool IsNewtTransitTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (_terrain[tileIndex] is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or
            Terrain.Beach or Terrain.Plains or Terrain.Hills or Terrain.Lowlands or
            Terrain.Canyon or Terrain.Trench || IsFreshwaterMountain(tileIndex));

    private bool IsNewtOrdinaryTile(int tileIndex) =>
        IsNewtTransitTile(tileIndex) &&
        (_terrain[tileIndex] is Terrain.Beach or Terrain.Plains or Terrain.Hills or
            Terrain.Lowlands or Terrain.Canyon or Terrain.Trench ||
            IsFreshwaterMountain(tileIndex));

    private bool IsFreshwaterMountain(int tileIndex) =>
        _terrain[tileIndex] is Terrain.Mountain &&
        _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake;

    private bool IsNewtFeedingTile(int tileIndex) =>
        _surfaceWater[tileIndex] is SurfaceWaterKind.River ||
        (_surfaceWater[tileIndex] is SurfaceWaterKind.FreshwaterLake &&
            _biomes[tileIndex] is not Biome.Arctic);

    private bool IsAtOrAdjacentToNewtFood(int tileIndex)
    {
        if (IsNewtFeedingTile(tileIndex))
        {
            return true;
        }

        var position = new GridPosition(tileIndex % Width, tileIndex / Width);
        foreach (var direction in Directions)
        {
            var y = position.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var neighborIndex = y * Width + Mod(position.X + direction.X, Width);
            if (IsNewtFeedingTile(neighborIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetMovementIntervalTicks(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => 5 * TicksPerSecond,
        CritterSpecies.Jellyfish => 5 * TicksPerSecond,
        CritterSpecies.Worm => 6 * TicksPerSecond,
        CritterSpecies.Fish => 3 * TicksPerSecond,
        CritterSpecies.Newt => 5 * TicksPerSecond,
        CritterSpecies.MegaToad => 6 * TicksPerSecond,
        CritterSpecies.Crab => 6,
        CritterSpecies.Ape => 6,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    private long GetFirstMovementTick(CritterSpecies species)
    {
        var interval = GetMovementIntervalTicks(species);
        // Hunter perception is bounded but costlier than wandering. Distribute
        // newly born/evolved hunters across their interval so cohorts do not scan
        // on the same tick.
        return species is CritterSpecies.Fish or CritterSpecies.MegaToad
            ? Tick + 1 + NextInt(interval)
            : Tick + interval;
    }

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
