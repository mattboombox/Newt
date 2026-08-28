using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class TerrainShadingTests
{
    [Theory]
    [InlineData(Terrain.Beach)]
    [InlineData(Terrain.Plains)]
    [InlineData(Terrain.Hills)]
    [InlineData(Terrain.Lowlands)]
    [InlineData(Terrain.Canyon)]
    [InlineData(Terrain.Trench)]
    public void LandShadingIsSubtleAndHigherGroundIsLighter(Terrain terrain)
    {
        var previous = float.MinValue;
        for (var step = 0; step <= 30; step++)
        {
            var brightness = TerrainShading.GetBrightness(terrain, -1f + step * 0.1f, 0f);
            Assert.InRange(brightness, 0.90f, 1.10f);
            Assert.True(brightness >= previous);
            previous = brightness;
        }
        Assert.Equal(1f, TerrainShading.GetBrightness(terrain, 0f, 0f));
        Assert.True(TerrainShading.GetBrightness(terrain, 0.3f, 0f) >
            TerrainShading.GetBrightness(terrain, 0.1f, 0f));
    }

    [Theory]
    [InlineData(Terrain.Ocean)]
    [InlineData(Terrain.DeepOcean)]
    [InlineData(Terrain.Shallows)]
    [InlineData(Terrain.Ice)]
    public void WaterShadingIsSubtleAndTracksDepthBelowSeaLevel(Terrain terrain)
    {
        var shallow = TerrainShading.GetBrightness(terrain, -0.05f, 0f);
        var deep = TerrainShading.GetBrightness(terrain, -0.45f, 0f);
        Assert.True(deep < shallow);
        Assert.InRange(TerrainShading.GetBrightness(terrain, -1f, 1f), 0.97f, 1.03f);
        Assert.InRange(TerrainShading.GetBrightness(terrain, 2f, -1f), 0.97f, 1.03f);
        Assert.Equal(shallow, TerrainShading.GetBrightness(terrain, 0.2f, 0.25f), precision: 5);
    }

    [Theory]
    [InlineData(Terrain.Mountain)]
    [InlineData(Terrain.RingWorldWall)]
    public void ExistingStrongShadingAndArtificialWallsAreUnchanged(Terrain terrain)
    {
        Assert.Equal(1f, TerrainShading.GetBrightness(terrain, -1f, 0f));
        Assert.Equal(1f, TerrainShading.GetBrightness(terrain, 2f, 0f));
    }

    [Fact]
    public void LandformBoundariesDoNotIntroduceAnExtraShadingStep()
    {
        Assert.Equal(
            TerrainShading.GetBrightness(Terrain.Plains, 0.34f, 0f),
            TerrainShading.GetBrightness(Terrain.Hills, 0.34f, 0f));
    }
}
