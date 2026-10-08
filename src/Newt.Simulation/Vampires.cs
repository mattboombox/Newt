namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int VampireLairCapacity = 3;
    private readonly HashSet<int> _vampireLairs = [];
    private readonly Dictionary<int, int> _vampireHomes = [];

    public bool HasVampireLair(GridPosition position) => _vampireLairs.Contains(GetIndex(position));
    public int GetVampireLairPopulation(GridPosition position) =>
        _vampireHomes.Count(pair => pair.Value == GetIndex(position));

    private void DetachVampireFromLair(int id)
    {
        if (_vampireColonists.Remove(id))
            _apeSettlerPaths.Remove(id);
        if (_vampireHomes.Remove(id, out var lair) &&
            !_vampireHomes.ContainsValue(lair) && _vampireLairs.Remove(lair))
            AddApeRuin(lair);
    }

    private bool HasVampireBirthSlot(int index) =>
        !_vampireHomes.TryGetValue(_critterIds[index].Value, out var home) ||
        GetVampireLairPopulation(GetPosition(home)) < VampireLairCapacity;

    private int PrepareVampireLair(int index)
    {
        var id = _critterIds[index].Value;
        if (_vampireHomes.TryGetValue(id, out var home))
            return home;
        var tile = GetIndex(_positions[index]);
        if (_apeStructures.ContainsKey(tile) || _wolfDenCharges.ContainsKey(tile) ||
            _vampireLairs.Contains(tile) || _surfaceWater[tile] is not SurfaceWaterKind.None ||
            _terrain[tile] is Terrain.Shallows)
            return -1;
        _vampireLairs.Add(tile);
        _vampireHomes[id] = tile;
        return tile;
    }

    private void ApplyCombatDamage(int attacker, int victim)
    {
        if (_species[attacker] is CritterSpecies.Vampire && _species[victim] is CritterSpecies.Vampire)
            return;
        var damage = Math.Min(_energy[victim], GetCombatDamage(attacker));
        // A fresh fighter must survive a hit, even against a stronger opponent.
        // Use starting energy rather than current energy so wounded fighters can die.
        if (_species[attacker] is not CritterSpecies.ApeBomber && CanCritterFight(_species[victim]))
            damage = Math.Min(damage, GetMaximumHitDamage(attacker, victim));
        _energy[victim] -= damage;
        // Traders lose provisions when hit; the robber gains the energy actually taken.
        // Combine this with vampire feeding so vampires do not collect twice.
        if (_species[attacker] is not CritterSpecies.ApeBomber &&
            (_species[victim] is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor ||
            _species[attacker] is CritterSpecies.Vampire && IsLivingApe(_species[victim])))
            _energy[attacker] = Math.Min(_vampireColonists.ContainsKey(_critterIds[attacker].Value)
                    ? ApeSettlerInitialEnergy : CritterNutritions.Get(_species[attacker]).MaximumEnergy,
                _energy[attacker] + damage);
        if (_species[attacker] is CritterSpecies.ApeBomber)
            _energy[attacker] = Math.Max(0, _energy[attacker] - damage);
    }

    private int GetMaximumHitDamage(int attacker, int victim) =>
        _species[attacker] is CritterSpecies.ApeBomber ? 20 : CanCritterFight(_species[victim])
            ? Math.Min(GetCombatDamage(attacker), Math.Max(1, CritterNutritions.Get(_species[victim]).InitialEnergy - 1))
            : GetCombatDamage(attacker);
}
