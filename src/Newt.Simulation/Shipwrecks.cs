namespace Newt.Simulation;

public enum ShipwreckKind : byte { Ship, Pirate }

public sealed partial class SimulationWorld
{
    private readonly Dictionary<int, (ShipwreckKind Kind, long Expires)> _shipwrecks = [];
    private long _nextShipwreckDecayTick = long.MaxValue;

    public ShipwreckKind? GetShipwreck(GridPosition position) =>
        _shipwrecks.TryGetValue(GetIndex(position), out var wreck) && Tick < wreck.Expires ? wreck.Kind : null;

    private void DepositShipwreck(int index, int tile)
    {
        if (_whaleFallNutrition[tile] > 0) return;
        if (_surfaceWater[tile] is SurfaceWaterKind.River) return;
        if (_surfaceWater[tile] is not SurfaceWaterKind.FreshwaterLake &&
            _terrain[tile] is not (Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows)) return;
        var species = _species[index];
        var pirate = IsBarbarianApe(index) && IsLivingApe(species);
        var colonist = GetCritter(index).IsColonist;
        if (!(IsLivingApe(species) || species is CritterSpecies.ApeTraderSailor or CritterSpecies.Vampire) ||
            !(pirate || colonist || IsVeteranWarrior(index) || species is CritterSpecies.ApeSailor or
                CritterSpecies.ApeTraderSailor or CritterSpecies.ApeWarrior or CritterSpecies.ApeChieftain or
                CritterSpecies.ApeBomber or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or CritterSpecies.ApeRoyalGuard))
            return;
        var expires = Tick + ApeRuinDecayTicks;
        _shipwrecks[tile] = (pirate ? ShipwreckKind.Pirate : ShipwreckKind.Ship, expires);
        _nextShipwreckDecayTick = Math.Min(_nextShipwreckDecayTick, expires);
    }

    private void AdvanceShipwrecks()
    {
        if (Tick < _nextShipwreckDecayTick) return;
        foreach (var tile in _shipwrecks.Where(pair => pair.Value.Expires <= Tick).Select(pair => pair.Key).ToArray())
            _shipwrecks.Remove(tile);
        _nextShipwreckDecayTick = _shipwrecks.Count == 0 ? long.MaxValue : _shipwrecks.Values.Min(wreck => wreck.Expires);
    }
}
