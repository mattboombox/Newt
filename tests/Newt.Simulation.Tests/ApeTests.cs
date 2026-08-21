using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTests
{
    [Fact]
    public void MonkeyEvolvesIntoApe()
    {
        Assert.Equal(1, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.Monkey));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(CritterSpecies.Monkey, out var evolved));
        Assert.Equal(CritterSpecies.Ape, evolved);
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(CritterSpecies.Ape, out var ancestor));
        Assert.Equal(CritterSpecies.Monkey, ancestor);
    }

    [Fact]
    public void ApeHuntsEverySpeciesOutsideItsOwnCivilization()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(
                species is not (CritterSpecies.Ape or CritterSpecies.ApeSailor),
                SimulationWorld.CanEat(CritterSpecies.Ape, species));
        }
    }

    [Fact]
    public void FirstReproductionFoundsVillageBesideGrasslandWithoutFarm()
    {
        var world = CreateFedApeWorld(hasGrassland: true);

        AdvanceUntilVillage(world);

        Assert.True(world.ApeVillageCount == 1, DescribeWorld(world));
        var village = FindStructure(world, ApeStructureKind.Village);
        Assert.True(HasAdjacentBiome(world, village, Biome.Grassland));
        Assert.Equal(0, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(0, CountStructures(world, ApeStructureKind.NavalDistrict));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
    }

    [Fact]
    public void FirstReproductionFoundsVillageBesideBeachWithoutHarbor()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        world.SetTerrain(new GridPosition(0, 1), Terrain.Beach);

        AdvanceUntilVillage(world);

        Assert.True(world.ApeVillageCount == 1, DescribeWorld(world));
        Assert.Equal(0, CountStructures(world, ApeStructureKind.Farm));
        var village = FindStructure(world, ApeStructureKind.Village);
        Assert.True(HasAdjacentTerrain(world, village, Terrain.Beach));
        Assert.Equal(0, CountStructures(world, ApeStructureKind.NavalDistrict));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
    }

    [Fact]
    public void FiveResidentsBuildFarmAndHousingAddsFiveCapacity()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        world.AdvanceOneTick();

        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(5, world.GetApeVillagePopulationCapacity(village));
        AddPreyToEmptyTiles(world, 8);

        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                CountStructures(world, ApeStructureKind.ResidentialDistrict) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.ResidentialDistrict));
        Assert.Equal(10, world.GetApeVillagePopulationCapacity(village));
    }

    [Theory]
    [InlineData(Biome.Swamp, ApeStructureKind.RicePaddy)]
    [InlineData(Biome.Forest, ApeStructureKind.Orchard)]
    public void FiveResidentsBuildBiomeFoodDistrictThatProducesLikeFarm(
        Biome biome,
        ApeStructureKind expectedStructure)
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetBiome(new GridPosition(x, y), biome);
            }
        }
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        world.AdvanceOneTick();

        Assert.Equal(1, CountStructures(world, expectedStructure));
        var startingFood = world.GetApeVillageFood(village);
        for (var tick = 0; tick < 14 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(startingFood + 1, world.GetApeVillageFood(village));
    }

    [Fact]
    public void VillageFoodStorageGrowsWithFoodDistrictsAndStopsAtItsLimit()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        Assert.Equal(5, world.GetApeVillageFoodCapacity(village));
        AddAssignedResidents(world, village, 4);

        world.AdvanceOneTick();

        Assert.Equal(15, world.GetApeVillageFoodCapacity(village));
        foreach (var ape in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape)
            .Skip(1)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(ape));
        }
        foreach (var prey in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is not CritterSpecies.Ape)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(prey));
        }
        Assert.Equal(1, world.GetApeVillageResidentCount(village));

        for (var tick = 0; tick < 6 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetApeVillageResidentCount(village));
        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(15, world.GetApeVillageFood(village));
        Assert.Equal(15, world.GetApeVillageFoodCapacity(village));
    }

    [Fact]
    public void LumberCampUsesFoodToBootstrapAndProducesWood()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetBiome(new GridPosition(x, y), Biome.Forest);
            }
        }
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        Assert.Equal(6, world.GetApeVillageWood(village));
        AddAssignedResidents(world, village, 4);

        for (var tick = 0;
            tick < 60 * SimulationWorld.TicksPerSecond &&
                CountStructures(world, ApeStructureKind.LumberCamp) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.LumberCamp));
        Assert.Equal(6, world.GetApeVillageWood(village));
        foreach (var ape in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape)
            .Skip(1)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(ape));
        }
        var startingWood = world.GetApeVillageWood(village);

        for (var tick = 0; tick < 14 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingWood + 1, world.GetApeVillageWood(village));
    }

    [Theory]
    [InlineData(Biome.Jungle, 10)]
    [InlineData(Biome.Forest, 14)]
    [InlineData(Biome.Taiga, 18)]
    public void LumberProductionRateDependsOnBiome(Biome biome, int expectedSeconds)
    {
        Assert.Equal(
            expectedSeconds * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetApeLumberProductionIntervalTicks(biome));
    }

    [Fact]
    public void ApeBuildingResourceCostsAvoidBootstrapDeadlocks()
    {
        Assert.Equal(0, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.LumberCamp));
        Assert.Equal(3, SimulationWorld.GetApeStructureFoodCost(ApeStructureKind.LumberCamp));
        Assert.Equal(4, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.ResidentialDistrict));
        Assert.Equal(5, SimulationWorld.GetApeStructureFoodCost(ApeStructureKind.ResidentialDistrict));
        Assert.Equal(6, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.NavalDistrict));
        Assert.Equal(2, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.Farm));
        Assert.Equal(2, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.RicePaddy));
        Assert.Equal(2, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.Orchard));
    }

    [Fact]
    public void CappedGrasslandVillageAlternatesHousingWithAdditionalFarms()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                CountStructures(world, ApeStructureKind.ResidentialDistrict) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(1, CountStructures(world, ApeStructureKind.ResidentialDistrict));
        AddAssignedResidents(world, village, 5);

        world.AdvanceOneTick();

        Assert.Equal(2, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(1, CountStructures(world, ApeStructureKind.ResidentialDistrict));
        Assert.Equal(10, world.GetApeVillagePopulationCapacity(village));
    }

    [Fact]
    public void FiveCoastalResidentsBuildHarborThatRecruitsSailor()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        world.SetTerrain(new GridPosition(0, 1), Terrain.Beach);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.ApeSailor) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.NavalDistrict));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ApeSailor));
        Assert.Equal(5, world.GetApeVillageResidentCount(village));
    }

    [Fact]
    public void FiveResidentVillageKeepsFourCiviliansAfterHarborRecruitment()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        world.SetTerrain(new GridPosition(0, 1), Terrain.Beach);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        for (var tick = 0; tick < 3 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(5, world.GetApeVillageResidentCount(village));
        Assert.Equal(4, world.GetApeVillageCivilianCount(village));
        Assert.Equal(1, world.GetApeVillageSailorCount(village));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(14, 2)]
    [InlineData(15, 3)]
    [InlineData(19, 3)]
    [InlineData(20, 4)]
    [InlineData(100, 4)]
    public void SailorLimitScalesWithVillagePopulation(int population, int expectedLimit)
    {
        Assert.Equal(expectedLimit, SimulationWorld.GetApeSailorLimitForPopulation(population));
    }

    [Fact]
    public void ApeSailorsCannotReproduceEvenAtFullEnergy()
    {
        Assert.False(SimulationWorld.CanSpeciesReproduce(CritterSpecies.ApeSailor));
        Assert.True(SimulationWorld.CanSpeciesReproduce(CritterSpecies.Ape));
    }

    [Fact]
    public void AssignedSailorFillsItsEnergyFromVillageFoodAfterRecruitment()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();
        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));

        var beach = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is not null)
            .SelectMany(position => GetCardinalNeighbors(world, position))
            .Distinct()
            .First(position => world.GetApeStructure(position) is null &&
                !world.IsOccupied(position));
        world.SetTerrain(beach, Terrain.Beach);
        world.StoreApeVillageFood(village, 8);

        for (var tick = 0;
            tick < 45 * SimulationWorld.TicksPerSecond &&
                world.GetApeVillageSailorCount(village) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(1, world.GetApeVillageSailorCount(village));

        world.AdvanceOneTick();

        var sailor = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.ApeSailor);
        Assert.Equal(sailor.MaximumEnergy, sailor.Energy);
        Assert.False(sailor.CanReproduce);
    }

    [Fact]
    public void InlandVillageAddsOnlyOneHarborAfterItsDistrictsReachBeach()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();
        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(0, CountStructures(world, ApeStructureKind.NavalDistrict));

        var reachableTiles = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is not null)
            .SelectMany(position => GetCardinalNeighbors(world, position))
            .Distinct()
            .Where(position => world.GetApeStructure(position) is null &&
                !world.IsOccupied(position))
            .Take(2)
            .ToArray();
        Assert.Equal(2, reachableTiles.Length);
        foreach (var beach in reachableTiles)
        {
            world.SetTerrain(beach, Terrain.Beach);
        }

        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.NavalDistrict));
    }

    [Fact]
    public void ApeSailorHuntsSeaLifeExceptPlankton()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var isSeaPrey = species is CritterSpecies.Jellyfish or CritterSpecies.Worm or
                CritterSpecies.Trilobite or CritterSpecies.SeaScorpion or CritterSpecies.Nautilus or
                CritterSpecies.Fish or CritterSpecies.Crab or CritterSpecies.Squid or
                CritterSpecies.SquidEgg;
            Assert.Equal(isSeaPrey, SimulationWorld.CanEat(CritterSpecies.ApeSailor, species));
        }
        Assert.False(SimulationWorld.CanEat(CritterSpecies.ApeSailor, CritterSpecies.Plankton));
    }

    [Fact]
    public void OrdinaryApeOnlyPursuesFishWhenItIsAlreadyAdjacent()
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.Ape,
            CritterSpecies.Fish,
            1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.Ape,
            CritterSpecies.Fish,
            2));
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.ApeSailor,
            CritterSpecies.Fish,
            6));
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.Ape,
            CritterSpecies.Deer,
            6));
    }

    [Fact]
    public void EmptyVillageAndConnectedBuildingsBecomeScavengableNutrition()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();

        var structureTiles = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is not null)
            .ToArray();
        Assert.True(structureTiles.Length >= 2);
        var nutritionBefore = structureTiles.ToDictionary(
            position => position,
            world.GetTileNutrition);

        foreach (var residentPosition in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape or CritterSpecies.ApeSailor)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(residentPosition));
        }

        world.AdvanceOneTick();

        Assert.Equal(0, world.ApeVillageCount);
        foreach (var structureTile in structureTiles)
        {
            Assert.Null(world.GetApeStructure(structureTile));
            Assert.Equal(nutritionBefore[structureTile] + 1, world.GetTileNutrition(structureTile));
        }
    }

    [Fact]
    public void ResidentApeMealIsSplitWithoutDoubleCounting()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var ape = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Ape);
        var startingEnergy = ape.Energy;
        var preyPosition = GetCardinalNeighbors(world, ape.Position)
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        world.AddCritter(CritterSpecies.Deer, preyPosition);

        for (var tick = 0;
            tick < 10 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Deer) > 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Deer));
        Assert.True(world.TryGetCritter(ape.Id, out var fedApe));
        var personalFood = fedApe.Energy - startingEnergy;
        var settlementFood = world.GetApeVillageFood(village) + world.GetApeCarriedFood(ape.Id);
        Assert.True(personalFood > 0);
        Assert.Equal(5, personalFood + settlementFood);
    }

    [Fact]
    public void StuckResidentAutomaticallyReturnsCarriedFoodAfterThirtySeconds()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        Assert.True(world.RemoveCritterAt(world.GetCritter(0).Position));

        var remote = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is null && !world.IsOccupied(position))
            .OrderByDescending(position => WrappedDistance(world, position, village))
            .First();
        var residentId = world.AddCritter(CritterSpecies.Ape, remote);
        Assert.True(world.TryAssignApeToVillage(residentId, village));
        for (var meal = 0; meal < 2 && world.GetApeCarriedFood(residentId) == 0; meal++)
        {
            Assert.True(world.TryGetCritter(residentId, out var hunter));
            var preyPosition = GetCardinalNeighbors(world, hunter.Position)
                .First(position => world.GetApeStructure(position) is null &&
                    !world.IsOccupied(position));
            world.AddCritter(CritterSpecies.Deer, preyPosition);
            for (var tick = 0;
                tick < 10 * SimulationWorld.TicksPerSecond &&
                    world.GetCritterCount(CritterSpecies.Deer) > 0;
                tick++)
            {
                world.AdvanceOneTick();
            }
        }

        var carriedFood = world.GetApeCarriedFood(residentId);
        Assert.True(carriedFood > 0);
        Assert.True(world.TryGetCritter(residentId, out var returningApe));
        foreach (var neighbor in GetSurroundingNeighbors(world, returningApe.Position))
        {
            if (world.GetApeStructure(neighbor) is null)
            {
                world.SetTerrain(neighbor, Terrain.Mountain);
            }
        }

        for (var tick = 0;
            tick < SimulationWorld.ApeFoodReturnStallTicks + 6 * SimulationWorld.TicksPerSecond;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetApeCarriedFood(residentId));
        Assert.Equal(carriedFood, world.GetApeVillageFood(village));
        Assert.True(world.TryGetCritter(residentId, out var unstuckApe));
        Assert.Equal(returningApe.Position, unstuckApe.Position);
    }

    [Fact]
    public void HungryResidentConsumesVillageFoodWithoutReturningHome()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var founder = world.GetCritter(0);
        for (var meal = 0; meal < 3 && world.GetApeVillageFood(village) == 0; meal++)
        {
            Assert.True(world.TryGetCritter(founder.Id, out var hunter));
            var preyPosition = GetCardinalNeighbors(world, hunter.Position)
                .First(position => !world.IsOccupied(position) &&
                    world.GetApeStructure(position) is null);
            world.AddCritter(CritterSpecies.Deer, preyPosition);
            for (var tick = 0;
                tick < 30 * SimulationWorld.TicksPerSecond &&
                    (world.GetCritterCount(CritterSpecies.Deer) > 0 ||
                        world.GetApeVillageFood(village) == 0);
                tick++)
            {
                world.AdvanceOneTick();
            }
        }

        var storedFood = world.GetApeVillageFood(village);
        Assert.True(storedFood > 0);
        foreach (var apePosition in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape or CritterSpecies.ApeSailor)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(apePosition));
        }

        var remote = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .Where(position => world.GetApeStructure(position) is null && !world.IsOccupied(position))
            .OrderByDescending(position => WrappedDistance(world, position, village))
            .First();
        foreach (var neighbor in GetSurroundingNeighbors(world, remote))
        {
            if (world.GetApeStructure(neighbor) is null)
            {
                world.SetTerrain(neighbor, Terrain.Mountain);
            }
        }
        var residentId = world.AddCritter(CritterSpecies.Ape, remote);
        Assert.True(world.TryAssignApeToVillage(residentId, village));

        for (var tick = 0; tick < 70 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(residentId, out var resident));
        Assert.Equal(remote, resident.Position);
        Assert.Equal(6, resident.Energy);
        Assert.Equal(storedFood - 1, world.GetApeVillageFood(village));
    }

    [Fact]
    public void SailorReturnsSeafoodToConnectedHarbor()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        world.SetTerrain(new GridPosition(0, 1), Terrain.Beach);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.ApeSailor) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        var harbor = FindStructure(world, ApeStructureKind.NavalDistrict);
        var startingFood = world.GetApeVillageFood(village);
        var startingSailor = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.ApeSailor);
        var ocean = GetCardinalNeighbors(world, harbor)
            .First(position => world.GetApeStructure(position) is null && !world.IsOccupied(position));
        world.SetTerrain(ocean, Terrain.Ocean);
        world.AddCritter(CritterSpecies.Fish, ocean);

        for (var tick = 0;
            tick < 30 * SimulationWorld.TicksPerSecond &&
                world.GetApeVillageFood(village) <= startingFood;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        var deliveredFood = world.GetApeVillageFood(village) - startingFood;
        Assert.True(world.TryGetCritter(startingSailor.Id, out var sailor));
        Assert.Equal(4, sailor.Energy - startingSailor.Energy + deliveredFood);
        Assert.Equal(0, world.GetApeCarriedFood(sailor.Id));
    }

    [Fact]
    public void LaterReproductionCreatesAResidentAtTheVillage()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddPreyToEmptyTiles(world, 3);

        for (var tick = 0;
            tick < 4 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Ape) < 2;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Ape));
        Assert.Equal(2, world.GetApeVillageResidentCount(village));
    }

    private static SimulationWorld CreateFedApeWorld(bool hasGrassland)
    {
        var world = new SimulationWorld(9, 3, Terrain.Plains, seed: 1701);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        if (hasGrassland)
        {
            for (var y = 0; y < world.Height; y++)
            {
                for (var x = 0; x < world.Width; x++)
                {
                    world.SetBiome(new GridPosition(x, y), Biome.Grassland);
                }
            }
        }

        world.AddCritter(CritterSpecies.Ape, new GridPosition(4, 1));
        world.AddCritter(CritterSpecies.Deer, new GridPosition(3, 1));
        world.AddCritter(CritterSpecies.Deer, new GridPosition(5, 1));
        world.AddCritter(CritterSpecies.Elk, new GridPosition(4, 0));
        world.AddCritter(CritterSpecies.Elk, new GridPosition(4, 2));
        return world;
    }

    private static void AdvanceUntilVillage(SimulationWorld world)
    {
        for (var tick = 0;
            tick < 4 * 60 * SimulationWorld.TicksPerSecond && world.ApeVillageCount == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }
    }

    private static int CountStructures(SimulationWorld world, ApeStructureKind kind)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetApeStructure(new GridPosition(x, y)) is { } structure && structure == kind)
                {
                    count++;
                }
            }
        }
        return count;
    }

    private static GridPosition FindStructure(SimulationWorld world, ApeStructureKind kind)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (world.GetApeStructure(position) == kind)
                {
                    return position;
                }
            }
        }
        throw new InvalidOperationException($"No {kind} exists.");
    }

    private static void AddPreyToEmptyTiles(SimulationWorld world, int count)
    {
        for (var y = 0; y < world.Height && count > 0; y++)
        {
            for (var x = 0; x < world.Width && count > 0; x++)
            {
                var position = new GridPosition(x, y);
                if (!world.IsOccupied(position) && world.GetApeStructure(position) is null)
                {
                    world.AddCritter(CritterSpecies.Deer, position);
                    count--;
                }
            }
        }
    }

    private static void RemoveAllExcept(SimulationWorld world, CritterSpecies species)
    {
        var positions = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species != species)
            .Select(critter => critter.Position)
            .ToArray();
        foreach (var position in positions)
        {
            Assert.True(world.RemoveCritterAt(position));
        }
    }

    private static void AddAssignedResidents(
        SimulationWorld world,
        GridPosition village,
        int count)
    {
        for (var y = 0; y < world.Height && count > 0; y++)
        {
            for (var x = 0; x < world.Width && count > 0; x++)
            {
                var position = new GridPosition(x, y);
                if (world.IsOccupied(position) || world.GetApeStructure(position) is not null ||
                    !CritterHabitats.CanOccupy(
                        CritterHabitat.LandDweller,
                        world.GetTerrain(position),
                        world.GetSurfaceWater(position),
                        world.GetBiome(position),
                        world.GetSurfaceCover(position)))
                {
                    continue;
                }

                var apeId = world.AddCritter(CritterSpecies.Ape, position);
                Assert.True(world.TryAssignApeToVillage(apeId, village));
                count--;
            }
        }
        Assert.Equal(0, count);
    }

    private static bool HasAdjacentBiome(
        SimulationWorld world,
        GridPosition position,
        Biome biome) => GetCardinalNeighbors(world, position)
        .Any(neighbor => world.GetBiome(neighbor) == biome);

    private static bool HasAdjacentTerrain(
        SimulationWorld world,
        GridPosition position,
        Terrain terrain) => GetCardinalNeighbors(world, position)
        .Any(neighbor => world.GetTerrain(neighbor) == terrain);

    private static IEnumerable<GridPosition> GetCardinalNeighbors(
        SimulationWorld world,
        GridPosition position)
    {
        foreach (var (x, y) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var neighborY = position.Y + y;
            if (neighborY >= 0 && neighborY < world.Height)
            {
                yield return new GridPosition((position.X + x + world.Width) % world.Width, neighborY);
            }
        }
    }

    private static IEnumerable<GridPosition> GetSurroundingNeighbors(
        SimulationWorld world,
        GridPosition position)
    {
        for (var y = -1; y <= 1; y++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if ((x == 0 && y == 0) || position.Y + y < 0 || position.Y + y >= world.Height)
                {
                    continue;
                }
                yield return new GridPosition(
                    (position.X + x + world.Width) % world.Width,
                    position.Y + y);
            }
        }
    }

    private static int WrappedDistance(
        SimulationWorld world,
        GridPosition first,
        GridPosition second)
    {
        var horizontal = Math.Abs(first.X - second.X);
        return Math.Min(horizontal, world.Width - horizontal) + Math.Abs(first.Y - second.Y);
    }

    private static string DescribeWorld(SimulationWorld world) => string.Join(
        ", ",
        Enumerable.Range(0, world.CritterCount).Select(index =>
        {
            var critter = world.GetCritter(index);
            return $"{critter.Species}@{critter.Position}:{critter.Energy}";
        }));
}
