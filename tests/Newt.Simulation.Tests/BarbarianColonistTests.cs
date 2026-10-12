using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class BarbarianColonistTests
{
    private static readonly GridPosition Camp = new(10, 10);
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(100, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnBarbarianApeVillage(Camp));
        return world;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    [InlineData(50)]
    public void CampEventLaunchesWithAnyFoodAndConsumesUpToFifty(int food)
    {
        var world = CreateWorld();
        world.StoreApeVillageFood(Camp, food);
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!.SetValue(world,
            (long)(60 * SimulationWorld.TicksPerSecond));
        var advance = typeof(SimulationWorld).GetMethod("AdvanceApeVillages", PrivateInstance)!;
        for (var roll = 0; roll < 5000 && !Residents(world).Any(critter => critter.IsColonist); roll++)
            advance.Invoke(world, null);
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        Assert.Equal(CritterSpecies.Ape, colonist.Species);
        Assert.False(world.IsBarbarianApe(colonist.Id));
        Assert.Equal(0, world.GetApeVillageFood(Camp));
        Assert.NotNull(colonist.ColonistDestination);
    }

    [Fact]
    public void OriginApesAndDogsCannotAttackColonistAndRegularWarriorsDoNotTargetIt()
    {
        var world = CreateWorld();
        Assert.True(world.TrySendApeColonist(Camp));
        var chief = Residents(world).Single(critter => critter.Species == CritterSpecies.ApeChieftain);
        var chiefIndex = Index(world, chief.Id);
        for (var roll = 0; roll < 5000 && world.GetApeVillageDogCount(Camp) < 5; roll++)
            world.TryRecruitDogFromWolfKill(chiefIndex);
        Assert.Equal(5, world.GetApeVillageDogCount(Camp));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var regular = world.AddCritter(CritterSpecies.ApeWarrior, new(80, 25));
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", PrivateInstance)!;
        foreach (var resident in Residents(world).Where(critter => critter.Id != colonist.Id))
            Assert.False((bool)canAttack.Invoke(world, [Index(world, resident.Id), Index(world, colonist.Id)])!);
        Assert.False(world.IsBarbarianApe(regular));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CampColonistCanFoundEitherVillageType(bool barbarian)
    {
        var world = CreateWorld();
        var destination = Enumerable.Range(50, 35).Select(x => new GridPosition(x, 10))
            .First(position => ((world.Seed ^ ((ulong)(uint)(position.Y * world.Width + position.X) *
                2_654_435_761UL)) % 100 < 50) == barbarian);
        Assert.True(world.TrySendApeColonist(destination));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var advance = typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!;
        for (var step = 0; step < 200 && world.GetApeStructure(destination) is null; step++)
            advance.Invoke(world, [Index(world, colonist.Id)]);
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(destination));
        Assert.Equal(barbarian, world.IsBarbarianVillage(destination));
        Assert.True(world.TryGetCritter(colonist.Id, out var founder));
        Assert.False(founder.IsColonist);
        Assert.Equal(barbarian, world.IsBarbarianApe(founder.Id));
    }

    [Theory]
    [InlineData(1, Terrain.Plains, true)]
    [InlineData(2, Terrain.Plains, false)]
    [InlineData(1, Terrain.Shallows, false)]
    public void LowEnergyColonistSettlesImmediatelyNearOrigin(int energy, Terrain terrain, bool settles)
    {
        var world = CreateWorld();
        Assert.True(world.TrySendApeColonist(new(60, 10)));
        var colonist = Assert.Single(Residents(world), critter => critter.IsColonist);
        var index = Index(world, colonist.Id);
        var position = new GridPosition(12, 10);
        world.SetTerrain(position, terrain);
        typeof(SimulationWorld).GetMethod("MoveCritter", PrivateInstance)!
            .Invoke(world, [index, position.Y * world.Width + position.X, position, false]);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", PrivateInstance)!.GetValue(world)!;
        energies[index] = energy;

        typeof(SimulationWorld).GetMethod("TryAdvanceApeSettler", PrivateInstance)!.Invoke(world, [index]);

        Assert.Equal(settles ? ApeStructureKind.Village : (ApeStructureKind?)null,
            world.GetApeStructure(position));
        Assert.True(world.TryGetCritter(colonist.Id, out var founder));
        Assert.Equal(!settles, founder.IsColonist);
        if (settles)
            Assert.Equal(CritterNutritions.Get(CritterSpecies.Ape).InitialEnergy, founder.Energy);
    }

    private static CritterSnapshot[] Residents(SimulationWorld world) =>
        Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();

    private static int Index(SimulationWorld world, CritterId id) =>
        Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
}

