namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int VillageWolfDenWarRadius = 16;

    private bool TryStartWolfDenWar(GridPosition building, GridPosition denPosition)
    {
        if (!LifeEnabled || GetApeStructureVillage(building) is not { } villagePosition) return false;
        var village = GetIndex(villagePosition);
        var den = GetIndex(denPosition);
        if (!IsRoadVillage(village) || _apeWars.ContainsKey(village) ||
            _apeWars.Values.Any(war => war.WolfDen && war.Defender == den)) return false;
        var distances = BuildWarDistances([den], VillageWolfDenWarRadius);
        if (distances[village] < 0) return false;
        var participants = _apeVillageHomes.Where(pair => pair.Value == village &&
                _critterIndicesById.TryGetValue(pair.Key, out var index) &&
                (IsApeMilitary(_species[index]) || _species[index] is CritterSpecies.Dog or
                    CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen ||
                    _species[index] is CritterSpecies.Ape && CanRecruitApe(index)))
            .Select(pair => pair.Key).ToHashSet();
        var war = new ApeWar(village, den, BuildWarDistances(den), false)
        {
            Id = _nextApeWarId++, WolfDen = true, AttackingParticipants = participants,
            StartingTargetPopulation = GetWolfDenAssociatedWolfCount(denPosition)
        };
        foreach (var id in participants)
        {
            var index = _critterIndicesById[id];
            if (_species[index] is CritterSpecies.Ape)
            {
                ChangeCritterSpecies(index, CritterSpecies.ApeWarrior, true, true);
                war.Levies.Add(id);
            }
            if (IsApeMilitary(_species[index])) war.AttackingWarriors.Add(id);
        }
        if (war.AttackingWarriors.Count == 0) return false;
        _apeWars[village] = war;
        RecruitApeCultureBombers(war, village);
        return true;
    }

    private bool AreWolfDenWarEnemies(int attacker, int wolf) =>
        _species[wolf] is CritterSpecies.Wolf && TryGetApeWar(attacker, out var war) && war.WolfDen &&
        (_wolfDenHomes.TryGetValue(_critterIds[wolf].Value, out var home) && home == war.Defender ||
            _wolfDenTargets.TryGetValue(_critterIds[wolf].Value, out var target) && target == war.Defender);
}
