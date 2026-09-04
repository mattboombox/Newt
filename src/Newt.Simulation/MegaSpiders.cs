namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int MegaSpiderPerceptionRadius = 6;
    private readonly Dictionary<int, int> _megaSpiderWebHomes = [];
    private readonly Dictionary<int, int> _megaSpiderWebFood = [];

    public int MegaSpiderWebCount => _megaSpiderWebFood.Count;

    public int? GetMegaSpiderWebFood(GridPosition position) =>
        Contains(position) && _megaSpiderWebFood.TryGetValue(GetIndex(position), out var food)
            ? food
            : null;

    internal int GetMegaSpiderWebAssociatedSpiderCount(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        return _megaSpiderWebHomes.Count(pair => pair.Value == tileIndex);
    }

    public GridPosition? GetMegaSpiderWeb(CritterId spiderId) =>
        spiderId.IsValid && _megaSpiderWebHomes.TryGetValue(spiderId.Value, out var tileIndex) &&
            _megaSpiderWebFood.ContainsKey(tileIndex)
                ? GetPosition(tileIndex)
                : null;

    internal bool TryCreateMegaSpiderWeb(CritterId spiderId, GridPosition position)
    {
        if (!spiderId.IsValid || !Contains(position) ||
            !_critterIndicesById.TryGetValue(spiderId.Value, out var spiderIndex) ||
            _species[spiderIndex] is not CritterSpecies.MegaSpider ||
            _megaSpiderWebHomes.ContainsKey(spiderId.Value))
        {
            return false;
        }

        var tileIndex = GetIndex(position);
        var occupant = _occupants[tileIndex];
        if (!IsValidMegaSpiderWebTile(tileIndex) ||
            _megaSpiderWebFood.ContainsKey(tileIndex) ||
            _wolfDenCharges.ContainsKey(tileIndex) ||
            _teleporters.Contains(tileIndex) ||
            _apeStructures.ContainsKey(tileIndex) ||
            (occupant >= 0 && occupant != spiderIndex))
        {
            return false;
        }

        _megaSpiderWebFood.Add(tileIndex, 0);
        _megaSpiderWebHomes[spiderId.Value] = tileIndex;
        return true;
    }

    public bool IsCritterCaughtInMegaSpiderWeb(CritterId critterId) =>
        critterId.IsValid && _critterIndicesById.TryGetValue(critterId.Value, out var critterIndex) &&
        IsCaughtInMegaSpiderWeb(critterIndex);

    public bool RemoveMegaSpiderWebAt(GridPosition position)
    {
        if (!Contains(position))
        {
            return false;
        }

        var tileIndex = GetIndex(position);
        if (!_megaSpiderWebFood.Remove(tileIndex))
        {
            return false;
        }

        foreach (var spiderId in _megaSpiderWebHomes
            .Where(pair => pair.Value == tileIndex)
            .Select(pair => pair.Key)
            .ToArray())
        {
            _megaSpiderWebHomes.Remove(spiderId);
        }
        return true;
    }

    private void AdvanceMegaSpiderWebs()
    {
        foreach (var webTile in _megaSpiderWebFood.Keys.ToArray())
        {
            var hasLivingOwner = _megaSpiderWebHomes.Any(pair =>
                pair.Value == webTile &&
                _critterIndicesById.TryGetValue(pair.Key, out var spiderIndex) &&
                _species[spiderIndex] is CritterSpecies.MegaSpider);
            if (!hasLivingOwner || !IsValidMegaSpiderWebTile(webTile))
            {
                RemoveMegaSpiderWebAt(GetPosition(webTile));
            }
        }
    }

    private void RemoveInvalidMegaSpiderWebAt(GridPosition position)
    {
        var tileIndex = GetIndex(position);
        if (_megaSpiderWebFood.ContainsKey(tileIndex) && !IsValidMegaSpiderWebTile(tileIndex))
        {
            RemoveMegaSpiderWebAt(position);
        }
    }

    private bool IsValidMegaSpiderWebTile(int tileIndex) =>
        tileIndex >= 0 && tileIndex < _terrain.Length &&
        _surfaceCovers[tileIndex] is SurfaceCover.None &&
        _surfaceWater[tileIndex] is SurfaceWaterKind.None &&
        _terrain[tileIndex] is Terrain.Beach or Terrain.Lowlands or
            Terrain.Canyon or Terrain.Trench or Terrain.Plains or Terrain.Hills;

    private bool TryCreateMegaSpiderWeb(int spiderIndex)
    {
        var spiderId = _critterIds[spiderIndex].Value;
        if (_megaSpiderWebHomes.TryGetValue(spiderId, out var existingTile) &&
            _megaSpiderWebFood.ContainsKey(existingTile))
        {
            return false;
        }
        _megaSpiderWebHomes.Remove(spiderId);

        var current = _positions[spiderIndex];
        Span<GridPosition> candidates = stackalloc GridPosition[MovementDirections.Length + 1];
        candidates[0] = current;
        for (var index = 0; index < MovementDirections.Length; index++)
        {
            var direction = MovementDirections[index];
            candidates[index + 1] = new GridPosition(
                Mod(current.X + direction.X, Width),
                current.Y + direction.Y);
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Y < 0 || candidate.Y >= Height)
            {
                continue;
            }
            if (TryCreateMegaSpiderWeb(_critterIds[spiderIndex], candidate))
            {
                return true;
            }
        }

        return false;
    }

    private void DetachMegaSpiderFromWeb(int spiderId)
    {
        if (_megaSpiderWebHomes.Remove(spiderId, out var webTile))
        {
            _megaSpiderWebFood.Remove(webTile);
        }
    }

    private bool IsCaughtInMegaSpiderWeb(int critterIndex)
    {
        if (_species[critterIndex] is CritterSpecies.MegaSpider or
            CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.ApeWarrior or
            CritterSpecies.ApeChieftain)
        {
            return false;
        }
        return _megaSpiderWebFood.ContainsKey(GetIndex(_positions[critterIndex]));
    }

    private bool TryFeedMegaSpiderFromWeb(int spiderIndex, CritterNutrition nutrition)
    {
        var spiderId = _critterIds[spiderIndex].Value;
        if (!_megaSpiderWebHomes.TryGetValue(spiderId, out var webTile) ||
            !_megaSpiderWebFood.TryGetValue(webTile, out var storedFood) || storedFood <= 0)
        {
            return false;
        }

        var consumed = Math.Min(nutrition.MetabolismCost, storedFood);
        _megaSpiderWebFood[webTile] = storedFood - consumed;
        _energy[spiderIndex] = Math.Min(nutrition.MaximumEnergy, _energy[spiderIndex] + consumed);
        return true;
    }

    private GridPosition? TryMoveMegaSpider(
        int spiderIndex,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var spiderId = _critterIds[spiderIndex].Value;
        if (_megaSpiderWebHomes.TryGetValue(spiderId, out var webTile) &&
            _megaSpiderWebFood.ContainsKey(webTile))
        {
            var trappedIndex = _occupants[webTile];
            if (trappedIndex >= 0 && trappedIndex != spiderIndex &&
                _species[trappedIndex] is not CritterSpecies.MegaSpider)
            {
                var webPosition = GetPosition(webTile);
                if (WrappedManhattanDistance(_positions[spiderIndex], webPosition) <= 1)
                {
                    return webPosition;
                }
                return TryMoveTowardMegaSpiderWeb(spiderIndex, webTile, reservedPrey);
            }
        }

        return TryMoveHunter(spiderIndex, MegaSpiderPerceptionRadius, reservedPrey);
    }

    private bool TryStoreCaughtPrey(int spiderIndex, int preyIndex)
    {
        var spiderId = _critterIds[spiderIndex].Value;
        var webTile = GetIndex(_positions[preyIndex]);
        if (!_megaSpiderWebHomes.TryGetValue(spiderId, out var homeTile) ||
            homeTile != webTile || !_megaSpiderWebFood.ContainsKey(webTile) ||
            _species[preyIndex] is CritterSpecies.MegaSpider)
        {
            return false;
        }

        var preyPosition = _positions[preyIndex];
        var foodEnergy = CritterNutritions.Get(_species[preyIndex]).FoodEnergy;
        RemoveCritterAt(preyPosition);
        if (!_megaSpiderWebFood.TryGetValue(webTile, out var storedFood) ||
            !_critterIndicesById.TryGetValue(spiderId, out spiderIndex))
        {
            return true;
        }

        _megaSpiderWebFood[webTile] = checked(storedFood + foodEnergy);
        if (_occupants[webTile] < 0)
        {
            MoveCritter(spiderIndex, webTile, GetPosition(webTile));
        }
        _preyTargets[spiderIndex] = -1;
        return true;
    }

    private GridPosition? TryMoveTowardMegaSpiderWeb(
        int spiderIndex,
        int webTile,
        IReadOnlySet<GridPosition>? reservedPrey)
    {
        var current = _positions[spiderIndex];
        var target = GetPosition(webTile);
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
            var candidateTile = GetIndex(candidate);
            if (candidateTile == webTile && _occupants[candidateTile] >= 0)
            {
                continue;
            }
            if (!CanLiveOn(CritterSpecies.MegaSpider, candidateTile) ||
                (_occupants[candidateTile] >= 0 &&
                    !CanShoveMovementBlocker(spiderIndex, candidateTile, reservedPrey)))
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
            var destinationTile = GetIndex(best.Value);
            if (_occupants[destinationTile] >= 0 &&
                !TryShoveMovementBlocker(spiderIndex, destinationTile, reservedPrey))
            {
                return null;
            }
            MoveCritter(spiderIndex, destinationTile, best.Value);
        }
        return null;
    }
}
