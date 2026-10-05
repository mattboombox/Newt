using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VampireColonyTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Lair = new(5, 10);
    private static readonly GridPosition Village = new(40, 10);

    private static SimulationWorld CreateWorld(int population, bool full = false)
    {
        var world = new SimulationWorld(100, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        world.AddCritter(CritterSpecies.Vampire, Lair);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", Private)!.GetValue(world)!;
        energies[0] = 12;
        world.AdvanceOneTick();
        if (full)
        {
            for (var i = 0; i < world.CritterCount; i++) energies[i] = 12;
            world.AdvanceOneTick();
        }
        Assert.True(world.TrySpawnTestApeVillage(Village));
        Assert.True(world.TrySpawnTestApeVillage(new(10, 10)));
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        var villageTile = Village.Y * world.Width + Village.X;
        while (homes.Count(pair => pair.Value == villageTile) < population)
        {
            var count = homes.Count(pair => pair.Value == villageTile);
            var ape = world.AddCritter(CritterSpecies.Ape, new(60 + count % 20, 15 + count / 20));
            homes[ape.Value] = villageTile;
        }
        return world;
    }

    private static CritterSnapshot Colonist(SimulationWorld world) =>
        Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).Single(c => c.IsColonist);

    private static void Move(SimulationWorld world, CritterId id)
    {
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
        typeof(SimulationWorld).GetMethod("TryMoveVampire", Private)!.Invoke(world, [index, null]);
    }

    [Fact]
    public void ColonyToolRequiresFiftyResidentsAndUsesApeColonistEnergy()
    {
        var small = CreateWorld(49);
        Assert.False(small.TrySendApeColonist(Lair));
        var world = CreateWorld(50);
        Assert.True(world.TrySendApeColonist(Lair));
        var colonist = Colonist(world);
        Assert.Equal(CritterSpecies.Vampire, colonist.Species);
        Assert.Equal(100, colonist.Energy);
        Assert.Equal(100, colonist.MaximumEnergy);
        Assert.False(colonist.CanReproduce);
        Assert.False(world.TrySendApeColonist(Lair));
        Assert.Equal(2, world.GetVampireLairPopulation(Lair));
    }

    [Fact]
    public void ColonistCrossesSeaAndFoundsTwoResidentLairNearVillage()
    {
        var world = CreateWorld(50);
        for (var y = 0; y < world.Height; y++) world.SetTerrain(new(20, y), Terrain.Ocean);
        Assert.True(world.TrySendApeColonist(Lair));
        var colonist = Colonist(world);
        var destination = colonist.ColonistDestination!.Value;
        var crossedSea = false;
        for (var step = 0; step < 100 && world.TryGetCritter(colonist.Id, out var current) && current.IsColonist; step++)
        {
            Move(world, colonist.Id);
            world.TryGetCritter(colonist.Id, out current);
            crossedSea |= current.Position.X == 20;
        }
        Assert.True(crossedSea);
        Assert.True(world.HasVampireLair(destination));
        Assert.Equal(2, world.GetVampireLairPopulation(destination));
        Assert.InRange(Math.Abs(destination.X - Village.X) + Math.Abs(destination.Y - Village.Y), 2, 8);
        Assert.True(world.TryGetCritter(colonist.Id, out var settled));
        Assert.False(settled.IsColonist);
        Assert.Equal(2, world.GetVampireLairPopulation(Lair));
    }

    [Fact]
    public void ShrinkingVillageInvalidatesDestination()
    {
        var world = CreateWorld(50);
        Assert.True(world.TrySendApeColonist(Lair));
        var colonist = Colonist(world);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        homes.Remove(homes.First(pair => pair.Value == Village.Y * world.Width + Village.X).Key);
        Move(world, colonist.Id);
        Assert.Null(Colonist(world).ColonistDestination);
        Assert.False(world.HasVampireLair(colonist.ColonistDestination!.Value));
    }

    [Fact]
    public void SourceHuntingVillageIsExcludedEvenWhenItHasFiftyResidents()
    {
        var world = CreateWorld(50);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        foreach (var id in homes.Keys.ToArray()) homes[id] = 10 * world.Width + 10;
        Assert.False(world.TrySendApeColonist(Lair));
    }

    [Fact]
    public void ImpassableMountainBarriersPreventColonyLaunch()
    {
        var world = CreateWorld(50);
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(20, y), Terrain.Mountain);
            world.SetTerrain(new(0, y), Terrain.Mountain);
        }
        Assert.False(world.TrySendApeColonist(Lair));
    }

    [Fact]
    public void RemovingColonistClearsTravelStateAndPermitsAnotherLaunch()
    {
        var world = CreateWorld(50);
        Assert.True(world.TrySendApeColonist(Lair));
        world.RemoveCritterAt(Colonist(world).Position);
        Assert.True(world.TrySendApeColonist(Lair));
    }

    [Fact]
    public void FullLairsLaunchNaturalColoniesOnlyWhenEventsEnabled()
    {
        var world = CreateWorld(50, full: true);
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, 60L * SimulationWorld.TicksPerSecond);
        var tile = Lair.Y * world.Width + Lair.X;
        Assert.False(world.TryLaunchVampireColony(tile));
        NaturalEvents.SetEnabled(world, true);
        var launched = false;
        for (var attempt = 0; attempt < 1000 && !launched; attempt++) launched = world.TryLaunchVampireColony(tile);
        Assert.True(launched);
        Assert.False(world.TryLaunchVampireColony(tile));
        Assert.Equal(3, world.GetVampireLairPopulation(Lair));
    }
}


