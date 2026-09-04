namespace Newt.Simulation;

/// <summary>
/// Owns deterministic world state. It has no dependency on MonoGame, wall-clock
/// time, rendering, or input, so the same seed and commands produce the same run.
/// </summary>
public sealed partial class SimulationWorld
{
    private const int FishPerceptionRadius = 6;
    private const int FishPredatorFleeRadius = 5;
    private const int TrilobitePredatorFleeRadius = 3;
    internal const int TrilobitePerceptionRadius = 5;
    private const int LandPreyFleeRadius = 5;
    internal const int NautilusPerceptionRadius = 5;
    private const int SquidPerceptionRadius = 5;
    private const int SquidEggHatchRadius = 2;
    private const int SeaScorpionPerceptionRadius = 4;
    private const int MegaToadPerceptionRadius = 3;
    private const int TherapsidPerceptionRadius = 4;
    internal const int ToothedWhalePerceptionRadius = 7;
    private const int ApePerceptionRadius = 6;
    internal const int ApeDefenderPerceptionRadius = ApeVillageClaimRadius;
    internal const int CrabFeedingPerceptionRadius = 5;
    private const int ApeVillageSearchRadius = 28;
    private const int ApeVillageClaimRadius = 28;
    private const int ApeVillageMinimumDistance = 12;
    private const int ApeVillageClusterRadius = 4;
    private const int ApeVillageClusterChancePercent = 80;
    private const int ApeSettlerPopulationThreshold = 100;
    private const int ApeSettlerFoodCost = 100;
    private const int ApeSettlerInitialEnergy = 100;
    private const int ApeSettlerCheckIntervalTicks = 60 * TicksPerSecond;
    private const int ApeSettlerChancePercent = 1;
    private const int ApeSettlerMinimumDistance = 40;
    internal const int LoneApeSailorColonistDelayTicks = 60 * TicksPerSecond;
    private const int ApeVillageBasePopulationCapacity = 5;
    private const int ApeResidentialPopulationCapacity = 5;
    internal const int ApeResidentialUnderuseTicks = 2 * 60 * TicksPerSecond;
    internal const long ApeRuinDecayTicks = 2L * SeasonSystem.TicksPerYear;
    private const int ApeFoodDistrictPopulationThreshold = 5;
    private const int ApeVillageBaseFoodCapacity = 5;
    private const int ApeFoodDistrictStorageCapacity = 10;
    internal const int ApeVillageHuntingFoodThreshold = 10;
    private const int ApeInitialWood = 20;
    private const int ApeMaximumWood = 30;
    private const int ApeLumberCampFoodCost = 3;
    private const int ApeFoodDistrictWoodCost = 2;
    private const int ApeResidentialFoodCost = 5;
    private const int ApeResidentialWoodCost = 4;
    private const int ApeHarborWoodCost = 6;
    private const int ApeMilitaryDistrictWoodCost = 6;
    private const int ApeSailorWoodCost = 2;
    private const int ApeWarriorWoodCost = 2;
    private const int ApeFarmFoodIntervalTicks = 14 * TicksPerSecond;
    private const int ApeSailorRecruitmentIntervalTicks = 30 * TicksPerSecond;
    private const int ApeWarriorRecruitmentIntervalTicks = 30 * TicksPerSecond;
    private const int ApeSailorsPerHarbor = 4;
    private const int ApeWarriorsPerMilitaryDistrict = 4;
    private const int ApeMilitaryDistrictPopulationInterval = 50;
    private const int ApeInfrastructurePopulationInterval = 150;
    internal const int WolfPerceptionRadius = 5;
    private const int WolfDenSearchRadius = 8;
    private const int NewtFreshwaterPerceptionRadius = 8;
    private const int NewtMegaToadFleeRadius = 4;
    private const int MaximumPlanktonShoveChainLength = 4;
    private const int StrandedMovementIntervalMultiplier = 4;
    private const int StrandedRecoverySearchRadius = 16;
    public const int ReproductionTruceTicks = 30 * TicksPerSecond;
    public const int CombatDamageFlashTicks = TicksPerSecond / 2;
    public const int MaximumWolfDenCharges = 5;
    public const int WolfDenChargeDecayTicks = 2 * 60 * TicksPerSecond;
    public const int ApeFoodReturnStallTicks = 30 * TicksPerSecond;
    public const int PlanktonRecoveryIntervalTicks = 15_000;
    public const int PlanktonMovementIntervalTicks = 10 * TicksPerSecond;
    public const int PlanktonFeedingIntervalTicks = 20 * TicksPerSecond;
    public const int TicksPerSecond = 20;
    public const int TileNutritionRegenerationSeconds = 480;
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
    private readonly byte[] _depositedTileNutrition;
    private readonly byte[] _tileNutritionCapacities;
    private readonly long[] _tileNutritionLastTicks;
    private readonly HashSet<GridPosition> _activeSurfaceCovers = [];
    private readonly long[] _lifeRecoveryUntilTicks;
    private readonly List<LifeRecoveryTile> _lifeRecoveryTiles = [];
    private readonly SurfaceWaterKind[] _surfaceWater;
    private readonly Dictionary<int, int> _lakeTileCounts = [];
    private readonly List<int> _lakeInspectionTiles = [];
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
    private readonly Dictionary<int, int> _wolfDenHomes = [];
    private readonly Dictionary<int, int> _wolfDenTargets = [];
    private readonly Dictionary<int, int> _wolfDenCharges = [];
    private readonly Dictionary<int, long> _wolfDenNextDecayTicks = [];
    private readonly HashSet<int> _teleporters = [];
    private readonly Dictionary<int, ApeStructureKind> _apeStructures = [];
    private readonly Dictionary<int, int> _apeAuxiliaryVillages = [];
    private readonly Dictionary<int, int> _apeVillageHomes = [];
    private readonly Dictionary<int, int> _apeVillageTargets = [];
    private readonly Dictionary<int, (int OriginVillageTile, int TargetVillageTile)> _apeSettlerTargets = [];
    private readonly Dictionary<int, (int TargetVillageTile, Queue<int> Tiles)> _apeSettlerPaths = [];
    private readonly Dictionary<int, int> _apeVillageFood = [];
    private readonly Dictionary<int, int> _apeVillageWood = [];
    private readonly Dictionary<int, int> _apeVillageIds = [];
    private readonly Dictionary<int, int> _apeCarriedFood = [];
    private readonly Dictionary<int, (int BestDistance, long LastProgressTick)>
        _apeFoodReturnProgress = [];
    private readonly Dictionary<int, long> _apeStructureNextActionTicks = [];
    private readonly Dictionary<int, long> _apeFoodDistrictInactiveSinceTicks = [];
    private readonly Dictionary<int, long> _apeResidentialUnderusedSinceTicks = [];
    private readonly Dictionary<int, long> _apeRuinDecayTicks = [];
    private readonly Dictionary<int, long> _loneApeSailorSinceTicks = [];
    private readonly Dictionary<int, (CritterSpecies ParentSpecies, long UntilTick)>
        _reproductionTruces = [];
    private readonly HashSet<int> _lethallyDisplacedCritterIds = [];
    private readonly CritterSpecies[] _species;
    private readonly GridPosition[] _positions;
    private readonly long[] _nextMovementTicks;
    private readonly int[] _energy;
    private readonly long[] _nextMetabolismTicks;
    private readonly int[] _preyTargets;
    private readonly long[] _damageFlashUntilTicks;
    private readonly int[] _speciesCounts = new int[Enum.GetValues<CritterSpecies>().Length];
    private ulong _randomState;
    private long _nextPlanktonRecoveryTick = long.MaxValue;
    private long _nextApeRuinDecayTick = long.MaxValue;
    private int _nextCritterId = 1;
    private int _nextApeVillageId = 1;
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
        _depositedTileNutrition = new byte[_terrain.Length];
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

    /// <summary>The primary, movable saltwater source; additional sources are preserved separately.</summary>
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

    public int GetCritterCount(CritterSpecies species) => _speciesCounts[(int)species];

    public bool PlanktonRecoveryEnabled { get; private set; }

    public int ActiveSpringCount => _activeSprings.Count;

    public int VolcanoCount => _volcanoes.Count;

    public int ActiveLavaFlowCount => _lavaFlows.Count;

    public int ActiveImpactWaveCount => _impactWaves.Count;

    public int WolfDenCount => _wolfDenCharges.Count;

    public int TeleporterCount => _teleporters.Count;

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

    public bool HasTeleporter(GridPosition position) =>
        Contains(position) && _teleporters.Contains(GetIndex(position));

    public ApeStructureKind? GetApeStructure(GridPosition position) =>
        _apeStructures.TryGetValue(GetIndex(position), out var structure) ? structure : null;

    /// <summary>Returns the village that owns an Ape structure, including the village itself.</summary>
    public GridPosition? GetApeStructureVillage(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_apeStructures.TryGetValue(tileIndex, out var structure))
        {
            return null;
        }

