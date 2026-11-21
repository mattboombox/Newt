namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private const int ApeDefenseSquadSize = ApeWarriorsPerMilitaryDistrict;
    private const int ApeDefenseFormationDistance = 2;

    private sealed class ApeDefenseSquad(int leaderId, int number)
    {
        public int LeaderId { get; } = leaderId;
        public int Number { get; } = number;
        public CritterId Target { get; set; }
        public bool SearchedForTarget { get; set; }
    }

    private Dictionary<int, Dictionary<int, ApeDefenseSquad>>? _villageDefenseSquads;

    private Dictionary<int, ApeDefenseSquad> BuildVillageDefenseSquads(int village) =>
        CreateVillageSquadRoster(village, includeWar: false);

    private Dictionary<int, ApeDefenseSquad> CreateVillageSquadRoster(int village, bool includeWar)
    {
        var escorts = GetChieftainEscort(GetPosition(village));
        var squads = new Dictionary<int, ApeDefenseSquad>();
        var warriors = new List<int>();
        foreach (var (id, _) in _apeVillageHomes.Where(pair => pair.Value == village).OrderBy(pair => pair.Key))
        {
            if (!_critterIndicesById.TryGetValue(id, out var index) ||
                _species[index] is not CritterSpecies.ApeWarrior || (!includeWar && TryGetApeWar(index, out _)) ||
                Array.Exists(escorts, escort => escort.Value == id))
            {
                continue;
            }

            warriors.Add(id);
        }
        // Give each squad a veteran before assigning a second veteran to any squad.
        var ordered = warriors.OrderByDescending(id => IsVeteranWarrior(_critterIndicesById[id]))
            .ThenBy(id => id).ToArray();
        var leaders = ordered.Take((ordered.Length + ApeDefenseSquadSize - 1) / ApeDefenseSquadSize)
            .Select((id, i) => new ApeDefenseSquad(id, i + 1)).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            squads.Add(ordered[i], leaders[i % leaders.Length]);
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

    private bool IsDefenseSquadLeaderOf(int mover, int blocker) =>
        _species[mover] is CritterSpecies.ApeWarrior && _species[blocker] is CritterSpecies.ApeWarrior &&
        TryGetVillageDefenseSquad(mover, out _, out var squad) && squad.LeaderId == _critterIds[mover].Value &&
        TryGetVillageDefenseSquad(blocker, out _, out var followerSquad) && followerSquad.LeaderId == squad.LeaderId;

    private bool CanSquadLeaderSwap(int mover, int destination, IReadOnlySet<GridPosition>? reserved) =>
        _occupants[destination] is var blocker && blocker >= 0 && IsDefenseSquadLeaderOf(mover, blocker) &&
        CanSwapPastMovementBlocker(mover, destination, reserved);

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

            if (index == leader)
            {
                TryMoveTowardApeStructure(index, targetTile, reservedPrey);
                return true;
            }
        }
        else
        {
            prey = FindHunterPrey(index, CritterSpecies.ApeWarrior, 1, reservedPrey, defenseCenter: home);
            if (prey is not null)
                return true;
        }

        if (index == leader)
        {
            MoveVillagePerimeterPatrol(index, village, squad.Number, reservedPrey);
        }
        else if (WrappedManhattanDistance(current, _positions[leader]) > ApeDefenseFormationDistance)
        {
            TryMoveAlongSurfacePath(index, GetIndex(_positions[leader]), reservedPrey, stopAdjacent: true);
        }
        return true;
    }
}
