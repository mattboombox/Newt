using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class TeleporterTests
{
    [Fact]
    public void TeleporterPlacementRequiresAnOpenNonWallTile()
    {
        var world = new SimulationWorld(4, 3, Terrain.Plains, seed: 501);
        var portal = new GridPosition(1, 1);

        Assert.True(world.TryPlaceTeleporter(portal));
        Assert.True(world.HasTeleporter(portal));
        Assert.False(world.TryPlaceTeleporter(portal));

        var occupied = new GridPosition(2, 1);
        world.AddCritter(CritterSpecies.Deer, occupied);
        Assert.False(world.TryPlaceTeleporter(occupied));

        var wall = new GridPosition(0, 0);
        world.SetTerrain(wall, Terrain.RingWorldWall);
        Assert.False(world.TryPlaceTeleporter(wall));
        Assert.Equal(1, world.TeleporterCount);
    }

    [Fact]
    public void SingleTeleporterSendsCritterToRandomValidEmptyNonPortalTile()
    {
        var world = new SimulationWorld(6, 1, Terrain.Plains, seed: 502);
        var portal = new GridPosition(2, 0);
        var occupied = new GridPosition(5, 0);
        Assert.True(world.TryPlaceTeleporter(portal));
        var travelerId = world.AddCritter(CritterSpecies.Deer, portal);
        var residentId = world.AddCritter(CritterSpecies.Deer, occupied);

        Assert.True(world.TryActivateTeleporterAt(portal));

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.NotEqual(portal, traveler.Position);
        Assert.NotEqual(occupied, traveler.Position);
        Assert.False(world.HasTeleporter(traveler.Position));
        Assert.True(world.TryGetCritter(residentId, out var resident));
        Assert.Equal(occupied, resident.Position);
        Assert.Equal(2, world.CritterCount);
    }

    [Fact]
    public void CritterMovementOntoTeleporterActivatesItAutomatically()
    {
        var world = new SimulationWorld(2, 1, Terrain.DeepOcean, seed: 506);
        var start = new GridPosition(0, 0);
        var portal = new GridPosition(1, 0);
        Assert.True(world.TryPlaceTeleporter(portal));
        var travelerId = world.AddCritter(CritterSpecies.Plankton, start);

        for (var tick = 0; tick < SimulationWorld.PlanktonMovementIntervalTicks; tick++)
        {
            world.AdvanceOneTick();
        }

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.Equal(start, traveler.Position);
        Assert.False(world.IsOccupied(portal));
    }

    [Fact]
    public void TwoTeleportersSendCritterBesideTheOtherPortalWithoutOverlap()
    {
        var world = new SimulationWorld(10, 3, Terrain.Plains, seed: 503);
        var source = new GridPosition(1, 1);
        var destination = new GridPosition(7, 1);
        var occupiedArrival = new GridPosition(7, 0);
        var openArrival = new GridPosition(8, 0);
        Assert.True(world.TryPlaceTeleporter(source));
        Assert.True(world.TryPlaceTeleporter(destination));

        foreach (var neighbor in GetNeighbors(world, destination))
        {
            world.SetTerrain(neighbor, Terrain.Mountain);
        }
        world.SetTerrain(occupiedArrival, Terrain.Plains);
        world.SetTerrain(openArrival, Terrain.Plains);
        var travelerId = world.AddCritter(CritterSpecies.Deer, source);
        var residentId = world.AddCritter(CritterSpecies.Deer, occupiedArrival);

        Assert.True(world.TryActivateTeleporterAt(source));

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.Equal(openArrival, traveler.Position);
        Assert.True(world.TryGetCritter(residentId, out var resident));
        Assert.Equal(occupiedArrival, resident.Position);
        Assert.False(world.IsOccupied(source));
        Assert.False(world.IsOccupied(destination));
    }

    [Fact]
    public void MoreThanTwoTeleportersChooseAnotherPortalAndLandBesideIt()
    {
        var world = new SimulationWorld(16, 3, Terrain.Plains, seed: 504);
        var source = new GridPosition(1, 1);
        var destinations = new[] { new GridPosition(7, 1), new GridPosition(13, 1) };
        Assert.True(world.TryPlaceTeleporter(source));
        Assert.All(destinations, portal => Assert.True(world.TryPlaceTeleporter(portal)));
        var travelerId = world.AddCritter(CritterSpecies.Deer, source);

        Assert.True(world.TryActivateTeleporterAt(source));

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.Contains(destinations, portal => IsNeighbor(world, portal, traveler.Position));
        Assert.False(world.HasTeleporter(traveler.Position));
    }

    [Fact]
    public void BlockedTeleporterNetworkFallsBackToRandomValidTile()
    {
        var world = new SimulationWorld(8, 3, Terrain.Mountain, seed: 505);
        var source = new GridPosition(1, 1);
        var destination = new GridPosition(6, 1);
        var distantArrival = new GridPosition(3, 1);
        world.SetTerrain(source, Terrain.Plains);
        world.SetTerrain(distantArrival, Terrain.Plains);
        Assert.True(world.TryPlaceTeleporter(source));
        Assert.True(world.TryPlaceTeleporter(destination));
        var travelerId = world.AddCritter(CritterSpecies.Deer, source);

        Assert.True(world.TryActivateTeleporterAt(source));

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.Equal(distantArrival, traveler.Position);
        Assert.False(world.IsOccupied(source));
        Assert.False(world.HasTeleporter(traveler.Position));
    }

    [Fact]
    public void TeleporterLeavesCritterInPlaceWhenNoValidTileExistsAnywhere()
    {
        var world = new SimulationWorld(8, 3, Terrain.Mountain, seed: 507);
        var source = new GridPosition(1, 1);
        var destination = new GridPosition(6, 1);
        world.SetTerrain(source, Terrain.Plains);
        Assert.True(world.TryPlaceTeleporter(source));
        Assert.True(world.TryPlaceTeleporter(destination));
        var travelerId = world.AddCritter(CritterSpecies.Deer, source);

        Assert.False(world.TryActivateTeleporterAt(source));

        Assert.True(world.TryGetCritter(travelerId, out var traveler));
        Assert.Equal(source, traveler.Position);
        Assert.True(world.IsOccupied(source));
    }

    private static IEnumerable<GridPosition> GetNeighbors(
        SimulationWorld world,
        GridPosition center)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                {
                    continue;
                }

                var y = center.Y + offsetY;
                if (y >= 0 && y < world.Height)
                {
                    yield return new GridPosition(
                        (center.X + offsetX + world.Width) % world.Width,
                        y);
                }
            }
        }
    }

    private static bool IsNeighbor(
        SimulationWorld world,
        GridPosition portal,
        GridPosition arrival)
    {
        var horizontal = Math.Abs(portal.X - arrival.X);
        horizontal = Math.Min(horizontal, world.Width - horizontal);
        return Math.Max(horizontal, Math.Abs(portal.Y - arrival.Y)) == 1;
    }
}
