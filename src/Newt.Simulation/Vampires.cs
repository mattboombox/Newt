namespace Newt.Simulation;

public sealed partial class SimulationWorld
{
    public const int VampireLairCapacity = 2;
    private readonly HashSet<int> _vampireLairs = [];
    private readonly Dictionary<int, int> _vampireHomes = [];

    public bool HasVampireLair(GridPosition position) => _vampireLairs.Contains(GetIndex(position));
    public int GetVampireLairPopulation(GridPosition position) =>
        _vampireHomes.Count(pair => pair.Value == GetIndex(position));

    private void DetachVampireFromLair(int id)
    {
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
        var damage = Math.Min(_energy[victim], GetCombatDamage(attacker));
        _energy[victim] -= damage;
        if (_species[attacker] is CritterSpecies.Vampire && IsLivingApe(_species[victim]))
            _energy[attacker] = Math.Min(CritterNutritions.Get(CritterSpecies.Vampire).MaximumEnergy,
                _energy[attacker] + damage);
    }
}
