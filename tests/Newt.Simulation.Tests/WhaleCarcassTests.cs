using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WhaleCarcassTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void OnlyOceanForagersCanConsumeWhaleFall()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 1, Terrain.Ocean) { SeasonsEnabled = false };
            world.SetTemperature(new(0, 0), 0.5f);
            world.AddCritter(CritterSpecies.BaleenWhale, new(0, 0));
            typeof(SimulationWorld).GetMethod("RemoveCritterAtIndex", Flags)!.Invoke(world, [0]);
            var consume = typeof(SimulationWorld).GetMethod("TryConsumeEnvironmentalNutrition", Flags)!;
            consume.Invoke(world, [species, 0]);
            var remaining = (byte[])typeof(SimulationWorld).GetField("_whaleFallNutrition", Flags)!.GetValue(world)!;
            var canEat = species is CritterSpecies.Worm or CritterSpecies.Trilobite or CritterSpecies.Nautilus;
            Assert.Equal(canEat ? 9 : 10, remaining[0]);
        }
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void WhaleStarvationDepositsExactlyTenOnDeathTile(CritterSpecies species)
    {
        var world = new SimulationWorld(2, 1, Terrain.DeepOcean) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(1, 0);
        var id = world.AddCritter(species, position);
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", Flags)!.GetValue(world)!;
        energy[0] = 0;

        world.AdvanceOneTick();

        Assert.False(world.TryGetCritter(id, out _));
        Assert.Equal(10, world.GetTileNutrition(position));
        Assert.Equal(0, world.GetTileNutrition(new(0, 0)));
        world.AdvanceOneTick();
        Assert.Equal(10, world.GetTileNutrition(position));
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void DirectWhaleRemovalAddsTenToExistingFood(CritterSpecies species)
    {
        var world = new SimulationWorld(1, 1, Terrain.DeepOcean) { SeasonsEnabled = false };
        var position = new GridPosition(0, 0);
        world.AddCritter(species, position);
        typeof(SimulationWorld).GetMethod("DepositTileNutrition", Flags)!.Invoke(world, [0]);
        typeof(SimulationWorld).GetMethod("RemoveCritterAtIndex", Flags)!.Invoke(world, [0]);
        Assert.Equal(11, world.GetTileNutrition(position));
    }
}
