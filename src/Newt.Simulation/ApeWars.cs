namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private sealed class ApeWar(int attacker, int defender, int[] distances, bool bloodWar)
    {
        public int Attacker = attacker, Defender = defender;
        public int[] Distances = distances;
        public bool BloodWar = bloodWar;
        public bool CivilWar;
        public int[] BlueDistances = [];
        public Dictionary<int, CritterSpecies> OriginalRoles = [];
        public HashSet<int> AttackingWarriors = [], DefendingWarriors = [], Levies = [];
    }
    private readonly Dictionary<int, ApeWar> _apeWars = [];
    private readonly Dictionary<int, CritterSpecies?> _returningWarSailors = [];
    public const int VillageWarCheckIntervalTicks = 60 * TicksPerSecond;
    public const int VillageWarCooldownTicks = 5 * 60 * TicksPerSecond;
    public const int VillageWarPopulationThreshold = 50;
    public const int VillageSevereWarPopulationThreshold = 100;
    public const int VillageWarChancePercent = 1;
    private readonly Dictionary<int, long> _villageWarCooldowns = [];

    internal void AdvanceVillageWarOutbreaks()
    {
        if (!LifeEnabled || !NaturalEventsEnabled || Tick < VillageWarCooldownTicks ||
            Tick % VillageWarCheckIntervalTicks != 0) return;
        foreach (var village in _apeStructures.Where(pair => pair.Value == ApeStructureKind.Village)
                     .Select(pair => pair.Key).Order().ToArray())
            TryStartVillageWar(village);
    }

    internal bool TryStartVillageWar(int village)
    {
        bool Eligible(int tile, int minimum) => IsRoadVillage(tile) && !_apeWars.ContainsKey(tile) &&
            Tick >= _villageWarCooldowns.GetValueOrDefault(tile) &&
            _apeVillageHomes.Count(pair => pair.Value == tile &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) && IsLivingApe(_species[index])) >= minimum;
        if (!LifeEnabled || !NaturalEventsEnabled || Tick < VillageWarCooldownTicks ||
            !Eligible(village, VillageWarPopulationThreshold) || NextInt(100) >= VillageWarChancePercent)
            return false;
        // Each variant has an equal one-in-three chance before eligibility checks.
        var variant = NextInt(3);
        var minimum = variant == 0 ? VillageWarPopulationThreshold : VillageSevereWarPopulationThreshold;
        if (!Eligible(village, minimum)) return false;
        if (variant == 2) return TryStartApeCivilWar(GetPosition(village));
        var opponents = _apeStructures.Where(pair => pair.Key != village &&
                pair.Value == ApeStructureKind.Village && Eligible(pair.Key, minimum))
            .Select(pair => pair.Key).Order().ToArray();
        // Try candidates in a random order; unreachable villages must not block reachable ones.
        for (var i = opponents.Length - 1; i >= 0; i--)
        {
            var other = NextInt(i + 1);
            (opponents[i], opponents[other]) = (opponents[other], opponents[i]);
            if (TryStartApeWar(GetPosition(village), GetPosition(opponents[i]), bloodWar: variant == 1))
                return true;
        }
        return false;
    }

    private bool CanWarApeSail(int index) =>
        _species[index] is (CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain) &&
        (TryGetApeWar(index, out _) || _returningWarSailors.ContainsKey(_critterIds[index].Value));

    private bool IsWarTransitTile(int tile) =>
        CanLiveOn(CritterSpecies.ApeWarrior, tile) || CanLiveOn(CritterSpecies.ApeSailor, tile);
    public bool IsApeVillageAtWar(GridPosition position) =>
        GetApeStructureVillage(position) is { } village && _apeWars.ContainsKey(GetIndex(village));

    public bool TryGetApeWarSide(CritterId id, out bool attacking)
    {
        attacking = false;
        if (!_critterIndicesById.TryGetValue(id.Value, out var index) || !TryGetApeWar(index, out var war))
            return false;
        attacking = war.CivilWar ? war.AttackingWarriors.Contains(id.Value) : _apeVillageHomes[id.Value] == war.Attacker;
        return true;
    }

    public bool TryStartApeCivilWar(GridPosition villageBuilding)
    {
        if (!LifeEnabled || GetApeStructureVillage(villageBuilding) is not { } position)
            return false;
        var village = GetIndex(position);
        if (!IsRoadVillage(village) || _apeWars.ContainsKey(village)) return false;
        var residents = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                _species[index] is not (CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor) &&
                IsLivingApe(_species[index]))
            .Select(pair => pair.Key).Order().ToArray();
        if (residents.Length < 2) return false;
        // Shuffle once, then split evenly. Team membership stays fixed as casualties occur.
        for (var i = residents.Length - 1; i > 0; i--)
        {
            var other = NextInt(i + 1);
            (residents[i], residents[other]) = (residents[other], residents[i]);
        }
        var war = new ApeWar(village, village, [], false) { CivilWar = true };
        for (var i = 0; i < residents.Length; i++)
        {
            var id = residents[i];
            var index = _critterIndicesById[id];
            war.OriginalRoles[id] = _species[index];
            (i < residents.Length / 2 ? war.AttackingWarriors : war.DefendingWarriors).Add(id);
            if (_species[index] is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain))
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, preserveEnergy: true, preserveApeVillage: true);
        }
        _apeWars[village] = war;
        RefreshCivilWarRoutes(war);
        return true;
    }

    private void RefreshCivilWarRoutes(ApeWar war)
    {
        IEnumerable<int> Positions(HashSet<int> army) => army.Where(IsSurvivingCivilWarFighter)
            .Select(id => GetIndex(_positions[_critterIndicesById[id]]));
        war.Distances = BuildWarDistances(Positions(war.DefendingWarriors));
        war.BlueDistances = BuildWarDistances(Positions(war.AttackingWarriors));
    }

    public bool TryStartApeWar(GridPosition attackerBuilding, GridPosition defenderBuilding, bool bloodWar = false)
    {
        if (!LifeEnabled || GetApeStructureVillage(attackerBuilding) is not { } attackerPosition ||
            GetApeStructureVillage(defenderBuilding) is not { } defenderPosition)
            return false;
        var attacker = GetIndex(attackerPosition);
        var defender = GetIndex(defenderPosition);
        if (attacker == defender || !IsRoadVillage(attacker) || !IsRoadVillage(defender) ||
            _apeWars.ContainsKey(attacker) || _apeWars.ContainsKey(defender))
            return false;
        var distances = BuildWarDistances(defender);
        if (distances[attacker] < 0)
            return false;
        var residents = new[] { attacker, defender }.Select(village => _apeVillageHomes
            .Where(pair => pair.Value == village && _critterIndicesById.ContainsKey(pair.Key))
            .Select(pair => pair.Key).Order().ToArray()).ToArray();
        if (!residents[0].Any(id => _species[_critterIndicesById[id]] is CritterSpecies.Ape or CritterSpecies.ApeWarrior) ||
            (bloodWar ? residents[1].Length == 0 : !residents[1].Any(id =>
                _species[_critterIndicesById[id]] is CritterSpecies.Ape or CritterSpecies.ApeWarrior)))
            return false;
        var war = new ApeWar(attacker, defender, distances, bloodWar);
        for (var side = 0; side < 2; side++)
        {
            if (bloodWar && side == 1)
            {
                // Keep food and wood production running; chiefs also retain their role.
                foreach (var id in residents[side])
                {
                    var index = _critterIndicesById[id];
                    if (_species[index] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ||
                        !IsLivingApe(_species[index]) ||
                        _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
                            CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack)
                        continue;
                    war.OriginalRoles[id] = _species[index];
                    ChangeCritterSpecies(index, CritterSpecies.ApeWarrior,
                        preserveEnergy: true, preserveApeVillage: true);
                }
                war.DefendingWarriors.UnionWith(residents[side].Where(id =>
                    _species[_critterIndicesById[id]] is CritterSpecies.ApeWarrior));
                continue;
            }
            var unemployed = residents[side].Where(id => _species[_critterIndicesById[id]] is CritterSpecies.Ape).ToArray();
            foreach (var id in unemployed.Take((int)Math.Ceiling(unemployed.Length * 0.9)))
            {
                ChangeCritterSpecies(_critterIndicesById[id], CritterSpecies.ApeWarrior,
                    preserveEnergy: true, preserveApeVillage: true);
                war.Levies.Add(id);
            }
            var army = side == 0 ? war.AttackingWarriors : war.DefendingWarriors;
            army.UnionWith(residents[side].Where(id => _species[_critterIndicesById[id]] is CritterSpecies.ApeWarrior));
        }
        _apeWars[attacker] = _apeWars[defender] = war;
        if (bloodWar) war.Distances = BuildWarDistances(GetBloodWarTargets(war));
        return true;
    }

    private IEnumerable<int> GetBloodWarTargets(ApeWar war) => _apeVillageHomes
        .Where(pair => pair.Value == war.Defender && _critterIndicesById.TryGetValue(pair.Key, out var index) &&
            _species[index] is not (CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor))
        .Select(pair => GetIndex(_positions[_critterIndicesById[pair.Key]]));

    private int[] BuildWarDistances(int target) => BuildWarDistances([target]);

    private int[] BuildWarDistances(IEnumerable<int> targets)
    {
        var distances = new int[_terrain.Length];
        Array.Fill(distances, -1);
        var queue = new Queue<int>();
        foreach (var target in targets.Distinct())
        {
            distances[target] = 0;
            queue.Enqueue(target);
        }
        while (queue.TryDequeue(out var tile))
            foreach (var next in RoadNeighbors(tile))
                if (distances[next] < 0 && IsWarTransitTile(next))
                {
                    distances[next] = distances[tile] + 1;
                    queue.Enqueue(next);
                }
        return distances;
    }

    private bool TryGetApeWar(int index, out ApeWar war)
    {
        war = null!;
        return _apeVillageHomes.TryGetValue(_critterIds[index].Value, out var village) &&
            _apeWars.TryGetValue(village, out war!) && (!war.CivilWar ||
                war.OriginalRoles.ContainsKey(_critterIds[index].Value));
    }

    private bool AreWarEnemies(int first, int second) =>
        TryGetApeWar(first, out var war) &&
        _apeVillageHomes.TryGetValue(_critterIds[first].Value, out var home) &&
        _apeVillageHomes.TryGetValue(_critterIds[second].Value, out var other) &&
        (war.CivilWar
            ? home == other && (war.AttackingWarriors.Contains(_critterIds[first].Value) &&
                war.DefendingWarriors.Contains(_critterIds[second].Value) ||
                war.DefendingWarriors.Contains(_critterIds[first].Value) && war.AttackingWarriors.Contains(_critterIds[second].Value))
            : other == (home == war.Attacker ? war.Defender : war.Attacker));

    private bool CanTargetWarApe(int first, int second) => AreWarEnemies(first, second) &&
        (_species[second] is CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain ||
            TryGetApeWar(first, out var war) && war.BloodWar &&
            _apeVillageHomes[_critterIds[first].Value] == war.Attacker);

    internal void AdvanceApeWars()
    {
        foreach (var war in _apeWars.Values.Distinct().ToArray())
        {
            var attackers = war.AttackingWarriors.Count(id => war.CivilWar ? IsSurvivingCivilWarFighter(id) : IsSurvivingWarrior(id));
            var defenders = war.DefendingWarriors.Count(id => war.CivilWar ? IsSurvivingCivilWarFighter(id) : IsSurvivingWarrior(id));
            var attackerLost = !IsRoadVillage(war.Attacker) || attackers * 10 <= war.AttackingWarriors.Count;
            var defenderLost = war.BloodWar
                ? !IsRoadVillage(war.Defender) || !GetBloodWarTargets(war).Any()
                : !IsRoadVillage(war.Defender) || defenders * 10 <= war.DefendingWarriors.Count;
            if (!attackerLost && !defenderLost)
            {
                if (war.CivilWar && Tick % TicksPerSecond == 0)
                    RefreshCivilWarRoutes(war);
                else if (!war.CivilWar && Tick % ((war.BloodWar ? 1 : 30) * TicksPerSecond) == 0)
                    war.Distances = war.BloodWar ? BuildWarDistances(GetBloodWarTargets(war)) : BuildWarDistances(war.Defender);
                continue;
            }
            // Simultaneous defeat ends in a draw; there is no victor to receive spoils.
            if (!war.CivilWar && attackerLost != defenderLost)
            {
                var loser = attackerLost ? war.Attacker : war.Defender;
                var winner = attackerLost ? war.Defender : war.Attacker;
                _apeVillageFood[winner] = _apeVillageFood.GetValueOrDefault(winner) + _apeVillageFood.GetValueOrDefault(loser);
                _apeVillageWood[winner] = _apeVillageWood.GetValueOrDefault(winner) + _apeVillageWood.GetValueOrDefault(loser);
                _apeVillageFood[loser] = _apeVillageWood[loser] = 0;
            }
            EndApeWar(war);
        }
    }

    /// <summary>Ends either war variant without declaring a winner or transferring stores.</summary>
    public bool TryStopApeWar(GridPosition villageBuilding)
    {
        if (GetApeStructureVillage(villageBuilding) is not { } village ||
            !_apeWars.TryGetValue(GetIndex(village), out var war))
            return false;
        EndApeWar(war);
        return true;
    }

    private void EndApeWar(ApeWar war)
    {
        _villageWarCooldowns[war.Attacker] = _villageWarCooldowns[war.Defender] = Tick + VillageWarCooldownTicks;
        foreach (var pair in _apeVillageHomes)
            if ((pair.Value == war.Attacker || pair.Value == war.Defender) &&
                _critterIndicesById.TryGetValue(pair.Key, out var sailor) && CanWarApeSail(sailor) &&
                !CanLiveOn(_species[sailor], GetIndex(_positions[sailor])))
                _returningWarSailors[pair.Key] = war.OriginalRoles.TryGetValue(pair.Key, out var role)
                    ? role : war.Levies.Contains(pair.Key) ? CritterSpecies.Ape : null;
        _apeWars.Remove(war.Attacker);
        _apeWars.Remove(war.Defender);
        foreach (var pair in _apeVillageHomes)
            if (pair.Value == war.Attacker || pair.Value == war.Defender)
                _chieftainAttackTargets.Remove(pair.Key);
        foreach (var id in war.Levies)
            if (!_returningWarSailors.ContainsKey(id) && _critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.ApeWarrior)
                ChangeCritterSpecies(index, CritterSpecies.Ape, preserveEnergy: true, preserveApeVillage: true);
        foreach (var (id, role) in war.OriginalRoles)
            if (!_returningWarSailors.ContainsKey(id) && _critterIndicesById.TryGetValue(id, out var index) &&
                _species[index] is CritterSpecies.ApeWarrior && role != CritterSpecies.ApeWarrior)
                ChangeCritterSpecies(index, role, preserveEnergy: true, preserveApeVillage: true);
    }

    private bool IsSurvivingWarrior(int id) => _critterIndicesById.TryGetValue(id, out var index) &&
        _species[index] is CritterSpecies.ApeWarrior;

    private bool IsSurvivingCivilWarFighter(int id) => _critterIndicesById.TryGetValue(id, out var index) &&
        _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain;

    private GridPosition? TryReturnWarSailor(int index, IReadOnlySet<GridPosition>? reserved)
    {
        var id = _critterIds[index].Value;
        var current = GetIndex(_positions[index]);
        if (CanLiveOn(_species[index], current))
        {
            if (_returningWarSailors.Remove(id, out var role) && role is { } originalRole)
                ChangeCritterSpecies(index, originalRole, preserveEnergy: true, preserveApeVillage: true);
            return null;
        }
        // Search outward for a reachable landing, remembering the first step of each route.
        var queue = new Queue<(int Tile, int Step)>();
        var seen = new HashSet<int> { current };
        queue.Enqueue((current, -1));
        while (queue.TryDequeue(out var route))
            foreach (var next in RoadNeighbors(route.Tile))
            {
                if (!seen.Add(next) || !IsWarTransitTile(next) || _occupants[next] >= 0 ||
                    reserved?.Contains(GetPosition(next)) is true) continue;
                var step = route.Step < 0 ? next : route.Step;
                if (CanLiveOn(_species[index], next))
                {
                    MoveCritter(index, step, GetPosition(step), activateTeleporter: false);
                    if (CanLiveOn(_species[index], step))
                    {
                        if (_returningWarSailors.Remove(id, out var role) && role is { } originalRole)
                            ChangeCritterSpecies(index, originalRole, preserveEnergy: true, preserveApeVillage: true);
                    }
                    return null;
                }
                queue.Enqueue((next, step));
            }
        return null;
    }

    private GridPosition? TryMoveWarApe(int index, IReadOnlySet<GridPosition>? reserved)
    {
        if (!TryGetApeWar(index, out var war))
            return null;
        var current = GetIndex(_positions[index]);
        var neighbors = RoadNeighbors(current).ToArray();
        foreach (var tile in neighbors)
            if (_occupants[tile] is var enemy && enemy >= 0 && CanTargetWarApe(index, enemy) &&
                CanCritterLiveOn(index, tile) && reserved?.Contains(GetPosition(tile)) is not true)
                return GetPosition(tile);
        // Defenders intercept nearby invaders; otherwise hold near their village.
        var target = -1;
        var nearest = 7;
        var position = _positions[index];
        for (var dy = -6; dy <= 6; dy++)
            for (var dx = -6; dx <= 6; dx++)
            {
                var candidate = new GridPosition(Mod(position.X + dx, Width), position.Y + dy);
                if (!Contains(candidate)) continue;
                var tile = GetIndex(candidate);
                var enemy = _occupants[tile];
                var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (enemy >= 0 && distance < nearest && CanTargetWarApe(index, enemy))
                { target = tile; nearest = distance; }
            }
        var defending = !war.CivilWar && _apeVillageHomes[_critterIds[index].Value] == war.Defender;
        var distances = war.CivilWar && war.DefendingWarriors.Contains(_critterIds[index].Value)
            ? war.BlueDistances : war.Distances;
        if (target < 0 && defending && (war.BloodWar || war.Distances[current] <= 4))
            return null;
        int Distance(int tile) => target >= 0 ? WrappedManhattanDistance(GetPosition(tile), GetPosition(target)) :
            distances[tile] < 0 ? int.MaxValue : distances[tile];
        var best = neighbors.Where(tile => CanCritterLiveOn(index, tile) && _occupants[tile] < 0 &&
                reserved?.Contains(GetPosition(tile)) is not true && Distance(tile) < Distance(current))
            .OrderBy(Distance).ThenBy(_ => NextInt(8)).DefaultIfEmpty(-1).First();
        if (best < 0 && neighbors.Any(tile => Distance(tile) < Distance(current) &&
                CanCritterLiveOn(index, tile) && _occupants[tile] is var blocker && blocker >= 0 &&
                _apeVillageHomes.TryGetValue(_critterIds[blocker].Value, out var home) &&
                home == _apeVillageHomes[_critterIds[index].Value] && !AreWarEnemies(index, blocker)))
            best = FindWarSidestep(index, current, Distance, reserved);
        if (best >= 0)
            MoveCritter(index, best, GetPosition(best), activateTeleporter: false);
        return null;
    }

    private int FindWarSidestep(int index, int current, Func<int, int> distance,
        IReadOnlySet<GridPosition>? reserved)
    {
        // Only take a lateral step when a short open route actually leads forward.
        // Keeping the same distance until then avoids retreat/advance oscillation.
        var baseline = distance(current);
        var queue = new Queue<(int Tile, int FirstStep, int Depth)>();
        var seen = new HashSet<int> { current };
        queue.Enqueue((current, -1, 0));
        while (queue.TryDequeue(out var route))
        {
            if (route.Depth >= 4) continue;
            foreach (var next in RoadNeighbors(route.Tile))
            {
                if (!seen.Add(next) || !CanCritterLiveOn(index, next) || _occupants[next] >= 0 ||
                    reserved?.Contains(GetPosition(next)) is true || distance(next) > baseline)
                    continue;
                var firstStep = route.FirstStep < 0 ? next : route.FirstStep;
                if (distance(next) < baseline) return firstStep;
                queue.Enqueue((next, firstStep, route.Depth + 1));
            }
        }
        return -1;
    }
}
