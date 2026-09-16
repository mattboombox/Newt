using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class PlayerVillagePlacementTests
{
    private static readonly GridPosition Village = new(10, 10);

    private static SimulationWorld CreateWorld()
    {
        var world = new SimulationWorld(40, 20, Terrain.Plains, seed: 1701);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        foreach (var critter in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(critter.Position);
        return world;
    }

    [Theory]
    [InlineData(Biome.Grassland, Terrain.Plains, ApeStructureKind.Farm)]
    [InlineData(Biome.Arid, Terrain.Plains, ApeStructureKind.Farm)]
    [InlineData(Biome.Swamp, Terrain.Plains, ApeStructureKind.RicePaddy)]
    [InlineData(Biome.Jungle, Terrain.Plains, ApeStructureKind.RicePaddy)]
    [InlineData(Biome.Forest, Terrain.Plains, ApeStructureKind.Orchard)]
    [InlineData(Biome.Grassland, Terrain.Shallows, ApeStructureKind.Aquaculture)]
    public void FarmToolChoosesBiomeVariantWithoutResearch(Biome biome, Terrain terrain, ApeStructureKind expected)
    {
        var world = CreateWorld();
        var target = new GridPosition(11, 10);
        world.SetTerrain(target, terrain);
        world.SetBiome(target, biome);
        var wood = world.GetApeVillageWood(Village);
        Assert.Empty(world.GetApeVillageTechnologies(Village));
        Assert.True(world.TryPlaceApeBuilding(target, ApeStructureKind.Farm));
        Assert.Equal(expected, world.GetApeStructure(target));
        Assert.Equal(Village, world.GetApeStructureVillage(target));
        Assert.Equal(Math.Max(0, wood - SimulationWorld.GetApeStructureWoodCost(expected)),
            world.GetApeVillageWood(Village));
        Assert.Equal(1, world.GetApeVillageFarmCount(Village));
        Assert.Equal(0, world.GetApeVillageFoodPerMinute(Village));
    }

    [Theory]
    [InlineData(ApeStructureKind.LumberCamp)]
    [InlineData(ApeStructureKind.NavalDistrict)]
    [InlineData(ApeStructureKind.ResidentialDistrict)]
    [InlineData(ApeStructureKind.MilitaryDistrict)]
    [InlineData(ApeStructureKind.Library)]
    public void PlayerBuildingsIgnorePopulationAndClampCosts(ApeStructureKind kind)
    {
        var world = CreateWorld();
        world.StoreApeVillageFood(Village, 3);
        for (var x = 11; x <= 15; x++)
        {
            var target = new GridPosition(x, 10);
            if (kind is ApeStructureKind.NavalDistrict)
                world.SetTerrain(target, Terrain.Shallows);
            var food = world.GetApeVillageFood(Village);
            var wood = world.GetApeVillageWood(Village);
            Assert.True(world.TryPlaceApeBuilding(target, kind));
            Assert.Equal(Math.Max(0, food - SimulationWorld.GetApeStructureFoodCost(kind)), world.GetApeVillageFood(Village));
            Assert.Equal(Math.Max(0, wood - SimulationWorld.GetApeStructureWoodCost(kind)), world.GetApeVillageWood(Village));
        }
    }

    [Fact]
    public void PlacementRequiresUnbrokenConnectionAndDoesNotChargeForInvalidTiles()
    {
        var world = CreateWorld();
        var wood = world.GetApeVillageWood(Village);
        Assert.False(world.TryPlaceApeBuilding(new GridPosition(15, 10), ApeStructureKind.Library));
        Assert.Equal(wood, world.GetApeVillageWood(Village));
        Assert.True(world.TryPlaceApeBuilding(new GridPosition(11, 10), ApeStructureKind.Library));
        Assert.True(world.TryPlaceApeBuilding(new GridPosition(12, 10), ApeStructureKind.Library));
        Assert.True(world.RemoveApeStructureAt(new GridPosition(11, 10)));
        Assert.False(world.TryPlaceApeBuilding(new GridPosition(13, 10), ApeStructureKind.Library));
        Assert.False(world.TryPlaceApeBuilding(Village, ApeStructureKind.Library));
        var unsuitable = new GridPosition(10, 11);
        world.SetTerrain(unsuitable, Terrain.Mountain);
        Assert.False(world.TryPlaceApeBuilding(unsuitable, ApeStructureKind.Farm));
    }

    [Fact]
    public void DogsJoinNearestVillageRegardlessOfPopulationLimit()
    {
        var world = CreateWorld();
        var second = new GridPosition(30, 10);
        Assert.True(world.TrySpawnTestApeVillage(second));
        for (var y = 2; y < 7; y++)
        {
            var position = new GridPosition(29, y);
            Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, position));
            Assert.True(world.TryGetCritterAt(position, out var dog));
            Assert.Equal(CritterSpecies.Dog, dog.Species);
            Assert.Equal(second, world.GetApeHomeVillage(dog.Id));
        }
    }

    [Fact]
    public void DogWithoutVillageSpawnsAsWolf()
    {
        var world = new SimulationWorld(5, 5, Terrain.Plains);
        var position = new GridPosition(2, 2);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, position));
        Assert.True(world.TryGetCritterAt(position, out var wolf));
        Assert.Equal(CritterSpecies.Wolf, wolf.Species);
        Assert.Null(world.GetApeHomeVillage(wolf.Id));
    }

    [Fact]
    public void PlayerSpawnedDogJoinsNearestBarbarianCamp()
    {
        var world = CreateWorld();
        var camp = new GridPosition(30, 10);
        Assert.True(world.TrySpawnBarbarianApeVillage(camp));
        var position = new GridPosition(29, 5);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Dog, position));
        Assert.True(world.TryGetCritterAt(position, out var dog));
        Assert.Equal(CritterSpecies.Dog, dog.Species);
        Assert.Equal(camp, world.GetApeHomeVillage(dog.Id));
        Assert.True(world.IsBarbarianApe(dog.Id));
        Assert.Equal(1, world.GetApeVillageDogCount(camp));
    }

    [Theory]
    [InlineData(ApeStructureKind.Farm)]
    [InlineData(ApeStructureKind.LumberCamp)]
    [InlineData(ApeStructureKind.NavalDistrict)]
    [InlineData(ApeStructureKind.ResidentialDistrict)]
    [InlineData(ApeStructureKind.MilitaryDistrict)]
    [InlineData(ApeStructureKind.Library)]
    public void PlayerCannotAttachBuildingsToBarbarianCamp(ApeStructureKind kind)
    {
        var world = CreateWorld();
        Assert.True(world.RemoveApeStructureAt(Village));
        Assert.True(world.TrySpawnBarbarianApeVillage(Village));
        foreach (var resident in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            world.RemoveCritterAt(resident.Position);
        var target = new GridPosition(11, 10);
        if (kind is ApeStructureKind.NavalDistrict)
            world.SetTerrain(target, Terrain.Shallows);
        var food = world.GetApeVillageFood(Village);
        var wood = world.GetApeVillageWood(Village);
        Assert.False(world.TryPlaceApeBuilding(target, kind));
        Assert.Null(world.GetApeStructure(target));
        Assert.Equal(food, world.GetApeVillageFood(Village));
        Assert.Equal(wood, world.GetApeVillageWood(Village));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DestroyedCampSurvivorsBecomeRegularApes(bool coastal)
    {
        var world = CreateWorld();
        Assert.True(world.RemoveApeStructureAt(Village));
        if (coastal)
        {
            for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                world.SetTerrain(new GridPosition(x, y), Terrain.Beach);
            world.SetTerrain(new GridPosition(10, 11), Terrain.Shallows);
        }
        Assert.True(world.TrySpawnBarbarianApeVillage(Village));
        var residents = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        Assert.Contains(residents, critter => critter.Species is CritterSpecies.ApeWarrior);
        Assert.Contains(residents, critter => critter.Species is CritterSpecies.ApeChieftain);
        if (coastal)
            Assert.Contains(residents, critter => critter.Species is CritterSpecies.ApeSailor);

        Assert.True(world.RemoveApeStructureAt(Village));

        foreach (var resident in residents)
        {
            Assert.True(world.TryGetCritter(resident.Id, out var survivor));
            Assert.Equal(CritterSpecies.Ape, survivor.Species);
            Assert.Equal(resident.Position, survivor.Position);
            Assert.Equal(Math.Min(resident.Energy, CritterNutritions.Get(CritterSpecies.Ape).MaximumEnergy), survivor.Energy);
            Assert.Null(world.GetApeHomeVillage(survivor.Id));
            Assert.False(world.IsBarbarianApe(survivor.Id));
        }
    }
}
