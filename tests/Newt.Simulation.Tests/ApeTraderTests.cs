using Newt.Simulation;
using System.Reflection;

namespace Newt.Simulation.Tests;

public sealed class ApeTraderTests
{
    [Theory]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.Wolf, 1, 12)]
    [InlineData(CritterSpecies.ApeTraderSailor, CritterSpecies.ToothedWhale, 10, 12)]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.Vampire, 1, 12)]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.Wolf, 10, 1)]
    [InlineData(CritterSpecies.ApeTrader, CritterSpecies.Wolf, 100, 12)]
    public void DamagingTraderStealsActualDamageAsEnergy(
        CritterSpecies traderSpecies, CritterSpecies robberSpecies, int robberEnergy, int traderEnergy)
    {
        var world = new SimulationWorld(5, 5, Terrain.Plains, seed: 17);
        var robberPosition = new GridPosition(1, 1);
        var traderPosition = new GridPosition(2, 1);
        if (robberSpecies is CritterSpecies.ToothedWhale)
            world.SetTerrain(robberPosition, Terrain.Ocean);
        if (traderSpecies is CritterSpecies.ApeTraderSailor)
            world.SetTerrain(traderPosition, Terrain.Ocean);
        world.AddCritter(robberSpecies, robberPosition);
        world.AddCritter(traderSpecies, traderPosition);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(world)!;
        var maximum = CritterNutritions.Get(robberSpecies).MaximumEnergy;
        energies[0] = Math.Min(robberEnergy, maximum);
        energies[1] = traderEnergy;
        var before = energies[0];

        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, new object[] { 0, 1 });

        var damage = traderEnergy - energies[1];
        Assert.True(damage > 0);
        Assert.Equal(Math.Min(maximum, before + damage), energies[0]);
    }

    private static readonly GridPosition First = new(10, 10);
    private static readonly GridPosition Second = new(30, 10);

    private static SimulationWorld CreateWorld(bool sea = false)
    {
        var world = new SimulationWorld(60, 50, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(First));
        Assert.True(world.TrySpawnTestApeVillage(Second));
        if (sea)
        {
            AddHarbor(world, First);
            AddHarbor(world, Second);
            for (var x = First.X; x <= Second.X; x++)
                world.SetTerrain(new(x, 8), Terrain.Ocean);
        }
        return world;
    }

    private static void AddHarbor(SimulationWorld world, GridPosition village)
    {
        var harbor = new GridPosition(village.X, village.Y - 1);
        world.RemoveCritterAt(harbor);
        world.RemoveApeStructureAt(harbor);
        world.SetTerrain(harbor, Terrain.Shallows);
        Assert.True(world.TryPlaceApeBuilding(harbor, ApeStructureKind.NavalDistrict));
    }

    private static GridPosition AddMarket(SimulationWorld world, GridPosition village)
    {
        var market = new GridPosition(village.X - 1, village.Y);
        world.RemoveCritterAt(market);
        world.RemoveApeStructureAt(market);
        Assert.True(world.TryPlaceApeBuilding(market, ApeStructureKind.Market));
        return market;
    }

    private static CritterSnapshot Recruit(SimulationWorld world, GridPosition market)
    {
        for (var i = world.CritterCount - 1; i >= 0; i--)
            world.RemoveCritterAt(world.GetCritter(i).Position);
        var village = world.GetApeStructureVillage(market)!.Value;
        world.StoreApeVillageFood(village, SimulationWorld.ApeTraderRecruitmentFoodCost);
        Assert.True(world.TryRecruitApeMarketTrader(market.Y * world.Width + market.X));
        return Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Single(c => c.Species is CritterSpecies.ApeTrader or CritterSpecies.ApeTraderSailor);
    }

    [Fact]
    public void StalledTraderRecoversAfterThirtySecondsWithoutChangingIdentityOrProvisions()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Recruit(world, AddMarket(world, First));
        trader = Arrive(world, trader, First);
        var energy = trader.Energy;
        var market = world.GetApeTraderMarket(trader.Id);
        var destination = world.GetApeTraderDestination(trader.Id);
        world.AddCritter(CritterSpecies.ApeWarrior, new(11, 10));
        SetTick(world, SimulationWorld.ApeTraderStallTicks - 1);
        trader = Move(world, trader);
        Assert.Equal(First, trader.Position);
        SetTick(world, SimulationWorld.ApeTraderStallTicks);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(12, 10), trader.Position);
        Assert.Equal(energy, trader.Energy);
        Assert.Equal(market, world.GetApeTraderMarket(trader.Id));
        Assert.Equal(destination, world.GetApeTraderDestination(trader.Id));
        Assert.Equal(First, world.GetApeHomeVillage(trader.Id));
        Assert.Equal(1, world.GetApeTraderRecoveryCount(trader.Id));
        Assert.Equal(1, world.ApeTraderRecoveryCount);
    }

    [Fact]
    public void FullyBlockedRecoveryWaitsTenSecondsBeforeRetrying()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Arrive(world, Recruit(world, AddMarket(world, First)), First);
        for (var x = 11; x <= 15; x++) world.AddCritter(CritterSpecies.ApeWarrior, new(x, 10));
        SetTick(world, SimulationWorld.ApeTraderStallTicks);
        trader = Move(world, trader);
        Assert.Equal(First, trader.Position);
        Assert.Equal(0, world.ApeTraderRecoveryCount);
        world.RemoveCritterAt(new(12, 10));
        SetTick(world, SimulationWorld.ApeTraderStallTicks + SimulationWorld.ApeTraderRecoveryRetryTicks - 1);
        trader = Move(world, trader);
        Assert.Equal(First, trader.Position);
        SetTick(world, SimulationWorld.ApeTraderStallTicks + SimulationWorld.ApeTraderRecoveryRetryTicks);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(12, 10), trader.Position);
    }

    [Fact]
    public void RejoiningSameRouteStepDoesNotResetStallTimer()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Arrive(world, Recruit(world, AddMarket(world, First)), First);
        world.AddCritter(CritterSpecies.ApeWarrior, new(11, 10));
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        for (var time = 100; time < SimulationWorld.ApeTraderStallTicks; time += 100)
        {
            SetTick(world, time);
            typeof(SimulationWorld).GetMethod("MoveCritter", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(world, [indices[trader.Id.Value], 11 * world.Width + 10, new GridPosition(10, 11), false]);
            trader = Move(world, trader);
            Assert.Equal(First, trader.Position);
        }
        SetTick(world, SimulationWorld.ApeTraderStallTicks);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(12, 10), trader.Position);
        Assert.Equal(1, world.ApeTraderRecoveryCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StalledTraderBypassesOccupiedHarborAndChangesTravelForm(bool landing)
    {
        var world = CreateWorld(sea: true);
        var trader = Recruit(world, AddMarket(world, First));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var journeys = (System.Collections.IDictionary)typeof(SimulationWorld).GetField("_traderJourneys", flags)!.GetValue(world)!;
        var journey = journeys[trader.Id.Value]!;
        var tiles = (List<int>)journey.GetType().GetField("Tiles")!.GetValue(journey)!;
        var harbor = new GridPosition(landing ? Second.X : First.X, 9);
        var harborStep = tiles.IndexOf(harbor.Y * world.Width + harbor.X);
        Assert.True(harborStep > 0 && harborStep + 1 < tiles.Count);
        for (var step = 0; step < harborStep - 1; step++) trader = Move(world, trader);
        var origin = trader.Position;
        var energy = trader.Energy;
        var market = world.GetApeTraderMarket(trader.Id);
        var destination = world.GetApeTraderDestination(trader.Id);
        var blocker = world.AddCritter(CritterSpecies.ApeWarrior, harbor);
        SetTick(world, SimulationWorld.ApeTraderStallTicks - 1);
        trader = Move(world, trader);
        Assert.Equal(origin, trader.Position);
        Assert.Equal(0, world.ApeTraderRecoveryCount);
        SetTick(world, SimulationWorld.ApeTraderStallTicks);
        trader = Move(world, trader);
        var expected = tiles[harborStep + 1];
        Assert.Equal(new GridPosition(expected % world.Width, expected / world.Width), trader.Position);
        Assert.Equal(1, world.ApeTraderRecoveryCount);
        Assert.True(world.TryGetCritter(trader.Id, out var after));
        Assert.Equal(landing ? CritterSpecies.ApeTrader : CritterSpecies.ApeTraderSailor, after.Species);
        Assert.Equal(1, world.GetCritterCount(after.Species));
        Assert.Equal(0, world.GetCritterCount(landing ? CritterSpecies.ApeTraderSailor : CritterSpecies.ApeTrader));
        Assert.Equal(energy, after.Energy);
        Assert.Equal(market, world.GetApeTraderMarket(trader.Id));
        Assert.Equal(destination, world.GetApeTraderDestination(trader.Id));
        Assert.Equal(First, world.GetApeHomeVillage(trader.Id));
        Assert.True(world.TryGetCritter(blocker, out var untouched));
        Assert.Equal(harbor, untouched.Position);
    }

    private static void SetTick(SimulationWorld world, long tick) =>
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!.SetValue(world, tick);

    [Fact]
    public void RealForwardProgressRestartsTheThirtySecondTimer()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Arrive(world, Recruit(world, AddMarket(world, First)), First);
        SetTick(world, 500);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(11, 10), trader.Position);
        world.AddCritter(CritterSpecies.ApeWarrior, new(12, 10));
        SetTick(world, 500 + SimulationWorld.ApeTraderStallTicks - 1);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(11, 10), trader.Position);
        SetTick(world, 500 + SimulationWorld.ApeTraderStallTicks);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(13, 10), trader.Position);
    }

    [Fact]
    public void SeaRecoverySkipsReservedTilesAndPreservesSailingForm()
    {
        var world = CreateWorld(sea: true);
        var trader = Recruit(world, AddMarket(world, First));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var journeys = (System.Collections.IDictionary)typeof(SimulationWorld).GetField("_traderJourneys", flags)!.GetValue(world)!;
        var journey = journeys[trader.Id.Value]!;
        var type = journey.GetType();
        var tiles = (List<int>)type.GetField("Tiles")!.GetValue(journey)!;
        var seaStep = tiles.FindIndex(tile => world.GetTerrain(new(tile % world.Width, tile / world.Width)) == Terrain.Ocean);
        Assert.True(seaStep >= 0);
        for (var i = 0; i < seaStep; i++) trader = Move(world, trader);
        Assert.Equal(CritterSpecies.ApeTraderSailor, trader.Species);
        var energy = trader.Energy;
        var next = tiles[seaStep + 1];
        var reserved = new HashSet<GridPosition> { new(next % world.Width, next / world.Width) };
        SetTick(world, SimulationWorld.ApeTraderStallTicks);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_critterIndicesById", flags)!.GetValue(world)!;
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryRecoverStalledTrader", flags)!
            .Invoke(world, [indices[trader.Id.Value], journey, reserved])!);
        Assert.True(world.TryGetCritter(trader.Id, out var after));
        Assert.Equal(CritterSpecies.ApeTraderSailor, after.Species);
        Assert.Equal(energy, after.Energy);
        var target = tiles[seaStep + 2];
        Assert.Equal(new GridPosition(target % world.Width, target / world.Width), after.Position);
    }

    [Fact]
    public void EligibleVillageBuildsMarketAutomaticallyWhenItCanAffordIt()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var wood = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageWood", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        wood[First.Y * world.Width + First.X] = 6;
        world.StoreApeVillageFood(First, 5);
        world.AdvanceApeTraders();
        Assert.False(world.CanBuildApeMarket(First));
        var markets = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is ApeStructureKind.Market).ToArray();
        var market = Assert.Single(markets);
        Assert.Equal(First, world.GetApeStructureVillage(market));
        Assert.Equal(0, world.GetApeVillageFood(First));
        Assert.Equal(0, world.GetApeVillageWood(First));
        Assert.Equal(0, world.GetApeMarketTraderCount(market));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TradersSurviveShovingEachOtherAndResumeTheirJourneys(bool sea)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        var firstMarket = AddMarket(world, First);
        var secondMarket = AddMarket(world, Second);
        var first = Recruit(world, firstMarket);
        world.StoreApeVillageFood(Second, 3);
        Assert.True(world.TryRecruitApeMarketTrader(secondMarket.Y * world.Width + secondMarket.X));
        var second = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Single(critter => critter.Id != first.Id);
        var y = sea ? 8 : 10;
        first = Arrive(world, first, new(19, y));
        second = Arrive(world, second, new(20, y));
        var firstEnergy = first.Energy;
        var secondEnergy = second.Energy;
        first = Move(world, first);
        Assert.Equal(new GridPosition(20, y), first.Position);
        Assert.True(world.TryGetCritter(second.Id, out second));
        Assert.NotEqual(new GridPosition(20, y), second.Position);
        world.AdvanceApeTraders();
        Assert.True(world.TryGetCritter(first.Id, out first));
        Assert.True(world.TryGetCritter(second.Id, out second));
        Assert.Equal(firstEnergy, first.Energy);
        Assert.Equal(secondEnergy, second.Energy);
        var firstArrived = false;
        var secondArrived = false;
        for (var i = 0; i < 100; i++)
        {
            first = Move(world, first);
            second = Move(world, second);
            world.AdvanceApeTraders();
            Assert.True(world.TryGetCritter(first.Id, out first));
            Assert.True(world.TryGetCritter(second.Id, out second));
            firstArrived |= first.Position == secondMarket;
            secondArrived |= second.Position == firstMarket;
            Assert.Equal(firstMarket, world.GetApeTraderMarket(first.Id));
            Assert.Equal(secondMarket, world.GetApeTraderMarket(second.Id));
            foreach (var critter in new[] { first, second })
            {
                Assert.True(world.TryGetCritterAt(critter.Position, out var occupant));
                Assert.Equal(critter.Id, occupant.Id);
            }
        }
        Assert.True(firstArrived);
        Assert.True(secondArrived);
        Assert.Equal(1, world.GetApeMarketTraderCount(firstMarket));
        Assert.Equal(1, world.GetApeMarketTraderCount(secondMarket));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShovedTraderRejoinsRouteWithoutBeingDeleted(bool sea)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        var firstMarket = AddMarket(world, First);
        var secondMarket = AddMarket(world, Second);
        var first = Recruit(world, firstMarket);
        world.StoreApeVillageFood(Second, 3);
        Assert.True(world.TryRecruitApeMarketTrader(secondMarket.Y * world.Width + secondMarket.X));
        var second = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Single(critter => critter.Id != first.Id);
        second = Arrive(world, second, firstMarket);
        var y = sea ? 8 : 10;
        second = Arrive(world, second, new(20, y));
        first = Arrive(world, first, new(19, y));
        var energy = second.Energy;
        first = Move(world, first);
        Assert.Equal(new GridPosition(20, y), first.Position);
        Assert.True(world.TryGetCritter(second.Id, out second));
        Assert.NotEqual(new GridPosition(20, y), second.Position);
        Assert.NotEqual(new GridPosition(19, y), second.Position); // Shove, not an exchange.
        world.AdvanceApeTraders();
        Assert.True(world.TryGetCritter(second.Id, out second));
        Assert.Equal(energy, second.Energy);
        var arrived = false;
        for (var i = 0; i < 100 && !arrived; i++)
        {
            first = Move(world, first);
            Assert.True(world.TryGetCritter(second.Id, out second));
            arrived |= second.Position == secondMarket;
            second = Move(world, second);
            arrived |= second.Position == secondMarket;
            world.AdvanceApeTraders();
            Assert.True(world.TryGetCritter(first.Id, out first));
            Assert.True(world.TryGetCritter(second.Id, out second));
        }
        Assert.True(arrived);
        Assert.Equal(secondMarket, world.GetApeTraderMarket(second.Id));
    }
    private static CritterSnapshot Move(SimulationWorld world, CritterSnapshot trader)
    {
        world.MoveApeTrader(trader.Id);
        Assert.True(world.TryGetCritter(trader.Id, out trader));
        return trader;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TraderDisplacedSeveralTilesRecoversAndKeepsTrading(bool sea)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        else
            for (var y = 4; y <= 8; y++)
                for (var x = 16; x <= 24; x++)
                    world.SetTerrain(new(x, y), Terrain.Ocean);
        var market = AddMarket(world, First);
        var destinationMarket = AddMarket(world, Second);
        var trader = Recruit(world, market);
        trader = Arrive(world, trader, new(20, sea ? 8 : 10));
        var displaced = new GridPosition(20, sea ? 4 : 14);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        typeof(SimulationWorld).GetMethod("MoveCritter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [indices[trader.Id.Value], displaced.Y * world.Width + displaced.X, displaced, false]);
        Assert.True(world.TryGetCritter(trader.Id, out trader));
        for (var trip = 0; trip < 100; trip++)
        {
            trader = Arrive(world, trader, destinationMarket);
            trader = Arrive(world, trader, market);
        }
        Assert.Equal(market, trader.Position);
    }

    private static CritterSnapshot Arrive(SimulationWorld world, CritterSnapshot trader, GridPosition target)
    {
        for (var i = 0; i < 200 && trader.Position != target; i++)
            trader = Move(world, trader);
        Assert.Equal(target, trader.Position);
        return trader;
    }

    [Theory]
    [InlineData(CritterSpecies.ApeTrader)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    public void TradersCanShoveEverySpeciesExceptMilitaryApes(CritterSpecies trader)
    {
        foreach (var blocker in Enum.GetValues<CritterSpecies>())
            Assert.Equal(blocker is not (CritterSpecies.ApeWarrior or CritterSpecies.ApeBomber or CritterSpecies.ApeChieftain or CritterSpecies.ApeKing or CritterSpecies.ApeQueen or CritterSpecies.ApeRoyalGuard),
                SimulationWorld.CanDisplace(trader, blocker));
    }

    [Fact]
    public void TraderPassesCivilianEvenWhenAllSidestepsAreOccupied()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Recruit(world, AddMarket(world, First));
        trader = Arrive(world, trader, First);
        for (var y = 9; y <= 11; y++)
        for (var x = 10; x <= 12; x++)
            if (new GridPosition(x, y) != First)
                world.AddCritter(CritterSpecies.ApeFarmer, new(x, y));
        var count = world.CritterCount;
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(11, 10), trader.Position);
        Assert.Equal(count, world.CritterCount);
        Assert.Equal(count, Enumerable.Range(0, count).Select(i => world.GetCritter(i).Position).Distinct().Count());
        Assert.Contains(Enumerable.Range(0, count).Select(world.GetCritter),
            c => c.Species == CritterSpecies.ApeFarmer && c.Position == First);
        trader = Move(world, trader);
        Assert.Equal(new GridPosition(12, 10), trader.Position);
    }

    [Fact]
    public void MarketTraderShovesFarmersAndFollowsRoad()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var market = AddMarket(world, First);
        var trader = Recruit(world, market);
        trader = Arrive(world, trader, First);
        var farmers = Enumerable.Range(11, 5)
            .Select(x => world.AddCritter(CritterSpecies.ApeFarmer, new(x, 10))).ToArray();
        for (var x = 11; x <= 15; x++)
        {
            trader = Move(world, trader);
            Assert.Equal(new GridPosition(x, 10), trader.Position);
        }
        foreach (var id in farmers.Append(trader.Id))
        {
            Assert.True(world.TryGetCritter(id, out var critter));
            Assert.True(world.TryGetCritterAt(critter.Position, out var occupant));
            Assert.Equal(id, occupant.Id);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarketRequiresReachableOtherVillageAndIsLimitedToOne(bool sea)
    {
        var world = CreateWorld(sea);
        Assert.Equal(sea, world.CanBuildApeMarket(First));
        if (!sea)
        {
            Assert.False(world.TryBuildApeStructure(610, ApeStructureKind.Market));
            Assert.True(world.TryPlaceVillageRoad(First));
        }
        Assert.True(world.CanBuildApeMarket(First));
        AddMarket(world, First);
        Assert.False(world.CanBuildApeMarket(First));
        Assert.False(world.TryBuildApeStructure(610, ApeStructureKind.Market));
        Assert.False(world.TryPlaceApeBuilding(new(10, 11), ApeStructureKind.Market));
    }

    [Fact]
    public void SingleHarborOrDisconnectedWaterDoesNotUnlockMarket()
    {
        var world = CreateWorld();
        AddHarbor(world, First);
        Assert.False(world.CanBuildApeMarket(First));
        AddHarbor(world, Second);
        Assert.False(world.CanBuildApeMarket(First));
        world.AdvanceApeTraders();
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.ApeTraderSailor));
    }

    [Fact]
    public void RoadsAloneDoNotCreateTradersAndOneMarketRecruitsOnlyOne()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        world.AdvanceApeTraders();
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.ApeTrader));
        var market = AddMarket(world, First);
        var trader = Recruit(world, market);
        trader = Move(world, trader);
        world.StoreApeVillageFood(First, 5);
        Assert.False(world.TryRecruitApeMarketTrader(market.Y * world.Width + market.X));
        Assert.Equal(1, world.GetApeMarketTraderCount(market));
        Assert.Equal(market, world.GetApeTraderMarket(trader.Id));
    }

    [Fact]
    public void MarketConstructionChargesNormalCostsAndFailedPurchaseChargesNothing()
    {
        var world = CreateWorld();
        var wood = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageWood", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var tile = First.Y * world.Width + First.X;
        wood[tile] = 6;
        world.StoreApeVillageFood(First, 5);
        Assert.False(world.TryPurchaseApeStructure(tile, ApeStructureKind.Market));
        Assert.Equal(6, world.GetApeVillageWood(First));
        Assert.Equal(5, world.GetApeVillageFood(First));
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.True(world.TryPurchaseApeStructure(tile, ApeStructureKind.Market));
        Assert.Equal(0, world.GetApeVillageWood(First));
        Assert.Equal(0, world.GetApeVillageFood(First));
    }

    [Fact]
    public void RecruitmentRequiresFoodAndWaitsForInterval()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var market = AddMarket(world, First);
        var tile = market.Y * world.Width + market.X;
        Assert.False(world.TryRecruitApeMarketTrader(tile));
        world.StoreApeVillageFood(First, 3);
        world.AdvanceApeTraders();
        Assert.Equal(0, world.GetApeMarketTraderCount(market));
        // Keep residents so the village remains alive while its recruitment clock runs.
        for (var tick = 0; tick <= SimulationWorld.ApeMarketRecruitmentIntervalTicks; tick++)
        {
            world.RemoveCritterAt(market);
            world.StoreApeVillageFood(First, 3);
            world.AdvanceOneTick();
        }
        Assert.Equal(1, world.GetApeMarketTraderCount(market));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TradersVisitDestinationMarketAndReturnFromTheirActualStop(bool sea, bool destinationHasMarket)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        var homeMarket = AddMarket(world, First);
        var stop = destinationHasMarket ? AddMarket(world, Second) : Second;
        var trader = Recruit(world, homeMarket);
        Assert.Equal(Second, world.GetApeTraderDestination(trader.Id));
        trader = Arrive(world, trader, stop);
        world.AdvanceApeTraders();
        Assert.True(world.TryGetCritter(trader.Id, out trader));
        trader = Move(world, trader);
        Assert.Equal(First, world.GetApeTraderDestination(trader.Id));
        trader = Arrive(world, trader, homeMarket);
        Assert.Equal(homeMarket, trader.Position);
    }

    [Fact]
    public void SeaTradersCrossOpenWaterDirectly()
    {
        var world = CreateWorld(sea: true);
        for (var y = 2; y <= 8; y++)
            for (var x = First.X; x <= Second.X; x++)
                world.SetTerrain(new(x, y), Terrain.Ocean);
        var trader = Recruit(world, AddMarket(world, First));
        for (var i = 0; i < 50 && trader.Position != Second; i++)
        {
            trader = Move(world, trader);
            Assert.True(trader.Position.Y >= 8, "Open-water crossing wandered away from the harbors.");
        }
        Assert.Equal(Second, trader.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeaTradersAvoidDeepOceanWhenAShortDetourExists(bool detour)
    {
        var world = CreateWorld(sea: true);
        for (var x = 17; x <= 23; x++)
            world.SetTerrain(new(x, 8), Terrain.DeepOcean);
        if (detour)
            for (var x = First.X; x <= Second.X; x++)
                world.SetTerrain(new(x, 7), Terrain.Ocean);
        var trader = Recruit(world, AddMarket(world, First));
        var crossedDeepOcean = false;
        for (var i = 0; i < 50 && trader.Position != Second; i++)
        {
            trader = Move(world, trader);
            crossedDeepOcean |= world.GetTerrain(trader.Position) is Terrain.DeepOcean;
        }
        Assert.Equal(Second, trader.Position);
        Assert.Equal(!detour, crossedDeepOcean);
    }

    [Fact]
    public void SameTraderBoardsAndDisembarksPreservingOwnershipAndTradingOnlyAtVillage()
    {
        var world = CreateWorld(sea: true);
        var market = AddMarket(world, First);
        var trader = Recruit(world, market);
        var initialEnergy = trader.Energy;
        var forms = new HashSet<CritterSpecies>();
        for (var i = 0; i < 100 && trader.Position != Second; i++)
        {
            trader = Move(world, trader);
            forms.Add(trader.Species);
            Assert.Equal(world.GetApeStructureVillage(trader.Position) == Second
                ? initialEnergy - initialEnergy / 2 : initialEnergy, trader.Energy);
            Assert.Equal(market, world.GetApeTraderMarket(trader.Id));
            Assert.Equal(First, world.GetApeHomeVillage(trader.Id));
            Assert.Equal(1, world.GetCritterCount(CritterSpecies.ApeTrader) + world.GetCritterCount(CritterSpecies.ApeTraderSailor));
        }
        Assert.Equal(Second, trader.Position);
        Assert.Equal(CritterSpecies.ApeTrader, trader.Species);
        Assert.Contains(CritterSpecies.ApeTraderSailor, forms);
        Assert.Contains(CritterSpecies.ApeTrader, forms);
    }

    [Fact]
    public void TraderCanContinueFromSeaPortToInlandRoadVillage()
    {
        var world = CreateWorld(sea: true);
        var third = new GridPosition(30, 35);
        Assert.True(world.TrySpawnTestApeVillage(third));
        Assert.True(world.TryPlaceVillageRoad(third));
        var trader = Recruit(world, AddMarket(world, First));
        var visited = new HashSet<GridPosition>();
        for (var i = 0; i < 300; i++)
        {
            trader = Move(world, trader);
            visited.Add(trader.Position);
        }
        Assert.Contains(world.GetApeTraderMarket(trader.Id)!.Value, visited);
        Assert.Contains(Second, visited);
        Assert.Contains(third, visited);
    }

    [Fact]
    public void DestinationSelectionAvoidsImmediateReturnWhenAnotherVillageIsReachable()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var third = new GridPosition(30, 35);
        Assert.True(world.TrySpawnTestApeVillage(third));
        Assert.True(world.TryPlaceVillageRoad(third));
        var trader = Recruit(world, AddMarket(world, First));
        var destination = world.GetApeTraderDestination(trader.Id)!.Value;
        trader = Arrive(world, trader, destination);
        trader = Move(world, trader);
        Assert.NotEqual(First, world.GetApeTraderDestination(trader.Id));
        Assert.NotEqual(destination, world.GetApeTraderDestination(trader.Id));
    }

    [Theory]
    [InlineData(false, 0, 0, 4, 10, 4, 10)]
    [InlineData(true, 0, 0, 4, 10, 4, 10)]
    [InlineData(false, 100, 50, 80, 30, 28, 40)]
    [InlineData(true, 100, 50, 80, 30, 28, 40)]
    [InlineData(false, 0, 50, 4, 30, 4, 40)]
    [InlineData(false, 100, 0, 80, 10, 28, 10)]
    public void TradersExchangeFoodAndWoodOncePerVisitedVillage(bool sea, int food, int wood,
        int expectedEnergy, int expectedCargo, int expectedFood, int expectedWood)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        var market = AddMarket(world, First);
        var woods = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageWood", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        woods[First.Y * world.Width + First.X] = 20;
        var trader = Recruit(world, market);
        Assert.Equal(20, world.GetApeTraderWood(trader.Id));
        Assert.Equal(0, world.GetApeVillageWood(First));
        for (var i = 0; i < 5; i++)
            trader = Move(world, trader);
        var indices = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_critterIndicesById", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var energies = (int[])typeof(SimulationWorld)
            .GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        energies[indices[trader.Id.Value]] = 8;
        var foods = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageFood", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        foods[Second.Y * world.Width + Second.X] = food;
        woods[Second.Y * world.Width + Second.X] = wood;
        trader = Arrive(world, trader, Second);
        Assert.Equal(expectedEnergy, trader.Energy);
        Assert.Equal(expectedCargo, world.GetApeTraderWood(trader.Id));
        Assert.Equal(expectedFood, world.GetApeVillageFood(Second));
        Assert.Equal(expectedWood, world.GetApeVillageWood(Second));
        var refill = typeof(SimulationWorld).GetMethod("RefillApeTraderProvisions",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var tick = 0; tick < 10; tick++)
            refill.Invoke(world, new object[] { indices[trader.Id.Value], false });
        Assert.Equal(expectedFood, world.GetApeVillageFood(Second));
        Assert.Equal(expectedWood, world.GetApeVillageWood(Second));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovedMarketOrHarborRetiresItsTrader(bool harbor)
    {
        var world = CreateWorld(sea: true);
        var market = AddMarket(world, First);
        var trader = Recruit(world, market);
        world.RemoveApeStructureAt(harbor ? new(30, 9) : market);
        world.AdvanceApeTraders();
        Assert.False(world.TryGetCritter(trader.Id, out _));
        Assert.Equal(0, world.GetApeMarketTraderCount(market));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeTrader, Terrain.Plains)]
    [InlineData(CritterSpecies.ApeTraderSailor, Terrain.Ocean)]
    public void TraderProvisionsAreConsumedOverTime(CritterSpecies species, Terrain terrain)
    {
        var world = new SimulationWorld(10, 10, terrain, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var id = world.AddCritter(species, new(5, 5));
        var nutrition = CritterNutritions.Get(species);
        for (var i = 0; i < nutrition.MetabolismIntervalTicks; i++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var trader));
        Assert.Equal(nutrition.InitialEnergy - 1, trader.Energy);
        Assert.False(trader.CanReproduce);
    }
}
