using System.Text;
using RogueNet.Logic.MapGeneration;

namespace RogueNetServer.UnitTests.MapGeneration;

public sealed class DungeonMapGeneratorTests
{
    [Fact]
    public void Generate_usesConfiguredDimensionsAndInitializesEveryTile()
    {
        var map = new DungeonMapGenerator().Generate(CreateSettings(width: 80, height: 45), new Random(1001));

        Assert.Equal(80, map.Width);
        Assert.Equal(45, map.Height);
        Assert.Equal(45, map.Tiles.GetLength(0));
        Assert.Equal(80, map.Tiles.GetLength(1));
        Assert.All(map.Tiles.Cast<DungeonTile>(), tile => Assert.NotNull(tile));
    }

    [Fact]
    public void Generate_placesRoomsWithinConfiguredSizeProfilesAndBounds()
    {
        var settings = CreateSettings(roomSizes:
        [
            new(RoomSizeCategory.Large, 12, 18, 8, 12, 2, 2),
            new(RoomSizeCategory.Medium, 8, 12, 6, 8, 3, 3),
            new(RoomSizeCategory.Small, 4, 7, 3, 5, 4, 4)
        ]);
        var map = new DungeonMapGenerator().Generate(settings, new Random(2002));

        Assert.Equal(9, map.Rooms.Count);
        Assert.Equal(2, map.Rooms.Count(room => room.Category == RoomSizeCategory.Large));
        Assert.Equal(3, map.Rooms.Count(room => room.Category == RoomSizeCategory.Medium));
        Assert.Equal(4, map.Rooms.Count(room => room.Category == RoomSizeCategory.Small));
        foreach (var room in map.Rooms)
        {
            Assert.InRange(room.X, 0, map.Width - room.Width);
            Assert.InRange(room.Y, 0, map.Height - room.Height);
            Assert.InRange(room.Width, WidthRange(room.Category).Minimum, WidthRange(room.Category).Maximum);
            Assert.InRange(room.Height, HeightRange(room.Category).Minimum, HeightRange(room.Category).Maximum);
        }
    }

    [Fact]
    public void Generate_keepsRoomsSeparatedByConfiguredBuffer()
    {
        var map = new DungeonMapGenerator().Generate(CreateSettings(), new Random(3003));

        for (var firstIndex = 0; firstIndex < map.Rooms.Count; firstIndex++)
        for (var secondIndex = firstIndex + 1; secondIndex < map.Rooms.Count; secondIndex++)
            Assert.False(map.Rooms[firstIndex].Overlaps(map.Rooms[secondIndex], buffer: 1));
    }

    [Fact]
    public void Generate_buildsConnectedRoomGraphAndWalkableRoutes()
    {
        var settings = CreateSettings(roomSizes: [new(RoomSizeCategory.Small, 5, 5, 5, 5, 7, 7)]);
        var map = new DungeonMapGenerator().Generate(settings, new Random(4004));

        Assert.Equal(map.Rooms.Count - 1, map.Connections.Count);
        Assert.Equal(map.Rooms.Count, GetReachableRoomIds(map).Count);
        Assert.All(map.Connections, connection =>
        {
            Assert.Contains(map.Rooms, room => room.Id == connection.FromRoomId);
            Assert.Contains(map.Rooms, room => room.Id == connection.ToRoomId);
        });
    }

    [Fact]
    public void Generate_addsConfiguredNumberOfOptionalLoopConnections()
    {
        var settings = CreateSettings(minimumExtraConnections: 2, maximumExtraConnections: 2);
        var map = new DungeonMapGenerator().Generate(settings, new Random(4504));

        Assert.Equal(map.Rooms.Count + 1, map.Connections.Count);
        Assert.Equal(2, map.Connections.Count(connection => connection.IsOptionalLoop));
    }

    [Fact]
    public void Generate_isReproducibleForSameSeed()
    {
        var generator = new DungeonMapGenerator();
        var settings = CreateSettings();
        var first = generator.Generate(settings, new Random(5005));
        var second = generator.Generate(settings, new Random(5005));

        Assert.Equal(CreateMapSnapshot(first), CreateMapSnapshot(second));
    }

