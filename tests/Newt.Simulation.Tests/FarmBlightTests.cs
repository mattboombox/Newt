using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class FarmBlightTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition Village = new(10, 10);
    private static readonly GridPosition Farm = new(11, 10);

    private static SimulationWorld CreateWorld(ulong seed = 17)
    {
        var world = new SimulationWorld(48, 24, Terrain.Plains, seed);
        world.SeasonsEnabled = false;
        world.ApeVillageMergingEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        return world;
    }

    private static void AddFarm(SimulationWorld world, GridPosition position, Biome biome = Biome.Grassland)
    {
        world.RemoveCritterAt(position);
        world.SetBiome(position, biome);
        Assert.True(world.TryPlaceApeBuilding(position, ApeStructureKind.Farm));
    }

    private static void SetTick(SimulationWorld world, long tick) =>
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, tick);

    [Theory]
    [InlineData(Biome.Grassland)]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Forest)]
    public void CropFarmsBecomeUnproductiveUnstaffedAndBlockConstructionForOneYear(Biome biome)
    {
        var world = CreateWorld();
        AddFarm(world, Farm, biome);
        Assert.True(world.TryInfectApeAt(Farm, PlagueKind.Blight));
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(Farm));
        Assert.False(world.IsApeStructureOperational(Farm));
        Assert.False(world.IsApeStructureStaffed(Farm));
        Assert.Null(world.GetApeStructureProductionPerMinute(Farm));
        Assert.Equal(0, world.GetApeVillageFarmCount(Village));
        Assert.False(world.TryPlaceApeBuilding(Farm, ApeStructureKind.ResidentialDistrict));
        Assert.False(world.TryPlaceTeleporter(Farm));
        Assert.False(world.TryBlightFarmAt(Farm));
        Assert.Equal(SeasonSystem.TicksPerYear, world.GetFarmBlightRecoveryTick(Farm));
        SetTick(world, SeasonSystem.TicksPerYear - 1);
        world.AdvanceFarmBlight();
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(Farm));
        SetTick(world, SeasonSystem.TicksPerYear);
        world.AdvanceFarmBlight();
        Assert.Null(world.GetApeStructure(Farm));
        Assert.Null(world.GetFarmBlightRecoveryTick(Farm));
        Assert.True(world.TryPlaceApeBuilding(Farm, ApeStructureKind.Farm));
    }

    [Fact]
    public void BlightSpreadsOneRingPerIntervalIncludingDiagonals()
    {
        var world = CreateWorld();
        var second = new GridPosition(12, 11);
        var third = new GridPosition(13, 11);
        AddFarm(world, Farm);
        world.RemoveCritterAt(new(11, 11));
        Assert.True(world.TryPlaceApeBuilding(new(11, 11), ApeStructureKind.ResidentialDistrict));
        AddFarm(world, second);
        AddFarm(world, third);
        Assert.True(world.TryBlightFarmAt(Farm));
        SetTick(world, SimulationWorld.FarmBlightSpreadIntervalTicks - 1);
        world.AdvanceFarmBlight();
        Assert.Equal(ApeStructureKind.Farm, world.GetApeStructure(second));
        SetTick(world, SimulationWorld.FarmBlightSpreadIntervalTicks);
        world.AdvanceFarmBlight();
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(second));
        Assert.Equal(ApeStructureKind.Farm, world.GetApeStructure(third));
        SetTick(world, 2 * SimulationWorld.FarmBlightSpreadIntervalTicks);
        world.AdvanceFarmBlight();
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(third));
        Assert.Equal(SeasonSystem.TicksPerYear + 2 * SimulationWorld.FarmBlightSpreadIntervalTicks,
            world.GetFarmBlightRecoveryTick(third));
    }

    [Fact]
    public void BlightReleasesFarmerAndKeepsFormerResidentInVillage()
    {
        var world = CreateWorld();
        AddFarm(world, Farm);
        var workerPosition = new GridPosition(9, 10);
        world.RemoveCritterAt(workerPosition);
        var worker = world.AddCritter(CritterSpecies.ApeFarmer, workerPosition);
        Assert.True(world.TryAssignApeToVillage(worker, Village));
        var tile = Farm.Y * world.Width + Farm.X;
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeFarmers", Private)!.GetValue(world)!)[tile] = worker.Value;
        ((Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeWorkplaces", Private)!.GetValue(world)!)[worker.Value] = tile;
        Assert.True(world.IsApeStructureStaffed(Farm));
        Assert.True(world.TryBlightFarmAt(Farm));
        Assert.True(world.TryGetCritter(worker, out var freed));
        Assert.Equal(CritterSpecies.Ape, freed.Species);
        Assert.Equal(Village, world.GetApeHomeVillage(worker));
        Assert.False(world.IsApeStructureStaffed(Farm));
        world.RemoveApeStructureAt(Village);
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(Farm));
    }

    [Fact]
    public void BlightRejectsVillagesHousingAndAquaculture()
    {
        var world = CreateWorld();
        Assert.False(world.TryBlightFarmAt(Village));
        Assert.False(world.TryBlightFarmAt(new(-1, 0)));
        world.RemoveCritterAt(Farm);
        world.SetTerrain(Farm, Terrain.Shallows);
        Assert.True(world.TryPlaceApeBuilding(Farm, ApeStructureKind.Farm));
        Assert.Equal(ApeStructureKind.Aquaculture, world.GetApeStructure(Farm));
        Assert.False(world.TryBlightFarmAt(Farm));
    }

    [Fact]
    public void BlightAdvancesThroughNormalWorldTicksAndDoesNotInfectHousing()
    {
        var world = CreateWorld();
        AddFarm(world, Farm);
        var adjacent = new GridPosition(12, 10);
        AddFarm(world, adjacent);
        var housing = new GridPosition(11, 11);
        world.RemoveCritterAt(housing);
        Assert.True(world.TryPlaceApeBuilding(housing, ApeStructureKind.ResidentialDistrict));
        Assert.True(world.TryBlightFarmAt(Farm));
        SetTick(world, SimulationWorld.FarmBlightSpreadIntervalTicks - 1);
        world.AdvanceOneTick();
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(adjacent));
        Assert.Equal(ApeStructureKind.ResidentialDistrict, world.GetApeStructure(housing));
        SetTick(world, SeasonSystem.TicksPerYear - 1);
        world.AdvanceOneTick();
        Assert.Null(world.GetApeStructure(Farm));
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(adjacent));
    }

    [Fact]
    public void RemovingBlightedFarmCancelsItsSpreadAndExpiration()
    {
        var world = CreateWorld();
        AddFarm(world, Farm);
        AddFarm(world, new(12, 10));
        Assert.True(world.TryBlightFarmAt(Farm));
        Assert.True(world.RemoveApeStructureAt(Farm));
        Assert.True(world.TryPlaceApeBuilding(Farm, ApeStructureKind.ResidentialDistrict));
        SetTick(world, SeasonSystem.TicksPerYear);
        world.AdvanceFarmBlight();
        Assert.Equal(ApeStructureKind.ResidentialDistrict, world.GetApeStructure(Farm));
        Assert.Equal(ApeStructureKind.Farm, world.GetApeStructure(new(12, 10)));
    }

    [Fact]
    public void NaturalVillagePlagueCanChooseBlight()
    {
        var world = CreateWorld();
        AddFarm(world, Farm);
        // Populate the outbreak threshold without running unrelated economic growth.
        var homes = (Dictionary<int, int>)typeof(SimulationWorld).GetField("_apeVillageHomes", Private)!.GetValue(world)!;
        for (var y = 0; y < world.Height && world.GetApeVillageResidentCount(Village) < 200; y++)
            for (var x = 0; x < world.Width && world.GetApeVillageResidentCount(Village) < 200; x++)
            {
                var position = new GridPosition(x, y);
                if (world.IsOccupied(position) || world.GetApeStructure(position) is not null) continue;
                var id = world.AddCritter(CritterSpecies.Ape, position);
                homes[id.Value] = Village.Y * world.Width + Village.X;
            }
        NaturalEvents.SetEnabled(world, true);
        var infections = (Dictionary<int, (PlagueKind, long, long)>)typeof(SimulationWorld)
            .GetField("_plagues", Private)!.GetValue(world)!;
        for (var attempt = 0; attempt < 10000 && world.GetApeStructure(Farm) is not ApeStructureKind.BlightedFarm; attempt++)
        {
            world.TryStartVillagePlague(Village.Y * world.Width + Village.X);
            infections.Clear();
        }
        Assert.Equal(ApeStructureKind.BlightedFarm, world.GetApeStructure(Farm));
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(11, false)]
    public void SettledApesForageOnlyWhenVillageFoodIsLow(int storedFood, bool shouldFeed)
    {
        var world = CreateWorld();
        AddFarm(world, Farm);
        AddFarm(world, new(12, 10));
        var position = new GridPosition(9, 10);
        world.RemoveCritterAt(position);
        world.SetBiome(position, Biome.Swamp);
        var ape = world.AddCritter(CritterSpecies.Ape, position);
        Assert.True(world.TryAssignApeToVillage(ape, Village));
        world.StoreApeVillageFood(Village, storedFood);
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == ape);
        var before = world.GetCritter(index).Energy;
        var foodBefore = world.GetTileNutrition(position);
        var fed = (bool)typeof(SimulationWorld).GetMethod("TryFeedApeFoliage", Private)!.Invoke(world, [index])!;
        Assert.Equal(shouldFeed, fed);
        Assert.Equal(before + (shouldFeed ? 1 : 0), world.GetCritter(index).Energy);
        Assert.Equal(foodBefore - (shouldFeed ? 1 : 0), world.GetTileNutrition(position));
        if (!shouldFeed)
            Assert.False((bool)typeof(SimulationWorld).GetMethod("TryMoveToAdjacentApeFoliage", Private)!
                .Invoke(world, [index])!);
    }

    [Fact]
    public void UnaffiliatedApesCanStillForage()
    {
        var world = CreateWorld();
        var position = new GridPosition(30, 10);
        world.SetBiome(position, Biome.Swamp);
        var ape = world.AddCritter(CritterSpecies.Ape, position);
        var index = world.CritterCount - 1;
        Assert.Null(world.GetApeHomeVillage(ape));
        Assert.True((bool)typeof(SimulationWorld).GetMethod("TryFeedApeFoliage", Private)!.Invoke(world, [index])!);
    }
}
