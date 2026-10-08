namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, ApeWar> _lichWars = [];

    private const int LichAttackWarDeclarationChancePercent = 25;

    private bool TryDeclareWarOnAttackingLich(int attacker, int victim)
    {
        // Run only on an actual attack; reject ineligible participants with
        // constant-time lookups before rolling or building any war routes.
        if (!LifeEnabled || _species[attacker] is not CritterSpecies.Lich ||
            !(IsLivingApe(_species[victim]) || _species[victim] is CritterSpecies.Dog or
                CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor) ||
            !_apeVillageHomes.TryGetValue(_critterIds[victim].Value, out var village) ||
            !IsWarVillage(village) || _apeWars.ContainsKey(village) ||
            _lichWars.ContainsKey(_critterIds[attacker].Value) ||
            NextInt(100) >= LichAttackWarDeclarationChancePercent)
            return false;

        return TryStartLichWar(_critterIds[attacker].Value, GetPosition(village), lichAttacking: false);
    }

    private int GetLichAt(GridPosition position) => Contains(position) &&
        _occupants[GetIndex(position)] is var index && index >= 0 && _species[index] is CritterSpecies.Lich
            ? _critterIds[index].Value : -1;

    public GridPosition? GetWarToolParticipant(GridPosition position) =>
        GetLichAt(position) >= 0 ? position : GetApeStructureVillage(position);

    private bool IsLichArmyMember(int index, int lich) =>
        (_critterIds[index].Value == lich && _species[index] is CritterSpecies.Lich) ||
        (_lichArmy.TryGetValue(_critterIds[index].Value, out var leader) && leader == lich &&
            _species[index] is CritterSpecies.UndeadApe or CritterSpecies.UndeadWarrior);

    private IEnumerable<int> LichWarArmy(ApeWar war) => Enumerable.Range(0, _count)
        .Where(index => IsLichArmyMember(index, war.LichId!.Value));
    private IEnumerable<int> LichWarVillage(ApeWar war) => _apeVillageHomes
        .Where(pair => pair.Value == war.Attacker && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            (IsLichPrey(_species[index]) || _species[index] is CritterSpecies.Dog))
        .Select(pair => _critterIndicesById[pair.Key]);

    private bool TryGetLichWar(int index, out ApeWar war)
    {
        war = null!;
        var id = _critterIds[index].Value;
        if ((_lichWars.TryGetValue(id, out war!) ||
            (_lichArmy.TryGetValue(id, out var lich) && _lichWars.TryGetValue(lich, out war!))) &&
            war.LichId is { } leader && IsLichArmyMember(index, leader))
            return true;
        return _apeVillageHomes.TryGetValue(id, out var village) &&
            _apeWars.TryGetValue(village, out war!) && war.LichId is not null;
    }

    private bool AreLichWarEnemies(int first, int second) => TryGetLichWar(first, out var war) &&
        (IsLichArmyMember(first, war.LichId!.Value)
            ? _apeVillageHomes.TryGetValue(_critterIds[second].Value, out var village) && village == war.Attacker
            : IsLichArmyMember(second, war.LichId!.Value));

    private bool TryStartLichWar(int lich, GridPosition building, bool lichAttacking)
    {
        if (!LifeEnabled || _lichWars.ContainsKey(lich) ||
            GetApeStructureVillage(building) is not { } position)
            return false;
        var village = GetIndex(position);
        if (_apeWars.ContainsKey(village) || !IsWarVillage(village) ||
            !_critterIndicesById.TryGetValue(lich, out var leader))
            return false;
        var distances = BuildWarDistances(GetIndex(_positions[leader]));
        if (distances[village] < 0 || GetApeVillageResidentCountByTile(village) == 0)
            return false;
        var war = new ApeWar(village, -lich, distances, true) { LichId = lich, LichAttacking = lichAttacking };
        if (!_barbarianVillageTiles.Contains(village))
        {
            var unemployed = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) && CanRecruitApe(index) && _species[index] is CritterSpecies.Ape)
                .Select(pair => pair.Key).Order().ToArray();
            foreach (var id in unemployed.Take((int)Math.Ceiling(unemployed.Length * 0.9)))
            {
                ChangeCritterSpecies(_critterIndicesById[id], CritterSpecies.ApeWarrior, true, true);
                war.Levies.Add(id);
            }
        }
        war.AttackingWarriors.UnionWith(_apeVillageHomes.Where(pair => pair.Value == village &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) && _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard)
            .Select(pair => pair.Key));
        _apeWars[village] = _lichWars[lich] = war;
        RecruitApeCultureBombers(war, village);
        RefreshLichWarRoutes(war);
        return true;
    }

    private void RefreshLichWarRoutes(ApeWar war)
    {
        war.Distances = BuildWarDistances(LichWarArmy(war).Select(index => GetIndex(_positions[index])));
        war.BlueDistances = BuildWarDistances(LichWarVillage(war).Select(index => GetIndex(_positions[index])));
    }

    private void AdvanceLichWar(ApeWar war)
    {
        if (!IsWarVillage(war.Attacker) || !LichWarVillage(war).Any() || !LichWarArmy(war).Any())
            EndApeWar(war);
        else if (Tick % TicksPerSecond == 0)
            RefreshLichWarRoutes(war);
    }

    private GridPosition? TryMoveLichWarMember(int index, ApeWar war, IReadOnlySet<GridPosition>? reserved)
    {
        var current = GetIndex(_positions[index]);
        var neighbors = RoadNeighbors(current).ToArray();
        foreach (var tile in neighbors)
            if (_occupants[tile] is var enemy && enemy >= 0 && AreLichWarEnemies(index, enemy) &&
                CanCritterLiveOn(index, tile) && reserved?.Contains(GetPosition(tile)) is not true)
                return GetPosition(tile);
        var distances = IsLichArmyMember(index, war.LichId!.Value) ? war.BlueDistances : war.Distances;
        int Distance(int tile) => distances[tile] < 0 ? int.MaxValue : distances[tile];
        foreach (var tile in neighbors.Where(tile => CanCritterLiveOn(index, tile) &&
            reserved?.Contains(GetPosition(tile)) is not true && Distance(tile) < Distance(current))
            .OrderBy(Distance).ThenBy(tile => tile))
        {
            if (_occupants[tile] >= 0 && (AreLichWarEnemies(index, _occupants[tile]) ||
                !TryShoveMovementBlocker(index, tile, reserved)))
                continue;
            MoveCritter(index, tile, GetPosition(tile), activateTeleporter: false);
            return null;
        }
        var step = FindWarSidestep(index, current, Distance, reserved);
        if (step >= 0)
            MoveCritter(index, step, GetPosition(step), activateTeleporter: false);
        return null;
    }
}
