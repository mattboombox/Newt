namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    internal const int VampireColonyVillageRadius = 8;
    private sealed class VampireColony(int origin, int huntingVillage, int village, int target)
    {
        public int Origin = origin, Village = village, Target = target;
        public readonly int HuntingVillage = huntingVillage;
        public long NextSearchTick;
    }
    private readonly Dictionary<int, VampireColony> _vampireColonists = [];

    public bool TrySendVampireColonist(GridPosition lair) => TrySendVampireColonist(lair, out _);

    public bool TrySendVampireColonist(GridPosition lair, out string status)
    {
        status = "Select an active vampire lair";
        if (!LifeEnabled || !Contains(lair) || !_vampireLairs.Contains(GetIndex(lair)))
            return false;
        var origin = GetIndex(lair);
        var huntingVillage = _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village &&
                IsRoadVillage(pair.Key) && GetApeVillageResidentCountByTile(pair.Key) > 0)
            .Select(pair => pair.Key)
            .OrderBy(tile => WrappedManhattanDistance(lair, GetPosition(tile)))
            .ThenBy(tile => tile).DefaultIfEmpty(-1).First();
        if (_vampireColonists.Values.Any(colony => colony.Origin == origin))
        {
            status = "This lair already has a traveling colonist";
            return false;
        }
        var scout = _vampireHomes.Where(pair => pair.Value == origin && _critterIndicesById.ContainsKey(pair.Key))
            .Select(pair => _critterIndicesById[pair.Key]).DefaultIfEmpty(-1).First();
        if (scout < 0)
        {
            status = "This lair has no resident vampire";
            return false;
        }
        var spawn = RoadNeighbors(origin).FirstOrDefault(tile => _occupants[tile] < 0 &&
            CanLiveOn(CritterSpecies.Vampire, tile) && !_apeStructures.ContainsKey(tile) &&
            !_vampireLairs.Contains(tile) && !_teleporters.Contains(tile), -1);
        if (spawn < 0)
        {
            status = "No free spawn tile beside this lair";
            return false;
        }
        if (!_apeStructures.Any(pair => pair.Value is ApeStructureKind.Village &&
            pair.Key != huntingVillage && IsRoadVillage(pair.Key)))
        {
            status = "Needs another regular village";
            return false;
        }
        if (!FindVampireColonySite(scout, spawn, huntingVillage, out var village, out var target, out var path))
        {
            status = "No reachable colony site near another regular village";
            return false;
        }
        var id = AddCritter(CritterSpecies.Vampire, GetPosition(spawn));
        _energy[_critterIndicesById[id.Value]] = ApeSettlerInitialEnergy;
        _vampireColonists[id.Value] = new(origin, huntingVillage, village, target);
        _apeSettlerPaths[id.Value] = (target, path!);
        status = "Vampire colonist launched!";
        return true;
    }

    internal bool TryLaunchVampireColony(int lair)
    {
        if (!LifeEnabled || !NaturalEventsEnabled || Tick == 0 || Tick % ApeSettlerCheckIntervalTicks != 0 ||
            !_vampireLairs.Contains(lair) || GetVampireLairPopulation(GetPosition(lair)) < VampireLairCapacity ||
            _vampireColonists.Values.Any(colony => colony.Origin == lair) ||
            NextInt(100) >= ApeSettlerChancePercent)
            return false;
        return TrySendVampireColonist(GetPosition(lair));
    }

    private void AdvanceVampireColonies()
    {
        if (Tick % ApeSettlerCheckIntervalTicks != 0)
            return;
        foreach (var lair in _vampireLairs.ToArray())
            TryLaunchVampireColony(lair);
    }

    private bool IsVampireColonySite(int index, int village, int tile) =>
        tile >= 0 && tile < _terrain.Length && IsRoadVillage(village) &&
        WrappedManhattanDistance(GetPosition(tile), GetPosition(village)) is >= 2 and <= VampireColonyVillageRadius &&
        (_occupants[tile] < 0 || _occupants[tile] == index) &&
        CanLiveOn(CritterSpecies.Vampire, tile) && _surfaceWater[tile] is SurfaceWaterKind.None &&
        !_apeStructures.ContainsKey(tile) && !_wolfDenCharges.ContainsKey(tile) &&
        !_megaSpiderWebFood.ContainsKey(tile) && !_teleporters.Contains(tile) &&
        _vampireLairs.All(lair => WrappedManhattanDistance(GetPosition(tile), GetPosition(lair)) >= 8);

    private bool FindVampireColonySite(int index, int start, int huntingVillage, out int village, out int target, out Queue<int>? path)
    {
        village = target = -1;
        path = null;
        foreach (var home in _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village &&
                pair.Key != huntingVillage && IsRoadVillage(pair.Key))
            .Select(pair => pair.Key).OrderBy(tile => WrappedManhattanDistance(GetPosition(start), GetPosition(tile))))
        {
            var center = GetPosition(home);
            var sites = new HashSet<int>();
            for (var dy = -VampireColonyVillageRadius; dy <= VampireColonyVillageRadius; dy++)
            for (var dx = -VampireColonyVillageRadius; dx <= VampireColonyVillageRadius; dx++)
            {
                var y = center.Y + dy;
                if (y < 0 || y >= Height)
                    continue;
                var tile = GetIndex(new GridPosition(Mod(center.X + dx, Width), y));
                if (IsVampireColonySite(index, home, tile) && IsDifferentVampireHuntingArea(home, tile, huntingVillage))
                    sites.Add(tile);
            }
            foreach (var tile in sites.OrderBy(tile => WrappedManhattanDistance(GetPosition(start), GetPosition(tile))).ThenBy(tile => tile))
            {
                var route = FindApeSettlerPath(index, start, tile);
                if (route is null)
                    continue;
                village = home;
                target = tile;
                path = route;
                return true;
            }
        }
        return false;
    }

    private bool IsDifferentVampireHuntingArea(int village, int tile, int huntingVillage) =>
        village != huntingVillage && (huntingVillage < 0 ||
            WrappedManhattanDistance(GetPosition(tile), GetPosition(village)) <
            WrappedManhattanDistance(GetPosition(tile), GetPosition(huntingVillage)));

    private GridPosition? TryMoveVampire(int index, IReadOnlySet<GridPosition>? reserved)
    {
        var id = _critterIds[index].Value;
        if (!_vampireColonists.TryGetValue(id, out var colony))
            return TryMoveHunter(index, ApePerceptionRadius, reserved);
        if (!IsVampireColonySite(index, colony.Village, colony.Target) ||
            !IsDifferentVampireHuntingArea(colony.Village, colony.Target, colony.HuntingVillage))
        {
            if (Tick < colony.NextSearchTick)
                return null;
            colony.NextSearchTick = Tick + ApeTraderRecoveryRetryTicks;
            if (!FindVampireColonySite(index, GetIndex(_positions[index]), colony.HuntingVillage, out colony.Village, out colony.Target, out var path))
            {
                _apeSettlerPaths.Remove(id);
                return null;
            }
            _apeSettlerPaths[id] = (colony.Target, path!);
        }
        if (GetIndex(_positions[index]) != colony.Target)
        {
            MoveApeSettlerToward(index, colony.Target);
            return null;
        }
        _vampireLairs.Add(colony.Target);
        _vampireHomes[id] = colony.Target;
        _vampireColonists.Remove(id);
        _apeSettlerPaths.Remove(id);
        _energy[index] = CritterNutritions.Get(CritterSpecies.Vampire).InitialEnergy;
        foreach (var tile in RoadNeighbors(colony.Target))
        {
            if (_occupants[tile] >= 0 || !CanLiveOn(CritterSpecies.Vampire, tile) ||
                _surfaceWater[tile] is not SurfaceWaterKind.None ||
                _apeStructures.ContainsKey(tile) || _vampireLairs.Contains(tile) ||
                _wolfDenCharges.ContainsKey(tile) || _megaSpiderWebFood.ContainsKey(tile) || _teleporters.Contains(tile))
                continue;
            var companion = AddCritter(CritterSpecies.Vampire, GetPosition(tile));
            _vampireHomes[companion.Value] = colony.Target;
            break;
        }
        return null;
    }
}
