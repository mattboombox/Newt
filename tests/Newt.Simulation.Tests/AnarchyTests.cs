using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class AnarchyTests
{
    [Fact]
    public void NaturalAnarchyUsesCivilWarEligibilityAndCooldown()
    {
        var world = CreateWorld(100);
        var tile = Village.Y * world.Width + Village.X;
        var tick = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!;
        NaturalEvents.SetEnabled(world, true);
        for (var i = 0; i < 500; i++) Assert.False(world.TryStartVillageWar(tile));
        tick.SetValue(world, (long)SimulationWorld.VillageWarCooldownTicks);
        NaturalEvents.SetEnabled(world, false);
        for (var i = 0; i < 500; i++) Assert.False(world.TryStartVillageWar(tile));
        NaturalEvents.SetEnabled(world, true);
        for (var attempt = 0; attempt < 50000 && world.GetBarbarianPopulationBySpecies().Sum() == 0; attempt++)
        {
            if (!world.TryStartVillageWar(tile)) continue;
            if (world.IsApeVillageAtWar(Village))
            {
                Assert.True(world.TryStopApeWar(Village));
                tick.SetValue(world, world.Tick + SimulationWorld.VillageWarCooldownTicks);
            }
        }
        Assert.Equal(99, world.GetBarbarianPopulationBySpecies().Sum());
        Assert.False(world.IsApeVillageAtWar(Village));
        var homes = Homes(world);
        // Refill the source so population eligibility cannot hide a missing cooldown.
        for (var i = 0; i < 99; i++)
            homes[world.AddCritter(CritterSpecies.Ape, new(30 + i % 20, 42 + i / 20)).Value] = tile;
        for (var i = 0; i < 5000; i++) Assert.False(world.TryStartVillageWar(tile));
        tick.SetValue(world, world.Tick + SimulationWorld.VillageWarCooldownTicks);
        var started = false;
        for (var i = 0; i < 10000 && !started; i++) started = world.TryStartVillageWar(tile);
        Assert.True(started);
    }

    [Fact]
    public void NaturalAnarchyRequiresTheSevereEventPopulationThreshold()
    {
        var world = CreateWorld(99);
        NaturalEvents.SetEnabled(world, true);
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!
            .SetValue(world, (long)SimulationWorld.VillageWarCooldownTicks);
        var tile = Village.Y * world.Width + Village.X;
        for (var i = 0; i < 5000; i++) Assert.False(world.TryStartVillageWar(tile));
        Assert.Equal(0, world.GetBarbarianPopulationBySpecies().Sum());
    }

    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Village = new(40, 30);
    private static Dictionary<int, int> Homes(SimulationWorld world) =>
        (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;

    private static SimulationWorld CreateWorld(int population)
    {
        var world = new SimulationWorld(100, 60, Terrain.Plains, seed: 17) { ApeVillageMergingEnabled = false };
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        var homes = Homes(world);
        var village = Village.Y * world.Width + Village.X;
        for (var i = 2; i < population; i++)
        {
            var position = new GridPosition(30 + i % 20, 32 + i / 20);
            homes[world.AddCritter(CritterSpecies.Ape, position).Value] = village;
        }
        return world;
    }

    [Fact]
    public void HundredUnemployedApesCreateNineFullCampsWithDogsAndLeaveWorkersAndOtherVillagesAlone()
    {
        var world = CreateWorld(100);
        var source = Village.Y * world.Width + Village.X;
        var homes = Homes(world);
        var workers = new[] { CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack, CritterSpecies.ApeScholar, CritterSpecies.ApeWarrior };
        var workerIds = workers.Select((role, i) => world.AddCritter(role, new(34 + i, 40))).ToArray();
        foreach (var id in workerIds) homes[id.Value] = source;
        var dogs = Enumerable.Range(0, 5).Select(i => world.AddCritter(CritterSpecies.Dog, new(34 + i, 41))).ToArray();
        foreach (var id in dogs) homes[id.Value] = source;
        var other = new GridPosition(80, 30);
        Assert.True(world.TrySpawnTestApeVillage(other));
        var otherResidents = homes.Where(pair => pair.Value == other.Y * world.Width + other.X).ToArray();
        var auxiliary = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeAuxiliaryVillages", Private)!.GetValue(world)!;
        var districts = Enumerable.Range(0, 9).Select(i => new GridPosition(36 + i, 28)).ToArray();
        foreach (var district in districts)
        {
            var tile = district.Y * world.Width + district.X;
            typeof(SimulationWorld).GetMethod("SetApeStructure", Private)!.Invoke(world, [tile, ApeStructureKind.ResidentialDistrict]);
            auxiliary[tile] = source;
        }
        var before = world.CritterCount;
        Assert.Equal(9, world.TriggerAnarchy(districts[0]));
        Assert.Equal(before, world.CritterCount);
        Assert.Equal(99, world.GetBarbarianPopulationBySpecies().Sum() - dogs.Length);
        Assert.Single(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            c => c.Species == CritterSpecies.Ape && world.GetApeHomeVillage(c.Id) == Village);
        foreach (var district in districts)
        {
            Assert.True(world.IsBarbarianVillage(district));
            Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(district));
            Assert.False(auxiliary.ContainsKey(district.Y * world.Width + district.X));
            Assert.Equal(11, world.GetApeVillagePopulationCapacity(district));
            var residents = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
                .Where(c => world.GetApeHomeVillage(c.Id) == district && c.Species != CritterSpecies.Dog).ToArray();
            Assert.Equal(11, residents.Length);
            Assert.Single(residents, c => c.Species == CritterSpecies.ApeChieftain);
            Assert.Equal(10, residents.Count(c => c.Species == CritterSpecies.ApeWarrior));
        }
        foreach (var id in dogs) Assert.True(world.IsBarbarianApe(id));
        for (var i = 0; i < workers.Length; i++)
        {
            Assert.True(world.TryGetCritter(workerIds[i], out var worker));
            Assert.Equal(workers[i], worker.Species);
            Assert.Equal(Village, world.GetApeHomeVillage(worker.Id));
        }
        Assert.All(otherResidents, pair => Assert.Equal(pair.Value, homes[pair.Key]));
        Assert.Equal(0, world.TriggerAnarchy(Village));
        Assert.True(world.TryStartApeWar(districts[0], other));
        Assert.Equal(11, Assert.Single(world.GetActiveApeWars()).Armies.Attacking.Fighters);
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(11, 1)]
    [InlineData(22, 2)]
    [InlineData(23, 2)]
    public void OnlyCompleteGroupsBecomeCampsAndFreeGroundCanBeUsed(int population, int camps)
    {
        var world = CreateWorld(population);
        Assert.Equal(camps, world.TriggerAnarchy(Village));
        Assert.Equal(camps * 11, world.GetBarbarianPopulationBySpecies().Sum());
        Assert.Equal(population % 11, Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Count(c => c.Species == CritterSpecies.Ape));
        if (population % 11 == 0)
        {
            Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(Village));
            Assert.Equal(0, world.GetApeVillageResidentCount(Village));
            Assert.DoesNotContain(Homes(world).Values, tile => tile == Village.Y * world.Width + Village.X);
            Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), critter =>
            {
                Assert.NotEqual(Village, world.GetApeHomeVillage(critter.Id));
                Assert.True(world.IsBarbarianVillage(world.GetApeHomeVillage(critter.Id)!.Value));
            });
        }
        else Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(Village));
    }

    [Fact]
    public void InvalidSelectionAndDisabledLifeDoNotConvertAnyone()
    {
        var world = CreateWorld(22);
        Assert.Equal(0, world.TriggerAnarchy(new(-1, -1)));
        Assert.Equal(0, world.TriggerAnarchy(new(0, 0)));
        LifeSystem.SetEnabled(world, false);
        Assert.Equal(0, world.TriggerAnarchy(Village));
        Assert.Equal(0, world.GetBarbarianPopulationBySpecies().Sum());
    }
}
