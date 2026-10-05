using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VampireTests
{
    [Fact]
    public void VampiresCannotHuntFightOrFeedOnEachOther()
    {
        var world = CreateWorld();
        var first = world.AddCritter(CritterSpecies.Vampire, new(10, 10));
        var second = world.AddCritter(CritterSpecies.Vampire, new(11, 10));
        SetEnergy(world, first, 2);
        Assert.Null(world.FindHunterPrey(0, CritterSpecies.Vampire, 8, null));
        for (var attempt = 0; attempt < 20; attempt++)
        {
            world.CommitEncounter(new(10, 10), new(11, 10));
            world.CommitEncounter(new(11, 10), new(10, 10));
        }
        typeof(SimulationWorld).GetMethod("ApplyCombatDamage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(world, [0, 1]);
        Assert.True(world.TryGetCritter(first, out var a));
        Assert.True(world.TryGetCritter(second, out var b));
        Assert.Equal(2, a.Energy);
        Assert.Equal(6, b.Energy);
        Assert.False(a.IsDamageFlashing);
        Assert.False(b.IsDamageFlashing);
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(3, 4, false)]
    [InlineData(5, 6, false)]
    public void HuntingPrefersPreyThatSurvivesDamageThenNearest(int nearEnergy, int farEnergy, bool preferFar)
    {
        var world = CreateWorld();
        world.AddCritter(CritterSpecies.Vampire, new(10, 10));
        var near = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        var far = world.AddCritter(CritterSpecies.Ape, new(13, 10));
        SetEnergy(world, near, nearEnergy);
        SetEnergy(world, far, farEnergy);
        Assert.Equal(new GridPosition(preferFar ? 13 : 11, 10),
            world.FindHunterPrey(0, CritterSpecies.Vampire, 8, null));
        if (preferFar)
            Assert.Equal(new GridPosition(11, 10), world.FindHunterPrey(0, CritterSpecies.Vampire, 8,
                new HashSet<GridPosition> { new(13, 10) }));
    }

    private static SimulationWorld CreateWorld(ulong seed = 17)
    {
        var world = new SimulationWorld(30, 20, Terrain.Plains, seed);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        return world;
    }

    private static void SetEnergy(SimulationWorld world, CritterId id, int energy)
    {
        var values = (int[])typeof(SimulationWorld).GetField("_energy", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
        var index = Enumerable.Range(0, world.CritterCount).Single(i => world.GetCritter(i).Id == id);
        values[index] = energy;
    }

    [Fact]
    public void InfectionIsNotContagiousAndDeathCreatesImmuneVampire()
    {
        var world = CreateWorld();
        var infected = world.AddCritter(CritterSpecies.Ape, new(10, 10));
        var neighbor = world.AddCritter(CritterSpecies.Ape, new(11, 10));
        Assert.True(world.TryInfectApeAt(new(10, 10), PlagueKind.Vampire, 0));
        for (var tick = 0; tick < SimulationWorld.PlagueSpreadIntervalTicks; tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(neighbor, out var healthy));
        Assert.Equal(PlagueKind.None, healthy.Plague);
        SetEnergy(world, infected, 0);
        world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(infected, out var vampire));
        Assert.Equal(CritterSpecies.Vampire, vampire.Species);
        Assert.Equal(PlagueKind.None, vampire.Plague);
        Assert.True(vampire.IsPlagueImmune);
        foreach (var strain in new[] { PlagueKind.Plague, PlagueKind.Zombie, PlagueKind.Vampire })
            Assert.False(world.TryInfectApeAt(vampire.Position, strain, 0));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void AttacksFeedExactlyActualDamageWithoutKillBonusOrInfection(int victimEnergy)
    {
        // Exercise both combat outcomes across seeds, including a lethal partial hit.
        var successfulHits = 0;
        for (ulong seed = 1; seed <= 30; seed++)
        {
            var world = CreateWorld(seed);
            var vampire = world.AddCritter(CritterSpecies.Vampire, new(10, 10));
            var ape = world.AddCritter(CritterSpecies.Ape, new(11, 10));
            SetEnergy(world, ape, victimEnergy);
            world.CommitEncounter(new(10, 10), new(11, 10));
            Assert.True(world.TryGetCritter(vampire, out var after));
            var alive = world.TryGetCritter(ape, out var victim);
            var damage = victimEnergy - (alive ? victim.Energy : 0);
            if (damage > 0)
            {
                successfulHits++;
                Assert.Equal(Math.Min(4, victimEnergy), damage);
                Assert.Equal(6 + damage, after.Energy);
            }
            if (alive)
                Assert.Equal(PlagueKind.None, victim.Plague);
        }
        Assert.True(successfulHits > 0);
    }

    [Fact]
    public void FirstBirthCreatesLairAndThreeResidentsRespectItsCapacity()
    {
        var world = CreateWorld();
        var parent = world.AddCritter(CritterSpecies.Vampire, new(10, 10));
        SetEnergy(world, parent, 12);
        Assert.False(world.HasVampireLair(new(10, 10)));
        world.AdvanceOneTick();
        Assert.True(world.HasVampireLair(new(10, 10)));
        Assert.Equal(2, world.GetVampireLairPopulation(new(10, 10)));
        var vampires = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        Assert.Equal(2, vampires.Length);
        foreach (var vampire in vampires)
            SetEnergy(world, vampire.Id, 12);
        world.AdvanceOneTick();
        Assert.Equal(3, world.GetCritterCount(CritterSpecies.Vampire));
        foreach (var vampire in Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray())
            SetEnergy(world, vampire.Id, 12);
        world.AdvanceOneTick();
        Assert.Equal(3, world.GetCritterCount(CritterSpecies.Vampire));
        Assert.All(Enumerable.Range(0, world.CritterCount).Select(world.GetCritter), vampire => Assert.False(vampire.CanReproduce));
        Assert.True(world.TryGetCritter(vampires[1].Id, out var child));
        world.RemoveCritterAt(child.Position);
        world.AdvanceOneTick();
        Assert.Equal(3, world.GetCritterCount(CritterSpecies.Vampire));
        Assert.Equal(3, world.GetVampireLairPopulation(new(10, 10)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LairBecomesRuinOnlyWhenLastVampireIsLost(bool starvation)
    {
        var world = CreateWorld();
        var lair = new GridPosition(10, 10);
        var parent = world.AddCritter(CritterSpecies.Vampire, lair);
        SetEnergy(world, parent, 12);
        world.AdvanceOneTick();
        var residents = Enumerable.Range(0, world.CritterCount).Select(world.GetCritter).ToArray();
        world.RemoveCritterAt(residents[1].Position);
        Assert.True(world.HasVampireLair(lair));
        Assert.Equal(1, world.GetVampireLairPopulation(lair));
        Assert.Null(world.GetApeStructure(lair));

        if (starvation)
        {
            SetEnergy(world, parent, 0);
            world.AdvanceOneTick();
        }
        else
        {
            Assert.True(world.TryGetCritter(parent, out var survivor));
            world.RemoveCritterAt(survivor.Position);
        }
        Assert.False(world.HasVampireLair(lair));
        Assert.Equal(0, world.GetVampireLairPopulation(lair));
        Assert.Equal(ApeStructureKind.Ruin, world.GetApeStructure(lair));
    }

    [Theory]
    [InlineData(CritterSpecies.UndeadApe)]
    [InlineData(CritterSpecies.ApeWarrior)]
    [InlineData(CritterSpecies.ApeChieftain)]
    public void UndeadAndApeLeadersDoNotHuntVampires(CritterSpecies species)
    {
        var world = CreateWorld();
        world.AddCritter(species, new(10, 10));
        var vampire = world.AddCritter(CritterSpecies.Vampire, new(11, 10));
        Assert.Null(world.FindHunterPrey(0, species, 5, null));
        world.CommitEncounter(new(10, 10), new(11, 10));
        Assert.True(world.TryGetCritter(vampire, out var untouched));
        Assert.Equal(6, untouched.Energy);
    }

    [Fact]
    public void VampireCannotFeedFromTerrainOrVillageStores()
    {
        var world = CreateWorld();
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++)
            world.SetBiome(new(x, y), Biome.Grassland);
        var vampire = world.AddCritter(CritterSpecies.Vampire, new(10, 10));
        SetEnergy(world, vampire, 2);
        for (var tick = 0; tick < 10 * SimulationWorld.TicksPerSecond; tick++)
            world.AdvanceOneTick();
        Assert.True(world.TryGetCritter(vampire, out var hungry));
        Assert.Equal(2, hungry.Energy);
        Assert.False(CritterNutritions.Get(CritterSpecies.Vampire).FeedsFromEnvironment);
    }
}

