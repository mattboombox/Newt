using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeVillageMergingTests
{
    [Fact]
    public void TouchingVillagesCombineResidentsDogsResearchAndUniqueBuildings()
    {
        var world = CreateWorld();
        // Found the right-hand village first to verify identity is not chosen by tile order.
        var older = new GridPosition(30, 15);
        var newer = new GridPosition(15, 15);
        Assert.True(world.TrySpawnTestApeVillage(older));
        Assert.True(world.TrySpawnTestApeVillage(newer));
        var identity = world.GetApeVillageId(older);
        foreach (var village in new[] { older, newer })
        {
            while (world.GetApeVillagePopulationCapacity(village) < 205)
                Assert.True(world.TryBuildApeStructure(Tile(world, village), ApeStructureKind.ResidentialDistrict));
            AddResident(world, village, CritterSpecies.ApeChieftain);
            AddResident(world, village, CritterSpecies.ApeScholar);
            AddResident(world, village, CritterSpecies.ApeScholar);
            while (world.GetApeVillageResidentCount(village) < 200)
                AddResident(world, village, CritterSpecies.Ape);
            Assert.True(world.TryBuildApeStructure(Tile(world, village), ApeStructureKind.Library));
            var killer = Enumerable.Range(0, world.CritterCount).First(index =>
                world.GetApeHomeVillage(world.GetCritter(index).Id) == village);
            for (var attempt = 0; attempt < 1000 && world.GetApeVillageDogCount(village) == 0; attempt++)
                world.TryRecruitDogFromWolfKill(killer);
            Assert.Equal(1, world.GetApeVillageDogCount(village));
            world.StoreApeVillageFood(village, 1);
        }
        Assert.True(world.TryResearchApeTechnology(Tile(world, newer), ApeTechnology.Aquaculture));
        GrowUntilTouching(world, older, newer);
        var residents = world.CritterCount;
        var buildings = Positions(world).Where(position => world.GetApeStructure(position) is not null).ToArray();
        var housing = world.GetApeVillagePopulationCapacity(older) + world.GetApeVillagePopulationCapacity(newer);
        var food = world.GetApeVillageFood(older) + world.GetApeVillageFood(newer);
        var wood = world.GetApeVillageWood(older) + world.GetApeVillageWood(newer);

        world.MergeAdjacentApeVillages();

        Assert.Equal(1, world.ApeVillageCount);
        Assert.Equal(identity, world.GetApeVillageId(newer));
        Assert.Equal(ApeStructureKind.ResidentialDistrict, world.GetApeStructure(newer));
        Assert.Equal(residents, world.CritterCount);
        Assert.All(Enumerable.Range(0, world.CritterCount), index =>
            Assert.Equal(older, world.GetApeHomeVillage(world.GetCritter(index).Id)));
        Assert.All(buildings, position => Assert.Equal(older, world.GetApeStructureVillage(position)));
        Assert.Single(buildings, position => world.GetApeStructure(position) is ApeStructureKind.Library);
        Assert.Equal(1, world.GetApeVillageChieftainCount(older));
        Assert.Equal(1, world.GetApeVillageWarriorCount(older));
        Assert.Equal(2, world.GetApeVillageScholarCount(older));
        Assert.Equal(2, world.GetApeVillageDogCount(older));
        Assert.True(world.HasApeTechnology(Tile(world, older), ApeTechnology.Aquaculture));
        Assert.Empty(world.GetApeVillageTechnologies(newer));
        Assert.Equal(housing + 5, world.GetApeVillagePopulationCapacity(older));
        Assert.Equal(Math.Min(food, world.GetApeVillageFoodCapacity(older)), world.GetApeVillageFood(older));
        Assert.Equal(Math.Min(wood, world.GetApeVillageWoodCapacity(older)), world.GetApeVillageWood(older));
        Assert.False(world.TryBuildApeStructure(Tile(world, older), ApeStructureKind.Library));
        world.MergeAdjacentApeVillages();
        Assert.Equal(1, world.ApeVillageCount);
    }

    [Fact]
    public void SeparateVillagesStaySeparateAndSwitchCanDisableThenEnableMerging()
    {
        var world = CreateWorld();
        var first = new GridPosition(15, 15);
        var second = new GridPosition(30, 15);
        Assert.True(world.TrySpawnTestApeVillage(first));
        Assert.True(world.TrySpawnTestApeVillage(second));
        world.MergeAdjacentApeVillages();
        Assert.Equal(2, world.ApeVillageCount);
        world.ApeVillageMergingEnabled = false;
        GrowUntilTouching(world, first, second);
        world.MergeAdjacentApeVillages();
        Assert.Equal(2, world.ApeVillageCount);
        world.ApeVillageMergingEnabled = true;
        world.AdvanceOneTick();
        Assert.Equal(1, world.ApeVillageCount);
    }

    [Fact]
    public void BarbarianCampDoesNotMergeWithTouchingApeBuildings()
    {
        var world = CreateWorld();
        var village = new GridPosition(15, 15);
        var camp = new GridPosition(30, 15);
        Assert.True(world.TrySpawnTestApeVillage(village));
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        // Camp guards initially occupy all neighboring tiles; leave room for a building to touch it.
        var guard = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).First(critter =>
            critter.Position != camp && Math.Abs(critter.Position.X - camp.X) <= 1 &&
            Math.Abs(critter.Position.Y - camp.Y) <= 1);
        Assert.True(world.RemoveCritterAt(guard.Position));
        AddResident(world, camp, CritterSpecies.ApeWarrior);
        GrowUntilTouching(world, village, camp);
        world.MergeAdjacentApeVillages();
        Assert.Equal(2, world.ApeVillageCount);
        Assert.True(world.IsBarbarianVillage(camp));
        Assert.Equal(10, world.GetApeVillageResidentCount(camp));
        Assert.Equal(2, world.GetApeVillageResidentCount(village));
    }

    [Fact]
    public void TouchingChainMergesInOnePassAcrossWorldSeam()
    {
        var world = CreateWorld();
        var first = new GridPosition(4, 15);
        var second = new GridPosition(69, 15);
        var third = new GridPosition(54, 15);
        Assert.True(world.TrySpawnTestApeVillage(first));
        Assert.True(world.TrySpawnTestApeVillage(second));
        Assert.True(world.TrySpawnTestApeVillage(third));
        GrowUntilTouching(world, first, second);
        GrowUntilTouching(world, second, third);
        world.MergeAdjacentApeVillages();
        Assert.Equal(1, world.ApeVillageCount);
        Assert.Equal(first, world.GetApeStructureVillage(second));
        Assert.Equal(first, world.GetApeStructureVillage(third));
        Assert.Equal(6, world.GetApeVillageResidentCount(first));
    }

    [Fact]
    public void VillageCentersCannotBePlacedInShallows()
    {
        var world = CreateWorld();
        var first = new GridPosition(30, 15);
        var second = new GridPosition(15, 15);
        world.SetTerrain(second, Terrain.Shallows);
        Assert.True(world.TrySpawnTestApeVillage(first));
        Assert.False(world.TrySpawnTestApeVillage(second));
        Assert.False(world.TrySpawnBarbarianApeVillage(second));
        Assert.Equal(1, world.ApeVillageCount);
        Assert.Null(world.GetApeStructure(second));
        Assert.Null(world.GetApeStructureVillage(second));
        Assert.Equal(2, world.GetApeVillageResidentCount(first));
    }

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in Positions(world))
            world.SetBiome(position, Biome.Grassland);
        return world;
    }

    private static int Tile(SimulationWorld world, GridPosition position) => position.Y * world.Width + position.X;

    private static IEnumerable<GridPosition> Positions(SimulationWorld world) =>
        Enumerable.Range(0, world.Width * world.Height).Select(tile => new GridPosition(tile % world.Width, tile / world.Width));

    private static void AddResident(SimulationWorld world, GridPosition village, CritterSpecies species)
    {
        var position = Positions(world).First(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null);
        var id = world.AddCritter(species, position);
        Assert.True(world.TryAssignApeToVillage(id, village));
    }

    private static void GrowUntilTouching(SimulationWorld world, GridPosition first, GridPosition second)
    {
        for (var attempt = 0; attempt < 1500; attempt++)
        {
            var firstBuildings = Positions(world).Where(position => world.GetApeStructureVillage(position) == first).ToArray();
            var secondBuildings = Positions(world).Where(position => world.GetApeStructureVillage(position) == second).ToHashSet();
            if (firstBuildings.Any(position => Enumerable.Range(-1, 3).Any(dx =>
                    Enumerable.Range(-1, 3).Any(dy => (dx != 0 || dy != 0) && secondBuildings.Contains(
                        new GridPosition((position.X + dx + world.Width) % world.Width, position.Y + dy))))))
                return;
            Assert.True(world.TryBuildApeStructure(Tile(world, first), ApeStructureKind.ResidentialDistrict));
        }
        Assert.Fail("Village buildings never touched.");
    }
}
