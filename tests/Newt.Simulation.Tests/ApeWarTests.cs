using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeWarTests
{
    private static readonly GridPosition First = new(10, 10), Second = new(30, 10);
    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(50, 30, Terrain.Plains, seed: 17);
        world.ApeVillageMergingEnabled = false;
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        Assert.True(world.TryPlaceApeBuilding(new(9, 10), ApeStructureKind.ResidentialDistrict));
        Assert.True(world.TryPlaceApeBuilding(new(29, 10), ApeStructureKind.ResidentialDistrict));
        for (var i = world.CritterCount - 1; i >= 0; i--)
            world.RemoveCritterAt(world.GetCritter(i).Position);
        foreach (var village in new[] { First, Second })
            for (var i = 0; i < 10; i++)
            {
                var id = world.AddCritter(CritterSpecies.Ape, new(village.X + i % 5, village.Y + i / 5));
                Assert.True(world.TryAssignApeToVillage(id, village));
            }
        return world;
    }
    private static CritterSnapshot[] Residents(SimulationWorld world, GridPosition village) =>
        Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(c => world.GetApeHomeVillage(c.Id) == village).ToArray();

    [Theory]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    [InlineData(CritterSpecies.Ape)]
    public void MilitaryCanCrossWaterOnlyDuringWarAndReturnAfterPeace(CritterSpecies species)
    {
        var world = CreateWorld();
        var position = new GridPosition(19, 15);
        world.RemoveCritterAt(Residents(world, First).First().Position);
        world.RemoveCritterAt(Residents(world, First).First().Position);
        var id = world.AddCritter(species, position);
        Assert.True(world.TryAssignApeToVillage(id, First));
        // Two ocean barriers prevent a land route, including around the wrapped world.
        for (var y = 0; y < world.Height; y++)
            foreach (var x in new[] { 0, 20, 21, 22 })
                world.SetTerrain(new(x, y), Terrain.Ocean);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var canLive = typeof(SimulationWorld).GetMethod("CanCritterLiveOn", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var water = 15 * world.Width + 20;
        Assert.False((bool)canLive.Invoke(world, [indices[id.Value], water])!);
        Assert.True(world.TryStartApeWar(First, Second));
        Assert.True((bool)canLive.Invoke(world, [indices[id.Value], water])!);
        var move = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        move.Invoke(world, [indices[id.Value], null]);
        Assert.True(world.TryGetCritter(id, out var afloat));
        Assert.Equal(20, afloat.Position.X);
        Assert.Equal(species == CritterSpecies.Ape ? CritterSpecies.ApeWarrior : species, afloat.Species);
        foreach (var enemy in Residents(world, Second).Where(c => c.Species == CritterSpecies.ApeWarrior))
            world.RemoveCritterAt(enemy.Position);
        world.AdvanceApeWars();
        Assert.False(world.IsApeVillageAtWar(First));
        Assert.True((bool)canLive.Invoke(world, [indices[id.Value], water])!);
        var returnHome = typeof(SimulationWorld).GetMethod("TryReturnWarSailor", BindingFlags.Instance | BindingFlags.NonPublic)!;
        returnHome.Invoke(world, [indices[id.Value], null]);
        Assert.True(world.TryGetCritter(id, out var landed));
        Assert.Equal(19, landed.Position.X);
        Assert.Equal(species, landed.Species);
        Assert.False((bool)canLive.Invoke(world, [indices[id.Value], water])!);
    }

    [Fact]
    public void BothSidesLevyNinetyPercentAndCannotJoinAnotherWar()
    {
        var world = CreateWorld();
        Assert.False(world.TryStartApeWar(First, First));
        Assert.True(world.TryStartApeWar(First, Second));
        foreach (var village in new[] { First, Second })
        {
            Assert.True(world.IsApeVillageAtWar(village));
            Assert.Equal(9, Residents(world, village).Count(c => c.Species == CritterSpecies.ApeWarrior));
            Assert.Single(Residents(world, village), c => c.Species == CritterSpecies.Ape);
        }
        Assert.False(world.TryStartApeWar(Second, First));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeWarrior, 0)]
    [InlineData(CritterSpecies.ApeChieftain, 0)]
    [InlineData(CritterSpecies.ApeWarrior, 1)]
    [InlineData(CritterSpecies.ApeWarrior, 2)]
    public void CrowdedArmySidestepsOnlyWhenAnOpenRouteLeadsForward(CritterSpecies species, int obstruction)
    {
        var world = CreateWorld();
        foreach (var resident in Residents(world, First).Take(4)) world.RemoveCritterAt(resident.Position);
        var start = new GridPosition(15, 10);
        var id = world.AddCritter(species, start);
        Assert.True(world.TryAssignApeToVillage(id, First));
        var allies = new List<CritterId>();
        for (var y = 9; y <= 11; y++)
        {
            var ally = world.AddCritter(CritterSpecies.ApeWarrior, new(16, y));
            Assert.True(world.TryAssignApeToVillage(ally, First));
            allies.Add(ally);
        }
        var reserved = new HashSet<GridPosition>();
        for (var y = 6; y <= 14; y++)
            for (var x = 11; x <= 19; x++)
            {
                if (y == 10) continue;
                if (obstruction == 1) reserved.Add(new(x, y));
                if (obstruction == 2 && x != 16) world.SetTerrain(new(x, y), Terrain.Mountain);
            }
        Assert.True(world.TryStartApeWar(First, Second));
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var mover = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var visited = new HashSet<GridPosition> { start };
        for (var step = 0; step < 4; step++)
        {
            Assert.Null(mover.Invoke(world, [indices[id.Value], reserved]));
            Assert.True(world.TryGetCritter(id, out var moved));
            if (obstruction != 0)
                Assert.Equal(start, moved.Position);
            else
            {
                Assert.True(visited.Add(moved.Position), "Sidestepping must not oscillate between tiles.");
                if (step == 0) Assert.Equal(start.X, moved.Position.X);
                if (step == 3) Assert.True(moved.Position.X > start.X);
            }
        }
        for (var i = 0; i < allies.Count; i++)
        {
            Assert.True(world.TryGetCritter(allies[i], out var ally));
            Assert.Equal(new GridPosition(16, 9 + i), ally.Position);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefeatTransfersAllStoresOnceAndDemobilizesSurvivors(bool attackerLoses)
    {
        var world = CreateWorld();
        var loser = attackerLoses ? First : Second;
        var winner = attackerLoses ? Second : First;
        world.StoreApeVillageFood(loser, 10);
        world.StoreApeVillageFood(winner, 5);
        var totalFood = world.GetApeVillageFood(loser) + world.GetApeVillageFood(winner);
        var wood = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageWood", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        wood[loser.Y * world.Width + loser.X] = 12;
        wood[winner.Y * world.Width + winner.X] = 3;
        Assert.True(world.TryStartApeWar(First, Second));
        var army = Residents(world, loser).Where(c => c.Species == CritterSpecies.ApeWarrior).ToArray();
        foreach (var c in army.Take(8)) world.RemoveCritterAt(c.Position);
        world.AdvanceApeWars();
        Assert.True(world.IsApeVillageAtWar(loser));
        world.RemoveCritterAt(army[8].Position);
        world.AdvanceApeWars();
        Assert.False(world.IsApeVillageAtWar(loser));
        Assert.Equal(0, world.GetApeVillageFood(loser));
        Assert.Equal(totalFood, world.GetApeVillageFood(winner));
        Assert.Equal(15, world.GetApeVillageWood(winner));
        Assert.Equal(0, world.GetApeVillageWood(loser));
        Assert.All(Residents(world, winner), c => Assert.Equal(CritterSpecies.Ape, c.Species));
        world.AdvanceApeWars();
        Assert.Equal(totalFood, world.GetApeVillageFood(winner));
    }

    [Fact]
    public void ArmiesMarchAndFightWhenWarIsStartedFromBuildings()
    {
        var world = CreateWorld();
        Assert.True(world.TryStartApeWar(new(9, 10), new(29, 10)));
        var mover = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var fought = false;
        for (var step = 0; step < 300 && world.IsApeVillageAtWar(First); step++)
        {
            var ids = Enumerable.Range(0, world.CritterCount).Select(i => world.GetCritter(i).Id).ToArray();
            foreach (var id in ids)
            {
                if (!world.TryGetCritter(id, out var critter) || critter.Species != CritterSpecies.ApeWarrior) continue;
                if (mover.Invoke(world, [indices[id.Value], null]) is GridPosition enemy)
                {
                    fought = true;
                    world.CommitEncounter(critter.Position, enemy);
                }
            }
            world.AdvanceApeWars();
        }
        Assert.True(fought);
        Assert.False(world.IsApeVillageAtWar(First));
    }

    [Fact]
    public void OpposingWarriorsCanFightOnlyDuringWar()
    {
        var world = CreateWorld();
        Assert.True(world.TryStartApeWar(First, Second));
        var first = Residents(world, First).First(c => c.Species == CritterSpecies.ApeWarrior);
        var second = Residents(world, Second).First(c => c.Species == CritterSpecies.ApeWarrior);
        var energy = first.Energy + second.Energy;
        world.CommitEncounter(first.Position, second.Position);
        Assert.True(world.TryGetCritter(first.Id, out first));
        Assert.True(world.TryGetCritter(second.Id, out second));
        Assert.True(first.Energy + second.Energy < energy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnlyBloodWarAttackersTargetWorkers(bool bloodWar)
    {
        var world = CreateWorld();
        var workers = new List<CritterId>();
        foreach (var village in new[] { First, Second })
        {
            var resident = Residents(world, village).First();
            world.RemoveCritterAt(resident.Position);
            var worker = world.AddCritter(CritterSpecies.ApeFarmer, resident.Position);
            Assert.True(world.TryAssignApeToVillage(worker, village));
            workers.Add(worker);
        }
        Assert.True(world.TryStartApeWar(First, Second, bloodWar));
        var attacker = Residents(world, First).First(c => c.Species == CritterSpecies.ApeWarrior);
        var defender = Residents(world, Second).First(c => c.Species == CritterSpecies.ApeWarrior);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(bloodWar, (bool)canAttack.Invoke(world, [indices[attacker.Id.Value], indices[workers[1].Value]])!);
        Assert.False((bool)canAttack.Invoke(world, [indices[defender.Id.Value], indices[workers[0].Value]])!);
        Assert.False((bool)canAttack.Invoke(world, [indices[attacker.Id.Value], indices[workers[0].Value]])!);
    }

    [Fact]
    public void BloodWarContinuesAfterArmyDefeatAndPursuesLastDistantWorker()
    {
        var world = CreateWorld();
        world.RemoveCritterAt(Residents(world, Second).First().Position);
        var worker = world.AddCritter(CritterSpecies.ApeLumberjack, new(20, 25));
        Assert.True(world.TryAssignApeToVillage(worker, Second));
        Assert.True(world.TryStartApeWar(First, Second, bloodWar: true));
        // A worker arriving after mobilization is still a blood-war target.
        world.RemoveCritterAt(new(20, 25));
        worker = world.AddCritter(CritterSpecies.ApeLumberjack, new(20, 25));
        Assert.True(world.TryAssignApeToVillage(worker, Second));
        foreach (var resident in Residents(world, Second).Where(c => c.Id != worker))
            world.RemoveCritterAt(resident.Position);
        world.AdvanceApeWars();
        Assert.True(world.IsApeVillageAtWar(First));
        var attacker = Residents(world, First).First(c => c.Species == CritterSpecies.ApeWarrior);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var mover = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var reached = false;
        for (var step = 0; step < 40; step++)
            if (mover.Invoke(world, [indices[attacker.Id.Value], null]) is GridPosition target)
            {
                Assert.Equal(new GridPosition(20, 25), target);
                reached = true;
                break;
            }
        Assert.True(reached);
        world.RemoveCritterAt(new(20, 25));
        world.AdvanceApeWars();
        Assert.False(world.IsApeVillageAtWar(First));
    }

    [Fact]
    public void BloodWarEndsAtNinetyPercentAttackerLosses()
    {
        var world = CreateWorld();
        // Ten initial warriors make the 90% threshold exact.
        var resident = Residents(world, First).First();
        world.RemoveCritterAt(resident.Position);
        var standing = world.AddCritter(CritterSpecies.ApeWarrior, resident.Position);
        Assert.True(world.TryAssignApeToVillage(standing, First));
        Assert.True(world.TryStartApeWar(First, Second, bloodWar: true));
        var army = Residents(world, First).Where(c => c.Species == CritterSpecies.ApeWarrior).ToArray();
        Assert.Equal(10, army.Length);
        foreach (var unit in army.Take(8)) world.RemoveCritterAt(unit.Position);
        world.AdvanceApeWars();
        Assert.True(world.IsApeVillageAtWar(First));
        world.RemoveCritterAt(army[8].Position);
        world.AdvanceApeWars();
        Assert.False(world.IsApeVillageAtWar(First));
        Assert.NotEmpty(Residents(world, Second));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StoppingEitherSideEndsEitherWarWithoutSpoils(bool bloodWar, bool stopDefender)
    {
        var world = CreateWorld();
        world.StoreApeVillageFood(First, 10);
        world.StoreApeVillageFood(Second, 5);
        var firstFood = world.GetApeVillageFood(First);
        var secondFood = world.GetApeVillageFood(Second);
        Assert.False(world.TryStopApeWar(First));
        Assert.True(world.TryStartApeWar(First, Second, bloodWar));
        // Associated buildings must work just like clicking the village itself.
        Assert.True(world.TryStopApeWar(stopDefender ? new(29, 10) : new(9, 10)));
        Assert.False(world.IsApeVillageAtWar(First));
        Assert.False(world.IsApeVillageAtWar(Second));
        Assert.Equal(firstFood, world.GetApeVillageFood(First));
        Assert.Equal(secondFood, world.GetApeVillageFood(Second));
        Assert.All(Residents(world, First).Concat(Residents(world, Second)),
            c => Assert.Equal(CritterSpecies.Ape, c.Species));
        Assert.False(world.TryStopApeWar(Second));
        Assert.True(world.TryStartApeWar(First, Second, bloodWar));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefenderCanStopWarAfterAllAttackersDisappear(bool bloodWar)
    {
        var world = CreateWorld();
        Assert.True(world.TryStartApeWar(First, Second, bloodWar));
        foreach (var resident in Residents(world, First)) world.RemoveCritterAt(resident.Position);
        Assert.True(world.TryStopApeWar(Second));
        Assert.False(world.IsApeVillageAtWar(Second));
        Assert.False(world.IsApeVillageAtWar(First));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CivilWarSplitsResidentsAndRestoresJobsWithoutTransferringStores(bool manualStop)
    {
        var world = CreateWorld();
        var replaced = Residents(world, First).First();
        world.RemoveCritterAt(replaced.Position);
        var farmer = world.AddCritter(CritterSpecies.ApeFarmer, replaced.Position);
        Assert.True(world.TryAssignApeToVillage(farmer, First));
        var roles = Residents(world, First).ToDictionary(c => c.Id, c => c.Species);
        world.StoreApeVillageFood(First, 10);
        var food = world.GetApeVillageFood(First);
        var wood = world.GetApeVillageWood(First);
        Assert.True(world.TryStartApeCivilWar(new(9, 10)));
        Assert.False(world.TryStartApeCivilWar(First));
        Assert.False(world.TryStartApeWar(First, Second));
        var residents = Residents(world, First);
        Assert.All(residents, c => Assert.Equal(CritterSpecies.ApeWarrior, c.Species));
        var red = residents.Where(c => world.TryGetApeWarSide(c.Id, out var attacking) && attacking).ToArray();
        var blue = residents.Where(c => world.TryGetApeWarSide(c.Id, out var attacking) && !attacking).ToArray();
        Assert.Equal(5, red.Length);
        Assert.Equal(5, blue.Length);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var canAttack = typeof(SimulationWorld).GetMethod("CanEatInCurrentContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True((bool)canAttack.Invoke(world, [indices[red[0].Id.Value], indices[blue[0].Id.Value]])!);
        Assert.True((bool)canAttack.Invoke(world, [indices[blue[0].Id.Value], indices[red[0].Id.Value]])!);
        Assert.False((bool)canAttack.Invoke(world, [indices[red[0].Id.Value], indices[red[1].Id.Value]])!);
        if (manualStop) Assert.True(world.TryStopApeWar(First));
        else
        {
            foreach (var unit in red) world.RemoveCritterAt(unit.Position);
            world.AdvanceApeWars();
        }
        Assert.False(world.IsApeVillageAtWar(First));
        Assert.Equal(food, world.GetApeVillageFood(First));
        Assert.Equal(wood, world.GetApeVillageWood(First));
        Assert.All(Residents(world, First), c =>
        {
            Assert.Equal(roles[c.Id], c.Species);
            Assert.False(world.TryGetApeWarSide(c.Id, out _));
        });
    }

    [Fact]
    public void CivilWarTeamsActuallyFightToCompletion()
    {
        var world = CreateWorld();
        Assert.True(world.TryStartApeCivilWar(First));
        var mover = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var fought = false;
        for (var step = 0; step < 300 && world.IsApeVillageAtWar(First); step++)
        {
            foreach (var resident in Residents(world, First))
                if (indices.TryGetValue(resident.Id.Value, out var index) &&
                    mover.Invoke(world, [index, null]) is GridPosition enemy)
                {
                    fought = true;
                    world.CommitEncounter(world.GetCritter(index).Position, enemy);
                }
            world.AdvanceApeWars();
        }
        Assert.True(fought);
        Assert.False(world.IsApeVillageAtWar(First));
    }

    [Fact]
    public void CivilWarExcludesTradersAndKeepsChiefAsAnActiveCombatant()
    {
        var world = CreateWorld();
        foreach (var resident in Residents(world, First)) world.RemoveCritterAt(resident.Position);
        var chief = world.AddCritter(CritterSpecies.ApeChieftain, new(10, 10));
        var ape = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        Assert.True(world.TryAssignApeToVillage(chief, First));
        Assert.True(world.TryAssignApeToVillage(ape, First));
        var trader = world.AddCritter(CritterSpecies.ApeTrader, new(40, 20));
        world.SetTerrain(new(41, 20), Terrain.Ocean);
        var sailorTrader = world.AddCritter(CritterSpecies.ApeTraderSailor, new(41, 20));
        // Even if a trader has a village home entry, it must not be mobilized.
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        homes[trader.Value] = homes[sailorTrader.Value] = First.Y * world.Width + First.X;
        Assert.True(world.TryStartApeCivilWar(First));
        Assert.True(world.TryGetApeWarSide(chief, out var chiefRed));
        Assert.True(world.TryGetApeWarSide(ape, out var apeRed));
        Assert.NotEqual(chiefRed, apeRed);
        Assert.True(world.TryGetCritter(chief, out var chiefSnapshot));
        Assert.Equal(CritterSpecies.ApeChieftain, chiefSnapshot.Species);
        foreach (var id in new[] { trader, sailorTrader }) Assert.False(world.TryGetApeWarSide(id, out _));
        Assert.True(world.TryGetCritter(trader, out var traderSnapshot));
        Assert.Equal(CritterSpecies.ApeTrader, traderSnapshot.Species);
        Assert.True(world.TryGetCritter(sailorTrader, out var sailorSnapshot));
        Assert.Equal(CritterSpecies.ApeTraderSailor, sailorSnapshot.Species);
        world.AdvanceApeWars();
        Assert.True(world.IsApeVillageAtWar(First)); // A chief-only team is still alive.
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var mover = typeof(SimulationWorld).GetMethod("TryMoveWarApe", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(new GridPosition(11, 10), (GridPosition)mover.Invoke(world, [indices[chief.Value], null])!);
        Assert.Equal(new GridPosition(10, 10), (GridPosition)mover.Invoke(world, [indices[ape.Value], null])!);
        Assert.True(world.TryStopApeWar(First));
        Assert.True(world.TryGetCritter(chief, out chiefSnapshot));
        Assert.Equal(CritterSpecies.ApeChieftain, chiefSnapshot.Species);
    }

    [Fact]
    public void BloodWarPreservesProductionWorkersAndChiefAndRestoresRecruits()
    {
        var world = CreateWorld();
        foreach (var resident in Residents(world, Second)) world.RemoveCritterAt(resident.Position);
        var roles = new[] { CritterSpecies.Ape, CritterSpecies.ApeFarmer, CritterSpecies.ApeLumberjack,
            CritterSpecies.ApeScholar, CritterSpecies.ApeSailor, CritterSpecies.ApeChieftain, CritterSpecies.ApeWarrior };
        var ids = new List<CritterId>();
        for (var i = 0; i < roles.Length; i++)
        {
            var position = new GridPosition(30 + i, 12);
            if (roles[i] == CritterSpecies.ApeSailor) world.SetTerrain(position, Terrain.Beach);
            var id = world.AddCritter(roles[i], position);
            Assert.True(world.TryAssignApeToVillage(id, Second));
            ids.Add(id);
        }
        Assert.True(world.TryStartApeWar(First, Second, bloodWar: true));
        for (var i = 0; i < ids.Count; i++)
        {
            Assert.True(world.TryGetCritter(ids[i], out var fighter));
            var keepsRole = roles[i] is CritterSpecies.ApeChieftain or CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack;
            Assert.Equal(keepsRole ? roles[i] : CritterSpecies.ApeWarrior, fighter.Species);
            if (roles[i] is CritterSpecies.ApeFarmer or CritterSpecies.ApeLumberjack)
                Assert.Equal(2, world.GetCritterCombatDamage(ids[i]));
            Assert.True(world.TryGetApeWarSide(ids[i], out var attacking));
            Assert.False(attacking);
        }
        Assert.True(world.TryStopApeWar(Second));
        for (var i = 0; i < ids.Count; i++)
        {
            Assert.True(world.TryGetCritter(ids[i], out var survivor));
            Assert.Equal(roles[i], survivor.Species);
        }
    }

    [Fact]
    public void NaturalWarsRespectTogglePopulationGraceAndCooldownAndCanChooseEveryVariant()
    {
        var world = CreateWorld();
        var tile = First.Y * world.Width + First.X;
        var tickProperty = typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!;
        tickProperty.SetValue(world, (long)SimulationWorld.VillageWarCooldownTicks);
        for (var i = 0; i < 200; i++) Assert.False(world.TryStartVillageWar(tile));
        // Populate both villages independently of housing/economic progression.
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        for (var i = 0; i < 180; i++)
        {
            var id = world.AddCritter(CritterSpecies.Ape, new(1 + i % 45, 20 + i / 45));
            homes[id.Value] = (i < 90 ? First : Second).Y * world.Width + (i < 90 ? First : Second).X;
        }
        tickProperty.SetValue(world, 0L);
        for (var i = 0; i < 200; i++) Assert.False(world.TryStartVillageWar(tile));
        tickProperty.SetValue(world, (long)SimulationWorld.VillageWarCooldownTicks);
        NaturalEvents.SetEnabled(world, false);
        for (var i = 0; i < 200; i++) Assert.False(world.TryStartVillageWar(tile));
        NaturalEvents.SetEnabled(world, true);
        var variants = new HashSet<string>();
        for (var outbreak = 0; outbreak < 60; outbreak++)
        {
            var started = false;
            for (var attempt = 0; attempt < 5000 && !started; attempt++) started = world.TryStartVillageWar(tile);
            Assert.True(started);
            var wars = (System.Collections.IDictionary)typeof(SimulationWorld)
                .GetField("_apeWars", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
            var war = wars[tile]!;
            var type = war.GetType();
            variants.Add((bool)type.GetField("CivilWar")!.GetValue(war)! ? "civil" :
                (bool)type.GetField("BloodWar")!.GetValue(war)! ? "blood" : "normal");
            Assert.False(world.TryStartVillageWar(tile));
            Assert.True(world.TryStopApeWar(First));
            for (var attempt = 0; attempt < 20; attempt++) Assert.False(world.TryStartVillageWar(tile));
            tickProperty.SetValue(world, world.Tick + SimulationWorld.VillageWarCooldownTicks);
        }
        Assert.Equal(3, variants.Count);
    }

    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void TradersAreNotBloodWarRecruitsAndCannotKeepVillageOrWarAlive(CritterSpecies species)
    {
        var world = CreateWorld();
        var position = new GridPosition(40, 20);
        if (species == CritterSpecies.ApeTraderSailor) world.SetTerrain(position, Terrain.Ocean);
        var trader = world.AddCritter(species, position);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        homes[trader.Value] = Second.Y * world.Width + Second.X;
        Assert.True(world.TryStartApeWar(First, Second, bloodWar: true));
        Assert.True(world.TryGetCritter(trader, out var snapshot));
        Assert.Equal(species, snapshot.Species);
        foreach (var resident in Residents(world, Second).Where(c => c.Id != trader))
            world.RemoveCritterAt(resident.Position);
        world.AdvanceApeWars();
        Assert.False(world.IsApeVillageAtWar(First));
        typeof(SimulationWorld).GetMethod("AdvanceApeVillages", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(world, null);
        Assert.NotEqual(ApeStructureKind.Village, world.GetApeStructure(Second));
        Assert.True(world.TryGetCritter(trader, out snapshot));
        Assert.Equal(species, snapshot.Species);
    }
}




