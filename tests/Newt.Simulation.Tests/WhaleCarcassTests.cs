using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class WhaleCarcassTests
{
    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void StrandedWhaleCannotCrossMountainsToReachOcean(CritterSpecies species)
    {
        var world = new SimulationWorld(5, 1, Terrain.Mountain);
        var stranded = new GridPosition(0, 0);
        var ocean = new GridPosition(2, 0);
        world.SetTerrain(stranded, Terrain.Beach);
        world.SetTerrain(ocean, Terrain.Ocean);
        Assert.True(world.TrySpawnCritter(species, stranded));
        var recover = typeof(SimulationWorld).GetMethod("TryMoveStranded", Flags)!;

        recover.Invoke(world, [0]);

        Assert.Equal(stranded, world.GetCritter(0).Position);

        world.SetTerrain(new GridPosition(1, 0), Terrain.Shallows);
        recover.Invoke(world, [0]);

        Assert.Equal(new GridPosition(1, 0), world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void WhaleFallsAreClearedAndCannotBeDepositedOnMountains(CritterSpecies species)
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean);
        var tile = new GridPosition(0, 0);
        world.AddCritter(species, tile);
        Assert.True(world.RemoveCritterAt(tile));
        Assert.Equal(10, world.GetWhaleFallNutrition(tile));

        world.AddCritter(species, tile);
        world.SetTerrain(tile, Terrain.Mountain);
        Assert.Equal(0, world.GetWhaleFallNutrition(tile));
        Assert.True(world.RemoveCritterAt(tile));
        Assert.Equal(0, world.GetWhaleFallNutrition(tile));
    }

    [Fact]
    public void TeleporterCannotDropWhaleOnMountain()
    {
        var world = new SimulationWorld(2, 1, Terrain.Mountain);
        var portal = new GridPosition(0, 0);
        world.SetTerrain(portal, Terrain.Ocean);
        Assert.True(world.TryPlaceTeleporter(portal));
        world.AddCritter(CritterSpecies.ToothedWhale, portal);

        Assert.False(world.TryActivateTeleporterAt(portal));
        Assert.Equal(portal, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Fish, Terrain.Ocean)]
    [InlineData(CritterSpecies.Crab, Terrain.Ocean)]
    [InlineData(CritterSpecies.Nautilus, Terrain.Shallows)]
    [InlineData(CritterSpecies.Squid, Terrain.Ocean)]
    [InlineData(CritterSpecies.SeaScorpion, Terrain.Ocean)]
    [InlineData(CritterSpecies.Wolf, Terrain.Plains)]
    [InlineData(CritterSpecies.Crab, Terrain.Beach)]
    public void EligibleCrittersFeedFromWhaleFallsDuringMovementTurns(CritterSpecies species, Terrain terrain)
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        var tile = new GridPosition(0, 0);
        world.SetTemperature(tile, 0.5f);
        world.AddCritter(CritterSpecies.BaleenWhale, tile);
        world.SetTerrain(tile, terrain);
        Assert.True(world.RemoveCritterAt(tile));
        world.AddCritter(species, tile);
        var before = world.GetCritter(0).Energy;
        var movementTicks = (long[])typeof(SimulationWorld).GetField("_nextMovementTicks", Flags)!.GetValue(world)!;
        var firstMovementTick = movementTicks[0];
        while (world.Tick < firstMovementTick)
            world.AdvanceOneTick();

        Assert.Equal(before + 1, world.GetCritter(0).Energy);
        Assert.Equal(9, world.GetWhaleFallNutrition(tile));
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void WhaleFallVisualNutritionEndsWhenCarcassIsConsumed(CritterSpecies species)
    {
        var world = new SimulationWorld(1, 1, Terrain.DeepOcean);
        var tile = new GridPosition(0, 0);
        Assert.Equal(0, world.GetWhaleFallNutrition(tile));
        world.AddCritter(species, tile);
        Assert.True(world.RemoveCritterAt(tile));
        Assert.Equal(10, world.GetWhaleFallNutrition(tile));
        var consume = typeof(SimulationWorld).GetMethod("TryConsumeEnvironmentalNutrition", Flags)!;
        for (var remaining = 9; remaining >= 0; remaining--)
        {
            Assert.True((bool)consume.Invoke(world, [CritterSpecies.Worm, 0])!);
            Assert.Equal(remaining, world.GetWhaleFallNutrition(tile));
        }
        // Ordinary tile nutrients must not keep a depleted carcass visible.
        typeof(SimulationWorld).GetMethod("DepositTileNutrition", Flags)!.Invoke(world, [0]);
        Assert.True(world.GetTileNutrition(tile) > 0);
        Assert.Equal(0, world.GetWhaleFallNutrition(tile));
    }

    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void OnlyListedSpeciesAreExcludedFromWhaleFall()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 1, Terrain.Ocean) { SeasonsEnabled = false };
            world.SetTemperature(new(0, 0), 0.5f);
            world.AddCritter(CritterSpecies.BaleenWhale, new(0, 0));
            typeof(SimulationWorld).GetMethod("RemoveCritterAtIndex", Flags)!.Invoke(world, [0]);
            var consume = typeof(SimulationWorld).GetMethod("TryConsumeEnvironmentalNutrition", Flags)!;
            consume.Invoke(world, [species, 0]);
            var excluded = species is CritterSpecies.Deer or CritterSpecies.Elk or CritterSpecies.MegaSlug or
                CritterSpecies.Gazelle or CritterSpecies.ToothedWhale or CritterSpecies.Plankton or
                CritterSpecies.BaleenWhale;
            Assert.Equal(excluded ? 10 : 9, world.GetWhaleFallNutrition(new(0, 0)));
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
