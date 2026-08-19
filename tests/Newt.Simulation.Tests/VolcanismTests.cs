using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VolcanismTests
{
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
    public void VolcanoRaisesMountainShouldersAndAHillSkirt()
    {
        var world = CreateLandWorld();
        var vent = new GridPosition(7, 7);

        Volcanism.SpawnVolcano(world, vent, VolcanoState.Dormant);

        var nearby = Enumerable.Range(4, 7)
            .SelectMany(y => Enumerable.Range(4, 7).Select(x => new GridPosition(x, y)))
            .ToArray();
        Assert.True(nearby.Count(position => world.GetTerrain(position) is Terrain.Mountain) >= 9);
        Assert.Contains(nearby, position => world.GetTerrain(position) is Terrain.Hills);
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

        Volcanism.MakeExtinct(world, parent, spawnSuccessor: true);

        Assert.Equal(VolcanoState.Extinct, parent.State);
        Assert.Equal(1, world.VolcanoCount);
        Assert.Equal(Terrain.Mountain, world.GetTerrain(parentPosition));
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
