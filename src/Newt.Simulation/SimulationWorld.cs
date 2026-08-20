namespace Newt.Simulation;

/// <summary>
/// Owns deterministic world state. It has no dependency on MonoGame, wall-clock
/// time, rendering, or input, so the same seed and commands produce the same run.
/// </summary>
public sealed class SimulationWorld
{
    private const int FishPerceptionRadius = 6;
    private const int FishPredatorFleeRadius = 5;
    private const int TrilobitePredatorFleeRadius = 3;
    private const int LandPreyFleeRadius = 5;
    private const int NautilusPerceptionRadius = 5;
    private const int SquidPerceptionRadius = 5;
    private const int SquidEggHatchRadius = 2;
    private const int SeaScorpionPerceptionRadius = 4;
    private const int MegaToadPerceptionRadius = 3;
    private const int TherapsidPerceptionRadius = 4;
    private const int ApePerceptionRadius = 6;
    private const int ApeVillageSearchRadius = 28;
    private const int ApeVillageClaimRadius = 28;
    private const int ApeVillageMinimumDistance = 12;
    private const int ApeVillageBasePopulationCapacity = 5;
    private const int ApeResidentialPopulationCapacity = 5;
    private const int ApeFoodDistrictPopulationThreshold = 5;
    private const int ApeResidentialFoodCost = 5;
    private const int ApeFarmFoodIntervalTicks = 14 * TicksPerSecond;
    private const int ApeSailorRecruitmentIntervalTicks = 30 * TicksPerSecond;
    private const int ApeSailorsPerHarbor = 4;
    private const int WolfPerceptionRadius = 6;
    private const int WolfDenSearchRadius = 8;
    private const int NewtFreshwaterPerceptionRadius = 8;
    private const int NewtMegaToadFleeRadius = 4;
    private const int MaximumPlanktonShoveChainLength = 4;
    public const int ReproductionTruceTicks = 30 * TicksPerSecond;
    public const int CombatDamageFlashTicks = TicksPerSecond / 2;
    public const int MaximumWolfDenCharges = 5;
    private const int MutualPredatorCombatDamage = 1;
    public const int TicksPerSecond = 20;
    public const float MinimumGroundElevation = -1f;
    public const float MaximumGroundElevation = 2f;
    public const float RingWorldWallElevation = 2.15f;
    public const float MinimumSeaLevel = -1f;
    public const float MaximumSeaLevel = 1f;
    public const float MinimumGlobalClimateOffset = -1f;
    public const float MaximumGlobalClimateOffset = 1f;

    private static readonly GridPosition[] CardinalDirections =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    private static readonly GridPosition[] MovementDirections =
    [
        .. CardinalDirections,
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    ];

    private readonly Terrain[] _terrain;
    private readonly float[] _elevation;
    private readonly float[] _temperature;
    private readonly float[] _moisture;
    private readonly Biome[] _biomes;
    private readonly SurfaceCover[] _surfaceCovers;
    private readonly long[] _surfaceCoverUntilTicks;
    private readonly byte[] _tileNutrition;
    private readonly byte[] _tileNutritionCapacities;
    private readonly long[] _tileNutritionLastTicks;
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
    private readonly Dictionary<int, int> _wolfDenHomes = [];
    private readonly Dictionary<int, int> _wolfDenTargets = [];
    private readonly Dictionary<int, int> _wolfDenCharges = [];
    private readonly Dictionary<int, ApeStructureKind> _apeStructures = [];
    private readonly Dictionary<int, int> _apeAuxiliaryVillages = [];
    private readonly Dictionary<int, int> _apeVillageHomes = [];
    private readonly Dictionary<int, int> _apeVillageTargets = [];
    private readonly Dictionary<int, int> _apeVillageFood = [];
    private readonly Dictionary<int, int> _apeCarriedFood = [];
    private readonly Dictionary<int, long> _apeStructureNextActionTicks = [];
    private readonly Dictionary<int, (CritterSpecies ParentSpecies, long UntilTick)>
        _reproductionTruces = [];
    private readonly CritterSpecies[] _species;
    private readonly GridPosition[] _positions;
    private readonly long[] _nextMovementTicks;
    private readonly int[] _energy;
    private readonly long[] _nextMetabolismTicks;
    private readonly long[] _nextAmbientFeedingTicks;
    private readonly int[] _preyTargets;
    private readonly long[] _damageFlashUntilTicks;
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
        _tileNutrition = new byte[_terrain.Length];
        _tileNutritionCapacities = new byte[_terrain.Length];
        _tileNutritionLastTicks = new long[_terrain.Length];
        Array.Fill(_tileNutritionLastTicks, -1);
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
        _damageFlashUntilTicks = new long[_terrain.Length];
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

    public int WolfDenCount => _wolfDenCharges.Count;

    public int ApeVillageCount => _apeStructures.Count(pair => pair.Value is ApeStructureKind.Village);

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

    public int? GetWolfDenCharges(GridPosition position) =>
        _wolfDenCharges.TryGetValue(GetIndex(position), out var charges) ? charges : null;

    public ApeStructureKind? GetApeStructure(GridPosition position) =>
        _apeStructures.TryGetValue(GetIndex(position), out var structure) ? structure : null;

    public int GetApeVillageResidentCount(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _apeVillageHomes.Count(pair => pair.Value == tileIndex);
    }

    public int GetApeVillagePopulationCapacity(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _apeStructures.TryGetValue(tileIndex, out var structure) &&
            structure is ApeStructureKind.Village
                ? GetApeVillagePopulationCapacityByTile(tileIndex)
                : 0;
    }

    public int GetApeVillageFood(GridPosition position) =>
        _apeVillageFood.TryGetValue(GetIndex(position), out var food) ? food : 0;

    public int GetApeCarriedFood(CritterId apeId) =>
        _apeCarriedFood.TryGetValue(apeId.Value, out var food) ? food : 0;

    public int GetTileNutrition(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        RefreshTileNutrition(tileIndex);
        return _tileNutrition[tileIndex];
    }

    public int GetTileNutritionCapacity(GridPosition position) =>
        CalculateTileNutritionCapacity(GetIndex(position));

    internal bool TryAssignApeToVillage(CritterId apeId, GridPosition villagePosition)
    {
        var villageTile = GetIndex(villagePosition);
        if (!_critterIndicesById.TryGetValue(apeId.Value, out var apeIndex) ||
            _species[apeIndex] is not (CritterSpecies.Ape or CritterSpecies.ApeSailor) ||
            !_apeStructures.TryGetValue(villageTile, out var structure) ||
            structure is not ApeStructureKind.Village ||
            GetApeVillageResidentCountByTile(villageTile) >=
                GetApeVillagePopulationCapacityByTile(villageTile))
        {
            return false;
        }

        _apeVillageHomes[apeId.Value] = villageTile;
        _apeVillageTargets.Remove(apeId.Value);
        return true;
    }

    internal int GetWolfDenAssociatedWolfCount(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _wolfDenHomes.Count(pair => pair.Value == tileIndex) +
            _wolfDenTargets.Count(pair => pair.Value == tileIndex);
    }

    public bool TryPlaceWolfDen(GridPosition position)
        => TryCreateWolfDen(position, initialCharges: 1);

    private bool TryCreateWolfDen(GridPosition position, int initialCharges)
    {
        if (!Contains(position))
        {
            return false;
        }
        var tileIndex = GetIndex(position);
        if (_wolfDenCharges.ContainsKey(tileIndex) || _apeStructures.ContainsKey(tileIndex) ||
            !CanLiveOn(CritterSpecies.Wolf, tileIndex))
        {
            return false;
        }

        _wolfDenCharges.Add(tileIndex, initialCharges);
        return true;
    }

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
            RemoveInvalidApeStructureAt(position);
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
        RemoveWolfDenAt(position);
        RemoveApeStructureAt(position);
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
        RemoveInvalidApeStructureAt(position);
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

        if (cover is SurfaceCover.Lava or SurfaceCover.Stone)
        {
            RemoveWolfDenAt(position);
            RemoveApeStructureAt(position);
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
            RemoveInvalidApeStructureAt(position);
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

    internal bool AddWolfDenCharge(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_wolfDenCharges.ContainsKey(tileIndex) &&
            !TryCreateWolfDen(position, initialCharges: 0))
        {
            return false;
        }

        _wolfDenCharges.TryGetValue(tileIndex, out var charges);
        if (charges >= MaximumWolfDenCharges)
        {
            return false;
        }
        _wolfDenCharges[tileIndex] = charges + 1;
        return true;
    }

