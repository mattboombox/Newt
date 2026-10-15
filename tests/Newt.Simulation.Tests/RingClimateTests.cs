using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class RingClimateTests
{
    [Theory]
    [InlineData(1280, 0UL)]
    [InlineData(1280, 9UL)]
    [InlineData(1280, 2500UL)]
    [InlineData(1280, 5000UL)]
    [InlineData(1280, 7500UL)]
    [InlineData(2560, 9999UL)]
    public void BothClimateCyclesFormSeaIceInColdOceanZones(int width, ulong seed)
    {
        var world = new SimulationWorld(width, 5, Terrain.DeepOcean, seed)
        {
            Body = WorldBody.RingWorld,
            SeasonsEnabled = false,
            OceanSeed = new GridPosition(0, 2),
        };
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < width; x++)
            world.SetElevation(new(x, y), -0.5f);

        TerrainClassifier.RebuildAll(world);

        for (var cycle = 0; cycle < 2; cycle++)
        {
            var positions = Enumerable.Range(cycle * width / 2, width / 2)
                .Select(x => new GridPosition(x, 2)).ToArray();
            Assert.Contains(positions, p => world.GetTerrain(p) is Terrain.Ice);
            Assert.Contains(positions, p => world.GetTerrain(p) is Terrain.DeepOcean);
            Assert.All(positions.Where(p => world.GetTerrain(p) is Terrain.Ice),
                p => Assert.True(world.GetTemperature(p) < ClimateSystem.SeaIceThreshold));
        }
    }

    [Theory]
    [InlineData(1280, 640)]
    [InlineData(2560, 1280)]
    public void EngineeredClimateKeepsTwoSetsWithScaledZoneLengths(int width, int setWidth)
    {
        var world = new SimulationWorld(width, 1, Terrain.Plains, seed: 9) { Body = WorldBody.RingWorld };
        var temperatures = Enumerable.Range(0, width)
            .Select(x => ClimateSystem.GetRingTemperatureNormal(world, x)).ToArray();
        var warmStarts = Enumerable.Range(0, width).Count(x =>
            temperatures[x] > 0.49f && temperatures[(x + width - 1) % width] <= 0.49f);
        Assert.Equal(2, warmStarts);
        for (var x = 0; x < width - setWidth; x++)
        {
            Assert.InRange(Math.Abs(temperatures[x] - temperatures[x + setWidth]), 0, 0.00001f);
            Assert.InRange(Math.Abs(ClimateSystem.GetRingMoistureNormal(world, x) -
                ClimateSystem.GetRingMoistureNormal(world, x + setWidth)), 0, 0.00001f);
        }
    }
}
