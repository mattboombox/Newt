using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MegaSpiderTests
{
    [Fact]
    public void MegaSpiderEvolvesFromSeaScorpionAndDevolvesBack()
    {
        Assert.Equal(1, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.SeaScorpion));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(
            CritterSpecies.SeaScorpion,
            out var evolved));
        Assert.Equal(CritterSpecies.MegaSpider, evolved);
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(evolved, out var devolved));
        Assert.Equal(CritterSpecies.SeaScorpion, devolved);
    }

    [Fact]
    public void FirstReproductionBuildsOneWebInsteadOfProducingOffspring()
    {
        var world = CreateFedSpiderWorld(seed: 201);

        AdvanceUntil(world, () => world.MegaSpiderWebCount == 1);

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaSpider));
        Assert.Single(FindWebs(world));
        Assert.Equal(1, world.GetMegaSpiderWebAssociatedSpiderCount(FindWebs(world).Single()));
    }

    [Fact]
    public void RemovedWebIsRebuiltAtTheNextReproduction()
    {
        var world = CreateFedSpiderWorld(seed: 202);
        AdvanceUntil(world, () => world.MegaSpiderWebCount == 1);
        var firstWeb = FindWebs(world).Single();
        Assert.True(world.RemoveMegaSpiderWebAt(firstWeb));

        AddPreyNearSpider(world, 1);
        AdvanceUntil(world, () => world.MegaSpiderWebCount == 1);

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaSpider));
        Assert.Equal(1, world.MegaSpiderWebCount);
    }

    [Fact]
    public void WebIsRemovedAsSoonAsItsSpiderDies()
    {
        var world = CreateFedSpiderWorld(seed: 203);
        AdvanceUntil(world, () => world.MegaSpiderWebCount == 1);
        var spider = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.MegaSpider);

        Assert.True(world.RemoveCritterAt(spider.Position));

        Assert.Equal(0, world.MegaSpiderWebCount);
    }

    [Fact]
    public void LandAndShallowsSupportWebsButOpenOceanDoesNot()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains, seed: 204);
        world.SetTerrain(new GridPosition(1, 0), Terrain.Shallows);
        world.SetTerrain(new GridPosition(2, 0), Terrain.Ocean);
        var spiderId = world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));

        Assert.True(world.TryCreateMegaSpiderWeb(spiderId, new GridPosition(1, 0)));
        Assert.True(world.RemoveMegaSpiderWebAt(new GridPosition(1, 0)));
        Assert.False(world.TryCreateMegaSpiderWeb(spiderId, new GridPosition(2, 0)));
    }

    [Fact]
    public void WebTrapsPreyAndOwnerReturnsToStoreItAsFood()
    {
        var world = new SimulationWorld(9, 1, Terrain.Plains, seed: 205);
        world.SeasonsEnabled = false;
        var spiderId = world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        var web = new GridPosition(4, 0);
        Assert.True(world.TryCreateMegaSpiderWeb(spiderId, web));
        var preyId = world.AddCritter(CritterSpecies.Deer, web);

        for (var tick = 0; tick < 3 * SimulationWorld.GetMovementIntervalTicks(CritterSpecies.Deer); tick++)
        {
            Assert.True(world.TryGetCritter(preyId, out var prey));
            Assert.Equal(web, prey.Position);
            Assert.True(world.IsCritterCaughtInMegaSpiderWeb(preyId));
            world.AdvanceOneTick();
        }

        AdvanceUntil(world, () => !world.TryGetCritter(preyId, out _));

        Assert.Equal((int)CritterBodySize.Large, world.GetMegaSpiderWebFood(web));
        Assert.True(world.TryGetCritter(spiderId, out var spider));
        Assert.Equal(web, spider.Position);
    }

    [Fact]
    public void MegaSpidersNeverBecomeCaughtInWebsOrEatEachOther()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 206);
        var ownerId = world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        var web = new GridPosition(1, 0);
        Assert.True(world.TryCreateMegaSpiderWeb(ownerId, web));
        var visitingSpiderId = world.AddCritter(CritterSpecies.MegaSpider, web);

        Assert.False(world.IsCritterCaughtInMegaSpiderWeb(visitingSpiderId));
        Assert.False(SimulationWorld.CanEat(
            CritterSpecies.MegaSpider,
            CritterSpecies.MegaSpider));
        Assert.All(
            Enum.GetValues<CritterSpecies>().Where(prey => prey is not CritterSpecies.MegaSpider),
            prey => Assert.True(SimulationWorld.CanEat(CritterSpecies.MegaSpider, prey)));
    }

    [Fact]
    public void StoredWebFoodOffsetsMetabolismOneUnitAtATime()
    {
        var world = new SimulationWorld(2, 1, Terrain.Plains, seed: 207);
        world.SeasonsEnabled = false;
        var spiderId = world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        var web = new GridPosition(1, 0);
        Assert.True(world.TryCreateMegaSpiderWeb(spiderId, web));
        var preyId = world.AddCritter(CritterSpecies.Deer, web);
        AdvanceUntil(world, () => !world.TryGetCritter(preyId, out _));
        var foodAfterCapture = world.GetMegaSpiderWebFood(web)!.Value;
        var energyAfterCapture = world.GetCritter(0).Energy;

        for (var tick = 0;
            tick < CritterNutritions.Get(CritterSpecies.MegaSpider).MetabolismIntervalTicks;
            tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.GetMegaSpiderWebFood(web) < foodAfterCapture);
        Assert.Equal(energyAfterCapture, world.GetCritter(0).Energy);
    }

    [Fact]
    public void ReturningSpiderShovesABlockerInsteadOfSharingItsTile()
    {
        var world = new SimulationWorld(7, 1, Terrain.Plains, seed: 208);
        world.SeasonsEnabled = false;
        var spiderId = world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        var blockerId = world.AddCritter(CritterSpecies.Newt, new GridPosition(1, 0));
        var web = new GridPosition(3, 0);
        Assert.True(world.TryCreateMegaSpiderWeb(spiderId, web));
        var preyId = world.AddCritter(CritterSpecies.Deer, web);

        AdvanceUntil(world, () =>
            world.TryGetCritter(spiderId, out var spider) && spider.Position != new GridPosition(0, 0));

        Assert.True(world.TryGetCritter(spiderId, out var movedSpider));
        Assert.True(world.TryGetCritter(blockerId, out var movedBlocker));
        Assert.True(world.TryGetCritter(preyId, out var trappedPrey));
        Assert.NotEqual(movedSpider.Position, movedBlocker.Position);
        Assert.NotEqual(movedSpider.Position, trappedPrey.Position);
        Assert.Equal(
            world.CritterCount,
            Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Select(critter => critter.Position)
                .Distinct()
                .Count());
    }

    [Fact]
    public void MegaSpiderFightsWolfInsteadOfEatingItOutright()
    {
        var world = new SimulationWorld(1, 2, Terrain.Plains, seed: 209);
        world.SeasonsEnabled = false;
        world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Wolf, new GridPosition(0, 1));

        for (var tick = 0; tick < 6 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            if (Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy) < 10)
            {
                break;
            }
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.MegaSpider));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Wolf));
        Assert.Equal(
            8,
            Enumerable.Range(0, world.CritterCount)
                .Select(world.GetCritter)
                .Sum(critter => critter.Energy));
    }

    [Theory]
    [InlineData(CritterSpecies.Wolf)]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.SeaScorpion)]
    [InlineData(CritterSpecies.MegaToad)]
    [InlineData(CritterSpecies.Squid)]
    [InlineData(CritterSpecies.Therapsid)]
    [InlineData(CritterSpecies.Ape)]
    public void MegaSpiderTargetsThatFightBackAreRecognizedAsPredators(CritterSpecies species)
    {
        Assert.True(SimulationWorld.IsPredator(species));
    }

    private static SimulationWorld CreateFedSpiderWorld(ulong seed)
    {
        var world = new SimulationWorld(4, 1, Terrain.Plains, seed);
        world.SeasonsEnabled = false;
        world.AdjustEvolutionChance(-CritterEvolution.MaximumChanceSteps);
        world.AddCritter(CritterSpecies.MegaSpider, new GridPosition(0, 0));
        // Stranded filter feeders cannot flee on land and provide enough food
        // without involving the predator-versus-predator combat rule.
        Assert.True(world.TrySpawnCritter(CritterSpecies.BaleenWhale, new GridPosition(1, 0)));
        Assert.True(world.TrySpawnCritter(CritterSpecies.BaleenWhale, new GridPosition(2, 0)));
        return world;
    }

    private static void AddPreyNearSpider(SimulationWorld world, int count)
    {
        var spider = Enumerable.Range(0, world.CritterCount)
            .Select(world.GetCritter)
            .Single(critter => critter.Species is CritterSpecies.MegaSpider);
        for (var offset = 1; offset < world.Width && count > 0; offset++)
        {
            var position = new GridPosition((spider.Position.X + offset) % world.Width, spider.Position.Y);
            if (!world.IsOccupied(position))
            {
                Assert.True(world.TrySpawnCritter(CritterSpecies.BaleenWhale, position));
                count--;
            }
        }
        Assert.Equal(0, count);
    }

    private static GridPosition[] FindWebs(SimulationWorld world) =>
        Enumerable.Range(0, world.Width)
            .Select(x => new GridPosition(x, 0))
            .Where(position => world.GetMegaSpiderWebFood(position) is not null)
            .ToArray();

    private static void AdvanceUntil(SimulationWorld world, Func<bool> condition)
    {
        for (var tick = 0; tick < 10_000 && !condition(); tick++)
        {
            world.AdvanceOneTick();
        }
        Assert.True(condition());
    }
}
