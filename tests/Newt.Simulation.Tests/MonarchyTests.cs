using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class MonarchyTests
{
    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 4)]
    public void ChiefsDealOneMoreDamageThanTheirWarriors(bool barbarian, int expected)
    {
        var world = new SimulationWorld(40, 20, Terrain.Plains, seed: 17);
        var village = new GridPosition(10, 10);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        if (barbarian)
            Assert.True(world.TrySpawnBarbarianApeVillage(village));
        var chief = world.AddCritter(CritterSpecies.ApeChieftain, new(25, 10));
        var warrior = world.AddCritter(CritterSpecies.ApeWarrior, new(26, 10));
        if (barbarian)
        {
            var homes = (Dictionary<int, int>)typeof(SimulationWorld)
                .GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
            homes[chief.Value] = homes[warrior.Value] = village.Y * world.Width + village.X;
        }
        Assert.Equal(expected, world.GetCritterCombatDamage(chief));
        Assert.Equal(world.GetCritterCombatDamage(warrior) + 1, world.GetCritterCombatDamage(chief));
    }

    [Theory]
    [InlineData(CritterSpecies.ApeRoyalGuard, 5)]
    [InlineData(CritterSpecies.ApeKing, 2)]
    [InlineData(CritterSpecies.ApeQueen, 2)]
    public void RoyalRolesUseTheirAdjustedBaseDamage(CritterSpecies species, int expected)
    {
        var world = new SimulationWorld(1, 1, Terrain.Plains);
        var id = world.AddCritter(species, new(0, 0));
        Assert.Equal(expected, SimulationWorld.GetCombatDamage(species));
        Assert.Equal(expected, world.GetCritterCombatDamage(id));
        Assert.Equal(SimulationWorld.GetCombatDamage(CritterSpecies.ApeWarrior) + 1,
            SimulationWorld.GetCombatDamage(CritterSpecies.ApeRoyalGuard));
        Assert.Equal(2,
            SimulationWorld.GetCombatDamage(CritterSpecies.ApeKing));
    }
    private static readonly GridPosition Village = new(15, 15);

    private static SimulationWorld World()
    {
        var world = new SimulationWorld(80, 40, Terrain.Plains, 17)
        {
            ApeVillageMergingEnabled = false,
            SeasonsEnabled = false,
        };
        NaturalEvents.SetEnabled(world, false);
        foreach (var p in Positions(world)) world.SetBiome(p, Biome.Grassland);
        Assert.True(world.TrySpawnTestApeVillage(Village));
        var homes = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        foreach (var position in Positions(world).Where(p => !world.IsOccupied(p) && world.GetApeStructure(p) is null).Take(12).ToArray())
            homes[world.AddCritter(CritterSpecies.Ape, position).Value] = Village.Y * world.Width + Village.X;
        return world;
    }

    private static IEnumerable<GridPosition> Positions(SimulationWorld world) =>
        Enumerable.Range(0, world.Width * world.Height)
            .Select(i => new GridPosition(i % world.Width, i / world.Width));

    private static CritterSnapshot[] Residents(SimulationWorld world) =>
        Enumerable.Range(0, world.CritterCount).Select(world.GetCritter)
            .Where(c => world.GetApeHomeVillage(c.Id) == Village).ToArray();

    private static bool IsMonarch(CritterSnapshot c) => c.Species is CritterSpecies.ApeKing or CritterSpecies.ApeQueen;

    [Fact]
    public void CastleRecruitsHealthyUnemployedApesWithoutSpawningOrPromotingChief()
    {
        var world = World();
        var homes = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var chief = world.AddCritter(CritterSpecies.ApeChieftain, new(20, 15));
        homes[chief.Value] = Village.Y * world.Width + Village.X;
        var recruits = Residents(world).Where(c => c.Species == CritterSpecies.Ape).Select(c => c.Id).ToHashSet();
        var population = world.CritterCount;
        Assert.False(world.TryAssignApeCulture(Village, ApeCulture.Monarchy));
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        Assert.Equal(ApeCulture.Monarchy, world.GetApeVillageCulture(Village));
        Assert.Single(Positions(world), p => world.GetApeStructure(p) == ApeStructureKind.Castle);
        var monarch = Assert.Single(Residents(world), IsMonarch);
        Assert.Contains(monarch.Id, recruits);
        Assert.Equal(population, world.CritterCount);
        Assert.True(world.TryGetCritter(chief, out var unchangedChief));
        Assert.Equal(CritterSpecies.ApeChieftain, unchangedChief.Species);
        Assert.Single(Residents(world), c => c.Species == CritterSpecies.ApeBishop);
        var guards = Residents(world).Where(c => c.Species == CritterSpecies.ApeRoyalGuard).ToArray();
        Assert.Equal(4, guards.Length);
        Assert.All(Residents(world).Where(c => IsMonarch(c) || c.Species is CritterSpecies.ApeBishop or CritterSpecies.ApeRoyalGuard),
            c => Assert.Contains(c.Id, recruits));
        Assert.Equal(guards.Select(c => c.Id).OrderBy(id => id.Value),
            world.GetChieftainEscort(Village).OrderBy(id => id.Value));
        Assert.Contains(Positions(world), p => world.TryPlaceApeBuilding(p, ApeStructureKind.Library));
        Assert.False(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
    }

    [Fact]
    public void RoyalGuardMovesTowardItsMonarch()
    {
        var world = World();
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        var monarch = Assert.Single(Residents(world), IsMonarch);
        var guard = Residents(world).First(c => c.Species == CritterSpecies.ApeRoyalGuard);
        var far = new GridPosition(40, 15);
        var guardIndex = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == guard.Id);
        var positions = (GridPosition[])typeof(SimulationWorld).GetField("_positions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var occupants = (int[])typeof(SimulationWorld).GetField("_occupants", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        occupants[guard.Position.Y * world.Width + guard.Position.X] = -1;
        positions[guardIndex] = far;
        occupants[far.Y * world.Width + far.X] = guardIndex;
        Assert.True(world.TryFollowVillageChieftain(guardIndex, null, out var prey));
        Assert.Null(prey);
        Assert.True(world.TryGetCritter(guard.Id, out var moved));
        Assert.True(Math.Abs(moved.Position.X - monarch.Position.X) + Math.Abs(moved.Position.Y - monarch.Position.Y)
            < Math.Abs(far.X - monarch.Position.X) + Math.Abs(far.Y - monarch.Position.Y));
    }

    [Theory]
    [InlineData(CritterSpecies.UndeadApe, false, false)]
    [InlineData(CritterSpecies.UndeadWarrior, false, false)]
    [InlineData(CritterSpecies.Lich, false, false)]
    [InlineData(CritterSpecies.Ape, true, false)]
    [InlineData(CritterSpecies.Ape, false, true)]
    [InlineData(CritterSpecies.ApeWarrior, false, false)]
    [InlineData(CritterSpecies.ApeChieftain, false, false)]
    public void CastleLeavesVacanciesWhenOnlyIneligibleResidentsRemain(CritterSpecies species, bool sick, bool employed)
    {
        var world = World();
        foreach (var resident in Residents(world)) world.RemoveCritterAt(resident.Position);
        var homes = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var id = world.AddCritter(species, new(25, 15));
        homes[id.Value] = Village.Y * world.Width + Village.X;
        if (sick) Assert.True(world.TryInfectApeAt(new(25, 15), PlagueKind.Plague, (id.Value + 1) % 2));
        if (employed)
        {
            var workplaces = (Dictionary<int, int>)typeof(SimulationWorld)
                .GetField("_apeWorkplaces", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
            workplaces[id.Value] = Village.Y * world.Width + Village.X;
        }
        var population = world.CritterCount;
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        var villageTile = Village.Y * world.Width + Village.X;
        typeof(SimulationWorld).GetMethod("RecruitApeCultureUnits", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [villageTile]);
        Assert.Equal(false, typeof(SimulationWorld).GetMethod("RecruitApeCultureLeader", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [villageTile]));
        Assert.Equal(population, world.CritterCount);
        Assert.True(world.TryGetCritter(id, out var unchanged));
        Assert.Equal(species, unchanged.Species);
        Assert.DoesNotContain(Residents(world), c => IsMonarch(c) || c.Species is CritterSpecies.ApeBishop or CritterSpecies.ApeRoyalGuard);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurpleWarriorsOverrideWarTeamsButOnlyCivilWarDefenders(bool civilWar)
    {
        var world = World();
        var homes = (Dictionary<int, int>)typeof(SimulationWorld)
            .GetField("_apeVillageHomes", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        foreach (var position in Positions(world).Where(p => !world.IsOccupied(p) && world.GetApeStructure(p) is null).Take(10).ToArray())
            homes[world.AddCritter(CritterSpecies.ApeWarrior, position).Value] = Village.Y * world.Width + Village.X;
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        Assert.All(Residents(world), c => Assert.False(world.UsesMonarchyWarriorAppearance(c.Id)));
        if (civilWar)
            Assert.True(world.TryStartApeCivilWar(Village));
        else
        {
            var opponent = new GridPosition(35, 15);
            Assert.True(world.TrySpawnTestApeVillage(opponent));
            Assert.True(world.TryStartApeWar(Village, opponent));
        }
        var warriors = Residents(world).Where(c => c.Species == CritterSpecies.ApeWarrior).ToArray();
        Assert.NotEmpty(warriors);
        foreach (var warrior in warriors)
        {
            Assert.True(world.TryGetApeWarSide(warrior.Id, out var attacking));
            Assert.Equal(!civilWar || !attacking, world.UsesMonarchyWarriorAppearance(warrior.Id));
            Assert.False(world.UsesLudditeWarriorAppearance(warrior.Id));
        }
        Assert.All(Residents(world).Where(c => c.Species != CritterSpecies.ApeWarrior),
            c => Assert.False(world.UsesMonarchyWarriorAppearance(c.Id)));
        Assert.True(world.TryStopApeWar(Village));
        Assert.All(Residents(world), c => Assert.False(world.UsesMonarchyWarriorAppearance(c.Id)));
    }

    [Fact]
    public void DestroyingCastleRemovesCultureAndRestoresOrdinaryRoles()
    {
        var world = World();
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        var monarch = Assert.Single(Residents(world), IsMonarch);
        var castle = Assert.Single(Positions(world), p => world.GetApeStructure(p) == ApeStructureKind.Castle);
        world.RemoveApeStructureAt(castle);
        Assert.Null(world.GetApeVillageCulture(Village));
        Assert.True(world.TryGetCritter(monarch.Id, out var chief));
        Assert.Equal(CritterSpecies.ApeChieftain, chief.Species);
        Assert.DoesNotContain(Residents(world), c => IsMonarch(c) || c.Species is CritterSpecies.ApeBishop or CritterSpecies.ApeRoyalGuard);
    }

    [Theory]
    [InlineData(CritterSpecies.ApeKing)]
    [InlineData(CritterSpecies.ApeQueen)]
    [InlineData(CritterSpecies.ApeBishop)]
    [InlineData(CritterSpecies.ApeRoyalGuard)]
    public void RoyalRolesHaveCompleteSimulationDefinitions(CritterSpecies role)
    {
        Assert.Equal(CritterHabitat.LandDweller, CritterHabitats.GetHabitat(role));
        Assert.True(CritterNutritions.Get(role).MaximumEnergy > 0);
        Assert.True(SimulationWorld.GetMovementIntervalTicks(role) > 0);
        var world = World();
        var id = world.AddCritter(role, new(30, 20));
        Assert.True(world.TryGetCritter(id, out var critter));
        Assert.Equal(role, critter.Species);
    }

    [Fact]
    public void CastleReplacesLostStaffAndRecognizesAQueenAsItsOnlyLeader()
    {
        var world = World();
        Assert.True(world.TryAssignApeCulture(Village, ApeCulture.Monarchy, true));
        var monarch = Assert.Single(Residents(world), IsMonarch);
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == monarch.Id);
        typeof(SimulationWorld).GetMethod("ChangeCritterSpecies", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [index, CritterSpecies.ApeQueen, true, true]);
        var recruitLeader = typeof(SimulationWorld).GetMethod("RecruitApeCultureLeader", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(true, recruitLeader.Invoke(world, [Village.Y * world.Width + Village.X]));
        Assert.Equal(CritterSpecies.ApeQueen, Assert.Single(Residents(world), IsMonarch).Species);
        world.RemoveCritterAt(monarch.Position);
        world.RemoveCritterAt(Residents(world).First(c => c.Species == CritterSpecies.ApeRoyalGuard).Position);
        world.RemoveCritterAt(Residents(world).Single(c => c.Species == CritterSpecies.ApeBishop).Position);
        typeof(SimulationWorld).GetMethod("RecruitApeCultureUnits", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [Village.Y * world.Width + Village.X]);
        Assert.Equal(true, recruitLeader.Invoke(world, [Village.Y * world.Width + Village.X]));
        Assert.Single(Residents(world), IsMonarch);
        Assert.Single(Residents(world), c => c.Species == CritterSpecies.ApeBishop);
        Assert.Equal(4, Residents(world).Count(c => c.Species == CritterSpecies.ApeRoyalGuard));
    }
}
