using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class SandwormTests
{
    [Fact]
    public void SpawnNeedsTwoEmptyDesertTiles()
    {
        var world = CreateWorld(8, 3);
        var head = new GridPosition(3, 1);
        var tail = new GridPosition(4, 1);

        Assert.False(world.TrySpawnCritter(CritterSpecies.Sandworm, head));
        world.SetBiome(head, Biome.Desert);
        Assert.False(world.TrySpawnCritter(CritterSpecies.Sandworm, head));
        world.SetBiome(tail, Biome.Desert);
        world.AddCritter(CritterSpecies.Gazelle, tail);
        Assert.False(world.TrySpawnCritter(CritterSpecies.Sandworm, head));
        world.RemoveCritterAt(tail);

        Assert.True(world.TrySpawnCritter(CritterSpecies.Sandworm, head));
        Assert.True(world.TryGetSandwormSegment(head, out var first));
        Assert.True(world.TryGetSandwormSegment(tail, out var last));
        Assert.Equal(2, first.Length);
        Assert.Equal(0, first.Index);
        Assert.Equal(1, last.Index);
        Assert.Equal(first.Worm, last.Worm);
        Assert.True(world.RemoveCritterAt(head));
        Assert.False(world.TryGetSandwormSegment(tail, out _));
    }

    [Fact]
    public void DesertMountainsBlockSpawningAndMovement()
    {
        var world = CreateWorld(8, 3);
        var mountain = new GridPosition(4, 1);
        var head = new GridPosition(3, 1);
        var tail = new GridPosition(2, 1);
        world.SetTerrain(mountain, Terrain.Mountain);
        foreach (var tile in new[] { mountain, head, tail })
            world.SetBiome(tile, Biome.Desert);

        Assert.False(world.TrySpawnCritter(CritterSpecies.Sandworm, mountain));
        var prey = world.AddCritter(CritterSpecies.Custodian, mountain);
        var worm = world.AddCritter(CritterSpecies.Sandworm, head);
        for (var move = 0; move < 6; move++)
        {
            AdvanceOneMove(world);
            Assert.True(world.TryGetCritter(worm, out var critter));
            Assert.NotEqual(mountain, critter.Position);
            Assert.False(world.TryGetSandwormSegment(mountain, out _));
        }

        Assert.True(world.TryGetCritter(prey, out _));
    }

    [Fact]
    public void PursuesThroughConnectedDesertAndSwallowsWithoutCombat()
    {
        var world = CreateWorld(9, 5);
        GridPosition[] desert = [new(1, 2), new(2, 2), new(3, 2), new(3, 1), new(4, 1), new(5, 1)];
        foreach (var tile in desert)
            world.SetBiome(tile, Biome.Desert);

        var prey = world.AddCritter(CritterSpecies.Custodian, desert[^1]);
        var worm = world.AddCritter(CritterSpecies.Sandworm, desert[1]);
        for (var step = 2; step < desert.Length; step++)
        {
            AdvanceOneMove(world);
            Assert.True(world.TryGetCritter(worm, out var head));
            Assert.Equal(desert[step], head.Position);
            Assert.True(world.TryGetSandwormSegment(desert[step - 1], out var tail));
            Assert.Equal(1, tail.Index);
            Assert.Equal(step < desert.Length - 1, world.TryGetCritter(prey, out _));
        }
    }

    [Fact]
    public void GrowsEveryFiveMealsAndSplitsIntoTwoThreeSegmentWorms()
    {
        var world = CreateWorld(60, 3);
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new GridPosition(x, 1), Biome.Desert);

        var worm = world.AddCritter(CritterSpecies.Sandworm, new GridPosition(50, 1));
        for (var meal = 1; meal <= 40; meal++)
        {
            var next = new GridPosition(50 - meal, 1);
            var prey = world.AddCritter(CritterSpecies.Custodian, next);
            AdvanceOneMove(world);
            Assert.False(world.TryGetCritter(prey, out _));
            Assert.True(world.TryGetSandwormSegment(next, out var head));
            Assert.Equal(worm, head.Worm);
            Assert.Equal(meal < 40 ? 2 + meal / 5 : 3, head.Length);
        }

        Assert.Equal(2, world.GetCritterCount(CritterSpecies.Sandworm));
        var segments = Enumerable.Range(0, world.Width)
            .Select(x => world.TryGetSandwormSegment(new GridPosition(x, 1), out var segment) ? segment : default)
            .Where(segment => segment.Worm.IsValid)
            .GroupBy(segment => segment.Worm)
            .ToArray();
        Assert.Equal(2, segments.Length);
        Assert.All(segments, group => Assert.Equal(new[] { 0, 1, 2 }, group.Select(s => s.Index).Order().ToArray()));
    }

    [Fact]
    public void TrappedWormFlipsItsHeadAndTailAndBacksOut()
    {
        var world = CreateWorld(10, 3);
        for (var x = 1; x <= 7; x++)
            world.SetBiome(new GridPosition(x, 1), Biome.Desert);

        var worm = world.AddCritter(CritterSpecies.Sandworm, new GridPosition(6, 1));
        for (var x = 5; x >= 1; x--)
        {
            world.AddCritter(CritterSpecies.Custodian, new GridPosition(x, 1));
            AdvanceOneMove(world);
        }

        var deadEnd = new GridPosition(1, 1);
        Assert.Equal(deadEnd, world.GetCritter(0).Position);
        AdvanceOneMove(world);
        Assert.True(world.TryGetCritter(worm, out var flipped));
        Assert.Equal(new GridPosition(3, 1), flipped.Position);
        Assert.True(world.TryGetSandwormSegment(deadEnd, out var tail));
        Assert.Equal(2, tail.Index);
        Assert.False(world.IsOccupied(deadEnd));

        AdvanceOneMove(world);
        Assert.True(world.TryGetCritter(worm, out var escaped));
        Assert.Equal(new GridPosition(4, 1), escaped.Position);
    }

    [Fact]
    public void LosingDesertUnderAnySegmentKillsTheWormBeforeItMoves()
    {
        foreach (var changeTail in new[] { false, true })
        {
            var (world, head, tail, worm) = CreateHazardWorld();
            world.SetBiome(changeTail ? tail : head, Biome.Grassland);
            world.AdvanceOneTick();
            AssertSandwormRemains(world, head, tail, worm);
        }
    }

    [Fact]
    public void LavaOnTheHeadOrTailLeavesAWhaleFallAtEachSegment()
    {
        foreach (var hitTail in new[] { false, true })
        {
            var (world, head, tail, worm) = CreateHazardWorld();
            Assert.True(Volcanism.PlaceLava(world, hitTail ? tail : head));
            AssertSandwormRemains(world, head, tail, worm);
        }
    }

    [Fact]
    public void ACraterCanKillASandwormByHittingItsTail()
    {
        var (world, head, tail, worm) = CreateHazardWorld();
        Impacts.CreateMeteorImpact(world, new GridPosition(tail.X + 2, tail.Y), 0f);
        for (var tick = 0; tick < 20 && world.TryGetCritter(worm, out _); tick++) Impacts.Advance(world);
        AssertSandwormRemains(world, head, tail, worm);
    }

    [Fact]
    public void ATsunamiCanKillASandwormByReachingItsTail()
    {
        var (world, head, tail, worm) = CreateHazardWorld();
        var ocean = new GridPosition(tail.X + 1, tail.Y);
        world.SetTerrain(ocean, Terrain.Ocean);
        world.SetElevation(ocean, -0.2f);
        Assert.True(Tsunamis.Create(world, ocean, 0f));
        world.AdvanceOneTick();
        world.AdvanceOneTick();
        AssertSandwormRemains(world, head, tail, worm);
    }

    [Fact]
    public void ADesertWormCanConvertWithoutChangingIdentity()
    {
        var world = CreateWorld(8, 3, seed: 137);
        var head = new GridPosition(4, 1);
        var tail = new GridPosition(5, 1);
        world.SetBiome(head, Biome.Desert);
        world.SetBiome(tail, Biome.Desert);
        Assert.True(world.TrySpawnCritter(CritterSpecies.Worm, head));
        var id = world.GetCritter(0).Id;

        for (var tick = 0; tick < SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Worm); tick++)
            world.AdvanceOneTick();

        Assert.True(world.TryGetCritter(id, out var converted));
        Assert.Equal(CritterSpecies.Sandworm, converted.Species);
        Assert.Equal(head, converted.Position);
        Assert.True(world.TryGetSandwormSegment(tail, out var segment));
        Assert.Equal(id, segment.Worm);
        Assert.Equal(2, segment.Length);
    }

    [Fact]
    public void LargerSandwormSwallowsSmallerWormsByTheTailAndKeepsHalfMeals()
    {
        var world = CreateWorld(62, 7);
        var smaller = new List<CritterId>();
        foreach (var y in new[] { 3, 5 })
        {
            for (var x = 25; x <= 31; x++)
                world.SetBiome(new GridPosition(x, y), Biome.Desert);
            smaller.Add(world.AddCritter(CritterSpecies.Sandworm, new GridPosition(30, y)));
            for (var x = 29; x >= 25; x--)
                FeedSandworm(world, new GridPosition(x, y));
            for (var x = 28; x <= 31; x++)
                world.SetBiome(new GridPosition(x, y), Biome.Grassland);
        }

        for (var x = 27; x <= 58; x++)
            world.SetBiome(new GridPosition(x, 1), Biome.Desert);
        var ticksPerMove = SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Sandworm);
        for (var tick = 1; tick < ticksPerMove; tick++)
            world.AdvanceOneTick();
        var hunter = world.AddCritter(CritterSpecies.Sandworm, new GridPosition(57, 1));
        for (var x = 56; x >= 27; x--)
            FeedSandworm(world, new GridPosition(x, 1));

        foreach (var y in new[] { 3, 5 })
        {
            var tail = new GridPosition(27, y);
            var approach = new GridPosition(27, y - 1);
            // Open the passage just before the hunter moves so it can block
            // the smaller worm's escape before the smaller worm's next turn.
            for (var tick = 1; tick < ticksPerMove; tick++)
                world.AdvanceOneTick();
            world.SetBiome(approach, Biome.Desert);
            world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(hunter, out var approaching));
            Assert.Equal(approach, approaching.Position);
            Assert.True(world.TryGetSandwormSegment(tail, out var target));
            Assert.Equal(2, target.Index);
            AdvanceOneMove(world);
            Assert.True(world.TryGetCritter(hunter, out var arrived));
            Assert.Equal(tail, arrived.Position);
            Assert.False(world.TryGetCritter(target.Worm, out _));
            if (y == 3)
                world.SetBiome(new GridPosition(26, y), Biome.Grassland);
        }

        Assert.All(smaller, id => Assert.False(world.TryGetCritter(id, out _)));
        foreach (var y in new[] { 3, 5 })
            for (var x = 25; x <= 27; x++)
                Assert.Equal(10, world.GetWhaleFallNutrition(new GridPosition(x, y)));

        var firstMeal = new GridPosition(26, 5);
        FeedSandworm(world, firstMeal);
        Assert.True(world.TryGetSandwormSegment(firstMeal, out var first));
        Assert.Equal(8, first.Length);
        var secondMeal = new GridPosition(25, 5);
        FeedSandworm(world, secondMeal);
        Assert.True(world.TryGetSandwormSegment(secondMeal, out var second));
        Assert.Equal(9, second.Length);
    }

    [Fact]
    public void StaysInDesertWithoutLosingEnergy()
    {
        var world = CreateWorld(8, 4);
        GridPosition[] desert = [new(2, 1), new(3, 1), new(2, 2), new(3, 2)];
        foreach (var tile in desert)
            world.SetBiome(tile, Biome.Desert);
        var worm = world.AddCritter(CritterSpecies.Sandworm, desert[0]);
        var initialEnergy = world.GetCritter(0).Energy;
        var moves = CritterNutrition.MetabolismIntervalSeconds * 2 + 1;
        for (var i = 0; i < moves; i++)
        {
            AdvanceOneMove(world);
            Assert.True(world.TryGetCritter(worm, out var head));
            Assert.Contains(head.Position, desert);
            Assert.Equal(initialEnergy, head.Energy);
        }
    }

    private static (SimulationWorld World, GridPosition Head, GridPosition Tail, CritterId Worm) CreateHazardWorld()
    {
        var world = CreateWorld(30, 10);
        var head = new GridPosition(12, 4);
        var tail = new GridPosition(13, 4);
        foreach (var tile in new[] { head, tail })
        {
            world.SetElevation(tile, 0.2f);
            world.SetBiome(tile, Biome.Desert);
        }
        return (world, head, tail, world.AddCritter(CritterSpecies.Sandworm, head));
    }

    private static void AssertSandwormRemains(SimulationWorld world, GridPosition head, GridPosition tail, CritterId id)
    {
        Assert.False(world.TryGetCritter(id, out _));
        foreach (var tile in new[] { head, tail })
        {
            Assert.False(world.TryGetSandwormSegment(tile, out _));
            Assert.Equal(10, world.GetWhaleFallNutrition(tile));
        }
    }

    private static void FeedSandworm(SimulationWorld world, GridPosition tile)
    {
        var prey = world.AddCritter(CritterSpecies.Custodian, tile);
        AdvanceOneMove(world);
        Assert.False(world.TryGetCritter(prey, out _));
    }

    private static SimulationWorld CreateWorld(int width, int height, ulong seed = 1)
    {
        var world = new SimulationWorld(width, height, Terrain.Plains, seed) { SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        return world;
    }

    private static void AdvanceOneMove(SimulationWorld world)
    {
        for (var tick = 0; tick < SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Sandworm); tick++)
            world.AdvanceOneTick();
    }
}
