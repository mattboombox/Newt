using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VolcanismTests
{
    [Fact]
    public void FlowBuildsMoreElevationAwayFromTheVent()
    {
        var world = CreateLandWorld();
        NaturalEvents.SetEnabled(world, false);
        world.SeasonsEnabled = false;
        var vent = new GridPosition(7, 7);
        var before = world.GetElevation(vent);
        Assert.True(Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant));
        var ventGain = world.GetElevation(vent) - before;
        var flow = new LavaFlowActivity(vent, 0, 2, world.Tick + 5) { Vent = vent };
        world.LavaFlows.Add(flow);
        for (var tick = 0; tick < 5; tick++) world.AdvanceOneTick();
        Assert.NotEqual(vent, flow.Position);
        var flankGain = world.GetElevation(flow.Position) - before;
        Assert.InRange(flankGain, 0.024f, 0.094f);
        Assert.True(flankGain > 2 * ventGain);
    }

    [Fact]
    public void LavaReturningToVentDoesNotApplyTheLargerFlankDeposit()
    {
        var world = CreateLandWorld();
        NaturalEvents.SetEnabled(world, false);
        world.SeasonsEnabled = false;
        var vent = new GridPosition(7, 7);
        Assert.True(Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant));
        for (var y = 0; y < world.Height; y++)
            for (var x = 0; x < world.Width; x++)
                if (new GridPosition(x, y) != vent) world.SetElevation(new(x, y), 0.9f);
        var before = world.GetElevation(vent);
        var flow = new LavaFlowActivity(new(6, 7), 0, 2, world.Tick + 5) { Vent = vent };
        world.LavaFlows.Add(flow);
        for (var tick = 0; tick < 5; tick++) world.AdvanceOneTick();
        Assert.Equal(vent, flow.Position);
        Assert.InRange(world.GetElevation(vent) - before, 0.003f, 0.01f);
    }

    [Fact]
    public void VolcanoStaysActiveLongerAndSendsLongerFlowsAcrossItsFlanks()
    {
        var world = CreateLandWorld();
        NaturalEvents.SetEnabled(world, false);
        world.SeasonsEnabled = false;
        var vent = new GridPosition(7, 7);
        Assert.True(Volcanism.SpawnVolcano(world, vent));
        var volcano = Assert.Single(world.Volcanoes);
        Assert.InRange(volcano.NextStateTick - world.Tick,
            40 * SimulationWorld.TicksPerSecond, 70 * SimulationWorld.TicksPerSecond);
        Assert.True(Volcanism.TriggerEruption(world, vent));
        Assert.All(world.LavaFlows, flow => Assert.InRange(flow.RemainingSteps, 10, 23));
        // Isolate one eruption so later eruptions cannot hide concentrated growth.
        volcano.NextEruptionTick = long.MaxValue;
        for (var tick = 0; tick < 120; tick++) world.AdvanceOneTick();
        Assert.Empty(world.LavaFlows);
        var raised = Enumerable.Range(0, world.Height).SelectMany(y =>
            Enumerable.Range(0, world.Width).Select(x => new GridPosition(x, y)))
            .Where(position => position != vent && world.GetElevation(position) > 0.2f).ToArray();
        Assert.True(raised.Length >= 10);
        Assert.True(raised.Max(world.GetElevation) > world.GetElevation(vent));
    }

    [Fact]
    public void LavaCoverRemainsSeparateFromRaisedTerrainAndBiome()
    {
        var world = CreateLandWorld();
        var vent = new GridPosition(7, 7);
        var elevation = world.GetElevation(vent);

        Assert.True(Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant));

        Assert.Equal(SurfaceCover.Lava, world.GetSurfaceCover(vent));
        Assert.True(world.GetElevation(vent) > elevation);
        Assert.True(Enum.IsDefined(world.GetBiome(vent)));
    }

    [Fact]
    public void VolcanoStartsAtOneLavaPointWithoutRaisingSurroundingLand()
    {
        var world = CreateLandWorld();
        var vent = new GridPosition(7, 7);

        var nearby = Enumerable.Range(4, 7)
            .SelectMany(y => Enumerable.Range(4, 7).Select(x => new GridPosition(x, y)))
            .ToArray();
        var elevations = nearby.ToDictionary(position => position, world.GetElevation);
        Assert.True(Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant));
        Assert.Equal(1, CountCover(world, SurfaceCover.Lava));
        Assert.InRange(world.GetElevation(vent) - elevations[vent], 0.003f, 0.01f);
        Assert.All(nearby.Where(position => position != vent), position =>
            Assert.Equal(elevations[position], world.GetElevation(position)));
        Assert.DoesNotContain(nearby, position => world.GetTerrain(position) is Terrain.Mountain);
    }

    [Fact]
    public void RepeatedEruptionsBuildVentGraduallyInsteadOfCreatingMountain()
    {
        var world = CreateLandWorld();
        var vent = new GridPosition(7, 7);
        Assert.True(Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant));
        for (var eruption = 0; eruption < 5; eruption++)
        {
            var before = world.GetElevation(vent);
            Assert.True(Volcanism.TriggerEruption(world, vent));
            Assert.InRange(world.GetElevation(vent) - before, 0.003f, 0.01f);
        }
        Assert.True(world.GetElevation(vent) < TerrainClassifier.MountainElevationThreshold);
        Assert.NotEqual(Terrain.Mountain, world.GetTerrain(vent));
    }

    [Fact]
    public void LavaFlowAdvancesOnlyAfterItsTickInterval()
    {
        var world = CreateLandWorld();
        var vent = new GridPosition(7, 7);
        Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant);
        Volcanism.TriggerEruption(world, vent);
        var initialLava = CountCover(world, SurfaceCover.Lava);

        for (var tick = 0; tick < 4; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(initialLava, CountCover(world, SurfaceCover.Lava));

        world.AdvanceOneTick();

        Assert.True(CountCover(world, SurfaceCover.Lava) > initialLava);
    }

    [Fact]
    public void LavaCoolsToStoneThenRevealsBiomeAgain()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        var biome = world.GetBiome(position);
        world.SetSurfaceCover(position, SurfaceCover.Lava, world.Tick + 1);

        world.AdvanceOneTick();

        Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(position));
        Assert.Equal(Biome.None, world.GetBiome(position));
        ClimateSystem.RebuildMoistureAndBiomes(world);
        Assert.Equal(Biome.None, world.GetBiome(position));
        world.SetSurfaceCover(position, SurfaceCover.Stone, world.Tick + 1);

        world.AdvanceOneTick();

        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(position));
        Assert.Equal(biome, world.GetBiome(position));
    }

    [Fact]
    public void RiverAcceleratesStoneRecovery()
    {
        var world = CreateLandWorld();
        var besideRiver = new GridPosition(5, 5);
        var dry = new GridPosition(11, 11);
        world.SetSurfaceCover(besideRiver, SurfaceCover.Stone, world.Tick + 100);
        world.SetSurfaceCover(dry, SurfaceCover.Stone, world.Tick + 100);
        world.SetSurfaceWater(new GridPosition(5, 6), SurfaceWaterKind.River);

        for (var tick = 0; tick < 25; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(besideRiver));
        Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(dry));
    }

    [Fact]
    public void DryLavaDoesNotRequestAFreshwaterRebuild()
    {
        var world = CreateLandWorld();

        Assert.True(Volcanism.DepositLava(world, new GridPosition(7, 7), 0.02f));

        Assert.False(world.VolcanicFreshwaterRefreshPending);
    }

    [Fact]
    public void LavaDestroysWolfDenAndStoredCharges()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        Assert.True(world.AddWolfDenCharge(position));

        Assert.True(Volcanism.DepositLava(world, position, 0.02f));

        Assert.Null(world.GetWolfDenCharges(position));
    }

    [Fact]
    public void LavaTouchingARiverRequestsAFreshwaterRebuild()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        world.SetSurfaceWater(position, SurfaceWaterKind.River);

        Assert.True(Volcanism.DepositLava(world, position, 0.02f));

        Assert.True(world.VolcanicFreshwaterRefreshPending);
    }

    [Fact]
    public void ExtinctVolcanoCanSpawnAdjacentActiveSuccessor()
    {
        var world = CreateLandWorld();
        var parentPosition = new GridPosition(7, 7);
        Volcanism.SpawnVolcano(world, parentPosition, VolcanoState.Dormant);
        var parent = world.Volcanoes.Single();
        var elevation = world.GetElevation(parentPosition);

        Volcanism.MakeExtinct(world, parent, spawnSuccessor: true);

        Assert.Equal(VolcanoState.Extinct, parent.State);
        Assert.Equal(1, world.VolcanoCount);
        Assert.Equal(elevation, world.GetElevation(parentPosition));
        Assert.NotEqual(Terrain.Mountain, world.GetTerrain(parentPosition));
        var successor = world.GetVolcano(0);
        Assert.Equal(VolcanoState.Active, successor.State);
        Assert.InRange(Math.Abs(successor.Position.X - parentPosition.X), 0, 1);
        Assert.InRange(Math.Abs(successor.Position.Y - parentPosition.Y), 0, 1);
    }

    [Fact]
    public void VolcanoToolOperationRefusesOccupiedTile()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        world.AddCritter(CritterSpecies.Monkey, position);

        Assert.False(Volcanism.SpawnVolcano(world, position));
        Assert.Equal(0, world.VolcanoCount);
    }

    [Fact]
    public void VolcanoToolOperationRefusesMegaWall()
    {
        var world = new SimulationWorld(8, 5, Terrain.Plains, seed: 38)
        {
            Body = WorldBody.RingWorld,
        };
        TerrainClassifier.RebuildAll(world);
        var wall = new GridPosition(4, 0);
        var elevation = world.GetElevation(wall);

        Assert.Equal(Terrain.MegaWall, world.GetTerrain(wall));
        Assert.False(Volcanism.SpawnVolcano(world, wall));
        Assert.Equal(0, world.VolcanoCount);
        Assert.Equal(elevation, world.GetElevation(wall));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(wall));
    }

    [Fact]
    public void StoneToolCoversOnlyClickedTileWithoutChangingElevation()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        var elevation = world.GetElevation(position);

        Assert.True(Volcanism.PlaceStone(world, position));

        Assert.Equal(SurfaceCover.Stone, world.GetSurfaceCover(position));
        Assert.Equal(elevation, world.GetElevation(position));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(new GridPosition(8, 7)));
    }

    [Fact]
    public void BiomeReclaimsToolPlacedStone()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        var biome = world.GetBiome(position);
        Volcanism.PlaceStone(world, position);

        for (var tick = 0; tick < 45 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(position));
        Assert.Equal(biome, world.GetBiome(position));
    }

    [Fact]
    public void LavaToolRaisesAndCoversOnlyClickedTile()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        var elevation = world.GetElevation(position);
        var neighbor = new GridPosition(8, 7);
        var neighborElevation = world.GetElevation(neighbor);

        Assert.True(Volcanism.PlaceLava(world, position));

        Assert.Equal(SurfaceCover.Lava, world.GetSurfaceCover(position));
        Assert.Equal(elevation + 0.03f, world.GetElevation(position), precision: 5);
        Assert.Equal(neighborElevation, world.GetElevation(neighbor));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(neighbor));
    }

    [Fact]
    public void TerrainCoverToolCanClearStoneOrLava()
    {
        var world = CreateLandWorld();
        var position = new GridPosition(7, 7);
        Volcanism.PlaceStone(world, position);

        Assert.True(Volcanism.ClearGeologicalCover(world, position));
        Assert.Equal(SurfaceCover.None, world.GetSurfaceCover(position));
    }

    private static SimulationWorld CreateLandWorld()
    {
        var world = new SimulationWorld(15, 15, Terrain.Plains, seed: 37);
        world.OceanSeed = new GridPosition(0, 0);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                world.SetElevation(new GridPosition(x, y), 0.2f);
            }
        }
        TerrainClassifier.RebuildAll(world);
        return world;
    }

    private static int CountCover(SimulationWorld world, SurfaceCover cover)
    {
        var count = 0;
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (world.GetSurfaceCover(new GridPosition(x, y)) == cover)
                {
                    count++;
                }
            }
        }
        return count;
    }
}
