using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CustodianTests
{
    [Fact]
    public void RebuildingRingWallsPreservesCustodiansOnBothWalls()
    {
        var world = new SimulationWorld(8, 4, Terrain.Plains) { Body = WorldBody.RingWorld };
        var north = world.AddCritter(CritterSpecies.Custodian, new(2, 0));
        var south = world.AddCritter(CritterSpecies.Custodian, new(3, 3));
        var groundAnimal = world.AddCritter(CritterSpecies.Deer, new(4, 0));
        for (var rebuild = 0; rebuild < 3; rebuild++)
        {
            TerrainClassifier.ApplyRingWorldWalls(world);
            Assert.True(world.TryGetCritter(north, out var northDrone));
            Assert.True(world.TryGetCritter(south, out var southDrone));
            Assert.Equal(new GridPosition(2, 0), northDrone.Position);
            Assert.Equal(new GridPosition(3, 3), southDrone.Position);
            Assert.Equal(30, northDrone.Energy);
            Assert.Equal(30, southDrone.Energy);
        }
        Assert.False(world.TryGetCritter(groundAnimal, out _));
    }

    [Fact]
    public void WallRebuildPreservesFabricatorRecruitsAndTheirAppearance()
    {
        var world = new SimulationWorld(24, 8, Terrain.RingWorldWall) { Body = WorldBody.RingWorld };
        world.SeedRingWorldCustodians();
        var recruits = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        TerrainClassifier.ApplyRingWorldWalls(world);
        Assert.Equal(5, world.GetCritterCount(CritterSpecies.Custodian));
        foreach (var recruit in recruits)
        {
            Assert.True(world.TryGetCritter(recruit.Id, out var survivor));
            Assert.Equal(recruit.Position, survivor.Position);
            Assert.Equal(recruit.IsAncientCustodian, survivor.IsAncientCustodian);
        }
    }

    [Theory]
    [InlineData(7, 30)]
    [InlineData(8, 8)]
    public void FabricatorRepairsOnlyAtOrBelowQuarterEnergy(int energy, int expected)
    {
        var world = new SimulationWorld(8, 3, Terrain.RingWorldWall)
        {
            Body = WorldBody.RingWorld,
            SeasonsEnabled = false,
        };
        world.SeedRingWorldCustodians();
        var drone = world.GetCritter(0);
        while (world.CritterCount > 1)
            world.RemoveCritterAt(world.GetCritter(1).Position);
        var energies = (int[])typeof(SimulationWorld).GetField("_energy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(world)!;
        energies[0] = energy;
        for (var tick = 0; tick < 30 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(drone.Id, out var repaired));
        Assert.Equal(expected, repaired.Energy);
        Assert.True(repaired.IsAncientCustodian);
    }

    [Fact]
    public void CustodianReturnsToRebuildDestroyedPortalAtOriginalLocation()
    {
        var world = new SimulationWorld(24, 8, Terrain.Plains)
        {
            Body = WorldBody.RingWorld,
            SeasonsEnabled = false,
        };
        var portal = new GridPosition(10, 4);
        Assert.True(world.TryPlaceTeleporter(portal));
        world.AddCritter(CritterSpecies.Custodian, new(0, 0));
        Assert.True(world.RemoveTeleporterAt(portal));
        Assert.False(world.RemoveTeleporterAt(portal));
        Assert.Equal(0, world.TeleporterCount);
        for (var tick = 0; tick < 120 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.HasTeleporter(portal));
        Assert.Equal(1, world.TeleporterCount);
        Assert.Equal(30, world.GetCritter(0).Energy);
    }

    [Fact]
    public void RepairWaitsForDisasterCoverToClear()
    {
        var world = new SimulationWorld(8, 3, Terrain.Plains)
        {
            Body = WorldBody.RingWorld,
            SeasonsEnabled = false,
        };
        var portal = new GridPosition(4, 1);
        Assert.True(world.TryPlaceTeleporter(portal));
        world.AddCritter(CritterSpecies.Custodian, new(0, 0));
        world.RemoveTeleporterAt(portal);
        world.SetSurfaceCover(portal, SurfaceCover.Lava, long.MaxValue);
        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.False(world.HasTeleporter(portal));
        world.SetSurfaceCover(portal, SurfaceCover.None, 0);
        for (var tick = 0; tick < 60 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.HasTeleporter(portal));
    }

    [Fact]
    public void NoSpeciesCanHuntOrEatCustodians()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            Assert.False(SimulationWorld.CanEat(species, CritterSpecies.Custodian));
            Assert.False(SimulationWorld.CanPursuePreyAtDistance(species, CritterSpecies.Custodian, 1));
            var world = new SimulationWorld(3, 1, Terrain.Plains);
            Assert.True(world.TrySpawnCritter(species, new(0, 0)));
            var drone = world.AddCritter(CritterSpecies.Custodian, new(1, 0));
            Assert.Null(world.FindHunterPrey(0, species, 8, null));
            world.CommitEncounter(new(0, 0), new(1, 0));
            Assert.True(world.TryGetCritter(drone, out var untouched));
            Assert.Equal(30, untouched.Energy);
        }
    }

    [Fact]
    public void CustodianOnAWebIsNotTrappedAndFliesAway()
    {
        var world = new SimulationWorld(8, 3, Terrain.Plains) { SeasonsEnabled = false };
        var web = new GridPosition(3, 1);
        world.SetBiome(web, Biome.Forest);
        var spider = world.AddCritter(CritterSpecies.MegaSpider, web);
        Assert.True(world.TryCreateMegaSpiderWeb(spider, web));
        world.RemoveCritterAt(web);
        var drone = world.AddCritter(CritterSpecies.Custodian, web);
        Assert.False(world.IsCritterCaughtInMegaSpiderWeb(drone));
        for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(drone, out var flying));
        Assert.NotEqual(web, flying.Position);
        Assert.Equal(30, flying.Energy);
        Assert.False(world.IsCritterCaughtInMegaSpiderWeb(drone));
    }

    [Fact]
    public void CanOccupyEveryTerrainIncludingArcticMountainsAndLava()
    {
        foreach (var terrain in Enum.GetValues<Terrain>())
        {
            Assert.True(CritterHabitats.CanOccupy(CritterHabitats.GetHabitat(CritterSpecies.Custodian),
                terrain, SurfaceWaterKind.FreshwaterLake, Biome.Arctic, SurfaceCover.Lava));
            var world = new SimulationWorld(1, 1, terrain);
            Assert.True(world.TryAddCritter(CritterSpecies.Custodian, new(0, 0)));
        }
    }

    [Theory]
    [InlineData(WorldMapType.RingWorld, 5)]
    [InlineData(WorldMapType.Continents, 0)]
    public void OnlyRingWorldsStartWithFiveCustodians(WorldMapType mapType, int expected)
    {
        var world = WorldGenerator.Generate(new(WorldPreset.Micro, 42, MapType: mapType));
        Assert.Equal(expected, world.GetCritterCount(CritterSpecies.Custodian));
        if (expected > 0)
        {
            Assert.True(world.CustodianFabricator is not null);
            Assert.Equal(Terrain.RingWorldWall, world.GetTerrain(world.CustodianFabricator!.Value));
            for (var index = 0; index < world.CritterCount; index++)
            {
                var drone = world.GetCritter(index);
                Assert.Equal(index < 3, drone.IsAncientCustodian);
                if (index < 3)
                    Assert.Equal(new GridPosition(index * world.Width / 3, world.Height / 2), drone.Position);
                else
                {
                    var fabricator = world.CustodianFabricator.Value;
                    var dx = Math.Abs(drone.Position.X - fabricator.X);
                    Assert.True(Math.Min(dx, world.Width - dx) <= 2);
                    Assert.True(Math.Abs(drone.Position.Y - fabricator.Y) <= 2);
                }
            }
        }
        else
            Assert.Null(world.CustodianFabricator);
    }

    [Fact]
    public void PatrolCrossesEveryTerrainWithoutEnergyLossAndFabricatorReplacesLosses()
    {
        var world = new SimulationWorld(24, 12, Terrain.RingWorldWall, seed: 9)
        {
            Body = WorldBody.RingWorld,
            SeasonsEnabled = false,
        };
        world.SeedRingWorldCustodians();
        var drone = world.GetCritter(0);
        Assert.Equal(30, drone.Energy);
        Assert.Equal(10, world.GetCritterCombatDamage(drone.Id));
        var positions = new HashSet<GridPosition>();
        for (var tick = 0; tick < 240 * SimulationWorld.TicksPerSecond; tick++)
        {
            world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(drone.Id, out var snapshot));
            Assert.Equal(30, snapshot.Energy);
            positions.Add(snapshot.Position);
        }
        Assert.True(positions.Count > 12);
        Assert.True(positions.Select(p => p.Y).Distinct().Count() > 3);
        Assert.True(world.TryGetCritter(drone.Id, out var survivor));
        world.RemoveCritterAt(survivor.Position);
        for (var tick = 0; tick < 120 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.Equal(5, world.GetCritterCount(CritterSpecies.Custodian));
        Assert.False(world.TryGetCritter(drone.Id, out _));
    }

    [Fact]
    public void ReplacementsStartModernAndBecomeAncientAfterLongService()
    {
        var world = new SimulationWorld(24, 12, Terrain.RingWorldWall) { Body = WorldBody.RingWorld };
        world.SeedRingWorldCustodians();
        var original = world.GetCritter(0);
        Assert.True(original.IsAncientCustodian);
        world.RemoveCritterAt(original.Position);
        Assert.Equal(4, world.GetCritterCount(CritterSpecies.Custodian));
        SetTick(world, SimulationWorld.CustodianRecruitmentIntervalTicks - 1);
        world.AdvanceCustodianFabricator();
        Assert.Equal(4, world.GetCritterCount(CritterSpecies.Custodian));
        SetTick(world, SimulationWorld.CustodianRecruitmentIntervalTicks);
        world.AdvanceCustodianFabricator();
        Assert.Equal(5, world.GetCritterCount(CritterSpecies.Custodian));
        var recruit = world.GetCritter(world.CritterCount - 1);
        Assert.False(recruit.IsAncientCustodian);
        Assert.Equal(30, recruit.Energy);
        var ancientTick = world.Tick + SimulationWorld.CustodianAncientAgeTicks;
        SetTick(world, ancientTick - 1);
        Assert.True(world.TryGetCritter(recruit.Id, out var modern));
        Assert.False(modern.IsAncientCustodian);
        SetTick(world, ancientTick);
        Assert.True(world.TryGetCritter(recruit.Id, out var ancient));
        Assert.True(ancient.IsAncientCustodian);
        Assert.Equal(30, ancient.Energy);
        world.AdvanceCustodianFabricator();
        Assert.Equal(5, world.GetCritterCount(CritterSpecies.Custodian));
    }

    private static void SetTick(SimulationWorld world, long tick) =>
        typeof(SimulationWorld).GetProperty(nameof(SimulationWorld.Tick))!.SetValue(world, tick);

    [Theory]
    [InlineData(CritterSpecies.UndeadApe)]
    [InlineData(CritterSpecies.UndeadWarrior)]
    [InlineData(CritterSpecies.Lich)]
    [InlineData(CritterSpecies.Ape)]
    public void AttacksThreatsForTenDamageAndNeverFeeds(CritterSpecies species)
    {
        // Exercise multiple combat outcomes, including retaliation and lethal hits.
        var attacked = false;
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var world = new SimulationWorld(3, 1, Terrain.Plains, seed);
            var drone = world.AddCritter(CritterSpecies.Custodian, new(0, 0));
            var victim = world.AddCritter(species, new(1, 0));
            if (species is CritterSpecies.Ape)
                Assert.True(world.TryInfectApeAt(new(1, 0), PlagueKind.Zombie, (victim.Value + 1) % 2));
            var energy = world.GetCritter(1).Energy;
            world.CommitEncounter(new(0, 0), new(1, 0));
            if (!world.TryGetCritter(victim, out var after))
            {
                attacked = true;
                Assert.True(energy <= 10);
            }
            else if (after.Energy < energy)
            {
                attacked = true;
                Assert.Equal(energy - 10, after.Energy);
            }
            Assert.True(world.TryGetCritter(drone, out var custodian));
            Assert.True(custodian.Energy <= 30);
        }
        Assert.True(attacked);
    }

    [Fact]
    public void LeavesHealthyApesAlone()
    {
        var world = new SimulationWorld(3, 1, Terrain.Plains);
        world.AddCritter(CritterSpecies.Custodian, new(0, 0));
        var ape = world.AddCritter(CritterSpecies.Ape, new(1, 0));
        world.CommitEncounter(new(0, 0), new(1, 0));
        Assert.True(world.TryGetCritter(ape, out var healthy));
        Assert.Equal(CritterNutritions.Get(CritterSpecies.Ape).InitialEnergy, healthy.Energy);
    }

    [Fact]
    public void PatrolEventuallyVisitsATeleporter()
    {
        var world = new SimulationWorld(24, 1, Terrain.RingWorldWall, seed: 42);
        var portal = new GridPosition(12, 0);
        world.SetTerrain(portal, Terrain.Plains);
        Assert.True(world.TryPlaceTeleporter(portal));
        var drone = world.AddCritter(CritterSpecies.Custodian, new(0, 0));
        var previous = new GridPosition(0, 0);
        var teleported = false;
        for (var action = 0; action < 240; action++)
        {
            for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
                world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(drone, out var snapshot));
            var dx = Math.Abs(snapshot.Position.X - previous.X);
            if (Math.Min(dx, world.Width - dx) > 1)
                teleported = true;
            previous = snapshot.Position;
        }
        Assert.True(teleported);
    }

    [Fact]
    public void WideRingPatrolMakesRepeatedTripsToNearestPortal()
    {
        var world = new SimulationWorld(1280, 8, Terrain.RingWorldWall, seed: 42)
        {
            SeasonsEnabled = false,
        };
        foreach (var x in new[] { 0, 640 })
        {
            var portal = new GridPosition(x, 7);
            world.SetTerrain(portal, Terrain.Plains);
            Assert.True(world.TryPlaceTeleporter(portal));
        }
        var drone = world.AddCritter(CritterSpecies.Custodian, new(0, 0));
        var previous = new GridPosition(0, 0);
        var trips = 0;
        for (var action = 0; action < 800; action++)
        {
            for (var tick = 0; tick < 5 * SimulationWorld.TicksPerSecond; tick++)
                world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(drone, out var snapshot));
            var dx = Math.Abs(snapshot.Position.X - previous.X);
            if (Math.Min(dx, world.Width - dx) > 1)
            {
                trips++;
                Assert.True(action >= 180, "Drone should patrol for fifteen minutes before seeking a portal.");
            }
            previous = snapshot.Position;
        }
        Assert.True(trips >= 2, $"Expected repeated portal visits, saw {trips}.");
    }
}
