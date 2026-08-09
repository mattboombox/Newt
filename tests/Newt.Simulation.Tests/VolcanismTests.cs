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
        world.AddCritter(CritterSpecies.Ape, position);

        Assert.False(Volcanism.SpawnVolcano(world, position));
        Assert.Equal(0, world.VolcanoCount);
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
