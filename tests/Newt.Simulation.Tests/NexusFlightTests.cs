using Newt.Game;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class NexusFlightTests
{
    [Theory]
    [InlineData(48, 48)]
    [InlineData(59, 14)]
    public void OpenAirspaceAllowsFreePlayerMovementAndCustodiansOnly(int x, int y)
    {
        var world = new SimulationWorld(96, 96, Terrain.Plains) { Body = WorldBody.Nexus };
        TerrainClassifier.ApplyMegaWalls(world);
        var opening = new GridPosition(x, y);
        Assert.False(NexusLayout.Contains(world, opening));
        Assert.True(NexusLayout.ContainsAirspace(world, opening));
        Assert.True(ThreeDGeometry.PlayerCanEnterNexusTile(world, opening));
        Assert.False(world.TryAddCritter(CritterSpecies.Ape, opening));
        Assert.True(world.TryAddCritter(CritterSpecies.Custodian, opening));
        Assert.False(world.TryAddCritter(CritterSpecies.Custodian, new(0, 0)));
        Assert.False(ThreeDGeometry.PlayerCanEnterNexusTile(world, new(0, 0)));
    }

    [Fact]
    public void WallsAndOpeningsDoNotForcePlayerOntoAnArtificialFloor()
    {
        var world = new SimulationWorld(96, 96, Terrain.Plains) { Body = WorldBody.Nexus };
        TerrainClassifier.ApplyMegaWalls(world);
        Assert.True(ThreeDGeometry.PlayerUsesOpenAirMovement(world, NexusLayout.Center(world)));
        Assert.True(ThreeDGeometry.PlayerUsesOpenAirMovement(world, new(59, 14)));
        var wall = NexusLayout.ArmPosition(world, 0, 0.98f);
        world.SetStructuralTerrain(wall, Terrain.MegaWall, SimulationWorld.MegaWallElevation);
        Assert.True(ThreeDGeometry.PlayerUsesOpenAirMovement(world, wall));
        var land = NexusLayout.ArmPosition(world, 0, 0.5f);
        Assert.False(ThreeDGeometry.PlayerUsesOpenAirMovement(world, land));
        foreach (var height in new[] { -2f, 0f, 1.2f, 8f, 20f })
        {
            Assert.Equal(height, ThreeDGeometry.PlayerOpenAirHeight(height, false, 0.1f, 32f, false));
            Assert.True(ThreeDGeometry.PlayerOpenAirHeight(height, true, 0.1f, 32f, false) > height);
            Assert.True(ThreeDGeometry.PlayerOpenAirHeight(height, true, 0.1f, 32f, true) >
                ThreeDGeometry.PlayerOpenAirHeight(height, true, 0.1f, 32f, false));
        }
    }
    [Fact]
    public void CustodianCrossesCenterOpeningToRepairPortal()
    {
        var world = new SimulationWorld(96, 96, Terrain.Plains) { Body = WorldBody.Nexus, SeasonsEnabled = false };
        TerrainClassifier.ApplyMegaWalls(world);
        var start = NexusLayout.ArmPosition(world, 0, 0.4f);
        var target = NexusLayout.ArmPosition(world, 5, 0.4f);
        Assert.True(world.TryPlaceTeleporter(target));
        Assert.True(world.RemoveTeleporterAt(target));
        var drone = world.AddCritter(CritterSpecies.Custodian, start);
        var crossedOpening = false;
        for (var tick = 0; tick < 300 * SimulationWorld.TicksPerSecond && !world.HasTeleporter(target); tick++)
        {
            world.AdvanceOneTick();
            Assert.True(world.TryGetCritter(drone, out var snapshot));
            crossedOpening |= !NexusLayout.Contains(world, snapshot.Position);
            Assert.True(NexusLayout.ContainsAirspace(world, snapshot.Position));
        }
        Assert.True(crossedOpening);
        Assert.True(world.HasTeleporter(target));
    }
}