        var villageTile = structure is ApeStructureKind.Village
            ? tileIndex
            : _apeAuxiliaryVillages.TryGetValue(tileIndex, out var ownerTile)
                ? ownerTile
                : -1;
        return villageTile >= 0
            ? new GridPosition(villageTile % Width, villageTile / Width)
            : null;
    }

    /// <summary>Returns the stable, world-local identity of a village or its owned structure.</summary>
    public int? GetApeVillageId(GridPosition position)
    {
        var village = GetApeStructureVillage(position);
        return village is { } villagePosition &&
            _apeVillageIds.TryGetValue(GetIndex(villagePosition), out var villageId)
                ? villageId
                : null;
    }

    public int GetApeVillageResidentCount(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _apeVillageHomes.Count(pair => pair.Value == tileIndex);
    }

    public int GetApeVillageSailorCount(GridPosition position)
    {
        var villageTile = GetIndex(position);
        return _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeSailor);
    }

    public int GetApeVillageWarriorCount(GridPosition position)
    {
        var villageTile = GetIndex(position);
        return _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeWarrior);
    }

    public int GetApeVillageChieftainCount(GridPosition position)
    {
        var villageTile = GetIndex(position);
        return _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeChieftain);
    }

    public int GetApeVillageCivilianCount(GridPosition position)
    {
        var villageTile = GetIndex(position);
        return _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.Ape);
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

    public int GetApeVillageFoodCapacity(GridPosition position)
    {
        var villageTile = GetIndex(position);
        return _apeStructures.TryGetValue(villageTile, out var structure) &&
            structure is ApeStructureKind.Village
                ? GetApeVillageFoodCapacityByTile(villageTile)
                : 0;
    }

    public int GetApeVillageWood(GridPosition position) =>
        _apeVillageWood.TryGetValue(GetIndex(position), out var wood) ? wood : 0;

    public int GetApeVillageWoodCapacity(GridPosition position) =>
        _apeStructures.TryGetValue(GetIndex(position), out var structure) &&
            structure is ApeStructureKind.Village
                ? ApeMaximumWood
                : 0;

    internal void StoreApeVillageFood(GridPosition position, int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var villageTile = GetIndex(position);
        if (!_apeStructures.TryGetValue(villageTile, out var structure) ||
            structure is not ApeStructureKind.Village)
        {
            throw new InvalidOperationException($"Tile {position} is not an Ape Village.");
        }
        AddApeVillageFood(villageTile, amount);
    }

    public int GetApeCarriedFood(CritterId apeId) =>
        _apeCarriedFood.TryGetValue(apeId.Value, out var food) ? food : 0;

    public int GetTileNutrition(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        RefreshTileNutrition(tileIndex);
        return _tileNutrition[tileIndex] + _depositedTileNutrition[tileIndex];
    }

    public int GetTileNutritionCapacity(GridPosition position) =>
        CalculateTileNutritionCapacity(GetIndex(position));

    internal bool TryAssignApeToVillage(CritterId apeId, GridPosition villagePosition)
    {
        var villageTile = GetIndex(villagePosition);
        if (!_critterIndicesById.TryGetValue(apeId.Value, out var apeIndex) ||
            _species[apeIndex] is not (CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain) ||
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

    public bool TryPlaceTeleporter(GridPosition position)
    {
        if (!Contains(position))
        {
            return false;
        }

        var tileIndex = GetIndex(position);
        if (_teleporters.Contains(tileIndex) || _occupants[tileIndex] >= 0 ||
            _terrain[tileIndex] is Terrain.RingWorldWall ||
            _surfaceCovers[tileIndex] is not SurfaceCover.None ||
            _wolfDenCharges.ContainsKey(tileIndex) || _megaSpiderWebFood.ContainsKey(tileIndex) ||
            _apeStructures.ContainsKey(tileIndex) ||
            _volcanoes.Any(volcano => volcano.Position == position))
        {
            return false;
        }

        return _teleporters.Add(tileIndex);
    }

    public bool RemoveTeleporterAt(GridPosition position) =>
        Contains(position) && _teleporters.Remove(GetIndex(position));

    private bool TryCreateWolfDen(GridPosition position, int initialCharges)
    {
        if (!Contains(position))
        {
            return false;
        }
        var tileIndex = GetIndex(position);
        if (_wolfDenCharges.ContainsKey(tileIndex) || _megaSpiderWebFood.ContainsKey(tileIndex) ||
            _teleporters.Contains(tileIndex) ||
            _apeStructures.ContainsKey(tileIndex) ||
            _terrain[tileIndex] is Terrain.Shallows ||
            !CanLiveOn(CritterSpecies.Wolf, tileIndex))
        {
            return false;
        }

        _wolfDenCharges.Add(tileIndex, initialCharges);
        _wolfDenNextDecayTicks[tileIndex] = Tick + WolfDenChargeDecayTicks;
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

    /// <summary>
    /// Counts the cardinally connected lake under a tile, including frozen lakes
    /// and horizontal wrap. Rivers never join separate lakes for this query.
    /// </summary>
    public int GetLakeTileCount(GridPosition position)
    {
        var start = GetIndex(position);
        if (_surfaceWater[start] is not SurfaceWaterKind.FreshwaterLake)
        {
            return 0;
        }
        if (_lakeTileCounts.TryGetValue(start, out var count))
        {
            return count;
        }

        // Cache every tile in the component so moving the pointer across a large
        // lake does not flood-fill it again on every rendered frame.
        _lakeInspectionTiles.Clear();
        Visit(start);
        for (var cursor = 0; cursor < _lakeInspectionTiles.Count; cursor++)
        {
            var current = GetPosition(_lakeInspectionTiles[cursor]);
            foreach (var direction in CardinalDirections)
            {
                var y = current.Y + direction.Y;
                if (y >= 0 && y < Height)
                {
                    Visit(y * Width + Mod(current.X + direction.X, Width));
                }
            }
        }
        count = _lakeInspectionTiles.Count;
        foreach (var tile in _lakeInspectionTiles)
        {
            _lakeTileCounts[tile] = count;
        }
        return count;

        void Visit(int tile)
        {
            if (_surfaceWater[tile] is SurfaceWaterKind.FreshwaterLake &&
                _lakeTileCounts.TryAdd(tile, 0))
            {
                _lakeInspectionTiles.Add(tile);
            }
        }
    }

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
        => SetTerrain(position, terrain, validateApeStructure: true);

    internal void SetClimateTerrain(GridPosition position, Terrain terrain)
        => SetTerrain(position, terrain, validateApeStructure: false);

    private void SetTerrain(
        GridPosition position,
        Terrain terrain,
        bool validateApeStructure)
    {
        var index = GetIndex(position);
        if (_terrain[index] != terrain)
        {
            _terrain[index] = terrain;
            if (terrain is Terrain.RingWorldWall)
            {
                _teleporters.Remove(index);
            }
            if (validateApeStructure)
            {
                RemoveInvalidApeStructureAt(position);
            }
            RemoveInvalidMegaSpiderWebAt(position);
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
        RemoveMegaSpiderWebAt(position);
        RemoveTeleporterAt(position);
        RemoveApeStructureAt(position);
        RemoveCritterAt(position);
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
            RemoveMegaSpiderWebAt(position);
            RemoveTeleporterAt(position);
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
    }

    internal void SetSurfaceWater(GridPosition position, SurfaceWaterKind water)
    {
        var index = GetIndex(position);
        if (_surfaceWater[index] != water)
        {
            if (_surfaceWater[index] is SurfaceWaterKind.FreshwaterLake ||
                water is SurfaceWaterKind.FreshwaterLake)
            {
                _lakeTileCounts.Clear();
            }
            _surfaceWater[index] = water;
            RemoveInvalidApeStructureAt(position);
            RemoveInvalidMegaSpiderWebAt(position);
        }
    }

    internal void SetWaterSurfaceElevation(GridPosition position, float? elevation) =>
        _waterSurfaceElevations[GetIndex(position)] = elevation ?? float.NaN;

    internal void AddRiverConnection(GridPosition position, RiverConnection connection) =>
        _riverConnections[GetIndex(position)] |= connection;

    internal void ClearFreshwaterForTerrainRebuild(GridPosition position)
    {
        var index = GetIndex(position);
        if (_surfaceWater[index] is SurfaceWaterKind.FreshwaterLake)
        {
            _lakeTileCounts.Clear();
        }
        _surfaceWater[index] = SurfaceWaterKind.None;
        _waterSurfaceElevations[index] = float.NaN;
        _riverConnections[index] = RiverConnection.None;
        // The caller validates structures once final coastlines are available.
    }

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
        var distinctSeeds = seeds.Where(seed => seed != OceanSeed).Distinct().ToArray();
        _additionalOceanSeeds.Clear();
        _additionalOceanSeeds.AddRange(distinctSeeds);
    }

    internal bool TryRegisterOceanSeed(GridPosition position)
    {
        if (!Contains(position) || GetElevation(position) > SeaLevel ||
            HasOceans && (GetTerrain(position) is Terrain.DeepOcean or Terrain.Ocean or
                Terrain.Shallows or Terrain.Ice) ||
            _additionalOceanSeeds.Contains(position))
        {
            return false;
        }
        if (position == OceanSeed)
        {
            if (HasOceans)
            {
                return false;
            }
        }
        else
        {
            _additionalOceanSeeds.Add(position);
        }
        HasOceans = true;
        return true;
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
        _lakeTileCounts.Clear();
        Array.Clear(_surfaceWater);
        Array.Fill(_waterSurfaceElevations, float.NaN);
        Array.Clear(_riverConnections);
        _activeSprings.Clear();
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
        if (charges == 0)
        {
            _wolfDenNextDecayTicks[tileIndex] = Tick + WolfDenChargeDecayTicks;
        }
        return true;
    }

    private void AdvanceWolfDens()
    {
        foreach (var denTile in _wolfDenCharges.Keys.ToArray())
        {
            if (!_wolfDenNextDecayTicks.TryGetValue(denTile, out var nextDecayTick))
            {
                _wolfDenNextDecayTicks[denTile] = Tick + WolfDenChargeDecayTicks;
                continue;
            }
            if (Tick < nextDecayTick)
            {
                continue;
            }

            var elapsedIntervals = 1 + (Tick - nextDecayTick) / WolfDenChargeDecayTicks;
            _wolfDenNextDecayTicks[denTile] =
                nextDecayTick + elapsedIntervals * WolfDenChargeDecayTicks;
            _wolfDenCharges[denTile] = Math.Max(
                0,
                _wolfDenCharges[denTile] - (int)elapsedIntervals);
            RemoveWolfDenIfEmptyAndUnassociated(denTile);
        }
    }

    public bool RemoveWolfDenAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_wolfDenCharges.Remove(tileIndex))
        {
            return false;
        }
        _wolfDenNextDecayTicks.Remove(tileIndex);

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
        _wolfDenNextDecayTicks.Remove(tileIndex);
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
        _apeResidentialUnderusedSinceTicks.Remove(tileIndex);
        _apeRuinDecayTicks.Remove(tileIndex);

        if (structure is not ApeStructureKind.Village)
        {
            var hasVillage = _apeAuxiliaryVillages.TryGetValue(tileIndex, out var villageTile);
            _apeAuxiliaryVillages.Remove(tileIndex);
            _apeStructureNextActionTicks.Remove(tileIndex);
            _apeFoodDistrictInactiveSinceTicks.Remove(tileIndex);
            if (hasVillage)
            {
                ClampApeVillageFood(villageTile);
            }
            return true;
        }

        _apeVillageFood.Remove(tileIndex);
        _apeVillageWood.Remove(tileIndex);
        _apeVillageIds.Remove(tileIndex);
        _loneApeSailorSinceTicks.Remove(tileIndex);

        foreach (var auxiliaryTile in _apeAuxiliaryVillages
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _apeAuxiliaryVillages.Remove(auxiliaryTile);
            _apeStructures.Remove(auxiliaryTile);
            _apeStructureNextActionTicks.Remove(auxiliaryTile);
            _apeFoodDistrictInactiveSinceTicks.Remove(auxiliaryTile);
            _apeResidentialUnderusedSinceTicks.Remove(auxiliaryTile);
            _apeRuinDecayTicks.Remove(auxiliaryTile);
        }
        foreach (var id in _apeVillageHomes
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _apeVillageHomes.Remove(id);
            _apeCarriedFood.Remove(id);
            _apeFoodReturnProgress.Remove(id);
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
        _apeSettlerTargets.Remove(apeId);
        _apeSettlerPaths.Remove(apeId);
        _apeCarriedFood.Remove(apeId);
        _apeFoodReturnProgress.Remove(apeId);
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
            ApeStructureKind.Village =>
                CanBuildApeStructureOn(tileIndex, structure) && CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.Farm or ApeStructureKind.RicePaddy or ApeStructureKind.Orchard or
                ApeStructureKind.Aquaculture =>
                IsApeFoodDistrictStructurallyValid(tileIndex, structure),
            ApeStructureKind.LumberCamp => IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.NavalDistrict => IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.MilitaryDistrict => IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.ResidentialDistrict =>
                IsValidApeAuxiliaryTile(tileIndex, structure),
            ApeStructureKind.Ruin => true,
            _ => false,
        };
        if (!valid)
        {
            RemoveApeStructureAt(position);
        }
    }

    internal void RevalidateApeStructures()
    {
        foreach (var tileIndex in _apeStructures.Keys.ToArray())
        {
            RemoveInvalidApeStructureAt(
                new GridPosition(tileIndex % Width, tileIndex / Width));
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

        return AddCritterUnchecked(species, position, tileIndex);
    }

    private CritterId AddCritterUnchecked(
        CritterSpecies species,
        GridPosition position,
        int tileIndex)
    {

        var index = _count++;
        var id = new CritterId(_nextCritterId++);
        _critterIds[index] = id;
        _critterIndicesById.Add(id.Value, index);
        _species[index] = species;
        _positions[index] = position;
        _nextMovementTicks[index] = GetFirstMovementTick(species, id);
        var nutrition = CritterNutritions.Get(species);
        _energy[index] = nutrition.InitialEnergy;
        _nextMetabolismTicks[index] = nutrition.HasMetabolism
            ? Tick + nutrition.MetabolismIntervalTicks
            : long.MaxValue;
        _preyTargets[index] = -1;
        _damageFlashUntilTicks[index] = 0;
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
    /// Spawns a player-selected critter on any empty in-bounds tile. Habitat
    /// rules resume immediately afterward, so an incompatible critter is
    /// stranded and attempts to recover normally.
    /// </summary>
    public bool TrySpawnCritter(CritterSpecies species, GridPosition position)
    {
        if (!LifeEnabled || !Contains(position))
        {
            return false;
        }

        var tileIndex = GetIndex(position);
        if (_occupants[tileIndex] >= 0)
        {
            return false;
        }

        AddCritterUnchecked(species, position, tileIndex);
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

    /// <summary>Seeds one plankton immediately, then restores extinction after a long delay.</summary>
    public bool EnablePlanktonRecovery()
    {
        if (!LifeEnabled)
        {
            return false;
        }
        PlanktonRecoveryEnabled = true;
        _nextPlanktonRecoveryTick = Tick + PlanktonRecoveryIntervalTicks;
        return EnsurePlanktonPopulation();
    }

    internal void DisablePlanktonRecovery()
    {
        PlanktonRecoveryEnabled = false;
        _nextPlanktonRecoveryTick = long.MaxValue;
    }

    public CritterSnapshot GetCritter(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index >= _count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var nutrition = CritterNutritions.Get(_species[index]);
        var isColonist = false;
        GridPosition? colonistDestination = null;
        if (_species[index] is CritterSpecies.Ape &&
            _apeSettlerTargets.TryGetValue(_critterIds[index].Value, out var colonistRoute))
        {
            isColonist = true;
            colonistDestination = GetPosition(colonistRoute.TargetVillageTile);
        }
        return new CritterSnapshot(
            _critterIds[index],
            _species[index],
            _positions[index],
            _energy[index],
            nutrition.MaximumEnergy,
            nutrition.HasMetabolism && _energy[index] <= nutrition.HungryThreshold,
            !isColonist && CanSpeciesReproduce(_species[index]) &&
                _energy[index] >= nutrition.ReproductionThreshold,
            Tick < _damageFlashUntilTicks[index],
            GetPlague(index),
            isColonist,
            colonistDestination);
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
        AdvanceWolfDens();
        AdvanceMegaSpiderWebs();
        SpreadPlagues();
        AdvanceCritterLifecycles();
        AdvanceApeVillages();
        AdvanceApeRuins();
        AdvanceVillagePlagueOutbreaks();
        if (PlanktonRecoveryEnabled && Tick >= _nextPlanktonRecoveryTick)
        {
            _nextPlanktonRecoveryTick = Tick + PlanktonRecoveryIntervalTicks;
            if (GetCritterCount(CritterSpecies.Plankton) == 0)
            {
                EnsurePlanktonPopulation();
            }
        }
        AdvanceCritterMovements();
    }

    private void AdvanceCritterMovements()
    {
        _lethallyDisplacedCritterIds.Clear();
        List<(GridPosition Predator, GridPosition Prey)>? predations = null;
        HashSet<GridPosition>? reservedPrey = null;
        for (var index = 0; index < _count; index++)
        {
            if (_lethallyDisplacedCritterIds.Contains(_critterIds[index].Value))
            {
                continue;
            }
            if (reservedPrey?.Contains(_positions[index]) is true)
            {
                continue;
            }
            if (Tick < _nextMovementTicks[index])
            {
                continue;
            }
            if (IsCaughtInMegaSpiderWeb(index))
            {
                _nextMovementTicks[index] = Tick + GetMovementIntervalTicks(_species[index]);
                continue;
            }

            var isStranded = !CanCritterRemainOnTile(index, GetIndex(_positions[index]));
            _nextMovementTicks[index] += GetMovementIntervalTicks(_species[index]) *
                (isStranded ? StrandedMovementIntervalMultiplier : 1);
            var prey = isStranded
                ? TryMoveStranded(index)
                : _species[index] switch
            {
                CritterSpecies.Plankton => TryMovePlankton(index),
                CritterSpecies.Fish => TryMoveFish(index, reservedPrey),
                CritterSpecies.Nautilus => TryMoveNautilus(index, reservedPrey),
                CritterSpecies.Squid =>
                    TryMoveHunter(index, SquidPerceptionRadius, reservedPrey),
                CritterSpecies.SeaScorpion =>
                    TryMoveHunter(index, SeaScorpionPerceptionRadius, reservedPrey),
                CritterSpecies.MegaSpider => TryMoveMegaSpider(index, reservedPrey),
                CritterSpecies.MegaToad => TryMoveHunter(index, MegaToadPerceptionRadius, reservedPrey),
                CritterSpecies.Therapsid => TryMoveTherapsid(index, reservedPrey),
                CritterSpecies.Monkey => TryMoveMonkey(index, reservedPrey),
                CritterSpecies.Ape => TryMoveApe(index, reservedPrey),
                CritterSpecies.ApeSailor => TryMoveApeSailor(index, reservedPrey),
                CritterSpecies.ApeWarrior =>
                    TryMoveHunter(
                        index,
                        ApeDefenderPerceptionRadius,
                        reservedPrey,
                        huntWhenFull: true),
                CritterSpecies.ApeChieftain =>
                    TryMoveHunter(
                        index,
                        ApeDefenderPerceptionRadius,
                        reservedPrey,
                        huntWhenFull: true),
                CritterSpecies.UndeadApe => TryMoveHunter(index, ApePerceptionRadius, reservedPrey),
                CritterSpecies.Wolf => TryMoveWolf(index, reservedPrey),
                CritterSpecies.ToothedWhale =>
                    TryMoveHunter(index, ToothedWhalePerceptionRadius, reservedPrey),
                CritterSpecies.BaleenWhale =>
                    TryMoveHunter(index, ToothedWhalePerceptionRadius, reservedPrey),
                CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle =>
                    TryMoveGrazer(index),
                CritterSpecies.Newt => TryMoveNewt(index),
                CritterSpecies.Worm or CritterSpecies.Trilobite => TryMoveTrilobiteLike(index),
                CritterSpecies.Crab => TryMoveCrab(index),
                CritterSpecies.SquidEgg => TryMoveSquidEgg(index),
                _ => TryMove(index, reservedPrey),
            };
            if (prey is not null)
            {
                reservedPrey ??= [];
                reservedPrey.Add(prey.Value);
                (predations ??= []).Add((_positions[index], prey.Value));
            }
        }

        foreach (var critterId in _lethallyDisplacedCritterIds)
        {
            if (_critterIndicesById.TryGetValue(critterId, out var critterIndex))
            {
                RemoveCritterAtIndex(critterIndex);
            }
        }
        _lethallyDisplacedCritterIds.Clear();

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
        List<CritterId>? deaths = null;
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
            if (species is CritterSpecies.SquidEgg &&
                (_terrain[GetIndex(_positions[index])] is Terrain.Shallows ||
                    HasSquidEggHatchPreyNearby(index)))
            {
                ChangeCritterSpecies(index, CritterSpecies.Squid, preserveEnergy: false);
                continue;
            }

            var isStranded = !CanCritterRemainOnTile(index, GetIndex(_positions[index]));
            var nutrition = CritterNutritions.Get(species);

            var metabolized = nutrition.HasMetabolism && Tick >= _nextMetabolismTicks[index];
            if (metabolized)
            {
                AdvanceSchedule(ref _nextMetabolismTicks[index], nutrition.MetabolismIntervalTicks);
                _energy[index] = Math.Max(0, _energy[index] - nutrition.MetabolismCost);
                if (!isStranded && species is CritterSpecies.MegaSpider)
                {
                    TryFeedMegaSpiderFromWeb(index, nutrition);
                }
                if (!isStranded && species is CritterSpecies.Ape)
                {
                    TryFeedApeFromVillage(index, nutrition);
                }
            }

            DrainPlagueEnergy(index);
            if (_energy[index] == 0)
            {
                if (TryReanimateApe(index))
                {
                    continue;
                }
                DepositTileNutrition(GetIndex(_positions[index]));
                (deaths ??= []).Add(_critterIds[index]);
                continue;
            }

            if (isStranded || IsCaughtInMegaSpiderWeb(index))
            {
                continue;
            }

            if (species is CritterSpecies.Plankton && IsPlanktonFeedingTick(index))
            {
                TryFeedFromTerrain(index);
            }

            if (!CanSpeciesReproduce(species) ||
                _energy[index] < nutrition.ReproductionThreshold)
            {
                continue;
            }

            if (species is CritterSpecies.Wolf)
            {
                reservedBirthTiles ??= [];
                var wolfBirthPosition = TryReproduceWolfAtDen(
                    index,
                    nutrition,
                    reservedBirthTiles);
                if (wolfBirthPosition is not null)
                {
                    reservedBirthTiles.Add(wolfBirthPosition.Value);
                    (births ??= []).Add((
                        CritterSpecies.Wolf,
                        wolfBirthPosition.Value,
                        CritterSpecies.Wolf,
                        -1));
                }
                continue;
            }

            if (species is CritterSpecies.MegaSpider &&
                (!_megaSpiderWebHomes.TryGetValue(_critterIds[index].Value, out var webTile) ||
                    !_megaSpiderWebFood.ContainsKey(webTile)))
            {
                if (TryCreateMegaSpiderWeb(index))
                {
                    _energy[index] -= nutrition.ReproductionCost;
                }
                continue;
            }

            if (IsLivingApe(species) &&
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
            if (IsLivingApe(species))
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
            foreach (var critterId in deaths)
            {
                RemoveCritter(critterId);
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

    private GridPosition? TryReproduceWolfAtDen(
        int wolfIndex,
        CritterNutrition nutrition,
        IReadOnlySet<GridPosition> reservedBirthTiles)
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
            return null;
        }

        var denPosition = new GridPosition(denTile % Width, denTile / Width);
        if (_wolfDenCharges.TryGetValue(denTile, out var charges) &&
            charges >= MaximumWolfDenCharges)
        {
            var birthPosition = FindBirthPosition(
                wolfIndex,
                CritterSpecies.Wolf,
                reservedBirthTiles);
            if (birthPosition is null)
            {
                return null;
            }

            CompleteWolfDenAssignment(wolfId, denTile);
            _energy[wolfIndex] -= nutrition.ReproductionCost;
            return birthPosition;
        }

        if (!AddWolfDenCharge(denPosition))
        {
            if (!_wolfDenCharges.ContainsKey(denTile))
            {
                _wolfDenTargets.Remove(wolfId);
            }
            return null;
        }

        CompleteWolfDenAssignment(wolfId, denTile);
        _energy[wolfIndex] -= nutrition.ReproductionCost;
        return null;
    }

    private void CompleteWolfDenAssignment(int wolfId, int denTile)
    {
        _wolfDenHomes[wolfId] = denTile;
        if (_wolfDenTargets.Remove(wolfId, out var completedTarget))
        {
            RemoveWolfDenIfEmptyAndUnassociated(completedTarget);
        }
    }

    private bool TryPrepareApeReproduction(int apeIndex, CritterNutrition nutrition)
    {
        var apeId = _critterIds[apeIndex].Value;
        if (_apeSettlerTargets.ContainsKey(apeId))
        {
            return false;
        }

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

        SetApeStructure(villageTile, ApeStructureKind.Village);
        _apeVillageFood[villageTile] = 0;
        _apeVillageWood[villageTile] = ApeInitialWood;
        _apeVillageIds[villageTile] = _nextApeVillageId++;
        _apeVillageHomes[apeId] = villageTile;
        _apeVillageTargets.Remove(apeId);
        _energy[apeIndex] -= nutrition.ReproductionCost;
        return false;
    }

    private bool TryFeedApeFromVillage(int apeIndex, CritterNutrition nutrition)
    {
        if (_energy[apeIndex] > nutrition.HungryThreshold ||
            !_apeVillageHomes.TryGetValue(_critterIds[apeIndex].Value, out var villageTile) ||
            !_apeVillageFood.TryGetValue(villageTile, out var food) ||
            food <= 0)
        {
            return false;
        }

        _apeVillageFood[villageTile] = food - 1;
        _energy[apeIndex] = Math.Min(nutrition.MaximumEnergy, _energy[apeIndex] + 1);
        return true;
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
        var bestFoodVillage = -1;
        var bestFoodDistance = int.MaxValue;
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
                if (IsApeFoodDistrict(auxiliaryKind) && distance < bestFoodDistance)
                {
                    bestFoodVillage = villageTile;
                    bestFoodDistance = distance;
                }
                else if (auxiliaryKind is ApeStructureKind.NavalDistrict && distance < bestHarborDistance)
                {
                    bestHarborVillage = villageTile;
                    bestHarborDistance = distance;
                }
            }
        }
        return bestFoodVillage >= 0 ? bestFoodVillage : bestHarborVillage;
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
            HasBlockingApeStructure(villageTile) || _wolfDenCharges.ContainsKey(villageTile) ||
            _megaSpiderWebFood.ContainsKey(villageTile) ||
            _teleporters.Contains(villageTile) ||
            (_occupants[villageTile] >= 0 && _occupants[villageTile] != apeIndex) ||
            !CanBuildApeStructureOn(villageTile, ApeStructureKind.Village) ||
            !CanLiveOn(CritterSpecies.Ape, villageTile) || !IsFarEnoughFromApeVillages(villageTile))
        {
            return false;
        }

        var village = new GridPosition(villageTile % Width, villageTile / Width);
        foreach (var desiredKind in new[]
        {
            ApeStructureKind.Farm,
            ApeStructureKind.RicePaddy,
            ApeStructureKind.Orchard,
            ApeStructureKind.Aquaculture,
            ApeStructureKind.NavalDistrict,
        })
        {
            var directions = desiredKind is ApeStructureKind.NavalDistrict
                ? MovementDirections : CardinalDirections;
            foreach (var direction in directions)
            {
                var y = village.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(village.X + direction.X, Width), y);
                var candidateTile = GetIndex(candidate);
                if (_occupants[candidateTile] >= 0 || HasBlockingApeStructure(candidateTile) ||
                    _teleporters.Contains(candidateTile) ||
                    _wolfDenCharges.ContainsKey(candidateTile) ||
                    _megaSpiderWebFood.ContainsKey(candidateTile) ||
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

    private bool HasBlockingApeStructure(int tileIndex) =>
        _apeStructures.TryGetValue(tileIndex, out var structure) &&
        structure is not ApeStructureKind.Ruin;

    private void SetApeStructure(int tileIndex, ApeStructureKind structure)
    {
        _apeRuinDecayTicks.Remove(tileIndex);
        _apeResidentialUnderusedSinceTicks.Remove(tileIndex);
        _apeStructures[tileIndex] = structure;
    }

    private bool IsValidApeAuxiliaryTile(int tileIndex, ApeStructureKind kind) =>
        CanBuildApeStructureOn(tileIndex, kind) &&
        _surfaceCovers[tileIndex] is SurfaceCover.None && kind switch
        {
            ApeStructureKind.Farm =>
                _terrain[tileIndex] is not Terrain.Beach &&
                _biomes[tileIndex] is Biome.Grassland or Biome.Arid &&
                CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.RicePaddy =>
                _terrain[tileIndex] is not Terrain.Beach &&
                _biomes[tileIndex] is Biome.Swamp && CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.Orchard =>
                _terrain[tileIndex] is not Terrain.Beach &&
                _biomes[tileIndex] is Biome.Forest && CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.Aquaculture => IsApeAquacultureTile(tileIndex),
            ApeStructureKind.LumberCamp =>
                _biomes[tileIndex] is
                    Biome.Forest or Biome.Jungle or Biome.Taiga or Biome.Swamp or Biome.Grassland or Biome.Arid &&
                CanLiveOn(CritterSpecies.Ape, tileIndex),
            ApeStructureKind.NavalDistrict => IsApeHarborTile(tileIndex),
            ApeStructureKind.MilitaryDistrict =>
                CanLiveOn(CritterSpecies.Ape, tileIndex) &&
                _terrain[tileIndex] is not Terrain.Shallows,
            ApeStructureKind.ResidentialDistrict =>
                CanLiveOn(CritterSpecies.Ape, tileIndex) &&
                _terrain[tileIndex] is not Terrain.Shallows,
            _ => false,
        };

    private bool IsApeFoodDistrictStructurallyValid(
        int tileIndex,
        ApeStructureKind kind) =>
        CanBuildApeStructureOn(tileIndex, kind) &&
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (kind is ApeStructureKind.Aquaculture
            ? IsApeAquacultureTile(tileIndex)
            : _terrain[tileIndex] is not Terrain.Beach &&
                CanLiveOn(CritterSpecies.Ape, tileIndex));

    private bool IsApeAquacultureTile(int tileIndex) =>
        _terrain[tileIndex] is Terrain.Shallows ||
        _surfaceWater[tileIndex] is SurfaceWaterKind.FreshwaterLake;

    private bool CanBuildApeStructureOn(int tileIndex, ApeStructureKind kind) =>
        kind is ApeStructureKind.NavalDistrict ||
        _surfaceWater[tileIndex] is not SurfaceWaterKind.River;

    private bool IsApeHarborTile(int tileIndex)
    {
        if (_terrain[tileIndex] is Terrain.Beach)
        {
            return true;
        }
        if (_surfaceWater[tileIndex] is not
            (SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake))
        {
            return false;
        }

        var position = GetPosition(tileIndex);
        foreach (var direction in MovementDirections)
        {
            var y = position.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var neighborTile = GetIndex(new GridPosition(Mod(position.X + direction.X, Width), y));
            if (_surfaceWater[neighborTile] is SurfaceWaterKind.None &&
                CanLiveOn(CritterSpecies.Ape, neighborTile))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsApeFoodDistrictActive(int tileIndex, ApeStructureKind kind) =>
        IsApeFoodDistrictStructurallyValid(tileIndex, kind) && kind switch
        {
            ApeStructureKind.Farm => _biomes[tileIndex] is Biome.Grassland or Biome.Arid,
            ApeStructureKind.RicePaddy => _biomes[tileIndex] is Biome.Swamp,
            ApeStructureKind.Orchard => _biomes[tileIndex] is Biome.Forest,
            ApeStructureKind.Aquaculture => IsApeAquacultureTile(tileIndex),
            _ => false,
        };

    public bool IsApeStructureOperational(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _apeStructures.TryGetValue(tileIndex, out var structure) &&
            structure is not ApeStructureKind.Ruin &&
            (!IsApeFoodDistrict(structure) || IsApeFoodDistrictActive(tileIndex, structure));
    }

    public double? GetApeStructureProductionPerMinute(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (!_apeStructures.TryGetValue(tileIndex, out var structure))
        {
            return null;
        }

        var interval = structure switch
        {
            ApeStructureKind.Farm or ApeStructureKind.RicePaddy or ApeStructureKind.Orchard or
                ApeStructureKind.Aquaculture =>
                GetApeFoodProductionIntervalTicks(tileIndex, structure),
            ApeStructureKind.LumberCamp =>
                GetApeLumberProductionIntervalTicks(_biomes[tileIndex]),
            _ => 0,
        };
        return interval > 0 ? 60d * TicksPerSecond / interval : null;
    }

    private int GetApeVillageResidentCountByTile(int villageTile) =>
        _apeVillageHomes.Count(pair => pair.Value == villageTile);

    private int GetApeVillagePopulationCapacityByTile(int villageTile) =>
        ApeVillageBasePopulationCapacity +
        ApeResidentialPopulationCapacity * _apeAuxiliaryVillages.Count(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) &&
            structure is ApeStructureKind.ResidentialDistrict);

    private int GetApeVillageFoodCapacityByTile(int villageTile) =>
        ApeVillageBaseFoodCapacity +
        ApeFoodDistrictStorageCapacity * GetApeFoodDistrictCount(villageTile);

    private void AddApeVillageFood(int villageTile, int amount)
    {
        _apeVillageFood.TryGetValue(villageTile, out var food);
        _apeVillageFood[villageTile] = Math.Min(
            GetApeVillageFoodCapacityByTile(villageTile),
            food + amount);
    }

    private void ClampApeVillageFood(int villageTile)
    {
        if (_apeVillageFood.TryGetValue(villageTile, out var food))
        {
            _apeVillageFood[villageTile] = Math.Min(
                food,
                GetApeVillageFoodCapacityByTile(villageTile));
        }
    }

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
            if (population == 0)
            {
                RemoveAbandonedApeVillage(villageTile);
                continue;
            }

            if (TryLaunchLoneApeSailorColonist(villageTile, population))
            {
                RemoveAbandonedApeVillage(villageTile);
                continue;
            }

            TryRecruitApeChieftain(villageTile);

            TrySendApeSettler(villageTile, population);
            var infrastructureLimit = GetApeInfrastructureLimitForPopulation(population);

            if (population >= ApeFoodDistrictPopulationThreshold &&
                !HasApeFoodDistrict(villageTile) &&
                !HasApeStructure(villageTile, ApeStructureKind.NavalDistrict))
            {
                if (!TryBuildApeFoodDistrict(villageTile))
                {
                    TryPurchaseApeStructure(villageTile, ApeStructureKind.NavalDistrict);
                }
            }

            if (population >= ApeFoodDistrictPopulationThreshold &&
                population < GetApeVillagePopulationCapacityByTile(villageTile) &&
                GetApeStructureCount(villageTile, ApeStructureKind.LumberCamp) < infrastructureLimit)
            {
                TryPurchaseApeStructure(villageTile, ApeStructureKind.LumberCamp);
            }

            // A settlement that began inland can add one harbor later when its
            // connected district network finally reaches an open harbor tile.
            if (population >= ApeFoodDistrictPopulationThreshold &&
                GetApeStructureCount(villageTile, ApeStructureKind.NavalDistrict) < infrastructureLimit)
            {
                TryPurchaseApeStructure(villageTile, ApeStructureKind.NavalDistrict);
            }

            if (GetApeStructureCount(villageTile, ApeStructureKind.MilitaryDistrict) <
                GetApeMilitaryDistrictLimitForPopulation(population))
            {
                TryPurchaseApeStructure(villageTile, ApeStructureKind.MilitaryDistrict);
            }

            RemoveLongInactiveApeFoodDistricts(villageTile);

            foreach (var structureTile in _apeAuxiliaryVillages
                .Where(pair => pair.Value == villageTile)
                .Select(pair => pair.Key)
                .ToArray())
            {
                if (!_apeStructures.TryGetValue(structureTile, out var structure))
                {
                    continue;
                }
                if (IsApeFoodDistrict(structure))
                {
                    AdvanceApeFoodDistrict(villageTile, structureTile, structure);
                }
                else if (structure is ApeStructureKind.LumberCamp)
                {
                    AdvanceApeLumberCamp(villageTile, structureTile);
                }
                else if (structure is ApeStructureKind.NavalDistrict)
                {
                    AdvanceApeHarborRecruitment(villageTile, structureTile);
                }
                else if (structure is ApeStructureKind.MilitaryDistrict)
                {
                    AdvanceApeMilitaryRecruitment(villageTile, structureTile);
                }
            }

            population = GetApeVillageResidentCountByTile(villageTile);
            UpdateUnderusedApeResidentialDistricts(villageTile, population);
            if (population >= GetApeVillagePopulationCapacityByTile(villageTile))
            {
                TryBuildNextApeVillageExpansion(villageTile);
            }
        }
    }

    private void RemoveAbandonedApeVillage(int villageTile)
    {
        var abandonedTiles = _apeAuxiliaryVillages
            .Where(pair => pair.Value == villageTile)
            .Select(pair => pair.Key)
            .Append(villageTile)
            .ToArray();
        var villagePosition = new GridPosition(villageTile % Width, villageTile / Width);
        if (!RemoveApeStructureAt(villagePosition))
        {
            return;
        }

        foreach (var abandonedTile in abandonedTiles)
        {
            AddApeRuin(abandonedTile);
        }
    }

    private void TrySendApeSettler(int villageTile, int population)
    {
        if (population < ApeSettlerPopulationThreshold ||
            Tick == 0 || Tick % ApeSettlerCheckIntervalTicks != 0 ||
            NextInt(100) >= ApeSettlerChancePercent ||
            !_apeVillageFood.TryGetValue(villageTile, out var food) || food < ApeSettlerFoodCost)
        {
            return;
        }

        TryCreateApeSettler(villageTile, food);
    }

    public bool TrySendApeColonist(GridPosition position)
    {
        var clickedTile = GetIndex(position);
        if (_apeStructures.TryGetValue(clickedTile, out var structure) &&
            structure is ApeStructureKind.Village)
        {
            _apeVillageFood.TryGetValue(clickedTile, out var villageFood);
            return TryCreateApeSettler(
                clickedTile,
                villageFood,
                allowInsufficientFood: true);
        }

        var villageTile = FindNearestApeVillage(clickedTile);
        if (villageTile < 0 ||
            !TryGetApeVillageAuxiliarySite(-1, clickedTile, out _, out _))
        {
            return false;
        }
        _apeVillageFood.TryGetValue(villageTile, out var food);
        return TryCreateApeSettler(
            villageTile,
            food,
            allowInsufficientFood: true,
            requestedTargetVillageTile: clickedTile);
    }

    private int FindNearestApeVillage(int targetTile)
    {
        var target = GetPosition(targetTile);
        var nearestVillage = -1;
        var nearestDistance = int.MaxValue;
        foreach (var pair in _apeStructures)
        {
            if (pair.Value is not ApeStructureKind.Village)
            {
                continue;
            }

            var distance = WrappedManhattanDistance(GetPosition(pair.Key), target);
            if (distance < nearestDistance ||
                distance == nearestDistance && pair.Key < nearestVillage)
            {
                nearestVillage = pair.Key;
                nearestDistance = distance;
            }
        }
        return nearestVillage;
    }

    private bool TryCreateApeSettler(
        int villageTile,
        int food,
        bool allowInsufficientFood = false,
        int requestedTargetVillageTile = -1)
    {
        if (!allowInsufficientFood && food < ApeSettlerFoodCost)
        {
            return false;
        }

        var targetVillageTile = requestedTargetVillageTile >= 0
            ? requestedTargetVillageTile
            : FindDistantApeVillageSite(villageTile);
        var spawnTile = targetVillageTile >= 0 ? FindApeSettlerSpawnTile(villageTile) : -1;
        if (spawnTile < 0)
        {
            return false;
        }

        var settler = AddCritter(CritterSpecies.Ape, GetPosition(spawnTile));
        var settlerIndex = _critterIndicesById[settler.Value];
        _energy[settlerIndex] = ApeSettlerInitialEnergy;
        _apeSettlerTargets[settler.Value] = (villageTile, targetVillageTile);
        _apeVillageFood[villageTile] = Math.Max(0, food - ApeSettlerFoodCost);
        return true;
    }

    private int FindDistantApeVillageSite(int originVillageTile)
    {
        var origin = GetPosition(originVillageTile);
        var minimumDistance = Math.Min(
            ApeSettlerMinimumDistance,
            Math.Max(ApeVillageMinimumDistance + 1, (Width + Height) / 4));
        var selectedTile = -1;
        var candidates = 0;
        for (var tileIndex = 0; tileIndex < _terrain.Length; tileIndex++)
        {
            if (WrappedManhattanDistance(GetPosition(tileIndex), origin) < minimumDistance ||
                !TryGetApeVillageAuxiliarySite(-1, tileIndex, out _, out _))
            {
                continue;
            }

            if (NextInt(++candidates) == 0)
            {
                selectedTile = tileIndex;
            }
        }
        return selectedTile;
    }

    private int FindApeSettlerSpawnTile(int villageTile)
    {
        foreach (var structureTile in EnumerateConnectedApeStructureTiles(villageTile))
        {
            var structure = GetPosition(structureTile);
            foreach (var direction in MovementDirections)
            {
                var y = structure.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                var tileIndex = GetIndex(new GridPosition(Mod(structure.X + direction.X, Width), y));
                if (_occupants[tileIndex] < 0 && !_apeStructures.ContainsKey(tileIndex) &&
                    CanLiveOn(CritterSpecies.Ape, tileIndex))
                {
                    return tileIndex;
                }
            }
        }
        return -1;
    }

    private bool TryBuildNextApeVillageExpansion(int villageTile)
    {
        foreach (var kind in GetApeVillageExpansionCandidates(villageTile))
        {
            if (TryPurchaseApeStructure(villageTile, kind))
            {
                return true;
            }
        }
        return false;
    }

    private IEnumerable<ApeStructureKind> GetApeVillageExpansionCandidates(int villageTile)
    {
        var foodDistricts = GetApeFoodDistrictCount(villageTile);
        var homes = GetApeStructureCount(villageTile, ApeStructureKind.ResidentialDistrict);
        if (foodDistricts <= homes)
        {
            yield return ApeStructureKind.Farm;
            yield return ApeStructureKind.RicePaddy;
            yield return ApeStructureKind.Orchard;
            yield return ApeStructureKind.Aquaculture;
            yield return ApeStructureKind.ResidentialDistrict;
        }
        else
        {
            yield return ApeStructureKind.ResidentialDistrict;
        }
    }

    internal static int GetApeStructureFoodCost(ApeStructureKind kind) => kind switch
    {
        ApeStructureKind.LumberCamp => ApeLumberCampFoodCost,
        ApeStructureKind.ResidentialDistrict => ApeResidentialFoodCost,
        _ => 0,
    };

    internal static int GetApeStructureWoodCost(ApeStructureKind kind) => kind switch
    {
        ApeStructureKind.Farm or ApeStructureKind.RicePaddy or ApeStructureKind.Orchard or
            ApeStructureKind.Aquaculture =>
            ApeFoodDistrictWoodCost,
        ApeStructureKind.ResidentialDistrict => ApeResidentialWoodCost,
        ApeStructureKind.NavalDistrict => ApeHarborWoodCost,
        ApeStructureKind.MilitaryDistrict => ApeMilitaryDistrictWoodCost,
        _ => 0,
    };

    private int GetApeStructureCount(int villageTile, ApeStructureKind kind) =>
        _apeAuxiliaryVillages.Count(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) && structure == kind);

    private bool HasApeStructure(int villageTile, ApeStructureKind kind) =>
        GetApeStructureCount(villageTile, kind) > 0;

    internal static int GetApeMilitaryDistrictLimitForPopulation(int population) =>
        Math.Max(0, population) / ApeMilitaryDistrictPopulationInterval;

    internal static int GetApeInfrastructureLimitForPopulation(int population) =>
        1 + Math.Max(0, population) / ApeInfrastructurePopulationInterval;

    private int GetApeFoodDistrictCount(int villageTile) =>
        _apeAuxiliaryVillages.Count(pair =>
            pair.Value == villageTile &&
            _apeStructures.TryGetValue(pair.Key, out var structure) &&
            IsApeFoodDistrict(structure));

    private bool HasApeFoodDistrict(int villageTile) =>
        GetApeFoodDistrictCount(villageTile) > 0;

    private bool TryBuildApeFoodDistrict(int villageTile) =>
        TryBuildApeStructure(villageTile, ApeStructureKind.Farm) ||
        TryBuildApeStructure(villageTile, ApeStructureKind.RicePaddy) ||
        TryBuildApeStructure(villageTile, ApeStructureKind.Orchard) ||
        TryBuildApeStructure(villageTile, ApeStructureKind.Aquaculture);

    private static bool IsApeFoodDistrict(ApeStructureKind kind) =>
        kind is ApeStructureKind.Farm or ApeStructureKind.RicePaddy or ApeStructureKind.Orchard or
            ApeStructureKind.Aquaculture;

    private bool TryPurchaseApeStructure(int villageTile, ApeStructureKind kind)
    {
        _apeVillageFood.TryGetValue(villageTile, out var food);
        _apeVillageWood.TryGetValue(villageTile, out var wood);
        var foodCost = GetApeStructureFoodCost(kind);
        var woodCost = GetApeStructureWoodCost(kind);
        if (food < foodCost || wood < woodCost ||
            !TryBuildApeStructure(villageTile, kind))
        {
            return false;
        }

        _apeVillageFood[villageTile] = food - foodCost;
        _apeVillageWood[villageTile] = wood - woodCost;
        return true;
    }

    internal bool TryBuildApeStructure(int villageTile, ApeStructureKind kind)
    {
        if (kind is ApeStructureKind.Village or ApeStructureKind.Ruin)
        {
            return false;
        }
        var constructionTile = FindApeConstructionSite(villageTile, kind);
        if (constructionTile < 0)
        {
            return false;
        }

        SetApeStructure(constructionTile, kind);
        _apeAuxiliaryVillages[constructionTile] = villageTile;
        if (IsApeFoodDistrict(kind))
        {
            _apeStructureNextActionTicks[constructionTile] = Tick +
                GetApeFoodProductionIntervalTicks(constructionTile, kind);
        }
        else if (kind is ApeStructureKind.LumberCamp)
        {
            _apeStructureNextActionTicks[constructionTile] =
                Tick + GetApeLumberProductionIntervalTicks(_biomes[constructionTile]);
        }
        else if (kind is ApeStructureKind.NavalDistrict)
        {
            _apeStructureNextActionTicks[constructionTile] =
                Tick + ApeSailorRecruitmentIntervalTicks;
        }
        else if (kind is ApeStructureKind.MilitaryDistrict)
        {
            _apeStructureNextActionTicks[constructionTile] =
                Tick + ApeWarriorRecruitmentIntervalTicks;
        }
        return true;
    }

    private int FindApeConstructionSite(int villageTile, ApeStructureKind kind)
    {
        var candidates = new HashSet<int>();
        foreach (var structureTile in EnumerateConnectedApeStructureTiles(villageTile).ToArray())
        {
            var structurePosition = new GridPosition(structureTile % Width, structureTile / Width);
            var directions = kind is ApeStructureKind.NavalDistrict
                ? MovementDirections : CardinalDirections;
            foreach (var direction in directions)
            {
                var y = structurePosition.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(structurePosition.X + direction.X, Width), y);
                var candidateTile = GetIndex(candidate);
                if (_occupants[candidateTile] < 0 && !HasBlockingApeStructure(candidateTile) &&
                    !_teleporters.Contains(candidateTile) &&
                    !_wolfDenCharges.ContainsKey(candidateTile) &&
                    !_megaSpiderWebFood.ContainsKey(candidateTile) &&
                    IsValidApeAuxiliaryTile(candidateTile, kind))
                {
                    candidates.Add(candidateTile);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return -1;
        }

        var villagePosition = GetPosition(villageTile);
        var nearbyCandidates = candidates
            .Where(tile => WrappedManhattanDistance(GetPosition(tile), villagePosition) <= ApeVillageClusterRadius)
            .OrderBy(tile => tile)
            .ToArray();
        var selectedCandidates = nearbyCandidates.Length > 0 &&
            NextInt(100) < ApeVillageClusterChancePercent
            ? nearbyCandidates
            : candidates.OrderBy(tile => tile).ToArray();
        return selectedCandidates[NextInt(selectedCandidates.Length)];
    }

    private void RemoveLongInactiveApeFoodDistricts(int villageTile)
    {
        foreach (var structureTile in _apeAuxiliaryVillages
            .Where(pair => pair.Value == villageTile)
            .Select(pair => pair.Key)
            .ToArray())
        {
            if (!_apeStructures.TryGetValue(structureTile, out var structure) ||
                !IsApeFoodDistrict(structure))
            {
                continue;
            }

            if (IsApeFoodDistrictActive(structureTile, structure))
            {
                _apeFoodDistrictInactiveSinceTicks.Remove(structureTile);
                continue;
            }

            if (!_apeFoodDistrictInactiveSinceTicks.TryGetValue(structureTile, out var inactiveSince))
            {
                _apeFoodDistrictInactiveSinceTicks[structureTile] = Tick;
            }
            else if (Tick - inactiveSince > 2L * SeasonSystem.TicksPerYear)
            {
                ConvertApeStructureToRuin(structureTile);
            }
        }
    }

    private bool TryLaunchLoneApeSailorColonist(int villageTile, int population)
    {
        var sailorId = population == 1
            ? _apeVillageHomes
                .Where(pair => pair.Value == villageTile &&
                    _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
                    _species[residentIndex] is CritterSpecies.ApeSailor)
                .Select(pair => pair.Key)
                .FirstOrDefault()
            : 0;
        if (sailorId == 0)
        {
            _loneApeSailorSinceTicks.Remove(villageTile);
            return false;
        }

        if (!_loneApeSailorSinceTicks.TryGetValue(villageTile, out var aloneSince))
        {
            _loneApeSailorSinceTicks[villageTile] = Tick;
            return false;
        }
        if (Tick - aloneSince < LoneApeSailorColonistDelayTicks)
        {
            return false;
        }

        var targetVillageTile = FindDistantApeVillageSite(villageTile);
        if (targetVillageTile < 0)
        {
            _loneApeSailorSinceTicks[villageTile] = Tick;
            return false;
        }

        var sailorIndex = _critterIndicesById[sailorId];
        ChangeCritterSpecies(sailorIndex, CritterSpecies.Ape, preserveEnergy: false);
        _energy[sailorIndex] = ApeSettlerInitialEnergy;
        _apeSettlerTargets[sailorId] = (villageTile, targetVillageTile);
        _loneApeSailorSinceTicks.Remove(villageTile);
        return true;
    }

    private void UpdateUnderusedApeResidentialDistricts(int villageTile, int population)
    {
        var villagePosition = GetPosition(villageTile);
        var districts = _apeAuxiliaryVillages
            .Where(pair => pair.Value == villageTile &&
                _apeStructures.TryGetValue(pair.Key, out var structure) &&
                structure is ApeStructureKind.ResidentialDistrict)
            .Select(pair => pair.Key)
            .OrderBy(tile => WrappedManhattanDistance(GetPosition(tile), villagePosition))
            .ThenBy(tile => tile)
            .ToArray();
        var requiredDistricts = Math.Clamp(
            (population - ApeVillageBasePopulationCapacity + ApeResidentialPopulationCapacity - 1) /
                ApeResidentialPopulationCapacity,
            0,
            districts.Length);

        for (var index = 0; index < requiredDistricts; index++)
        {
            _apeResidentialUnderusedSinceTicks.Remove(districts[index]);
        }

        for (var index = requiredDistricts; index < districts.Length; index++)
        {
            var districtTile = districts[index];
            if (!_apeResidentialUnderusedSinceTicks.TryGetValue(districtTile, out var underusedSince))
            {
                _apeResidentialUnderusedSinceTicks[districtTile] = Tick;
            }
            else if (Tick - underusedSince >= ApeResidentialUnderuseTicks)
            {
                ConvertApeStructureToRuin(districtTile);
            }
        }
    }

    private void ConvertApeStructureToRuin(int tileIndex)
    {
        if (RemoveApeStructureAt(GetPosition(tileIndex)))
        {
            AddApeRuin(tileIndex);
        }
    }

    private void AddApeRuin(int tileIndex)
    {
        SetApeStructure(tileIndex, ApeStructureKind.Ruin);
        var decayTick = Tick + ApeRuinDecayTicks;
        _apeRuinDecayTicks[tileIndex] = decayTick;
        _nextApeRuinDecayTick = Math.Min(_nextApeRuinDecayTick, decayTick);
    }

    private void AdvanceApeRuins()
    {
        if (Tick < _nextApeRuinDecayTick)
        {
            return;
        }

        foreach (var tileIndex in _apeRuinDecayTicks
            .Where(pair => Tick >= pair.Value)
            .Select(pair => pair.Key)
            .ToArray())
        {
            if (_apeStructures.TryGetValue(tileIndex, out var structure) &&
                structure is ApeStructureKind.Ruin)
            {
                _apeStructures.Remove(tileIndex);
            }
            _apeRuinDecayTicks.Remove(tileIndex);
        }
        _nextApeRuinDecayTick = _apeRuinDecayTicks.Count == 0
            ? long.MaxValue
            : _apeRuinDecayTicks.Values.Min();
    }

    private void AdvanceApeFoodDistrict(
        int villageTile,
        int farmTile,
        ApeStructureKind structure)
    {
        if (!IsApeFoodDistrictActive(farmTile, structure))
        {
            _apeStructureNextActionTicks[farmTile] = Tick + ApeFarmFoodIntervalTicks;
            return;
        }

        var interval = GetApeFoodProductionIntervalTicks(farmTile, structure);
        if (!_apeStructureNextActionTicks.TryGetValue(farmTile, out var nextTick))
        {
            nextTick = Tick + interval;
        }
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[farmTile] = nextTick;
            return;
        }

        do
        {
            AddApeVillageFood(villageTile, 1);
            nextTick += interval;
        }
        while (nextTick <= Tick);
        _apeStructureNextActionTicks[farmTile] = nextTick;
    }

    private int GetApeFoodProductionIntervalTicks(
        int structureTile,
        ApeStructureKind structure) => structure switch
        {
            ApeStructureKind.Farm when _biomes[structureTile] is Biome.Arid =>
                2 * ApeFarmFoodIntervalTicks,
            ApeStructureKind.Aquaculture => GetApeAquacultureProductionIntervalTicks(
                ClimateSystem.ClassifyTemperature(_temperature[structureTile])),
            _ => ApeFarmFoodIntervalTicks,
        };

    internal static int GetApeAquacultureProductionIntervalTicks(TemperatureBand band) => band switch
    {
        TemperatureBand.Hot => 30 * TicksPerSecond,
        TemperatureBand.Temperate => 34 * TicksPerSecond,
        TemperatureBand.Cold => 38 * TicksPerSecond,
        TemperatureBand.Freezing => 44 * TicksPerSecond,
        _ => throw new ArgumentOutOfRangeException(nameof(band), band, null),
    };

    private void AdvanceApeLumberCamp(int villageTile, int lumberCampTile)
    {
        var interval = GetApeLumberProductionIntervalTicks(_biomes[lumberCampTile]);
        if (!_apeStructureNextActionTicks.TryGetValue(lumberCampTile, out var nextTick))
        {
            nextTick = Tick + interval;
        }
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[lumberCampTile] = nextTick;
            return;
        }

        do
        {
            _apeVillageWood.TryGetValue(villageTile, out var wood);
            _apeVillageWood[villageTile] = Math.Min(ApeMaximumWood, wood + 1);
            nextTick += interval;
        }
        while (nextTick <= Tick);
        _apeStructureNextActionTicks[lumberCampTile] = nextTick;
    }

    internal static int GetApeLumberProductionIntervalTicks(Biome biome) =>
        biome switch
        {
            Biome.Jungle => 10 * TicksPerSecond,
            Biome.Forest => 14 * TicksPerSecond,
            Biome.Taiga => 18 * TicksPerSecond,
            Biome.Swamp => 24 * TicksPerSecond,
            Biome.Grassland or Biome.Arid => 36 * TicksPerSecond,
            _ => 18 * TicksPerSecond,
        };

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
        var population = GetApeVillageResidentCountByTile(villageTile);
        var sailorLimit = GetApeSailorLimitForPopulation(population, harborCount);
        if (sailorCount >= sailorLimit)
        {
            return false;
        }

        _apeVillageWood.TryGetValue(villageTile, out var wood);
        var woodCost = sailorCount == 0 ? 0 : ApeSailorWoodCost;
        if (wood < woodCost)
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
        ChangeCritterSpecies(
            recruitIndex,
            CritterSpecies.ApeSailor,
            preserveEnergy: true,
            preserveApeVillage: true);
        MoveCritter(recruitIndex, harborTile, harbor);
        _apeVillageWood[villageTile] = wood - woodCost;
        return true;
    }

    internal static int GetApeSailorLimitForPopulation(int population) =>
        GetApeSailorLimitForPopulation(population, harborCount: 1);

    internal static int GetApeSailorLimitForPopulation(int population, int harborCount) =>
        Math.Min(
            ApeSailorsPerHarbor * Math.Max(0, harborCount),
            Math.Max(0, population) / 5);

    private void AdvanceApeMilitaryRecruitment(int villageTile, int militaryDistrictTile)
    {
        if (!_apeStructureNextActionTicks.TryGetValue(militaryDistrictTile, out var nextTick))
        {
            nextTick = Tick + ApeWarriorRecruitmentIntervalTicks;
        }
        if (Tick < nextTick)
        {
            _apeStructureNextActionTicks[militaryDistrictTile] = nextTick;
            return;
        }

        do
        {
            nextTick += ApeWarriorRecruitmentIntervalTicks;
        }
        while (nextTick <= Tick);
        _apeStructureNextActionTicks[militaryDistrictTile] = nextTick;
        TryRecruitApeWarrior(villageTile, militaryDistrictTile);
    }

    private bool TryRecruitApeWarrior(int villageTile, int militaryDistrictTile)
    {
        if (_occupants[militaryDistrictTile] >= 0)
        {
            return false;
        }

        var militaryDistrictCount = GetApeStructureCount(
            villageTile,
            ApeStructureKind.MilitaryDistrict);
        var warriorCount = _apeVillageHomes.Count(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeWarrior);
        if (warriorCount >= GetApeWarriorLimitForMilitaryDistricts(militaryDistrictCount))
        {
            return false;
        }

        _apeVillageWood.TryGetValue(villageTile, out var wood);
        var woodCost = warriorCount < militaryDistrictCount ? 0 : ApeWarriorWoodCost;
        if (wood < woodCost)
        {
            return false;
        }

        var district = GetPosition(militaryDistrictTile);
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
                district))
            .ThenBy(id => id)
            .First();
        var recruitIndex = _critterIndicesById[recruitId];
        ChangeCritterSpecies(
            recruitIndex,
            CritterSpecies.ApeWarrior,
            preserveEnergy: true,
            preserveApeVillage: true);
        MoveCritter(recruitIndex, militaryDistrictTile, district);
        _apeVillageWood[villageTile] = wood - woodCost;
        return true;
    }

    private bool TryRecruitApeChieftain(int villageTile)
    {
        var hasChieftain = _apeVillageHomes.Any(pair =>
            pair.Value == villageTile &&
            _critterIndicesById.TryGetValue(pair.Key, out var residentIndex) &&
            _species[residentIndex] is CritterSpecies.ApeChieftain);
        if (hasChieftain)
        {
            return false;
        }

        var village = GetPosition(villageTile);
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
                village))
            .ThenBy(id => id)
            .First();
        ChangeCritterSpecies(
            _critterIndicesById[recruitId],
            CritterSpecies.ApeChieftain,
            preserveEnergy: true,
            preserveApeVillage: true);
        return true;
    }

    internal static int GetApeWarriorLimitForMilitaryDistricts(int militaryDistrictCount) =>
        ApeWarriorsPerMilitaryDistrict * Math.Max(0, militaryDistrictCount);

    internal static bool CanSpeciesReproduce(CritterSpecies species) =>
        CritterNutritions.Get(species).CanReproduce;

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
        _terrain[tileIndex] is not Terrain.Shallows &&
        (_occupants[tileIndex] < 0 || _occupants[tileIndex] == wolfIndex) &&
        CanLiveOn(CritterSpecies.Wolf, tileIndex);

    internal CritterSpecies ChooseOffspringSpecies(CritterSpecies parentSpecies)
    {
        if (parentSpecies is CritterSpecies.Squid)
        {
            return CritterSpecies.SquidEgg;
        }
        if (parentSpecies is CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or
            CritterSpecies.ApeChieftain)
        {
            return CritterSpecies.Ape;
        }

        var branchCount = CritterEvolution.GetEvolvedSpeciesCount(parentSpecies);
        var branchIndex = branchCount > 0 ? NextInt(branchCount) : 0;
        return CritterEvolution.ChooseOffspring(
            parentSpecies,
            NextInt(CritterEvolution.MaximumChanceSteps),
            CritterEvolution.GetOffspringEvolutionChanceSteps(
                parentSpecies,
                EvolutionChanceSteps),
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
        bool preserveEnergy,
        bool preserveApeVillage = false)
    {
        var currentSpecies = _species[critterIndex];
        _speciesCounts[(int)currentSpecies]--;
        _speciesCounts[(int)targetSpecies]++;
        if (PlanktonRecoveryEnabled && currentSpecies is CritterSpecies.Plankton &&
            targetSpecies is not CritterSpecies.Plankton &&
            GetCritterCount(CritterSpecies.Plankton) == 0)
        {
            _nextPlanktonRecoveryTick = Tick + PlanktonRecoveryIntervalTicks;
        }
        _species[critterIndex] = targetSpecies;
        var critterId = _critterIds[critterIndex].Value;
        if (!IsLivingApe(targetSpecies))
        {
            _plagues.Remove(critterId);
        }
        DetachWolfFromDens(critterId);
        DetachMegaSpiderFromWeb(critterId);
        if (!preserveApeVillage)
        {
            DetachApeFromVillage(critterId);
        }
        var nutrition = CritterNutritions.Get(targetSpecies);
        _energy[critterIndex] = preserveEnergy && nutrition.MaximumEnergy > 0
            ? Math.Clamp(_energy[critterIndex], 1, nutrition.MaximumEnergy)
            : nutrition.InitialEnergy;
        _nextMovementTicks[critterIndex] = GetFirstMovementTick(
            targetSpecies,
            _critterIds[critterIndex]);
        _nextMetabolismTicks[critterIndex] = nutrition.HasMetabolism
            ? Tick + nutrition.MetabolismIntervalTicks
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
                if (occupant >= 0 && CanPursuePreyAtDistance(
                    CritterSpecies.Squid,
                    _species[occupant],
                    Math.Abs(offsetX) + Math.Abs(offsetY)))
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
        IReadOnlySet<GridPosition>? reservedPrey = null,
        bool allowPredation = true)
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
                if (CanPursuePrey(critterIndex, preyIndex))
                {
                    if (allowPredation && reservedPrey?.Contains(candidate) is not true)
                    {
                        return candidate;
                    }
                    if (!allowPredation)
                    {
                        continue;
                    }
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

    private GridPosition? TryMoveStranded(int critterIndex)
    {
        var species = _species[critterIndex];
        var current = _positions[critterIndex];
        var startDirection = NextInt(MovementDirections.Length);

        // Escape immediately when valid habitat is directly reachable.
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
            if (_occupants[destinationIndex] < 0 && CanLiveOn(species, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return null;
            }
        }

        var recoveryTarget = FindNearestOpenHabitat(
            species,
            current,
            StrandedRecoverySearchRadius);
        GridPosition? bestStep = null;
        var bestDistance = int.MaxValue;
        var ties = 0;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                _occupants[GetIndex(candidate)] >= 0)
            {
                continue;
            }

            if (recoveryTarget is null)
            {
                bestStep = candidate;
                break;
            }

            var distance = WrappedManhattanDistance(candidate, recoveryTarget.Value);
            if (distance < bestDistance)
            {
                bestStep = candidate;
                bestDistance = distance;
                ties = 1;
            }
            else if (distance == bestDistance && NextInt(++ties) == 0)
            {
                bestStep = candidate;
            }
        }

        if (bestStep is not null)
        {
            MoveCritter(critterIndex, GetIndex(bestStep.Value), bestStep.Value);
        }
        return null;
    }

    private GridPosition? FindNearestOpenHabitat(
        CritterSpecies species,
        GridPosition origin,
        int searchRadius)
    {
        GridPosition? selected = null;
        var bestDistance = int.MaxValue;
        var ties = 0;
        for (var offsetY = -searchRadius; offsetY <= searchRadius; offsetY++)
        {
            var y = origin.Y + offsetY;
            if (y < 0 || y >= Height)
            {
                continue;
            }
            var horizontalReach = searchRadius - Math.Abs(offsetY);
            for (var offsetX = -horizontalReach; offsetX <= horizontalReach; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }
                var candidate = new GridPosition(Mod(origin.X + offsetX, Width), y);
                var candidateIndex = GetIndex(candidate);
                if (_occupants[candidateIndex] >= 0 || !CanLiveOn(species, candidateIndex))
                {
                    continue;
                }

                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (distance < bestDistance)
                {
                    selected = candidate;
                    bestDistance = distance;
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

    private GridPosition? TryMovePlankton(int critterIndex)
    {
        return TryMove(critterIndex);
    }

    private bool IsPlanktonFeedingTick(int critterIndex) =>
        Tick % PlanktonFeedingIntervalTicks ==
            _critterIds[critterIndex].Value % PlanktonFeedingIntervalTicks;

    private GridPosition? TryMoveSquidEgg(int critterIndex)
    {
        TryMove(critterIndex);
        if (_terrain[GetIndex(_positions[critterIndex])] is Terrain.Shallows)
        {
            ChangeCritterSpecies(critterIndex, CritterSpecies.Squid, preserveEnergy: false);
        }
        return null;
    }

    private GridPosition? TryMoveTrilobiteLike(int critterIndex)
    {
        var species = _species[critterIndex];
        if (TryFleePredators(critterIndex, TrilobitePredatorFleeRadius))
        {
            return null;
        }

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(species);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
            TryApproachNearbyFeedingTile(
                critterIndex,
                species,
                TrilobitePerceptionRadius,
                reservedPrey: null))
        {
            return null;
        }

        if (TryReturnToDeepSeaTerrain(critterIndex, species))
        {
            return null;
        }

        return TryMove(critterIndex);
    }

    private bool TryReturnToDeepSeaTerrain(int critterIndex, CritterSpecies species)
    {
        var current = _positions[critterIndex];
        if (IsPreferredDeepSeaTerrain(species, GetIndex(current)))
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
            if (IsPreferredDeepSeaTerrain(species, destinationIndex) &&
                CanLiveOn(species, destinationIndex) &&
                CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return true;
            }
        }

        return false;
    }

    private bool IsPreferredDeepSeaTerrain(CritterSpecies species, int tileIndex) =>
        species is CritterSpecies.Nautilus
            ? _terrain[tileIndex] is Terrain.Ocean || IsNautilusFeedingTile(tileIndex)
            : IsTrilobiteFeedingTile(tileIndex);

    private GridPosition? TryMoveHunter(
        int critterIndex,
        int perceptionRadius,
        IReadOnlySet<GridPosition>? reservedPrey,
        bool huntWhenFull = false)
    {
        var predatorSpecies = _species[critterIndex];
        if (!huntWhenFull && predatorSpecies is not CritterSpecies.UndeadApe &&
            _energy[critterIndex] >= CritterNutritions.Get(predatorSpecies).MaximumEnergy)
        {
            _preyTargets[critterIndex] = -1;
            return TryMove(critterIndex, reservedPrey);
        }

        var target = FindHunterPrey(
            critterIndex,
            predatorSpecies,
            perceptionRadius,
            reservedPrey);
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
                        CanTherapsidStrikeAdjacentLakePrey(critterIndex, preyIndex) ||
                        CanStrikeAdjacentFeederCrab(critterIndex, preyIndex)) &&
                    reservedPrey?.Contains(candidate) is not true)
                {
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

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Fish);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
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
        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Therapsid);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
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

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Nautilus);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
            TryApproachNearbyFeedingTile(
                critterIndex,
                CritterSpecies.Nautilus,
                NautilusPerceptionRadius,
                reservedPrey))
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
        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Crab);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
            TryApproachNearbyFeedingTile(
                critterIndex,
                CritterSpecies.Crab,
                CrabFeedingPerceptionRadius,
                reservedPrey: null))
        {
            return null;
        }

        return TryMove(critterIndex);
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
                    !CanShoveMovementBlocker(critterIndex, candidateIndex, reservedPrey) &&
                    !(species is CritterSpecies.Fish &&
                        CanShoveWorm(critterIndex, candidateIndex, reservedPrey))))
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
            !TryShoveMovementBlocker(critterIndex, destinationIndex, reservedPrey) &&
            !(species is CritterSpecies.Fish &&
                TryShoveWorm(critterIndex, destinationIndex, reservedPrey)))
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

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Monkey);
        if (_energy[critterIndex] < nutrition.MaximumEnergy)
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
        if (TryAdvanceApeSettler(critterIndex))
        {
            return null;
        }

        if (TryHandleApeSettlementReturn(critterIndex, reservedPrey, out var returnMovement))
        {
            return returnMovement;
        }

        if (TryFeedApeFoliage(critterIndex))
        {
            return null;
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

        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
            TryMoveToAdjacentApeFoliage(critterIndex))
        {
            return null;
        }

        if (!ShouldApeHunt(critterIndex))
        {
            _preyTargets[critterIndex] = -1;
            return TryMove(critterIndex, reservedPrey, allowPredation: false);
        }

        return TryMoveHunter(critterIndex, ApePerceptionRadius, reservedPrey);
    }

    private bool TryAdvanceApeSettler(int apeIndex)
    {
        var apeId = _critterIds[apeIndex].Value;
        if (!_apeSettlerTargets.TryGetValue(apeId, out var settler))
        {
            return false;
        }

        var targetVillageTile = settler.TargetVillageTile;
        if (!IsApeSettlerDestinationValid(apeIndex, targetVillageTile))
        {
            targetVillageTile = FindDistantApeVillageSite(settler.OriginVillageTile);
            if (targetVillageTile < 0)
            {
                _apeSettlerTargets.Remove(apeId);
                _apeSettlerPaths.Remove(apeId);
                return false;
            }
            _apeSettlerTargets[apeId] = (settler.OriginVillageTile, targetVillageTile);
        }

        if (GetIndex(_positions[apeIndex]) == targetVillageTile)
        {
            if (!TryGetApeVillageAuxiliarySite(apeIndex, targetVillageTile, out _, out _))
            {
                targetVillageTile = FindDistantApeVillageSite(settler.OriginVillageTile);
                if (targetVillageTile < 0)
                {
                    _apeSettlerTargets.Remove(apeId);
                    _apeSettlerPaths.Remove(apeId);
                    return false;
                }
                _apeSettlerTargets[apeId] = (settler.OriginVillageTile, targetVillageTile);
                return true;
            }

            SetApeStructure(targetVillageTile, ApeStructureKind.Village);
            _apeVillageFood[targetVillageTile] = 0;
            _apeVillageWood[targetVillageTile] = ApeInitialWood;
            _apeVillageIds[targetVillageTile] = _nextApeVillageId++;
            _apeVillageHomes[apeId] = targetVillageTile;
            _apeSettlerTargets.Remove(apeId);
            _apeSettlerPaths.Remove(apeId);
            var apeNutrition = CritterNutritions.Get(CritterSpecies.Ape);
            _energy[apeIndex] = apeNutrition.InitialEnergy;
            _nextMetabolismTicks[apeIndex] = Tick + apeNutrition.MetabolismIntervalTicks;
            SpawnApeColonistFoundingCompanion(apeIndex, targetVillageTile);
            return true;
        }

        MoveApeSettlerToward(apeIndex, targetVillageTile);
        return true;
    }

    private void SpawnApeColonistFoundingCompanion(int founderIndex, int villageTile)
    {
        var founderPosition = _positions[founderIndex];
        var startDirection = NextInt(MovementDirections.Length);
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var companionPosition = new GridPosition(
                Mod(founderPosition.X + direction.X, Width),
                founderPosition.Y + direction.Y);
            if (companionPosition.Y < 0 || companionPosition.Y >= Height)
            {
                continue;
            }

            var companionTile = GetIndex(companionPosition);
            if (_occupants[companionTile] >= 0 || _apeStructures.ContainsKey(companionTile) ||
                !CanLiveOn(CritterSpecies.Ape, companionTile))
            {
                continue;
            }

            var companionId = AddCritter(CritterSpecies.Ape, companionPosition);
            _apeVillageHomes[companionId.Value] = villageTile;
            _reproductionTruces[companionId.Value] = (
                CritterSpecies.Ape,
                Tick + ReproductionTruceTicks);
            return;
        }
    }

    private bool IsApeSettlerDestinationValid(int apeIndex, int villageTile) =>
        villageTile >= 0 && villageTile < _terrain.Length &&
        !HasBlockingApeStructure(villageTile) &&
        !_wolfDenCharges.ContainsKey(villageTile) &&
        !_megaSpiderWebFood.ContainsKey(villageTile) &&
        !_teleporters.Contains(villageTile) &&
        (_occupants[villageTile] < 0 || _occupants[villageTile] == apeIndex) &&
        CanBuildApeStructureOn(villageTile, ApeStructureKind.Village) &&
        CanLiveOn(CritterSpecies.Ape, villageTile) &&
        IsFarEnoughFromApeVillages(villageTile);

    private void MoveApeSettlerToward(int apeIndex, int targetVillageTile)
    {
        var apeId = _critterIds[apeIndex].Value;
        var currentTile = GetIndex(_positions[apeIndex]);
        if (!_apeSettlerPaths.TryGetValue(apeId, out var route) ||
            route.TargetVillageTile != targetVillageTile || route.Tiles.Count == 0 ||
            GetApeSettlerPathEstimate(currentTile, route.Tiles.Peek()) != 1 ||
            !IsApeSettlerTransitTile(route.Tiles.Peek()))
        {
            var path = FindApeSettlerPath(apeIndex, currentTile, targetVillageTile);
            if (path is null || path.Count == 0)
            {
                _apeSettlerPaths.Remove(apeId);
                return;
            }
            route = (targetVillageTile, path);
            _apeSettlerPaths[apeId] = route;
        }

        var destinationIndex = route.Tiles.Peek();
        if (_occupants[destinationIndex] >= 0 &&
            !TryShoveMovementBlocker(apeIndex, destinationIndex, reservedPrey: null))
        {
            _apeSettlerPaths.Remove(apeId);
            return;
        }
        route.Tiles.Dequeue();
        MoveCritter(apeIndex, destinationIndex, GetPosition(destinationIndex));
    }

    private Queue<int>? FindApeSettlerPath(int apeIndex, int startTile, int targetTile)
    {
        var cameFrom = new int[_terrain.Length];
        var stepsFromStart = new int[_terrain.Length];
        Array.Fill(cameFrom, -1);
        Array.Fill(stepsFromStart, int.MaxValue);
        stepsFromStart[startTile] = 0;

        var frontier = new PriorityQueue<(int Tile, int Steps), (int Estimate, int Order)>();
        var order = 0;
        frontier.Enqueue((startTile, 0), (GetApeSettlerPathEstimate(startTile, targetTile), order++));
        while (frontier.TryDequeue(out var node, out _))
        {
            if (node.Steps != stepsFromStart[node.Tile])
            {
                continue;
            }
            if (node.Tile == targetTile)
            {
                break;
            }

            var position = GetPosition(node.Tile);
            foreach (var direction in MovementDirections)
            {
                var y = position.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }
                var neighborTile = GetIndex(new GridPosition(Mod(position.X + direction.X, Width), y));
                var nextSteps = node.Steps + 1;
                if (!IsApeSettlerTransitTile(neighborTile) ||
                    (_occupants[neighborTile] >= 0 && _occupants[neighborTile] != apeIndex &&
                        !CanShoveMovementBlocker(apeIndex, neighborTile)) ||
                    nextSteps >= stepsFromStart[neighborTile])
                {
                    continue;
                }

                cameFrom[neighborTile] = node.Tile;
                stepsFromStart[neighborTile] = nextSteps;
                frontier.Enqueue(
                    (neighborTile, nextSteps),
                    (nextSteps + GetApeSettlerPathEstimate(neighborTile, targetTile), order++));
            }
        }

        if (cameFrom[targetTile] < 0)
        {
            return null;
        }

        var reversePath = new Stack<int>();
        for (var tile = targetTile; tile != startTile; tile = cameFrom[tile])
        {
            reversePath.Push(tile);
        }
        return new Queue<int>(reversePath);
    }

    private int GetApeSettlerPathEstimate(int firstTile, int secondTile)
    {
        var first = GetPosition(firstTile);
        var second = GetPosition(secondTile);
        var horizontal = Math.Abs(first.X - second.X);
        horizontal = Math.Min(horizontal, Width - horizontal);
        return Math.Max(horizontal, Math.Abs(first.Y - second.Y));
    }

    private bool ShouldApeHunt(int critterIndex)
    {
        var apeId = _critterIds[critterIndex].Value;
        return !_apeVillageHomes.TryGetValue(apeId, out var villageTile) ||
            !_apeVillageFood.TryGetValue(villageTile, out var villageFood) ||
            villageFood < ApeVillageHuntingFoodThreshold;
    }

    internal bool ShouldApeHunt(CritterId apeId) =>
        _critterIndicesById.TryGetValue(apeId.Value, out var critterIndex) &&
        _species[critterIndex] is CritterSpecies.Ape &&
        ShouldApeHunt(critterIndex);

    private bool TryFeedApeFoliage(int critterIndex)
    {
        var nutrition = CritterNutritions.Get(CritterSpecies.Ape);
        var tileIndex = GetIndex(_positions[critterIndex]);
        if (_energy[critterIndex] >= nutrition.MaximumEnergy ||
            !IsApeFoliageTile(tileIndex) ||
            !TryConsumeNaturalTileNutrition(tileIndex))
        {
            return false;
        }

        _energy[critterIndex]++;
        return true;
    }

    private bool TryMoveToAdjacentApeFoliage(int critterIndex)
    {
        var current = _positions[critterIndex];
        var hasAdjacentFoliage = false;
        foreach (var direction in MovementDirections)
        {
            var y = current.Y + direction.Y;
            if (y < 0 || y >= Height)
            {
                continue;
            }

            var tileIndex = y * Width + Mod(current.X + direction.X, Width);
            if (IsApeFoliageTile(tileIndex) && HasNaturalTileNutrition(tileIndex))
            {
                hasAdjacentFoliage = true;
                break;
            }
        }
        if (!hasAdjacentFoliage)
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
            if (IsApeFoliageTile(destinationIndex) &&
                HasNaturalTileNutrition(destinationIndex) &&
                CanEnterOrShoveMovementBlocker(critterIndex, destinationIndex))
            {
                MoveCritter(critterIndex, destinationIndex, candidate);
                return true;
            }
        }

        return false;
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
            _apeFoodReturnProgress.Remove(apeId);
            return false;
        }

        if (IsAtOrAdjacentToConnectedApeStructure(critterIndex, villageTile))
        {
            DepositApeCarriedFood(apeId, villageTile, carriedFood);
            return true;
        }

        var target = FindNearestConnectedApeStructure(critterIndex, villageTile);
        var distance = target < 0
            ? int.MaxValue
            : WrappedManhattanDistance(
                _positions[critterIndex],
                new GridPosition(target % Width, target / Width));
        if (!_apeFoodReturnProgress.TryGetValue(apeId, out var progress) ||
            distance < progress.BestDistance)
        {
            _apeFoodReturnProgress[apeId] = (distance, Tick);
        }
        else if (Tick - progress.LastProgressTick >= ApeFoodReturnStallTicks)
        {
            DepositApeCarriedFood(apeId, villageTile, carriedFood);
            return true;
        }

        if (target < 0)
        {
            return false;
        }

        movement = TryMoveTowardApeStructure(critterIndex, target, reservedPrey);
        return true;
    }

    private void DepositApeCarriedFood(int apeId, int villageTile, int carriedFood)
    {
        AddApeVillageFood(villageTile, carriedFood);
        _apeCarriedFood.Remove(apeId);
        _apeFoodReturnProgress.Remove(apeId);
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

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var species = _species[critterIndex];
        var nutrition = CritterNutritions.Get(species);
        if (_energy[critterIndex] < nutrition.MaximumEnergy)
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
                if (occupant >= 0 &&
                    CanEatInCurrentContext(occupant, critterIndex) &&
                    CanHuntPreyOnCurrentTile(occupant, critterIndex))
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

        if (TryFeedFromTerrain(critterIndex))
        {
            return null;
        }

        var nutrition = CritterNutritions.Get(CritterSpecies.Newt);
        if (_energy[critterIndex] < nutrition.MaximumEnergy &&
            TryApproachNearbyNewtFood(critterIndex))
        {
            return null;
        }

        return TryWanderNewt(critterIndex);
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
                if (!CanFeedFromEnvironment(CritterSpecies.Newt, GetIndex(candidate)))
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

    internal GridPosition? FindHunterPrey(
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
                var distance = Math.Abs(offsetX) + Math.Abs(offsetY);
                if (occupant < 0 || occupant == critterIndex ||
                    !CanPursuePrey(critterIndex, occupant) ||
                    !IsPreyPursuitAllowedAtDistance(
                        predatorSpecies,
                        _species[occupant],
                        WrappedManhattanDistance(current, candidate)) ||
                    (!CanLiveOn(predatorSpecies, candidateIndex) &&
                        !CanTherapsidStrikeAdjacentLakePrey(critterIndex, occupant) &&
                        !CanStrikeAdjacentFeederCrab(critterIndex, occupant)))
                {
                    continue;
                }

                // Adjacent-only rules above determine eligibility, never priority.
                // All eligible species share nearest-distance selection and random ties.
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
        if (_species[predatorIndex] is CritterSpecies.MegaSpider &&
            TryStoreCaughtPrey(predatorIndex, preyIndex))
        {
            return;
        }
        if (_species[predatorIndex] is CritterSpecies.UndeadApe)
        {
            TryInfectApeAt(preyPosition, PlagueKind.Zombie);
        }
        if (_species[preyIndex] is CritterSpecies.UndeadApe)
        {
            TryInfectApeAt(predatorPosition, PlagueKind.Zombie);
        }
        if (CanTherapsidStrikeAdjacentLakePrey(predatorIndex, preyIndex))
        {
            var shorelinePreySpecies = _species[preyIndex];
            RemoveCritterAt(preyPosition);
            FeedPredatorAt(predatorPosition, shorelinePreySpecies);
            return;
        }

        if (CanStrikeAdjacentFeederCrab(predatorIndex, preyIndex) &&
            !CanLiveOn(_species[predatorIndex], GetIndex(preyPosition)))
        {
            RemoveCritterAt(preyPosition);
            FeedPredatorAt(predatorPosition, CritterSpecies.Crab);
            return;
        }

        var predatorSpecies = _species[predatorIndex];
        var preySpecies = _species[preyIndex];
        if (predatorSpecies != preySpecies &&
            CanEat(predatorSpecies, preySpecies) &&
            (CanEat(preySpecies, predatorSpecies) ||
                CanFightBackAgainst(preySpecies, predatorSpecies) ||
                predatorSpecies is CritterSpecies.MegaSpider && IsPredator(preySpecies)))
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
        var defenderWins = NextInt(2) == 0;
        if (defenderWins)
        {
            _energy[attackerIndex] = Math.Max(
                0,
                _energy[attackerIndex] - GetCombatDamage(defenderSpecies));
            _damageFlashUntilTicks[attackerIndex] = Tick + CombatDamageFlashTicks;
            if (_energy[attackerIndex] == 0)
            {
                if (TryReanimateApe(attackerIndex))
                {
                    return;
                }
                RemoveCritterAt(attackerPosition);
                FeedPredatorAt(defenderPosition, attackerSpecies);
            }
            return;
        }

        _energy[defenderIndex] = Math.Max(
            0,
            _energy[defenderIndex] - GetCombatDamage(attackerSpecies));
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
            !CanEatInCurrentContext(predatorIndex, preyIndex))
        {
            return;
        }

        var predatorSpecies = _species[predatorIndex];
        var preySpecies = _species[preyIndex];
        if (predatorSpecies is CritterSpecies.MegaSpider &&
            TryStoreCaughtPrey(predatorIndex, preyIndex))
        {
            return;
        }
        if (TryReanimateApe(preyIndex))
        {
            // The risen body still occupies its tile; it cannot also become a meal.
            _preyTargets[predatorIndex] = -1;
            return;
        }
        RemoveCritterAt(preyPosition);
        // Removing the prey may compact the predator to another array index,
        // so resolve it again through its still-occupied source tile.
        predatorIndex = _occupants[GetIndex(predatorPosition)];
        var destinationIndex = GetIndex(preyPosition);
        MoveCritter(predatorIndex, destinationIndex, preyPosition);
        _preyTargets[predatorIndex] = -1;
        FeedPredatorAt(_positions[predatorIndex], preySpecies);
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
        var foodEnergy = CritterNutritions.Get(preySpecies).FoodEnergy;
        var personalFood = foodEnergy;
        var settlementFood = 0;
        var apeId = _critterIds[predatorIndex].Value;
        if (predatorSpecies is CritterSpecies.Ape or CritterSpecies.ApeSailor &&
            _apeVillageHomes.ContainsKey(apeId))
        {
            var personalReserve = predatorSpecies is CritterSpecies.ApeSailor
                ? nutrition.MaximumEnergy
                : nutrition.ReproductionThreshold;
            personalFood = Math.Min(
                foodEnergy,
                Math.Max(0, personalReserve - _energy[predatorIndex]));
            settlementFood = foodEnergy - personalFood;
        }
        _energy[predatorIndex] = Math.Min(
            nutrition.MaximumEnergy,
            _energy[predatorIndex] + personalFood);
        if (settlementFood > 0)
        {
            _apeCarriedFood.TryGetValue(apeId, out var carriedFood);
            _apeCarriedFood[apeId] = carriedFood + settlementFood;
        }
    }

    internal static bool CanEat(CritterSpecies predator, CritterSpecies prey) =>
        (prey is CritterSpecies.Crab && IsCrabFeederPredator(predator)) ||
        predator switch
    {
        CritterSpecies.Jellyfish => prey is CritterSpecies.Plankton,
        CritterSpecies.Fish => prey is CritterSpecies.Plankton,
        CritterSpecies.SeaScorpion =>
            prey is CritterSpecies.Fish or CritterSpecies.Worm or CritterSpecies.Trilobite or CritterSpecies.Newt or
                CritterSpecies.Crab or CritterSpecies.Squid or CritterSpecies.Nautilus or
                CritterSpecies.Therapsid or CritterSpecies.Monkey or CritterSpecies.Ape or
                CritterSpecies.Wolf or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
                CritterSpecies.ApeSailor or CritterSpecies.Deer or CritterSpecies.Elk or
                CritterSpecies.Gazelle,
        CritterSpecies.MegaSpider => prey is not CritterSpecies.MegaSpider,
        CritterSpecies.Nautilus => prey is CritterSpecies.Plankton,
        CritterSpecies.Squid =>
            prey is CritterSpecies.Fish or CritterSpecies.Worm or CritterSpecies.Trilobite or
                CritterSpecies.Nautilus or CritterSpecies.Crab or
                CritterSpecies.Newt or CritterSpecies.SeaScorpion or
                CritterSpecies.ApeSailor or
                CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle,
        CritterSpecies.MegaToad => IsMegaToadPrey(prey),
        CritterSpecies.Therapsid => prey is CritterSpecies.Fish,
        CritterSpecies.Monkey => false,
        CritterSpecies.UndeadApe => IsLivingApe(prey),
        CritterSpecies.Ape => prey is not
            (CritterSpecies.Plankton or CritterSpecies.Worm or
                CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain),
        CritterSpecies.ApeSailor =>
            prey is CritterSpecies.Jellyfish or CritterSpecies.Trilobite or
                CritterSpecies.SeaScorpion or CritterSpecies.Nautilus or CritterSpecies.Fish or
                CritterSpecies.Crab or CritterSpecies.Squid or CritterSpecies.SquidEgg,
        CritterSpecies.ApeWarrior => IsApePredator(prey),
        CritterSpecies.ApeChieftain => IsApePredator(prey),
        CritterSpecies.Wolf =>
            prey is not (CritterSpecies.Wolf or CritterSpecies.MegaToad) &&
            IsLargeLandPredatorPrey(prey),
        CritterSpecies.ToothedWhale => prey is
            CritterSpecies.SeaScorpion or
            CritterSpecies.Nautilus or CritterSpecies.Fish or CritterSpecies.Squid or
            CritterSpecies.ApeSailor || IsToothedWhaleShallowsPrey(prey),
        CritterSpecies.BaleenWhale => prey is CritterSpecies.Plankton,
        _ => false,
    };

    private bool CanEatInCurrentContext(int predatorIndex, int preyIndex)
    {
        if (CanEat(_species[predatorIndex], _species[preyIndex]))
        {
            return true;
        }

        if (_species[predatorIndex] is not CritterSpecies.Ape ||
            _species[preyIndex] is not CritterSpecies.Worm ||
            !_apeVillageHomes.TryGetValue(_critterIds[predatorIndex].Value, out var villageTile))
        {
            return false;
        }

        return !_apeVillageFood.TryGetValue(villageTile, out var villageFood) || villageFood == 0;
    }

    private static bool IsCrabFeederPredator(CritterSpecies species) => species is
        CritterSpecies.Jellyfish or CritterSpecies.SeaScorpion or
        CritterSpecies.Nautilus or CritterSpecies.Squid or CritterSpecies.MegaToad or CritterSpecies.MegaSpider or
        CritterSpecies.Therapsid or CritterSpecies.Ape or CritterSpecies.ApeSailor or
        CritterSpecies.Wolf or CritterSpecies.ToothedWhale;

    internal static bool CanFightBackAgainst(CritterSpecies defender, CritterSpecies attacker) =>
        defender switch
        {
            CritterSpecies.Therapsid => attacker is CritterSpecies.MegaToad or CritterSpecies.Wolf,
            CritterSpecies.SeaScorpion or CritterSpecies.Squid =>
                attacker is CritterSpecies.MegaToad,
            CritterSpecies.Wolf => attacker is CritterSpecies.MegaToad,
            _ => false,
        };

    internal static int GetCombatDamage(CritterSpecies species) =>
        species switch
        {
            CritterSpecies.ApeChieftain => 4,
            CritterSpecies.ApeWarrior => 3,
            _ => IsHeavyCombatPredator(species) ? 2 : 1,
        };

    internal static bool IsPredator(CritterSpecies species) => species is
        CritterSpecies.ToothedWhale or CritterSpecies.SeaScorpion or CritterSpecies.MegaToad or
        CritterSpecies.MegaSpider or CritterSpecies.Wolf or CritterSpecies.Squid or
        CritterSpecies.Therapsid or CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
        CritterSpecies.UndeadApe;

    private static bool IsHeavyCombatPredator(CritterSpecies species) => species is
        CritterSpecies.ToothedWhale or CritterSpecies.SeaScorpion or CritterSpecies.MegaToad or
        CritterSpecies.Wolf or CritterSpecies.Squid;

    private static bool IsApePredator(CritterSpecies species) => species is
        CritterSpecies.SeaScorpion or CritterSpecies.MegaSpider or CritterSpecies.MegaToad or
        CritterSpecies.Wolf or CritterSpecies.ToothedWhale or CritterSpecies.UndeadApe;

    internal static bool CanPursuePreyAtDistance(
        CritterSpecies predator,
        CritterSpecies prey,
        int distance) =>
        CanEat(predator, prey) &&
        IsPreyPursuitAllowedAtDistance(predator, prey, distance);

    private static bool IsPreyPursuitAllowedAtDistance(
        CritterSpecies predator,
        CritterSpecies prey,
        int distance) =>
        !(distance > 1 &&
            (prey is CritterSpecies.Crab ||
                predator is CritterSpecies.MegaToad && prey is CritterSpecies.MegaToad ||
                predator is CritterSpecies.MegaToad && prey is CritterSpecies.Worm ||
                predator is CritterSpecies.Ape && prey is CritterSpecies.Fish ||
                (predator is CritterSpecies.Squid or CritterSpecies.SeaScorpion) &&
                    prey is CritterSpecies.Worm ||
                (predator is CritterSpecies.Squid or CritterSpecies.ToothedWhale) &&
                    prey is CritterSpecies.Nautilus ||
                predator is CritterSpecies.SeaScorpion &&
                    prey is CritterSpecies.Trilobite or CritterSpecies.Nautilus));

    internal static bool CanDisplace(CritterSpecies mover, CritterSpecies blocker) =>
        CritterNutritions.Get(mover).BodySize > CritterNutritions.Get(blocker).BodySize;

    internal static bool CanDisplacePlankton(CritterSpecies mover) =>
        mover is not CritterSpecies.SquidEgg;

    private static bool IsLargeLandPredatorPrey(CritterSpecies prey) =>
        prey is CritterSpecies.Trilobite or CritterSpecies.Nautilus or
            CritterSpecies.Fish or CritterSpecies.Newt or CritterSpecies.Crab or
            CritterSpecies.MegaToad or CritterSpecies.Therapsid or CritterSpecies.Monkey or
            CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle or
            CritterSpecies.Wolf or CritterSpecies.Ape or CritterSpecies.ApeSailor or
            CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain;

    private static bool IsMegaToadPrey(CritterSpecies prey) =>
        IsLargeLandPredatorPrey(prey) ||
        prey is CritterSpecies.Worm or CritterSpecies.SeaScorpion or CritterSpecies.Squid;

    private static bool IsToothedWhaleShallowsPrey(CritterSpecies prey) => prey is
        CritterSpecies.MegaToad or CritterSpecies.Therapsid or CritterSpecies.Ape or
        CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
        CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle or
        CritterSpecies.Wolf;

    private bool CanPursuePrey(int predatorIndex, int preyIndex)
    {
        if (!CanEatInCurrentContext(predatorIndex, preyIndex))
        {
            return false;
        }

        // These predators take monkeys only within one movement step, including
        // diagonals and the horizontal seam; monkeys receive no target priority.
        if (_species[predatorIndex] is CritterSpecies.Wolf or CritterSpecies.MegaToad &&
            _species[preyIndex] is CritterSpecies.Monkey)
        {
            var predator = _positions[predatorIndex];
            var prey = _positions[preyIndex];
            var horizontal = Math.Abs(predator.X - prey.X);
            if (Math.Min(horizontal, Width - horizontal) > 1 ||
                Math.Abs(predator.Y - prey.Y) > 1)
            {
                return false;
            }
        }

        if ((_species[preyIndex] is CritterSpecies.Crab ||
                _species[predatorIndex] is CritterSpecies.ToothedWhale &&
                _species[preyIndex] is CritterSpecies.Nautilus) &&
            WrappedManhattanDistance(
                _positions[predatorIndex],
                _positions[preyIndex]) != 1)
        {
            return false;
        }

        if (!CanHuntPreyOnCurrentTile(predatorIndex, preyIndex))
        {
            return false;
        }

        if (HasActiveReproductionTruceWith(predatorIndex, _species[preyIndex]) ||
            HasActiveReproductionTruceWith(preyIndex, _species[predatorIndex]))
        {
            return false;
        }

        return true;
    }

    private bool CanHuntPreyOnCurrentTile(int predatorIndex, int preyIndex) =>
        _species[predatorIndex] is not CritterSpecies.ToothedWhale ||
        !IsToothedWhaleShallowsPrey(_species[preyIndex]) ||
        _terrain[GetIndex(_positions[preyIndex])] is Terrain.Shallows;

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

    private bool CanTherapsidStrikeAdjacentLakePrey(int predatorIndex, int preyIndex)
    {
        if (_species[predatorIndex] is not CritterSpecies.Therapsid ||
            _species[preyIndex] is not CritterSpecies.Fish ||
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

    private bool CanStrikeAdjacentFeederCrab(int predatorIndex, int preyIndex) =>
        _species[preyIndex] is CritterSpecies.Crab &&
        IsCrabFeederPredator(_species[predatorIndex]) &&
        WrappedManhattanDistance(_positions[predatorIndex], _positions[preyIndex]) == 1;

    private bool CanFeedFromEnvironment(CritterSpecies species, int tileIndex)
        => FindEnvironmentalFoodTile(species, tileIndex) >= 0;

    private bool TryFeedFromTerrain(int critterIndex)
    {
        var nutrition = CritterNutritions.Get(_species[critterIndex]);
        if (!nutrition.FeedsFromEnvironment || _energy[critterIndex] >= nutrition.MaximumEnergy)
        {
            return false;
        }

        if (nutrition.FeedingStrategy is not CritterFeedingStrategy.Photosynthetic &&
            !TryConsumeEnvironmentalNutrition(
                _species[critterIndex],
                GetIndex(_positions[critterIndex])))
        {
            return false;
        }

        _energy[critterIndex]++;
        return true;
    }

    private bool TryConsumeEnvironmentalNutrition(CritterSpecies species, int tileIndex)
    {
        var foodTile = FindEnvironmentalFoodTile(species, tileIndex);
        if (foodTile < 0)
        {
            return false;
        }

        if (_depositedTileNutrition[foodTile] > 0)
        {
            _depositedTileNutrition[foodTile]--;
            return true;
        }

        return TryConsumeNaturalTileNutrition(foodTile);
    }

    private bool TryConsumeNaturalTileNutrition(int tileIndex)
    {
        if (!HasNaturalTileNutrition(tileIndex))
        {
            return false;
        }

        if (_tileNutrition[tileIndex] == _tileNutritionCapacities[tileIndex])
        {
            _tileNutritionLastTicks[tileIndex] = Tick;
        }
        _tileNutrition[tileIndex]--;
        return true;
    }

    private int FindEnvironmentalFoodTile(CritterSpecies species, int tileIndex)
    {
        if (IsFreshwaterTile(tileIndex) && !CanFeedInFreshwater(species))
        {
            return -1;
        }
        if (species is CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.Gazelle &&
            !IsGrazerFoliageTile(species, tileIndex))
        {
            return -1;
        }

        if (HasDepositedTileNutrition(tileIndex) ||
            (IsEnvironmentalFoodSourceForSpecies(species, tileIndex) &&
                HasNaturalTileNutrition(tileIndex)))
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
            if (IsNewtFeedingTile(candidateTile) &&
                (HasDepositedTileNutrition(candidateTile) ||
                    HasNaturalTileNutrition(candidateTile)))
            {
                return candidateTile;
            }
        }
        return -1;
    }

    private bool HasDepositedTileNutrition(int tileIndex) =>
        _depositedTileNutrition[tileIndex] > 0;

    private bool HasNaturalTileNutrition(int tileIndex)
    {
        RefreshTileNutrition(tileIndex);
        return _tileNutrition[tileIndex] > 0;
    }

    private void DepositTileNutrition(int tileIndex)
    {
        if (_depositedTileNutrition[tileIndex] < byte.MaxValue)
        {
            _depositedTileNutrition[tileIndex]++;
        }
    }

    private bool IsEnvironmentalFoodSourceForSpecies(CritterSpecies species, int tileIndex) =>
        species switch
    {
        CritterSpecies.Worm => IsTrilobiteFeedingTile(tileIndex),
        CritterSpecies.Trilobite => IsTrilobiteFeedingTile(tileIndex),
        CritterSpecies.Nautilus => IsNautilusFeedingTile(tileIndex),
        CritterSpecies.Crab => IsCrabFeedingTile(tileIndex),
        CritterSpecies.Fish => IsFishForagingTile(tileIndex),
        CritterSpecies.Therapsid => IsTherapsidFoliageTile(tileIndex),
        CritterSpecies.Newt =>
            IsNewtFeedingTile(tileIndex) || IsNewtFoliageTile(tileIndex),
        CritterSpecies.Monkey => IsMonkeyFoliageTile(tileIndex),
        CritterSpecies.Deer => IsGrazerFoliageTile(CritterSpecies.Deer, tileIndex),
        CritterSpecies.Elk => IsGrazerFoliageTile(CritterSpecies.Elk, tileIndex),
        CritterSpecies.Gazelle => IsGrazerFoliageTile(CritterSpecies.Gazelle, tileIndex),
        _ => true,
    };

    private bool IsFreshwaterTile(int tileIndex) =>
        _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake;

    private static bool CanFeedInFreshwater(CritterSpecies species) => species is
        CritterSpecies.Fish or CritterSpecies.Newt;

    private int CalculateTileNutritionCapacity(int tileIndex)
    {
        if (_surfaceCovers[tileIndex] is not SurfaceCover.None)
        {
            return 0;
        }

        var temperatureBand = ClimateSystem.ClassifyTemperature(_temperature[tileIndex]);
        if (_surfaceWater[tileIndex] is
            SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake)
        {
            return 2;
        }

        return GetUnderlyingTileNutritionCapacity(tileIndex, temperatureBand);
    }

    private int GetUnderlyingTileNutritionCapacity(
        int tileIndex,
        TemperatureBand temperatureBand) =>
        _terrain[tileIndex] switch
        {
            Terrain.Beach => temperatureBand is TemperatureBand.Freezing ? 0 : 1,
            Terrain.Shallows => temperatureBand switch
            {
                TemperatureBand.Freezing => 2,
                TemperatureBand.Cold => 3,
                _ => 4,
            },
            Terrain.Ocean => 0,
            Terrain.DeepOcean => temperatureBand switch
            {
                TemperatureBand.Cold => 2,
                _ => 1,
            },
            Terrain.Ice => IsIceOverDeepOcean(tileIndex) ? 1 : 0,
            Terrain.Mountain or Terrain.RingWorldWall => 0,
            _ => _biomes[tileIndex] switch
            {
                Biome.Jungle => 6,
                Biome.Swamp => 5,
                Biome.Forest => 4,
                Biome.Grassland or Biome.Taiga => 3,
                Biome.Bog or Biome.Arid => 2,
                Biome.Tundra => 1,
                _ => 0,
            },
        };

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

        var regenerationInterval = TileNutritionRegenerationSeconds * TicksPerSecond / capacity;
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

    private bool IsTrilobiteFeedingTile(int tileIndex) =>
        IsTrilobiteFeedingTerrain(_terrain[tileIndex]) || IsIceOverDeepOcean(tileIndex);

    private static bool IsTrilobiteFeedingTerrain(Terrain terrain) =>
        terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Beach;

    private bool IsIceOverDeepOcean(int tileIndex) =>
        _terrain[tileIndex] is Terrain.Ice &&
        SeaLevel - _elevation[tileIndex] > TerrainClassifier.DeepOceanDepthThreshold;

    private static bool IsNautilusFeedingTerrain(Terrain terrain) =>
        terrain is Terrain.DeepOcean or Terrain.Shallows;

    private bool IsNautilusFeedingTile(int tileIndex) =>
        IsNautilusFeedingTerrain(_terrain[tileIndex]) || IsIceOverDeepOcean(tileIndex);

    private bool IsCrabFeedingTile(int tileIndex) =>
        _terrain[tileIndex] is Terrain.Beach or Terrain.Shallows ||
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle;

    private bool IsFishForagingTile(int tileIndex) =>
        _surfaceWater[tileIndex] is SurfaceWaterKind.River or SurfaceWaterKind.FreshwaterLake ||
        _terrain[tileIndex] is Terrain.Shallows;

    private bool IsMonkeyFoliageTile(int tileIndex) =>
        !IsFreshwaterTile(tileIndex) &&
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle or Biome.Forest &&
        CanLiveOn(CritterSpecies.Monkey, tileIndex);

    private bool IsApeFoliageTile(int tileIndex) =>
        !IsFreshwaterTile(tileIndex) &&
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle &&
        CanLiveOn(CritterSpecies.Ape, tileIndex);

    private bool IsTherapsidFoliageTile(int tileIndex) =>
        !IsFreshwaterTile(tileIndex) &&
        _biomes[tileIndex] is Biome.Swamp or Biome.Jungle or Biome.Arid &&
        CanLiveOn(CritterSpecies.Therapsid, tileIndex);

    private bool IsGrazerFoliageTile(CritterSpecies species, int tileIndex) =>
        !IsFreshwaterTile(tileIndex) && CanLiveOn(species, tileIndex) && species switch
        {
            CritterSpecies.Deer =>
                _biomes[tileIndex] is Biome.Grassland or Biome.Forest,
            CritterSpecies.Elk =>
                _biomes[tileIndex] is Biome.Grassland or Biome.Tundra or Biome.Taiga,
            CritterSpecies.Gazelle =>
                _biomes[tileIndex] is Biome.Arid or Biome.Grassland,
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
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        var blockerIndex = _occupants[destinationIndex];
        return blockerIndex >= 0 && !IsCaughtInMegaSpiderWeb(blockerIndex) &&
            (CanShovePlankton(moverIndex, destinationIndex, reservedPrey) ||
                CanShoveCritter(moverIndex, destinationIndex, reservedPrey) ||
                CanShoveNewt(moverIndex, destinationIndex, reservedPrey));
    }

    private bool TryShoveMovementBlocker(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        var blockerIndex = _occupants[destinationIndex];
        return blockerIndex >= 0 && !IsCaughtInMegaSpiderWeb(blockerIndex) &&
            (TryShovePlankton(moverIndex, destinationIndex, reservedPrey) ||
                TryShoveCritter(moverIndex, destinationIndex, reservedPrey) ||
                TryShoveNewt(moverIndex, destinationIndex, reservedPrey));
    }

    private bool CanShoveCritter(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var blockerIndex = _occupants[destinationIndex];
        return blockerIndex >= 0 &&
            _species[blockerIndex] is not CritterSpecies.Plankton &&
            CanDisplace(_species[moverIndex], _species[blockerIndex]) &&
            CanLiveOn(_species[moverIndex], destinationIndex) &&
            reservedPrey?.Contains(_positions[blockerIndex]) is not true &&
            FindCritterShoveDestination(moverIndex, blockerIndex, reservedPrey) is not null;
    }

    private bool TryShoveCritter(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (!CanShoveCritter(moverIndex, destinationIndex, reservedPrey))
        {
            return false;
        }

        var blockerIndex = _occupants[destinationIndex];
        var shoveDestination = FindCritterShoveDestination(
            moverIndex,
            blockerIndex,
            reservedPrey)!.Value;
        MoveCritter(blockerIndex, GetIndex(shoveDestination), shoveDestination, activateTeleporter: false);
        return true;
    }

    private GridPosition? FindCritterShoveDestination(
        int moverIndex,
        int blockerIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var blockerPosition = _positions[blockerIndex];
        var blockerSpecies = _species[blockerIndex];
        var startDirection = (moverIndex + blockerIndex) % MovementDirections.Length;
        for (var offset = 0; offset < MovementDirections.Length; offset++)
        {
            var direction = MovementDirections[(startDirection + offset) % MovementDirections.Length];
            var candidate = new GridPosition(
                Mod(blockerPosition.X + direction.X, Width),
                blockerPosition.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                reservedPrey?.Contains(candidate) is true)
            {
                continue;
            }

            var candidateIndex = GetIndex(candidate);
            if (_occupants[candidateIndex] < 0 && CanLiveOn(blockerSpecies, candidateIndex))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool CanShovePlankton(
        int moverIndex,
        int destinationIndex,
        IReadOnlySet<GridPosition>? reservedPrey = null)
    {
        var planktonIndex = _occupants[destinationIndex];
        if (planktonIndex < 0 || planktonIndex == moverIndex ||
            !CanDisplacePlankton(_species[moverIndex]) ||
            _species[planktonIndex] is not CritterSpecies.Plankton ||
            !CanCritterRemainOnTile(moverIndex, destinationIndex))
        {
            return false;
        }

        var planktonPosition = _positions[planktonIndex];
        if (reservedPrey?.Contains(planktonPosition) is true)
        {
            return false;
        }

        return true;
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
            MoveCritter(
                planktonIndex,
                GetIndex(shoveDestination.Value),
                shoveDestination.Value,
                activateTeleporter: false);
            return true;
        }

        if (TryShovePlanktonChain(moverIndex, planktonIndex, reservedPrey))
        {
            return true;
        }

        var moverSpecies = _species[moverIndex];
        var moverPosition = _positions[moverIndex];
        _lethallyDisplacedCritterIds.Add(_critterIds[planktonIndex].Value);
        if (CanEat(moverSpecies, CritterSpecies.Plankton))
        {
            FeedPredatorAt(moverPosition, CritterSpecies.Plankton);
        }
        return true;
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
            _species[newtIndex] is not CritterSpecies.Newt ||
            (_species[moverIndex] is not CritterSpecies.Fish &&
                !CanDisplace(_species[moverIndex], _species[newtIndex])) ||
            !CanCritterRemainOnTile(moverIndex, destinationIndex) ||
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
        MoveCritter(newtIndex, GetIndex(shoveDestination), shoveDestination, activateTeleporter: false);
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
        if (wormIndex < 0 || IsCaughtInMegaSpiderWeb(wormIndex) ||
            !CanDisplace(_species[moverIndex], _species[wormIndex]) ||
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
        MoveCritter(wormIndex, GetIndex(shoveDestination), shoveDestination, activateTeleporter: false);
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
            MoveCritter(planktonIndex, GetIndex(destination), destination, activateTeleporter: false);
        }

        return true;
    }

    private void MoveCritter(
        int critterIndex,
        int destinationIndex,
        GridPosition destination,
        bool activateTeleporter = true)
    {
        var origin = _positions[critterIndex];
        var movingSpecies = _species[critterIndex];
        _occupants[GetIndex(origin)] = -1;
        _occupants[destinationIndex] = critterIndex;
        _positions[critterIndex] = destination;
        if (activateTeleporter)
        {
            TryActivateTeleporterAt(destination);
        }
        TriggerWolfDenNear(origin, _positions[critterIndex], movingSpecies);
    }

    internal bool TryActivateTeleporterAt(GridPosition position)
    {
        if (!Contains(position))
        {
            return false;
        }

        var sourceTile = GetIndex(position);
        var critterIndex = _occupants[sourceTile];
        if (critterIndex < 0 || !_teleporters.Contains(sourceTile))
        {
            return false;
        }

        var arrival = _teleporters.Count == 1
            ? FindRandomUnoccupiedTeleporterArrival(critterIndex)
            : FindLinkedTeleporterArrival(critterIndex, sourceTile) ??
                FindRandomUnoccupiedTeleporterArrival(critterIndex);
        if (arrival is null)
        {
            return false;
        }

        var arrivalTile = GetIndex(arrival.Value);
        _occupants[sourceTile] = -1;
        _occupants[arrivalTile] = critterIndex;
        _positions[critterIndex] = arrival.Value;
        _preyTargets[critterIndex] = -1;
        return true;
    }

    private GridPosition? FindRandomUnoccupiedTeleporterArrival(int critterIndex)
    {
        var candidates = new List<int>();
        for (var tileIndex = 0; tileIndex < _terrain.Length; tileIndex++)
        {
            if (_occupants[tileIndex] < 0 && !_teleporters.Contains(tileIndex) &&
                CanLiveOn(_species[critterIndex], tileIndex))
            {
                candidates.Add(tileIndex);
            }
        }

        return candidates.Count == 0
            ? null
            : GetPosition(candidates[NextInt(candidates.Count)]);
    }

    private GridPosition? FindLinkedTeleporterArrival(int critterIndex, int sourceTile)
    {
        var destinations = new List<(int PortalTile, List<int> ArrivalTiles)>();
        foreach (var portalTile in _teleporters)
        {
            if (portalTile == sourceTile)
            {
                continue;
            }

            var portal = GetPosition(portalTile);
            var arrivals = new List<int>();
            foreach (var direction in MovementDirections)
            {
                var y = portal.Y + direction.Y;
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                var candidate = new GridPosition(Mod(portal.X + direction.X, Width), y);
                var candidateTile = GetIndex(candidate);
                if (_occupants[candidateTile] < 0 && !_teleporters.Contains(candidateTile) &&
                    CanLiveOn(_species[critterIndex], candidateTile) &&
                    !arrivals.Contains(candidateTile))
                {
                    arrivals.Add(candidateTile);
                }
            }

            if (arrivals.Count > 0)
            {
                destinations.Add((portalTile, arrivals));
            }
        }

        if (destinations.Count == 0)
        {
            return null;
        }

        var destination = destinations[NextInt(destinations.Count)];
        return GetPosition(destination.ArrivalTiles[NextInt(destination.ArrivalTiles.Count)]);
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
        var removed = false;

        // A tile should have exactly one critter. Still, resolve the position
        // defensively as well as the occupancy index: if an interrupted or
        // formerly buggy compact-array move left a duplicate entry behind,
        // terrain destruction must remove every critter at the tile instead of
        // leaving an unreachable immortal entry.
        while (true)
        {
            var critterIndex = _occupants[tileIndex];
            if (critterIndex < 0 || critterIndex >= _count ||
                _positions[critterIndex] != position)
            {
                critterIndex = -1;
                for (var index = 0; index < _count; index++)
                {
                    if (_positions[index] == position)
                    {
                        critterIndex = index;
                        break;
                    }
                }
            }

            if (critterIndex < 0)
            {
                _occupants[tileIndex] = -1;
                return removed;
            }

            RemoveCritterAtIndex(critterIndex);
            removed = true;
        }
    }

    private bool RemoveCritter(CritterId critterId)
    {
        if (!critterId.IsValid ||
            !_critterIndicesById.TryGetValue(critterId.Value, out var critterIndex))
        {
            return false;
        }

        return RemoveCritterAtIndex(critterIndex);
    }

    private bool RemoveCritterAtIndex(int critterIndex)
    {
        var tileIndex = GetIndex(_positions[critterIndex]);
        var lastIndex = _count - 1;
        var removedId = _critterIds[critterIndex];
        var removedSpecies = _species[critterIndex];
        _critterIndicesById.Remove(removedId.Value);
        _plagues.Remove(removedId.Value);
        DetachWolfFromDens(removedId.Value);
        DetachMegaSpiderFromWeb(removedId.Value);
        DetachApeFromVillage(removedId.Value);
        _reproductionTruces.Remove(removedId.Value);
        _speciesCounts[(int)removedSpecies]--;
        if (PlanktonRecoveryEnabled && removedSpecies is CritterSpecies.Plankton &&
            GetCritterCount(CritterSpecies.Plankton) == 0)
        {
            _nextPlanktonRecoveryTick = Tick + PlanktonRecoveryIntervalTicks;
        }
        if (_occupants[tileIndex] == critterIndex)
        {
            _occupants[tileIndex] = -1;
        }
        if (critterIndex != lastIndex)
        {
            _critterIds[critterIndex] = _critterIds[lastIndex];
            _critterIndicesById[_critterIds[critterIndex].Value] = critterIndex;
            _species[critterIndex] = _species[lastIndex];
            _positions[critterIndex] = _positions[lastIndex];
            _nextMovementTicks[critterIndex] = _nextMovementTicks[lastIndex];
            _energy[critterIndex] = _energy[lastIndex];
            _nextMetabolismTicks[critterIndex] = _nextMetabolismTicks[lastIndex];
            _preyTargets[critterIndex] = _preyTargets[lastIndex];
            _damageFlashUntilTicks[critterIndex] = _damageFlashUntilTicks[lastIndex];
            var movedTileIndex = GetIndex(_positions[critterIndex]);
            if (_occupants[movedTileIndex] == lastIndex)
            {
                _occupants[movedTileIndex] = critterIndex;
            }
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

    private GridPosition GetPosition(int tileIndex) =>
        new(tileIndex % Width, tileIndex / Width);

    private bool CanLiveOn(CritterSpecies species, int tileIndex) =>
        _terrain[tileIndex] is Terrain.Ice &&
            _surfaceCovers[tileIndex] is SurfaceCover.None ||
        (species is CritterSpecies.Fish
            ? IsFishTile(tileIndex)
            : species is CritterSpecies.Trilobite
                ? IsTrilobiteTile(tileIndex)
            : species is CritterSpecies.ApeSailor
                ? IsApeSailorTile(tileIndex)
            : species is CritterSpecies.Worm
                ? IsTrilobiteTile(tileIndex)
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
                    _surfaceCovers[tileIndex]));

    private bool CanCritterRemainOnTile(int critterIndex, int tileIndex) =>
        _species[critterIndex] is CritterSpecies.Ape &&
            _apeSettlerTargets.ContainsKey(_critterIds[critterIndex].Value)
                ? IsApeSettlerTransitTile(tileIndex)
                : CanLiveOn(_species[critterIndex], tileIndex);

    private bool IsApeSettlerTransitTile(int tileIndex) =>
        _terrain[tileIndex] is not (Terrain.Mountain or Terrain.RingWorldWall) &&
        _surfaceCovers[tileIndex] is not SurfaceCover.Lava;

    private bool IsTrilobiteTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        _terrain[tileIndex] is
            Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Beach;

    private bool IsApeSailorTile(int tileIndex) =>
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        (_terrain[tileIndex] is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Beach ||
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

    internal bool IsValidReproductionSite(CritterSpecies species, GridPosition position) => true;

    internal bool IsValidBirthSite(CritterSpecies species, GridPosition position) => true;

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

    internal static int GetMovementIntervalTicks(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => PlanktonMovementIntervalTicks,
        CritterSpecies.Jellyfish => 5 * TicksPerSecond,
        CritterSpecies.Worm => 6 * TicksPerSecond,
        CritterSpecies.Trilobite => 6 * TicksPerSecond,
        CritterSpecies.SeaScorpion => 4 * TicksPerSecond,
        CritterSpecies.MegaSpider => 4 * TicksPerSecond,
        CritterSpecies.Nautilus => 6 * TicksPerSecond,
        CritterSpecies.Squid => 3 * TicksPerSecond,
        CritterSpecies.SquidEgg => PlanktonMovementIntervalTicks,
        CritterSpecies.Fish => 2 * TicksPerSecond,
        CritterSpecies.Newt => 5 * TicksPerSecond,
        CritterSpecies.MegaToad => 6 * TicksPerSecond,
        CritterSpecies.Therapsid => 6 * TicksPerSecond,
        CritterSpecies.Monkey => 5 * TicksPerSecond,
        CritterSpecies.Ape => 3 * TicksPerSecond,
        CritterSpecies.ApeSailor => 2 * TicksPerSecond,
        CritterSpecies.ApeWarrior => 3 * TicksPerSecond,
        CritterSpecies.ApeChieftain => 3 * TicksPerSecond,
        CritterSpecies.UndeadApe => 3 * TicksPerSecond,
        CritterSpecies.Deer => 3 * TicksPerSecond,
        CritterSpecies.Elk => 4 * TicksPerSecond,
        CritterSpecies.Gazelle => 3 * TicksPerSecond,
        CritterSpecies.Wolf => 5 * TicksPerSecond / 2,
        CritterSpecies.Crab => 4 * TicksPerSecond,
        CritterSpecies.ToothedWhale => 4 * TicksPerSecond,
        CritterSpecies.BaleenWhale => 4 * TicksPerSecond,
        _ => throw new ArgumentOutOfRangeException(nameof(species)),
    };

    private long GetFirstMovementTick(CritterSpecies species, CritterId id)
    {
        var interval = GetMovementIntervalTicks(species);
        // Distribute large drifting cohorts and perception-heavy hunters across
        // their interval so newly born groups do not all act on the same tick.
        if (species is CritterSpecies.Plankton or CritterSpecies.SquidEgg)
        {
            var phase = ((ulong)id.Value * 131UL + Seed) % (uint)interval;
            return Tick + 1 + (long)phase;
        }

        return species is CritterSpecies.Fish or CritterSpecies.Nautilus or
            CritterSpecies.Squid or CritterSpecies.SeaScorpion or CritterSpecies.MegaToad or
            CritterSpecies.MegaSpider or
            CritterSpecies.Therapsid or CritterSpecies.Monkey or CritterSpecies.Ape or
            CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or CritterSpecies.UndeadApe or CritterSpecies.Wolf or CritterSpecies.ToothedWhale or
            CritterSpecies.BaleenWhale
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
