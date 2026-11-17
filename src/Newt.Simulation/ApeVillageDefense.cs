namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int ApeDefenseSquadSize = ApeWarriorsPerMilitaryDistrict;
    private const int ApeDefenseFormationDistance = 2;

    private sealed class ApeDefenseSquad(int leaderId)
    {
        public int LeaderId { get; } = leaderId;
        public CritterId Target { get; set; }
        public bool SearchedForTarget { get; set; }
    }

    private Dictionary<int, Dictionary<int, ApeDefenseSquad>>? _villageDefenseSquads;

    private Dictionary<int, ApeDefenseSquad> BuildVillageDefenseSquads(int village)
    {
        var escorts = GetChieftainEscort(GetPosition(village));
        var squads = new Dictionary<int, ApeDefenseSquad>();
        ApeDefenseSquad? squad = null;
        foreach (var (id, _) in _apeVillageHomes.Where(pair => pair.Value == village).OrderBy(pair => pair.Key))
        {
            if (!_critterIndicesById.TryGetValue(id, out var index) ||
                _species[index] is not CritterSpecies.ApeWarrior || TryGetApeWar(index, out _) ||
                Array.Exists(escorts, escort => escort.Value == id))
            {
                continue;
            }

            if (squads.Count % ApeDefenseSquadSize == 0)
                squad = new ApeDefenseSquad(id);
            squads.Add(id, squad!);
        }
        return squads;
    }

    private bool TryGetVillageDefenseSquad(int index, out int village, out ApeDefenseSquad squad)
    {
        village = -1;
        squad = null!;
        var id = _critterIds[index].Value;
        if (_species[index] is not CritterSpecies.ApeWarrior || IsBarbarianApe(index) ||
            !_apeVillageHomes.TryGetValue(id, out village) || !_apeVillageFood.ContainsKey(village))
        {
            return false;
        }

        if (_villageDefenseSquads is null)
            return BuildVillageDefenseSquads(village).TryGetValue(id, out squad!);
        if (!_villageDefenseSquads.TryGetValue(village, out var squads))
        {
            squads = BuildVillageDefenseSquads(village);
            _villageDefenseSquads.Add(village, squads);
        }
        return squads.TryGetValue(id, out squad!);
    }

    private bool CanDefendVillageAgainstWhale(int warrior, int whale) =>
        _species[whale] is CritterSpecies.ToothedWhale &&
        TryGetVillageDefenseSquad(warrior, out var village, out _) &&
        WrappedManhattanDistance(GetPosition(village), _positions[whale]) <= ApeDefenderPerceptionRadius;

    internal bool TryMoveVillageDefenseWarrior(
        int index, IReadOnlySet<GridPosition>? reservedPrey, out GridPosition? prey)
    {
        prey = null;
        if (!TryGetVillageDefenseSquad(index, out var village, out var squad) ||
            !_critterIndicesById.TryGetValue(squad.LeaderId, out var leader))
        {
            return false;
        }

        var home = GetPosition(village);
        var current = _positions[index];
        _preyTargets[index] = -1;
        if (!squad.SearchedForTarget)
        {
            if (FindHunterPrey(leader, CritterSpecies.ApeWarrior, ApeDefenderPerceptionRadius,
                reservedPrey: null, defenseCenter: home) is { } targetPosition)
            {
                squad.Target = _critterIds[_occupants[GetIndex(targetPosition)]];
            }
            squad.SearchedForTarget = true;
        }

        if (squad.Target.IsValid && _critterIndicesById.TryGetValue(squad.Target.Value, out var targetIndex) &&
            WrappedManhattanDistance(home, _positions[targetIndex]) <= ApeDefenderPerceptionRadius &&
            CanPursuePrey(index, targetIndex))
        {
            var target = _positions[targetIndex];
            var targetTile = GetIndex(target);
            _preyTargets[index] = targetTile;
            if (MovementDirections.Any(direction => OffsetTile(current, direction) == target))
            {
                if (reservedPrey?.Contains(target) != true && CanCritterLiveOn(index, targetTile))
                    prey = target;
                return true;
            }

            if (WrappedManhattanDistance(current, _positions[leader]) <= ApeDefenseFormationDistance)
            {
                TryMoveTowardApeStructure(index, targetTile, reservedPrey);
                return true;
            }
        }
        else
        {
            prey = FindHunterPrey(index, CritterSpecies.ApeWarrior, 1, reservedPrey, defenseCenter: home);
            if (prey is not null || TryPrioritizeApeReproduction(index, reservedPrey))
                return true;
        }

        if (index == leader)
        {
            if (WrappedManhattanDistance(current, home) > ApeDefenseFormationDistance)
                TryMoveTowardApeStructure(index, village, reservedPrey);
        }
        else if (WrappedManhattanDistance(current, _positions[leader]) > ApeDefenseFormationDistance)
        {
            TryMoveTowardApeStructure(index, GetIndex(_positions[leader]), reservedPrey);
        }
        return true;
    }
}
