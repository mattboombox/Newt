namespace Newt.Simulation;

public sealed record NecropolisSnapshot(CritterId Lich, GridPosition Position, int ApeKills,
    int StoredKills, int Guards, int Escorts, bool Retreating, bool AtWar);

public sealed partial class SimulationWorld
{
    public const int NecropolisFoundingKills = 100;
    public const int NecropolisWarriorCost = 1;
    public const int NecropolisHealCost = 10;
    public const int NecropolisGarrisonSize = 4;
    public const int NecropolisEscortSize = 4;
    public const int NecropolisWarRadius = 32;

    private sealed class LichCampaign
    {
        public int Kills, Reserve;
        public long FoundedTick;
        public int Tile = -1;
        public bool Founded, Retreating, Mobilizing;
        public HashSet<int> Warriors = [], Escorts = [];
        public Dictionary<int, int> Guards = [];
    }

    private readonly Dictionary<int, LichCampaign> _lichCampaigns = [];
    private readonly Dictionary<int, int> _necropolisOwners = [];

    public int NecropolisCount => _necropolisOwners.Count;
    public int GetLichApeKills(CritterId lich) => _lichCampaigns.TryGetValue(lich.Value, out var state) ? state.Kills : 0;
    public bool HasNecropolis(GridPosition position) => Contains(position) && _necropolisOwners.ContainsKey(GetIndex(position));
    public NecropolisSnapshot? GetNecropolis(GridPosition position) =>
        Contains(position) && _necropolisOwners.TryGetValue(GetIndex(position), out var lich)
            ? GetLichNecropolis(new CritterId(lich)) : null;

    public NecropolisSnapshot? GetLichNecropolis(CritterId lich) =>
        _lichCampaigns.TryGetValue(lich.Value, out var state) && state.Tile >= 0
            ? new(lich, GetPosition(state.Tile), state.Kills, state.Reserve,
                state.Guards.Count, state.Escorts.Count, state.Retreating, _lichWars.ContainsKey(lich.Value)) : null;

    public bool TryPlaceNecropolis(GridPosition position)
    {
        if (!LifeEnabled || !Contains(position)) return false;
        var tile = GetIndex(position);
        if (_occupants[tile] >= 0 || !IsNecropolisSite(tile) || !CanLiveOn(CritterSpecies.Lich, tile)) return false;
        var posts = CardinalDirections.Select(direction => OffsetTile(position, direction))
            .Where(Contains).Select(GetIndex).Concat(RoadNeighbors(tile))
            .Where(next => next != tile && _occupants[next] < 0 &&
                IsNecropolisSite(next) && CanLiveOn(CritterSpecies.UndeadWarrior, next))
            .Distinct().Take(NecropolisGarrisonSize).ToArray();
        // Validate the whole group first so a failed placement creates nothing.
        if (posts.Length != NecropolisGarrisonSize) return false;
        var lich = AddCritterUnchecked(CritterSpecies.Lich, position, tile, spawnLichArmy: false);
        var state = new LichCampaign { Tile = tile, Founded = true, FoundedTick = Tick };
        _lichCampaigns[lich.Value] = state;
        _necropolisOwners[tile] = lich.Value;
        foreach (var post in posts)
        {
            var guard = AddCritterUnchecked(CritterSpecies.UndeadWarrior, GetPosition(post), post);
            SetLichArmyOwner(guard.Value, lich.Value);
            state.Warriors.Add(guard.Value);
            state.Guards[guard.Value] = post;
        }
        return true;
    }

    // Called only for a defeated living ape, before it is removed or raised.
    private void RecordLichApeKill(int killer, int victim)
    {
        if (!IsLichPrey(_species[victim])) return;
        var lich = _species[killer] is CritterSpecies.Lich ? _critterIds[killer].Value :
            IsLichUndead(_species[killer]) && _lichArmy.TryGetValue(_critterIds[killer].Value, out var owner) ? owner : -1;
        if (lich < 0 || !_critterIndicesById.TryGetValue(lich, out var leader) || _species[leader] is not CritterSpecies.Lich)
            return;
        if (!_lichCampaigns.TryGetValue(lich, out var state)) _lichCampaigns[lich] = state = new();
        state.Kills++;
        if (state.Founded) state.Reserve++;
    }

