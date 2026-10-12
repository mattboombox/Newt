using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RingClimateTests
{
    [Theory]
    [InlineData(1280, 2)]
    [InlineData(2560, 4)]
    public void EngineeredClimateRepeatsEvery640Tiles(int width, int sets)
    {
        var world = new SimulationWorld(width, 1, Terrain.Plains, seed: 9) { Body = WorldBody.RingWorld };
        var temperatures = Enumerable.Range(0, width)
            .Select(x => ClimateSystem.GetRingTemperatureNormal(world, x)).ToArray();
        var warmStarts = Enumerable.Range(0, width).Count(x =>
            temperatures[x] > 0.49f && temperatures[(x + width - 1) % width] <= 0.49f);
        Assert.Equal(sets, warmStarts);
        for (var x = 0; x < width - 640; x++)
        {
            Assert.InRange(Math.Abs(temperatures[x] - temperatures[x + 640]), 0, 0.00001f);
            Assert.InRange(Math.Abs(ClimateSystem.GetRingMoistureNormal(world, x) -
                ClimateSystem.GetRingMoistureNormal(world, x + 640)), 0, 0.00001f);
        }
    }
}
