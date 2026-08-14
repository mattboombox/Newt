using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SimulationWorldTests
{
    [Fact]
    public void CritterIdsRemainStableWhenCompactStorageMovesAnotherCritter()
    {
        var world = new SimulationWorld(3, 1, Terrain.Ocean);
        var removedPosition = new GridPosition(0, 0);
        var survivorPosition = new GridPosition(1, 0);
        var removedId = world.AddCritter(CritterSpecies.Plankton, removedPosition);
        var survivorId = world.AddCritter(CritterSpecies.Worm, survivorPosition);

        Assert.True(world.RemoveCritterAt(removedPosition));

        Assert.False(world.TryGetCritter(removedId, out _));
        Assert.True(world.TryGetCritter(survivorId, out var survivor));
        Assert.Equal(survivorPosition, survivor.Position);
        Assert.Equal(survivorId, world.GetCritter(0).Id);
        Assert.NotEqual(
            survivorId,
            world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 0)));
    }

    [Fact]
    public void PlanktonRecoverySeedsOneDeepOceanCritterAndRestoresExtinction()
    {
        var world = new SimulationWorld(12, 8, Terrain.DeepOcean, seed: 91);

        Assert.True(world.EnablePlanktonRecovery());
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        var first = world.GetCritter(0).Position;
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(first));

        Assert.True(world.RemoveCritterAt(first));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));

        world.AdvanceOneTick();

        Assert.Equal(1, world.CritterCount);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(world.GetCritter(0).Position));
    }

    [Fact]
    public void SameSeedProducesSameMovement()
    {
        var first = CreateOceanWorld(42);
        var second = CreateOceanWorld(42);

        for (var tick = 0; tick < 200; tick++)
        {
            first.AdvanceOneTick();
            second.AdvanceOneTick();
        }

        Assert.Equal(first.GetCritter(0), second.GetCritter(0));
    }

    [Fact]
    public void OccupiedTileRejectsAnotherCritter()
    {
        var world = CreateOceanWorld(1);

        Assert.Throws<InvalidOperationException>(() =>
            world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2)));
    }

    [Fact]
    public void PlanktonUsesItsFiveSecondMovementInterval()
    {
        var world = CreateOceanWorld(7);
        var initialPosition = world.GetCritter(0).Position;

        for (var tick = 0; tick < 99; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(initialPosition, world.GetCritter(0).Position);
        world.AdvanceOneTick();
        Assert.NotEqual(initialPosition, world.GetCritter(0).Position);
    }

    [Fact]
    public void PlanktonStrandedOnLandIsRemovedBeforeItCanAct()
    {
        var world = new SimulationWorld(3, 3, Terrain.Ocean, seed: 13);
        var stranded = new GridPosition(1, 1);
        world.AddCritter(CritterSpecies.Plankton, stranded);
        world.SetTerrain(stranded, Terrain.Plains);

        world.AdvanceOneTick();

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.False(world.IsOccupied(stranded));
    }

    [Fact]
    public void PlanktonRecoveryReplacesStrandedPlanktonInTheSameTick()
    {
        var world = new SimulationWorld(3, 3, Terrain.DeepOcean, seed: 31);
        Assert.True(world.EnablePlanktonRecovery());
        var stranded = world.GetCritter(0).Position;
        world.SetTerrain(stranded, Terrain.Plains);

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(
            Terrain.DeepOcean,
            world.GetTerrain(world.GetCritter(0).Position));
    }

    [Fact]
    public void DisablingLifeRemovesCrittersAndStopsPlanktonRecovery()
    {
        var world = new SimulationWorld(4, 4, Terrain.DeepOcean, seed: 37);
        Assert.True(world.EnablePlanktonRecovery());
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 0));

        LifeSystem.SetEnabled(world, false);

        Assert.False(world.LifeEnabled);
        Assert.False(world.PlanktonRecoveryEnabled);
        Assert.Equal(0, world.CritterCount);
        Assert.False(world.TryAddCritter(CritterSpecies.Plankton, new GridPosition(1, 1)));

        world.AdvanceOneTick();
        Assert.Equal(0, world.CritterCount);

        LifeSystem.SetEnabled(world, true);

        Assert.True(world.LifeEnabled);
        Assert.True(world.PlanktonRecoveryEnabled);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Fact]
    public void PlanktonConvertsFourEnergyIntoAnAdjacentOffspring()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 17);
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(0, 0));

        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond - 1; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.CritterCount);
        world.AdvanceOneTick();

        Assert.Equal(2, world.CritterCount);
        var parent = world.GetCritter(0);
        var offspring = world.GetCritter(1);
        Assert.Equal(1, parent.Energy);
        Assert.Equal(1, offspring.Energy);
        Assert.Equal(
            1,
            Math.Abs(parent.Position.X - offspring.Position.X) +
            Math.Abs(parent.Position.Y - offspring.Position.Y));
    }

    [Fact]
    public void CrowdedPlanktonKeepsItsReproductionEnergyUntilSpaceOpens()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean, seed: 23);
        var center = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Plankton, center);

        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var crowded = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Position == center);
        Assert.Equal(4, crowded.Energy);
        Assert.True(crowded.CanReproduce);
    }

    [Fact]
    public void EvolutionTreeAndHalfPercentReproductionChanceAreApplied()
    {
        Assert.Equal(
            CritterSpecies.Jellyfish,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Plankton,
                roll: 9,
                evolutionChanceSteps: 10));
        Assert.Equal(
            CritterSpecies.Worm,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Plankton,
                roll: 9,
                evolutionChanceSteps: 10,
                branchIndex: 1));
        Assert.Equal(
            CritterSpecies.Fish,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Worm,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.Equal(
            CritterSpecies.Newt,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Fish,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Newt,
            out var newtAncestor));
        Assert.Equal(CritterSpecies.Fish, newtAncestor);
        Assert.Equal(
            CritterSpecies.MegaToad,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Newt,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.MegaToad,
            out var megaToadAncestor));
        Assert.Equal(CritterSpecies.Newt, megaToadAncestor);
        Assert.Equal(
            CritterSpecies.Plankton,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Plankton,
                roll: 10,
                evolutionChanceSteps: 10));
        Assert.Equal(
            CritterSpecies.Jellyfish,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Jellyfish,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
    }

    [Fact]
    public void EvolutionChanceAdjustsFromZeroToOneHundredPercentInHalfPercentSteps()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean);

        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        Assert.Equal(0, world.EvolutionChancePercent);
        world.AdjustEvolutionChance(1);
        Assert.Equal(0.5f, world.EvolutionChancePercent);
        world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
        Assert.Equal(100, world.EvolutionChancePercent);
    }

    [Fact]
    public void ManualEvolutionMovesUpAndDownTheTreeWithoutMovingTheCritter()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean);
        var position = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Plankton, position);

        Assert.True(world.TryEvolveCritterAt(position));
        Assert.Contains(
            world.GetCritter(0).Species,
            new[] { CritterSpecies.Jellyfish, CritterSpecies.Worm });
        Assert.Equal(position, world.GetCritter(0).Position);

        Assert.True(world.TryDevolveCritterAt(position));
        Assert.Equal(CritterSpecies.Plankton, world.GetCritter(0).Species);
        Assert.False(world.TryDevolveCritterAt(position));
    }

    [Fact]
    public void WormSmellsAndMovesIntoAdjacentShallows()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 31);
        var start = new GridPosition(0, 0);
        var shallows = new GridPosition(1, 0);
        world.SetTerrain(shallows, Terrain.Shallows);
        world.AddCritter(CritterSpecies.Worm, start);

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(shallows, world.GetCritter(0).Position);
    }

    [Fact]
    public void WormFeedsFromDetritusOnlyInShallows()
    {
        var ocean = new SimulationWorld(1, 1, Terrain.Ocean, seed: 37);
        var shallows = new SimulationWorld(1, 1, Terrain.Shallows, seed: 37);
        ocean.SeasonsEnabled = false;
        shallows.SeasonsEnabled = false;
        ocean.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));
        shallows.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));

        for (var tick = 0; tick < 22 * SimulationWorld.TicksPerSecond; tick++)
        {
            ocean.AdvanceOneTick();
            shallows.AdvanceOneTick();
        }

        Assert.Equal(2, ocean.GetCritter(0).Energy);
        Assert.Equal(3, shallows.GetCritter(0).Energy);
    }

    [Fact]
    public void JellyfishConsumesWormItBumpsInto()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 41);
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Worm));
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void FishPursuesAndConsumesWormWithinLocalPerceptionRadius()
    {
        var world = new SimulationWorld(1, 3, Terrain.Ocean, seed: 43);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 2));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Worm));
        Assert.Equal(new GridPosition(0, 2), world.GetCritter(0).Position);
        Assert.Equal(5, world.GetCritter(0).Energy);
    }

    [Fact]
    public void NewtSearchesOnceAndCrossesOceanToReachFreshwater()
    {
        var world = new SimulationWorld(7, 1, Terrain.Ocean, seed: 53);
        world.SeasonsEnabled = false;
        var start = new GridPosition(1, 0);
        var freshwater = new GridPosition(4, 0);
        world.SetTerrain(freshwater, Terrain.Plains);
        world.SetSurfaceWater(freshwater, SurfaceWaterKind.FreshwaterLake);
        world.AddCritter(CritterSpecies.Newt, start);

        for (var tick = 0; tick < 15 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(freshwater, world.GetCritter(0).Position);
        Assert.Equal(1, world.NewtNavigationBuildCount);
    }

    [Fact]
    public void NewtsShareFreshwaterNavigationField()
    {
        var world = new SimulationWorld(7, 3, Terrain.Ocean, seed: 59);
        world.SeasonsEnabled = false;
        var freshwater = new GridPosition(4, 1);
        world.SetTerrain(freshwater, Terrain.Plains);
        world.SetSurfaceWater(freshwater, SurfaceWaterKind.River);
        world.AddCritter(CritterSpecies.Newt, new GridPosition(1, 0));
        world.AddCritter(CritterSpecies.Newt, new GridPosition(1, 2));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.NewtNavigationBuildCount);
    }

    [Fact]
    public void NewtDoesNotRepeatFailedFreshwaterSearch()
    {
        var world = new SimulationWorld(3, 1, Terrain.Ocean, seed: 61);
        world.SeasonsEnabled = false;
        var start = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Newt, start);

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var freshwater = new GridPosition(2, 0);
        world.SetTerrain(freshwater, Terrain.Plains);
        world.SetSurfaceWater(freshwater, SurfaceWaterKind.River);
        for (var tick = 0; tick < 10 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.NewtNavigationBuildCount);
    }

    [Fact]
    public void NewtFeedsOnlyInRiversAndUnfrozenLakes()
    {
        var dry = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var river = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var lake = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var frozenLake = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        dry.SeasonsEnabled = false;
        river.SeasonsEnabled = false;
        lake.SeasonsEnabled = false;
        frozenLake.SeasonsEnabled = false;
        river.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.River);
        lake.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);
        frozenLake.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);
        frozenLake.SetBiome(new GridPosition(0, 0), Biome.Arctic);
        dry.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        river.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        lake.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        frozenLake.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));

        for (var tick = 0; tick < 18 * SimulationWorld.TicksPerSecond; tick++)
        {
            dry.AdvanceOneTick();
            river.AdvanceOneTick();
            lake.AdvanceOneTick();
            frozenLake.AdvanceOneTick();
        }

        Assert.Equal(4, dry.GetCritter(0).Energy);
        Assert.Equal(5, river.GetCritter(0).Energy);
        Assert.Equal(5, lake.GetCritter(0).Energy);
        Assert.Equal(4, frozenLake.GetCritter(0).Energy);
    }

    [Fact]
    public void NewtFeedsFromAdjacentShoreButNotAdjacentFrozenLake()
    {
        var river = CreateBlockedNewtShore(SurfaceWaterKind.River, frozen: false);
        var lake = CreateBlockedNewtShore(SurfaceWaterKind.FreshwaterLake, frozen: false);
        var frozenLake = CreateBlockedNewtShore(SurfaceWaterKind.FreshwaterLake, frozen: true);

        for (var tick = 0; tick < 18 * SimulationWorld.TicksPerSecond; tick++)
        {
            river.AdvanceOneTick();
            lake.AdvanceOneTick();
            frozenLake.AdvanceOneTick();
        }

        Assert.Equal(5, river.GetCritter(0).Energy);
        Assert.Equal(5, lake.GetCritter(0).Energy);
        Assert.Equal(4, frozenLake.GetCritter(0).Energy);
    }

    [Fact]
    public void HungryNewtWaitsAtShoreUntilItsFeedingInterval()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 70);
        world.SeasonsEnabled = false;
        var shore = new GridPosition(0, 0);
        var river = new GridPosition(1, 0);
        world.SetTerrain(river, Terrain.Shallows);
        world.AddCritter(CritterSpecies.Newt, shore);

        for (var tick = 0; tick < 75 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(3, world.GetCritter(0).Energy);

        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        for (var tick = 0; tick < 15 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(shore, world.GetCritter(0).Position);
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void HungryNewtLocallyDetectsAndApproachesFreshwater()
    {
        var world = new SimulationWorld(17, 1, Terrain.Plains, seed: 72);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));

        for (var tick = 0; tick < 75 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var before = world.GetCritter(0).Position;
        var river = new GridPosition((before.X + 4) % world.Width, 0);
        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var after = world.GetCritter(0).Position;
        var beforeHorizontal = Math.Abs(before.X - river.X);
        var afterHorizontal = Math.Abs(after.X - river.X);
        var beforeDistance = Math.Min(beforeHorizontal, world.Width - beforeHorizontal);
        var afterDistance = Math.Min(afterHorizontal, world.Width - afterHorizontal);
        Assert.True(afterDistance < beforeDistance);
    }

    [Fact]
    public void MegaToadLivesOnLandShallowsAndLakesButNotOpenOcean()
    {
        var land = new SimulationWorld(1, 1, Terrain.Plains);
        var shallows = new SimulationWorld(1, 1, Terrain.Shallows);
        var lake = new SimulationWorld(1, 1, Terrain.Plains);
        var ocean = new SimulationWorld(1, 1, Terrain.Ocean);
        lake.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);

        Assert.True(land.TryAddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0)));
        Assert.True(shallows.TryAddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0)));
        Assert.True(lake.TryAddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0)));
        Assert.False(ocean.TryAddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0)));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt)]
    [InlineData(CritterSpecies.MegaToad)]
    public void FreshwaterLetsAmphibiansEnterMountains(CritterSpecies species)
    {
        var world = new SimulationWorld(3, 1, Terrain.Mountain);
        var dryMountain = new GridPosition(0, 0);
        var riverMountain = new GridPosition(1, 0);
        var snowyLakeMountain = new GridPosition(2, 0);
        world.SetSurfaceWater(riverMountain, SurfaceWaterKind.River);
        world.SetSurfaceWater(snowyLakeMountain, SurfaceWaterKind.FreshwaterLake);
        world.SetBiome(snowyLakeMountain, Biome.Arctic);

        Assert.False(world.TryAddCritter(species, dryMountain));
        Assert.True(world.TryAddCritter(species, riverMountain));
        Assert.True(world.TryAddCritter(species, snowyLakeMountain));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt, SurfaceWaterKind.River, 5)]
    [InlineData(CritterSpecies.MegaToad, SurfaceWaterKind.FreshwaterLake, 6)]
    public void AmphibiansMoveOntoFreshwaterMountainTiles(
        CritterSpecies species,
        SurfaceWaterKind water,
        int movementSeconds)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 73);
        world.SeasonsEnabled = false;
        var start = new GridPosition(0, 0);
        var mountainWater = new GridPosition(1, 0);
        world.SetTerrain(mountainWater, Terrain.Mountain);
        world.SetSurfaceWater(mountainWater, water);
        world.AddCritter(species, start);

        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(mountainWater, world.GetCritter(0).Position);
    }

    [Fact]
    public void MegaToadLocallyHuntsFishInShallows()
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 71);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(6, world.GetCritter(0).Energy);
    }

    private static SimulationWorld CreateBlockedNewtShore(
        SurfaceWaterKind water,
        bool frozen)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 69);
        world.SeasonsEnabled = false;
        var shore = new GridPosition(0, 0);
        var waterTile = new GridPosition(1, 0);
        world.SetTerrain(waterTile, Terrain.Shallows);
        world.SetSurfaceWater(waterTile, water);
        if (frozen)
        {
            world.SetBiome(waterTile, Biome.Arctic);
        }
        world.AddCritter(CritterSpecies.Newt, shore);
        world.AddCritter(CritterSpecies.Jellyfish, waterTile);
        return world;
    }

    [Fact]
    public void JellyfishConsumesPlanktonItBumpsInto()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 47);
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Jellyfish));
        var jellyfish = world.GetCritter(0);
        Assert.Equal(new GridPosition(0, 1), jellyfish.Position);
        Assert.Equal(3, jellyfish.Energy);
    }

    [Theory]
    [InlineData(Terrain.DeepOcean)]
    [InlineData(Terrain.Ocean)]
    [InlineData(Terrain.Shallows)]
    public void OceanDwellerPresetAllowsSaltwaterTerrain(Terrain terrain)
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.OceanDweller,
            terrain,
            SurfaceWaterKind.None));
    }

    [Theory]
    [InlineData(Terrain.Shallows)]
    [InlineData(Terrain.Trench)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Plains)]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Ice)]
    public void LandDwellerPresetAllowsConfiguredTerrainAndRivers(Terrain terrain)
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.None));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.River));
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.LandDweller,
            terrain,
            SurfaceWaterKind.FreshwaterLake));
    }

    [Fact]
    public void FreshwaterDwellerPresetRequiresRiverOrLake()
    {
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.None));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.River));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.FreshwaterDweller,
            Terrain.Plains,
            SurfaceWaterKind.FreshwaterLake));
    }

    [Theory]
    [InlineData(CritterHabitat.OceanDweller, Terrain.Ocean)]
    [InlineData(CritterHabitat.LandDweller, Terrain.Plains)]
    [InlineData(CritterHabitat.FreshwaterDweller, Terrain.Plains)]
    public void LavaBlocksAllNonFlierHabitats(CritterHabitat habitat, Terrain terrain)
    {
        var water = habitat is CritterHabitat.FreshwaterDweller
            ? SurfaceWaterKind.River
            : SurfaceWaterKind.None;

        Assert.False(CritterHabitats.CanOccupy(
            habitat,
            terrain,
            water,
            Biome.None,
            SurfaceCover.Lava));
    }

    [Fact]
    public void FliersIgnoreLavaAndAllTerrainExceptSnowyMountains()
    {
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Trench,
            SurfaceWaterKind.FreshwaterLake,
            Biome.None,
            SurfaceCover.Lava));
        Assert.True(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Mountain,
            SurfaceWaterKind.None,
            Biome.Desert,
            SurfaceCover.Lava));
        Assert.False(CritterHabitats.CanOccupy(
            CritterHabitat.Flier,
            Terrain.Mountain,
            SurfaceWaterKind.None,
            Biome.Arctic,
            SurfaceCover.None));
    }

    private static SimulationWorld CreateOceanWorld(ulong seed)
    {
        var world = new SimulationWorld(8, 8, Terrain.Ocean, seed);
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 2));
        return world;
    }
}
