namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public int GetApeVillageFarmCount(GridPosition village) => GetApeFoodDistrictCount(GetIndex(village));

    public double GetApeVillageFoodPerMinute(GridPosition village)
    {
        var villageTile = GetIndex(village);
        return _apeAuxiliaryVillages.Where(pair => pair.Value == villageTile &&
                _apeStructures.TryGetValue(pair.Key, out var kind) && IsApeFoodDistrict(kind) &&
                IsApeStructureOperational(GetPosition(pair.Key)))
            .Sum(pair => GetApeStructureProductionPerMinute(GetPosition(pair.Key)) ?? 0);
    }

    // Player placement keeps terrain and connection rules, but bypasses progression and affordability.
    public bool TryPlaceApeBuilding(GridPosition position, ApeStructureKind kind)
    {
        if (!LifeEnabled || !Contains(position))
            return false;
        var tile = GetIndex(position);
        if (kind is ApeStructureKind.Farm)
            kind = IsApeAquacultureTile(tile) ? ApeStructureKind.Aquaculture : _biomes[tile] switch
            {
                Biome.Swamp or Biome.Jungle => ApeStructureKind.RicePaddy,
                Biome.Forest => ApeStructureKind.Orchard,
                _ => ApeStructureKind.Farm,
            };
        if (_occupants[tile] >= 0 || HasBlockingApeStructure(tile) ||
            _teleporters.Contains(tile) || _wolfDenCharges.ContainsKey(tile) ||
            _megaSpiderWebFood.ContainsKey(tile) || !IsValidApeAuxiliaryTile(tile, kind))
            return false;

        foreach (var village in _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village &&
                !_barbarianVillageTiles.Contains(pair.Key))
            .Select(pair => pair.Key).Order().ToArray())
        {
            if (!GetPlayerConnectedVillageTiles(village).Any(connected =>
                ArePlayerBuildingsAdjacent(connected, tile, kind)))
                continue;
            BuildApeStructureAt(village, tile, kind);
            _apeVillageFood[village] = Math.Max(0,
                _apeVillageFood.GetValueOrDefault(village) - GetApeStructureFoodCost(kind));
            _apeVillageWood[village] = Math.Max(0,
                _apeVillageWood.GetValueOrDefault(village) - GetApeStructureWoodCost(kind));
            return true;
        }
        return false;
    }

    private HashSet<int> GetPlayerConnectedVillageTiles(int village)
    {
        var connected = new HashSet<int> { village };
        var pending = new Queue<int>();
        pending.Enqueue(village);
        while (pending.TryDequeue(out var current))
        {
            foreach (var direction in MovementDirections)
            {
                var origin = GetPosition(current);
                var neighbor = new GridPosition(Mod(origin.X + direction.X, Width), origin.Y + direction.Y);
                if (!Contains(neighbor))
                    continue;
                var tile = GetIndex(neighbor);
                if (!connected.Contains(tile) && _apeAuxiliaryVillages.TryGetValue(tile, out var owner) &&
                    owner == village && _apeStructures.TryGetValue(tile, out var kind) &&
                    kind is not ApeStructureKind.Ruin && ArePlayerBuildingsAdjacent(current, tile, kind))
                {
                    connected.Add(tile);
                    pending.Enqueue(tile);
                }
            }
        }
        return connected;
    }

    private bool ArePlayerBuildingsAdjacent(int existing, int candidate, ApeStructureKind kind)
    {
        var first = GetPosition(existing);
        var second = GetPosition(candidate);
        var dx = Math.Abs(first.X - second.X);
        dx = Math.Min(dx, Width - dx);
        var dy = Math.Abs(first.Y - second.Y);
        return dx + dy == 1 || (dx == 1 && dy == 1 &&
            (kind is ApeStructureKind.NavalDistrict ||
                _apeStructures[existing] is ApeStructureKind.NavalDistrict));
    }
}
