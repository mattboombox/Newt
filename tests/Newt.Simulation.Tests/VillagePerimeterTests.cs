using System.Reflection;
using Newt.Simulation;

namespace Newt.Simulation.Tests;

public sealed class VillagePerimeterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly GridPosition[] Directions = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    [Theory]
    [InlineData(ApeStructureKind.Farm, false)]
    [InlineData(ApeStructureKind.MilitaryDistrict, false)]
    [InlineData(ApeStructureKind.ResidentialDistrict, false)]
    [InlineData(ApeStructureKind.Farm, true)]
    [InlineData(ApeStructureKind.MilitaryDistrict, true)]
    [InlineData(ApeStructureKind.ResidentialDistrict, true)]
    public void BuildingsExpandAcrossEveryDirectedCubeSeam(ApeStructureKind kind, bool automatic)
    {
        const int size = 16;
        for (var face = 0; face < 6; face++)
        for (var edge = 0; edge < 4; edge++)
        {
            var world = World();
            var home = new GridPosition(face % 3 * size + (edge == 1 ? size - 1 : edge == 3 ? 0 : size / 2),
                face / 3 * size + (edge == 0 ? 0 : edge == 2 ? size - 1 : size / 2));
            Assert.True(world.TrySpawnTestApeVillage(home));
            Clear(world);
            var target = PlanetoidLayout.Offset(home, Directions[edge].X, Directions[edge].Y, size);
            foreach (var direction in Directions)
            {
                var neighbor = PlanetoidLayout.Offset(home, direction.X, direction.Y, size);
                if (neighbor != target) world.SetTerrain(neighbor, Terrain.MegaWall);
            }
            Assert.True(automatic ? world.TryBuildApeStructure(Index(world, home), kind)
                : world.TryPlaceApeBuilding(target, kind), $"Face {face}, edge {edge}, {kind}");
            Assert.Equal(kind, world.GetApeStructure(target));
            Assert.Equal(home, world.GetApeStructureVillage(target));
        }
    }

    [Fact]
    public void PeacefulSquadCirculatesAroundBuildingsOnBothSidesOfASeam()
    {
        var world = World();
        var home = new GridPosition(8, 0);
        Assert.True(world.TrySpawnTestApeVillage(home));
        Clear(world);
        var across = PlanetoidLayout.Offset(home, 0, -1, 16);
        Assert.True(world.TryPlaceApeBuilding(across, ApeStructureKind.MilitaryDistrict));
        var id = world.AddCritter(CritterSpecies.ApeWarrior, home);
        Assert.True(world.TryAssignApeToVillage(id, home));
        var visited = new HashSet<GridPosition>();
        var previous = home;
        for (var step = 0; step < 100; step++)
        {
            Assert.True(world.TryMoveVillageDefenseWarrior(0, null, out var prey));
            Assert.Null(prey);
            var current = world.GetCritter(0).Position;
            Assert.True((bool)Invoke(world, "ArePathNeighbors", Index(world, previous), Index(world, current))!);
            visited.Add(current);
            previous = current;
        }
        Assert.True(visited.Count >= 16, $"Visited only {visited.Count} perimeter tiles.");
        Assert.True(visited.Select(p => PlanetoidLayout.FaceAt(p.X, p.Y, 16)).Distinct().Count() >= 2);
        Assert.Equal(1, world.GetApeMilitaryDistrictNumber(across));
        Assert.Equal(1, world.GetApeWarriorSquadNumber(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryWarriorsSwapAcrossRotatedSeamUnlessReserved(bool reserved)
    {
        var world = World();
        var start = new GridPosition(8, 0);
        var across = PlanetoidLayout.Offset(start, 0, -1, 16);
        var first = world.AddCritter(CritterSpecies.ApeWarrior, start);
        var second = world.AddCritter(CritterSpecies.ApeWarrior, across);
        var blocked = reserved ? new HashSet<GridPosition> { across } : null;
        Assert.Equal(!reserved, (bool)Invoke(world, "TrySwapPastMovementBlocker", 0, Index(world, across), blocked)!);
        Assert.Equal(reserved ? start : across, world.GetCritter(0).Position);
        Assert.Equal(reserved ? across : start, world.GetCritter(1).Position);
        Assert.True(world.TryGetCritterAt(world.GetCritter(0).Position, out var occupant));
        Assert.Equal(first, occupant.Id);
        Assert.True(world.TryGetCritterAt(world.GetCritter(1).Position, out occupant));
        Assert.Equal(second, occupant.Id);
    }

    [Fact]
    public void DistrictAndSquadNumbersAreLocalToVillageAndSurviveWarMobilization()
    {
        var world = World();
        var home = new GridPosition(8, 8);
        var other = new GridPosition(40, 8);
        Assert.True(world.TrySpawnTestApeVillage(home));
        Assert.True(world.TrySpawnTestApeVillage(other));
        Clear(world);
        Assert.True(world.TryPlaceApeBuilding(new(8, 7), ApeStructureKind.MilitaryDistrict));
        Assert.True(world.TryPlaceApeBuilding(new(9, 8), ApeStructureKind.MilitaryDistrict));
        Assert.True(world.TryPlaceApeBuilding(new(40, 7), ApeStructureKind.MilitaryDistrict));
        Assert.True(world.TryPlaceApeBuilding(new(7, 8), ApeStructureKind.ResidentialDistrict));
        Assert.Equal(1, world.GetApeMilitaryDistrictNumber(new(8, 7)));
        Assert.Equal(2, world.GetApeMilitaryDistrictNumber(new(9, 8)));
        Assert.Equal(1, world.GetApeMilitaryDistrictNumber(new(40, 7)));
        var soldiers = Enumerable.Range(0, 8).Select(i =>
        {
            var id = world.AddCritter(CritterSpecies.ApeWarrior, new(5 + i, 10));
            Assert.True(world.TryAssignApeToVillage(id, home));
            return id;
        }).ToArray();
        var enemyPosition = new GridPosition(5, 9);
        var enemy = world.AddCritter(CritterSpecies.ApeWarrior, enemyPosition);
        Assert.True(world.TryAssignApeToVillage(enemy, other));
        var numbers = soldiers.Select(world.GetApeWarriorSquadNumber).ToArray();
        Assert.Equal(4, numbers.Count(number => number == 1));
        Assert.Equal(4, numbers.Count(number => number == 2));
        Assert.True(world.TryStartApeWar(home, other));
        Assert.Equal(numbers, soldiers.Select(world.GetApeWarriorSquadNumber).ToArray());
        var enemyIndex = world.CritterCount - 1;
        Assert.False((bool)Invoke(world, "CanSwapPastMovementBlocker", 0, Index(world, enemyPosition), null, null)!);
        Assert.Equal(enemy, world.GetCritter(enemyIndex).Id);
    }

    private static SimulationWorld World()
    {
        var world = new SimulationWorld(48, 32, Terrain.Plains, seed: 17)
        { Body = WorldBody.Planetoid, ApeVillageMergingEnabled = false, SeasonsEnabled = false };
        NaturalEvents.SetEnabled(world, false);
        for (var y = 0; y < world.Height; y++)
        for (var x = 0; x < world.Width; x++) world.SetBiome(new(x, y), Biome.Grassland);
        return world;
    }
    private static void Clear(SimulationWorld world)
    {
        while (world.CritterCount > 0) world.RemoveCritterAt(world.GetCritter(0).Position);
    }
    private static int Index(SimulationWorld world, GridPosition p) => p.Y * world.Width + p.X;
    private static object? Invoke(SimulationWorld world, string name, params object?[] args) =>
        typeof(SimulationWorld).GetMethod(name, Private)!.Invoke(world, args);
}
