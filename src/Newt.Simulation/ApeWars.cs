namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    private sealed class ApeWar(int attacker, int defender, int[] distances, bool bloodWar)
    {
        public int Attacker = attacker, Defender = defender;
        public int[] Distances = distances;
        public bool BloodWar = bloodWar;
        public bool CivilWar;
        public int? LichId;
        public bool LichAttacking;
        public bool MergeOnVictory;
        public HashSet<int> Bombers = [];
        public HashSet<int>? AttackingParticipants;
        public HashSet<int> DemobilizedWorkers = [];
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

    private bool IsWarVillage(int tile) =>
        _apeStructures.TryGetValue(tile, out var kind) && kind is ApeStructureKind.Village;

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
                pair.Value == ApeStructureKind.Village &&
                (_barbarianVillageTiles.Contains(pair.Key)
                    ? !_apeWars.ContainsKey(pair.Key) && Tick >= _villageWarCooldowns.GetValueOrDefault(pair.Key) &&
                        _apeVillageHomes.Any(home => home.Value == pair.Key &&
                            _critterIndicesById.TryGetValue(home.Key, out var index) && IsLivingApe(_species[index]))
                    : Eligible(pair.Key, minimum)))
            .Select(pair => pair.Key)
            .Concat(Enumerable.Range(0, _count).Where(index => _species[index] is CritterSpecies.Lich &&
                !_lichWars.ContainsKey(_critterIds[index].Value) &&
                Tick >= _villageWarCooldowns.GetValueOrDefault(-_critterIds[index].Value))
                .Select(index => GetIndex(_positions[index])))
            .Distinct().Order().ToArray();
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
        _species[index] is (CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen) &&
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
        attacking = war.LichId is { } lich ? IsLichArmyMember(index, lich) == war.LichAttacking :
            war.CivilWar ? war.AttackingWarriors.Contains(id.Value) : _apeVillageHomes[id.Value] == war.Attacker;
        return true;
    }

    public string? GetApeWarBehavior(CritterId id)
    {
        if (!_critterIndicesById.TryGetValue(id.Value, out var index) ||
            !TryGetApeWar(index, out var war) || !TryGetApeWarSide(id, out var attacking))
            return null;
        var kind = war.CivilWar ? "C" : war.BloodWar ? "B" : "W";
        var village = war.Defender;
        if (war.LichId is { } lich)
        {
            if (attacking && !IsLichArmyMember(index, lich))
                return $"{kind} Attacking Lich #{lich}";
            village = war.Attacker;
        }
        var villageId = GetApeVillageId(GetPosition(village));
        return $"{kind} {(attacking ? "Attacking" : "Defending")} village #{villageId}";
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
                CanRecruitApe(index) &&
                !(GetApeCultureDefinition(village)?.Units.Any(unit => unit.Species == _species[index] && !unit.Combatant) is true))
            .Select(pair => pair.Key).Order().ToArray();
        if (residents.Length < 2) return false;
        // Shuffle once, then split evenly. Team membership stays fixed as casualties occur.
        for (var i = residents.Length - 1; i > 0; i--)
        {
            var other = NextInt(i + 1);
            (residents[i], residents[other]) = (residents[other], residents[i]);
        }
        var war = new ApeWar(village, village, [], false) { CivilWar = true };
        var culturalDefenders = residents.Where(id => IsApeCulturalCombatUnit(village, _species[_critterIndicesById[id]])).ToHashSet();
        var redSlots = (residents.Length - culturalDefenders.Count) / 2;
        for (var i = 0; i < residents.Length; i++)
        {
            var id = residents[i];
            var index = _critterIndicesById[id];
            war.OriginalRoles[id] = _species[index];
            (culturalDefenders.Contains(id) || redSlots == 0 ? war.DefendingWarriors : war.AttackingWarriors).Add(id);
            if (!culturalDefenders.Contains(id) && war.AttackingWarriors.Contains(id)) redSlots--;
            if (_species[index] is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen))
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, preserveEnergy: true, preserveApeVillage: true);
        }
        _apeWars[village] = war;
        RecruitApeCultureBombers(war, village);
        RefreshCivilWarRoutes(war);
        return true;
    }

    private void RefreshCivilWarRoutes(ApeWar war)
    {
        IEnumerable<int> Positions(HashSet<int> army) => army.Where(id => !war.DemobilizedWorkers.Contains(id) && IsSurvivingCivilWarFighter(id))
            .Select(id => GetIndex(_positions[_critterIndicesById[id]]));
        war.Distances = BuildWarDistances(Positions(war.DefendingWarriors));
        war.BlueDistances = BuildWarDistances(Positions(war.AttackingWarriors));
    }

    public bool TryStartApeWar(GridPosition attackerBuilding, GridPosition defenderBuilding, bool bloodWar = false)
    {
        if (!Contains(attackerBuilding) || !Contains(defenderBuilding))
            return false;
        var attackingLich = GetLichAt(attackerBuilding);
        var defendingLich = GetLichAt(defenderBuilding);
        if (attackingLich >= 0 || defendingLich >= 0)
            return attackingLich >= 0 && defendingLich >= 0 ? false :
                TryStartLichWar(attackingLich >= 0 ? attackingLich : defendingLich,
                    attackingLich >= 0 ? defenderBuilding : attackerBuilding, attackingLich >= 0);
        if (!LifeEnabled || GetApeStructureVillage(attackerBuilding) is not { } attackerPosition ||
            GetApeStructureVillage(defenderBuilding) is not { } defenderPosition)
            return false;
        var attacker = GetIndex(attackerPosition);
        var defender = GetIndex(defenderPosition);
        if (attacker == defender || !IsRoadVillage(attacker) || !IsWarVillage(defender) ||
            _apeWars.ContainsKey(attacker) || _apeWars.ContainsKey(defender))
            return false;
        var barbarianDefender = _barbarianVillageTiles.Contains(defender);
        bloodWar |= barbarianDefender;
        var distances = BuildWarDistances(defender);
        if (distances[attacker] < 0)
            return false;
        var residents = new[] { attacker, defender }.Select(village => _apeVillageHomes
            .Where(pair => pair.Value == village && _critterIndicesById.ContainsKey(pair.Key))
            .Select(pair => pair.Key).Order().ToArray()).ToArray();
        if (!residents[0].Any(id => _species[_critterIndicesById[id]] is CritterSpecies.Ape or CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard) ||
            (bloodWar ? residents[1].Length == 0 : !residents[1].Any(id =>
                _species[_critterIndicesById[id]] is CritterSpecies.Ape or CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard)))
            return false;
        var war = new ApeWar(attacker, defender, distances, bloodWar);
        if (barbarianDefender)
        {
            var campPopulation = residents[1].Count(id =>
                IsLivingApe(_species[_critterIndicesById[id]]) || _species[_critterIndicesById[id]] is CritterSpecies.Dog);
            var limit = campPopulation * 4;
            var standingArmy = residents[0].Where(id => IsApeMilitary(_species[_critterIndicesById[id]]) ||
                _species[_critterIndicesById[id]] is CritterSpecies.Dog or CritterSpecies.ApeChieftain or
                    CritterSpecies.ApeKing or CritterSpecies.ApeQueen).Take(limit).ToArray();
            var unemployed = residents[0].Where(id => _species[_critterIndicesById[id]] is CritterSpecies.Ape &&
                CanRecruitApe(_critterIndicesById[id])).ToArray();
            war.AttackingParticipants = standingArmy.Concat(unemployed.Take(
                Math.Min(limit - standingArmy.Length, (int)Math.Ceiling(unemployed.Length * 0.9)))).ToHashSet();
        }
        for (var side = 0; side < 2; side++)
        {
            if (side == 1 && barbarianDefender)
            {
                // Camps fight with their existing warriors, pirates, and chiefs.
                war.DefendingWarriors.UnionWith(residents[side].Where(id =>
                    _species[_critterIndicesById[id]] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard));
                continue;
            }
            if (bloodWar && side == 1)
            {
                // Keep food and wood production running; chiefs also retain their role.
                foreach (var id in residents[side])
                {
                    var index = _critterIndicesById[id];
                    if (_species[index] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ||
                        !CanRecruitApe(index) ||
                        _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or
                            CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack ||
                        GetApeCultureDefinition(side == 0 ? attacker : defender)?.Units.Any(unit =>
                            unit.Species == _species[index] && !unit.Combatant) is true)
                        continue;
                    war.OriginalRoles[id] = _species[index];
                    ChangeCritterSpecies(index, CritterSpecies.ApeWarrior,
                        preserveEnergy: true, preserveApeVillage: true);
                }
                war.DefendingWarriors.UnionWith(residents[side].Where(id =>
                    _species[_critterIndicesById[id]] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard));
                continue;
            }
            var unemployed = residents[side].Where(id => _species[_critterIndicesById[id]] is CritterSpecies.Ape && CanRecruitApe(_critterIndicesById[id]) &&
                (side != 0 || war.AttackingParticipants is null || war.AttackingParticipants.Contains(id))).ToArray();
            foreach (var id in unemployed.Take(side == 0 && war.AttackingParticipants is not null
                ? unemployed.Length : (int)Math.Ceiling(unemployed.Length * 0.9)))
            {
                ChangeCritterSpecies(_critterIndicesById[id], CritterSpecies.ApeWarrior,
                    preserveEnergy: true, preserveApeVillage: true);
                war.Levies.Add(id);
            }
            var army = side == 0 ? war.AttackingWarriors : war.DefendingWarriors;
            army.UnionWith(residents[side].Where(id =>
                (side != 0 || war.AttackingParticipants is null || war.AttackingParticipants.Contains(id)) &&
                _species[_critterIndicesById[id]] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard));
        }
        _apeWars[attacker] = _apeWars[defender] = war;
        RecruitApeCultureBombers(war, attacker);
        RecruitApeCultureBombers(war, defender);
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
        if (TryGetLichWar(index, out war))
            return true;
        war = null!;
        return _apeVillageHomes.TryGetValue(_critterIds[index].Value, out var village) &&
            _apeWars.TryGetValue(village, out war!) &&
            (village != war.Attacker || war.AttackingParticipants is null ||
                war.AttackingParticipants.Contains(_critterIds[index].Value)) && (!war.CivilWar ||
                war.OriginalRoles.ContainsKey(_critterIds[index].Value) &&
                !war.DemobilizedWorkers.Contains(_critterIds[index].Value));
    }

    private bool AreWarEnemies(int first, int second) =>
        AreLichWarEnemies(first, second) ||
        TryGetApeWar(first, out var war) &&
        _apeVillageHomes.TryGetValue(_critterIds[first].Value, out var home) &&
        _apeVillageHomes.TryGetValue(_critterIds[second].Value, out var other) &&
        (war.CivilWar
            ? home == other && !war.DemobilizedWorkers.Contains(_critterIds[second].Value) && (war.AttackingWarriors.Contains(_critterIds[first].Value) &&
                war.DefendingWarriors.Contains(_critterIds[second].Value) ||
                war.DefendingWarriors.Contains(_critterIds[first].Value) && war.AttackingWarriors.Contains(_critterIds[second].Value))
            : other == (home == war.Attacker ? war.Defender : war.Attacker));

    private bool CanTargetWarApe(int first, int second) => AreLichWarEnemies(first, second) || (AreWarEnemies(first, second) &&
        (_species[second] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen ||
            TryGetApeWar(first, out var war) && war.BloodWar &&
            _apeVillageHomes[_critterIds[first].Value] == war.Attacker));

    internal void AdvanceApeWars()
    {
        foreach (var war in _apeWars.Values.Distinct().ToArray())
        {
            if (war.LichId is not null)
            {
                AdvanceLichWar(war);
                continue;
            }
            bool Surviving(int id) => war.CivilWar
                ? !war.DemobilizedWorkers.Contains(id) && IsSurvivingCivilWarFighter(id)
                : IsSurvivingWarrior(id);
            var attackers = war.AttackingWarriors.Count(Surviving);
            var defenders = war.DefendingWarriors.Count(Surviving);
            if (war.CivilWar && (attackers * 10 <= war.AttackingWarriors.Count ||
                defenders * 10 <= war.DefendingWarriors.Count))
            {
                foreach (var (id, role) in war.OriginalRoles)
                {
                    if (role is CritterSpecies.Ape or CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen ||
                        war.Bombers.Contains(id) || IsApeCulturalCombatUnit(war.Attacker, role) ||
                        !Surviving(id) || IsVeteranWarrior(_critterIndicesById[id]) || !war.DemobilizedWorkers.Add(id)) continue;
                    var index = _critterIndicesById[id];
                    if (!CanLiveOn(_species[index], GetIndex(_positions[index])))
                        _returningWarSailors[id] = role;
                    else
                        ChangeCritterSpecies(index, role, preserveEnergy: true, preserveApeVillage: true);
                }
                attackers = war.AttackingWarriors.Count(Surviving);
                defenders = war.DefendingWarriors.Count(Surviving);
                RefreshCivilWarRoutes(war);
            }
            var attackerLost = !IsRoadVillage(war.Attacker) ||
                (war.CivilWar ? attackers == 0 : attackers * 10 <= war.AttackingWarriors.Count);
            var defenderLost = war.BloodWar
                ? !IsWarVillage(war.Defender) || !GetBloodWarTargets(war).Any()
                : !IsRoadVillage(war.Defender) ||
                    (war.CivilWar ? defenders == 0 : defenders * 10 <= war.DefendingWarriors.Count);
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
            if (war.CivilWar && defenderLost)
                RemoveApeCulture(war.Attacker);
            var mergeWinner = war.MergeOnVictory && attackerLost != defenderLost ?
                attackerLost ? war.Defender : war.Attacker : -1;
            var mergeLoser = mergeWinner < 0 ? -1 : mergeWinner == war.Attacker ? war.Defender : war.Attacker;
            EndApeWar(war);
            if (mergeWinner >= 0 && IsWarVillage(mergeWinner) && IsWarVillage(mergeLoser))
            {
                RemoveApeCulture(mergeLoser);
                MergeApeVillageInto(mergeLoser, mergeWinner);
            }
        }
    }

    /// <summary>Ends either war variant without declaring a winner or transferring stores.</summary>
    public bool TryStopApeWar(GridPosition villageBuilding)
    {
        if (!Contains(villageBuilding))
            return false;
        if (GetLichAt(villageBuilding) is var lich && lich >= 0 && _lichWars.TryGetValue(lich, out var lichWar))
        {
            EndApeWar(lichWar);
            return true;
        }
        if (GetApeStructureVillage(villageBuilding) is not { } village ||
            !_apeWars.TryGetValue(GetIndex(village), out var war))
            return false;
        EndApeWar(war);
        return true;
    }

    private void EndApeWar(ApeWar war)
    {
        _apeVillageMergeCheckNeeded = true;
        foreach (var id in war.Bombers)
            if (_critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.ApeBomber)
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, true, true);
        if (war.LichId is { } lich)
            _lichWars.Remove(lich);
        _villageWarCooldowns[war.Attacker] = _villageWarCooldowns[war.Defender] = Tick + VillageWarCooldownTicks;
        foreach (var pair in _apeVillageHomes)
            if ((pair.Value == war.Attacker || pair.Value == war.Defender) &&
                _critterIndicesById.TryGetValue(pair.Key, out var sailor) && CanWarApeSail(sailor) &&
                !CanLiveOn(_species[sailor], GetIndex(_positions[sailor])))
                _returningWarSailors[pair.Key] = IsVeteranWarrior(sailor) ? null : war.OriginalRoles.TryGetValue(pair.Key, out var role)
                    ? role : war.Levies.Contains(pair.Key) ? CritterSpecies.Ape : null;
        _apeWars.Remove(war.Attacker);
        _apeWars.Remove(war.Defender);
        foreach (var pair in _apeVillageHomes)
            if (pair.Value == war.Attacker || pair.Value == war.Defender)
                _chieftainAttackTargets.Remove(pair.Key);
        foreach (var id in war.Levies)
            if (!_returningWarSailors.ContainsKey(id) && _critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard && !IsVeteranWarrior(index))
                ChangeCritterSpecies(index, CritterSpecies.Ape, preserveEnergy: true, preserveApeVillage: true);
        foreach (var (id, role) in war.OriginalRoles)
            if (!_returningWarSailors.ContainsKey(id) && _critterIndicesById.TryGetValue(id, out var index) &&
                _species[index] is CritterSpecies.ApeWarrior or CritterSpecies.ApeRoyalGuard && role != CritterSpecies.ApeWarrior && !IsVeteranWarrior(index))
                ChangeCritterSpecies(index, role, preserveEnergy: true, preserveApeVillage: true);
        PrioritizeVillageVeterans(war.Attacker);
        if (war.Defender != war.Attacker && !_barbarianVillageTiles.Contains(war.Defender))
            PrioritizeVillageVeterans(war.Defender);
    }

    private bool IsSurvivingWarrior(int id) => _critterIndicesById.TryGetValue(id, out var index) &&
        IsApeMilitary(_species[index]);

    private bool IsSurvivingCivilWarFighter(int id) => _critterIndicesById.TryGetValue(id, out var index) &&
        (IsApeMilitary(_species[index]) || _species[index] is CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen);

    private GridPosition? TryReturnWarSailor(int index, IReadOnlySet<GridPosition>? reserved)
    {
        var id = _critterIds[index].Value;
        var current = GetIndex(_positions[index]);
        if (CanLiveOn(_species[index], current))
        {
            if (_returningWarSailors.Remove(id, out var role) && role is { } originalRole && !IsVeteranWarrior(index))
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
                        if (_returningWarSailors.Remove(id, out var role) && role is { } originalRole && !IsVeteranWarrior(index))
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
        if (war.LichId is not null)
            return TryMoveLichWarMember(index, war, reserved);
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
        if (best < 0 && IsApeMilitary(_species[index]))
            foreach (var tile in neighbors.Where(tile => CanCritterLiveOn(index, tile) &&
                         reserved?.Contains(GetPosition(tile)) is not true && Distance(tile) < Distance(current))
                         .OrderBy(Distance).ThenBy(tile => tile))
            {
                var blocker = _occupants[tile];
                if (blocker < 0 || AreWarEnemies(index, blocker)) continue;
                if (TryShoveMovementBlocker(index, tile, reserved))
                {
                    best = tile;
                    break;
                }
                if (TrySwapPastMovementBlocker(index, tile, reserved)) return null;
            }
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
