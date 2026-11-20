using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeWarriorDefenseTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SquadLeadershipDoesNotGrantVeterancyAndCombatVeteransTakeOver(int existingVeterans)
    {
        var world = new SimulationWorld(50, 40, Terrain.Plains, seed: 1);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var village = new GridPosition(20, 20);
        Assert.True(world.TrySpawnTestApeVillage(village));
        Assert.True(world.TryBuildApeStructure(village.Y * world.Width + village.X, ApeStructureKind.ResidentialDistrict));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var fighters = Enumerable.Range(0, 8).Select(i =>
        {
            var id = world.AddCritter(CritterSpecies.ApeWarrior, new(20 + i, 22));
            Assert.True(world.TryAssignApeToVillage(id, village));
            return id;
        }).ToArray();
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var kills = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_warriorCombatKills", flags)!.GetValue(world)!;
        if (existingVeterans > 0) kills[fighters[^1].Value] = 5;
        var squads = (System.Collections.IDictionary)typeof(SimulationWorld)
            .GetMethod("BuildVillageDefenseSquads", flags)!.Invoke(world, [village.Y * world.Width + village.X])!;
        var leaders = fighters.Select(id => (int)squads[id.Value]!.GetType().GetProperty("LeaderId")!
            .GetValue(squads[id.Value])!).Distinct().ToArray();
        Assert.Equal(2, leaders.Length);
        if (existingVeterans > 0) Assert.Contains(fighters[^1].Value, leaders);
        foreach (var id in fighters)
        {
            Assert.True(world.TryGetCritter(id, out var warrior));
            var earnedVeterancy = existingVeterans > 0 && id == fighters[^1];
            Assert.Equal(earnedVeterancy, warrior.IsVeteranWarrior);
            Assert.Equal(earnedVeterancy ? 6 : 4, world.GetCritterCombatDamage(id));
            Assert.Equal(existingVeterans > 0 && id == fighters[^1] ? 5 : 0, warrior.CombatKills);
        }
        var follower = fighters.First(id => !leaders.Contains(id.Value));
        var followerIndex = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == follower);
        var recordKill = typeof(SimulationWorld).GetMethod("RecordWarriorCombatKill", flags)!;
        for (var i = 0; i < 5; i++) recordKill.Invoke(world, [followerIndex]);
        squads = (System.Collections.IDictionary)typeof(SimulationWorld)
            .GetMethod("BuildVillageDefenseSquads", flags)!.Invoke(world, [village.Y * world.Width + village.X])!;
        Assert.Equal(follower.Value, (int)squads[follower.Value]!.GetType().GetProperty("LeaderId")!
            .GetValue(squads[follower.Value])!);
        Assert.True(world.TryGetCritter(follower, out var promoted));
        Assert.True(promoted.IsVeteranWarrior);
        Assert.Equal(5, promoted.CombatKills);
        Assert.Equal(6, world.GetCritterCombatDamage(follower));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void MovementLoopLetsVeteranLeaderPassFollowerWithoutLosingOccupants(bool cramped, bool enemyPresent)
    {
        var world = new SimulationWorld(50, 40, Terrain.Plains, seed: 1);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        var home = new GridPosition(20, 20);
        Assert.True(world.TrySpawnTestApeVillage(home));
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
        var follower = world.AddCritter(CritterSpecies.ApeWarrior, new(21, 20));
        var leader = world.AddCritter(CritterSpecies.ApeWarrior, home);
        Assert.True(world.TryAssignApeToVillage(follower, home));
        Assert.True(world.TryAssignApeToVillage(leader, home));
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var kills = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_warriorCombatKills", flags)!.GetValue(world)!;
        kills[leader.Value] = 5;
        for (var y = 19; y <= 21; y++)
        for (var x = 19; x <= 22; x++)
            if (!(y == 20 && x >= 20) && !(x == 21 && y == 21 && !cramped))
                world.SetTerrain(new(x, y), Terrain.MegaWall);
        if (cramped) world.SetTerrain(new(22, 20), Terrain.MegaWall);
        if (enemyPresent) world.AddCritter(CritterSpecies.Wolf, new(23, 20));
        // Full energy with spare housing must not park an idle squad at a building.
        typeof(SimulationWorld).GetMethod("SetApeStructure", flags)!.Invoke(world,
            [18 * world.Width + 18, ApeStructureKind.ResidentialDistrict]);
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeAuxiliaryVillages", flags)!.GetValue(world)!)
            [18 * world.Width + 18] = home.Y * world.Width + home.X;
        var energy = (int[])typeof(SimulationWorld).GetField("_energy", flags)!.GetValue(world)!;
        energy[IndexOf(leader)] = CritterNutritions.Get(CritterSpecies.ApeWarrior).MaximumEnergy;
        var nextMovement = (long[])typeof(SimulationWorld).GetField("_nextMovementTicks", flags)!.GetValue(world)!;
        for (var i = 0; i < world.CritterCount; i++) nextMovement[i] = long.MaxValue;
        nextMovement[IndexOf(leader)] = 0;
        typeof(SimulationWorld).GetMethod("AdvanceCritterMovements", flags)!.Invoke(world, null);
        Assert.True(world.TryGetCritter(leader, out var movedLeader));
        Assert.True(movedLeader.IsVeteranWarrior); // The renderer selects the veteran sprite from this flag.
        Assert.Equal(6, world.GetCritterCombatDamage(leader));
        Assert.Equal(new GridPosition(21, 20), movedLeader.Position);
        Assert.True(world.TryGetCritter(follower, out var movedFollower));
        Assert.NotEqual(movedLeader.Position, movedFollower.Position);
        Assert.True(world.TryGetCritterAt(movedLeader.Position, out var leaderOccupant));
        Assert.Equal(leader, leaderOccupant.Id);
        Assert.True(world.TryGetCritterAt(movedFollower.Position, out var followerOccupant));
        Assert.Equal(follower, followerOccupant.Id);
        if (cramped) Assert.Equal(home, movedFollower.Position);

        int IndexOf(CritterId id) => Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
    }

    [Fact]
    public void VeteransLeadSeparateSquadsAndIdleLeadersPatrolWhileFollowersCatchUp()
    {
        var village = new GridPosition(20, 20);
        var world = new SimulationWorld(50, 40, Terrain.Plains, seed: 1);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(village));
        Assert.True(world.TryBuildApeStructure(village.Y * world.Width + village.X, ApeStructureKind.ResidentialDistrict));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);
        var fighters = Enumerable.Range(0, 8).Select(i =>
        {
            var position = i < 6 ? new GridPosition(10 + i, 18) : new GridPosition(17, 18 + (i - 6) * 6);
            var id = world.AddCritter(CritterSpecies.ApeWarrior, position);
            Assert.True(world.TryAssignApeToVillage(id, village));
            return id;
        }).ToArray();
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var kills = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_warriorCombatKills", flags)!.GetValue(world)!;
        kills[fighters[6].Value] = kills[fighters[7].Value] = 5;
        var squads = (System.Collections.IDictionary)typeof(SimulationWorld)
            .GetMethod("BuildVillageDefenseSquads", flags)!.Invoke(world, [village.Y * world.Width + village.X])!;
        var groups = fighters.GroupBy(id => (int)squads[id.Value]!.GetType()
            .GetProperty("LeaderId")!.GetValue(squads[id.Value])!).ToArray();
        Assert.Equal(2, groups.Length);
        Assert.All(groups, group =>
        {
            Assert.Contains(group.Key, new[] { fighters[6].Value, fighters[7].Value });
            Assert.Equal(4, group.Count());
        });
        var followerGroup = groups.Single(group => group.Contains(fighters[0]));
        var leader = new CritterId(followerGroup.Key);
        Assert.True(world.TryGetCritter(leader, out var beforeLeader));
        Assert.True(world.TryMoveVillageDefenseWarrior(IndexOf(leader), null, out var prey));
        Assert.Null(prey);
        Assert.True(world.TryGetCritter(leader, out var afterLeader));
        Assert.NotEqual(beforeLeader.Position, afterLeader.Position);
        Assert.True(world.TryGetCritter(fighters[0], out var beforeFollower));
        Assert.True(world.TryMoveVillageDefenseWarrior(IndexOf(fighters[0]), null, out prey));
        Assert.Null(prey);
        Assert.True(world.TryGetCritter(fighters[0], out var afterFollower));
        Assert.True(Distance(afterFollower.Position, afterLeader.Position) < Distance(beforeFollower.Position, afterLeader.Position));
        // Losing the leader elects a surviving warrior instead of leaving an orphaned squad.
        world.RemoveCritterAt(afterLeader.Position);
        Assert.True(world.TryMoveVillageDefenseWarrior(IndexOf(fighters[0]), null, out _));

        int IndexOf(CritterId id) => Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
        static int Distance(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }

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
