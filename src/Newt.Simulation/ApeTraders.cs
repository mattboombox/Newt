namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int ApeMarketRecruitmentIntervalTicks = 30 * TicksPerSecond;
    internal const int ApeTraderRecruitmentFoodCost = 3;

    private sealed class TraderJourney(int market, int origin, int destination, List<int> tiles)
    {
        public int Market = market;
        public int Origin = origin;
        public int Destination = destination;
        public List<int> Tiles = tiles;
        public int Step;
    }

    private readonly Dictionary<int, TraderJourney> _traderJourneys = [];
    private readonly Dictionary<int, HashSet<int>> _traderSettlementLinks = [];
    private long _nextMarketConstructionTick;
    private bool _traderSeaTravelEnabled;

    private bool IsTraderWater(int tile) => IsFishTile(tile) && !IsRoadTerrain(tile);
    private bool IsTraderHarbor(int tile) =>
        _apeStructures.GetValueOrDefault(tile) is ApeStructureKind.NavalDistrict && IsTraderWater(tile);

    // Roads connect settlements. Within a settlement traders can walk between its
    // center, market and harbor; only a harbor permits changing between land and sea.
    private void RebuildTraderSettlementLinks()
    {
        _traderSettlementLinks.Clear();
        _traderSeaTravelEnabled = _apeAuxiliaryVillages
            .Where(pair => IsTraderHarbor(pair.Key) && IsRoadVillage(pair.Value))
            .Select(pair => pair.Value).Distinct().Take(2).Count() == 2;
        foreach (var village in _apeStructures.Keys.Where(IsRoadVillage))
        {
            var tiles = GetPlayerConnectedVillageTiles(village);
            foreach (var tile in tiles)
            {
                if (!IsRoadTerrain(tile) && !IsTraderHarbor(tile))
                    continue;
                foreach (var next in RoadNeighbors(tile))
                {
                    if (!tiles.Contains(next) || (!IsRoadTerrain(next) && !IsTraderHarbor(next)) ||
                        !ArePlayerBuildingsAdjacent(tile, next, _apeStructures[next]))
                        continue;
                    if (!_traderSettlementLinks.TryGetValue(tile, out var links))
                        _traderSettlementLinks[tile] = links = [];
                    links.Add(next);
                }
            }
        }
    }

    private bool CanTraverseTraderEdge(int from, int to)
    {
        if (!_traderSeaTravelEnabled && (IsTraderWater(from) || IsTraderWater(to)))
            return false;
        if (_traderSettlementLinks.TryGetValue(from, out var districtLinks) && districtLinks.Contains(to))
            return (IsRoadTerrain(from) || IsTraderHarbor(from)) &&
                (IsRoadTerrain(to) || IsTraderHarbor(to));
        if (IsTraderWater(from) && IsTraderWater(to))
            return RoadNeighbors(from).Contains(to);
        return IsRoadTerrain(from) && IsRoadTerrain(to) &&
            _roadLinks.TryGetValue(from, out var roadLinks) && roadLinks.Contains(to);
    }

    private IEnumerable<int> TraderNeighbors(int tile)
    {
        var neighbors = new HashSet<int>();
        if (_traderSettlementLinks.TryGetValue(tile, out var districts))
            neighbors.UnionWith(districts);
        if (_roadLinks.TryGetValue(tile, out var roads))
            neighbors.UnionWith(roads);
        if (IsTraderWater(tile))
            neighbors.UnionWith(RoadNeighbors(tile).Where(IsTraderWater));
        return neighbors.Where(next => CanTraverseTraderEdge(tile, next)).Order();
    }

    private List<int>? FindTraderPath(int start, int currentVillage, int previousVillage = -1, bool random = true)
    {
        var previous = new Dictionary<int, int> { [start] = -1 };
        var costs = new Dictionary<int, double> { [start] = 0 };
        var pending = new PriorityQueue<int, (double Cost, int Tile)>();
        var destinations = new List<int>();
        pending.Enqueue(start, (0, start));
        while (pending.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > costs[current])
                continue;
            if (current != currentVillage && IsRoadVillage(current))
            {
                destinations.Add(current);
                if (!random)
                    break;
            }
            foreach (var next in TraderNeighbors(current))
            {
                var cost = costs[current] + TraderEdgeCost(current, next);
                if (costs.TryGetValue(next, out var best) && cost >= best)
                    continue;
                costs[next] = cost;
                previous[next] = current;
                pending.Enqueue(next, (cost, next));
            }
        }
        if (destinations.Count == 0)
            return null;
        if (destinations.Count > 1)
            destinations.Remove(previousVillage);
        var end = destinations[random ? NextInt(destinations.Count) : 0];
        // Keep village selection separate from the final stop. Settlement links
        // connect markets to the same road/harbor network as their village center.
        if (random)
            end = _apeAuxiliaryVillages
                .Where(pair => pair.Value == end &&
                    _apeStructures.GetValueOrDefault(pair.Key) is ApeStructureKind.Market &&
                    costs.ContainsKey(pair.Key))
                .Select(pair => pair.Key).DefaultIfEmpty(end).Min();
        var path = new List<int>();
        for (var tile = end; tile >= 0; tile = previous[tile])
            path.Add(tile);
        path.Reverse();
        return path;
    }

    private int TraderDestinationVillage(List<int> path) =>
        _apeAuxiliaryVillages.GetValueOrDefault(path[^1], path[^1]);

    private double TraderEdgeCost(int from, int to)
    {
        if (!IsTraderWater(from) || !IsTraderWater(to))
            return 1;
        var origin = GetPosition(from);
        var destination = GetPosition(to);
        // Diagonals cover more distance, including across the world's horizontal seam.
        // A finite depth penalty permits necessary crossings without huge coastal detours.
        var distance = origin.X != destination.X && origin.Y != destination.Y ? Math.Sqrt(2) : 1;
        return distance * (GetTerrain(destination) is Terrain.DeepOcean ? 2 : 1);
    }

    public bool CanBuildApeMarket(GridPosition village)
    {
        var tile = GetIndex(village);
        if (!IsRoadVillage(tile) || HasApeStructure(tile, ApeStructureKind.Market))
            return false;
        RebuildTraderSettlementLinks();
        return FindTraderPath(tile, tile, random: false) is not null;
    }

    public int GetApeMarketTraderCount(GridPosition market) =>
        _traderJourneys.Count(pair => pair.Value.Market == GetIndex(market) &&
            _critterIndicesById.ContainsKey(pair.Key));

    public GridPosition? GetApeTraderMarket(CritterId trader) =>
        _traderJourneys.TryGetValue(trader.Value, out var journey) ? GetPosition(journey.Market) : null;

    public GridPosition? GetApeTraderDestination(CritterId trader) =>
        _traderJourneys.TryGetValue(trader.Value, out var journey) ? GetPosition(journey.Destination) : null;

    internal void AdvanceApeTraders()
    {
        if (!LifeEnabled)
            return;
        RebuildTraderSettlementLinks();
        foreach (var (id, journey) in _traderJourneys.ToArray())
        {
            if (!_critterIndicesById.TryGetValue(id, out var index))
            {
                _traderJourneys.Remove(id);
                continue;
            }
            if (_apeStructures.GetValueOrDefault(journey.Market) is not ApeStructureKind.Market ||
                !IsRoadVillage(journey.Destination) ||
                journey.Tiles.Skip(journey.Step).Zip(journey.Tiles.Skip(journey.Step + 1))
                    .Any(edge => !CanTraverseTraderEdge(edge.First, edge.Second)))
                RemoveCritterAtIndex(index);
        }

        if (Tick >= _nextMarketConstructionTick)
        {
            _nextMarketConstructionTick = Tick + ApeMarketRecruitmentIntervalTicks;
            foreach (var village in _apeStructures.Keys.Where(IsRoadVillage).ToArray())
                if (GetApeVillageResidentCountByTile(village) > 0 &&
                    !HasApeStructure(village, ApeStructureKind.Market))
                    TryPurchaseApeStructure(village, ApeStructureKind.Market);
            RebuildTraderSettlementLinks();
        }
        foreach (var market in _apeStructures.Where(p => p.Value is ApeStructureKind.Market).Select(p => p.Key).ToArray())
        {
            if (Tick < _apeStructureNextActionTicks.GetValueOrDefault(market))
                continue;
            _apeStructureNextActionTicks[market] = Tick + ApeMarketRecruitmentIntervalTicks;
            TryRecruitApeMarketTrader(market);
        }
    }

    internal bool TryRecruitApeMarketTrader(int market)
    {
        if (!LifeEnabled || _apeStructures.GetValueOrDefault(market) is not ApeStructureKind.Market ||
            !_apeAuxiliaryVillages.TryGetValue(market, out var village) || !IsRoadVillage(village) ||
            GetApeMarketTraderCount(GetPosition(market)) >= 1 || _occupants[market] >= 0 ||
            _apeVillageFood.GetValueOrDefault(village) < ApeTraderRecruitmentFoodCost)
            return false;
        RebuildTraderSettlementLinks();
        var path = FindTraderPath(market, village);
        if (path is null)
            return false;
        var id = AddCritter(CritterSpecies.ApeTrader, GetPosition(market));
        var index = _critterIndicesById[id.Value];
        _energy[index] = ApeTraderRecruitmentFoodCost;
        _apeVillageFood[village] -= ApeTraderRecruitmentFoodCost;
        _traderJourneys[id.Value] = new(market, village, TraderDestinationVillage(path), path);
        RefillApeTraderProvisions(index);
        return true;
    }

    private void RefillApeTraderProvisions(int index)
    {
        var position = _positions[index];
        if (GetApeStructure(position) is not (ApeStructureKind.Village or ApeStructureKind.NavalDistrict or ApeStructureKind.Market) ||
            GetApeStructureVillage(position) is not { } village || !IsRoadVillage(GetIndex(village)) ||
            _plagues.ContainsKey(_critterIds[index].Value))
            return;
        var villageTile = GetIndex(village);
        var missing = CritterNutritions.Get(_species[index]).MaximumEnergy - _energy[index];
        var amount = Math.Min(missing, _apeVillageFood.GetValueOrDefault(villageTile));
        if (amount <= 0)
            return;
        _apeVillageFood[villageTile] -= amount;
        _energy[index] += amount;
    }

    internal void MoveApeTrader(CritterId id)
    {
        if (_critterIndicesById.TryGetValue(id.Value, out var index))
            TryMoveApeTrader(index, null);
    }

    private void TryRejoinTraderRoute(int index, TraderJourney journey, IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = GetIndex(_positions[index]);
        // A shove is a sidestep, not a lost route. Prefer the next route tile so
        // opposing traders can pass, then fall back to the tile we were pushed from.
        for (var step = Math.Min(journey.Step + 1, journey.Tiles.Count - 1); step >= journey.Step; step--)
        {
            var tile = journey.Tiles[step];
            var position = GetPosition(tile);
            if (!RoadNeighbors(current).Contains(tile) ||
                !CanLiveOn(_species[index], tile) ||
                IsTraderWater(tile) != (_species[index] is CritterSpecies.ApeTraderSailor) ||
                reservedPrey?.Contains(position) is true)
                continue;
            var blocker = _occupants[tile];
            // Wait for another trader to pass rather than shoving it back and forth.
            if (blocker >= 0 && _species[blocker] is (CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor))
                continue;
            if (!CanEnterOrShoveMovementBlocker(index, tile, reservedPrey))
                continue;
            MoveCritter(index, tile, position, activateTeleporter: false);
            journey.Step = step;
            RefillApeTraderProvisions(index);
            return;
        }

        // Repeated shoves can leave a trader more than one tile from its route.
        // Search a bounded local area for an empty recovery path, keeping the
        // current travel form so recovery cannot board away from a harbor.
        var parents = new Dictionary<int, int> { [current] = -1 };
        var pending = new Queue<int>();
        pending.Enqueue(current);
        while (pending.TryDequeue(out var tile) && parents.Count <= 256)
        {
            foreach (var next in RoadNeighbors(tile))
            {
                if (parents.ContainsKey(next) || _occupants[next] >= 0 ||
                    reservedPrey?.Contains(GetPosition(next)) is true ||
                    !CanLiveOn(_species[index], next) ||
                    IsTraderWater(next) != (_species[index] is CritterSpecies.ApeTraderSailor))
                    continue;
                parents[next] = tile;
                var step = next == journey.Tiles[journey.Step] ? journey.Step :
                    journey.Step + 1 < journey.Tiles.Count && next == journey.Tiles[journey.Step + 1]
                        ? journey.Step + 1 : -1;
                if (step >= 0)
                {
                    var move = next;
                    while (parents[move] != current)
                        move = parents[move];
                    MoveCritter(index, move, GetPosition(move), activateTeleporter: false);
                    if (move == next)
                        journey.Step = step;
                    RefillApeTraderProvisions(index);
                    return;
                }
                pending.Enqueue(next);
            }
        }
    }

    private bool CanTraderRecoverFromShove(int index, int tile)
    {
        if (!_traderJourneys.TryGetValue(_critterIds[index].Value, out var journey))
            return true;
        return RoadNeighbors(tile).Any(next =>
            (next == journey.Tiles[journey.Step] ||
                journey.Step + 1 < journey.Tiles.Count && next == journey.Tiles[journey.Step + 1]) &&
            CanLiveOn(_species[index], next) && IsTraderWater(next) == IsTraderWater(tile));
    }
    private bool TryPassTrader(int index, TraderJourney journey, int next, IReadOnlySet<GridPosition>? reservedPrey)
    {
        var other = _occupants[next];
        var current = GetIndex(_positions[index]);
        if (other < 0 || _species[other] is not (CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor) ||
            !_traderJourneys.TryGetValue(_critterIds[other].Value, out var otherJourney) ||
            IsCaughtInMegaSpiderWeb(other) || reservedPrey?.Contains(_positions[index]) is true ||
            !CanTraverseTraderEdge(next, current))
            return false;
        var otherStep = otherJourney.Step;
        if (otherStep + 1 < otherJourney.Tiles.Count && otherJourney.Tiles[otherStep + 1] == current)
            otherStep++;
        else if (otherJourney.Tiles[otherStep] != current)
            return false;

        // Exchange occupied tiles atomically. This also lets a previously shoved
        // trader reclaim its route without trapping both traders in a narrow channel.
        var origin = _positions[index];
        var destination = _positions[other];
        _positions[index] = destination;
        _positions[other] = origin;
        _occupants[next] = index;
        _occupants[current] = other;
        journey.Step++;
        otherJourney.Step = otherStep;
        SetTraderTravelForm(index, next);
        SetTraderTravelForm(other, current);
        _nextMovementTicks[other] = Math.Max(_nextMovementTicks[other], Tick + GetMovementIntervalTicks(_species[other]));
        RefillApeTraderProvisions(index);
        RefillApeTraderProvisions(other);
        TriggerWolfDenNear(origin, destination, _species[index]);
        TriggerWolfDenNear(destination, origin, _species[other]);
        return true;
    }

    private void SetTraderTravelForm(int index, int tile)
    {
        var form = IsTraderWater(tile) ? CritterSpecies.ApeTraderSailor : CritterSpecies.ApeTrader;
        if (_species[index] == form)
            return;
        _speciesCounts[(int)_species[index]]--;
        _species[index] = form;
        _speciesCounts[(int)form]++;
    }
    private GridPosition? TryMoveApeTrader(int index, IReadOnlySet<GridPosition>? reservedPrey)
    {
        if (!_traderJourneys.TryGetValue(_critterIds[index].Value, out var journey))
            return null;
        if (GetIndex(_positions[index]) != journey.Tiles[journey.Step])
        {
            TryRejoinTraderRoute(index, journey, reservedPrey);
            return null;
        }
        RefillApeTraderProvisions(index);
        if (journey.Step == journey.Tiles.Count - 1)
        {
            RebuildTraderSettlementLinks();
            var path = FindTraderPath(journey.Tiles[^1], journey.Destination, journey.Origin);
            if (path is null)
                return null;
            journey.Origin = journey.Destination;
            journey.Destination = TraderDestinationVillage(path);
            journey.Tiles = path;
            journey.Step = 0;
        }
        var next = journey.Tiles[journey.Step + 1];
        if (reservedPrey?.Contains(GetPosition(next)) is true ||
            !CanTraverseTraderEdge(journey.Tiles[journey.Step], next))
            return null;
        if (TryPassTrader(index, journey, next, reservedPrey))
            return null;
        // Change only the form and species count: identity, provisions and metabolism
        // schedule survive boarding. General evolution resets too much state here.
        var form = IsTraderWater(next) ? CritterSpecies.ApeTraderSailor : CritterSpecies.ApeTrader;
        var oldForm = _species[index];
        _species[index] = form;
        if (!CanEnterOrShoveMovementBlocker(index, next, reservedPrey))
        {
            _species[index] = oldForm;
            return null;
        }
        if (form != oldForm)
        {
            _speciesCounts[(int)oldForm]--;
            _speciesCounts[(int)form]++;
        }
        MoveCritter(index, next, GetPosition(next), activateTeleporter: false);
        journey.Step++;

        RefillApeTraderProvisions(index);
        return null;
    }
}