    private bool IsNecropolisSite(int tile) => IsRoadLandTerrain(tile) &&
        _surfaceWater[tile] is SurfaceWaterKind.None && !_apeStructures.ContainsKey(tile) &&
        !_wolfDenCharges.ContainsKey(tile) && !_megaSpiderWebFood.ContainsKey(tile) &&
        !_teleporters.Contains(tile) && !_vampireLairs.Contains(tile) &&
        !_necropolisOwners.ContainsKey(tile) && !IsCustodianFabricator(GetPosition(tile)) &&
        !_volcanoes.Any(volcano => volcano.Position == GetPosition(tile));

    private void FoundNecropolis(int lich, LichCampaign state, int leader)
    {
        var tile = ReachableNecropolisTiles(GetIndex(_positions[leader]), 3)
            .Where(IsNecropolisSite).FirstOrDefault(-1);
        if (tile < 0) return;
        state.Tile = tile;
        state.Founded = true;
        state.FoundedTick = Tick;
        // The founding hundred is a milestone, not spendable currency.
        state.Reserve = Math.Max(0, state.Kills - NecropolisFoundingKills);
        _necropolisOwners[tile] = lich;
        state.Warriors.UnionWith(_lichArmy.Where(pair => pair.Value == lich &&
            _critterIndicesById.TryGetValue(pair.Key, out var index) && _species[index] is CritterSpecies.UndeadWarrior)
            .Select(pair => pair.Key));
        AssignNecropolisRoles(state);
        // Existing warriors become the initial garrison. Fill any missing founding guards for free.
        SpawnNecropolisWarriors(lich, state, NecropolisGarrisonSize - state.Guards.Count, free: true);
    }

    private bool IsCampaignWarrior(int id, int lich) =>
        _critterIndicesById.TryGetValue(id, out var index) && _species[index] is CritterSpecies.UndeadWarrior &&
        _lichArmy.GetValueOrDefault(id, -1) == lich;

    // Local searches stay bounded independently of world and population size.
    private IEnumerable<int> ReachableNecropolisTiles(int origin, int radius)
    {
        var seen = new HashSet<int> { origin };
        var pending = new Queue<(int Tile, int Distance)>();
        pending.Enqueue((origin, 0));
        while (pending.TryDequeue(out var step))
        {
            yield return step.Tile;
            if (step.Distance >= radius) continue;
            foreach (var next in RoadNeighbors(step.Tile))
                if (seen.Add(next) && CanLiveOn(CritterSpecies.UndeadWarrior, next))
                    pending.Enqueue((next, step.Distance + 1));
        }
    }

    private void AssignNecropolisRoles(LichCampaign state)
    {
        if (state.Guards.Count >= NecropolisGarrisonSize && state.Escorts.Count >= NecropolisEscortSize) return;
        foreach (var id in state.Warriors.Order())
        {
            if (state.Guards.ContainsKey(id) || state.Escorts.Contains(id)) continue;
            if (state.Guards.Count < NecropolisGarrisonSize)
            {
                var post = CardinalDirections.Select(direction => OffsetTile(GetPosition(state.Tile), direction))
                    .Where(Contains).Select(GetIndex).Concat(ReachableNecropolisTiles(state.Tile, 2))
                    .Where(tile => tile != state.Tile &&
                    CanLiveOn(CritterSpecies.UndeadWarrior, tile) && !state.Guards.ContainsValue(tile))
                    .FirstOrDefault(-1);
                if (post >= 0) state.Guards[id] = post;
            }
            else if (state.Escorts.Count < NecropolisEscortSize) state.Escorts.Add(id);
            if (state.Guards.Count >= NecropolisGarrisonSize && state.Escorts.Count >= NecropolisEscortSize) break;
        }
    }

    private void SpawnNecropolisWarriors(int lich, LichCampaign state, int maximum, bool free = false)
    {
        maximum = Math.Min(maximum, LichUndeadWarriorLimit - GetLichUndeadWarriorCount(new CritterId(lich)));
        if (maximum <= 0) return;
        foreach (var tile in ReachableNecropolisTiles(state.Tile, 8))
        {
            if (maximum <= 0 || !free && state.Reserve < NecropolisWarriorCost) break;
            if (_necropolisOwners.ContainsKey(tile) || _occupants[tile] >= 0 ||
                !CanLiveOn(CritterSpecies.UndeadWarrior, tile) || _surfaceCovers[tile] is not SurfaceCover.None)
                continue;
            var warrior = AddCritter(CritterSpecies.UndeadWarrior, GetPosition(tile));
            SetLichArmyOwner(warrior.Value, lich);
            state.Warriors.Add(warrior.Value);
            if (!free) state.Reserve -= NecropolisWarriorCost;
            maximum--;
        }
        AssignNecropolisRoles(state);
    }

