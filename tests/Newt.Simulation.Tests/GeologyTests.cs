using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class GeologyTests
{
    [Fact]
    public void UpliftRaisesCenterMoreThanEdge()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        var center = new GridPosition(10, 10);
        var edge = new GridPosition(14, 10);

        Geology.ApplyRadialUplift(world, center, radius: 5, strength: 0.5f);

        Assert.Equal(0.5f, world.GetElevation(center), precision: 5);
        Assert.InRange(world.GetElevation(edge), 0.01f, 0.5f);
        Assert.True(world.GetElevation(center) > world.GetElevation(edge));
    }

    [Fact]
    public void UpliftCanRaiseOceanIntoLand()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        var center = new GridPosition(10, 10);
        Geology.ApplyRadialUplift(world, center, radius: 4, strength: 0.4f);

        Assert.Equal(Terrain.Hills, world.GetTerrain(center));
        Assert.Contains(
            Terrain.Shallows,
            NeighborTerrains(world, center));
    }

    [Fact]
    public void UpliftWrapsAcrossHorizontalWorldEdge()
    {
        var world = new SimulationWorld(20, 20, Terrain.Ocean);
        Geology.ApplyRadialUplift(world, new GridPosition(0, 10), radius: 3, strength: 0.3f);

        Assert.True(world.GetElevation(new GridPosition(19, 10)) > 0);
    }

    private static IEnumerable<Terrain> NeighborTerrains(SimulationWorld world, GridPosition center)
    {
        for (var y = center.Y - 5; y <= center.Y + 5; y++)
        {
            for (var x = center.X - 5; x <= center.X + 5; x++)
            {
                if (x < 0 || x >= world.Width || y < 0 || y >= world.Height)
                {
                    continue;
                }

                yield return world.GetTerrain(new GridPosition(x, y));
            }
        }
    }
}
