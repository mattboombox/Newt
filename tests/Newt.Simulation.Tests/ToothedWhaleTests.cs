using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class ToothedWhaleTests
{
    [Fact]
    public void WhaleAttacksAdjacentPreyBeforeShovingPlankton()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var world = new SimulationWorld(5, 3, Terrain.Ocean, (ulong)seed);
            world.AddCritter(CritterSpecies.ToothedWhale, new(2, 1));
            var prey = new GridPosition(2, 0);
            world.AddCritter(CritterSpecies.Squid, prey);
            foreach (var position in new[] { new GridPosition(1, 1), new GridPosition(3, 1), new GridPosition(2, 2) })
                world.AddCritter(CritterSpecies.Plankton, position);
            var destination = (GridPosition?)typeof(SimulationWorld).GetMethod("TryMoveToothedWhale",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(world, [0, null]);
            Assert.Equal(prey, destination);
            Assert.Equal(new GridPosition(2, 1), world.GetCritter(0).Position);
        }
    }

    [Fact]
    public void WhaleShovesPlanktonTowardDistantPreyInsteadOfWandering()
    {
        for (var seed = 1; seed <= 40; seed++)
        {
            var world = new SimulationWorld(10, 3, Terrain.Ocean, (ulong)seed);
            world.AddCritter(CritterSpecies.ToothedWhale, new(2, 1));
            world.AddCritter(CritterSpecies.Squid, new(5, 1));
            foreach (var position in new[] { new GridPosition(1, 1), new GridPosition(3, 1),
                new GridPosition(2, 0), new GridPosition(2, 2) })
                world.AddCritter(CritterSpecies.Plankton, position);
            typeof(SimulationWorld).GetMethod("TryMoveToothedWhale",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(world, [0, null]);
            Assert.Equal(new GridPosition(3, 1), world.GetCritter(0).Position);
        }
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void WhalesReproduceOnlyAtFullEnergy(CritterSpecies species)
    {
        var world = new SimulationWorld(10, 10, Terrain.Ocean, seed: 17);
        NaturalEvents.SetEnabled(world, false);
        world.SeasonsEnabled = false;
        world.AddCritter(species, new(5, 5));
        var nutrition = CritterNutritions.Get(species);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
        Assert.Equal(nutrition.MaximumEnergy, nutrition.ReproductionThreshold);
        energies[0] = nutrition.MaximumEnergy - 1;
        Assert.False(world.GetCritter(0).CanReproduce);
        world.AdvanceOneTick();
        Assert.Equal(1, world.CritterCount);
        energies[0] = nutrition.MaximumEnergy;
        Assert.True(world.GetCritter(0).CanReproduce);
        world.AdvanceOneTick();
        Assert.Equal(2, world.CritterCount);
        Assert.Equal(nutrition.MaximumEnergy - nutrition.ReproductionCost, world.GetCritter(0).Energy);
    }

    [Fact]
    public void NewlyEvolvedWhaleCrossesLandAtNormalSpeedToReachOcean()
    {
        var world = new SimulationWorld(1, 6, Terrain.Plains, seed: 51);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.SetTerrain(new GridPosition(0, 5), Terrain.Ocean);
        var id = world.AddCritter(CritterSpecies.Therapsid, new GridPosition(0, 0));
        typeof(SimulationWorld).GetMethod("ChangeCritterSpecies",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(world, new object[] { 0, CritterSpecies.ToothedWhale, false, false });
        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var first));
        Assert.Equal(new GridPosition(0, 1), first.Position);
        for (var tick = 0; tick < 4 * SimulationWorld.GetMovementIntervalTicks(CritterSpecies.ToothedWhale); tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(id, out var recovered));
        Assert.Equal(new GridPosition(0, 5), recovered.Position);
    }

    [Theory]
    [InlineData(CritterSpecies.ToothedWhale)]
    [InlineData(CritterSpecies.BaleenWhale)]
    public void WhalesStartWithMoreEnergyAndHaveLargerReserves(CritterSpecies species)
    {
        var nutrition = CritterNutritions.Get(species);
        Assert.Equal(10, nutrition.InitialEnergy);
        Assert.Equal(30, nutrition.MaximumEnergy);
    }

    [Theory]
    [InlineData(CritterSpecies.ApeSailor)]
    [InlineData(CritterSpecies.ApeTraderSailor)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    public void WhaleTargetsSailingApesInOcean(CritterSpecies species)
    {
        var world = new SimulationWorld(20, 1, Terrain.Ocean, seed: 51);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        var prey = new GridPosition(3, 0);
        Assert.True(world.TrySpawnCritter(species, prey));
        if (species is CritterSpecies.ApeChieftain)
        {
            var returning = (Dictionary<int, CritterSpecies?>)typeof(SimulationWorld).GetField("_returningWarSailors",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
            returning[world.GetCritter(1).Id.Value] = null;
        }
        Assert.Equal(prey, world.FindToothedWhalePrey(0, null));
    }

    [Fact]
    public void WhaleTracksMovingPreyBeyondDetectionRangeInsteadOfSwitchingToCloserCombatPrey()
    {
        var world = new SimulationWorld(40, 1, Terrain.Ocean, seed: 51);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
        energies[0] = 19;
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
        world.AddCritter(CritterSpecies.Squid, fishPosition);
        Assert.Equal(movedPrey.Position, world.FindToothedWhalePrey(0, null));
        // Removing the target compacts the critter arrays; its replacement is not the remembered prey.
        world.RemoveCritterAt(movedPrey.Position);
        Assert.Equal(fishPosition, world.FindToothedWhalePrey(0, null));
    }

    [Theory]
    [InlineData(19, true)]
    [InlineData(10, true)]
    [InlineData(5, true)]
    [InlineData(20, false)]
    [InlineData(21, false)]
    [InlineData(30, false)]
    public void WhaleIgnoresFishWhenContinuingCombatHunt(int energy, bool hungry)
    {
        var world = new SimulationWorld(40, 1, Terrain.Ocean, seed: 51);
        world.AddCritter(CritterSpecies.ToothedWhale, new(0, 0));
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
        energies[0] = 19;
        var squid = new GridPosition(1, 0);
        world.AddCritter(CritterSpecies.Squid, squid);
        Assert.Equal(squid, world.FindToothedWhalePrey(0, null));
        var fish = new GridPosition(3, 0);
        world.AddCritter(CritterSpecies.Fish, fish);
        energies[0] = energy;
        Assert.Equal(hungry, world.GetCritter(0).IsHungry);
        Assert.Equal(energy < 30 ? squid : (GridPosition?)null,
            world.FindToothedWhalePrey(0, null));
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
        Assert.Equal(6, SimulationWorld.GetCombatDamage(CritterSpecies.ToothedWhale));
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
    public void ToothedWhaleHuntsOceanPredatorsButNotFishOrNautiluses()
    {
        var expectedPrey = new HashSet<CritterSpecies>
        {
            CritterSpecies.SeaScorpion,
            CritterSpecies.Squid,
            CritterSpecies.ApeSailor,
            CritterSpecies.ApeTraderSailor,
            CritterSpecies.Crab,
            CritterSpecies.MegaToad,
            CritterSpecies.MegaSpider,
            CritterSpecies.Therapsid,
            CritterSpecies.Ape,
            CritterSpecies.ApeWarrior,
            CritterSpecies.ApeBomber,
            CritterSpecies.ApeLudditePriest,
            CritterSpecies.ApeKing,
            CritterSpecies.ApeQueen,
            CritterSpecies.ApeBishop,
            CritterSpecies.ApeRoyalGuard,
            CritterSpecies.Dog,
            CritterSpecies.ApeChieftain,
            CritterSpecies.ApeScholar,
            CritterSpecies.ApeFarmer,
            CritterSpecies.ApeLumberjack,
            CritterSpecies.Deer,
            CritterSpecies.MegaSlug,
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
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(shallows)!;
        energies[1] = CritterNutritions.Get(CritterSpecies.Deer).MaximumEnergy;

        for (var tick = 0; tick < 4 * SimulationWorld.TicksPerSecond; tick++)
        {
            ocean.AdvanceOneTick();
            shallows.AdvanceOneTick();
        }

        Assert.Equal(1, ocean.GetCritterCount(CritterSpecies.Deer));
        Assert.Equal(1, shallows.GetCritterCount(CritterSpecies.Deer));
        Assert.True(shallows.GetCritter(0).Energy < CritterNutritions.Get(CritterSpecies.ToothedWhale).InitialEnergy ||
            shallows.GetCritter(1).Energy < CritterNutritions.Get(CritterSpecies.Deer).MaximumEnergy);
    }

    [Fact]
    public void ToothedWhalePursuesAndFightsSquid()
    {
        var world = new SimulationWorld(2, 1, Terrain.Ocean, seed: 5103);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        world.AddCritter(CritterSpecies.Squid, new GridPosition(1, 0));
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
        energies[1] = CritterNutritions.Get(CritterSpecies.Squid).MaximumEnergy;
        Assert.Equal(new GridPosition(1, 0), world.FindToothedWhalePrey(0, null));
        world.CommitEncounter(new(0, 0), new(1, 0));

        Assert.Equal(1, world.GetCritterCount(CritterSpecies.ToothedWhale));
        Assert.Equal(1, world.GetCritterCount(CritterSpecies.Squid));
        var damage = CritterNutritions.Get(CritterSpecies.ToothedWhale).InitialEnergy +
            CritterNutritions.Get(CritterSpecies.Squid).MaximumEnergy -
            world.GetCritter(0).Energy - world.GetCritter(1).Energy;
        Assert.Contains(damage, new[] { 3, 6 });
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(7, false)]
    [InlineData(19, false)]
    public void ToothedWhaleNeverTargetsNautilusIncludingWorldSeam(
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
    public void ToothedWhaleCannotConsumeFishOrNautilusEvenWhenAdjacent(CritterSpecies prey)
    {
        var world = new SimulationWorld(1, 2, Terrain.Ocean, seed: 42);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        world.AddCritter(CritterSpecies.ToothedWhale, new GridPosition(0, 0));
        world.AddCritter(prey, new GridPosition(0, 1));

        Assert.False(SimulationWorld.CanEat(CritterSpecies.ToothedWhale, prey));
        Assert.Null(world.FindToothedWhalePrey(0, null));
        var before = world.GetCritter(1).Energy;
        world.CommitEncounter(new(0, 0), new(0, 1));
        Assert.Equal(before, world.GetCritter(1).Energy);
        Assert.Equal(1, world.GetCritterCount(prey));
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
