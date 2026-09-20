using Newt.Simulation;
using System.Reflection;

namespace Newt.Simulation.Tests;

public sealed class ApeTraderTests
{
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
    private static CritterSnapshot Move(SimulationWorld world, CritterSnapshot trader)
    {
        world.MoveApeTrader(trader.Id);
        Assert.True(world.TryGetCritter(trader.Id, out trader));
        return trader;
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
    public void TradersCanShoveEverySpecies(CritterSpecies trader)
    {
        foreach (var blocker in Enum.GetValues<CritterSpecies>())
            Assert.True(SimulationWorld.CanDisplace(trader, blocker));
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

    [Fact]
    public void SameTraderBoardsAtHarborAndDisembarksWithoutLosingProvisionsOrOwnership()
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
            Assert.Equal(initialEnergy, trader.Energy);
            Assert.Equal(market, world.GetApeTraderMarket(trader.Id));
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
        Assert.Contains(First, visited);
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
    [InlineData(false)]
    [InlineData(true)]
    public void TradersRefillFromVisitedVillageOnly(bool sea)
    {
        var world = CreateWorld(sea);
        if (!sea)
            Assert.True(world.TryPlaceVillageRoad(First));
        var trader = Recruit(world, AddMarket(world, First));
        var initial = trader.Energy;
        // Move beyond the origin's center and harbor before giving it more food.
        for (var i = 0; i < 5; i++)
            trader = Move(world, trader);
        world.StoreApeVillageFood(First, 5);
        world.StoreApeVillageFood(Second, 5);
        trader = Arrive(world, trader, Second);
        Assert.Equal(initial + 5, trader.Energy);
        Assert.Equal(0, world.GetApeVillageFood(Second));
        Assert.Equal(5, world.GetApeVillageFood(First));
        trader = Arrive(world, trader, First);
        Assert.Equal(initial + 10, trader.Energy);
        Assert.Equal(0, world.GetApeVillageFood(First));
        for (var i = 0; i < 12; i++)
        {
            world.StoreApeVillageFood(Second, 5);
            trader = Arrive(world, trader, Second);
            trader = Arrive(world, trader, First);
        }
        Assert.Equal(48, trader.Energy);
        Assert.True(world.GetApeVillageFood(Second) > 0);
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