    internal void AdvanceNecropolises()
    {
        if (!LifeEnabled || Tick % TicksPerSecond != 0) return;
        foreach (var (lich, state) in _lichCampaigns.ToArray())
        {
            if (!_critterIndicesById.TryGetValue(lich, out var leader) || _species[leader] is not CritterSpecies.Lich)
            {
                RemoveLichNecropolis(lich);
                continue;
            }
            if (!state.Founded && state.Kills >= NecropolisFoundingKills) FoundNecropolis(lich, state, leader);
            if (state.Tile < 0) continue;
            if (!IsRoadLandTerrain(state.Tile) || _surfaceWater[state.Tile] is not SurfaceWaterKind.None)
            {
                RemoveNecropolisAt(GetPosition(state.Tile));
                continue;
            }
            state.Warriors.RemoveWhere(id => !IsCampaignWarrior(id, lich));
            foreach (var id in state.Guards.Keys.Where(id => !state.Warriors.Contains(id)).ToArray()) state.Guards.Remove(id);
            foreach (var id in state.Guards.Where(pair => !CanLiveOn(CritterSpecies.UndeadWarrior, pair.Value))
                .Select(pair => pair.Key).ToArray()) state.Guards.Remove(id);
            state.Escorts.IntersectWith(state.Warriors);
            AssignNecropolisRoles(state);
            UpdateNecropolisRetreat(leader, state);
            if (state.Retreating) continue; // Save the healing reserve while retreating.
            var atWar = _lichWars.ContainsKey(lich);
            if (state.Mobilizing && atWar)
            {
                SpawnNecropolisWarriors(lich, state, state.Reserve / NecropolisWarriorCost);
                state.Mobilizing = state.Reserve >= NecropolisWarriorCost;
            }
            else if (!atWar)
            {
                state.Mobilizing = false;
                SpawnNecropolisWarriors(lich, state,
                    NecropolisGarrisonSize + NecropolisEscortSize - state.Guards.Count - state.Escorts.Count);
            }
            TryStartNecropolisWar(lich, state);
        }
    }

    private void UpdateNecropolisRetreat(int leader, LichCampaign state)
    {
        var maximum = CritterNutritions.Get(CritterSpecies.Lich).MaximumEnergy;
        if (_energy[leader] * 3 < maximum) state.Retreating = true;
        if (!state.Retreating || !(GetIndex(_positions[leader]) == state.Tile ||
            ArePathNeighbors(GetIndex(_positions[leader]), state.Tile))) return;
        if (_lichWars.TryGetValue(_critterIds[leader].Value, out var war) && war.LichAttacking) EndApeWar(war);
        state.Mobilizing = false;
        if (_energy[leader] >= maximum) state.Retreating = false;
        else if (state.Reserve >= NecropolisHealCost)
        {
            state.Reserve -= NecropolisHealCost;
            _energy[leader] = maximum;
            state.Retreating = false;
        }
    }

    private bool TryStartNecropolisWar(int lich, LichCampaign state)
    {
        if (!NaturalEventsEnabled || state.Retreating || Tick < VillageWarCooldownTicks ||
            Tick % VillageWarCheckIntervalTicks != 0 || _lichWars.ContainsKey(lich) ||
            Tick < _villageWarCooldowns.GetValueOrDefault(-lich) || NextInt(100) >= VillageWarChancePercent) return false;
        var distances = BuildWarDistances([state.Tile], NecropolisWarRadius);
        var villages = _apeStructures.Where(pair => pair.Value is ApeStructureKind.Village &&
            IsRoadVillage(pair.Key) && !_apeWars.ContainsKey(pair.Key) && distances[pair.Key] >= 0 &&
            Tick >= _villageWarCooldowns.GetValueOrDefault(pair.Key) && GetApeVillageResidentCountByTile(pair.Key) > 0)
            .Select(pair => pair.Key).Order().ToArray();
        while (villages.Length > 0)
        {
            var selected = NextInt(villages.Length);
            if (TryStartLichWar(lich, GetPosition(villages[selected]), lichAttacking: true)) return true;
            villages = villages.Where((_, i) => i != selected).ToArray();
        }
        return false;
    }

