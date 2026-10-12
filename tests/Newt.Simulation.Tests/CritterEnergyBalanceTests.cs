using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CritterEnergyBalanceTests
{
    [Fact]
    public void EverySpeciesUsesSharedEnergyFormulas()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var nutrition = CritterNutritions.Get(species);
            Assert.Equal(Math.Max(1, nutrition.MaximumEnergy / 3), nutrition.InitialEnergy);
            Assert.Equal(nutrition.MaximumEnergy, nutrition.ReproductionThreshold);
            Assert.Equal((nutrition.MaximumEnergy + 1) / 2, nutrition.ReproductionCost);
            Assert.Equal(1, nutrition.MetabolismCost);
            Assert.Equal(nutrition.HasMetabolism ? 75 * SimulationWorld.TicksPerSecond : 0,
                nutrition.MetabolismIntervalTicks);
        }
    }

    [Fact]
    public void NonBreedingAndNonMetabolizingSpeciesRetainTheirExemptions()
    {
        foreach (var species in new[] { CritterSpecies.Custodian, CritterSpecies.Lich,
            CritterSpecies.UndeadWarrior, CritterSpecies.SquidEgg })
        {
            Assert.False(CritterNutritions.Get(species).CanReproduce);
            Assert.False(CritterNutritions.Get(species).HasMetabolism);
            Assert.False(SimulationWorld.CanSpeciesReproduce(species));
        }
        foreach (var species in new[] { CritterSpecies.ApeTrader, CritterSpecies.ApeTraderSailor,
            CritterSpecies.ApeBomber, CritterSpecies.ApeScholar, CritterSpecies.UndeadApe,
            CritterSpecies.ApeLudditePriest, CritterSpecies.ApeBishop })
            Assert.False(SimulationWorld.CanSpeciesReproduce(species));
    }

    [Fact]
    public void SimultaneousSpawnsStaggerMetabolismAndKeepTheSharedInterval()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var world = new SimulationWorld(40, 1, Terrain.Ocean, seed: 17);
        world.SeasonsEnabled = false;
        NaturalEvents.SetEnabled(world, false);
        for (var i = 0; i < SimulationWorld.TicksPerSecond; i++)
            world.AddCritter(CritterSpecies.SeaScorpion, new(i, 0));
        var schedule = (long[])typeof(SimulationWorld).GetField("_nextMetabolismTicks", flags)!.GetValue(world)!;
        var due = schedule.Take(world.CritterCount).ToArray();
        var nutrition = CritterNutritions.Get(CritterSpecies.SeaScorpion);
        Assert.Equal(SimulationWorld.TicksPerSecond, due.Distinct().Count());
        Assert.Equal(nutrition.MetabolismIntervalTicks, due.Min());
        Assert.Equal(nutrition.MetabolismIntervalTicks + SimulationWorld.TicksPerSecond - 1, due.Max());
        typeof(SimulationWorld).GetProperty("Tick")!.SetValue(world, due[0]);
        typeof(SimulationWorld).GetMethod("AdvanceCritterLifecycles", flags)!.Invoke(world, null);
        Assert.Equal(nutrition.InitialEnergy - 1, world.GetCritter(0).Energy);
        Assert.Equal(nutrition.InitialEnergy, world.GetCritter(1).Energy);
        Assert.Equal(due[0] + nutrition.MetabolismIntervalTicks, schedule[0]);
        Assert.Equal(due[1], schedule[1]);
    }

    [Fact]
    public void AllReproducingSpeciesRequireFullEnergy()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            var nutrition = CritterNutritions.Get(species);
            if (nutrition.CanReproduce)
                Assert.Equal(nutrition.MaximumEnergy, nutrition.ReproductionThreshold);
        }
    }

    [Fact]
    public void CrittersSmallerThanTherapsidsHaveAtMostEightEnergy()
    {
        var therapsid = CritterNutritions.Get(CritterSpecies.Therapsid);
        Assert.Equal(8, therapsid.MaximumEnergy);
        var smaller = Enum.GetValues<CritterSpecies>()
            // Mechanical drones carry a fixed battery rather than biological food reserves.
            .Where(species => species is not CritterSpecies.Custodian)
            .Where(species => CritterNutritions.Get(species).BodySize < therapsid.BodySize)
            .ToArray();
        Assert.NotEmpty(smaller);
        Assert.All(smaller, species =>
        {
            var nutrition = CritterNutritions.Get(species);
            Assert.True(nutrition.InitialEnergy <= therapsid.MaximumEnergy,
                $"{species} starts with more than eight energy.");
            Assert.True(nutrition.MaximumEnergy <= therapsid.MaximumEnergy,
                $"{species} can store more than eight energy.");
            Assert.True(nutrition.ReproductionThreshold <= nutrition.MaximumEnergy,
                $"{species} cannot reach its reproduction threshold.");
        });
    }
}
