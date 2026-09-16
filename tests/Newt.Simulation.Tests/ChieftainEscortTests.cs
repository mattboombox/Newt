using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ChieftainEscortTests
{
    private static readonly GridPosition Village = new(10, 10);

    private static (SimulationWorld World, CritterId Chief) CreateWorld(bool barbarian)
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        Assert.True(barbarian ? world.TrySpawnBarbarianApeVillage(Village) : world.TrySpawnTestApeVillage(Village));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);
        if (!barbarian)
            Assert.True(world.TryPlaceApeBuilding(new GridPosition(11, 10), ApeStructureKind.ResidentialDistrict));
        var chief = world.AddCritter(CritterSpecies.ApeChieftain, Village);
        Assert.True(world.TryAssignApeToVillage(chief, Village));
        for (var x = 2; x < 8; x++)
        {
            var warrior = world.AddCritter(CritterSpecies.ApeWarrior, new GridPosition(x, 5));
            Assert.True(world.TryAssignApeToVillage(warrior, Village));
        }
        for (var x = 2; x < 7; x++)
            Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, new GridPosition(x, 20)));
        return (world, chief);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SquadHasFourMembersAndExcludesReservedDogs(bool barbarian)
    {
        var (world, chief) = CreateWorld(barbarian);
        var squad = world.GetChieftainEscort(Village);
        Assert.Equal(4, squad.Length);
        var members = squad.Select(id => { Assert.True(world.TryGetCritter(id, out var critter)); return critter; }).ToArray();
        Assert.Equal(2, members.Count(critter => critter.Species is CritterSpecies.ApeWarrior));
        Assert.Equal(2, members.Count(critter => critter.Species is CritterSpecies.Dog));
        var dogs = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Dog).OrderBy(critter => critter.Id.Value).ToArray();
        Assert.DoesNotContain(dogs[0].Id, squad);
        Assert.DoesNotContain(dogs[1].Id, squad);
        Assert.True(world.TryGetCritter(squad[0], out var removed));
        world.RemoveCritterAt(removed.Position);
        Assert.Equal(4, world.GetChieftainEscort(Village).Length);
        Assert.DoesNotContain(removed.Id, world.GetChieftainEscort(Village));
        Assert.True(world.TryGetCritter(chief, out var leader));
        world.RemoveCritterAt(leader.Position);
        Assert.Empty(world.GetChieftainEscort(Village));
    }

    [Theory]
    [InlineData(false, CritterSpecies.ApeWarrior)]
    [InlineData(true, CritterSpecies.ApeWarrior)]
    [InlineData(false, CritterSpecies.Dog)]
    [InlineData(true, CritterSpecies.Dog)]
    public void EscortsApproachChiefAndHoldNearby(bool barbarian, CritterSpecies species)
    {
        var (world, _) = CreateWorld(barbarian);
        var escort = world.GetChieftainEscort(Village).First(id =>
            world.TryGetCritter(id, out var critter) && critter.Species == species);
        var index = Enumerable.Range(0, world.CritterCount).Single(index => world.GetCritter(index).Id == escort);
        for (var step = 0; step < 30; step++)
        {
            Assert.True(world.TryFollowVillageChieftain(index, null, out var prey));
            Assert.Null(prey);
        }
        Assert.True(world.TryGetCritter(escort, out var follower));
        Assert.InRange(Math.Abs(follower.Position.X - Village.X) + Math.Abs(follower.Position.Y - Village.Y), 1, 2);
        var stopped = follower.Position;
        Assert.True(world.TryFollowVillageChieftain(index, null, out _));
        Assert.Equal(stopped, world.GetCritter(index).Position);
        var outsider = Enumerable.Range(0, world.CritterCount).First(i => world.GetCritter(i).Species is CritterSpecies.ApeWarrior &&
            !world.GetChieftainEscort(Village).Contains(world.GetCritter(i).Id));
        Assert.False(world.TryFollowVillageChieftain(outsider, null, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EscortStillFightsAdjacentEnemy(bool barbarian)
    {
        var (world, _) = CreateWorld(barbarian);
        var escort = world.GetChieftainEscort(Village)[0];
        var index = Enumerable.Range(0, world.CritterCount).Single(index => world.GetCritter(index).Id == escort);
        var current = world.GetCritter(index).Position;
        var enemy = new GridPosition(current.X, current.Y - 1);
        world.AddCritter(CritterSpecies.Wolf, enemy);
        Assert.True(world.TryFollowVillageChieftain(index, null, out var prey));
        Assert.Equal(enemy, prey);
    }
}
