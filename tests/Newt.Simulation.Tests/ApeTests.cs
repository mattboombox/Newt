using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ApeTests
{
    [Fact]
    public void SickApeCountIncludesBothStrainsAndSailorsButNotHealthyImmuneOrUndeadApes()
    {
        var world = CreatePlagueWorld(5, 1, Terrain.Ice);
        var ape = new GridPosition(0, 0);
        var sailor = new GridPosition(1, 0);
        var healthy = new GridPosition(2, 0);
        var immune = new GridPosition(4, 0);
        world.AddCritter(CritterSpecies.Ape, ape);
        world.AddCritter(CritterSpecies.ApeSailor, sailor);
        world.AddCritter(CritterSpecies.Ape, healthy);
        world.AddCritter(CritterSpecies.UndeadApe, new GridPosition(3, 0));
        world.AddCritter(CritterSpecies.Ape, immune);
        Assert.Equal(0, world.SickApeCount);

        Assert.True(world.TryInfectApeAt(ape, PlagueKind.Plague));
        Assert.True(world.TryInfectApeAt(sailor, PlagueKind.Zombie));
        Assert.False(world.TryInfectApeAt(immune, PlagueKind.Plague));
        Assert.Equal(2, world.SickApeCount);
        Assert.Equal(5, world.CritterCount);

        Assert.True(world.TryInfectApeAt(ape, PlagueKind.Zombie));
        Assert.False(world.TryInfectApeAt(sailor, PlagueKind.Zombie));
        Assert.Equal(2, world.SickApeCount);
        Assert.True(world.RemoveCritterAt(healthy));
        Assert.Equal(2, world.SickApeCount);
        Assert.True(world.RemoveCritterAt(ape));
        Assert.Equal(1, world.SickApeCount);
        Assert.True(world.RemoveCritterAt(sailor));
        Assert.Equal(0, world.SickApeCount);
    }

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    public void PlagueSpreadsOneRingPerSecondIncludingDiagonalsAndHorizontalWrap(PlagueKind kind)
    {
        var world = CreatePlagueWorld(6, 3);
        foreach (var position in AllPositions(world))
        {
            world.AddCritter(CritterSpecies.Ape, position);
        }
        Assert.True(world.TryInfectApeAt(new GridPosition(0, 0), kind));
        AdvancePlagueTicks(world, SimulationWorld.PlagueSpreadIntervalTicks);

        Assert.Equal(kind, CritterAt(world, new GridPosition(1, 0)).Plague);
        Assert.Equal(kind, CritterAt(world, new GridPosition(1, 1)).Plague);
        Assert.Equal(kind, CritterAt(world, new GridPosition(5, 0)).Plague);
        Assert.Equal(kind, CritterAt(world, new GridPosition(5, 1)).Plague);
        Assert.Equal(PlagueKind.None, CritterAt(world, new GridPosition(2, 0)).Plague);
        Assert.Equal(PlagueKind.None, CritterAt(world, new GridPosition(0, 2)).Plague);
        Assert.True(CritterAt(world, new GridPosition(4, 0)).IsPlagueImmune);
        Assert.False(world.TryInfectApeAt(new GridPosition(4, 0), kind));
        Assert.Equal(PlagueKind.None, CritterAt(world, new GridPosition(4, 0)).Plague);
        Assert.Equal(PlagueKind.None, CritterAt(world, new GridPosition(4, 1)).Plague);

        AdvancePlagueTicks(world, SimulationWorld.PlagueSpreadIntervalTicks);
        Assert.Equal(kind, CritterAt(world, new GridPosition(2, 0)).Plague);
        Assert.Equal(kind, CritterAt(world, new GridPosition(4, 1)).Plague);
        Assert.Equal(PlagueKind.None, CritterAt(world, new GridPosition(4, 0)).Plague);
    }

    [Theory]
    [InlineData(PlagueKind.Plague, CritterSpecies.Ape)]
    [InlineData(PlagueKind.Zombie, CritterSpecies.Ape)]
    [InlineData(PlagueKind.Plague, CritterSpecies.ApeSailor)]
    [InlineData(PlagueKind.Zombie, CritterSpecies.ApeSailor)]
    public void PlagueDrainsGraduallyAndOnlyZombieVictimsRise(PlagueKind kind, CritterSpecies species)
    {
        var world = CreatePlagueWorld(1, 1, Terrain.Ice);
        var position = new GridPosition(0, 0);
        var id = world.AddCritter(species, position);
        Assert.True(world.TryInfectApeAt(position, kind));
        Assert.Equal(1, world.SickApeCount);
        AdvancePlagueTicks(world, SimulationWorld.PlagueDrainIntervalTicks - 1);
        Assert.Equal(6, world.GetCritter(0).Energy);
        world.AdvanceOneTick();
        Assert.Equal(5, world.GetCritter(0).Energy);
        AdvancePlagueTicks(world, 5 * SimulationWorld.PlagueDrainIntervalTicks);
        Assert.Equal(0, world.SickApeCount);

        if (kind is PlagueKind.Plague)
        {
            Assert.False(world.TryGetCritter(id, out _));
            Assert.Equal(0, world.CritterCount);
        }
        else
        {
            Assert.True(world.TryGetCritter(id, out var undead));
            Assert.Equal(CritterSpecies.UndeadApe, undead.Species);
            Assert.Equal(position, undead.Position);
            Assert.Equal(6, undead.Energy);
            Assert.False(undead.CanReproduce);
            Assert.Equal(0, world.GetCritterCount(CritterSpecies.Ape));
            Assert.Equal(1, world.GetCritterCount(CritterSpecies.UndeadApe));
            AdvancePlagueTicks(world, SimulationWorld.PlagueDrainIntervalTicks);
            Assert.Equal(6, world.GetCritter(0).Energy);
        }
    }

    [Fact]
    public void PlagueRejectsOtherSpeciesAndInvalidTargets()
    {
        var world = CreatePlagueWorld(3, 1);
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.UndeadApe, new GridPosition(1, 0));
        Assert.False(world.TryInfectApeAt(new GridPosition(0, 0), PlagueKind.Plague));
        Assert.False(world.TryInfectApeAt(new GridPosition(1, 0), PlagueKind.Zombie));
        Assert.False(world.TryInfectApeAt(new GridPosition(2, 0), PlagueKind.Plague));
        Assert.False(world.TryInfectApeAt(new GridPosition(-1, 0), PlagueKind.Plague));
        Assert.False(world.TryInfectApeAt(new GridPosition(0, 0), PlagueKind.None));
    }

    [Fact]
    public void InfectionSurvivesCompactionAndUpgradingDoesNotDelayDrain()
    {
        var world = CreatePlagueWorld(2, 1);
        var first = new GridPosition(0, 0);
        var second = new GridPosition(1, 0);
        world.AddCritter(CritterSpecies.Ape, first);
        var id = world.AddCritter(CritterSpecies.Ape, second);
        Assert.True(world.TryInfectApeAt(second, PlagueKind.Plague));
        AdvancePlagueTicks(world, SimulationWorld.PlagueDrainIntervalTicks - 1);
        Assert.True(world.RemoveCritterAt(first));
        Assert.True(world.TryInfectApeAt(second, PlagueKind.Zombie));
        Assert.False(world.TryInfectApeAt(second, PlagueKind.Plague));
        Assert.False(world.TryInfectApeAt(second, PlagueKind.Zombie));
        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var infected));
        Assert.Equal(PlagueKind.Zombie, infected.Plague);
        Assert.Equal(5, infected.Energy);
        Assert.True(world.RemoveCritterAt(infected.Position));
        world.AddCritter(CritterSpecies.Ape, infected.Position);
        Assert.Equal(PlagueKind.None, world.GetCritter(0).Plague);
    }

    [Fact]
    public void UndeadHuntOnlyLivingApesAndKeepHuntingWhenFullWithoutReproducing()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(species is CritterSpecies.Ape or CritterSpecies.ApeSailor,
                SimulationWorld.CanEat(CritterSpecies.UndeadApe, species));
        }
        var world = CreatePlagueWorld(2, 1, Terrain.Ice);
        var undeadId = world.AddCritter(CritterSpecies.UndeadApe, new GridPosition(0, 0));
        for (var meal = 0; meal < 3; meal++)
        {
            var empty = AllPositions(world).Single(position => !world.IsOccupied(position));
            CritterId preyId;
            do
            {
                preyId = world.AddCritter(CritterSpecies.ApeSailor, empty);
                if (preyId.Value % 5 != 0)
                {
                    world.RemoveCritterAt(empty);
                }
            } while (preyId.Value % 5 != 0);

            AdvancePlagueTicks(world, 3 * SimulationWorld.TicksPerSecond);
            Assert.False(world.TryGetCritter(preyId, out _));
            Assert.True(world.TryGetCritter(undeadId, out var undead));
            Assert.False(undead.CanReproduce);
            Assert.Equal(1, world.CritterCount);
        }
        Assert.Equal(14, world.GetCritter(0).Energy);
        AdvancePlagueTicks(world, 30 * SimulationWorld.TicksPerSecond);
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(0, world.ApeVillageCount);
    }

    [Fact]
    public void UndeadInfectAndReanimateSailorsKilledBeforePlagueDrainsTheirEnergy()
    {
        var world = CreatePlagueWorld(2, 1, Terrain.Ice);
        world.AddCritter(CritterSpecies.UndeadApe, new GridPosition(0, 0));
        var sailorId = world.AddCritter(CritterSpecies.ApeSailor, new GridPosition(1, 0));
        AdvancePlagueTicks(world, 3 * SimulationWorld.TicksPerSecond);
        Assert.True(world.TryGetCritter(sailorId, out var risen));
        Assert.Equal(CritterSpecies.UndeadApe, risen.Species);
        Assert.Equal(2, world.GetCritterCount(CritterSpecies.UndeadApe));
        Assert.NotEqual(world.GetCritter(0).Position, world.GetCritter(1).Position);
    }

    [Fact]
    public void ReanimatedResidentsLeaveTheirVillage()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var village = FindStructure(world, ApeStructureKind.Village);
        var ape = world.GetCritter(0);
        Assert.True(world.TryInfectApeAt(ape.Position, PlagueKind.Zombie));
        foreach (var position in AllPositions(world))
        {
            world.SetBiome(position, Biome.Desert);
        }
        for (var tick = 0; tick < 5 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            if (world.TryGetCritter(ape.Id, out var current) && current.Species is CritterSpecies.UndeadApe)
            {
                Assert.Equal(0, world.GetApeVillageResidentCount(village));
                Assert.False(world.TryAssignApeToVillage(ape.Id, village));
                return;
            }
        }
        Assert.Fail("The infected village resident never reanimated.");
    }

    [Fact]
    public void ZombieOutbreakIsDeterministicAndPreservesUniqueOccupancy()
    {
        var first = CreatePlagueWorld(8, 4);
        var second = CreatePlagueWorld(8, 4);
        foreach (var world in new[] { first, second })
        {
            foreach (var position in AllPositions(world))
            {
                world.AddCritter(CritterSpecies.Ape, position);
            }
            world.TryInfectApeAt(new GridPosition(0, 0), PlagueKind.Zombie);
        }
        for (var tick = 0; tick < 100 * SimulationWorld.TicksPerSecond; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
            Assert.Equal(first.CritterCount, second.CritterCount);
            for (var index = 0; index < first.CritterCount; index++)
            {
                var critter = first.GetCritter(index);
                Assert.Equal(critter, second.GetCritter(index));
                Assert.Equal(critter, CritterAt(first, critter.Position));
            }
        }
        Assert.True(first.GetCritterCount(CritterSpecies.UndeadApe) > 0);
    }

    [Theory]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.ApeSailor)]
    public void SpontaneousPlagueRequiresAtLeastTwoHundredAssignedResidents(CritterSpecies extraSpecies)
    {
        var (world, village) = CreateVillageForPlague(199);
        var villageTile = village.Y * world.Width + village.X;
        // A large world population must not substitute for this village's residents.
        var unassignedPosition = AllPositions(world).First(position =>
            !world.IsOccupied(position) && world.GetApeStructure(position) is null);
        world.SetTerrain(unassignedPosition, Terrain.Beach);
        var extraApe = world.AddCritter(extraSpecies, unassignedPosition);
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            Assert.False(world.TryStartVillagePlague(villageTile));
        }
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            critter => Assert.Equal(PlagueKind.None, critter.Plague));

        Assert.True(world.TryAssignApeToVillage(extraApe, village));
        Assert.Equal(200, world.GetApeVillageResidentCount(village));
        var missedRolls = 0;
        while (missedRolls < 1000 && !world.TryStartVillagePlague(villageTile))
        {
            missedRolls++;
        }

        Assert.InRange(missedRolls, 1, 999);
        var infected = Assert.Single(Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter), critter => critter.Plague is not PlagueKind.None);
        Assert.Equal(PlagueKind.Plague, infected.Plague);
        Assert.False(infected.IsPlagueImmune);
    }

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    public void VillageWithActiveInfectionDoesNotSeedAnotherOutbreak(PlagueKind kind)
    {
        var (world, village) = CreateVillageForPlague(201);
        var target = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .First(critter => !critter.IsPlagueImmune);
        Assert.True(world.TryInfectApeAt(target.Position, kind));

        for (var attempt = 0; attempt < 1000; attempt++)
        {
            Assert.False(world.TryStartVillagePlague(village.Y * world.Width + village.X));
        }

        var infected = Assert.Single(Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter), critter => critter.Plague is not PlagueKind.None);
        Assert.Equal(kind, infected.Plague);
        Assert.Equal(target.Id, infected.Id);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void DisablingNaturalEventsPreventsSpontaneousVillagePlague(int population)
    {
        var (world, village) = CreateVillageForPlague(population);
        NaturalEvents.SetEnabled(world, false);
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            Assert.False(world.TryStartVillagePlague(village.Y * world.Width + village.X));
        }
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            critter => Assert.Equal(PlagueKind.None, critter.Plague));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void VillagePlagueRollsOnlyAtMinuteBoundaries(int population)
    {
        var (world, village) = CreateVillageForPlague(population);
        Assert.NotEqual(0, world.Tick % SimulationWorld.VillagePlagueCheckIntervalTicks);
        for (var attempt = 0; attempt < 1000; attempt++)
        {
            world.AdvanceVillagePlagueOutbreaks();
        }
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            critter => Assert.Equal(PlagueKind.None, critter.Plague));

        NaturalEvents.SetEnabled(world, false);
        var ticksUntilCheck = SimulationWorld.VillagePlagueCheckIntervalTicks -
            (int)(world.Tick % SimulationWorld.VillagePlagueCheckIntervalTicks);
        AdvancePlagueTicks(world, ticksUntilCheck);
        Assert.Equal(population, world.GetApeVillageResidentCount(village));
        NaturalEvents.SetEnabled(world, true);
        // Exercise the rare roll at a check boundary without simulating hundreds
        // of minutes of unrelated village growth and movement.
        for (var attempt = 0; attempt < 1000 &&
            Enumerable.Range(0, world.CritterCount).All(index =>
                world.GetCritter(index).Plague is PlagueKind.None); attempt++)
        {
            world.AdvanceVillagePlagueOutbreaks();
        }
        Assert.Single(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter),
            critter => critter.Plague is PlagueKind.Plague);
    }

    private static (SimulationWorld World, GridPosition Village) CreateVillageForPlague(int population)
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 50, height: 30);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        // Build housing directly so disease tests do not depend on years of economic growth.
        while (world.GetApeVillagePopulationCapacity(village) < 205)
        {
            Assert.True(world.TryBuildApeStructure(
                village.Y * world.Width + village.X, ApeStructureKind.ResidentialDistrict));
        }
        AddAssignedResidents(world, village, population - world.GetApeVillageResidentCount(village));
        Assert.Equal(population, world.GetApeVillageResidentCount(village));
        NaturalEvents.SetEnabled(world, true);
        return (world, village);
    }
    private static SimulationWorld CreatePlagueWorld(int width, int height, Terrain terrain = Terrain.Plains)
    {
        var world = new SimulationWorld(width, height, terrain, seed: 2101);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        return world;
    }

    private static CritterSnapshot CritterAt(SimulationWorld world, GridPosition position)
    {
        Assert.True(world.TryGetCritterAt(position, out var critter));
        return critter;
    }

    private static void AdvancePlagueTicks(SimulationWorld world, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            world.AdvanceOneTick();
        }
    }

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
    public void ApeHuntsEverythingOutsideItsCivilizationExceptPlanktonAndWorms()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var isApePrey = species is not
                (CritterSpecies.Plankton or CritterSpecies.Worm or
                    CritterSpecies.Ape or CritterSpecies.ApeSailor);
            Assert.Equal(
                isApePrey,
                SimulationWorld.CanEat(CritterSpecies.Ape, species));
        }
    }

    [Theory]
    [InlineData(Biome.Swamp, true)]
    [InlineData(Biome.Jungle, true)]
    [InlineData(Biome.Forest, false)]
    public void ApeFeedsFromWetlandFoliageLikeMonkeyButNotForest(
        Biome biome,
        bool expectedToFeed)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 2101);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var position = new GridPosition(0, 0);
        world.SetBiome(position, biome);
        world.AddCritter(CritterSpecies.Ape, position);

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedToFeed ? 7 : 6, world.GetCritter(0).Energy);
    }

    [Fact]
    public void HungryApeMovesOntoAdjacentWetlandFoliage()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 2102);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var wetland = new GridPosition(1, 0);
        world.SetBiome(new GridPosition(0, 0), Biome.Grassland);
        world.SetBiome(wetland, Biome.Jungle);
        world.AddCritter(CritterSpecies.Ape, new GridPosition(0, 0));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(wetland, world.GetCritter(0).Position);
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
        Assert.Equal(20, world.GetApeVillageWood(village));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
    }

    [Fact]
    public void ApeVillageFoundingTreatsRiversAsNaturalBorders()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in AllPositions(world))
        {
            world.SetSurfaceWater(position, SurfaceWaterKind.River);
        }

        for (var tick = 0; tick < 15 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.ApeVillageCount);

        var drySite = AllPositions(world).First(position => !world.IsOccupied(position));
        var dryDistrict = GetCardinalNeighbors(world, drySite)
            .First(position => !world.IsOccupied(position));
        world.SetSurfaceWater(drySite, SurfaceWaterKind.None);
        world.SetSurfaceWater(dryDistrict, SurfaceWaterKind.None);

        for (var tick = 0;
            tick < 30 * SimulationWorld.TicksPerSecond && world.ApeVillageCount == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.ApeVillageCount);
        Assert.All(
            AllPositions(world).Where(position => world.GetApeStructure(position) is not null),
            position => Assert.NotEqual(SurfaceWaterKind.River, world.GetSurfaceWater(position)));
    }

    [Fact]
    public void VillageAndOwnedDistrictExposeStableWorldLocalIdentity()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);

        Assert.Equal(1, world.GetApeVillageId(village));
        Assert.Equal(village, world.GetApeStructureVillage(village));

        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();
        var farm = FindStructure(world, ApeStructureKind.Farm);

        Assert.Equal(village, world.GetApeStructureVillage(farm));
        Assert.Equal(world.GetApeVillageId(village), world.GetApeVillageId(farm));

        var otherWorld = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(otherWorld);
        Assert.Equal(
            1,
            otherWorld.GetApeVillageId(FindStructure(otherWorld, ApeStructureKind.Village)));
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
        Assert.Contains(GetSurroundingNeighbors(world, village),
            position => world.GetTerrain(position) is Terrain.Beach);
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

    [Fact]
    public void FarmPersistsButStopsProducingOutsideGrasslandSeason()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        var founder = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Ape);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();
        var farm = FindStructure(world, ApeStructureKind.Farm);
        RemoveAllExcept(world, CritterSpecies.Ape);
        Assert.True(world.TryGetCritter(founder.Id, out var founderSnapshot));
        Assert.True(world.RemoveCritterAt(founderSnapshot.Position));

        world.SetBiome(farm, Biome.Forest);
        var startingFood = world.GetApeVillageFood(village);
        for (var tick = 0; tick < 28 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(ApeStructureKind.Farm, world.GetApeStructure(farm));
        Assert.False(world.IsApeStructureOperational(farm));
        Assert.Equal(startingFood, world.GetApeVillageFood(village));

        world.SetBiome(farm, Biome.Grassland);
        for (var tick = 0; tick < 14 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(ApeStructureKind.Farm, world.GetApeStructure(farm));
        Assert.True(world.IsApeStructureOperational(farm));
        Assert.Equal(startingFood + 1, world.GetApeVillageFood(village));
    }

    [Theory]
    [InlineData(Biome.Grassland, ApeStructureKind.Farm)]
    [InlineData(Biome.Arid, ApeStructureKind.Farm)]
    [InlineData(Biome.Swamp, ApeStructureKind.RicePaddy)]
    [InlineData(Biome.Forest, ApeStructureKind.Orchard)]
    public void FiveResidentsCannotBuildFoodDistrictOnBiomeClassifiedBeaches(
        Biome biome,
        ApeStructureKind foodDistrict)
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
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetTerrain(new GridPosition(x, y), Terrain.Beach);
            }
        }
        AddAssignedResidents(world, village, 4);

        world.AdvanceOneTick();

        Assert.Equal(0, CountStructures(world, foodDistrict));
    }

    [Theory]
    [InlineData(Biome.Swamp, ApeStructureKind.RicePaddy, 14)]
    [InlineData(Biome.Forest, ApeStructureKind.Orchard, 14)]
    [InlineData(Biome.Arid, ApeStructureKind.Farm, 28)]
    public void FiveResidentsBuildBiomeFoodDistrictAtItsProductionRate(
        Biome biome,
        ApeStructureKind expectedStructure,
        int productionSeconds)
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
        for (var tick = 0; tick < productionSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(startingFood + 1, world.GetApeVillageFood(village));
    }

    [Theory]
    [InlineData(TemperatureBand.Freezing, 44)]
    [InlineData(TemperatureBand.Cold, 38)]
    [InlineData(TemperatureBand.Temperate, 34)]
    [InlineData(TemperatureBand.Hot, 30)]
    public void AquacultureProductionRateImprovesInWarmerShallows(
        TemperatureBand temperature,
        int expectedSeconds)
    {
        var interval = SimulationWorld.GetApeAquacultureProductionIntervalTicks(temperature);

        Assert.Equal(expectedSeconds * SimulationWorld.TicksPerSecond, interval);
        Assert.True(interval > 28 * SimulationWorld.TicksPerSecond);
    }

    [Theory]
    [InlineData(Terrain.Shallows, SurfaceWaterKind.None)]
    [InlineData(Terrain.Plains, SurfaceWaterKind.FreshwaterLake)]
    public void VillageCanBuildAquacultureInShallowsAndFreshwaterLakes(
        Terrain terrain,
        SurfaceWaterKind water)
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var village = FindStructure(world, ApeStructureKind.Village);
        var site = GetCardinalNeighbors(world, village)
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        world.SetTerrain(site, terrain);
        world.SetSurfaceWater(site, water);

        Assert.True(world.TryBuildApeStructure(
            village.Y * world.Width + village.X,
            ApeStructureKind.Aquaculture));
        Assert.Equal(ApeStructureKind.Aquaculture, world.GetApeStructure(site));
        Assert.True(world.IsApeStructureOperational(site));
        var expectedRate = 60d * SimulationWorld.TicksPerSecond /
            SimulationWorld.GetApeAquacultureProductionIntervalTicks(
                world.GetTemperatureBand(site));
        Assert.Equal(expectedRate, world.GetApeStructureProductionPerMinute(site));

        if (water is SurfaceWaterKind.FreshwaterLake)
        {
            world.SetSurfaceWater(site, SurfaceWaterKind.None);
        }
        else
        {
            world.SetTerrain(site, Terrain.Ocean);
        }

        Assert.Null(world.GetApeStructure(site));
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

    [Theory]
    [InlineData(Biome.Forest, 14)]
    [InlineData(Biome.Swamp, 24)]
    [InlineData(Biome.Grassland, 36)]
    [InlineData(Biome.Arid, 36)]
    public void LumberCampUsesFoodToBootstrapAndProducesWood(
        Biome biome,
        int productionSeconds)
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
        Assert.Equal(20, world.GetApeVillageWood(village));
        Assert.True(world.TryBuildApeStructure(
            village.Y * world.Width + village.X,
            ApeStructureKind.ResidentialDistrict));
        AddAssignedResidents(world, village, 4);
        world.StoreApeVillageFood(village, 3);

        for (var tick = 0;
            tick < 60 * SimulationWorld.TicksPerSecond &&
                CountStructures(world, ApeStructureKind.LumberCamp) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, CountStructures(world, ApeStructureKind.LumberCamp));
        Assert.Equal(20, world.GetApeVillageWood(village));
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

        for (var tick = 0; tick < productionSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(startingWood + 1, world.GetApeVillageWood(village));
    }

    [Theory]
    [InlineData(Biome.Jungle, 10)]
    [InlineData(Biome.Forest, 14)]
    [InlineData(Biome.Taiga, 18)]
    [InlineData(Biome.Swamp, 24)]
    [InlineData(Biome.Grassland, 36)]
    [InlineData(Biome.Arid, 36)]
    public void LumberProductionRateDependsOnBiome(Biome biome, int expectedSeconds)
    {
        Assert.Equal(
            expectedSeconds * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetApeLumberProductionIntervalTicks(biome));
    }

    [Fact]
    public void AridLumberCampCanBeBuiltAndRemainOnBeach()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var village = FindStructure(world, ApeStructureKind.Village);
        foreach (var position in AllPositions(world)
            .Where(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null))
        {
            world.SetBiome(position, Biome.Tundra);
        }
        var site = GetCardinalNeighbors(world, village)
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        world.SetTerrain(site, Terrain.Beach);
        world.SetBiome(site, Biome.Arid);

        Assert.True(world.TryBuildApeStructure(
            village.Y * world.Width + village.X,
            ApeStructureKind.LumberCamp));
        Assert.Equal(ApeStructureKind.LumberCamp, world.GetApeStructure(site));

        world.RevalidateApeStructures();

        Assert.Equal(ApeStructureKind.LumberCamp, world.GetApeStructure(site));
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
        Assert.Equal(2, SimulationWorld.GetApeStructureWoodCost(ApeStructureKind.Aquaculture));
    }

    [Fact]
    public void ResidentApesHuntOnlyBelowTenStoredVillageFood()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        var founder = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Ape);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();

        world.StoreApeVillageFood(village, SimulationWorld.ApeVillageHuntingFoodThreshold - 1);

        Assert.True(world.ShouldApeHunt(founder.Id));
        world.StoreApeVillageFood(village, 1);
        Assert.Equal(
            SimulationWorld.ApeVillageHuntingFoodThreshold,
            world.GetApeVillageFood(village));
        Assert.False(world.ShouldApeHunt(founder.Id));

        var unassignedPosition = Enumerable.Range(0, world.Width * world.Height)
            .Select(tile => new GridPosition(tile % world.Width, tile / world.Width))
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        var unassignedApe = world.AddCritter(CritterSpecies.Ape, unassignedPosition);
        Assert.True(world.ShouldApeHunt(unassignedApe));
    }

    [Fact]
    public void ResidentApeTargetsWormOnlyWhenVillageHasNoStoredFood()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var village = FindStructure(world, ApeStructureKind.Village);
        var apeIndex = Enumerable.Range(0, world.CritterCount)
            .Single(index => world.GetCritter(index).Species is CritterSpecies.Ape);
        var ape = world.GetCritter(apeIndex);
        var wormPosition = GetCardinalNeighbors(world, ape.Position)
            .First(position => !world.IsOccupied(position) &&
                world.GetApeStructure(position) is null);
        world.SetTerrain(wormPosition, Terrain.Beach);
        world.AddCritter(CritterSpecies.Worm, wormPosition);

        Assert.Equal(0, world.GetApeVillageFood(village));
        Assert.Equal(
            wormPosition,
            world.FindHunterPrey(apeIndex, CritterSpecies.Ape, perceptionRadius: 6, reservedPrey: null));

        world.StoreApeVillageFood(village, 1);

        Assert.Null(
            world.FindHunterPrey(apeIndex, CritterSpecies.Ape, perceptionRadius: 6, reservedPrey: null));
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

    [Theory]
    [InlineData(SurfaceWaterKind.River, ApeStructureKind.NavalDistrict)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, ApeStructureKind.Aquaculture)]
    public void FiveResidentsUseRiverForHarborAndLakeForAquaculture(
        SurfaceWaterKind water,
        ApeStructureKind expectedStructure)
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        var harborTile = new GridPosition(0, 1);
        world.SetSurfaceWater(harborTile, water);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);

        for (var tick = 0;
            tick < 45 * SimulationWorld.TicksPerSecond &&
                CountStructures(world, expectedStructure) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        var district = FindStructure(world, expectedStructure);
        Assert.Equal(water, world.GetSurfaceWater(district));
        Assert.Contains(
            GetCardinalNeighbors(world, district),
            neighbor => world.GetSurfaceWater(neighbor) is SurfaceWaterKind.None);
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void ApeSailorsCanEnterFreshwater(SurfaceWaterKind water)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 5201);
        var position = new GridPosition(0, 0);
        world.SetSurfaceWater(position, water);

        Assert.True(world.TryAddCritter(CritterSpecies.ApeSailor, position));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void VillageBuildsAndRecruitsFromFreshwaterHarborWithOnlyDiagonalLandAccess(SurfaceWaterKind water)
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var harbor = GetSurroundingNeighbors(world, village)
            .First(position => WrappedDistance(world, position, village) == 2 &&
                !world.IsOccupied(position) && world.GetApeStructure(position) is null);
        world.SetTerrain(harbor, Terrain.Mountain);
        world.SetSurfaceWater(harbor, water);
        foreach (var neighbor in GetCardinalNeighbors(world, harbor))
        {
            world.SetTerrain(neighbor, Terrain.Mountain);
        }
        AddAssignedResidents(world, village, 4);

        for (var tick = 0; tick < 45 * SimulationWorld.TicksPerSecond &&
            world.GetApeVillageSailorCount(village) == 0; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(ApeStructureKind.NavalDistrict, world.GetApeStructure(harbor));
        Assert.True(world.IsApeStructureOperational(harbor));
        Assert.Equal(village, world.GetApeStructureVillage(harbor));
        Assert.Equal(1, world.GetApeVillageSailorCount(village));
        Assert.True(world.TryGetCritterAt(harbor, out var sailor));
        Assert.Equal(CritterSpecies.ApeSailor, sailor.Species);

        world.RevalidateApeStructures();
        Assert.Equal(ApeStructureKind.NavalDistrict, world.GetApeStructure(harbor));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SailorLeavesBeachThroughDiagonalMountainRiverAndHuntsInLake(bool wraps)
    {
        var world = new SimulationWorld(5, 4, Terrain.Mountain, seed: 5203);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var start = new GridPosition(wraps ? 4 : 0, 0);
        var river = new GridPosition(wraps ? 0 : 1, 1);
        var lake = new GridPosition(wraps ? 1 : 2, 2);
        world.SetTerrain(start, Terrain.Beach);
        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);
        var sailorId = world.AddCritter(CritterSpecies.ApeSailor, start);
        var fishId = world.AddCritter(CritterSpecies.Fish, lake);
        var usedRiver = false;

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond &&
            world.TryGetCritter(fishId, out _); tick++)
        {
            world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(sailorId, out var sailor));
            Assert.Contains(sailor.Position, new[] { start, river, lake });
            usedRiver |= sailor.Position == river;
        }

        Assert.True(usedRiver);
        Assert.False(world.TryGetCritter(fishId, out _));
        Assert.True(world.TryGetCritter(sailorId, out var hunter));
        Assert.Equal(lake, hunter.Position);
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void ApeSailorsMoveAndHuntThroughFreshwater(SurfaceWaterKind water)
    {
        var world = new SimulationWorld(5, 1, Terrain.Plains, seed: 5202);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        foreach (var position in AllPositions(world))
        {
            world.SetSurfaceWater(position, water);
        }
        world.AddCritter(CritterSpecies.ApeSailor, new GridPosition(0, 0));
        Assert.True(world.TrySpawnCritter(CritterSpecies.SquidEgg, new GridPosition(2, 0)));

        for (var tick = 0;
            tick < 8 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.SquidEgg) > 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ApeSailor));
    }

    [Fact]
    public void HarborSurvivesIntermediateClimateCoastlineReclassification()
    {
        var world = CreateFedApeWorld(hasGrassland: false);
        var harborTile = new GridPosition(0, 1);
        world.SetTerrain(harborTile, Terrain.Beach);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        AddAssignedResidents(world, village, 4);
        world.AdvanceOneTick();
        var harbor = FindStructure(world, ApeStructureKind.NavalDistrict);

        world.SetClimateTerrain(harbor, Terrain.Plains);

        Assert.Equal(ApeStructureKind.NavalDistrict, world.GetApeStructure(harbor));
        world.SetClimateTerrain(harbor, Terrain.Beach);
        world.RevalidateApeStructures();
        Assert.Equal(ApeStructureKind.NavalDistrict, world.GetApeStructure(harbor));
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

    [Theory]
    [InlineData(PlagueKind.Plague)]
    [InlineData(PlagueKind.Zombie)]
    public void AssignedSailorHasDoubleEnergyCapButVillageFoodDoesNotPreventPlagueDeath(PlagueKind kind)
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

        var sailor = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.ApeSailor);
        Assert.Equal(28, sailor.MaximumEnergy);
        Assert.True(sailor.Energy <= CritterNutritions.Get(CritterSpecies.Ape).MaximumEnergy);
        Assert.False(sailor.CanReproduce);

        // Leave the sailor assigned to a stocked village, but remove all prey.
        RemoveAllExcept(world, CritterSpecies.ApeSailor);
        world.StoreApeVillageFood(village, world.GetApeVillageFoodCapacity(village));
        AdvancePlagueTicks(world, SimulationWorld.PlagueDrainIntervalTicks);
        Assert.True(world.TryGetCritter(sailor.Id, out var healthy));
        Assert.Equal(sailor.Energy, healthy.Energy);
        Assert.True(world.TryInfectApeAt(healthy.Position, kind));

        for (var tick = 0; tick < sailor.Energy * SimulationWorld.PlagueDrainIntervalTicks; tick++)
        {
            // Even unlimited village supplies must not replace plague losses.
            world.StoreApeVillageFood(village, world.GetApeVillageFoodCapacity(village));
            world.AdvanceOneTick();
            if (!world.TryGetCritter(sailor.Id, out var current))
            {
                Assert.Equal(PlagueKind.Plague, kind);
                return;
            }
            if (current.Species is CritterSpecies.UndeadApe)
            {
                Assert.Equal(PlagueKind.Zombie, kind);
                return;
            }
        }
        Assert.Fail("Village supplies prevented the infected sailor from dying.");
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
    public void AssignedSailorFillsItsLargerReserveByHuntingBeforeCarryingSurplus()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var sea = AllPositions(world)
            .Where(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null)
            .SelectMany(position => GetCardinalNeighbors(world, position)
                .Where(neighbor => !world.IsOccupied(neighbor) && world.GetApeStructure(neighbor) is null)
                .Select(neighbor => new[] { position, neighbor }))
            .First();
        foreach (var position in sea)
        {
            world.SetTerrain(position, Terrain.Ocean);
        }
        var sailorId = world.AddCritter(CritterSpecies.ApeSailor, sea[0]);
        Assert.True(world.TryAssignApeToVillage(sailorId, village));
        RemoveAllExcept(world, CritterSpecies.ApeSailor);

        for (var meal = 0; meal < 6; meal++)
        {
            var empty = sea.Single(position => !world.IsOccupied(position));
            var fishId = world.AddCritter(CritterSpecies.Fish, empty);
            AdvancePlagueTicks(world, 2 * SimulationWorld.TicksPerSecond);
            Assert.False(world.TryGetCritter(fishId, out _));
        }

        Assert.True(world.TryGetCritter(sailorId, out var sailor));
        Assert.Equal(28, sailor.Energy);
        Assert.Equal(2, world.GetApeCarriedFood(sailorId));
        Assert.False(sailor.CanReproduce);
    }

    [Fact]
    public void ApeSailorHuntsSeaLifeExceptPlanktonAndWorms()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var isSeaPrey = species is CritterSpecies.Jellyfish or CritterSpecies.Trilobite or
                CritterSpecies.SeaScorpion or CritterSpecies.Nautilus or
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
    public void EmptyVillageAndConnectedBuildingsBecomeRuinsThatEventuallyDecay()
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
            Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(structureTile));
            Assert.Null(world.GetApeStructureVillage(structureTile));
            Assert.False(world.IsApeStructureOperational(structureTile));
        }

        for (var tick = 0; tick < SimulationWorld.ApeRuinDecayTicks - 1; tick++)
        {
            world.AdvanceOneTick();
        }
        foreach (var structureTile in structureTiles)
        {
            Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(structureTile));
        }

        world.AdvanceOneTick();
        foreach (var structureTile in structureTiles)
        {
            Assert.Null(world.GetApeStructure(structureTile));
        }
    }

    [Fact]
    public void LongUnderusedResidentialDistrictBecomesBuildableRuin()
    {
        var world = CreateFedApeWorld(hasGrassland: true);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        Assert.True(world.TryBuildApeStructure(
            village.Y * world.Width + village.X,
            ApeStructureKind.ResidentialDistrict));
        var district = FindStructure(world, ApeStructureKind.ResidentialDistrict);

        var sailors = AllPositions(world)
            .Where(position => !world.IsOccupied(position) && world.GetApeStructure(position) is null)
            .Take(2)
            .Select(position =>
            {
                world.SetTerrain(position, Terrain.Beach);
                var sailor = world.AddCritter(CritterSpecies.ApeSailor, position);
                Assert.True(world.TryAssignApeToVillage(sailor, village));
                return sailor;
            })
            .ToHashSet();
        Assert.Equal(2, sailors.Count);
        foreach (var critterPosition in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => !sailors.Contains(critter.Id))
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(critterPosition));
        }

        world.AdvanceOneTick();
        for (var tick = 0; tick < SimulationWorld.ApeResidentialUnderuseTicks - 1; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(ApeStructureKind.ResidentialDistrict, world.GetApeStructure(district));

        world.AdvanceOneTick();
        Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(district));
        Assert.Equal(5, world.GetApeVillagePopulationCapacity(village));

        foreach (var position in AllPositions(world).Where(position =>
            position != village && position != district &&
            world.GetApeStructure(position) is null))
        {
            world.SetTerrain(position, Terrain.Mountain);
        }

        Assert.True(world.TryBuildApeStructure(
            village.Y * world.Width + village.X,
            ApeStructureKind.ResidentialDistrict));
        Assert.Equal(ApeStructureKind.ResidentialDistrict, world.GetApeStructure(district));
        Assert.Equal(10, world.GetApeVillagePopulationCapacity(village));
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
        foreach (var resident in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(resident));
        }
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

    [Theory]
    [InlineData(Terrain.DeepOcean, SurfaceCover.None, CritterSpecies.Plankton, true)]
    [InlineData(Terrain.Ocean, SurfaceCover.None, null, true)]
    [InlineData(Terrain.Shallows, SurfaceCover.None, CritterSpecies.Newt, true)]
    [InlineData(Terrain.Ice, SurfaceCover.None, null, true)]
    [InlineData(Terrain.Plains, SurfaceCover.Stone, null, true)]
    [InlineData(Terrain.Mountain, SurfaceCover.None, null, false)]
    public void ColonistCrossesAllowedTerrainAndPushesSmallBlockersButNotMountains(
        Terrain transitTerrain,
        SurfaceCover transitCover,
        CritterSpecies? blockerSpecies,
        bool canTraverse)
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 31, height: 3);
        AdvanceUntilVillage(world);
        Assert.Equal(1, world.ApeVillageCount);
        RemoveAllExcept(world, CritterSpecies.Ape);

        var origin = FindStructure(world, ApeStructureKind.Village);
        var occupied = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Select(critter => critter.Position)
            .ToHashSet();
        foreach (var position in AllPositions(world))
        {
            if (world.GetApeStructure(position) is null && !occupied.Contains(position))
            {
                world.SetTerrain(position, transitTerrain);
                world.SetSurfaceCover(
                    position,
                    transitCover,
                    transitCover is SurfaceCover.None ? 0 : long.MaxValue);
            }
        }

        var destination = new GridPosition(
            (origin.X + world.Width / 2) % world.Width,
            origin.Y);
        var destinationAuxiliary = new GridPosition(
            (destination.X + 1) % world.Width,
            destination.Y);
        foreach (var position in new[] { destination, destinationAuxiliary })
        {
            world.SetTerrain(position, Terrain.Plains);
            world.SetSurfaceCover(position, SurfaceCover.None, 0);
            world.SetBiome(position, Biome.Grassland);
        }

        var spawn = AllPositions(world)
            .Where(position => world.GetApeStructure(position) is not null)
            .SelectMany(position => GetCardinalNeighbors(world, position))
            .First(position => world.GetApeStructure(position) is null &&
                !world.IsOccupied(position) && position != destination &&
                position != destinationAuxiliary);
        world.SetTerrain(spawn, Terrain.Plains);
        world.SetSurfaceCover(spawn, SurfaceCover.None, 0);
        world.SetBiome(spawn, Biome.Grassland);

        if (blockerSpecies is { } blocker)
        {
            foreach (var position in AllPositions(world))
            {
                if (!world.IsOccupied(position) && world.GetApeStructure(position) is null &&
                    position.Y == origin.Y &&
                    world.GetTerrain(position) == transitTerrain &&
                    world.GetSurfaceCover(position) == transitCover)
                {
                    world.AddCritter(blocker, position);
                }
            }
        }

        var existingApeIds = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Ape)
            .Select(critter => critter.Id)
            .ToHashSet();
        Assert.True(world.TrySendApeColonist(origin));
        var colonistId = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Ape &&
                !existingApeIds.Contains(critter.Id))
            .Id;
        Assert.True(world.TryGetCritter(colonistId, out var departingColonist));
        Assert.True(departingColonist.IsColonist);
        Assert.False(departingColonist.CanReproduce);
        Assert.Equal(100, departingColonist.Energy);
        Assert.NotNull(departingColonist.ColonistDestination);

        var enteredTransitTerrain = false;
        for (var tick = 0;
            tick < 90 * SimulationWorld.TicksPerSecond && world.ApeVillageCount < 2;
            tick++)
        {
            world.AdvanceOneTick();
            if (world.TryGetCritter(colonistId, out var colonist) &&
                world.GetTerrain(colonist.Position) == transitTerrain &&
                world.GetSurfaceCover(colonist.Position) == transitCover)
            {
                enteredTransitTerrain = true;
            }
        }

        Assert.Equal(canTraverse, enteredTransitTerrain);
        Assert.Equal(canTraverse ? 2 : 1, world.ApeVillageCount);
        Assert.True(world.TryGetCritter(colonistId, out var founder));
        Assert.Equal(!canTraverse, founder.IsColonist);
    }

    [Fact]
    public void ClickingDestinationSendsColonistThereFromNearestVillage()
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 81, height: 3);
        AdvanceUntilVillage(world);
        Assert.Equal(1, world.ApeVillageCount);
        RemoveAllExcept(world, CritterSpecies.Ape);

        var origin = FindStructure(world, ApeStructureKind.Village);
        var secondVillage = new GridPosition((origin.X + 20) % world.Width, origin.Y);
        Assert.True(world.TrySendApeColonist(secondVillage));
        var firstColonist = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.IsColonist);
        Assert.Equal(secondVillage, firstColonist.ColonistDestination);

        for (var tick = 0;
            tick < 90 * SimulationWorld.TicksPerSecond && world.ApeVillageCount < 2;
            tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(secondVillage));
        Assert.Equal(2, world.GetApeVillageResidentCount(secondVillage));
        Assert.Equal(20, world.GetApeVillageWood(secondVillage));
        Assert.True(world.TryGetCritter(firstColonist.Id, out var founder));
        Assert.False(founder.IsColonist);
        Assert.Equal(CritterNutritions.Get(CritterSpecies.Ape).InitialEnergy, founder.Energy);

        var destination = new GridPosition((origin.X + 40) % world.Width, origin.Y);
        Assert.True(world.TrySendApeColonist(destination));
        var departingColonist = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.IsColonist);

        Assert.Equal(destination, departingColonist.ColonistDestination);
        Assert.True(
            WrappedDistance(world, departingColonist.Position, secondVillage) <
            WrappedDistance(world, departingColonist.Position, origin));
    }

    [Fact]
    public void LoneApeSailorLeavesAsColonistAfterGracePeriod()
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 81, height: 3);
        NaturalEvents.SetEnabled(world, false);
        AdvanceUntilVillage(world);
        var village = FindStructure(world, ApeStructureKind.Village);
        var villageStructures = AllPositions(world)
            .Where(position => world.GetApeStructureVillage(position) == village)
            .ToArray();
        var sailorPosition = AllPositions(world).First(position =>
            !world.IsOccupied(position) && world.GetApeStructure(position) is null);
        world.SetTerrain(sailorPosition, Terrain.Beach);
        var sailorId = world.AddCritter(CritterSpecies.ApeSailor, sailorPosition);
        Assert.True(world.TryAssignApeToVillage(sailorId, village));
        foreach (var critterPosition in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Id != sailorId)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(critterPosition));
        }

        world.AdvanceOneTick();
        for (var tick = 0; tick < SimulationWorld.LoneApeSailorColonistDelayTicks - 1; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.True(world.TryGetCritter(sailorId, out var waitingSailor));
        Assert.Equal(CritterSpecies.ApeSailor, waitingSailor.Species);
        Assert.False(waitingSailor.IsColonist);
        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(village));

        world.AdvanceOneTick();

        Assert.True(world.TryGetCritter(sailorId, out var colonist));
        Assert.Equal(CritterSpecies.Ape, colonist.Species);
        Assert.True(colonist.IsColonist);
        Assert.NotNull(colonist.ColonistDestination);
        Assert.Equal(100, colonist.Energy);
        Assert.Equal(0, world.ApeVillageCount);
        Assert.All(villageStructures, position =>
            Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(position)));
    }

    [Fact]
    public void ColonistRoutesAroundMountainBarrier()
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 81, height: 7);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var origin = FindStructure(world, ApeStructureKind.Village);
        var destination = new GridPosition((origin.X + 40) % world.Width, origin.Y);
        var barrierX = (origin.X + 20) % world.Width;
        for (var y = 1; y < world.Height; y++)
        {
            var barrier = new GridPosition(barrierX, y);
            if (world.GetApeStructure(barrier) is null)
            {
                world.SetTerrain(barrier, Terrain.Mountain);
            }
        }

        Assert.True(world.TrySendApeColonist(destination));
        for (var tick = 0;
            tick < 120 * SimulationWorld.TicksPerSecond && world.ApeVillageCount < 2;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(ApeStructureKind.Village, world.GetApeStructure(destination));
    }

    [Fact]
    public void ClickingDestinationBesideShallowsSendsAquacultureSupportedColonist()
    {
        var world = CreateFedApeWorld(hasGrassland: true, width: 81, height: 3);
        AdvanceUntilVillage(world);
        RemoveAllExcept(world, CritterSpecies.Ape);
        var origin = FindStructure(world, ApeStructureKind.Village);
        var destination = new GridPosition((origin.X + 40) % world.Width, origin.Y);
        world.SetBiome(destination, Biome.None);
        var neighbors = GetCardinalNeighbors(world, destination).ToArray();
        foreach (var neighbor in neighbors)
        {
            world.SetTerrain(neighbor, Terrain.Mountain);
            world.SetBiome(neighbor, Biome.None);
        }
        world.SetTerrain(neighbors[0], Terrain.Shallows);

        Assert.True(world.TrySendApeColonist(destination));
        var colonist = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.IsColonist);
        Assert.Equal(destination, colonist.ColonistDestination);
    }

    private static SimulationWorld CreateFedApeWorld(bool hasGrassland, int width = 9, int height = 3)
    {
        var world = new SimulationWorld(width, height, Terrain.Plains, seed: 1701);
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

    private static IEnumerable<GridPosition> AllPositions(SimulationWorld world)
    {
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                yield return new GridPosition(x, y);
            }
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
