namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, (int Step, int Portal, int NextPortalVisit)> _custodianPatrols = [];
    private readonly Dictionary<int, long> _custodianCreatedTicks = [];
    private readonly HashSet<int> _destroyedTeleporterTiles = [];
    public const long CustodianAncientAgeTicks = 100L * SeasonSystem.TicksPerYear;
    public const int CustodianRecruitmentIntervalTicks = 60 * TicksPerSecond;
    private const int CustodianPortalPatrolSteps = 15 * 60 / 5;
    private readonly List<GridPosition> _custodianFabricators = [];
    public IReadOnlyList<GridPosition> CustodianFabricators => _custodianFabricators;
    public int CustodianCapacity => 5 * _custodianFabricators.Count;
    public GridPosition? CustodianFabricator => _custodianFabricators.Count > 0 ? _custodianFabricators[0] : null;
    public bool IsCustodianFabricator(GridPosition position) => _custodianFabricators.Contains(position);

    private bool IsAncientCustodian(int index) =>
        _species[index] is CritterSpecies.Custodian &&
        _custodianCreatedTicks.TryGetValue(_critterIds[index].Value, out var created) &&
        Tick - created >= CustodianAncientAgeTicks;

    internal void SeedRingWorldCustodians()
    {
        if (Body is not WorldBody.RingWorld || CustodianFabricator is not null)
            return;
        var fabricatorCount = Width >= WorldPreset.RingLarge.Width ? 2 : 1;
        // Spread stationary fabricators along the engineered north wall.
        for (var fabricator = 0; fabricator < fabricatorCount; fabricator++)
        for (var offset = 0; offset < Width; offset++)
        {
            var position = new GridPosition(Mod(Width * (fabricator * 2 + 1) / (fabricatorCount * 2) + offset, Width), 0);
            if (_terrain[GetIndex(position)] is Terrain.MegaWall && !IsCustodianFabricator(position))
            {
                _custodianFabricators.Add(position);
                break;
            }
        }
        SeedInitialCustodians();
    }

    internal void SeedNexusCustodians(bool mega)
    {
        if (Body is not WorldBody.Nexus || CustodianFabricator is not null) return;
        foreach (var arm in mega ? new[] { 0, NexusLayout.ArmCount / 2 } : new[] { 0 })
        {
            // Place the fabricator on the Mega Wall at this arm's rounded tip.
            for (var step = 0; step < 20; step++)
            {
                var position = NexusLayout.ArmPosition(this, arm, 1f - step / NexusLayout.Radius(this));
                if (NexusLayout.Contains(this, position) && _terrain[GetIndex(position)] is Terrain.MegaWall)
                {
                    _custodianFabricators.Add(position);
                    break;
                }
            }
        }
        SeedInitialCustodians();
    }

    private void SeedInitialCustodians()
    {
        if (CustodianFabricator is null)
            return;
        var fabricatorCount = _custodianFabricators.Count;
        // Three old drones are already patrolling the ring before the fabricator starts.
        for (var drone = 0; drone < 3 * fabricatorCount; drone++)
        {
            var start = new GridPosition(drone * Width / (3 * fabricatorCount), Height / 2);
            for (var offset = 0; offset < Width * Height; offset++)
            {
                var tile = (GetIndex(start) + offset) % (Width * Height);
                if (_occupants[tile] >= 0 || _teleporters.Contains(tile) ||
                    IsCustodianFabricator(GetPosition(tile)))
                    continue;
                if (!TryAddCritter(CritterSpecies.Custodian, GetPosition(tile)))
                    continue;
                var original = _critterIds[_occupants[tile]];
                _custodianCreatedTicks[original.Value] = Tick - CustodianAncientAgeTicks;
                break;
            }
        }
        // The remaining two launch at the fabricator with modern chassis.
        foreach (var fabricator in _custodianFabricators)
            for (var drone = 0; drone < 2; drone++)
                RecruitCustodianAt(fabricator);
    }

    private CritterId? RecruitCustodian()
    {
        if (!LifeEnabled || CustodianFabricator is not { } fabricator)
            return null;
        return RecruitCustodianAt(fabricator);
    }

    private CritterId? RecruitCustodianAt(GridPosition fabricator)
    {
        if (!LifeEnabled)
            return null;
        for (var radius = 1; radius <= 2; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    continue;
                var position = new GridPosition(Mod(fabricator.X + dx, Width), fabricator.Y + dy);
                if (!Contains(position) || IsCustodianFabricator(position) ||
                    Body is WorldBody.Nexus && !NexusLayout.Contains(this, position))
                    continue;
                var tile = GetIndex(position);
                if (_occupants[tile] < 0 && !_teleporters.Contains(tile))
                    return AddCritter(CritterSpecies.Custodian, position);
            }
        }
        return null;
    }

    internal void AdvanceCustodianFabricator()
    {
        if (Tick % CustodianRecruitmentIntervalTicks != 0)
            return;
        foreach (var fabricator in _custodianFabricators)
            if (GetCritterCount(CritterSpecies.Custodian) < CustodianCapacity)
                RecruitCustodianAt(fabricator);
    }

    private bool IsCustodianTarget(int index) =>
        _species[index] is CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior or CritterSpecies.Lich ||
        IsLivingApe(_species[index]) && GetPlague(index) is not PlagueKind.None;

    private GridPosition? TryMoveCustodian(int index, IReadOnlySet<GridPosition>? reservedPrey)
    {
        var maximumEnergy = CritterNutritions.Get(CritterSpecies.Custodian).MaximumEnergy;
        if (_energy[index] * 4 <= maximumEnergy && _custodianFabricators.Count > 0)
        {
            var fabricator = _custodianFabricators.OrderBy(position =>
                WrappedManhattanDistance(_positions[index], position)).First();
            _preyTargets[index] = -1;
            var damagedPosition = _positions[index];
            if (WrappedManhattanDistance(damagedPosition, fabricator) <= 1)
            {
                _energy[index] = maximumEnergy;
                return null;
            }
            GridPosition? repairStep = null;
            var bestDistance = int.MaxValue;
            foreach (var direction in MovementDirections)
            {
                var candidate = new GridPosition(Mod(damagedPosition.X + direction.X, Width), damagedPosition.Y + direction.Y);
                if (!Contains(candidate) || !CanLiveOn(CritterSpecies.Custodian, GetIndex(candidate)) || _occupants[GetIndex(candidate)] >= 0 ||
                    reservedPrey?.Contains(candidate) is true)
                    continue;
                var distance = WrappedManhattanDistance(candidate, fabricator);
                if (distance < bestDistance)
                {
                    repairStep = candidate;
                    bestDistance = distance;
                }
            }
            if (repairStep is { } step)
                MoveCritter(index, GetIndex(step), step, activateTeleporter: false);
            return null;
        }
        var target = FindHunterPrey(index, CritterSpecies.Custodian, 8, reservedPrey);
        if (target is not null)
            return TryMoveHunter(index, 8, reservedPrey, huntWhenFull: true, preferredTarget: target);

        _preyTargets[index] = -1;
        var id = _critterIds[index].Value;
        var state = _custodianPatrols.GetValueOrDefault(id,
            (Step: 0, Portal: -1, NextPortalVisit: CustodianPortalPatrolSteps));
        if (state.Portal >= 0 && (!_teleporters.Contains(state.Portal) ||
            _occupants[state.Portal] >= 0 && _occupants[state.Portal] != index))
            state.Portal = -1;

        var current = _positions[index];
        GridPosition? repairSite = null;
        var repairDistance = int.MaxValue;
        foreach (var tile in _destroyedTeleporterTiles.OrderBy(tile => tile))
        {
            // Keep damaged locations queued while disaster cover or buildings block repairs.
            if (_occupants[tile] >= 0 || _surfaceCovers[tile] is not SurfaceCover.None ||
                _terrain[tile] is Terrain.MegaWall || _apeStructures.ContainsKey(tile) ||
                _wolfDenCharges.ContainsKey(tile) || _megaSpiderWebFood.ContainsKey(tile) ||
                _volcanoes.Any(volcano => volcano.Position == GetPosition(tile)))
                continue;
            var distance = WrappedManhattanDistance(current, GetPosition(tile));
            if (distance < repairDistance)
            {
                repairDistance = distance;
                repairSite = GetPosition(tile);
            }
        }
        if (repairSite is { } site && repairDistance <= 1 && TryPlaceTeleporter(site))
        {
            state.Step++;
            _custodianPatrols[id] = state;
            return null;
        }
        // Portal trips have a short, staggered cadence independent of ring width.
        // Keep the destination until arrival, choosing the nearest open portal.
        if (state.Portal < 0 && state.Step >= state.NextPortalVisit)
        {
            var nearestDistance = int.MaxValue;
            foreach (var tile in _teleporters.OrderBy(tile => tile))
            {
                if (_occupants[tile] >= 0 && _occupants[tile] != index)
                    continue;
                var distance = WrappedManhattanDistance(current, GetPosition(tile));
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    state.Portal = tile;
                }
            }
        }
        if (repairSite is null && state.Portal == GetIndex(current) && TryActivateTeleporterAt(current))
        {
            state.Portal = -1;
            state.NextPortalVisit = state.Step + CustodianPortalPatrolSteps;
            state.Step++;
            _custodianPatrols[id] = state;
            return null;
        }
        var east = id % 2 == 0 ? 1 : -1;
        var phase = 2 * Math.PI * (state.Step / (double)Math.Max(16, Width) + id / 5.0);
        var arcY = (int)Math.Round((Height - 1) * (0.5 + 0.4 * Math.Sin(phase)));
        var goal = repairSite ?? (state.Portal >= 0 ? GetPosition(state.Portal) :
            new GridPosition(Mod(current.X + east, Width), arcY));

        GridPosition? best = null;
        var bestScore = int.MaxValue;
        foreach (var direction in MovementDirections)
        {
            var candidate = new GridPosition(Mod(current.X + direction.X, Width), current.Y + direction.Y);
            if (candidate.Y < 0 || candidate.Y >= Height ||
                !CanLiveOn(CritterSpecies.Custodian, GetIndex(candidate)) ||
                candidate == repairSite ||
                _occupants[GetIndex(candidate)] >= 0 || reservedPrey?.Contains(candidate) is true)
                continue;
            var score = WrappedManhattanDistance(candidate, goal);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }
        if (best is { } destination)
        {
            var enteringPortal = GetIndex(destination) == state.Portal;
            MoveCritter(index, GetIndex(destination), destination);
            if (enteringPortal && _positions[index] != destination)
            {
                state.Portal = -1;
                state.NextPortalVisit = state.Step + CustodianPortalPatrolSteps;
            }
        }
        state.Step++;
        _custodianPatrols[id] = state;
        return null;
    }
}
