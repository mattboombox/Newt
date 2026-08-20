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
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        world.AdvanceOneTick();

        Assert.Equal(1, CountStructures(world, ApeStructureKind.Farm));
        Assert.Equal(5, world.GetApeVillagePopulationCapacity(village));

        for (var tick = 0; tick < 71 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

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
    public void ResidentApeCarriesPreyFoodBackToVillage()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var ape = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Ape);
        var preyPosition = GetCardinalNeighbors(world, ape.Position)
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        world.AddCritter(CritterSpecies.Deer, preyPosition);

        for (var tick = 0;
            tick < 30 * SimulationWorld.TicksPerSecond && world.GetApeVillageFood(village) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Deer));
        Assert.Equal(4, world.GetApeVillageFood(village));
        Assert.Equal(0, world.GetApeCarriedFood(ape.Id));
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
        Assert.Equal(startingFood + 3, world.GetApeVillageFood(village));
        var sailor = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.ApeSailor);
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

    private static string DescribeWorld(SimulationWorld world) => string.Join(
        ", ",
        Enumerable.Range(0, world.CritterCount).Select(index =>
        {
            var critter = world.GetCritter(index);
            return $"{critter.Species}@{critter.Position}:{critter.Energy}";
        }));
}