    [Fact]
    public void Generate_makesMapBoundaryAndRockAdjacentToFloorIntoWalls()
    {
        var map = new DungeonMapGenerator().Generate(CreateSettings(), new Random(6006));

        for (var x = 0; x < map.Width; x++)
        {
            Assert.NotEqual(TerrainType.Floor, map.Tiles[0, x].Terrain);
            Assert.NotEqual(TerrainType.Floor, map.Tiles[map.Height - 1, x].Terrain);
        }
        for (var y = 0; y < map.Height; y++)
        {
            Assert.NotEqual(TerrainType.Floor, map.Tiles[y, 0].Terrain);
            Assert.NotEqual(TerrainType.Floor, map.Tiles[y, map.Width - 1].Terrain);
        }
        for (var y = 1; y < map.Height - 1; y++)
        for (var x = 1; x < map.Width - 1; x++)
        {
            if (map.Tiles[y, x].Terrain != TerrainType.Rock)
                continue;
            var hasAdjacentFloor = AdjacentCells(x, y, map.Width, map.Height)
                .Any(point => map.Tiles[point.Y, point.X].Terrain == TerrainType.Floor);
            Assert.False(hasAdjacentFloor, $"Rock at ({x}, {y}) is adjacent to floor instead of being a wall.");
        }
    }

    [Fact]
    public void Generate_placesOneEntranceAndExitAtLeastConfiguredDistanceApart()
    {
        const int minimumDistance = 12;
        var settings = CreateSettings(minimumStairDistance: minimumDistance);
        var map = new DungeonMapGenerator().Generate(settings, new Random(7007));

        var entrance = Assert.Single(FindTiles(map, TileFeature.EntranceStairs));
        var exit = Assert.Single(FindTiles(map, TileFeature.ExitStairs));
        Assert.True(ManhattanDistance(entrance, exit) >= minimumDistance);
    }

    [Fact]
    public void Generate_doesNotCreateDoorsWhenDoorChanceIsZero()
    {
        var settings = CreateSettings(doorChance: 0);
        var map = new DungeonMapGenerator().Generate(settings, new Random(8008));

        Assert.DoesNotContain(map.Tiles.Cast<DungeonTile>(), tile => IsDoor(tile.Feature));
    }

    [Fact]
    public void Generate_usesConfiguredDoorStateProbabilities()
    {
        var settings = CreateSettings(
            roomSizes: [new(RoomSizeCategory.Small, 5, 5, 5, 5, 2, 2)],
            doorChance: 1,
            lockedDoorChance: 0,
            openDoorChance: 1,
            closedDoorChance: 0);
        var map = new DungeonMapGenerator().Generate(settings, new Random(9009));
        var doors = map.Tiles.Cast<DungeonTile>().Where(tile => IsDoor(tile.Feature)).ToArray();

        Assert.NotEmpty(doors);
        Assert.All(doors, door => Assert.Equal(TileFeature.OpenDoor, door.Feature));
    }

    [Fact]
    public void Generate_rejectsInvalidDoorStateProbabilities()
    {
        var settings = CreateSettings(lockedDoorChance: 0.2, openDoorChance: 0.2, closedDoorChance: 0.2);

        Assert.Throws<ArgumentException>(() => new DungeonMapGenerator().Generate(settings, new Random(10010)));
    }

    [Fact]
    public void Generate_throwsWhenConfiguredRoomsCannotFit()
    {
        var settings = CreateSettings(width: 5, height: 5, roomPlacementAttempts: 2,
            roomSizes: [new(RoomSizeCategory.Small, 4, 4, 4, 4, 2, 2)]);

        Assert.Throws<InvalidOperationException>(() => new DungeonMapGenerator().Generate(settings, new Random(11011)));
    }

    [Fact]
    public void Generate_rejectsRoomSizeRangesOutsideMapBounds()
    {
        var settings = CreateSettings(width: 10, height: 10,
            roomSizes: [new(RoomSizeCategory.Small, 4, 11, 3, 5, 1, 1)]);

        Assert.Throws<ArgumentException>(() => new DungeonMapGenerator().Generate(settings, new Random(12012)));
    }

