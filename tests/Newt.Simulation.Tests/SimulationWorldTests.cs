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
    public void PlanktonRecoveryWaitsFifteenThousandTicksAfterExtinction()
    {
        var world = new SimulationWorld(12, 8, Terrain.DeepOcean, seed: 91);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);

        Assert.True(world.EnablePlanktonRecovery());
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        var first = world.GetCritter(0).Position;
        Assert.Equal(Terrain.DeepOcean, world.GetTerrain(first));

        Assert.True(world.RemoveCritterAt(first));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));

        for (var tick = 1; tick < SimulationWorld.PlanktonRecoveryIntervalTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.CritterCount);

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
    public void PlanktonUsesItsTenSecondMovementInterval()
    {
        var world = CreateOceanWorld(7);
        var planktonId = world.GetCritter(0).Id;
        var initialPosition = world.GetCritter(0).Position;
        var interval = SimulationWorld.PlanktonMovementIntervalTicks;

        for (var tick = 0;
            tick < interval && world.GetCritter(0).Position == initialPosition;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(planktonId, out var moved));
        Assert.NotEqual(initialPosition, moved.Position);
        var firstDestination = moved.Position;

        for (var tick = 1; tick < interval; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(planktonId, out var waiting));
        Assert.Equal(firstDestination, waiting.Position);
        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(planktonId, out var movedAgain));
        Assert.NotEqual(firstDestination, movedAgain.Position);
    }

    [Fact]
    public void PlanktonAndSquidEggsDriftEveryTenSecondsWhileJellyfishStayFaster()
    {
        Assert.Equal(
            10 * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Plankton));
        Assert.Equal(
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Plankton),
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.SquidEgg));
        Assert.Equal(
            5 * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Jellyfish));
    }

    [Fact]
    public void WolvesMoveEveryThreeSecondsAndPerceivePreyWithinFiveTiles()
    {
        Assert.Equal(
            3 * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Wolf));
        Assert.Equal(5, SimulationWorld.WolfPerceptionRadius);
    }

    [Fact]
    public void TrilobitesMatchNautilusSpeedStomachAndAwareness()
    {
        Assert.Equal(
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Nautilus),
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Trilobite));
        Assert.Equal(
            CritterNutritions.Get(CritterSpecies.Nautilus).MaximumEnergy,
            CritterNutritions.Get(CritterSpecies.Trilobite).MaximumEnergy);
        Assert.Equal(
            SimulationWorld.NautilusPerceptionRadius,
            SimulationWorld.TrilobitePerceptionRadius);
    }

    [Fact]
    public void PlanktonMovementIsStaggeredAcrossItsTenSecondInterval()
    {
        var world = new SimulationWorld(80, 3, Terrain.DeepOcean, seed: 71);
        var startingPositions = new Dictionary<CritterId, GridPosition>();
        for (var plankton = 0; plankton < 8; plankton++)
        {
            var position = new GridPosition(plankton * 10 + 4, 1);
            var id = world.AddCritter(CritterSpecies.Plankton, position);
            startingPositions.Add(id, position);
        }

        var firstMovementTicks = new Dictionary<CritterId, long>();
        for (var tick = 0;
            tick < SimulationWorld.PlanktonMovementIntervalTicks;
            tick++)
        {
            world.AdvanceOneTick();
            foreach (var (id, start) in startingPositions)
            {
                if (!firstMovementTicks.ContainsKey(id) &&
                    world.TryGetCritter(id, out var critter) &&
                    critter.Position != start)
                {
                    firstMovementTicks.Add(id, world.Tick);
                }
            }
        }

        Assert.Equal(startingPositions.Count, firstMovementTicks.Count);
        Assert.True(firstMovementTicks.Values.Distinct().Count() > 1);
    }

    [Fact]
    public void PlanktonFeedsEveryTwentySecondsOnAnIdStaggeredPhase()
    {
        var world = new SimulationWorld(3, 3, Terrain.DeepOcean, seed: 73);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var planktonId = world.AddCritter(
            CritterSpecies.Plankton,
            new GridPosition(1, 1));
        var secondPlanktonId = world.AddCritter(
            CritterSpecies.Plankton,
            new GridPosition(0, 0));

        Assert.Equal(1, planktonId.Value);
        Assert.Equal(2, secondPlanktonId.Value);
        Assert.Equal(1, world.GetCritter(0).Energy);

        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(planktonId, out var firstFeed));
        Assert.Equal(2, firstFeed.Energy);
        Assert.True(world.TryGetCritter(secondPlanktonId, out var stillWaiting));
        Assert.Equal(1, stillWaiting.Energy);

        for (var tick = 1;
            tick < SimulationWorld.PlanktonFeedingIntervalTicks;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(planktonId, out var waiting));
        Assert.Equal(2, waiting.Energy);
        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(planktonId, out var secondFeed));
        Assert.Equal(3, secondFeed.Energy);
    }

    [Fact]
    public void StrandedCritterMovesSlowlyBackTowardValidHabitat()
    {
        var world = new SimulationWorld(5, 1, Terrain.Ocean, seed: 13);
        world.SeasonsEnabled = false;
        var shore = new GridPosition(0, 0);
        var stranded = new GridPosition(2, 0);
        world.SetTerrain(shore, Terrain.Plains);
        world.SetTerrain(stranded, Terrain.Plains);
        world.AddCritter(CritterSpecies.Deer, stranded);
        world.SetTerrain(stranded, Terrain.Ocean);

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(new GridPosition(1, 0), world.GetCritter(0).Position);

        for (var tick = 0; tick < 11 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(new GridPosition(1, 0), world.GetCritter(0).Position);

        for (var tick = 0; tick < SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(shore, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Ape, Terrain.DeepOcean)]
    [InlineData(CritterSpecies.Fish, Terrain.Plains)]
    [InlineData(CritterSpecies.Deer, Terrain.Ocean)]
    [InlineData(CritterSpecies.Plankton, Terrain.Mountain)]
    public void PlayerSpawnBypassesHabitatWithoutWeakeningNormalAddition(
        CritterSpecies species,
        Terrain incompatibleTerrain)
    {
        var world = new SimulationWorld(1, 1, incompatibleTerrain, seed: 130);
        var position = new GridPosition(0, 0);

        Assert.False(world.TryAddCritter(species, position));
        Assert.True(world.TrySpawnCritter(species, position));
        Assert.Equal(species, world.GetCritter(0).Species);
        Assert.False(world.TrySpawnCritter(species, position));
    }

    [Theory]
    [InlineData(CritterSpecies.Plankton)]
    [InlineData(CritterSpecies.Jellyfish)]
    [InlineData(CritterSpecies.ApeSailor)]
    public void AquaticCrittersStrandedOnLandSurviveTheNextTick(
        CritterSpecies species)
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean, seed: 120);
        var stranded = new GridPosition(0, 0);
        world.AddCritter(species, stranded);
        world.SetTerrain(stranded, Terrain.Plains);

        world.AdvanceOneTick();

        Assert.Equal(1, world.CritterCount);
        Assert.True(world.IsOccupied(stranded));
    }

    [Fact]
    public void BarrenLandlockedNewtEventuallyStarves()
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 121);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));

        for (var tick = 0; tick < 5 * 75 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Newt));
        Assert.Equal(0, world.CritterCount);
    }

    [Fact]
    public void PlanktonRecoveryRecognizesAStillLivingStrandedPlankton()
    {
        var world = new SimulationWorld(3, 3, Terrain.DeepOcean, seed: 31);
        Assert.True(world.EnablePlanktonRecovery());
        var stranded = world.GetCritter(0).Position;
        world.SetTerrain(stranded, Terrain.Plains);

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(1, world.CritterCount);
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
        var world = new SimulationWorld(1, 2, Terrain.DeepOcean, seed: 17);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(0, 0));

        for (var tick = 0;
            tick < 90 * SimulationWorld.TicksPerSecond && world.CritterCount == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.CritterCount);
        var parent = world.GetCritter(0);
        var offspring = world.GetCritter(1);
        Assert.Equal(2, parent.Energy);
        Assert.Equal(1, offspring.Energy);
        Assert.Equal(
            1,
            Math.Abs(parent.Position.X - offspring.Position.X) +
            Math.Abs(parent.Position.Y - offspring.Position.Y));
    }

    [Fact]
    public void CrowdedPlanktonKeepsItsReproductionEnergyUntilSpaceOpens()
    {
        var world = new SimulationWorld(1, 1, Terrain.DeepOcean, seed: 23);
        world.SeasonsEnabled = false;
        var center = new GridPosition(0, 0);
        world.AddCritter(CritterSpecies.Plankton, center);

        for (var tick = 0; tick < 61 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var crowded = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Position == center);
        Assert.Equal(4, crowded.Energy);
        Assert.True(crowded.CanReproduce);
        Assert.Equal(1, world.GetTileNutrition(center));
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
            CritterSpecies.Trilobite,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Plankton,
                roll: 9,
                evolutionChanceSteps: 10,
                branchIndex: 2));
        Assert.Equal(
            CritterSpecies.Crab,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Trilobite,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Crab,
            out var crabAncestor));
        Assert.Equal(CritterSpecies.Trilobite, crabAncestor);
        Assert.Equal(
            CritterSpecies.SeaScorpion,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Trilobite,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 1));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.SeaScorpion,
            out var seaScorpionAncestor));
        Assert.Equal(CritterSpecies.Trilobite, seaScorpionAncestor);
        Assert.Equal(
            CritterSpecies.Fish,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Worm,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.Equal(
            CritterSpecies.Nautilus,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Worm,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 1));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Nautilus,
            out var nautilusAncestor));
        Assert.Equal(CritterSpecies.Worm, nautilusAncestor);
        Assert.Equal(
            CritterSpecies.Squid,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Nautilus,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Squid,
            out var squidAncestor));
        Assert.Equal(CritterSpecies.Nautilus, squidAncestor);
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
            CritterSpecies.Therapsid,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Newt,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 1));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Therapsid,
            out var therapsidAncestor));
        Assert.Equal(CritterSpecies.Newt, therapsidAncestor);
        Assert.Equal(
            CritterSpecies.Monkey,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Therapsid,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Monkey,
            out var monkeyAncestor));
        Assert.Equal(CritterSpecies.Therapsid, monkeyAncestor);
        Assert.Equal(
            CritterSpecies.Deer,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Therapsid,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 1));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Deer,
            out var deerAncestor));
        Assert.Equal(CritterSpecies.Therapsid, deerAncestor);
        Assert.Equal(
            CritterSpecies.Elk,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Deer,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Elk,
            out var elkAncestor));
        Assert.Equal(CritterSpecies.Deer, elkAncestor);
        Assert.Equal(
            CritterSpecies.Gazelle,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Deer,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 1));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Gazelle,
            out var gazelleAncestor));
        Assert.Equal(CritterSpecies.Deer, gazelleAncestor);
        Assert.Equal(
            CritterSpecies.Wolf,
            CritterEvolution.ChooseOffspring(
                CritterSpecies.Therapsid,
                roll: 0,
                evolutionChanceSteps: CritterEvolution.MaximumChanceSteps,
                branchIndex: 2));
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(
            CritterSpecies.Wolf,
            out var wolfAncestor));
        Assert.Equal(CritterSpecies.Therapsid, wolfAncestor);
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

        Assert.Equal(0.5f, world.EvolutionChancePercent);
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        Assert.Equal(0, world.EvolutionChancePercent);
        world.AdjustEvolutionChance(1);
        Assert.Equal(0.5f, world.EvolutionChancePercent);
        world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
        Assert.Equal(100, world.EvolutionChancePercent);
    }

    [Fact]
    public void TherapsidOffspringUseDoubleTheWorldEvolutionChance()
    {
        Assert.Equal(
            2,
            CritterEvolution.GetOffspringEvolutionChanceSteps(
                CritterSpecies.Therapsid,
                CritterEvolution.DefaultChanceSteps));
        Assert.Equal(
            CritterEvolution.MaximumChanceSteps,
            CritterEvolution.GetOffspringEvolutionChanceSteps(
                CritterSpecies.Therapsid,
                CritterEvolution.MaximumChanceSteps - 1));
        Assert.Equal(
            CritterEvolution.DefaultChanceSteps,
            CritterEvolution.GetOffspringEvolutionChanceSteps(
                CritterSpecies.Newt,
                CritterEvolution.DefaultChanceSteps));
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
            new[] { CritterSpecies.Jellyfish, CritterSpecies.Worm, CritterSpecies.Trilobite });
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

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond - 1; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(start, world.GetCritter(0).Position);
        world.AdvanceOneTick();
        Assert.Equal(shallows, world.GetCritter(0).Position);
    }

    [Fact]
    public void WormFeedsFromDetritusInDeepOceanAndShallows()
    {
        var deepOcean = new SimulationWorld(1, 1, Terrain.DeepOcean, seed: 37);
        var ocean = new SimulationWorld(1, 1, Terrain.Ocean, seed: 37);
        var shallows = new SimulationWorld(1, 1, Terrain.Shallows, seed: 37);
        deepOcean.SeasonsEnabled = false;
        ocean.SeasonsEnabled = false;
        shallows.SeasonsEnabled = false;
        deepOcean.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));
        ocean.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));
        shallows.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));

        for (var tick = 0; tick < 22 * SimulationWorld.TicksPerSecond; tick++)
        {
            deepOcean.AdvanceOneTick();
            ocean.AdvanceOneTick();
            shallows.AdvanceOneTick();
        }

        Assert.Equal(3, deepOcean.GetCritter(0).Energy);
        Assert.Equal(2, ocean.GetCritter(0).Energy);
        Assert.Equal(4, shallows.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Worm, 8)]
    [InlineData(CritterSpecies.Trilobite, 6)]
    [InlineData(CritterSpecies.Nautilus, 6)]
    public void DeepOceanIceFeedsItsTerrainForagers(
        CritterSpecies species,
        int movementSeconds)
    {
        var world = new SimulationWorld(1, 1, Terrain.Ice, seed: 37);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.SetElevation(position, -0.3f);
        world.AddCritter(species, position);

        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(
            CritterNutritions.Get(species).InitialEnergy + 1,
            world.GetCritter(0).Energy);
        Assert.Equal(0, world.GetTileNutrition(position));
    }

    [Fact]
    public void WormSmellsAndMovesIntoAdjacentDeepOcean()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 38);
        var deepOcean = new GridPosition(1, 0);
        world.SetTerrain(deepOcean, Terrain.DeepOcean);
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(deepOcean, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River, Biome.Grassland, 4)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, Biome.Grassland, 4)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, Biome.Arctic, 2)]
    public void WormTraversesFreshwaterAndFeedsWhereNewtsCan(
        SurfaceWaterKind water,
        Biome biome,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, Terrain.Mountain, seed: 98);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.SetSurfaceWater(position, water);
        world.SetBiome(position, biome);

        Assert.True(world.TryAddCritter(CritterSpecies.Worm, position));

        for (var tick = 0; tick < 22 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void WormSmellsAndMovesIntoAdjacentFreshwater()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 99);
        var freshwater = new GridPosition(1, 0);
        world.SetTerrain(freshwater, Terrain.Plains);
        world.SetSurfaceWater(freshwater, SurfaceWaterKind.River);
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(freshwater, world.GetCritter(0).Position);
    }

    [Fact]
    public void OrdinaryCritterMovementShovesBlockingPlanktonAside()
    {
        var world = new SimulationWorld(4, 1, Terrain.Ocean, seed: 61);
        world.SeasonsEnabled = false;
        var blockedDestination = new GridPosition(1, 0);
        var shoveDestination = new GridPosition(2, 0);
        world.SetTerrain(new GridPosition(3, 0), Terrain.Mountain);
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));

        for (var tick = 0; tick < 30; tick++)
        {
            world.AdvanceOneTick();
        }
        world.AddCritter(CritterSpecies.Plankton, blockedDestination);

        for (var tick = 30; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritterAt(blockedDestination, out var worm));
        Assert.Equal(CritterSpecies.Worm, worm.Species);
        Assert.True(world.TryGetCritterAt(shoveDestination, out var plankton));
        Assert.Equal(CritterSpecies.Plankton, plankton.Species);
    }

    [Fact]
    public void ImmovablePlanktonIsLethallyShovedWithoutBecomingAnIllegalMeal()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 63);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(1, 0));

        for (var tick = 0; tick < 8 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.True(world.TryGetCritterAt(new GridPosition(1, 0), out var worm));
        Assert.Equal(CritterSpecies.Worm, worm.Species);
        Assert.Equal(2, worm.Energy);
    }

    [Fact]
    public void DenseLethalPlanktonShovesKeepEveryCritterIndexedAtItsPosition()
    {
        var world = new SimulationWorld(8, 4, Terrain.Ocean, seed: 630);
        world.SeasonsEnabled = false;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                var species = (x + y) % 4 == 0
                    ? CritterSpecies.Worm
                    : CritterSpecies.Plankton;
                world.AddCritter(species, new GridPosition(x, y));
            }
        }

        for (var tick = 0; tick < 20 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            for (var index = 0; index < world.CritterCount; index++)
            {
                var critter = world.GetCritter(index);
                Assert.True(world.TryGetCritterAt(critter.Position, out var indexed));
                Assert.Equal(critter.Id, indexed.Id);
            }
        }
    }

    [Fact]
    public void SquidEggCannotLethallyShoveImmovablePlankton()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 64);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.SquidEgg, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(1, 0));

        for (var tick = 0; tick < SimulationWorld.PlanktonMovementIntervalTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Theory]
    [InlineData(CritterSpecies.Worm)]
    [InlineData(CritterSpecies.Trilobite)]
    public void WormsAndTrilobitesPushThroughDensePlanktonBlooms(CritterSpecies species)
    {
        var world = new SimulationWorld(6, 1, Terrain.Ocean, seed: 62);
        world.SeasonsEnabled = false;
        world.SetTerrain(new GridPosition(5, 0), Terrain.Mountain);
        world.AddCritter(species, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(1, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(2, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(3, 0));

        var movementSeconds = species is CritterSpecies.Worm ? 8 : 6;
        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritterAt(new GridPosition(1, 0), out var mover));
        Assert.Equal(species, mover.Species);
        Assert.True(world.TryGetCritterAt(new GridPosition(4, 0), out var displacedPlankton));
        Assert.Equal(CritterSpecies.Plankton, displacedPlankton.Species);
        Assert.Equal(3, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Fact]
    public void TrilobiteStaysInShallowsToFeedInsteadOfReturningToDeepWater()
    {
        var world = new SimulationWorld(2, 1, Terrain.Shallows, seed: 39);
        var start = new GridPosition(0, 0);
        var ocean = new GridPosition(1, 0);
        world.SetTerrain(ocean, Terrain.Ocean);
        world.AddCritter(CritterSpecies.Trilobite, start);

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(start, world.GetCritter(0).Position);
        Assert.Equal(3, world.GetCritter(0).Energy);
    }

    [Fact]
    public void TrilobiteFleesVisibleSquid()
    {
        var world = new SimulationWorld(11, 3, Terrain.Shallows, seed: 40);
        world.SeasonsEnabled = false;
        var trilobiteStart = new GridPosition(5, 1);
        world.AddCritter(CritterSpecies.Trilobite, trilobiteStart);
        world.AddCritter(CritterSpecies.Squid, new GridPosition(8, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var trilobite = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Trilobite);
        Assert.True(trilobite.Position.X < trilobiteStart.X);
    }

    [Theory]
    [InlineData(Terrain.DeepOcean, 3)]
    [InlineData(Terrain.Ocean, 2)]
    [InlineData(Terrain.Shallows, 4)]
    public void TrilobiteFeedsInDeepOceanAndShallows(Terrain terrain, int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, terrain, seed: 40);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Trilobite, new GridPosition(0, 0));

        for (var tick = 0; tick < 22 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void TrilobiteMovesOntoAndFeedsFromBeach()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 41);
        world.SeasonsEnabled = false;
        var beach = new GridPosition(1, 0);
        world.SetTerrain(beach, Terrain.Beach);
        world.SetTemperature(beach, 0.5f);
        var trilobiteId = world.AddCritter(
            CritterSpecies.Trilobite,
            new GridPosition(0, 0));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(trilobiteId, out var arrived));
        Assert.Equal(beach, arrived.Position);
        Assert.Equal(2, arrived.Energy);

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(trilobiteId, out var fed));
        Assert.Equal(beach, fed.Position);
        Assert.Equal(3, fed.Energy);
    }

    [Fact]
    public void CrabRoamsLandButPlainTerrainDoesNotFeedIt()
    {
        var beach = new SimulationWorld(1, 1, Terrain.Beach, seed: 42);
        var shallows = new SimulationWorld(1, 1, Terrain.Shallows, seed: 42);
        var plains = new SimulationWorld(1, 1, Terrain.Plains, seed: 42);
        beach.SeasonsEnabled = false;
        shallows.SeasonsEnabled = false;
        plains.SeasonsEnabled = false;
        beach.AddCritter(CritterSpecies.Crab, new GridPosition(0, 0));
        shallows.AddCritter(CritterSpecies.Crab, new GridPosition(0, 0));
        plains.AddCritter(CritterSpecies.Crab, new GridPosition(0, 0));

        for (var tick = 0; tick < 15 * SimulationWorld.TicksPerSecond; tick++)
        {
            beach.AdvanceOneTick();
            shallows.AdvanceOneTick();
            plains.AdvanceOneTick();
        }

        Assert.Equal(3, beach.GetCritter(0).Energy);
        Assert.Equal(5, shallows.GetCritter(0).Energy);
        Assert.Equal(3, plains.GetCritter(0).Energy);
        Assert.True(new SimulationWorld(1, 1, Terrain.Ocean)
            .TryAddCritter(CritterSpecies.Crab, new GridPosition(0, 0)));
        Assert.False(new SimulationWorld(1, 1, Terrain.DeepOcean)
            .TryAddCritter(CritterSpecies.Crab, new GridPosition(0, 0)));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River, Biome.Grassland)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, Biome.Grassland)]
    [InlineData(SurfaceWaterKind.None, Biome.Swamp)]
    [InlineData(SurfaceWaterKind.None, Biome.Jungle)]
    public void CrabFeedsInFreshwaterAndWetlandBiomes(
        SurfaceWaterKind water,
        Biome biome)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 4202);
        world.SeasonsEnabled = false;
        var position = new GridPosition(0, 0);
        world.SetSurfaceWater(position, water);
        world.SetBiome(position, biome);
        world.AddCritter(CritterSpecies.Crab, position);

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetCritter(0).Energy > 3);
    }

    [Fact]
    public void HungryCrabApproachesCoastalFoodFromFiveTilesAway()
    {
        var world = new SimulationWorld(11, 1, Terrain.Plains, seed: 4201);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var beach = new GridPosition(0, 0);
        var start = new GridPosition(5, 0);
        world.SetTerrain(beach, Terrain.Beach);
        world.AddCritter(CritterSpecies.Crab, start);

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(new GridPosition(4, 0), world.GetCritter(0).Position);
    }

    [Fact]
    public void CrabBreedingHasNoTerrainRestrictionBeyondValidHabitat()
    {
        var world = new SimulationWorld(4, 1, Terrain.Plains, seed: 43);
        var beach = new GridPosition(0, 0);
        var shallows = new GridPosition(1, 0);
        var plains = new GridPosition(2, 0);
        var hills = new GridPosition(3, 0);
        world.SetTerrain(beach, Terrain.Beach);
        world.SetTerrain(shallows, Terrain.Shallows);
        world.SetTerrain(hills, Terrain.Hills);

        Assert.True(world.IsValidReproductionSite(CritterSpecies.Crab, beach));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.Crab, shallows));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.Crab, plains));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.Crab, hills));
    }

    [Fact]
    public void CrabsAllowFreezingBeachesAndIceSheetsButRejectOtherArcticTerrain()
    {
        var world = new SimulationWorld(4, 1, Terrain.Plains, seed: 94);
        var coldBeach = new GridPosition(0, 0);
        var freezingBeach = new GridPosition(1, 0);
        var iceSheet = new GridPosition(2, 0);
        var arcticPlain = new GridPosition(3, 0);
        world.SetTerrain(coldBeach, Terrain.Beach);
        world.SetBiome(coldBeach, Biome.Tundra);
        world.SetTerrain(freezingBeach, Terrain.Beach);
        world.SetBiome(freezingBeach, Biome.Arctic);
        world.SetTerrain(iceSheet, Terrain.Ice);
        world.SetBiome(iceSheet, Biome.Arctic);
        world.SetBiome(arcticPlain, Biome.Arctic);

        Assert.True(world.TryAddCritter(CritterSpecies.Crab, coldBeach));
        Assert.True(world.TryAddCritter(CritterSpecies.Crab, freezingBeach));
        Assert.True(world.TryAddCritter(CritterSpecies.Crab, iceSheet));
        Assert.False(world.TryAddCritter(CritterSpecies.Crab, arcticPlain));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.Crab, freezingBeach));
    }

    [Theory]
    [InlineData(Terrain.Beach, 1, 3)]
    [InlineData(Terrain.Shallows, 2, 2)]
    public void CoastalFeedingReflectsTerrainProductivityWithinSixteenSeconds(
        Terrain terrain,
        int expectedCount,
        int expectedParentEnergy)
    {
        var world = new SimulationWorld(2, 1, terrain, seed: 44);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Crab, new GridPosition(0, 0));

        for (var tick = 0; tick < 16 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedCount, world.GetCritterCount(CritterSpecies.Crab));
        Assert.Equal(expectedParentEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void JumpStartFillsEveryEmptyDeepOceanTileWithPlankton()
    {
        var world = new SimulationWorld(4, 2, Terrain.Ocean, seed: 104);
        var firstDeepOcean = new GridPosition(0, 0);
        var occupiedDeepOcean = new GridPosition(1, 0);
        var existingPlankton = new GridPosition(2, 0);
        var lastDeepOcean = new GridPosition(3, 1);
        world.SetTerrain(firstDeepOcean, Terrain.DeepOcean);
        world.SetTerrain(occupiedDeepOcean, Terrain.DeepOcean);
        world.SetTerrain(existingPlankton, Terrain.DeepOcean);
        world.SetTerrain(lastDeepOcean, Terrain.DeepOcean);
        world.AddCritter(CritterSpecies.Worm, occupiedDeepOcean);
        world.AddCritter(CritterSpecies.Plankton, existingPlankton);

        Assert.Equal(2, world.JumpStartPlankton());

        Assert.Equal(3, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.True(world.TryGetCritterAt(firstDeepOcean, out var first));
        Assert.Equal(CritterSpecies.Plankton, first.Species);
        Assert.True(world.TryGetCritterAt(lastDeepOcean, out var last));
        Assert.Equal(CritterSpecies.Plankton, last.Species);
        Assert.True(world.TryGetCritterAt(occupiedDeepOcean, out var occupied));
        Assert.Equal(CritterSpecies.Worm, occupied.Species);
        Assert.Equal(0, world.JumpStartPlankton());

        LifeSystem.SetEnabled(world, false);
        Assert.Equal(4, world.JumpStartPlankton());
        Assert.True(world.LifeEnabled);
        Assert.Equal(4, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Fact]
    public void CrabCanPlaceOffspringInAnyValidHabitat()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 45);
        world.SeasonsEnabled = false;
        var beach = new GridPosition(0, 0);
        var nursery = new GridPosition(1, 0);
        world.SetTerrain(beach, Terrain.Beach);
        world.SetTemperature(beach, 0.8f);
        world.SetSurfaceWater(beach, SurfaceWaterKind.River);
        var parentId = world.AddCritter(CritterSpecies.Crab, beach);
        world.AddCritter(CritterSpecies.Crab, nursery);

        for (var tick = 0; tick < 12 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(parentId, out var parent));
        Assert.Equal(5, parent.Energy);
        Assert.True(world.RemoveCritterAt(nursery));
        for (var tick = 0;
            tick < 4 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Crab) == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Crab));
        Assert.True(world.TryGetCritter(parentId, out parent));
        Assert.Equal(2, parent.Energy);
    }

    [Fact]
    public void EveryCrabPredatorCanPursueItOnlyWhenAdjacent()
    {
        var expectedPredators = new HashSet<CritterSpecies>
        {
            CritterSpecies.Jellyfish,
            CritterSpecies.SeaScorpion,
            CritterSpecies.Nautilus,
            CritterSpecies.Squid,
            CritterSpecies.MegaToad,
            CritterSpecies.Therapsid,
            CritterSpecies.Ape,
            CritterSpecies.ApeSailor,
            CritterSpecies.Wolf,
            CritterSpecies.ToothedWhale,
        };
        Assert.Equal(
            expectedPredators,
            Enum.GetValues<CritterSpecies>()
                .Where(species => SimulationWorld.CanEat(species, CritterSpecies.Crab))
                .ToHashSet());

        foreach (var predator in Enum.GetValues<CritterSpecies>()
            .Where(species => SimulationWorld.CanEat(species, CritterSpecies.Crab)))
        {
            Assert.True(SimulationWorld.CanPursuePreyAtDistance(
                predator,
                CritterSpecies.Crab,
                distance: 1));
            Assert.False(SimulationWorld.CanPursuePreyAtDistance(
                predator,
                CritterSpecies.Crab,
                distance: 2));
        }
    }

    [Fact]
    public void LandPredatorConsumesAdjacentLakeCrabWithoutEnteringLake()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 4203);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        var shore = new GridPosition(0, 0);
        var lake = new GridPosition(1, 0);
        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);
        world.AddCritter(CritterSpecies.Wolf, shore);
        world.AddCritter(CritterSpecies.Crab, lake);

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Crab));
        Assert.True(world.TryGetCritterAt(shore, out var wolf));
        Assert.Equal(CritterSpecies.Wolf, wolf.Species);
    }

    [Theory]
    [InlineData(CritterSpecies.Worm)]
    [InlineData(CritterSpecies.Fish)]
    public void JellyfishDoesNotConsumeWormsOrFish(CritterSpecies protectedSpecies)
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 41);
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 0));
        world.AddCritter(protectedSpecies, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.False(SimulationWorld.CanEat(CritterSpecies.Jellyfish, protectedSpecies));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Jellyfish));
        Assert.Equal(1, world.GetCritterCount(protectedSpecies));
    }

    [Fact]
    public void NautilusHuntsPlankton()
    {
        var world = new SimulationWorld(3, 1, Terrain.Ocean, seed: 41);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Nautilus, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(1, 0));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(SimulationWorld.CanEat(
            CritterSpecies.Nautilus,
            CritterSpecies.Plankton));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Nautilus));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void DeepOceanForagingAloneNoLongerSupportsNautilusReproduction()
    {
        var world = new SimulationWorld(2, 1, Terrain.DeepOcean, seed: 41);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        world.SetTemperature(new GridPosition(0, 0), 0.25f);
        world.SetTemperature(new GridPosition(1, 0), 0.25f);
        world.AddCritter(CritterSpecies.Nautilus, new GridPosition(0, 0));

        for (var tick = 0; tick < 4 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Nautilus));
    }

    [Theory]
    [InlineData(Terrain.DeepOcean, 4)]
    [InlineData(Terrain.Ocean, 3)]
    [InlineData(Terrain.Shallows, 5)]
    public void NautilusFeedsInDeepOceanAndShallows(
        Terrain terrain,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, terrain, seed: 41);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Nautilus, new GridPosition(0, 0));

        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void NautilusFeedsBeforeRoamingFromAProductiveTile()
    {
        var world = new SimulationWorld(5, 1, Terrain.DeepOcean, seed: 41);
        world.SeasonsEnabled = false;
        var start = new GridPosition(2, 0);
        world.AddCritter(CritterSpecies.Nautilus, start);

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(start, world.GetCritter(0).Position);
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Squid, CritterSpecies.Nautilus)]
    [InlineData(CritterSpecies.SeaScorpion, CritterSpecies.Trilobite)]
    public void MarinePredatorsEatTheirAdjacentAncestorsWithoutHuntingThemAtRange(
        CritterSpecies predator,
        CritterSpecies ancestor)
    {
        Assert.True(SimulationWorld.CanEat(predator, ancestor));
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            predator,
            ancestor,
            distance: 1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            predator,
            ancestor,
            distance: 2));

        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 42);
        world.SeasonsEnabled = false;
        world.AddCritter(predator, new GridPosition(0, 0));
        world.AddCritter(ancestor, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(predator));
        Assert.Equal(0, world.GetCritterCount(ancestor));
        Assert.Equal(8, world.GetCritter(0).Energy);
    }

    [Fact]
    public void SeaScorpionsEatNautilusesOnlyWhenAdjacent()
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.SeaScorpion,
            CritterSpecies.Nautilus,
            distance: 1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.SeaScorpion,
            CritterSpecies.Nautilus,
            distance: 2));
    }

    [Fact]
    public void SquidAndSeaScorpionsActivelyPursueEachOther()
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.Squid,
            CritterSpecies.SeaScorpion,
            distance: 4));
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.SeaScorpion,
            CritterSpecies.Squid,
            distance: 4));
    }

    [Fact]
    public void NautilusShellProtectsItFromJellyfish()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 42);
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Nautilus, new GridPosition(0, 1));

        for (var tick = 0; tick < 12 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Jellyfish));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Nautilus));
    }

    [Theory]
    [InlineData(CritterSpecies.Fish, 8)]
    [InlineData(CritterSpecies.Trilobite, 8)]
    [InlineData(CritterSpecies.Crab, 8)]
    [InlineData(CritterSpecies.Newt, 7)]
    [InlineData(CritterSpecies.Deer, 9)]
    [InlineData(CritterSpecies.Elk, 9)]
    [InlineData(CritterSpecies.Gazelle, 9)]
    public void SquidHuntsItsAquaticPrey(CritterSpecies prey, int expectedEnergy)
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 47);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Squid, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Therapsid)]
    [InlineData(CritterSpecies.Monkey)]
    [InlineData(CritterSpecies.Ape)]
    [InlineData(CritterSpecies.Wolf)]
    [InlineData(CritterSpecies.Nautilus)]
    public void SeaScorpionDietIncludesAddedPrey(CritterSpecies prey)
    {
        Assert.True(SimulationWorld.CanEat(CritterSpecies.SeaScorpion, prey));
    }

    [Theory]
    [InlineData(CritterSpecies.SeaScorpion)]
    [InlineData(CritterSpecies.Squid)]
    public void JellyfishAreNotPreyForActiveMarineHunters(CritterSpecies predator)
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 46);
        world.SeasonsEnabled = false;
        world.AddCritter(predator, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Jellyfish, new GridPosition(0, 1));

        for (var tick = 0; tick < 12 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(predator));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Jellyfish));
    }

    [Fact]
    public void MutualPredatorCombatDealsOneDamageInsteadOfInstantlyEatingTheLoser()
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 52);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Squid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 1));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            var totalEnergy = Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy);
            if (totalEnergy < 8)
            {
                break;
            }
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SeaScorpion));
        Assert.Equal(
            7,
            Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy));
    }

    [Fact]
    public void MutualPredatorCombatEventuallyKillsAndFeedsTheWinner()
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 54);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Squid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 1));

        for (var tick = 0;
            tick < 30 * SimulationWorld.TicksPerSecond && world.CritterCount > 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.CritterCount);
        Assert.True(world.GetCritter(0).Energy >= 1);
    }

    [Fact]
    public void SquidReproductionProducesAnEgg()
    {
        var world = new SimulationWorld(1, 1, Terrain.Ocean, seed: 48);

        Assert.Equal(
            CritterSpecies.SquidEgg,
            world.ChooseOffspringSpecies(CritterSpecies.Squid));
    }

    [Fact]
    public void SquidEggDriftsOnTheSameScheduleAsPlankton()
    {
        var eggWorld = new SimulationWorld(3, 3, Terrain.Ocean, seed: 49);
        var planktonWorld = new SimulationWorld(3, 3, Terrain.Ocean, seed: 49);
        var start = new GridPosition(1, 1);
        eggWorld.AddCritter(CritterSpecies.SquidEgg, start);
        planktonWorld.AddCritter(CritterSpecies.Plankton, start);

        for (var tick = 0; tick < SimulationWorld.PlanktonMovementIntervalTicks; tick++)
        {
            eggWorld.AdvanceOneTick();
            planktonWorld.AdvanceOneTick();
        }

        Assert.Equal(planktonWorld.GetCritter(0).Position, eggWorld.GetCritter(0).Position);
        Assert.NotEqual(start, eggWorld.GetCritter(0).Position);
    }

    [Fact]
    public void SquidEggHatchesWhenSquidPreyComesNearby()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 50);
        world.AddCritter(CritterSpecies.SquidEgg, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 1));

        world.AdvanceOneTick();

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void SquidEggDoesNotHatchForCrabBeyondAdjacentRange()
    {
        var world = new SimulationWorld(5, 1, Terrain.Ocean, seed: 50);
        world.AddCritter(CritterSpecies.SquidEgg, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(2, 0));

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Squid));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Crab));
    }

    [Fact]
    public void SquidEggHatchesAsItDriftsIntoShallows()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 52);
        world.SetTerrain(new GridPosition(1, 0), Terrain.Shallows);
        world.AddCritter(CritterSpecies.SquidEgg, new GridPosition(0, 0));

        for (var tick = 0;
            tick < SimulationWorld.PlanktonMovementIntervalTicks &&
                world.GetCritterCount(CritterSpecies.SquidEgg) > 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        Assert.Equal(new GridPosition(1, 0), world.GetCritter(0).Position);
        Assert.Equal(4, world.GetCritter(0).Energy);
    }

    [Fact]
    public void SquidEggDoesNotHatchForNonPrey()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 51);
        world.AddCritter(CritterSpecies.SquidEgg, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(0, 1));

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SquidEgg));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Squid));
    }

    [Fact]
    public void FishDietContainsOnlyPlankton()
    {
        foreach (var prey in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(
                prey is CritterSpecies.Plankton,
                SimulationWorld.CanEat(CritterSpecies.Fish, prey));
        }
    }

    [Fact]
    public void FishShovesWormWhenWanderingAndSpaceIsAvailable()
    {
        var world = new SimulationWorld(3, 1, Terrain.Ocean, seed: 43);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(1, 0));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritterAt(new GridPosition(1, 0), out var fish));
        Assert.Equal(CritterSpecies.Fish, fish.Species);
        Assert.Equal(3, fish.Energy);
        Assert.True(world.TryGetCritterAt(new GridPosition(2, 0), out var worm));
        Assert.Equal(CritterSpecies.Worm, worm.Species);
    }

    [Fact]
    public void TrappedWormBlocksFishWithoutBecomingAMeal()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 43);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(1, 0));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Worm));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(3, world.GetCritter(0).Energy);
    }

    [Fact]
    public void CrowdedWormsBlockFishRouteToShallowsWithoutBeingEaten()
    {
        var world = new SimulationWorld(1, 4, Terrain.Ocean, seed: 43);
        world.SeasonsEnabled = false;
        var shallows = new GridPosition(0, 3);
        world.SetTerrain(shallows, Terrain.Shallows);
        world.SetTemperature(shallows, 0.8f);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 1));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(0, 2));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Worm));
        Assert.True(world.TryGetCritterAt(new GridPosition(0, 0), out var fish));
        Assert.Equal(CritterSpecies.Fish, fish.Species);
        Assert.Equal(3, fish.Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Worm)]
    [InlineData(CritterSpecies.Crab)]
    public void FishIgnoresCloserProtectedSpeciesAndHuntsPlankton(CritterSpecies fallbackPrey)
    {
        var world = new SimulationWorld(5, 3, Terrain.Ocean, seed: 43);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(2, 1));
        world.AddCritter(fallbackPrey, new GridPosition(3, 1));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(0, 0));

        for (var tick = 0;
            tick < 30 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Plankton) > 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(1, world.GetCritterCount(fallbackPrey));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Theory]
    [InlineData(CritterSpecies.Worm)]
    [InlineData(CritterSpecies.Crab)]
    public void FishApproachesNearbyShallowsWithoutEatingAdjacentProtectedSpecies(
        CritterSpecies fallbackPrey)
    {
        var world = new SimulationWorld(3, 2, Terrain.Ocean, seed: 43);
        world.SeasonsEnabled = false;
        var reef = new GridPosition(0, 1);
        world.SetTerrain(reef, Terrain.Shallows);
        world.SetTemperature(reef, 0.8f);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(fallbackPrey, new GridPosition(1, 0));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(fallbackPrey));
        Assert.Equal(reef, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(Terrain.Shallows, 0.8f, 6)]
    [InlineData(Terrain.Shallows, 0.5f, 6)]
    [InlineData(Terrain.Ocean, 0.8f, 2)]
    public void FishForageInShallowsAtAnyTemperature(
        Terrain terrain,
        float temperature,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, terrain, seed: 54);
        world.SeasonsEnabled = false;
        world.SetTemperature(new GridPosition(0, 0), temperature);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));

        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River, 4)]
    [InlineData(SurfaceWaterKind.FreshwaterLake, 4)]
    public void FishForageInRiversAndLakes(
        SurfaceWaterKind freshwater,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 55);
        world.SeasonsEnabled = false;
        world.SetSurfaceWater(new GridPosition(0, 0), freshwater);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));

        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void HungryFishVisitsNearbyShallowsButCanExhaustTheReef()
    {
        var world = new SimulationWorld(7, 1, Terrain.Ocean, seed: 55);
        world.SeasonsEnabled = false;
        var reef = new GridPosition(3, 0);
        world.SetTerrain(reef, Terrain.Shallows);
        world.SetTemperature(reef, 0.8f);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));

        var visitedReef = false;
        for (var tick = 0; tick < 4 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            visitedReef |= world.GetCritterCount(CritterSpecies.Fish) == 1 &&
                world.GetCritter(0).Position == reef;
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        Assert.True(visitedReef);
    }

    [Fact]
    public void ReefForagingCanEventuallySupportFishReproduction()
    {
        var world = new SimulationWorld(2, 1, Terrain.Shallows, seed: 55);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        world.SetTemperature(new GridPosition(0, 0), 0.8f);
        world.SetTemperature(new GridPosition(1, 0), 0.8f);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));

        for (var tick = 0;
            tick < 5 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Fish) == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Fish));
    }

    [Fact]
    public void LakeForagingCanSupportFishReproduction()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 56);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        for (var x = 0; x < world.Width; x++)
        {
            var position = new GridPosition(x, 0);
            world.SetSurfaceWater(position, SurfaceWaterKind.FreshwaterLake);
        }
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));

        for (var tick = 0;
            tick < 5 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Fish) == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Fish));
    }

    [Fact]
    public void FishCanPlaceOffspringInADiagonallyConnectedRiverTile()
    {
        var world = new SimulationWorld(3, 3, Terrain.Plains, seed: 56);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        var parent = new GridPosition(1, 1);
        var diagonalRiver = new GridPosition(2, 2);
        world.SetTerrain(parent, Terrain.Shallows);
        world.SetTemperature(parent, 0.8f);
        world.SetSurfaceWater(diagonalRiver, SurfaceWaterKind.River);
        world.AddCritter(CritterSpecies.Fish, parent);

        for (var tick = 0;
            tick < 5 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Fish) == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Fish));
        Assert.True(world.TryGetCritterAt(diagonalRiver, out var offspring));
        Assert.Equal(CritterSpecies.Fish, offspring.Species);
    }

    [Theory]
    [InlineData(CritterSpecies.SeaScorpion)]
    [InlineData(CritterSpecies.Squid)]
    [InlineData(CritterSpecies.MegaToad)]
    public void FishFleesSpeciesThatCanEatIt(CritterSpecies predator)
    {
        var world = new SimulationWorld(11, 3, Terrain.Shallows, seed: 55);
        world.SeasonsEnabled = false;
        var fishStart = new GridPosition(5, 1);
        world.AddCritter(CritterSpecies.Fish, fishStart);
        world.AddCritter(predator, new GridPosition(8, 1));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var fish = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Fish);
        Assert.True(fish.Position.X < fishStart.X);
    }

    [Fact]
    public void FleeingFishShovesPlanktonInsteadOfBeingBlockedOrEatingIt()
    {
        var world = new SimulationWorld(11, 3, Terrain.Shallows, seed: 56);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(5, 1));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(4, 0));
        world.AddCritter(CritterSpecies.Plankton, new GridPosition(4, 2));
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(8, 1));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var fish = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Fish);
        Assert.Equal(4, fish.Position.X);
        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Plankton));
    }

    [Fact]
    public void FishShovesBlockingNewtWithoutEatingIt()
    {
        var world = new SimulationWorld(4, 1, Terrain.Ocean, seed: 57);
        world.SeasonsEnabled = false;
        world.SetTerrain(new GridPosition(3, 0), Terrain.Mountain);
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Newt, new GridPosition(1, 0));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritterAt(new GridPosition(1, 0), out var fish));
        Assert.Equal(CritterSpecies.Fish, fish.Species);
        Assert.True(world.TryGetCritterAt(new GridPosition(2, 0), out var newt));
        Assert.Equal(CritterSpecies.Newt, newt.Species);
    }

    [Fact]
    public void NewtDoesNotMakeAMapWideMigrationToFreshwater()
    {
        var world = new SimulationWorld(25, 1, Terrain.Ocean, seed: 53);
        world.SeasonsEnabled = false;
        var start = new GridPosition(1, 0);
        var freshwater = new GridPosition(15, 0);
        world.SetTerrain(freshwater, Terrain.Plains);
        world.SetSurfaceWater(freshwater, SurfaceWaterKind.FreshwaterLake);
        world.AddCritter(CritterSpecies.Newt, start);

        for (var tick = 0; tick < 15 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(start, world.GetCritter(0).Position);
    }

    [Fact]
    public void NewtFeedsInFreshwaterSwampsAndJungles()
    {
        var dry = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var river = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var lake = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var frozenLake = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var swamp = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        var jungle = new SimulationWorld(1, 1, Terrain.Plains, seed: 67);
        dry.SeasonsEnabled = false;
        river.SeasonsEnabled = false;
        lake.SeasonsEnabled = false;
        frozenLake.SeasonsEnabled = false;
        swamp.SeasonsEnabled = false;
        jungle.SeasonsEnabled = false;
        river.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.River);
        lake.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);
        frozenLake.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);
        frozenLake.SetBiome(new GridPosition(0, 0), Biome.Arctic);
        swamp.SetBiome(new GridPosition(0, 0), Biome.Swamp);
        jungle.SetBiome(new GridPosition(0, 0), Biome.Jungle);
        dry.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        river.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        lake.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        frozenLake.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        swamp.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        jungle.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));

        for (var tick = 0; tick < 18 * SimulationWorld.TicksPerSecond; tick++)
        {
            dry.AdvanceOneTick();
            river.AdvanceOneTick();
            lake.AdvanceOneTick();
            frozenLake.AdvanceOneTick();
            swamp.AdvanceOneTick();
            jungle.AdvanceOneTick();
        }

        Assert.Equal(4, dry.GetCritter(0).Energy);
        Assert.Equal(6, river.GetCritter(0).Energy);
        Assert.Equal(6, lake.GetCritter(0).Energy);
        Assert.Equal(4, frozenLake.GetCritter(0).Energy);
        Assert.Equal(7, swamp.GetCritter(0).Energy);
        Assert.Equal(7, jungle.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Worm, 4, 4, 2)]
    [InlineData(CritterSpecies.Trilobite, 7, 5, 3)]
    [InlineData(CritterSpecies.Fish, 7, 7, 5)]
    [InlineData(CritterSpecies.Newt, 7, 7, 5)]
    public void SmallPreyUseSmallerReproductionReserves(
        CritterSpecies species,
        int maximumEnergy,
        int reproductionThreshold,
        int reproductionCost)
    {
        var nutrition = CritterNutritions.Get(species);

        Assert.Equal(maximumEnergy, nutrition.MaximumEnergy);
        Assert.Equal(reproductionThreshold, nutrition.ReproductionThreshold);
        Assert.Equal(reproductionCost, nutrition.ReproductionCost);
    }

    [Fact]
    public void EveryReproducingCritterKeepsAtLeastTwoEnergyAfterBirth()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var nutrition = CritterNutritions.Get(species);
            if (!nutrition.CanReproduce)
            {
                continue;
            }

            Assert.True(
                nutrition.ReproductionThreshold <= nutrition.MaximumEnergy,
                $"{species} cannot reach its reproduction threshold.");
            Assert.True(
                nutrition.ReproductionThreshold - nutrition.ReproductionCost >= 2,
                $"{species} keeps less than two energy after reproducing.");
        }
    }

    [Fact]
    public void PreyFoodEnergyComesDirectlyFromBodySize()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var nutrition = CritterNutritions.Get(species);
            Assert.Equal((int)nutrition.BodySize, nutrition.FoodEnergy);
        }
    }

    [Fact]
    public void WormsArePreyOnlyForSquidAndSeaScorpions()
    {
        foreach (var predator in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(
                predator is CritterSpecies.Squid or CritterSpecies.SeaScorpion,
                SimulationWorld.CanEat(predator, CritterSpecies.Worm));
        }
    }

    [Theory]
    [InlineData(CritterSpecies.Squid)]
    [InlineData(CritterSpecies.SeaScorpion)]
    public void ActiveMarineHuntersEatWormsOnlyWhenAdjacent(CritterSpecies predator)
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            predator,
            CritterSpecies.Worm,
            distance: 1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            predator,
            CritterSpecies.Worm,
            distance: 2));
    }

    [Theory]
    [InlineData(CritterSpecies.MegaToad, CritterSpecies.Deer, true)]
    [InlineData(CritterSpecies.Fish, CritterSpecies.Worm, true)]
    [InlineData(CritterSpecies.Worm, CritterSpecies.Plankton, true)]
    [InlineData(CritterSpecies.Worm, CritterSpecies.Fish, false)]
    [InlineData(CritterSpecies.Trilobite, CritterSpecies.Fish, false)]
    [InlineData(CritterSpecies.Fish, CritterSpecies.Crab, false)]
    [InlineData(CritterSpecies.Deer, CritterSpecies.Wolf, false)]
    public void LargerCrittersCanDisplaceSmallerCritters(
        CritterSpecies mover,
        CritterSpecies blocker,
        bool expected)
    {
        Assert.Equal(expected, SimulationWorld.CanDisplace(mover, blocker));
    }

    [Fact]
    public void EveryCritterExceptSquidEggCanDisplacePlankton()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(
                species is not CritterSpecies.SquidEgg,
                SimulationWorld.CanDisplacePlankton(species));
        }
    }

    [Fact]
    public void MegaToadHasTheLargestLandPredatorEnergyReserve()
    {
        var toad = CritterNutritions.Get(CritterSpecies.MegaToad);
        var therapsid = CritterNutritions.Get(CritterSpecies.Therapsid);
        var wolf = CritterNutritions.Get(CritterSpecies.Wolf);

        Assert.Equal(16, toad.MaximumEnergy);
        Assert.True(toad.MaximumEnergy > therapsid.MaximumEnergy);
        Assert.True(toad.MaximumEnergy > wolf.MaximumEnergy);
        Assert.Equal(5, toad.InitialEnergy);
        Assert.Equal(14, toad.ReproductionThreshold);
        Assert.Equal(9, toad.ReproductionCost);
    }

    [Theory]
    [InlineData(Biome.Swamp, 4)]
    [InlineData(Biome.Jungle, 5)]
    public void NewtCanBuildEnoughWetlandFoliageEnergyToReproduce(
        Biome biome,
        int expectedParentEnergy)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 69);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 0), biome);
        world.SetBiome(new GridPosition(1, 0), biome);
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));

        for (var tick = 0; tick < 54 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Newt));
        Assert.Equal(expectedParentEnergy, world.GetCritter(0).Energy);
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

        Assert.Equal(6, river.GetCritter(0).Energy);
        Assert.Equal(6, lake.GetCritter(0).Energy);
        Assert.Equal(4, frozenLake.GetCritter(0).Energy);
    }

    [Fact]
    public void HungryNewtUsesItsNextActionToFeedFromAdjacentShore()
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
        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
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

    [Fact]
    public void FishCanEnterRiverAndLakeMountainTilesButNotDryMountains()
    {
        var world = new SimulationWorld(3, 1, Terrain.Mountain);
        var dryMountain = new GridPosition(0, 0);
        var riverMountain = new GridPosition(1, 0);
        var lakeMountain = new GridPosition(2, 0);
        world.SetSurfaceWater(riverMountain, SurfaceWaterKind.River);
        world.SetSurfaceWater(lakeMountain, SurfaceWaterKind.FreshwaterLake);

        Assert.False(world.TryAddCritter(CritterSpecies.Fish, dryMountain));
        Assert.True(world.TryAddCritter(CritterSpecies.Fish, riverMountain));
        Assert.True(world.TryAddCritter(CritterSpecies.Fish, lakeMountain));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void FishCanSwimOntoFreshwaterMountainTiles(SurfaceWaterKind water)
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 76);
        world.SeasonsEnabled = false;
        var start = new GridPosition(0, 0);
        var mountainWater = new GridPosition(1, 0);
        world.SetTerrain(mountainWater, Terrain.Mountain);
        world.SetSurfaceWater(mountainWater, water);
        world.AddCritter(CritterSpecies.Fish, start);

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(mountainWater, world.GetCritter(0).Position);
    }

    [Theory]
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

    [Theory]
    [InlineData(CritterSpecies.MegaToad, SurfaceWaterKind.River, 6)]
    [InlineData(CritterSpecies.MegaToad, SurfaceWaterKind.FreshwaterLake, 6)]
    public void AmphibiansCanFollowDiagonalFreshwaterMountainConnections(
        CritterSpecies species,
        SurfaceWaterKind water,
        int movementSeconds)
    {
        var world = new SimulationWorld(2, 2, Terrain.Mountain, seed: 75);
        world.SeasonsEnabled = false;
        var start = new GridPosition(0, 0);
        var diagonalFreshwaterMountain = new GridPosition(1, 1);
        world.SetTerrain(start, Terrain.Plains);
        world.SetSurfaceWater(diagonalFreshwaterMountain, water);
        world.AddCritter(species, start);

        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(diagonalFreshwaterMountain, world.GetCritter(0).Position);
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
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Fact]
    public void FishDoesNotConsumeAdjacentCrab()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 77);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(0, 1));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Crab));
    }

    [Fact]
    public void FishCannotHuntNewtsInSharedWater()
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 78);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 1));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Newt));
    }

    [Fact]
    public void FishLeavesNewtsUneaten()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 81);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 1));

        for (var tick = 0; tick < 2 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Newt));
        var fish = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Fish);
        Assert.Equal(3, fish.Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Trilobite)]
    [InlineData(CritterSpecies.Crab)]
    public void MegaToadLocallyHuntsCoastalArthropods(CritterSpecies prey)
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 79);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Fact]
    public void MegaToadHuntsNearestLegalPreyWithoutSpeciesPreference()
    {
        var world = new SimulationWorld(5, 3, Terrain.Shallows, seed: 71);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(1, 1));
        world.AddCritter(CritterSpecies.Newt, new GridPosition(2, 1));
        world.AddCritter(CritterSpecies.Worm, new GridPosition(3, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Newt));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Worm));
    }

    [Fact]
    public void MegaToadHuntsCloserNewtBeforeFartherFish()
    {
        var world = new SimulationWorld(1, 4, Terrain.Shallows, seed: 80);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Newt, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 1));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 3));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Newt));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
    }

    [Fact]
    public void MegaToadCanSwallowAnotherMegaToad()
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 82);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(13, world.GetCritter(0).Energy);
    }

    [Fact]
    public void MegaToadCannibalismRequiresAnAdjacentEncounter()
    {
        Assert.True(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.MegaToad,
            CritterSpecies.MegaToad,
            distance: 1));
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.MegaToad,
            CritterSpecies.MegaToad,
            distance: 2));
    }

    [Fact]
    public void NewbornMegaToadHasAThirtySecondReproductionTruce()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains, seed: 82);
        world.SeasonsEnabled = false;
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceWater(new GridPosition(x, 0), SurfaceWaterKind.River);
        }
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Elk, new GridPosition(1, 0));
        world.AddCritter(CritterSpecies.Elk, new GridPosition(2, 0));

        var sawBirth = false;
        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            if (world.GetCritterCount(CritterSpecies.MegaToad) == 2)
            {
                sawBirth = true;
                break;
            }
        }

        Assert.True(sawBirth);
        for (var tick = 0;
            tick < SimulationWorld.ReproductionTruceTicks - 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.MegaToad));
    }

    [Fact]
    public void ClosedMegaToadPopulationCannotSustainItselfThroughCannibalism()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 182);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        for (var x = 0; x < world.Width; x++)
        {
            world.SetSurfaceWater(new GridPosition(x, 0), SurfaceWaterKind.River);
            world.AddCritter(CritterSpecies.MegaToad, new GridPosition(x, 0));
        }

        for (var tick = 0; tick < 20 * 60 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(
            world.GetCritterCount(CritterSpecies.MegaToad) == 0,
            string.Join(", ", Enumerable.Range(0, world.CritterCount).Select(index => world.GetCritter(index))));
    }

    [Theory]
    [InlineData(SurfaceWaterKind.River)]
    [InlineData(SurfaceWaterKind.FreshwaterLake)]
    public void MegaToadHuntsFishInFreshwater(SurfaceWaterKind freshwater)
    {
        Assert.True(SimulationWorld.CanEat(CritterSpecies.MegaToad, CritterSpecies.Fish));

        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 83);
        world.SeasonsEnabled = false;
        world.SetSurfaceWater(new GridPosition(0, 0), freshwater);
        world.SetSurfaceWater(new GridPosition(0, 1), freshwater);
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Fact]
    public void NewtFleesMegaToadBeforeWaitingForFood()
    {
        var world = new SimulationWorld(7, 3, Terrain.Plains, seed: 17);
        world.SeasonsEnabled = false;
        var start = new GridPosition(3, 1);
        var toad = new GridPosition(3, 2);
        world.SetBiome(start, Biome.Swamp);
        world.AddCritter(CritterSpecies.Newt, start);
        world.AddCritter(CritterSpecies.MegaToad, toad);

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var escaped = world.GetCritter(0).Position;
        var distance = Math.Abs(escaped.X - toad.X) + Math.Abs(escaped.Y - toad.Y);
        Assert.True(distance > 1);
    }

    [Fact]
    public void ReproductionSitesHaveNoSpeciesSpecificTerrainRequirement()
    {
        var world = new SimulationWorld(7, 1, Terrain.Plains, seed: 84);
        world.SeasonsEnabled = false;
        var river = new GridPosition(1, 0);
        var riverShore = new GridPosition(2, 0);
        var dryLand = new GridPosition(3, 0);
        var lakeShore = new GridPosition(4, 0);
        var lake = new GridPosition(5, 0);
        world.SetSurfaceWater(river, SurfaceWaterKind.River);
        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);

        Assert.True(world.IsValidReproductionSite(CritterSpecies.MegaToad, river));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.MegaToad, riverShore));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.MegaToad, lakeShore));
        Assert.True(world.IsValidReproductionSite(CritterSpecies.MegaToad, dryLand));
        Assert.True(world.IsValidBirthSite(CritterSpecies.MegaToad, river));
        Assert.True(world.IsValidBirthSite(CritterSpecies.MegaToad, lake));
        Assert.True(world.IsValidBirthSite(CritterSpecies.MegaToad, riverShore));
        Assert.True(world.IsValidBirthSite(CritterSpecies.MegaToad, dryLand));
    }

    [Fact]
    public void TherapsidHuntsFishAndAdjacentFeederCrabs()
    {
        foreach (var prey in Enum.GetValues<CritterSpecies>())
        {
            Assert.Equal(
                prey is CritterSpecies.Fish or CritterSpecies.Crab,
                SimulationWorld.CanEat(CritterSpecies.Therapsid, prey));
        }
    }

    [Fact]
    public void TherapsidHuntsFishInRivers()
    {
        Assert.True(SimulationWorld.CanEat(CritterSpecies.Therapsid, CritterSpecies.Fish));

        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 85);
        world.SeasonsEnabled = false;
        world.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.River);
        world.SetSurfaceWater(new GridPosition(0, 1), SurfaceWaterKind.River);
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Therapsid));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Fact]
    public void TherapsidStrikesAdjacentFishInLakes()
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 85);
        world.SeasonsEnabled = false;
        var shore = new GridPosition(0, 0);
        var lake = new GridPosition(0, 1);
        world.SetSurfaceWater(lake, SurfaceWaterKind.FreshwaterLake);
        world.AddCritter(CritterSpecies.Therapsid, shore);
        world.AddCritter(CritterSpecies.Fish, lake);

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Therapsid));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
        Assert.Equal(shore, world.GetCritter(0).Position);
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Fact]
    public void TherapsidDoesNotHuntMonkey()
    {
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Therapsid, CritterSpecies.Monkey));

        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 86);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 1), Biome.Jungle);
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 1));

        for (var tick = 0; tick < 7 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Therapsid));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
    }

    [Fact]
    public void TherapsidDoesNotHuntItsOwnOffspringSpecies()
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 86);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Therapsid));
    }

    [Theory]
    [InlineData(Biome.Swamp, 9)]
    [InlineData(Biome.Jungle, 11)]
    [InlineData(Biome.Forest, 4)]
    [InlineData(Biome.Arid, 6)]
    [InlineData(Biome.Bog, 4)]
    [InlineData(Biome.None, 4)]
    public void HungryTherapsidForagesOnlyInSupportedBiomes(
        Biome biome,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 87);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 0), biome);
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));

        for (var tick = 0; tick < 90 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Jungle)]
    [InlineData(Biome.Arid)]
    public void TherapsidPrefersNearbyFoliageOverVisiblePrey(Biome biome)
    {
        var world = new SimulationWorld(3, 2, Terrain.Plains, seed: 188);
        world.SeasonsEnabled = false;
        var foliage = new GridPosition(1, 0);
        world.SetBiome(foliage, biome);
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(1, 1));
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(2, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(foliage, world.GetCritter(0).Position);
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
    }

    [Fact]
    public void TherapsidDefenseDoesNotExpandItsHuntingDiet()
    {
        Assert.True(SimulationWorld.CanEat(CritterSpecies.MegaToad, CritterSpecies.Therapsid));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Therapsid, CritterSpecies.MegaToad));
        Assert.True(SimulationWorld.CanDefendAgainst(
            CritterSpecies.Therapsid,
            CritterSpecies.MegaToad));
        Assert.True(SimulationWorld.CanDefendAgainst(
            CritterSpecies.Therapsid,
            CritterSpecies.Wolf));
        Assert.False(SimulationWorld.CanDefendAgainst(
            CritterSpecies.Therapsid,
            CritterSpecies.Fish));
        Assert.Equal(
            20,
            SimulationWorld.GetDefenderCombatWinChancePercent(
                CritterSpecies.Therapsid,
                CritterSpecies.MegaToad));
        Assert.Equal(
            20,
            SimulationWorld.GetDefenderCombatWinChancePercent(
                CritterSpecies.Therapsid,
                CritterSpecies.Wolf));
        Assert.Equal(
            50,
            SimulationWorld.GetDefenderCombatWinChancePercent(
                CritterSpecies.SeaScorpion,
                CritterSpecies.Squid));
    }

    [Fact]
    public void CombatLoserFlashesForHalfASecond()
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 86);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Squid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 1));

        CritterSnapshot loser = default;
        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            loser = Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .FirstOrDefault(critter => critter.IsDamageFlashing);
            if (loser.Id.IsValid)
            {
                break;
            }
        }

        Assert.True(loser.Id.IsValid);
        Assert.Equal(3, loser.Energy);
        for (var tick = 1; tick < SimulationWorld.CombatDamageFlashTicks; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.True(world.TryGetCritter(loser.Id, out var stillFlashing));
        Assert.True(stillFlashing.IsDamageFlashing);

        world.AdvanceOneTick();

        Assert.True(world.TryGetCritter(loser.Id, out var finishedFlashing));
        Assert.False(finishedFlashing.IsDamageFlashing);
    }

    [Fact]
    public void MegaToadPrefersOrdinaryPreyOverTherapsid()
    {
        var world = new SimulationWorld(1, 3, Terrain.Shallows, seed: 87);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 1));
        world.AddCritter(CritterSpecies.Fish, new GridPosition(0, 2));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Therapsid));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Fish));
    }

    [Theory]
    [InlineData(CritterSpecies.Newt)]
    [InlineData(CritterSpecies.Crab)]
    [InlineData(CritterSpecies.Fish)]
    public void MonkeyLeavesAnimalPreyForPredators(CritterSpecies prey)
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 88);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
        Assert.Equal(1, world.GetCritterCount(prey));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Monkey, prey));
    }

    [Fact]
    public void MonkeyIsNotAPredator()
    {
        foreach (var prey in Enum.GetValues<CritterSpecies>())
        {
            Assert.False(SimulationWorld.CanEat(CritterSpecies.Monkey, prey));
        }
    }

    [Fact]
    public void MutantOffspringCannotEatItsParentDuringReproductionTruce()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 190);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(CritterEvolution.MaximumChanceSteps);
        world.SetBiome(new GridPosition(0, 0), Biome.Jungle);
        world.SetBiome(new GridPosition(1, 0), Biome.Jungle);
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 0));

        for (var tick = 0;
            tick < 90 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Ape) == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
        for (var tick = 1; tick < SimulationWorld.ReproductionTruceTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Monkey));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Ape));
    }

    [Theory]
    [InlineData(Biome.Swamp)]
    [InlineData(Biome.Jungle)]
    [InlineData(Biome.Forest)]
    public void MonkeyFeedsFromSupportedFoliage(Biome biome)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 89);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 0), biome);
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 0));

        for (var tick = 0; tick < 18 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(8, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(Biome.Swamp, 4)]
    [InlineData(Biome.Jungle, 5)]
    public void MonkeyRemainsInWetlandsAndBuildsEnoughEnergyToReproduce(
        Biome biome,
        int expectedParentEnergy)
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 90);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 0), biome);
        world.SetBiome(new GridPosition(1, 0), biome);
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 0));

        for (var tick = 0; tick < 72 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Monkey));
        var parent = world.GetCritter(0);
        Assert.Equal(expectedParentEnergy, parent.Energy);
        Assert.Equal(biome, world.GetBiome(parent.Position));
    }

    [Fact]
    public void MegaToadCanEatMonkey()
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 90);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Monkey, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Monkey));
        Assert.Equal(9, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Deer, 10)]
    [InlineData(CritterSpecies.Elk, 10)]
    [InlineData(CritterSpecies.Gazelle, 10)]
    public void MegaToadCanEatGrazers(CritterSpecies prey, int expectedEnergy)
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 91);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Deer, Biome.Grassland, 18, 7)]
    [InlineData(CritterSpecies.Deer, Biome.Forest, 18, 8)]
    [InlineData(CritterSpecies.Deer, Biome.Tundra, 18, 4)]
    [InlineData(CritterSpecies.Deer, Biome.Taiga, 18, 4)]
    [InlineData(CritterSpecies.Elk, Biome.Grassland, 20, 8)]
    [InlineData(CritterSpecies.Elk, Biome.Tundra, 20, 6)]
    [InlineData(CritterSpecies.Elk, Biome.Taiga, 20, 8)]
    [InlineData(CritterSpecies.Elk, Biome.Forest, 20, 5)]
    [InlineData(CritterSpecies.Gazelle, Biome.Grassland, 18, 7)]
    [InlineData(CritterSpecies.Gazelle, Biome.Arid, 18, 6)]
    [InlineData(CritterSpecies.Gazelle, Biome.Forest, 18, 4)]
    [InlineData(CritterSpecies.Gazelle, Biome.Desert, 18, 4)]
    public void GrazersFeedOnlyFromTheirSupportedBiomes(
        CritterSpecies species,
        Biome biome,
        int feedingSeconds,
        int expectedEnergy)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 92);
        world.SeasonsEnabled = false;
        world.SetBiome(new GridPosition(0, 0), biome);
        world.AddCritter(species, new GridPosition(0, 0));

        for (var tick = 0; tick < feedingSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void ElkAreSlowerAndRequireMoreEnergyToReproduceThanDeer()
    {
        var deerNutrition = CritterNutritions.Get(CritterSpecies.Deer);
        var elkNutrition = CritterNutritions.Get(CritterSpecies.Elk);
        Assert.True(elkNutrition.ReproductionThreshold > deerNutrition.ReproductionThreshold);
        Assert.True(elkNutrition.ReproductionCost > deerNutrition.ReproductionCost);

        var deerWorld = new SimulationWorld(2, 1, Terrain.Plains, seed: 93);
        var elkWorld = new SimulationWorld(2, 1, Terrain.Plains, seed: 93);
        deerWorld.SeasonsEnabled = false;
        elkWorld.SeasonsEnabled = false;
        deerWorld.AddCritter(CritterSpecies.Deer, new GridPosition(0, 0));
        elkWorld.AddCritter(CritterSpecies.Elk, new GridPosition(0, 0));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            deerWorld.AdvanceOneTick();
            elkWorld.AdvanceOneTick();
        }

        Assert.Equal(new GridPosition(1, 0), deerWorld.GetCritter(0).Position);
        Assert.Equal(new GridPosition(0, 0), elkWorld.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Deer)]
    [InlineData(CritterSpecies.Elk)]
    [InlineData(CritterSpecies.Gazelle)]
    public void GrazerReproductionDoesNotRequireOpenCardinalNeighbors(CritterSpecies species)
    {
        var center = new GridPosition(2, 2);
        var cardinalWorld = new SimulationWorld(5, 5, Terrain.Plains, seed: 94);
        cardinalWorld.AddCritter(species, center);
        cardinalWorld.AddCritter(CritterSpecies.Crab, new GridPosition(3, 2));
        Assert.True(cardinalWorld.IsValidReproductionSite(species, center));

        var diagonalWorld = new SimulationWorld(5, 5, Terrain.Plains, seed: 94);
        diagonalWorld.AddCritter(species, center);
        diagonalWorld.AddCritter(CritterSpecies.Crab, new GridPosition(3, 3));
        Assert.True(diagonalWorld.IsValidReproductionSite(species, center));
    }

    [Theory]
    [InlineData(CritterSpecies.Monkey, 5)]
    [InlineData(CritterSpecies.Deer, 3)]
    [InlineData(CritterSpecies.Elk, 4)]
    [InlineData(CritterSpecies.Gazelle, 3)]
    public void TerrestrialPreyFleeFromSpeciesThatCanEatThem(
        CritterSpecies prey,
        int movementSeconds)
    {
        var world = new SimulationWorld(9, 7, Terrain.Plains, seed: 100);
        world.SeasonsEnabled = false;
        var preyStart = new GridPosition(4, 1);
        var wolfPosition = new GridPosition(4, 4);
        world.AddCritter(prey, preyStart);
        world.AddCritter(CritterSpecies.Wolf, wolfPosition);
        for (var y = wolfPosition.Y - 1; y <= wolfPosition.Y + 1; y++)
        {
            for (var x = wolfPosition.X - 1; x <= wolfPosition.X + 1; x++)
            {
                var position = new GridPosition(x, y);
                if (position != wolfPosition)
                {
                    Assert.True(Volcanism.PlaceStone(world, position));
                }
            }
        }

        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        var escaped = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species == prey)
            .Position;
        var originalDistance = Math.Abs(preyStart.X - wolfPosition.X) +
            Math.Abs(preyStart.Y - wolfPosition.Y);
        var escapedDistance = Math.Abs(escaped.X - wolfPosition.X) +
            Math.Abs(escaped.Y - wolfPosition.Y);
        Assert.True(escapedDistance > originalDistance);
    }

    [Theory]
    [InlineData(CritterSpecies.Trilobite, 9)]
    [InlineData(CritterSpecies.Nautilus, 9)]
    [InlineData(CritterSpecies.Fish, 9)]
    [InlineData(CritterSpecies.Newt, 8)]
    [InlineData(CritterSpecies.Crab, 9)]
    [InlineData(CritterSpecies.Monkey, 9)]
    [InlineData(CritterSpecies.Deer, 10)]
    [InlineData(CritterSpecies.Elk, 10)]
    [InlineData(CritterSpecies.Gazelle, 10)]
    public void WolfUsesTheMegaToadDiet(CritterSpecies prey, int expectedEnergy)
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 95);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.Wolf, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Fact]
    public void WolfPrefersOrdinaryPreyOverDangerousLandPredators()
    {
        var world = new SimulationWorld(1, 3, Terrain.Shallows, seed: 96);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Wolf, new GridPosition(0, 1));
        world.AddCritter(CritterSpecies.Deer, new GridPosition(0, 2));

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(0, world.GetCritterCount(CritterSpecies.Deer));
    }

    [Theory]
    [InlineData(CritterSpecies.Wolf, 3)]
    [InlineData(CritterSpecies.MegaToad, 6)]
    public void TherapsidDefendsItselfWhenAttacked(
        CritterSpecies predator,
        int movementSeconds)
    {
        Assert.True(SimulationWorld.CanEat(predator, CritterSpecies.Therapsid));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Therapsid, predator));

        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 97);
        world.SeasonsEnabled = false;
        world.AddCritter(predator, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 1));

        for (var tick = 0; tick < movementSeconds * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(predator));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Therapsid));
        Assert.Equal(
            9,
            Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy));
    }

    [Fact]
    public void MegaToadHuntsWolfButWolfDoesNotHuntMegaToad()
    {
        Assert.True(SimulationWorld.CanEat(CritterSpecies.MegaToad, CritterSpecies.Wolf));
        Assert.False(SimulationWorld.CanEat(CritterSpecies.Wolf, CritterSpecies.MegaToad));

        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 98);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaToad, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Wolf, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaToad));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(
            9,
            Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy));
    }

    [Fact]
    public void WolfReproductionCreatesAChargedHillDenInsteadOfAnImmediatePup()
    {
        var world = new SimulationWorld(7, 1, Terrain.Plains, seed: 101);
        world.SeasonsEnabled = false;
        var hill = new GridPosition(5, 0);
        world.SetTerrain(hill, Terrain.Hills);
        world.AddCritter(CritterSpecies.Wolf, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(1, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(2, 0));

        var wolfNutrition = CritterNutritions.Get(CritterSpecies.Wolf);
        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritter(0).Energy < wolfNutrition.ReproductionThreshold;
            tick++)
        {
            world.AdvanceOneTick();
        }
        foreach (var prey in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Crab)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(prey));
        }

        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond && world.WolfDenCount == 0;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(1, world.WolfDenCount);
        Assert.Equal(1, world.GetWolfDenCharges(hill));
    }

    [Fact]
    public void MovingPreyConsumesOneDenChargeAndSpawnsOneWolf()
    {
        var world = new SimulationWorld(5, 1, Terrain.Plains, seed: 102);
        world.SeasonsEnabled = false;
        var den = new GridPosition(2, 0);
        Assert.True(world.AddWolfDenCharge(den));
        world.AddCritter(CritterSpecies.Deer, new GridPosition(3, 0));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.NotEqual(new GridPosition(3, 0), world.GetCritter(0).Position);
        Assert.Equal(0, world.GetWolfDenCharges(den));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(1, world.GetWolfDenAssociatedWolfCount(den));

        var wolf = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.Wolf);
        Assert.True(world.RemoveCritterAt(wolf.Position));
        Assert.Null(world.GetWolfDenCharges(den));
        Assert.Equal(0, world.WolfDenCount);
    }

    [Fact]
    public void BuildingToolOperationsPlaceAndRemoveAChargedWolfDen()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains, seed: 103);
        var position = new GridPosition(0, 0);
        var ocean = new GridPosition(1, 0);
        var shallows = new GridPosition(2, 0);
        world.SetTerrain(ocean, Terrain.Ocean);
        world.SetTerrain(shallows, Terrain.Shallows);

        Assert.True(world.TryPlaceWolfDen(position));
        Assert.Equal(1, world.GetWolfDenCharges(position));
        Assert.False(world.TryPlaceWolfDen(position));
        Assert.False(world.TryPlaceWolfDen(ocean));
        Assert.False(world.TryPlaceWolfDen(shallows));
        Assert.False(world.AddWolfDenCharge(shallows));
        Assert.True(world.RemoveWolfDenAt(position));
        Assert.Null(world.GetWolfDenCharges(position));
        Assert.False(world.RemoveWolfDenAt(position));
    }

    [Fact]
    public void WolfDenChargesAreCappedAtFive()
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 104);
        var den = new GridPosition(0, 0);
        Assert.True(world.TryPlaceWolfDen(den));

        for (var charge = 1; charge < SimulationWorld.MaximumWolfDenCharges; charge++)
        {
            Assert.True(world.AddWolfDenCharge(den));
        }

        Assert.Equal(SimulationWorld.MaximumWolfDenCharges, world.GetWolfDenCharges(den));
        Assert.False(world.AddWolfDenCharge(den));
        Assert.Equal(SimulationWorld.MaximumWolfDenCharges, world.GetWolfDenCharges(den));
    }

    [Fact]
    public void WolfDensPassivelyLoseOneChargeEveryTwoMinutes()
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 104);
        var den = new GridPosition(0, 0);
        Assert.True(world.TryPlaceWolfDen(den));
        Assert.True(world.AddWolfDenCharge(den));

        for (var tick = 1; tick < SimulationWorld.WolfDenChargeDecayTicks; tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.Equal(2, world.GetWolfDenCharges(den));

        world.AdvanceOneTick();

        Assert.Equal(1, world.GetWolfDenCharges(den));
    }

    [Fact]
    public void PreylessWolfDenDisappearsAfterItsLastChargeDecays()
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains, seed: 104);
        var den = new GridPosition(0, 0);
        Assert.True(world.TryPlaceWolfDen(den));

        for (var tick = 0; tick < SimulationWorld.WolfDenChargeDecayTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Null(world.GetWolfDenCharges(den));
        Assert.Equal(0, world.WolfDenCount);
    }

    [Fact]
    public void ReproducingAtAFullWolfDenSpawnsAWolfWithoutConsumingCharges()
    {
        var world = new SimulationWorld(20, 1, Terrain.Plains, seed: 105);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        var den = new GridPosition(0, 0);
        Assert.True(world.TryPlaceWolfDen(den));
        for (var charge = 1; charge < SimulationWorld.MaximumWolfDenCharges; charge++)
        {
            Assert.True(world.AddWolfDenCharge(den));
        }

        world.AddCritter(CritterSpecies.Wolf, new GridPosition(10, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(11, 0));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(12, 0));

        var wolfNutrition = CritterNutritions.Get(CritterSpecies.Wolf);
        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritter(0).Energy < wolfNutrition.ReproductionThreshold;
            tick++)
        {
            world.AdvanceOneTick();
        }
        foreach (var prey in Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Where(critter => critter.Species is CritterSpecies.Crab)
            .Select(critter => critter.Position)
            .ToArray())
        {
            Assert.True(world.RemoveCritterAt(prey));
        }

        for (var tick = 0;
            tick < 2 * 60 * SimulationWorld.TicksPerSecond &&
                world.GetCritterCount(CritterSpecies.Wolf) == 1;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(SimulationWorld.MaximumWolfDenCharges, world.GetWolfDenCharges(den));
    }

    [Theory]
    [InlineData(CritterSpecies.Fish, 8)]
    [InlineData(CritterSpecies.Crab, 8)]
    [InlineData(CritterSpecies.Newt, 7)]
    [InlineData(CritterSpecies.Nautilus, 8)]
    [InlineData(CritterSpecies.Therapsid, 9)]
    [InlineData(CritterSpecies.Monkey, 8)]
    [InlineData(CritterSpecies.Wolf, 9)]
    [InlineData(CritterSpecies.Deer, 9)]
    [InlineData(CritterSpecies.Elk, 9)]
    [InlineData(CritterSpecies.Gazelle, 9)]
    public void SeaScorpionHuntsItsAquaticPrey(CritterSpecies prey, int expectedEnergy)
    {
        var world = new SimulationWorld(1, 2, Terrain.Shallows, seed: 81);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SeaScorpion));
        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(expectedEnergy, world.GetCritter(0).Energy);
    }

    [Theory]
    [InlineData(CritterSpecies.Deer)]
    [InlineData(CritterSpecies.Elk)]
    [InlineData(CritterSpecies.Gazelle)]
    public void SeaScorpionHuntsGrazersThatWanderOntoBeaches(CritterSpecies prey)
    {
        var world = new SimulationWorld(1, 2, Terrain.Beach, seed: 82);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.SeaScorpion));
        Assert.Equal(0, world.GetCritterCount(prey));
    }

    [Fact]
    public void SeaScorpionOccupiesSaltwaterAndBeachButNotInland()
    {
        foreach (var terrain in new[]
            { Terrain.DeepOcean, Terrain.Ocean, Terrain.Shallows, Terrain.Beach })
        {
            Assert.True(new SimulationWorld(1, 1, terrain)
                .TryAddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 0)));
        }

        Assert.False(new SimulationWorld(1, 1, Terrain.Plains)
            .TryAddCritter(CritterSpecies.SeaScorpion, new GridPosition(0, 0)));
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
    [InlineData(Terrain.Lowlands)]
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
        Assert.Equal(
            terrain is Terrain.Ice,
            CritterHabitats.CanOccupy(
                CritterHabitat.LandDweller,
                terrain,
                SurfaceWaterKind.FreshwaterLake));
    }

    [Fact]
    public void EveryCritterSpeciesCanOccupyAnIceSheet()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var world = new SimulationWorld(1, 1, Terrain.Ice);
            Assert.True(world.TryAddCritter(species, new GridPosition(0, 0)), species.ToString());
        }
    }

    [Fact]
    public void CritterInValidHabitatWillNotEnterInvalidTerrain()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 106);
        world.SeasonsEnabled = false;
        var start = new GridPosition(0, 0);
        world.SetTerrain(new GridPosition(1, 0), Terrain.Ocean);
        world.AddCritter(CritterSpecies.Deer, start);

        for (var tick = 0; tick < 3 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(start, world.GetCritter(0).Position);
    }

    [Theory]
    [InlineData(CritterSpecies.Newt)]
    [InlineData(CritterSpecies.MegaToad)]
    [InlineData(CritterSpecies.Therapsid)]
    [InlineData(CritterSpecies.Monkey)]
    [InlineData(CritterSpecies.Deer)]
    [InlineData(CritterSpecies.Elk)]
    [InlineData(CritterSpecies.Gazelle)]
    [InlineData(CritterSpecies.Wolf)]
    [InlineData(CritterSpecies.Crab)]
    public void LandSpeciesCanOccupyLowlandsCanyonsAndTrenches(CritterSpecies species)
    {
        foreach (var terrain in new[] { Terrain.Lowlands, Terrain.Canyon, Terrain.Trench })
        {
            var world = new SimulationWorld(1, 1, terrain);
            Assert.True(world.TryAddCritter(species, new GridPosition(0, 0)));
        }
    }

    [Theory]
    [InlineData(Terrain.Lowlands)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Trench)]
    public void GazelleMovesOntoLowerDryTerrain(Terrain destinationTerrain)
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains, seed: 105);
        world.SeasonsEnabled = false;
        var destination = new GridPosition(1, 0);
        world.SetTerrain(destination, destinationTerrain);
        world.SetTerrain(new GridPosition(2, 0), Terrain.Mountain);
        world.AddCritter(CritterSpecies.Gazelle, new GridPosition(0, 0));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(destination, world.GetCritter(0).Position);
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
