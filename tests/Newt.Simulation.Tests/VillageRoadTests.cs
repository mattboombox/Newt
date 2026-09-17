using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillageRoadTests
{
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
            world.SetTerrain(new GridPosition(20, y), Terrain.Shallows);
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