    private static MapGenerationSettings CreateSettings(
        int width = 120,
        int height = 70,
        int minimumStairDistance = 10,
        RoomSizeDefinition[]? roomSizes = null,
        int roomPlacementAttempts = 500,
        int minimumExtraConnections = 0,
        int maximumExtraConnections = 0,
        double doorChance = 0.25,
        double lockedDoorChance = 0.10,
        double openDoorChance = 0.30,
        double closedDoorChance = 0.60) =>
        new()
        {
            Width = width,
            Height = height,
            RoomPlacementAttempts = roomPlacementAttempts,
            MinimumExtraConnections = minimumExtraConnections,
            MaximumExtraConnections = maximumExtraConnections,
            MinimumStairDistance = minimumStairDistance,
            DirectRoomConnectionChance = 0,
            DoorChance = doorChance,
            LockedDoorChance = lockedDoorChance,
            OpenDoorChance = openDoorChance,
            ClosedDoorChance = closedDoorChance,
            PillarChancePerRoomFloor = 0,
            WaterChancePerRoomFloor = 0,
            TrapChancePerRoomFloor = 0,
            TreasureChancePerRoomFloor = 0,
            RoomSizes = roomSizes ?? [new(RoomSizeCategory.Small, 5, 7, 4, 6, 5, 5)]
        };

    private static IReadOnlyList<int> GetReachableRoomIds(DungeonMap map)
    {
        var start = map.Rooms[0].Center;
        var pending = new Queue<MapPoint>();
        var visited = new HashSet<MapPoint> { start };
        pending.Enqueue(start);
        while (pending.TryDequeue(out var point))
        {
            foreach (var neighbor in Neighbors(point.X, point.Y, map.Width, map.Height))
            {
                if (!IsTraversable(map.Tiles[neighbor.Y, neighbor.X]) || !visited.Add(neighbor))
                    continue;
                pending.Enqueue(neighbor);
            }
        }
        return map.Rooms.Where(room => visited.Contains(room.Center)).Select(room => room.Id).ToArray();
    }

    private static bool IsTraversable(DungeonTile tile) => tile.Terrain == TerrainType.Floor || IsDoor(tile.Feature);

    private static bool IsDoor(TileFeature feature) => feature is
        TileFeature.ClosedDoor or TileFeature.OpenDoor or TileFeature.LockedDoor;

    private static IEnumerable<MapPoint> FindTiles(DungeonMap map, TileFeature feature)
    {
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
            if (map.Tiles[y, x].Feature == feature)
                yield return new MapPoint(x, y);
    }

    private static IEnumerable<MapPoint> Neighbors(int x, int y, int width, int height)
    {
        if (x > 0) yield return new MapPoint(x - 1, y);
        if (x < width - 1) yield return new MapPoint(x + 1, y);
        if (y > 0) yield return new MapPoint(x, y - 1);
        if (y < height - 1) yield return new MapPoint(x, y + 1);
    }

    private static IEnumerable<MapPoint> AdjacentCells(int x, int y, int width, int height)
    {
        for (var offsetY = -1; offsetY <= 1; offsetY++)
        for (var offsetX = -1; offsetX <= 1; offsetX++)
        {
            if (offsetX == 0 && offsetY == 0) continue;
            var neighborX = x + offsetX;
            var neighborY = y + offsetY;
            if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                yield return new MapPoint(neighborX, neighborY);
        }
    }

    private static long ManhattanDistance(MapPoint first, MapPoint second) =>
        Math.Abs((long)first.X - second.X) + Math.Abs((long)first.Y - second.Y);

    private static string CreateMapSnapshot(DungeonMap map)
    {
        var snapshot = new StringBuilder();
        snapshot.Append(map.Width).Append('x').Append(map.Height).Append('|');
        foreach (var room in map.Rooms)
            snapshot.Append($"R{room.Id}:{room.X},{room.Y},{room.Width},{room.Height},{room.Category};");
        foreach (var connection in map.Connections)
            snapshot.Append($"C{connection.FromRoomId},{connection.ToRoomId},{connection.IsOptionalLoop},{connection.Kind};");
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            var tile = map.Tiles[y, x];
            snapshot.Append((int)tile.Terrain).Append((int)tile.Feature).Append(tile.RoomId ?? 0).Append(',');
        }
        return snapshot.ToString();
    }

    private static (int Minimum, int Maximum) WidthRange(RoomSizeCategory category) => category switch
    {
        RoomSizeCategory.Large => (12, 18),
        RoomSizeCategory.Medium => (8, 12),
        _ => (4, 7)
    };

    private static (int Minimum, int Maximum) HeightRange(RoomSizeCategory category) => category switch
    {
        RoomSizeCategory.Large => (8, 12),
        RoomSizeCategory.Medium => (6, 8),
        _ => (3, 5)
    };
}