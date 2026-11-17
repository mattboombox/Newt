using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeWarriorDefenseTests
{
    [Theory]
    [InlineData(CritterSpecies.Wolf, Terrain.Plains)]
    [InlineData(CritterSpecies.ToothedWhale, Terrain.Shallows)]
    public void UnassignedWarriorsFollowTheirSquadLeaderAndHuntVillagePredators(
        CritterSpecies predator, Terrain predatorTerrain)
    {
        var village = new GridPosition(15, 10);
        var world = new SimulationWorld(35, 20, Terrain.Plains, seed: 1);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(village));
        Assert.True(world.TryBuildApeStructure(village.Y * world.Width + village.X, ApeStructureKind.ResidentialDistrict));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);

        AddResident(CritterSpecies.ApeChieftain, village);
        var escort = AddResident(CritterSpecies.ApeWarrior, new GridPosition(15, 11));
        for (var i = 1; i < SimulationWorld.ChieftainEscortSize; i++)
            AddResident(CritterSpecies.ApeWarrior, new GridPosition(15 + i, 11));

        var leaderPosition = new GridPosition(19, 10);
        var leader = AddResident(CritterSpecies.ApeWarrior, leaderPosition);
        var follower = AddResident(CritterSpecies.ApeWarrior, new GridPosition(19, 6));
        var predatorPosition = new GridPosition(20, 10);
        world.SetTerrain(predatorPosition, predatorTerrain);
        var threat = world.AddCritter(predator, predatorPosition);

        Assert.False(world.TryMoveVillageDefenseWarrior(IndexOf(escort), null, out _));
        Assert.True(world.TryMoveVillageDefenseWarrior(IndexOf(follower), null, out var followerPrey));
        Assert.Null(followerPrey);
        Assert.Equal(new GridPosition(19, 7), world.GetCritter(IndexOf(follower)).Position);
        Assert.True(world.TryMoveVillageDefenseWarrior(IndexOf(leader), null, out var leaderPrey));
        Assert.Equal(predatorPosition, leaderPrey);

        var energyBefore = world.GetCritter(IndexOf(leader)).Energy + world.GetCritter(IndexOf(threat)).Energy;
        world.CommitEncounter(leaderPosition, predatorPosition);
        if (world.TryGetCritter(leader, out var survivingLeader) && world.TryGetCritter(threat, out var survivingThreat))
            Assert.True(survivingLeader.Energy + survivingThreat.Energy < energyBefore);

        CritterId AddResident(CritterSpecies species, GridPosition position)
        {
            var id = world.AddCritter(species, position);
            Assert.True(world.TryAssignApeToVillage(id, village));
            return id;
        }

        int IndexOf(CritterId id) =>
            Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    public void WarriorPrioritizesNearbyBarbariansOverCloserWildlife(CritterSpecies raiderSpecies)
    {
        var world = new SimulationWorld(60, 20, Terrain.Plains, seed: 1);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        var village = new GridPosition(5, 10);
        var camp = new GridPosition(30, 10);
        Assert.True(world.TrySpawnTestApeVillage(village));
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);
        var position = new GridPosition(15, 10);
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, position);
        Assert.True(world.TryAssignApeToVillage(warrior, village));
        var wolfPosition = new GridPosition(14, 10);
        world.AddCritter(CritterSpecies.Wolf, wolfPosition);
        var raiderPosition = new GridPosition(19, 10);
        var raider = world.AddCritter(raiderSpecies, raiderPosition);
        Assert.True(world.TryAssignApeToVillage(raider, camp));
        var fartherRaiderPosition = new GridPosition(20, 10);
        var fartherRaider = world.AddCritter(raiderSpecies, fartherRaiderPosition);
        Assert.True(world.TryAssignApeToVillage(fartherRaider, camp));
        var friendlyPosition = new GridPosition(16, 10);
        var friendly = world.AddCritter(CritterSpecies.Ape, friendlyPosition);
        Assert.True(world.TryAssignApeToVillage(friendly, village));
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == warrior);

        Assert.Equal(raiderPosition, world.FindHunterPrey(index, CritterSpecies.ApeWarrior, 6, null));
        Assert.Equal(fartherRaiderPosition, world.FindHunterPrey(index, CritterSpecies.ApeWarrior, 6,
            new HashSet<GridPosition> { raiderPosition }));
        Assert.Equal(wolfPosition, world.FindHunterPrey(index, CritterSpecies.ApeWarrior, 3, null));
        Assert.Equal(wolfPosition, world.FindHunterPrey(index, CritterSpecies.ApeWarrior, 6,
            new HashSet<GridPosition> { raiderPosition, fartherRaiderPosition }));

        Assert.True(world.TryGetCritter(friendly, out var beforeFriendly));
        world.CommitEncounter(position, friendlyPosition);
        Assert.True(world.TryGetCritter(friendly, out var afterFriendly));
        Assert.Equal(beforeFriendly.Energy, afterFriendly.Energy);
        Assert.True(world.TryGetCritter(warrior, out var beforeWarrior));
        Assert.True(world.TryGetCritter(raider, out var beforeRaider));
        world.CommitEncounter(position, raiderPosition);
        Assert.True(world.TryGetCritter(warrior, out var afterWarrior));
        Assert.True(!world.TryGetCritter(raider, out var afterRaider) ||
            afterWarrior.Energy + afterRaider.Energy < beforeWarrior.Energy + beforeRaider.Energy);
    }
}