    public bool RemoveWolfDenAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_wolfDenCharges.Remove(tileIndex))
        {
            return false;
        }

        foreach (var id in _wolfDenHomes.Where(pair => pair.Value == tileIndex).Select(pair => pair.Key).ToArray())
        {
            _wolfDenHomes.Remove(id);
        }
        foreach (var id in _wolfDenTargets.Where(pair => pair.Value == tileIndex).Select(pair => pair.Key).ToArray())
        {
            _wolfDenTargets.Remove(id);
        }
        return true;
    }

    private void RemoveWolfDenIfEmptyAndUnassociated(int tileIndex)
    {
        if (!_wolfDenCharges.TryGetValue(tileIndex, out var charges) || charges != 0 ||
            _wolfDenHomes.ContainsValue(tileIndex) || _wolfDenTargets.ContainsValue(tileIndex))
        {
            return;
        }

        _wolfDenCharges.Remove(tileIndex);
    }

    private void DetachWolfFromDens(int wolfId)
    {
        var affectedDens = new HashSet<int>();
        if (_wolfDenHomes.Remove(wolfId, out var homeTile))
        {
            affectedDens.Add(homeTile);
        }
        if (_wolfDenTargets.Remove(wolfId, out var targetTile))
        {
            affectedDens.Add(targetTile);
        }

        foreach (var denTile in affectedDens)
        {
            RemoveWolfDenIfEmptyAndUnassociated(denTile);
        }
    }

    public bool RemoveApeStructureAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_apeStructures.Remove(tileIndex, out var structure))
        {
            return false;
        }

        if (structure is not ApeStructureKind.Village)
        {
            _apeAuxiliaryVillages.Remove(tileIndex);
            _apeStructureNextActionTicks.Remove(tileIndex);
            return true;
        }

        _apeVillageFood.Remove(tileIndex);

        foreach (var auxiliaryTile in _apeAuxiliaryVillages
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _apeAuxiliaryVillages.Remove(auxiliaryTile);
            _apeStructures.Remove(auxiliaryTile);
            _apeStructureNextActionTicks.Remove(auxiliaryTile);
        }
        foreach (var id in _apeVillageHomes
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _apeVillageHomes.Remove(id);
        }
        foreach (var id in _apeVillageTargets
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _apeVillageTargets.Remove(id);
        }
        return true;
    }

    private void DetachApeFromVillage(int apeId)
    {
        _apeVillageHomes.Remove(apeId);
        _apeVillageTargets.Remove(apeId);
        _apeCarriedFood.Remove(apeId);
    }

    private void RemoveInvalidApeStructureAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_apeStructures.TryGetValue(tileIndex, out var structure))
        {
            return;
        }

        var valid = structure switch
        {
            ApeStructureKind.Village => CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.Farm => IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.NavalDistrict => IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.ResidentialDistrict =>
                IsValidApeAuxiliaryTile(tileIndex, structure),
            _ => false,
        };
        if (!valid)
        {
            RemoveApeStructureAt(position);
        }
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
        _damageFlashUntilTicks[index] = 0;
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

    /// <summary>
    /// Seeds every currently empty Deep Ocean tile with one Plankton.
    /// Occupied tiles are preserved so jump-starting life never replaces an
    /// existing critter.
    /// </summary>
    public int JumpStartPlankton()
    {
        var initialPlanktonCount = GetCritterCount(CritterSpecies.Plankton);
        if (!LifeEnabled)
        {
            LifeSystem.SetEnabled(this, true);
        }

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var position = new GridPosition(x, y);
                var tileIndex = GetIndex(position);
                if (_terrain[tileIndex] is Terrain.DeepOcean && _occupants[tileIndex] < 0)
                {
                    TryAddCritter(CritterSpecies.Plankton, position);
                }
            }
        }

        return GetCritterCount(CritterSpecies.Plankton) - initialPlanktonCount;
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
            nutrition.CanReproduce && _energy[index] >= nutrition.ReproductionThreshold,
            Tick < _damageFlashUntilTicks[index]);
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
        AdvanceApeVillages();
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
                CritterSpecies.Fish => TryMoveFish(index, reservedPrey),
                CritterSpecies.Nautilus => TryMoveNautilus(index, reservedPrey),
                CritterSpecies.Squid =>
                    TryMoveHunter(index, SquidPerceptionRadius, reservedPrey),
                CritterSpecies.SeaScorpion =>
                    TryMoveHunter(index, SeaScorpionPerceptionRadius, reservedPrey),
                CritterSpecies.MegaToad => TryMoveHunter(index, MegaToadPerceptionRadius, reservedPrey),
                CritterSpecies.Therapsid => TryMoveTherapsid(index, reservedPrey),
                CritterSpecies.Monkey => TryMoveMonkey(index, reservedPrey),
                CritterSpecies.Ape => TryMoveApe(index, reservedPrey),
                CritterSpecies.ApeSailor => TryMoveApeSailor(index, reservedPrey),
                CritterSpecies.Wolf => TryMoveWolf(index, reservedPrey),
                CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle =>
                    TryMoveGrazer(index),
                CritterSpecies.Newt => TryMoveNewt(index),
                CritterSpecies.Worm => TryMoveWorm(index),
                CritterSpecies.Trilobite => TryMoveTrilobite(index),
                CritterSpecies.Crab => TryMoveCrab(index),
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
            CommitEncounter(predation.Predator, predation.Prey);
        }
    }

    private void AdvanceCritterLifecycles()
    {
        List<GridPosition>? deaths = null;
        List<(
            CritterSpecies Species,
            GridPosition Position,
            CritterSpecies ParentSpecies,
            int ApeVillage)>? births = null;
        HashSet<GridPosition>? reservedBirthTiles = null;
        Dictionary<int, int>? reservedApeVillageBirths = null;

        // Births and deaths are committed after every existing critter has had
        // one lifecycle turn. A newborn therefore never acts on its birth tick.
        for (var index = 0; index < _count; index++)
        {
            var species = _species[index];
            if (species is CritterSpecies.SquidEgg && HasSquidEggHatchPreyNearby(index))
            {
                ChangeCritterSpecies(index, CritterSpecies.Squid, preserveEnergy: false);
                continue;
            }

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
                if (species is CritterSpecies.Plankton ||
                    TryConsumeEnvironmentalNutrition(species, GetIndex(_positions[index])))
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

            if (!IsValidReproductionSite(species, _positions[index]))
            {
                continue;
            }

            if (species is CritterSpecies.Wolf)
            {
                TryChargeWolfDen(index, nutrition);
                continue;
            }

            if (species is (CritterSpecies.Ape or CritterSpecies.ApeSailor) &&
                !TryPrepareApeReproduction(index, nutrition))
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

            var apeVillage = -1;
            if (species is CritterSpecies.Ape or CritterSpecies.ApeSailor)
            {
                _apeVillageHomes.TryGetValue(_critterIds[index].Value, out apeVillage);
                reservedApeVillageBirths ??= [];
                reservedApeVillageBirths.TryGetValue(apeVillage, out var reservedVillageBirths);
                if (GetApeVillageResidentCountByTile(apeVillage) + reservedVillageBirths >=
                    GetApeVillagePopulationCapacityByTile(apeVillage))
                {
                    continue;
                }
                reservedApeVillageBirths[apeVillage] = reservedVillageBirths + 1;
            }

            reservedBirthTiles.Add(birthPosition.Value);
            (births ??= []).Add((
                offspringSpecies,
                birthPosition.Value,
                species,
                apeVillage));
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
                var added = TryAddCritter(birth.Species, birth.Position);
                if (added)
                {
                    var offspringIndex = _occupants[GetIndex(birth.Position)];
                    var offspringId = _critterIds[offspringIndex].Value;
                    _reproductionTruces[offspringId] = (
                        birth.ParentSpecies,
                        Tick + ReproductionTruceTicks);
                }
                if (added && birth.Species is CritterSpecies.Ape && birth.ApeVillage >= 0)
                {
                    var offspringIndex = _occupants[GetIndex(birth.Position)];
                    if (offspringIndex >= 0 && _species[offspringIndex] is CritterSpecies.Ape)
                    {
                        _apeVillageHomes[_critterIds[offspringIndex].Value] = birth.ApeVillage;
                    }
                }
            }
        }
    }

    private void TryChargeWolfDen(int wolfIndex, CritterNutrition nutrition)
    {
        var wolfId = _critterIds[wolfIndex].Value;
        if (!_wolfDenHomes.TryGetValue(wolfId, out var denTile) ||
            !_wolfDenCharges.ContainsKey(denTile))
        {
            if (_wolfDenHomes.Remove(wolfId, out var formerHome))
            {
                RemoveWolfDenIfEmptyAndUnassociated(formerHome);
            }
            if (!_wolfDenTargets.TryGetValue(wolfId, out denTile) ||
                !IsValidWolfDenSite(wolfIndex, denTile))
            {
                if (_wolfDenTargets.Remove(wolfId, out var formerTarget))
                {
                    RemoveWolfDenIfEmptyAndUnassociated(formerTarget);
                }
                denTile = FindWolfDenSite(wolfIndex);
                _wolfDenTargets[wolfId] = denTile;
            }
        }

        if (GetIndex(_positions[wolfIndex]) != denTile)
        {
            return;
        }

        var denPosition = new GridPosition(denTile % Width, denTile / Width);
        if (!AddWolfDenCharge(denPosition))
        {
            if (!_wolfDenCharges.ContainsKey(denTile))
            {
                _wolfDenTargets.Remove(wolfId);
            }
            return;
        }

        _wolfDenHomes[wolfId] = denTile;
        if (_wolfDenTargets.Remove(wolfId, out var completedTarget))
        {
            RemoveWolfDenIfEmptyAndUnassociated(completedTarget);
        }
        _energy[wolfIndex] -= nutrition.ReproductionCost;
    }

    private bool TryPrepareApeReproduction(int apeIndex, CritterNutrition nutrition)
    {
        var apeId = _critterIds[apeIndex].Value;
        var villageTile = -1;
        var hasVillage = _apeVillageHomes.TryGetValue(apeId, out villageTile);
        if (hasVillage &&
            (!_apeStructures.TryGetValue(villageTile, out var structure) ||
                structure is not ApeStructureKind.Village))
        {
            _apeVillageHomes.Remove(apeId);
            villageTile = -1;
            hasVillage = false;
        }

        if (!hasVillage)
        {
            villageTile = FindClaimableApeVillage(apeIndex);
            if (villageTile >= 0)
            {
                _apeVillageHomes[apeId] = villageTile;
                _apeVillageTargets.Remove(apeId);
                hasVillage = true;
            }
        }

        if (hasVillage)
        {
            return IsAtOrAdjacentToConnectedApeStructure(apeIndex, villageTile) &&
                GetApeVillageResidentCountByTile(villageTile) <
                    GetApeVillagePopulationCapacityByTile(villageTile);
        }

        if (!_apeVillageTargets.TryGetValue(apeId, out villageTile) ||
            !TryGetApeVillageAuxiliarySite(apeIndex, villageTile, out _, out _))
        {
            villageTile = FindApeVillageSite(apeIndex);
            if (villageTile < 0)
            {
                _apeVillageTargets.Remove(apeId);
                return false;
            }
            _apeVillageTargets[apeId] = villageTile;
        }

        if (GetIndex(_positions[apeIndex]) != villageTile ||
            !TryGetApeVillageAuxiliarySite(apeIndex, villageTile, out _, out _))
        {
            return false;
        }

        _apeStructures[villageTile] = ApeStructureKind.Village;
        _apeVillageFood[villageTile] = 0;
        _apeVillageHomes[apeId] = villageTile;
        _apeVillageTargets.Remove(apeId);
        _energy[apeIndex] -= nutrition.ReproductionCost;
        return false;
    }

    private int FindClaimableApeVillage(int apeIndex)
    {
        var current = _positions[apeIndex];
        var bestTile = -1;
        var bestDistance = int.MaxValue;
        foreach (var pair in _apeStructures)
        {
            if (pair.Value is not ApeStructureKind.Village ||
                GetApeVillageResidentCountByTile(pair.Key) >=
                    GetApeVillagePopulationCapacityByTile(pair.Key))
            {
                continue;
            }

            var position = new GridPosition(pair.Key % Width, pair.Key / Width);
            var distance = WrappedManhattanDistance(current, position);
            if (distance <= ApeVillageClaimRadius && distance < bestDistance)
            {
                bestTile = pair.Key;
                bestDistance = distance;
            }
        }
        return bestTile;
    }

    private int FindApeVillageSite(int apeIndex)
    {
        var current = _positions[apeIndex];
        var bestFarmVillage = -1;
        var bestFarmDistance = int.MaxValue;
        var bestHarborVillage = -1;
        var bestHarborDistance = int.MaxValue;
        for (var offsetY = -ApeVillageSearchRadius; offsetY <= ApeVillageSearchRadius; offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var horizontalReach = ApeVillageSearchRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                var candidate = new GridPosition(Mod(current.X + offsetX, Width), y);
                var villageTile = GetIndex(candidate);
                if (!TryGetApeVillageAuxiliarySite(
                        apeIndex,
                        villageTile,
                        out _,
                        out var auxiliaryKind))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (auxiliaryKind is ApeStructureKind.Farm && distance < bestFarmDistance)
                {
                    bestFarmVillage = villageTile;
                    bestFarmDistance = distance;
                }
                else if (auxiliaryKind is ApeStructureKind.NavalDistrict && distance < bestHarborDistance)
                {
                    bestHarborVillage = villageTile;
                    bestHarborDistance = distance;
                }
            }
        }
        return bestFarmVillage >= 0 ? bestFarmVillage : bestHarborVillage;
    }

    private bool TryGetApeVillageAuxiliarySite(
        int apeIndex,
        int villageTile,
        out int auxiliaryTile,
        out ApeStructureKind auxiliaryKind)
    {
        auxiliaryTile = -1;
        auxiliaryKind = default;
        if (villageTile < 0 || villageTile >= _terrain.Length ||
            _apeStructures.ContainsKey(villageTile) || _wolfDenCharges.ContainsKey(villageTile) ||
            (_occupants[villageTile] >= 0 && _occupants[villageTile] != apeIndex) ||
            !CanLiveOn(CritterSpecies.Ape, villageTile) || !IsFarEnoughFromApeVillages(villageTile))
        {
            return false;
        }

        var village = new GridPosition(villageTile % Width, villageTile / Width);
        foreach (var desiredKind in new[] { ApeStructureKind.Farm, ApeStructureKind.NavalDistrict })
        {
            foreach (var direction in CardinalDirections)
            {
                var y = village.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(village.X + direction.X, Width), y);
                var candidateTile = GetIndex(candidate);
                if (_occupants[candidateTile] >= 0 || _apeStructures.ContainsKey(candidateTile) ||
                    _wolfDenCharges.ContainsKey(candidateTile) ||
                    !IsValidApeAuxiliaryTile(candidateTile, desiredKind))
                {
                    continue;
                }

                auxiliaryTile = candidateTile;
                auxiliaryKind = desiredKind;
                return true;
            }
        }
        return false;
    }

    private bool IsFarEnoughFromApeVillages(int candidateTile)
    {
        var candidate = new GridPosition(candidateTile % Width, candidateTile / Width);
        return !_apeStructures.Any(pair =>
            pair.Value is ApeStructureKind.Village &&
            WrappedManhattanDistance(
                candidate,
                new GridPosition(pair.Key % Width, pair.Key / Width)) <= ApeVillageMinimumDistance);
    }

    private bool IsValidApeAuxiliaryTile(int tileIndex, ApeStructureKind kind) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None && kind switch
        {
            ApeStructureKind.Farm =>
                _biomes[tileIndex] is Biome.Grassland && CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.NavalDistrict => _terrain[tileIndex] is Terrain.Beach,
            ApeStructureKind.ResidentialDistrict =>
                CanLiveOn(CritterSpecies.Ape, tileIndex) &&
                _terrain[tileIndex] is not Terrain.Shallows,
            _ => false,
        };

    private int GetApeVillageResidentCountByTile(int villageTile) =>
        _apeVillageHomes.Count(pair => pair.Value == villageTile);

    private int GetApeVillagePopulationCapacityByTile(int villageTile) =>
        ApeVillageBasePopulationCapacity +
        ApeResidentialPopulationCapacity * _apeAuxiliaryVillages.Count(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) &&
            structure is ApeStructureKind.ResidentialDistrict);

    private bool IsAtOrAdjacentToConnectedApeStructure(int apeIndex, int villageTile)
    {
        var position = _positions[apeIndex];
        if (WrappedManhattanDistance(
                position,
                new GridPosition(villageTile % Width, villageTile / Width)) <= 1)
        {
            return true;
        }

        return _apeAuxiliaryVillages.Any(pair =>
            pair.Value == villageTile &&
            WrappedManhattanDistance(
                position,
                new GridPosition(pair.Key % Width, pair.Key / Width)) <= 1);
    }

    private void AdvanceApeVillages()
    {
        foreach (var villageTile in _apeStructures
            .Where(pair => pair.Value is ApeStructureKind.Village)
            .Select(pair => pair.Key)
            .ToArray())
        {
            var population = GetApeVillageResidentCountByTile(villageTile);
            if (population >= ApeFoodDistrictPopulationThreshold &&
                !HasApeStructure(villageTile, ApeStructureKind.Farm) &&
                !HasApeStructure(villageTile, ApeStructureKind.NavalDistrict))
            {
                if (!TryBuildApeStructure(villageTile, ApeStructureKind.Farm))
                {
                    TryBuildApeStructure(villageTile, ApeStructureKind.NavalDistrict);
                }
            }

            foreach (var structureTile in _apeAuxiliaryVillages
                .Where(pair => pair.Value == villageTile)
                .Select(pair => pair.Key)
                .ToArray())
            {
                if (!_apeStructures.TryGetValue(structureTile, out var structure))
                {
                    continue;
                }
                if (structure is ApeStructureKind.Farm)
                {
                    AdvanceApeFarm(villageTile, structureTile);
                }
                else if (structure is ApeStructureKind.NavalDistrict)
                {
                    AdvanceApeHarborRecruitment(villageTile, structureTile);
                }
            }

            population = GetApeVillageResidentCountByTile(villageTile);
            _apeVillageFood.TryGetValue(villageTile, out var food);
            if (population >= GetApeVillagePopulationCapacityByTile(villageTile) &&
                food >= ApeResidentialFoodCost &&
                TryBuildApeStructure(villageTile, ApeStructureKind.ResidentialDistrict))
            {
                _apeVillageFood[villageTile] = food - ApeResidentialFoodCost;
            }
        }
    }

    private bool HasApeStructure(int villageTile, ApeStructureKind kind) =>
        _apeAuxiliaryVillages.Any(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) && structure == kind);

    private bool TryBuildApeStructure(int villageTile, ApeStructureKind kind)
    {
        var constructionTile = FindApeConstructionSite(villageTile, kind);
        if (constructionTile < 0)
        {
            return false;
        }

        _apeStructures[constructionTile] = kind;
        _apeAuxiliaryVillages[constructionTile] = villageTile;
        if (kind is ApeStructureKind.Farm)
        {
            _apeStructureNextActionTicks[constructionTile] = Tick + ApeFarmFoodIntervalTicks;
        }
        else if (kind is ApeStructureKind.NavalDistrict)
        {
            _apeStructureNextActionTicks[constructionTile] =
                Tick + ApeSailorRecruitmentIntervalTicks;
        }
        return true;
    }

    private int FindApeConstructionSite(int villageTile, ApeStructureKind kind)
    {
        var candidates = new HashSet<int>();
        foreach (var structureTile in EnumerateConnectedApeStructureTiles(villageTile).ToArray())
        {
            var structurePosition = new GridPosition(structureTile % Width, structureTile / Width);
            foreach (var direction in CardinalDirections)
            {
                var y = structurePosition.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(structurePosition.X + direction.X, Width), y);
                var candidateTile = GetIndex(candidate);
                if (_occupants[candidateTile] < 0 && !_apeStructures.ContainsKey(candidateTile) &&
                    !_wolfDenCharges.ContainsKey(candidateTile) &&
                    IsValidApeAuxiliaryTile(candidateTile, kind))
                {
                    candidates.Add(candidateTile);
                }
            }
        }

        return candidates
            .OrderBy(tile => kind is ApeStructureKind.ResidentialDistrict &&
                _biomes[tile] is Biome.Grassland)
            .ThenBy(tile => tile)
            .DefaultIfEmpty(-1)
            .First();
    }

    private void AdvanceApeFarm(int villageTile, int farmTile)
    {
        if (!_apeStructureNextActionTicks.TryGetValue(farmTile, out var nextTick))
        {
            nextTick = Tick + ApeFarmFoodIntervalTicks;
        }
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[farmTile] = nextTick;
            return;
        }

        do
        {
            _apeVillageFood.TryGetValue(villageTile, out var food);
            _apeVillageFood[villageTile] = food + 1;
            nextTick += ApeFarmFoodIntervalTicks;
        }
        while (nextTick <= Tick);
        _apeStructureNextActionTicks[farmTile] = nextTick;
    }

    private void AdvanceApeHarborRecruitment(int villageTile, int harborTile)
    {
        if (!_apeStructureNextActionTicks.TryGetValue(harborTile, out var nextTick))
        {
            nextTick = Tick + ApeSailorRecruitmentIntervalTicks;
        }
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[harborTile] = nextTick;
            return;
        }

        do
        {
            nextTick += ApeSailorRecruitmentIntervalTicks;
        }
        while (nextTick <= Tick);
        _apeStructureNextActionTicks[harborTile] = nextTick;
        TryRecruitApeSailor(villageTile, harborTile);
    }

    private bool TryRecruitApeSailor(int villageTile, int harborTile)
    {
        if (_occupants[harborTile] >= 0)
        {
            return false;
        }

        var harborCount = _apeAuxiliaryVillages.Count(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) &&
            structure is ApeStructureKind.NavalDistrict);
        var sailorCount = _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeSailor);
        if (sailorCount >= harborCount * ApeSailorsPerHarbor)
        {
            return false;
        }

        var harbor = new GridPosition(harborTile % Width, harborTile / Width);
        var civilians = _apeVillageHomes
            .Where(pair => pair.Value == villageTile &&
                _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
                _species[residentIndex] is CritterSpecies.Ape &&
                !_apeCarriedFood.ContainsKey(pair.Key))
            .Select(pair => pair.Key)
            .ToArray();
        if (civilians.Length <= 1)
        {
            return false;
        }

        var recruitId = civilians
            .OrderBy(id => WrappedManhattanDistance(
                _positions[_critterIndicesById[id]],
                harbor))
            .ThenBy(id => id)
            .First();
        var recruitIndex = _critterIndicesById[recruitId];
        ChangeCritterSpecies(recruitIndex, CritterSpecies.ApeSailor, preserveEnergy: true);
        MoveCritter(recruitIndex, harborTile, harbor);
        _apeVillageHomes[recruitId] = villageTile;
        return true;
    }

    private int FindWolfDenSite(int wolfIndex)
    {
        var current = _positions[wolfIndex];
        var fallback = GetIndex(current);
        var bestHill = -1;
        var bestHillDistance = int.MaxValue;
        var hillTies = 0;
        var bestExistingDen = -1;
        var bestExistingDistance = int.MaxValue;
        var existingTies = 0;
        for (var offsetY = -WolfDenSearchRadius; offsetY <= WolfDenSearchRadius; offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var horizontalReach = WolfDenSearchRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                var candidate = new GridPosition(Mod(current.X + offsetX, Width), y);
                var tileIndex = GetIndex(candidate);
                var occupant = _occupants[tileIndex];
                if ((occupant >= 0 && occupant != wolfIndex) ||
                    !CanLiveOn(CritterSpecies.Wolf, tileIndex))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (_wolfDenCharges.ContainsKey(tileIndex))
                {
                    if (distance < bestExistingDistance)
                    {
                        bestExistingDen = tileIndex;
                        bestExistingDistance = distance;
                        existingTies = 1;
                    }
                    else if (distance == bestExistingDistance && NextInt(++existingTies) == 0)
                    {
                        bestExistingDen = tileIndex;
                    }
                    continue;
                }

                if (_terrain[tileIndex] is Terrain.Hills && distance < bestHillDistance)
                {
                    bestHill = tileIndex;
                    bestHillDistance = distance;
                    hillTies = 1;
                }
                else if (_terrain[tileIndex] is Terrain.Hills &&
                    distance == bestHillDistance && NextInt(++hillTies) == 0)
                {
                    bestHill = tileIndex;
                }
            }
        }
        return bestExistingDen >= 0 ? bestExistingDen : bestHill >= 0 ? bestHill : fallback;
    }

    private bool IsValidWolfDenSite(int wolfIndex, int tileIndex) =>
        tileIndex >= 0 && tileIndex < _terrain.Length &&
        (_occupants[tileIndex] < 0 || _occupants[tileIndex] == wolfIndex) &&
        CanLiveOn(CritterSpecies.Wolf, tileIndex);

    internal CritterSpecies ChooseOffspringSpecies(CritterSpecies parentSpecies)
    {
        if (parentSpecies is CritterSpecies.Squid)
        {
            return CritterSpecies.SquidEgg;
        }
        if (parentSpecies is CritterSpecies.ApeSailor)
        {
            return CritterSpecies.Ape;
        }

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

        ChangeCritterSpecies(critterIndex, targetSpecies, preserveEnergy: true);
        return true;
    }

    private void ChangeCritterSpecies(
        int critterIndex,
        CritterSpecies targetSpecies,
        bool preserveEnergy)
    {
        var currentSpecies = _species[critterIndex];
        _speciesCounts[(int)currentSpecies]--;
        _speciesCounts[(int)targetSpecies]++;
        _species[critterIndex] = targetSpecies;
        var critterId = _critterIds[critterIndex].Value;
        _newtMigrationPaths.Remove(critterId);
        _newtFreshwaterSearchCompleted.Remove(critterId);
        DetachWolfFromDens(critterId);
        DetachApeFromVillage(critterId);
        var nutrition = CritterNutritions.Get(targetSpecies);
        _energy[critterIndex] = preserveEnergy && nutrition.MaximumEnergy > 0
            ? Math.Clamp(_energy[critterIndex], 1, nutrition.MaximumEnergy)
            : nutrition.InitialEnergy;
        _nextMovementTicks[critterIndex] = GetFirstMovementTick(targetSpecies);
        _nextMetabolismTicks[critterIndex] = nutrition.HasMetabolism
            ? Tick + nutrition.MetabolismIntervalTicks
            : long.MaxValue;
        _nextAmbientFeedingTicks[critterIndex] = nutrition.FeedsFromEnvironment
            ? Tick + nutrition.AmbientFeedingIntervalTicks
            : long.MaxValue;
        _preyTargets[critterIndex] = -1;
        _damageFlashUntilTicks[critterIndex] = 0;
    }

    private bool HasSquidEggHatchPreyNearby(int eggIndex)
    {
        var eggPosition = _positions[eggIndex];
        for (var offsetY = -SquidEggHatchRadius; offsetY <= SquidEggHatchRadius; offsetY++)
        {
            var y = eggPosition.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var horizontalRadius = SquidEggHatchRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalRadius; offsetX <= horizontalRadius; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var occupant = _occupants[y * Width + Mod(eggPosition.X + offsetX, Width)];
                if (occupant >= 0 && CanEat(CritterSpecies.Squid, _species[occupant]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private GridPosition? FindBirthPosition(
        int parentIndex,
        CritterSpecies offspringSpecies,
        IReadOnlySet<GridPosition> reservedBirthTiles)
    {
        var birthDirections = offspringSpecies is CritterSpecies.Fish
            ? MovementDirections
            : CardinalDirections;
        var startDirection = NextInt(birthDirections.Length);
        var parentPosition = _positions[parentIndex];
        for (var offset = 0; offset < birthDirections.Length; offset++)
        {
            var direction = birthDirections[(startDirection + offset) % birthDirections.Length];
            var candidate = new GridPosition(
                Mod(parentPosition.X + direction.X, Width),
                parentPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height || reservedBirthTiles.Contains(candidate))
            {
                continue;
            }

            var tileIndex = GetIndex(candidate);
            if (_occupants[tileIndex] < 0 && CanLiveOn(offspringSpecies, tileIndex) &&
                IsValidBirthSite(offspringSpecies, candidate))
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
        var startDirection = NextInt(MovementDirections.Length);
        var current = _positions[critterIndex];
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
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
                if (_species[critterIndex] is CritterSpecies.Fish &&
                    _species[preyIndex] is CritterSpecies.Worm &&
                    TryShoveWorm(critterIndex, destinationIndex, reservedPrey))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
                if (CanPursuePrey(critterIndex, preyIndex) &&
                    reservedPrey?.Contains(candidate) is not true)
                {
                    return candidate;
                }
                if (CanLiveOn(_species[critterIndex], destinationIndex) &&
                    TryShoveMovementBlocker(critterIndex, destinationIndex, reservedPrey))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
                continue;
            }

            if (!CanLiveOn(_species[critterIndex], destinationIndex))
            {
                continue;
            }

            MoveCritter(critterIndex, destinationIndex, candidate);
            return null;
        }

        return null;
    }

    private GridPosition? TryMoveWorm(int critterIndex)
    {
        var current = _positions[critterIndex];
        if (!CanFeedFromEnvironment(CritterSpecies.Worm, GetIndex(current)))
        {
            var startDirection = NextInt(MovementDirections.Length);
            for (var offset = 0; offset < MovementDirections.Length; offset++)
            {
                var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
                var candidate = new GridPosition(
                    Mod(current.X + direction.X, Width),
                    current.Y + direction.Y);
                if (candidate.Y < 0 || candidate.Y >= Height)
                {
                    continue;
                }

                var destinationIndex = GetIndex(candidate);
                if (CanFeedFromEnvironment(CritterSpecies.Worm, destinationIndex) &&
                    CanLiveOn(CritterSpecies.Worm, destinationIndex) &&
                    CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
            }
        }

        return TryMove(critterIndex);
    }

    private GridPosition? TryMoveTrilobite(int critterIndex)
    {
        if (TryFleePredators(critterIndex, TrilobitePredatorFleeRadius))
        {
            return null;
        }

        if (TryReturnToDeepSeaTerrain(critterIndex, CritterSpecies.Trilobite))
        {
            return null;
        }

        return TryMove(critterIndex);
    }

    private bool TryReturnToDeepSeaTerrain(int critterIndex, CritterSpecies species)
    {
        var current = _positions[critterIndex];
        if (IsTrilobiteFeedingTerrain(_terrain[GetIndex(current)]))
        {
            return false;
        }

        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var destinationIndex = GetIndex(candidate);
            if (IsTrilobiteFeedingTerrain(_terrain[destinationIndex]) &&
                CanLiveOn(species, destinationIndex) &&
                CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return true;
            }
        }

        return false;
    }

    private GridPosition? TryMoveHunter(
        int critterIndex,
        int perceptionRadius,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var predatorSpecies = _species[critterIndex];
        var target = predatorSpecies is CritterSpecies.MegaToad or CritterSpecies.Fish
            ? FindHunterPrey(critterIndex, predatorSpecies, perceptionRadius, reservedPrey)
            : GetValidHunterTarget(critterIndex, predatorSpecies, reservedPrey) ??
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
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
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
                if (candidate == target &&
                    (CanLiveOn(predatorSpecies, destinationIndex) ||
                        CanTherapsidStrikeAdjacentLakePrey(critterIndex, preyIndex)) &&
                    reservedPrey?.Contains(candidate) is not true)
                {
                    if (predatorSpecies is CritterSpecies.Fish &&
                        _species[preyIndex] is CritterSpecies.Worm &&
                        TryShoveWorm(critterIndex, destinationIndex, reservedPrey))
                    {
                        MoveCritter(critterIndex, destinationIndex, candidate);
                        return null;
                    }
                    return candidate;
                }
                if (CanLiveOn(predatorSpecies, destinationIndex) &&
                    TryShoveMovementBlocker(critterIndex, destinationIndex, reservedPrey))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
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
            if (predatorSpecies is CritterSpecies.MegaToad)
            {
                // Do not opportunistically swallow last-choice prey while a
                // preferred target is visible but temporarily blocked.
                return null;
            }
            return TryMove(critterIndex, reservedPrey);
        }

        MoveCritter(critterIndex, GetIndex(best.Value), best.Value);
        return null;
    }

    private GridPosition? TryMoveFish(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (TryFleePredators(critterIndex, FishPredatorFleeRadius))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Fish);
        if (_energy[critterIndex] < nutrition.ReproductionThreshold &&
            CanFeedFromEnvironment(CritterSpecies.Fish, GetIndex(_positions[critterIndex])))
        {
            return null;
        }

        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            TryApproachNearbyFeedingTile(
                critterIndex,
                CritterSpecies.Fish,
                FishPerceptionRadius,
                reservedPrey))
        {
            return null;
        }

        return TryMoveHunter(critterIndex, FishPerceptionRadius, reservedPrey);
    }

    private GridPosition? TryMoveTherapsid(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var nutrition = CritterNutritions.Get(CritterSpecies.Therapsid);
        if (_energy[critterIndex] < nutrition.ReproductionThreshold &&
            CanFeedFromEnvironment(CritterSpecies.Therapsid, GetIndex(_positions[critterIndex])))
        {
            return null;
        }

        if (_energy[critterIndex] < nutrition.ReproductionThreshold &&
            TryApproachNearbyFeedingTile(
                critterIndex,
                CritterSpecies.Therapsid,
                TherapsidPerceptionRadius,
                reservedPrey))
        {
            return null;
        }

        return TryMoveHunter(critterIndex, TherapsidPerceptionRadius, reservedPrey);
    }

    private GridPosition? TryMoveNautilus(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (TryFleePredators(critterIndex, TrilobitePredatorFleeRadius))
        {
            return null;
        }

        if (TryReturnToDeepSeaTerrain(critterIndex, CritterSpecies.Nautilus))
        {
            return null;
        }

        return TryMoveHunter(critterIndex, NautilusPerceptionRadius, reservedPrey);
    }

    private GridPosition? TryMoveCrab(int critterIndex)
    {
        var nutrition = CritterNutritions.Get(CritterSpecies.Crab);
        if (_energy[critterIndex] < nutrition.ReproductionThreshold &&
            CanFeedFromEnvironment(CritterSpecies.Crab, GetIndex(_positions[critterIndex])))
        {
            return null;
        }

        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            TryMoveToAdjacentFeedingTile(critterIndex, CritterSpecies.Crab))
        {
            return null;
        }

        return TryMove(critterIndex);
    }

    private bool TryMoveToAdjacentFeedingTile(int critterIndex, CritterSpecies species)
    {
        var current = _positions[critterIndex];
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var destinationIndex = GetIndex(candidate);
            if (CanFeedFromEnvironment(species, destinationIndex) &&
                CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return true;
            }
        }

        return false;
    }

    private bool TryApproachNearbyFeedingTile(
        int critterIndex,
        CritterSpecies species,
        int perceptionRadius,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = _positions[critterIndex];
        GridPosition? target = null;
        var bestTargetDistance = int.MaxValue;
        var targetTies = 0;
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
                var candidateIndex = GetIndex(candidate);
                if (_occupants[candidateIndex] >= 0 ||
                    !CanFeedFromEnvironment(species, candidateIndex))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance < bestTargetDistance)
                {
                    bestTargetDistance = distance;
                    target = candidate;
                    targetTies = 1;
                }
                else if (distance == bestTargetDistance && NextInt(++targetTies) == 0)
                {
                    target = candidate;
                }
            }
        }

        if (target is null)
        {
            return false;
        }

        var bestDistance = WrappedManhattanDistance(current, target.Value);
        GridPosition? best = null;
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                reservedPrey?.Contains(candidate) is true)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (!CanLiveOn(species, candidateIndex) ||
                (_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex, reservedPrey)))
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
            return false;
        }

        var destinationIndex = GetIndex(best.Value);
        if (_occupants[destinationIndex] >= 0 &&
            !TryShoveMovementBlocker(critterIndex, destinationIndex, reservedPrey))
        {
            return false;
        }

        MoveCritter(critterIndex, destinationIndex, best.Value);
        return true;
    }

    private GridPosition? TryMoveMonkey(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (TryFleePredators(critterIndex, LandPreyFleeRadius))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Monkey);
        if (_energy[critterIndex] < nutrition.ReproductionThreshold &&
            CanFeedFromEnvironment(CritterSpecies.Monkey, GetIndex(_positions[critterIndex])))
        {
            return null;
        }

        if (_energy[critterIndex] <= nutrition.HungryThreshold)
        {
            var current = _positions[critterIndex];
            var startDirection = NextInt(MovementDirections.Length);
            for (var offset = 0; offset < MovementDirections.Length; offset++)
            {
                var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
                var candidate = new GridPosition(
                    Mod(current.X + direction.X, Width),
                    current.Y + direction.Y);
                if (candidate.Y < 0 || candidate.Y >= Height)
                {
                    continue;
                }

                var destinationIndex = GetIndex(candidate);
                if (CanFeedFromEnvironment(CritterSpecies.Monkey, destinationIndex) &&
                    CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
            }
        }

        return TryMove(critterIndex, reservedPrey);
    }

    private GridPosition? TryMoveWolf(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var nutrition = CritterNutritions.Get(CritterSpecies.Wolf);
        if (_energy[critterIndex] >= nutrition.ReproductionThreshold)
        {
            var wolfId = _critterIds[critterIndex].Value;
            if ((_wolfDenHomes.TryGetValue(wolfId, out var denTile) ||
                    _wolfDenTargets.TryGetValue(wolfId, out denTile)) &&
                GetIndex(_positions[critterIndex]) != denTile)
            {
                return TryMoveTowardWolfDen(critterIndex, denTile, reservedPrey);
            }
        }

        return TryMoveHunter(critterIndex, WolfPerceptionRadius, reservedPrey);
    }

    private GridPosition? TryMoveApe(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (TryHandleApeSettlementReturn(critterIndex, reservedPrey, out var returnMovement))
        {
            return returnMovement;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Ape);
        if (_energy[critterIndex] >= nutrition.ReproductionThreshold)
        {
            var apeId = _critterIds[critterIndex].Value;
            if ((_apeVillageHomes.TryGetValue(apeId, out var villageTile) ||
                    _apeVillageTargets.TryGetValue(apeId, out villageTile)) &&
                (_apeVillageHomes.ContainsKey(apeId)
                    ? !IsAtOrAdjacentToConnectedApeStructure(critterIndex, villageTile)
                    : GetIndex(_positions[critterIndex]) != villageTile))
            {
                return TryMoveTowardApeStructure(
                    critterIndex,
                    FindNearestConnectedApeStructure(critterIndex, villageTile),
                    reservedPrey);
            }
        }

        return TryMoveHunter(critterIndex, ApePerceptionRadius, reservedPrey);
    }

    private GridPosition? TryMoveApeSailor(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (TryHandleApeSettlementReturn(critterIndex, reservedPrey, out var returnMovement))
        {
            return returnMovement;
        }

        return TryMoveHunter(critterIndex, ApePerceptionRadius, reservedPrey);
    }

    private bool TryHandleApeSettlementReturn(
        int critterIndex,
        IReadOnlySet<GridPosition>? reservedPrey,
        out GridPosition? movement)
    {
        movement = null;
        var apeId = _critterIds[critterIndex].Value;
        if (!_apeCarriedFood.TryGetValue(apeId, out var carriedFood) || carriedFood <= 0 ||
            !_apeVillageHomes.TryGetValue(apeId, out var villageTile))
        {
            return false;
        }

        if (IsAtOrAdjacentToConnectedApeStructure(critterIndex, villageTile))
        {
            _apeVillageFood.TryGetValue(villageTile, out var villageFood);
            _apeVillageFood[villageTile] = villageFood + carriedFood;
            _apeCarriedFood.Remove(apeId);
            return true;
        }

        var target = FindNearestConnectedApeStructure(critterIndex, villageTile);
        if (target < 0)
        {
            return false;
        }

        movement = TryMoveTowardApeStructure(critterIndex, target, reservedPrey);
        return true;
    }

    private int FindNearestConnectedApeStructure(int apeIndex, int villageTile)
    {
        var species = _species[apeIndex];
        var current = _positions[apeIndex];
        var bestTile = -1;
        var bestDistance = int.MaxValue;
        foreach (var tile in EnumerateConnectedApeStructureTiles(villageTile))
        {
            if (species is CritterSpecies.ApeSailor &&
                (!_apeStructures.TryGetValue(tile, out var structure) ||
                    structure is not ApeStructureKind.NavalDistrict))
            {
                continue;
            }
            var position = new GridPosition(tile % Width, tile / Width);
            var distance = WrappedManhattanDistance(current, position);
            if (distance < bestDistance)
            {
                bestTile = tile;
                bestDistance = distance;
            }
        }
        return bestTile;
    }

    private IEnumerable<int> EnumerateConnectedApeStructureTiles(int villageTile)
    {
        yield return villageTile;
        foreach (var pair in _apeAuxiliaryVillages)
        {
            if (pair.Value == villageTile)
            {
                yield return pair.Key;
            }
        }
    }

    private GridPosition? TryMoveTowardApeStructure(
        int critterIndex,
        int structureTile,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = _positions[critterIndex];
        var target = new GridPosition(structureTile % Width, structureTile / Width);
        var bestDistance = WrappedManhattanDistance(current, target);
        GridPosition? best = null;
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(Mod(current.X + direction.X, Width), current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }
            var candidateIndex = GetIndex(candidate);
            if (!CanLiveOn(_species[critterIndex], candidateIndex) ||
                (_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex, reservedPrey)))
            {
                continue;
            }
            var distance = WrappedManhattanDistance(candidate, target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best is not null)
        {
            MoveCritter(critterIndex, GetIndex(best.Value), best.Value);
        }
        return null;
    }

    private GridPosition? TryMoveTowardWolfDen(
        int critterIndex,
        int denTile,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = _positions[critterIndex];
        var target = new GridPosition(denTile % Width, denTile / Width);
        var bestDistance = WrappedManhattanDistance(current, target);
        GridPosition? best = null;
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(Mod(current.X + direction.X, Width), current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }
            var candidateIndex = GetIndex(candidate);
            if (!CanLiveOn(CritterSpecies.Wolf, candidateIndex) ||
                (_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex, reservedPrey)))
            {
                continue;
            }
            var distance = WrappedManhattanDistance(candidate, target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best is not null)
        {
            MoveCritter(critterIndex, GetIndex(best.Value), best.Value);
        }
        return null;
    }

    private GridPosition? TryMoveGrazer(int critterIndex)
    {
        if (TryFleePredators(critterIndex, LandPreyFleeRadius))
        {
            return null;
        }

        var species = _species[critterIndex];
        var currentIndex = GetIndex(_positions[critterIndex]);
        var nutrition = CritterNutritions.Get(species);
        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            CanFeedFromEnvironment(species, currentIndex))
        {
            return null;
        }

        if (_energy[critterIndex] <= nutrition.HungryThreshold)
        {
            var current = _positions[critterIndex];
            var startDirection = NextInt(MovementDirections.Length);
            for (var offset = 0; offset < MovementDirections.Length; offset++)
            {
                var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
                var candidate = new GridPosition(
                    Mod(current.X + direction.X, Width),
                    current.Y + direction.Y);
                if (candidate.Y < 0 || candidate.Y >= Height)
                {
                    continue;
                }

                var destinationIndex = GetIndex(candidate);
                if (CanFeedFromEnvironment(species, destinationIndex) &&
                    CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
                {
                    MoveCritter(critterIndex, destinationIndex, candidate);
                    return null;
                }
            }
        }

        return TryMove(critterIndex);
    }

    private bool TryFleePredators(int critterIndex, int fleeRadius)
    {
        var current = _positions[critterIndex];
        Span<GridPosition> threats = stackalloc GridPosition[
            2 * fleeRadius * (fleeRadius + 1)];
        var threatCount = 0;
        for (var offsetY = -fleeRadius;
            offsetY <= fleeRadius;
            offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var horizontalReach = fleeRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var position = new GridPosition(Mod(current.X + offsetX, Width), y);
                var occupant = _occupants[GetIndex(position)];
                if (occupant >= 0 && CanEat(_species[occupant], _species[critterIndex]))
                {
                    threats[threatCount++] = position;
                }
            }
        }

        if (threatCount == 0)
        {
            return false;
        }

        var currentScore = GetFleeDistance(current, threats[..threatCount]);
        var bestScore = currentScore;
        GridPosition? best = null;
        var ties = 0;
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (!CanLiveOn(_species[critterIndex], candidateIndex) ||
                (_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex)))
            {
                continue;
            }

            var score = GetFleeDistance(candidate, threats[..threatCount]);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                ties = 1;
            }
            else if (score == bestScore && score > currentScore && NextInt(++ties) == 0)
            {
                best = candidate;
            }
        }

        if (best is not null)
        {
            var destinationIndex = GetIndex(best.Value);
            if (CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, best.Value);
            }
        }

        // A detected predator suppresses hunting even when escape is blocked.
        return true;
    }

    private GridPosition? TryMoveNewt(int critterIndex)
    {
        if (TryFleeMegaToads(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Newt);
        if (_energy[critterIndex] <= nutrition.HungryThreshold &&
            CanFeedFromEnvironment(CritterSpecies.Newt, GetIndex(_positions[critterIndex])))
        {
            // A hungry Newt that has reached food waits for its next feeding
            // interval instead of wandering out of feeding range.
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
        if (_occupants[destinationIndex] >= 0 &&
            !TryShoveMovementBlocker(critterIndex, destinationIndex))
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

    private bool TryFleeMegaToads(int critterIndex)
    {
        var current = _positions[critterIndex];
        Span<GridPosition> threats = stackalloc GridPosition[40];
        var threatCount = 0;
        for (var offsetY = -NewtMegaToadFleeRadius;
            offsetY <= NewtMegaToadFleeRadius;
            offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var horizontalReach = NewtMegaToadFleeRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var position = new GridPosition(Mod(current.X + offsetX, Width), y);
                var occupant = _occupants[GetIndex(position)];
                if (occupant >= 0 && _species[occupant] is CritterSpecies.MegaToad)
                {
                    threats[threatCount++] = position;
                }
            }
        }

        if (threatCount == 0)
        {
            return false;
        }

        var currentScore = GetFleeDistance(current, threats[..threatCount]);
        var bestScore = currentScore;
        GridPosition? best = null;
        var ties = 0;
        var ordinaryHabitat = IsNewtOrdinaryTile(GetIndex(current));
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if ((_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex)) ||
                (ordinaryHabitat
                    ? !IsNewtOrdinaryTile(candidateIndex)
                    : !IsNewtTransitTile(candidateIndex)))
            {
                continue;
            }

            var score = GetFleeDistance(candidate, threats[..threatCount]);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                ties = 1;
            }
            else if (score == bestScore && score > currentScore && NextInt(++ties) == 0)
            {
                best = candidate;
            }
        }

        if (best is not null)
        {
            var destinationIndex = GetIndex(best.Value);
            if (CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, best.Value);
            }
        }

        // Detecting a toad suppresses feeding waits and ordinary wandering even
        // if every safer step is temporarily blocked.
        return true;
    }

    private int GetFleeDistance(GridPosition position, ReadOnlySpan<GridPosition> threats)
    {
        var nearest = int.MaxValue;
        foreach (var threat in threats)
        {
            nearest = Math.Min(nearest, WrappedManhattanDistance(position, threat));
        }
        return nearest;
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
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if ((_occupants[candidateIndex] >= 0 &&
                    !CanShoveMovementBlocker(critterIndex, candidateIndex)) ||
                !IsNewtTransitTile(candidateIndex) ||
                WrappedManhattanDistance(candidate, target.Value) >= bestDistance)
            {
                continue;
            }

            best = candidate;
            bestDistance = WrappedManhattanDistance(candidate, target.Value);
        }

        if (best is not null)
        {
            var destinationIndex = GetIndex(best.Value);
            if (CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, best.Value);
            }
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
            foreach (var direction in MovementDirections)
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
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }

            var destinationIndex = GetIndex(candidate);
            if (IsNewtOrdinaryTile(destinationIndex) &&
                CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
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
            CanPursuePrey(critterIndex, occupant) &&
            (CanLiveOn(predatorSpecies, targetIndex) ||
                CanTherapsidStrikeAdjacentLakePrey(critterIndex, occupant)) &&
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
        GridPosition? selectedNewt = null;
        var bestNewtDistance = int.MaxValue;
        var newtTies = 0;
        GridPosition? selectedToad = null;
        var bestToadDistance = int.MaxValue;
        var toadTies = 0;
        GridPosition? selectedTherapsid = null;
        var bestTherapsidDistance = int.MaxValue;
        var therapsidTies = 0;
        GridPosition? selectedWolfFallback = null;
        var bestWolfFallbackDistance = int.MaxValue;
        var wolfFallbackTies = 0;
        GridPosition? selectedFishWormFallback = null;
        var bestFishWormFallbackDistance = int.MaxValue;
        var fishWormFallbackTies = 0;
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
                if (occupant < 0 || occupant == critterIndex ||
                    !CanPursuePrey(critterIndex, occupant) ||
                    (!CanLiveOn(predatorSpecies, candidateIndex) &&
                        !CanTherapsidStrikeAdjacentLakePrey(critterIndex, occupant)))
                {
                    continue;
                }

                var preySpecies = _species[occupant];
                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (predatorSpecies is CritterSpecies.Fish &&
                    preySpecies is (CritterSpecies.Worm or CritterSpecies.Crab))
                {
                    if (distance < bestFishWormFallbackDistance)
                    {
                        bestFishWormFallbackDistance = distance;
                        selectedFishWormFallback = candidate;
                        fishWormFallbackTies = 1;
                    }
                    else if (distance == bestFishWormFallbackDistance &&
                        NextInt(++fishWormFallbackTies) == 0)
                    {
                        selectedFishWormFallback = candidate;
                    }
                    continue;
                }

                if (predatorSpecies is CritterSpecies.MegaToad &&
                    preySpecies is CritterSpecies.MegaToad)
                {
                    if (distance < bestToadDistance)
                    {
                        bestToadDistance = distance;
                        selectedToad = candidate;
                        toadTies = 1;
                    }
                    else if (distance == bestToadDistance && NextInt(++toadTies) == 0)
                    {
                        selectedToad = candidate;
                    }
                    continue;
                }

                if (predatorSpecies is CritterSpecies.MegaToad &&
                    preySpecies is CritterSpecies.Newt)
                {
                    if (distance < bestNewtDistance)
                    {
                        bestNewtDistance = distance;
                        selectedNewt = candidate;
                        newtTies = 1;
                    }
                    else if (distance == bestNewtDistance && NextInt(++newtTies) == 0)
                    {
                        selectedNewt = candidate;
                    }
                    continue;
                }

                if (predatorSpecies is CritterSpecies.MegaToad &&
                    preySpecies is CritterSpecies.Therapsid)
                {
                    if (distance < bestTherapsidDistance)
                    {
                        bestTherapsidDistance = distance;
                        selectedTherapsid = candidate;
                        therapsidTies = 1;
                    }
                    else if (distance == bestTherapsidDistance && NextInt(++therapsidTies) == 0)
                    {
                        selectedTherapsid = candidate;
                    }
                    continue;
                }

                if (predatorSpecies is CritterSpecies.Wolf &&
                    preySpecies is CritterSpecies.Therapsid)
                {
                    if (distance < bestWolfFallbackDistance)
                    {
                        bestWolfFallbackDistance = distance;
                        selectedWolfFallback = candidate;
                        wolfFallbackTies = 1;
                    }
                    else if (distance == bestWolfFallbackDistance &&
                        NextInt(++wolfFallbackTies) == 0)
                    {
                        selectedWolfFallback = candidate;
                    }
                    continue;
                }

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

        if (predatorSpecies is CritterSpecies.Fish)
        {
            return selected ?? selectedFishWormFallback;
        }

        if (predatorSpecies is not CritterSpecies.MegaToad)
        {
            return predatorSpecies is CritterSpecies.Wolf
                ? selected ?? selectedWolfFallback
                : selected;
        }

        var selectedNonToad = selected ?? selectedNewt ?? selectedTherapsid;
        return (selectedToad, selectedNonToad) switch
        {
            (not null, not null) => NextInt(2) == 0 ? selectedToad : selectedNonToad,
            (not null, null) => selectedToad,
            _ => selectedNonToad,
        };
    }

    private void CommitEncounter(GridPosition predatorPosition, GridPosition preyPosition)
    {
        var predatorIndex = _occupants[GetIndex(predatorPosition)];
        var preyIndex = _occupants[GetIndex(preyPosition)];
        if (predatorIndex < 0 || preyIndex < 0)
        {
            return;
        }
        if (!CanPursuePrey(predatorIndex, preyIndex))
        {
            return;
        }
        if (CanTherapsidStrikeAdjacentLakePrey(predatorIndex, preyIndex))
        {
            var shorelinePreySpecies = _species[preyIndex];
            RemoveCritterAt(preyPosition);
            FeedPredatorAt(predatorPosition, shorelinePreySpecies);
            return;
        }

        var predatorSpecies = _species[predatorIndex];
        var preySpecies = _species[preyIndex];
        if (predatorSpecies != preySpecies &&
            CanEat(predatorSpecies, preySpecies) &&
            (CanEat(preySpecies, predatorSpecies) ||
                preySpecies is CritterSpecies.Therapsid &&
                    predatorSpecies is (CritterSpecies.MegaToad or CritterSpecies.Wolf) ||
                predatorSpecies is CritterSpecies.MegaToad &&
                preySpecies is CritterSpecies.Wolf))
        {
            CommitMutualPredatorCombat(predatorPosition, preyPosition);
            return;
        }

        CommitPredation(predatorPosition, preyPosition);
    }

    private void CommitMutualPredatorCombat(
        GridPosition attackerPosition,
        GridPosition defenderPosition)
    {
        var attackerIndex = _occupants[GetIndex(attackerPosition)];
        var defenderIndex = _occupants[GetIndex(defenderPosition)];
        if (attackerIndex < 0 || defenderIndex < 0)
        {
            return;
        }

        var attackerSpecies = _species[attackerIndex];
        var defenderSpecies = _species[defenderIndex];
        if (NextInt(2) == 0)
        {
            _energy[attackerIndex] = Math.Max(
                0,
                _energy[attackerIndex] - MutualPredatorCombatDamage);
            _damageFlashUntilTicks[attackerIndex] = Tick + CombatDamageFlashTicks;
            if (_energy[attackerIndex] == 0)
            {
                RemoveCritterAt(attackerPosition);
                FeedPredatorAt(defenderPosition, attackerSpecies);
            }
            return;
        }

        _energy[defenderIndex] = Math.Max(
            0,
            _energy[defenderIndex] - MutualPredatorCombatDamage);
        _damageFlashUntilTicks[defenderIndex] = Tick + CombatDamageFlashTicks;
        if (_energy[defenderIndex] == 0)
        {
            CommitPredation(attackerPosition, defenderPosition);
        }
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
        MoveCritter(predatorIndex, destinationIndex, preyPosition);
        _preyTargets[predatorIndex] = -1;
        FeedPredatorAt(preyPosition, preySpecies);
    }

    private void FeedPredatorAt(GridPosition predatorPosition, CritterSpecies preySpecies)
    {
        var predatorIndex = _occupants[GetIndex(predatorPosition)];
        if (predatorIndex < 0)
        {
            return;
        }

        var predatorSpecies = _species[predatorIndex];
        var nutrition = CritterNutritions.Get(predatorSpecies);
        var preyNutrition = CritterNutritions.Get(preySpecies);
        var foodEnergy = Math.Max(1, preyNutrition.MaximumEnergy / 2);
        _energy[predatorIndex] = Math.Min(
            nutrition.MaximumEnergy,
            _energy[predatorIndex] + foodEnergy);
        if (predatorSpecies is CritterSpecies.Ape or CritterSpecies.ApeSailor)
        {
            var apeId = _critterIds[predatorIndex].Value;
            if (_apeVillageHomes.ContainsKey(apeId))
            {
                _apeCarriedFood.TryGetValue(apeId, out var carriedFood);
                _apeCarriedFood[apeId] = carriedFood + foodEnergy;
            }
        }
    }

    internal static bool CanEat(CritterSpecies predator, CritterSpecies prey) => predator switch
    {
        CritterSpecies.Jellyfish => prey is CritterSpecies.Plankton,
        CritterSpecies.Fish =>
            prey is CritterSpecies.Plankton or CritterSpecies.Worm or CritterSpecies.Crab,
        CritterSpecies.SeaScorpion =>
            prey is CritterSpecies.Fish or CritterSpecies.Worm or CritterSpecies.Trilobite or
                CritterSpecies.Newt or CritterSpecies.Crab or CritterSpecies.Squid or
                CritterSpecies.ApeSailor or CritterSpecies.Deer or CritterSpecies.Elk or
                CritterSpecies.Gazelle,
        CritterSpecies.Nautilus => prey is CritterSpecies.Plankton,
        CritterSpecies.Squid =>
            prey is CritterSpecies.Fish or CritterSpecies.Trilobite or CritterSpecies.Crab or
                CritterSpecies.Newt or CritterSpecies.Nautilus or CritterSpecies.SeaScorpion or
                CritterSpecies.ApeSailor or
                CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle,
        CritterSpecies.MegaToad => IsLargeLandPredatorPrey(prey),
        CritterSpecies.Therapsid =>
            prey is CritterSpecies.Worm or CritterSpecies.Fish or CritterSpecies.Newt,
        CritterSpecies.Monkey => false,
        CritterSpecies.Ape => prey is not (CritterSpecies.Ape or CritterSpecies.ApeSailor),
        CritterSpecies.ApeSailor =>
            prey is CritterSpecies.Jellyfish or CritterSpecies.Worm or CritterSpecies.Trilobite or
                CritterSpecies.SeaScorpion or CritterSpecies.Nautilus or CritterSpecies.Fish or
                CritterSpecies.Crab or CritterSpecies.Squid or CritterSpecies.SquidEgg,
        CritterSpecies.Wolf =>
            prey is not (CritterSpecies.Wolf or CritterSpecies.MegaToad) &&
            IsLargeLandPredatorPrey(prey),
        _ => false,
    };

    private static bool IsLargeLandPredatorPrey(CritterSpecies prey) =>
        prey is CritterSpecies.Worm or CritterSpecies.Trilobite or CritterSpecies.Nautilus or
            CritterSpecies.Fish or CritterSpecies.Newt or CritterSpecies.Crab or
            CritterSpecies.MegaToad or CritterSpecies.Therapsid or CritterSpecies.Monkey or
            CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle or
            CritterSpecies.Wolf or CritterSpecies.Ape or CritterSpecies.ApeSailor;

    private bool CanPursuePrey(int predatorIndex, int preyIndex)
    {
        if (!CanEat(_species[predatorIndex], _species[preyIndex]))
        {
            return false;
        }

        if (HasActiveReproductionTruceWith(predatorIndex, _species[preyIndex]) ||
            HasActiveReproductionTruceWith(preyIndex, _species[predatorIndex]))
        {
            return false;
        }

        if (_species[predatorIndex] is CritterSpecies.Fish &&
            _species[preyIndex] is (CritterSpecies.Worm or CritterSpecies.Crab) &&
            HasNearbyAvailableFishForagingTile(predatorIndex))
        {
            return false;
        }
        return true;
    }

    private bool HasActiveReproductionTruceWith(int childIndex, CritterSpecies otherSpecies)
    {
        var childId = _critterIds[childIndex].Value;
        if (!_reproductionTruces.TryGetValue(childId, out var truce))
        {
            return false;
        }
        if (Tick >= truce.UntilTick)
        {
            _reproductionTruces.Remove(childId);
            return false;
        }
        return truce.ParentSpecies == otherSpecies;
    }

    private bool HasNearbyAvailableFishForagingTile(int fishIndex)
    {
        var current = _positions[fishIndex];
        for (var offsetY = -FishPerceptionRadius; offsetY <= FishPerceptionRadius; offsetY++)
        {
            var y = current.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var horizontalReach = FishPerceptionRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                var tileIndex = y * Width + Mod(current.X + offsetX, Width);
                if (CanFeedFromEnvironment(CritterSpecies.Fish, tileIndex) &&
                    (_occupants[tileIndex] < 0 || _occupants[tileIndex] == fishIndex))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CanTherapsidStrikeAdjacentLakePrey(int predatorIndex, int preyIndex)
    {
        if (_species[predatorIndex] is not CritterSpecies.Therapsid ||
            _species[preyIndex] is not (
                CritterSpecies.Worm or CritterSpecies.Fish or CritterSpecies.Newt) ||
            _surfaceWater[GetIndex(_positions[preyIndex])] is not SurfaceWaterKind.FreshwaterLake)
        {
            return false;
        }

        var predator = _positions[predatorIndex];
        var prey = _positions[preyIndex];
        var horizontal = Math.Abs(predator.X - prey.X);
        horizontal = Math.Min(horizontal, Width - horizontal);
        return Math.Max(horizontal, Math.Abs(predator.Y - prey.Y)) == 1;
    }

    private bool CanFeedFromEnvironment(CritterSpecies species, int tileIndex)
        => FindEnvironmentalFoodTile(species, tileIndex) >= 0;

    private bool TryConsumeEnvironmentalNutrition(CritterSpecies species, int tileIndex)
    {
        var foodTile = FindEnvironmentalFoodTile(species, tileIndex);
        if (foodTile < 0)
        {
            return false;
        }

        if (_tileNutrition[foodTile] == _tileNutritionCapacities[foodTile])
        {
            _tileNutritionLastTicks[foodTile] = Tick;
        }
        _tileNutrition[foodTile]--;
        return true;
    }

    private int FindEnvironmentalFoodTile(CritterSpecies species, int tileIndex)
    {
        if (IsEnvironmentalFoodSourceForSpecies(species, tileIndex) &&
            HasAvailableTileNutrition(tileIndex))
        {
            return tileIndex;
        }

        if (species is not CritterSpecies.Newt)
        {
            return -1;
        }

        var position = new GridPosition(tileIndex % Width, tileIndex / Width);
        foreach (var direction in CardinalDirections)
        {
            var y = position.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var candidateTile = y * Width + Mod(position.X + direction.X, Width);
            if (IsNewtFeedingTile(candidateTile) && HasAvailableTileNutrition(candidateTile))
            {
                return candidateTile;
            }
        }
        return -1;
    }

    private bool HasAvailableTileNutrition(int tileIndex)
    {
        RefreshTileNutrition(tileIndex);
        return _tileNutrition[tileIndex] > 0;
    }

    private bool IsEnvironmentalFoodSourceForSpecies(CritterSpecies species, int tileIndex) =>
        species switch
    {
        CritterSpecies.Worm => IsWormFeedingTile(tileIndex),
        CritterSpecies.Trilobite => IsTrilobiteFeedingTerrain(_terrain[tileIndex]),
        CritterSpecies.Nautilus => IsTrilobiteFeedingTerrain(_terrain[tileIndex]),
        CritterSpecies.Crab => IsCrabFeedingTile(tileIndex),
        CritterSpecies.Fish => IsFishForagingTile(tileIndex),
        CritterSpecies.Therapsid =>
            (_biomes[tileIndex] is Biome.Swamp or Biome.Jungle) &&
            CanLiveOn(CritterSpecies.Therapsid, tileIndex),
        CritterSpecies.Newt =>
            IsNewtFeedingTile(tileIndex) || IsNewtFoliageTile(tileIndex),
        CritterSpecies.Monkey => IsMonkeyFoliageTile(tileIndex),
        CritterSpecies.Deer => IsGrazerFoliageTile(CritterSpecies.Deer, tileIndex),
        CritterSpecies.Elk => IsGrazerFoliageTile(CritterSpecies.Elk, tileIndex),
        CritterSpecies.Gazelle => IsGrazerFoliageTile(CritterSpecies.Gazelle, tileIndex),
        _ => true,
    };

    private int CalculateTileNutritionCapacity(int tileIndex)
    {
        if (_surfaceCovers[tileIndex] is not SurfaceCover.None)
        {
            return 0;
        }

        var capacity = _surfaceWater[tileIndex] is
            (SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake)
                ? 2
                : _terrain[tileIndex] switch
                {
                    Terrain.Shallows => ClimateSystem.ClassifyTemperature(_temperature[tileIndex]) switch
                    {
                        TemperatureBand.Freezing or TemperatureBand.Cold => 1,
                        TemperatureBand.Hot => 3,
                        _ => 2,
                    },
                    Terrain.Beach => ClimateSystem.ClassifyTemperature(_temperature[tileIndex]) switch
                    {
                        TemperatureBand.Freezing or TemperatureBand.Cold => 1,
                        TemperatureBand.Hot => 3,
                        _ => 2,
                    },
                    Terrain.DeepOcean => 2,
                    Terrain.Mountain or Terrain.RingWorldWall => 0,
                    _ when _biomes[tileIndex] is Biome.Swamp or Biome.Jungle => 4,
                    _ when _biomes[tileIndex] is Biome.Forest => 4,
                    _ when _biomes[tileIndex] is Biome.Taiga => 3,
                    _ when _biomes[tileIndex] is Biome.Grassland => 2,
                    _ when _biomes[tileIndex] is not (Biome.None or Biome.Desert) => 1,
                    _ => 0,
                };
        if (capacity == 0)
        {
            return 0;
        }
        return capacity;
    }

    private void RefreshTileNutrition(int tileIndex)
    {
        var capacity = CalculateTileNutritionCapacity(tileIndex);
        var previousCapacity = _tileNutritionCapacities[tileIndex];
        if (_tileNutritionLastTicks[tileIndex] < 0)
        {
            _tileNutritionCapacities[tileIndex] = (byte)capacity;
            _tileNutrition[tileIndex] = (byte)capacity;
            _tileNutritionLastTicks[tileIndex] = Tick;
            return;
        }

        if (capacity != previousCapacity)
        {
            _tileNutritionCapacities[tileIndex] = (byte)capacity;
            _tileNutrition[tileIndex] = previousCapacity == 0
                ? (byte)capacity
                : (byte)Math.Min(_tileNutrition[tileIndex], capacity);
            _tileNutritionLastTicks[tileIndex] = Tick;
        }
        if (capacity == 0 || _tileNutrition[tileIndex] >= capacity)
        {
            return;
        }

        var regenerationInterval = 120 * TicksPerSecond / capacity;
        var elapsed = Tick - _tileNutritionLastTicks[tileIndex];
        var regenerated = (int)(elapsed / regenerationInterval);
        if (regenerated <= 0)
        {
            return;
        }

        _tileNutrition[tileIndex] = (byte)Math.Min(
            capacity,
            _tileNutrition[tileIndex] + regenerated);
        _tileNutritionLastTicks[tileIndex] += (long)regenerated * regenerationInterval;
    }

    private bool IsWormFeedingTile(int tileIndex) =>
        _terrain[tileIndex] is Terrain.DeepOcean or Terrain.Shallows ||
        IsNewtFeedingTile(tileIndex);

    private static bool IsTrilobiteFeedingTerrain(Terrain terrain) =>
        terrain is Terrain.DeepOcean or Terrain.Ocean;

    private bool IsCrabFeedingTile(int tileIndex) =>
        _terrain[tileIndex] is Terrain.Beach or Terrain.Shallows;

    private bool IsFishForagingTile(int tileIndex) =>
        _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake ||
        _terrain[tileIndex] is Terrain.Shallows;

    private bool IsMonkeyFoliageTile(int tileIndex) =>
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle &&
        CanLiveOn(CritterSpecies.Monkey, tileIndex);

    private bool IsGrazerFoliageTile(CritterSpecies species, int tileIndex) =>
        CanLiveOn(species, tileIndex) && species switch
        {
            CritterSpecies.Deer => _biomes[tileIndex] is Biome.Grassland or Biome.Forest,
            CritterSpecies.Elk =>
                _biomes[tileIndex] is Biome.Grassland or Biome.Tundra or Biome.Taiga,
            CritterSpecies.Gazelle =>
                _biomes[tileIndex] is Biome.Arid or Biome.Forest or Biome.Grassland,
            _ => false,
        };

    private int WrappedManhattanDistance(GridPosition first, GridPosition second)
    {
        var horizontal = Math.Abs(first.X - second.X);
        horizontal = Math.Min(horizontal, Width - horizontal);
        return horizontal + Math.Abs(first.Y - second.Y);
    }

    private bool CanEnterOrShoveMovementBlocker(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        if (_occupants[destinationIndex] < 0)
        {
            return true;
        }

        return TryShoveMovementBlocker(moverIndex, destinationIndex, reservedPrey);
    }

    private bool CanShoveMovementBlocker(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null) =>
        CanShovePlankton(moverIndex, destinationIndex, reservedPrey) ||
        CanShoveNewt(moverIndex, destinationIndex, reservedPrey);

    private bool TryShoveMovementBlocker(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null) =>
        TryShovePlankton(moverIndex, destinationIndex, reservedPrey) ||
        TryShoveNewt(moverIndex, destinationIndex, reservedPrey);

    private bool CanShovePlankton(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        var planktonIndex = _occupants[destinationIndex];
        if (planktonIndex < 0 || _species[moverIndex] is CritterSpecies.Plankton ||
            _species[planktonIndex] is not CritterSpecies.Plankton ||
            !CanLiveOn(_species[moverIndex], destinationIndex))
        {
            return false;
        }

        var planktonPosition = _positions[planktonIndex];
        if (reservedPrey?.Contains(planktonPosition) is true)
        {
            return false;
        }

        return FindPlanktonShoveDestination(moverIndex, planktonIndex, reservedPrey) is not null ||
            (_species[moverIndex] is CritterSpecies.Worm or CritterSpecies.Trilobite &&
                FindPlanktonShoveChain(moverIndex, planktonIndex, reservedPrey) is not null);
    }

    private bool TryShovePlankton(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        if (!CanShovePlankton(moverIndex, destinationIndex, reservedPrey))
        {
            return false;
        }

        var planktonIndex = _occupants[destinationIndex];
        var shoveDestination = FindPlanktonShoveDestination(
            moverIndex,
            planktonIndex,
            reservedPrey);
        if (shoveDestination is not null)
        {
            MoveCritter(planktonIndex, GetIndex(shoveDestination.Value), shoveDestination.Value);
            return true;
        }

        return TryShovePlanktonChain(moverIndex, planktonIndex, reservedPrey);
    }

    private GridPosition? FindPlanktonShoveDestination(
        int moverIndex,
        int planktonIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var planktonPosition = _positions[planktonIndex];
        var startDirection = (moverIndex + planktonIndex) % MovementDirections.Length;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(planktonPosition.X + direction.X, Width),
                planktonPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                reservedPrey?.Contains(candidate) is true)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (_occupants[candidateIndex] < 0 &&
                CanLiveOn(CritterSpecies.Plankton, candidateIndex))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool CanShoveNewt(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var newtIndex = _occupants[destinationIndex];
        if (newtIndex < 0 ||
            _species[moverIndex] is not (CritterSpecies.Fish or CritterSpecies.MegaToad) ||
            _species[newtIndex] is not CritterSpecies.Newt ||
            !CanLiveOn(_species[moverIndex], destinationIndex) ||
            reservedPrey?.Contains(_positions[newtIndex]) is true)
        {
            return false;
        }

        return FindNewtShoveDestination(moverIndex, newtIndex, reservedPrey) is not null;
    }

    private bool TryShoveNewt(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (!CanShoveNewt(moverIndex, destinationIndex, reservedPrey))
        {
            return false;
        }

        var newtIndex = _occupants[destinationIndex];
        var shoveDestination = FindNewtShoveDestination(
            moverIndex,
            newtIndex,
            reservedPrey)!.Value;
        MoveCritter(newtIndex, GetIndex(shoveDestination), shoveDestination);
        return true;
    }

    private GridPosition? FindNewtShoveDestination(
        int moverIndex,
        int newtIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var newtPosition = _positions[newtIndex];
        var startDirection = (moverIndex + newtIndex) % MovementDirections.Length;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(newtPosition.X + direction.X, Width),
                newtPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                reservedPrey?.Contains(candidate) is true)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (_occupants[candidateIndex] < 0 && CanLiveOn(CritterSpecies.Newt, candidateIndex))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool CanShoveWorm(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var wormIndex = _occupants[destinationIndex];
        if (wormIndex < 0 || _species[moverIndex] is not CritterSpecies.Fish ||
            _species[wormIndex] is not CritterSpecies.Worm ||
            !CanLiveOn(CritterSpecies.Fish, destinationIndex) ||
            reservedPrey?.Contains(_positions[wormIndex]) is true)
        {
            return false;
        }

        return FindWormShoveDestination(moverIndex, wormIndex, reservedPrey) is not null;
    }

    private bool TryShoveWorm(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (!CanShoveWorm(moverIndex, destinationIndex, reservedPrey))
        {
            return false;
        }

        var wormIndex = _occupants[destinationIndex];
        var shoveDestination = FindWormShoveDestination(
            moverIndex,
            wormIndex,
            reservedPrey)!.Value;
        MoveCritter(wormIndex, GetIndex(shoveDestination), shoveDestination);
        return true;
    }

    private GridPosition? FindWormShoveDestination(
        int moverIndex,
        int wormIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var wormPosition = _positions[wormIndex];
        var startDirection = (moverIndex + wormIndex) % MovementDirections.Length;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(wormPosition.X + direction.X, Width),
                wormPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                reservedPrey?.Contains(candidate) is true)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (_occupants[candidateIndex] < 0 && CanLiveOn(CritterSpecies.Worm, candidateIndex))
            {
                return candidate;
            }
        }

        return null;
    }

    private (GridPosition Direction, int Length)? FindPlanktonShoveChain(
        int moverIndex,
        int firstPlanktonIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var firstPosition = _positions[firstPlanktonIndex];
        var moverPosition = _positions[moverIndex];
        var startDirection = (moverIndex + firstPlanktonIndex) % MovementDirections.Length;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var current = firstPosition;
            for (var length = 1; length <= MaximumPlanktonShoveChainLength; length++)
            {
                var candidate = new GridPosition(
                    Mod(current.X + direction.X, Width),
                    current.Y + direction.Y);
                if (candidate.Y < 0 || candidate.Y >= Height || candidate == moverPosition ||
                    candidate == firstPosition || reservedPrey?.Contains(candidate) is true)
                {
                    break;
                }

                var candidateIndex = GetIndex(candidate);
                var occupant = _occupants[candidateIndex];
                if (occupant < 0)
                {
                    if (CanLiveOn(CritterSpecies.Plankton, candidateIndex))
                    {
                        return (direction, length);
                    }
                    break;
                }
                if (_species[occupant] is not CritterSpecies.Plankton)
                {
                    break;
                }

                current = candidate;
            }
        }

        return null;
    }

    private bool TryShovePlanktonChain(
        int moverIndex,
        int firstPlanktonIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var shove = FindPlanktonShoveChain(moverIndex, firstPlanktonIndex, reservedPrey);
        if (shove is null)
        {
            return false;
        }

        var firstPosition = _positions[firstPlanktonIndex];
        for (var step = shove.Value.Length; step >= 1; step--)
        {
            var source = new GridPosition(
                Mod(firstPosition.X + shove.Value.Direction.X * (step - 1), Width),
                firstPosition.Y + shove.Value.Direction.Y * (step - 1));
            var destination = new GridPosition(
                Mod(firstPosition.X + shove.Value.Direction.X * step, Width),
                firstPosition.Y + shove.Value.Direction.Y * step);
            var planktonIndex = _occupants[GetIndex(source)];
            MoveCritter(planktonIndex, GetIndex(destination), destination);
        }

        return true;
    }

    private void MoveCritter(int critterIndex, int destinationIndex, GridPosition destination)
    {
        var origin = _positions[critterIndex];
        var movingSpecies = _species[critterIndex];
        _occupants[GetIndex(origin)] = -1;
        _occupants[destinationIndex] = critterIndex;
        _positions[critterIndex] = destination;
        TriggerWolfDenNear(origin, destination, movingSpecies);
    }

    private void TriggerWolfDenNear(
        GridPosition origin,
        GridPosition destination,
        CritterSpecies movingSpecies)
    {
        if (!CanEat(CritterSpecies.Wolf, movingSpecies) ||
            movingSpecies is CritterSpecies.MegaToad or CritterSpecies.Therapsid)
        {
            return;
        }

        if (!TryTriggerWolfDenAround(destination))
        {
            TryTriggerWolfDenAround(origin);
        }
    }

    private bool TryTriggerWolfDenAround(GridPosition center)
    {
        var startDirection = NextInt(MovementDirections.Length + 1);
        for (var offset = 0; offset <= MovementDirections.Length; offset++)
        {
            var directionIndex = (startDirection + offset) % (MovementDirections.Length + 1);
            var direction = directionIndex == MovementDirections.Length
                ? new GridPosition(0, 0)
                : MovementDirections[directionIndex];
            var y = center.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var denPosition = new GridPosition(Mod(center.X + direction.X, Width), y);
            var denTile = GetIndex(denPosition);
            if (!_wolfDenCharges.TryGetValue(denTile, out var charges) || charges <= 0)
            {
                continue;
            }

            var spawnPosition = FindWolfDenSpawnPosition(denPosition);
            if (spawnPosition is null || !TryAddCritter(CritterSpecies.Wolf, spawnPosition.Value))
            {
                continue;
            }

            var spawnedWolfIndex = _occupants[GetIndex(spawnPosition.Value)];
            var spawnedWolfId = _critterIds[spawnedWolfIndex].Value;
            _wolfDenHomes[spawnedWolfId] = denTile;
            _wolfDenCharges[denTile] = charges - 1;
            return true;
        }
        return false;
    }

    private GridPosition? FindWolfDenSpawnPosition(GridPosition denPosition)
    {
        var denTile = GetIndex(denPosition);
        if (_occupants[denTile] < 0 && CanLiveOn(CritterSpecies.Wolf, denTile))
        {
            return denPosition;
        }

        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var y = denPosition.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var candidate = new GridPosition(Mod(denPosition.X + direction.X, Width), y);
            var tileIndex = GetIndex(candidate);
            if (_occupants[tileIndex] < 0 && CanLiveOn(CritterSpecies.Wolf, tileIndex))
            {
                return candidate;
            }
        }
        return null;
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
        DetachWolfFromDens(removedId.Value);
        DetachApeFromVillage(removedId.Value);
        _reproductionTruces.Remove(removedId.Value);
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
            _damageFlashUntilTicks[critterIndex] = _damageFlashUntilTicks[lastIndex];
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
        species is CritterSpecies.Fish
            ? IsFishTile(tileIndex)
            : species is CritterSpecies.ApeSailor
                ? IsApeSailorTile(tileIndex)
            : species is CritterSpecies.Worm
                ? IsWormTile(tileIndex)
            : species is CritterSpecies.Newt
            ? IsNewtTransitTile(tileIndex)
            : species is CritterSpecies.MegaToad
                ? IsMegaToadTile(tileIndex)
                : species is CritterSpecies.Crab
                    ? IsCrabTile(tileIndex)
                : CritterHabitats.CanOccupy(
                    CritterHabitats.GetHabitat(species),
                    _terrain[tileIndex],
                    _surfaceWater[tileIndex],
                    _biomes[tileIndex],
                    _surfaceCovers[tileIndex]);

    private bool IsApeSailorTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        _terrain[tileIndex] is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Beach;

    private bool IsWormTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (_terrain[tileIndex] is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows ||
            _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake);

    private bool IsFishTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (_terrain[tileIndex] is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows ||
            _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake);

    private bool IsCrabTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        _terrain[tileIndex] is not Terrain.Ice &&
        (_terrain[tileIndex] is Terrain.Beach ||
            (_biomes[tileIndex] is not Biome.Arctic &&
                _terrain[tileIndex] is Terrain.Ocean or Terrain.Shallows or Terrain.Plains or Terrain.Hills or
                    Terrain.Lowlands or Terrain.Canyon or Terrain.Trench));

    internal bool IsValidReproductionSite(CritterSpecies species, GridPosition position) =>
        species switch
        {
            CritterSpecies.MegaToad => IsAtOrAdjacentToMegaToadBreedingWater(GetIndex(position)),
            CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle =>
                !HasOccupiedCardinalNeighbor(position),
            _ => true,
        };

    private bool HasOccupiedCardinalNeighbor(GridPosition position)
    {
        foreach (var direction in CardinalDirections)
        {
            var neighbor = new GridPosition(
                Mod(position.X + direction.X, Width),
                position.Y + direction.Y);
            if (neighbor != position && neighbor.Y >= 0 && neighbor.Y < Height &&
                _occupants[GetIndex(neighbor)] >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal bool IsValidBirthSite(CritterSpecies species, GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return species switch
        {
            CritterSpecies.MegaToad => IsMegaToadBreedingWater(tileIndex),
            _ => true,
        };
    }

    private bool IsAtOrAdjacentToMegaToadBreedingWater(int tileIndex)
    {
        if (IsMegaToadBreedingWater(tileIndex))
        {
            return true;
        }

        var position = new GridPosition(tileIndex % Width, tileIndex / Width);
        foreach (var direction in CardinalDirections)
        {
            var y = position.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var neighborIndex = y * Width + Mod(position.X + direction.X, Width);
            if (IsMegaToadBreedingWater(neighborIndex))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsMegaToadBreedingWater(int tileIndex) =>
        _surfaceWater[tileIndex] is SurfaceWaterKind.River ||
        (_surfaceWater[tileIndex] is SurfaceWaterKind.FreshwaterLake &&
            _biomes[tileIndex] is not Biome.Arctic);

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

    private bool IsNewtFoliageTile(int tileIndex) =>
        IsNewtOrdinaryTile(tileIndex) &&
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle;

    private bool IsAtOrAdjacentToNewtFood(int tileIndex)
    {
        if (IsNewtFeedingTile(tileIndex))
        {
            return true;
        }

        var position = new GridPosition(tileIndex % Width, tileIndex / Width);
        foreach (var direction in CardinalDirections)
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
        CritterSpecies.Worm => 8 * TicksPerSecond,
        CritterSpecies.Trilobite => 6 * TicksPerSecond,
        CritterSpecies.SeaScorpion => 3 * TicksPerSecond,
        CritterSpecies.Nautilus => 6 * TicksPerSecond,
        CritterSpecies.Squid => 3 * TicksPerSecond,
        CritterSpecies.SquidEgg => 5 * TicksPerSecond,
        CritterSpecies.Fish => 2 * TicksPerSecond,
        CritterSpecies.Newt => 5 * TicksPerSecond,
        CritterSpecies.MegaToad => 6 * TicksPerSecond,
        CritterSpecies.Therapsid => 6 * TicksPerSecond,
        CritterSpecies.Monkey => 5 * TicksPerSecond,
        CritterSpecies.Ape => 3 * TicksPerSecond,
        CritterSpecies.ApeSailor => 3 * TicksPerSecond,
        CritterSpecies.Deer => 3 * TicksPerSecond,
        CritterSpecies.Elk => 6 * TicksPerSecond,
        CritterSpecies.Gazelle => 3 * TicksPerSecond,
        CritterSpecies.Wolf => 2 * TicksPerSecond,
        CritterSpecies.Crab => 4 * TicksPerSecond,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    private long GetFirstMovementTick(CritterSpecies species)
    {
        var interval = GetMovementIntervalTicks(species);
        // Hunter perception is bounded but costlier than wandering. Distribute
        // newly born/evolved hunters across their interval so cohorts do not scan
        // on the same tick.
        return species is CritterSpecies.Fish or CritterSpecies.Nautilus or
            CritterSpecies.Squid or CritterSpecies.SeaScorpion or CritterSpecies.MegaToad or
            CritterSpecies.Therapsid or CritterSpecies.Monkey or CritterSpecies.Ape or
            CritterSpecies.ApeSailor or CritterSpecies.Wolf
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
