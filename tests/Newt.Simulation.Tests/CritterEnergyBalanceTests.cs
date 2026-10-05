using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class CritterEnergyBalanceTests
{
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
