using System.Reflection;
using Newt.Simulation.Tests;
using Xunit;

var passed = 0;
var failed = 0;
foreach (var type in new[] { typeof(HungerTests), typeof(ToothedWhaleTests), typeof(CritterEnergyBalanceTests),
    typeof(SimulationWorldTests), typeof(OpportunisticFeedingTests), typeof(SnailTests), typeof(CombatDamageTests),
    typeof(TileNutritionTests), typeof(BaleenWhaleTests), typeof(WorldGeneratorTests), typeof(MegaRingInfrastructureTests),
    typeof(RingClimateTests) })
{
    var instance = Activator.CreateInstance(type);
    foreach (var method in type.GetMethods())
    {
        if (type == typeof(WorldGeneratorTests) && method.Name is not
            ("MegaRingWorldDoublesLengthAndRetainsRingFeatures" or "RingWorldIsLongWithoutScalingItsLocalFeaturesByFullWidth" or
            "RingWorldUsesLongitudinalEngineeredClimateZones"))
            continue;
        if (type == typeof(SimulationWorldTests) && method.Name is not
            ("SmallPreyUseSmallerReproductionReserves" or "MegaToadHasTheLargestLandPredatorEnergyReserve" or
            "HungryTherapsidForagesOnlyInSupportedBiomes" or "MonkeyFeedsFromSupportedFoliage" or
            "MonkeyHuntsOnlySnailsAndCrabs" or "MonkeyLeavesAnimalPreyForPredators" or
            "TherapsidPrefersNearbyFoliageOverVisiblePrey" or "GrazersFeedOnlyFromTheirSupportedBiomes"))
            continue;
        if (type == typeof(TileNutritionTests) && method.Name is not
            ("CapacityFollowsProductiveTerrainRules" or "LandNutritionFollowsBiomeHierarchy" or
            "GrazersCannotConsumeDepositedNutritionOutsideSupportedBiomes"))
            continue;
        var rows = method.GetCustomAttributes<InlineDataAttribute>()
            .SelectMany(attribute => attribute.GetData(method)).ToList();
        if (method.GetCustomAttribute<FactAttribute>() is not null && method.GetParameters().Length == 0)
            rows.Add([]);
        foreach (var row in rows)
        {
            try
            {
                method.Invoke(instance, row);
                passed++;
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine($"FAIL {method.Name}({string.Join(", ", row)}): {(error.InnerException ?? error).Message}");
            }
        }
    }
}
Console.WriteLine($"Passed: {passed}; failed: {failed}");
return failed == 0 ? 0 : 1;
