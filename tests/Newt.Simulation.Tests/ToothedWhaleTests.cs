using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ToothedWhaleTests
{
    [Fact]
    public void WhaleTracksMovingPreyBeyondDetectionRangeInsteadOfSwitchingToCloserFood()
    {
        var world = new SimulationWorld(40, 1, Terrain.Ocean, seed: 51);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        var preyPosition = new GridPosition(6, 0);
        Assert.True(world.TryPlaceTeleporter(preyPosition));
        var preyId = world.AddCritter(CritterSpecies.Squid, preyPosition);
        Assert.Equal(preyPosition, world.FindToothedWhalePrey(0, null));
        var distantPosition = new GridPosition(12, 0);
        Assert.True(world.TryPlaceTeleporter(distantPosition));
        Assert.True(world.TryActivateTeleporterAt(preyPosition));
        Assert.True(world.TryGetCritter(preyId, out var movedPrey));
        Assert.True(movedPrey.Position.X > SimulationWorld.ToothedWhalePerceptionRadius);
        var fishPosition = new GridPosition(1, 0);
        world.AddCritter(CritterSpecies.Fish, fishPosition);
        Assert.Equal(movedPrey.Position, world.FindToothedWhalePrey(0, null));
        // Removing the target compacts the critter arrays; its replacement is not the remembered prey.
        world.RemoveCritterAt(movedPrey.Position);
        Assert.Equal(fishPosition, world.FindToothedWhalePrey(0, null));
    }

    [Fact]
    public void WhaleContinuesCombatAfterFirstExchange()
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 5103);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Squid, new GridPosition(0, 1));
        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.Equal(2, world.CritterCount);
        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond && world.CritterCount == 2; tick++)
            world.AdvanceOneTick();
        Assert.Equal(1, world.CritterCount);
        Assert.Equal(3, SimulationWorld.GetCombatDamage(CritterSpecies.ToothedWhale));
        Assert.Equal(1, SimulationWorld.GetCombatDamage(CritterSpecies.BaleenWhale));
    }

    [Fact]
    public void ToothedWhaleIsFourthTherapsidEvolutionBranch()
    {
        Assert.Equal(4, CritterEvolution.GetEvolvedSpeciesCount(CritterSpecies.Therapsid));
        Assert.True(CritterEvolution.TryGetEvolvedSpecies(
            CritterSpecies.Therapsid,
            branchIndex: 3,
            out var evolved));
        Assert.Equal(CritterSpecies.ToothedWhale, evolved);
        Assert.True(CritterEvolution.TryGetDevolvedSpecies(evolved, out var ancestor));
        Assert.Equal(CritterSpecies.Therapsid, ancestor);
    }

    [Fact]
    public void ToothedWhaleLivesInSaltwaterButNotOnLandOrInFreshwater()
    {
        foreach (var terrain in new[] { Terrain.DeepOcean, Terrain.Ocean, Terrain.Shallows })
        {
            var world = new SimulationWorld(1, 1, terrain, seed: 5101);
            Assert.True(world.TryAddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0)));
        }

        var land = new SimulationWorld(1, 1, Terrain.Plains, seed: 5102);
        Assert.False(land.TryAddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0)));
        land.SetSurfaceWater(new GridPosition(0, 0), SurfaceWaterKind.FreshwaterLake);
        Assert.False(land.TryAddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0)));
    }

    [Fact]
    public void ToothedWhaleHuntsOceanPredatorsAndAdjacentNautilusesAndCrabs()
    {
        var expectedPrey = new HashSet<CritterSpecies>
        {
            CritterSpecies.SeaScorpion,
            CritterSpecies.Nautilus,
            CritterSpecies.Fish,
            CritterSpecies.Squid,
            CritterSpecies.ApeSailor,
            CritterSpecies.Crab,
            CritterSpecies.MegaToad,
            CritterSpecies.MegaSpider,
            CritterSpecies.Therapsid,
            CritterSpecies.Ape,
            CritterSpecies.ApeWarrior,
            CritterSpecies.ApeChieftain,
            CritterSpecies.ApeScholar,
            CritterSpecies.ApeFarmer,
            CritterSpecies.ApeLumberjack,
            CritterSpecies.Deer,
            CritterSpecies.Elk,
            CritterSpecies.Gazelle,
            CritterSpecies.Wolf,
        };

        Assert.Equal(
            expectedPrey,
            Enum.GetValues<CritterSpecies>()
                .Where(prey => SimulationWorld.CanEat(CritterSpecies.ToothedWhale, prey))
                .ToHashSet());
        Assert.False(SimulationWorld.CanPursuePreyAtDistance(
            CritterSpecies.ToothedWhale,
            CritterSpecies.Crab,
            distance: 2));
        Assert.False(SimulationWorld.CanEat(
            CritterSpecies.ToothedWhale,
            CritterSpecies.Jellyfish));
    }

    [Fact]
    public void ToothedWhaleTakesLargeLandPreyOnlyFromShallows()
    {
        var ocean = new SimulationWorld(2, 1, Terrain.Ocean, seed: 5104);
        ocean.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(ocean, false);
        ocean.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        Assert.True(ocean.TrySpawnCritter(CritterSpecies.Deer, new GridPosition(1, 0)));

        var shallows = new SimulationWorld(2, 1, Terrain.Shallows, seed: 5104);
        shallows.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(shallows, false);
        shallows.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        shallows.AddCritter(CritterSpecies.Deer, new GridPosition(1, 0));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            ocean.AdvanceOneTick();
            shallows.AdvanceOneTick();
        }

        Assert.Equal(1, ocean.GetCritterCount(CritterSpecies.Deer));
        Assert.Equal(0, shallows.GetCritterCount(CritterSpecies.Deer));
    }

    [Fact]
    public void ToothedWhalePursuesAndFightsSquid()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 5103);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Squid, new GridPosition(1, 0));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ToothedWhale));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        var damage = CritterNutritions.Get(CritterSpecies.ToothedWhale).InitialEnergy +
            CritterNutritions.Get(CritterSpecies.Squid).InitialEnergy -
            world.GetCritter(0).Energy - world.GetCritter(1).Energy;
        Assert.InRange(damage, 2, 3);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    [InlineData(19, true)]
    public void ToothedWhaleTargetsNautilusOnlyWhenAdjacentIncludingWorldSeam(
        int preyX, bool shouldTarget)
    {
        var world = new SimulationWorld(20, 1, Terrain.Ocean, seed: 5103);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        var prey = new GridPosition(preyX, 0);
        world.AddCritter(CritterSpecies.Nautilus, prey);

        var target = world.FindHunterPrey(
            0, CritterSpecies.ToothedWhale, SimulationWorld.ToothedWhalePerceptionRadius, null);

        Assert.Equal(shouldTarget ? prey : (GridPosition?)null, target);
    }

    [Theory]
    [InlineData(CritterSpecies.Nautilus)]
    [InlineData(CritterSpecies.Fish)]
    public void ToothedWhaleCanConsumeAdjacentMarinePrey(CritterSpecies prey)
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 42);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.Equal(0, world.GetCritterCount(prey));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ToothedWhale));
    }

    [Fact]
    public void ToothedWhaleMovesEveryFourSecondsAndPerceivesSevenTiles()
    {
        Assert.Equal(
            4 * SimulationWorld.TicksPerSecond,
            SimulationWorld.GetMovementIntervalTicks(CritterSpecies.ToothedWhale));
        Assert.Equal(7, SimulationWorld.ToothedWhalePerceptionRadius);
    }
}
