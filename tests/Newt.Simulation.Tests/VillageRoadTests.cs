using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillageRoadTests
{
    [Fact]
    public void ManualRoadConnectsChosenVillagesRatherThanCloserUnselectedVillage()
    {
        var world = CreateWorld(2, 2);
        var closerVillage = new GridPosition(4, 3);
        Assert.True(world.TrySpawnTestApeVillage(closerVillage));
        Assert.True(world.TryConnectVillageRoad(First, Second, out _));
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(First));
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(Second));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(closerVillage));
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManualRoadReusesNetworkConnectedToEitherSelectedVillage(bool reverse)
    {
        var world = CreateWorld(2, 2);
        var third = new GridPosition(15, 25);
        world.RemoveCritterAt(third);
        Assert.True(world.TrySpawnTestApeVillage(third));
        Assert.True(world.TryConnectVillageRoad(Second, third, out _));
        var existing = Positions(world).Where(tile => world.GetRoadConnections(tile) != RiverConnection.None)
            .ToDictionary(tile => tile, world.GetRoadConnections);
        Assert.True(world.TryConnectVillageRoad(reverse ? Second : First, reverse ? First : Second, out _));
        var added = Positions(world).Where(tile => world.GetRoadConnections(tile) != RiverConnection.None &&
            !existing.ContainsKey(tile)).ToArray();
        // Joining the nearby network is shorter than laying twenty tiles to Second.
        Assert.InRange(added.Length, 1, 18);
        Assert.Contains(added, tile => tile.Y > First.Y);
        Assert.All(existing, pair => Assert.Equal(pair.Value, world.GetRoadConnections(pair.Key) & pair.Value));
        Assert.Equal(2, world.VillageRoadCount);
        var tilesBefore = Positions(world).ToDictionary(tile => tile, world.GetRoadConnections);
        Assert.False(world.TryConnectVillageRoad(First, Second, out var status));
        Assert.Contains("already", status);
        Assert.All(tilesBefore, pair => Assert.Equal(pair.Value, world.GetRoadConnections(pair.Key)));
    }

    [Fact]
    public void ManualRoadAcceptsDistrictClicksAndRejectsSameVillageWithoutBuilding()
    {
        var world = CreateWorld(2, 2);
        Assert.False(world.TryConnectVillageRoad(First, new(10, 11), out _));
        Assert.Equal(0, world.VillageRoadCount);
        Assert.True(world.TryConnectVillageRoad(new(10, 11), new(30, 11), out _));
        Assert.Equal(1, world.VillageRoadCount);
        Assert.False(world.TryConnectVillageRoad(new(-1, 10), Second, out _));
    }

    [Fact]
    public void ManualRoadJoinsTwoExistingNetworks()
    {
        var world = CreateWorld(2, 2);
        var third = new GridPosition(15, 25);
        var fourth = new GridPosition(45, 25);
        Assert.True(world.TrySpawnTestApeVillage(third));
        Assert.True(world.TrySpawnTestApeVillage(fourth));
        Assert.True(world.TryConnectVillageRoad(First, Second, out _));
        Assert.True(world.TryConnectVillageRoad(third, fourth, out _));
        Assert.Equal(2, world.VillageRoadCount);
        Assert.True(world.TryConnectVillageRoad(First, fourth, out _));
        Assert.Equal(3, world.VillageRoadCount);
        Assert.False(world.TryConnectVillageRoad(Second, third, out var status));
        Assert.Contains("already", status);
    }

    [Fact]
    public void ManualRoadCanBridgeTwoShallowsTilesAndFailureLeavesRoadsUnchanged()
    {
        var world = CreateWorld(2, 2);
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new(20, y), Terrain.Shallows);
            world.SetTerrain(new(21, y), Terrain.Shallows);
            world.SetTerrain(new(60, y), Terrain.Ocean);
        }
        Assert.True(world.TryConnectVillageRoad(First, Second, out _));
        Assert.Equal(2, Positions(world).Count(tile => world.GetTerrain(tile) is Terrain.Shallows &&
            world.GetRoadConnections(tile) != RiverConnection.None));

        var isolated = CreateWorld(2, 2);
        for (var y = 0; y < isolated.Height; y++)
        {
            isolated.SetTerrain(new(20, y), Terrain.Ocean);
            isolated.SetTerrain(new(60, y), Terrain.Ocean);
        }
        Assert.False(isolated.TryConnectVillageRoad(First, Second, out _));
        Assert.Equal(0, isolated.VillageRoadCount);
        Assert.All(Positions(isolated), tile => Assert.Equal(RiverConnection.None, isolated.GetRoadConnections(tile)));
    }

    [Fact]
    public void RoadCostsRankBiomesAndAddEarthworksToBiomeDifficulty()
    {
        var ordered = new[] { Biome.Grassland, Biome.Arid, Biome.Desert, Biome.Forest,
            Biome.Tundra, Biome.Taiga, Biome.Arctic, Biome.Jungle, Biome.Bog };
        for (var index = 1; index < ordered.Length; index++)
            Assert.True(SimulationWorld.GetRoadConstructionCost(Terrain.Plains, ordered[index]) >
                SimulationWorld.GetRoadConstructionCost(Terrain.Plains, ordered[index - 1]));
        Assert.Equal(SimulationWorld.GetRoadConstructionCost(Terrain.Plains, Biome.Bog),
            SimulationWorld.GetRoadConstructionCost(Terrain.Plains, Biome.Swamp));
        foreach (var biome in Enum.GetValues<Biome>())
        {
            var plains = SimulationWorld.GetRoadConstructionCost(Terrain.Plains, biome);
            Assert.Equal(plains + 10, SimulationWorld.GetRoadConstructionCost(Terrain.Hills, biome));
            Assert.Equal(plains + 20, SimulationWorld.GetRoadConstructionCost(Terrain.Canyon, biome));
            Assert.Equal(plains + 25, SimulationWorld.GetRoadConstructionCost(Terrain.Trench, biome));
        }
    }

    [Theory]
    [InlineData(Terrain.Plains, Biome.Arctic)]
    [InlineData(Terrain.Plains, Biome.Forest)]
    [InlineData(Terrain.Plains, Biome.Taiga)]
    [InlineData(Terrain.Plains, Biome.Swamp)]
    [InlineData(Terrain.Hills, Biome.Grassland)]
    [InlineData(Terrain.Canyon, Biome.Grassland)]
    [InlineData(Terrain.Trench, Biome.Grassland)]
    public void RoadsPreferShortEasyDetoursOverDifficultGround(Terrain terrain, Biome biome)
    {
        var world = CreateWorld();
        var difficult = Enumerable.Range(15, 11).Select(x => new GridPosition(x, 10)).ToArray();
        foreach (var tile in difficult)
        {
            world.SetTerrain(tile, terrain);
            world.SetBiome(tile, biome);
        }
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.All(difficult, tile => Assert.Equal(RiverConnection.None, world.GetRoadConnections(tile)));
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Fact]
    public void UnchangedRoadsSkipValidationAndTerrainEditsStillRepairThem()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var validate = typeof(SimulationWorld).GetMethod("ValidateVillageRoads",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        validate.Invoke(world, null);
        var checks = world.VillageRoadValidationCount;
        for (var tick = 0; tick < 1000; tick++)
            validate.Invoke(world, null);
        Assert.Equal(checks, world.VillageRoadValidationCount);
        var blocked = new GridPosition(20, 10);
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(blocked));
        world.SetSurfaceWater(blocked, SurfaceWaterKind.FreshwaterLake);
        validate.Invoke(world, null);
        Assert.True(world.VillageRoadValidationCount > checks);
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(blocked));
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(3, false, false)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public void CheapShallowsBridgesBeatLongLandDetoursAndSpanUpToTwoTiles(int width, bool landRoute, bool connects)
    {
        var world = CreateWorld();
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 20; x < 20 + width; x++) world.SetTerrain(new(x, y), Terrain.Shallows);
            world.SetTerrain(new(60, y), Terrain.Ocean);
        }
        if (landRoute)
            for (var x = 20; x < 20 + width; x++) world.SetTerrain(new(x, 0), Terrain.Plains);
        Assert.Equal(connects, world.TryPlaceVillageRoad(First));
        var bridges = Positions(world).Where(p => world.GetTerrain(p) == Terrain.Shallows &&
            world.GetRoadConnections(p) != RiverConnection.None).ToArray();
        if (connects)
        {
            Assert.Equal(width, bridges.Length);
            Assert.All(bridges, bridge =>
                Assert.Equal(RiverConnection.East | RiverConnection.West, world.GetRoadConnections(bridge)));
            world.AdvanceOneTick();
            Assert.All(bridges, bridge => Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(bridge)));
            world.SetTerrain(bridges[0], Terrain.Ocean);
            world.AdvanceOneTick();
            Assert.All(bridges, bridge => Assert.Equal(RiverConnection.None, world.GetRoadConnections(bridge)));
        }
        else Assert.Empty(bridges);
    }

    private static readonly GridPosition First = new(10, 10);
    private static readonly GridPosition Second = new(30, 10);

    private static SimulationWorld CreateWorld(int firstPopulation = 50, int secondPopulation = 50)
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in Positions(world))
            world.SetBiome(position, Biome.Grassland);
        foreach (var village in new[] { First, Second })
        {
            Assert.True(world.TrySpawnTestApeVillage(village));
            var y = village.Y + 1;
            while (world.GetApeVillagePopulationCapacity(village) < 50)
            {
                var building = new GridPosition(village.X, y++);
                world.RemoveCritterAt(building);
                Assert.True(world.TryPlaceApeBuilding(building, ApeStructureKind.ResidentialDistrict));
            }
        }
        foreach (var (village, population) in new[] { (First, firstPopulation), (Second, secondPopulation) })
        {
            while (world.GetApeVillageResidentCount(village) < population)
            {
                var empty = Positions(world).First(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null);
                var id = world.AddCritter(CritterSpecies.Ape, empty);
                Assert.True(world.TryAssignApeToVillage(id, village));
            }
        }
        return world;
    }

    private static IEnumerable<GridPosition> Positions(SimulationWorld world) =>
        Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width));

    [Fact]
    public void RoadsFormAutomaticallyAtFiftyResidents()
    {
        var world = CreateWorld();
        Assert.Equal(0, world.VillageRoadCount);
        world.AdvanceOneTick();
        Assert.Equal(1, world.VillageRoadCount);
        world.AdvanceOneTick();
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Fact]
    public void ForcedRoadCanFollowDiagonalTiles()
    {
        var world = new SimulationWorld(40, 30, Terrain.Plains, seed: 17);
        foreach (var position in Positions(world))
            world.SetBiome(position, Biome.Grassland);
        var first = new GridPosition(10, 10);
        var second = new GridPosition(20, 20);
        Assert.True(world.TrySpawnTestApeVillage(first));
        Assert.True(world.TrySpawnTestApeVillage(second));
        Assert.True(world.TryPlaceVillageRoad(first));
        Assert.Equal(RiverConnection.SouthEast, world.GetRoadConnections(first));
        Assert.Equal(RiverConnection.NorthWest, world.GetRoadConnections(second));
        Assert.Equal(RiverConnection.NorthWest | RiverConnection.SouthEast,
            world.GetRoadConnections(new(15, 15)));
    }

    [Fact]
    public void DistrictClickConnectsCentersAndRoadCanBeRemoved()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(new GridPosition(10, 11)));
        Assert.Equal(1, world.VillageRoadCount);
        Assert.Equal(RiverConnection.East, world.GetRoadConnections(First));
        Assert.Equal(RiverConnection.West, world.GetRoadConnections(Second));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new GridPosition(10, 11)));
        Assert.False(world.TryPlaceVillageRoad(Second));
        Assert.True(world.RemoveVillageRoadAt(new GridPosition(20, 10)));
        Assert.Equal(0, world.VillageRoadCount);
        Assert.All(Positions(world), position => Assert.Equal(RiverConnection.None, world.GetRoadConnections(position)));
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.True(world.RemoveVillageRoadAt(new GridPosition(30, 11)));
        Assert.Equal(0, world.VillageRoadCount);
    }

    [Theory]
    [InlineData(49, 50)]
    [InlineData(50, 49)]
    public void BothVillagesNeedFiftyResidents(int first, int second)
    {
        var world = CreateWorld(first, second);
        Assert.False(world.TryPlaceVillageRoad(First, force: false));
        Assert.Equal(0, world.VillageRoadCount);
        Assert.True(world.TryPlaceVillageRoad(First));
    }

    [Fact]
    public void RoadsCrossRiversButRouteAroundLakesAndSea()
    {
        var world = CreateWorld();
        for (var y = 0; y < world.Height; y++)
        {
            world.SetTerrain(new GridPosition(20, y), Terrain.Ocean);
            world.SetTerrain(new GridPosition(60, y), Terrain.Ocean);
        }
        Assert.False(world.TryPlaceVillageRoad(First));
        var bridge = new GridPosition(20, 10);
        world.SetTerrain(bridge, Terrain.Plains);
        world.SetSurfaceWater(bridge, SurfaceWaterKind.River);
        world.SetSurfaceWater(new GridPosition(25, 10), SurfaceWaterKind.FreshwaterLake);
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Equal(RiverConnection.East | RiverConnection.West, world.GetRoadConnections(bridge));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new GridPosition(25, 10)));
        Assert.All(Positions(world).Where(position => world.GetTerrain(position) is Terrain.Shallows or Terrain.Ocean),
            position => Assert.Equal(RiverConnection.None, world.GetRoadConnections(position)));
    }

    [Fact]
    public void DestroyingVillageRemovesItsRoadImmediately()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.True(world.RemoveApeStructureAt(Second));
        Assert.Equal(0, world.VillageRoadCount);
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(First));
    }

    [Fact]
    public void RoadChoosesNearestEligibleVillageAcrossMapSeam()
    {
        var world = CreateWorld();
        var third = new GridPosition(75, 10);
        Assert.True(world.TrySpawnTestApeVillage(third));
        var y = 11;
        while (world.GetApeVillagePopulationCapacity(third) < 50)
        {
            var building = new GridPosition(75, y++);
            world.RemoveCritterAt(building);
            Assert.True(world.TryPlaceApeBuilding(building, ApeStructureKind.ResidentialDistrict));
        }
        while (world.GetApeVillageResidentCount(third) < 50)
        {
            var empty = Positions(world).First(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null);
            Assert.True(world.TryAssignApeToVillage(world.AddCritter(CritterSpecies.Ape, empty), third));
        }
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.Equal(RiverConnection.West, world.GetRoadConnections(First));
        Assert.Equal(RiverConnection.East, world.GetRoadConnections(third));
        Assert.Equal(RiverConnection.East | RiverConnection.West, world.GetRoadConnections(new GridPosition(0, 10)));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(Second));
    }

    [Fact]
    public void FloodingRoadReroutesItOnNextTick()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        world.SetSurfaceWater(new GridPosition(20, 10), SurfaceWaterKind.FreshwaterLake);
        world.AdvanceOneTick();
        Assert.Equal(1, world.VillageRoadCount);
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(new GridPosition(20, 10)));
    }

    [Fact]
    public void PopulationLossPreservesRoad()
    {
        var world = CreateWorld();
        Assert.True(world.TryPlaceVillageRoad(First));
        var resident = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .First(critter => world.GetApeHomeVillage(critter.Id) == First);
        world.RemoveCritterAt(resident.Position);
        world.AdvanceOneTick();
        Assert.Equal(1, world.VillageRoadCount);
    }

    [Fact]
    public void RoadsIgnoreCloserBarbarianCampsAndCannotStartFromThem()
    {
        var world = CreateWorld();
        var camp = new GridPosition(20, 5);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        Assert.False(world.TryPlaceVillageRoad(camp));
        Assert.True(world.TryPlaceVillageRoad(First));
        Assert.NotEqual(RiverConnection.None, world.GetRoadConnections(Second));
        Assert.Equal(RiverConnection.None, world.GetRoadConnections(camp));
        world.RemoveApeStructureAt(Second);
        Assert.False(world.TryPlaceVillageRoad(First));
    }
}