    private void MobilizeNecropolis(int lich)
    {
        if (!_lichCampaigns.TryGetValue(lich, out var state) || state.Tile < 0) return;
        if (_critterIndicesById.TryGetValue(lich, out var leader)) UpdateNecropolisRetreat(leader, state);
        state.Mobilizing = true;
        if (state.Retreating) return;
        SpawnNecropolisWarriors(lich, state, state.Reserve / NecropolisWarriorCost);
        state.Mobilizing = state.Reserve >= NecropolisWarriorCost;
    }

    private bool TryMoveNecropolisMember(int index, IReadOnlySet<GridPosition>? reserved, out GridPosition? prey)
    {
        prey = null;
        var id = _critterIds[index].Value;
        var lich = _species[index] is CritterSpecies.Lich ? id :
            _species[index] is CritterSpecies.UndeadWarrior ? _lichArmy.GetValueOrDefault(id, -1) : -1;
        if (lich < 0 || !_lichCampaigns.TryGetValue(lich, out var state) || state.Tile < 0) return false;
        if (_species[index] is CritterSpecies.Lich)
        {
            UpdateNecropolisRetreat(index, state);
            if (!state.Retreating && _lichWars.ContainsKey(lich)) return false;
            // Peaceful liches return all the way onto their structure and remain
            // there instead of resuming ordinary hunting after recovering.
            if (GetIndex(_positions[index]) != state.Tile)
            {
                TryMoveAlongSurfacePath(index, state.Tile, reserved);
                UpdateNecropolisRetreat(index, state);
            }
            return true;
        }
        var guard = state.Guards.TryGetValue(id, out var post);
        if (!guard && !state.Escorts.Contains(id)) return false;
        if (!_critterIndicesById.TryGetValue(lich, out var leader)) return false;
        var target = guard ? post : GetIndex(_positions[leader]);
        var cornerPost = false;
        if (!guard && target == state.Tile)
        {
            // Four stable escort slots use the diagonals; the garrison keeps the
            // cardinal posts. Stay in formation until the lich leaves its home.
            var slot = Array.IndexOf(state.Escorts.Order().ToArray(), id);
            var corners = new GridPosition[] { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) }
                .Select(direction => OffsetTile(GetPosition(state.Tile), direction)).Where(Contains)
                .Select(GetIndex).Distinct().Where(tile => tile != state.Tile &&
                    CanCritterLiveOn(index, tile) && !state.Guards.ContainsValue(tile)).ToArray();
            if (slot >= 0 && slot < corners.Length)
            {
                target = corners[slot];
                cornerPost = true;
            }
        }
        var distance = WrappedManhattanDistance(_positions[index], GetPosition(target));
        // Guards intercept only beside their posts; escorts stay close to their lich.
        if (distance <= 3) prey = FindHunterPrey(index, _species[index], 1, reserved);
        if (prey is null && distance > (guard || cornerPost ? 0 : 2)) TryMoveAlongSurfacePath(index, target, reserved);
        return true;
    }

    private void RegisterNecropolisWarrior(int index)
    {
        if (_species[index] is CritterSpecies.UndeadWarrior &&
            _lichArmy.TryGetValue(_critterIds[index].Value, out var lich) &&
            _lichCampaigns.TryGetValue(lich, out var state) && state.Tile >= 0)
        {
            state.Warriors.Add(_critterIds[index].Value);
            AssignNecropolisRoles(state);
        }
    }

    private void DetachNecropolisWarrior(int id)
    {
        if (!_lichArmy.TryGetValue(id, out var lich) || !_lichCampaigns.TryGetValue(lich, out var state)) return;
        state.Warriors.Remove(id);
        state.Guards.Remove(id);
        state.Escorts.Remove(id);
    }

    public bool RemoveNecropolisAt(GridPosition position)
    {
        if (!Contains(position) || !_necropolisOwners.Remove(GetIndex(position), out var lich)) return false;
        if (_lichCampaigns.TryGetValue(lich, out var state))
        {
            state.Tile = -1;
            state.Guards.Clear();
            state.Escorts.Clear();
            state.Retreating = state.Mobilizing = false;
        }
        return true;
    }

    private void RemoveLichNecropolis(int lich)
    {
        if (_lichCampaigns.Remove(lich, out var state) && state.Tile >= 0) _necropolisOwners.Remove(state.Tile);
    }
}
