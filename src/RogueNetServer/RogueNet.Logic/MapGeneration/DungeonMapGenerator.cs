namespace RogueNet.Logic.MapGeneration;

/// <summary>Generates a map by choosing room sizes and placing non-overlapping rooms.</summary>
public sealed class DungeonMapGenerator
{
    private const int RoomBuffer = 1;

    public DungeonMap Generate(MapGenerationSettings? settings = null, Random? random = null)
    {
        settings ??= new MapGenerationSettings();
        random ??= Random.Shared;

        ValidateSettings(settings);

        var map = new DungeonMap(settings.Width, settings.Height);
        var nextRoomId = 1;

        foreach (var size in settings.RoomSizes)
        {
            var roomCount = random.Next(size.MinimumCount, size.MaximumCount + 1);

            for (var i = 0; i < roomCount; i++)
            {
                var room = TryPlaceRoom(map, size, nextRoomId, settings.RoomPlacementAttempts, random);
                if (room is null)
                {
                    throw new InvalidOperationException(
                        $"Could not place room {i + 1} of {roomCount} for category {size.Category}. " +
                        $"Increase the map dimensions or room placement attempts.");
                }

                map.AddRoom(room);
                StampRoomFloor(map, room);
                nextRoomId++;
            }
        }

        BuildRoomWalls(map);
        ConnectRooms(map, settings, random);
        CarveCorridors(map, settings, random);
        CreateDirectRoomConnections(map, settings, random);
        BuildWallRing(map);
        PlaceStairs(map, settings.MinimumStairDistance, random);
        PlaceDecorations(map, settings, random);
        return map;
    }

    private static void BuildRoomWalls(DungeonMap map)
    {
        foreach (var room in map.Rooms)
        {
            for (var y = room.Y; y < room.Y + room.Height; y++)
            for (var x = room.X; x < room.X + room.Width; x++)
            {
                if (x == room.X || x == room.X + room.Width - 1 ||
                    y == room.Y || y == room.Y + room.Height - 1)
                    map.Tiles[y, x].Terrain = TerrainType.Wall;
            }
        }
    }

    private static void BuildWallRing(DungeonMap map)
    {
        var wallLocations = new List<MapPoint>();

        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            var terrain = map.Tiles[y, x].Terrain;
            if (terrain == TerrainType.Floor &&
                (x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1))
            {
                // The map has no tiles beyond its bounds, so use its edge tiles as the
                // outer wall instead of leaving edge-touching rooms open to the void.
                wallLocations.Add(new MapPoint(x, y));
                continue;
            }

            if (terrain != TerrainType.Rock)
                continue;

            var touchesFloor = false;
            for (var offsetY = -1; offsetY <= 1 && !touchesFloor; offsetY++)
            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                if (offsetX == 0 && offsetY == 0)
                    continue;

                var neighborX = x + offsetX;
                var neighborY = y + offsetY;
                if (neighborX >= 0 && neighborX < map.Width &&
                    neighborY >= 0 && neighborY < map.Height &&
                    map.Tiles[neighborY, neighborX].Terrain == TerrainType.Floor)
                {
                    touchesFloor = true;
                    break;
                }
            }

            if (touchesFloor)
                wallLocations.Add(new MapPoint(x, y));
        }

        foreach (var point in wallLocations)
            map.Tiles[point.Y, point.X].Terrain = TerrainType.Wall;
    }

    private static void PlaceStairs(DungeonMap map, int minimumDistance, Random random)
    {
        var floorTiles = new List<MapPoint>();
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            var tile = map.Tiles[y, x];
            if (tile.Terrain == TerrainType.Floor && tile.Feature == TileFeature.None)
                floorTiles.Add(new MapPoint(x, y));
        }

        Shuffle(floorTiles, random);
        foreach (var entrance in floorTiles)
        {
            var exitCandidates = floorTiles
                .Where(candidate => candidate != entrance &&
                                    ManhattanDistance(entrance, candidate) >= minimumDistance)
                .ToArray();

            if (exitCandidates.Length == 0)
                continue;

            map.Tiles[entrance.Y, entrance.X].Feature = TileFeature.EntranceStairs;
            var exit = exitCandidates[random.Next(exitCandidates.Length)];
            map.Tiles[exit.Y, exit.X].Feature = TileFeature.ExitStairs;
            return;
        }

        throw new InvalidOperationException(
            $"Could not place entrance and exit stairs at least {minimumDistance} tiles apart. " +
            "Increase the map size or lower the minimum stair distance.");
    }

    private static long ManhattanDistance(MapPoint first, MapPoint second) =>
        Math.Abs((long)first.X - second.X) + Math.Abs((long)first.Y - second.Y);

    private static void PlaceDecorations(DungeonMap map, MapGenerationSettings settings, Random random)
    {
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            var tile = map.Tiles[y, x];
            if (tile.Terrain != TerrainType.Floor || tile.RoomId is null || tile.Feature != TileFeature.None)
                continue;

            var roll = random.NextDouble();
            if (roll < settings.PillarChancePerRoomFloor)
                tile.Feature = TileFeature.Pillar;
            else if (roll < settings.PillarChancePerRoomFloor + settings.WaterChancePerRoomFloor)
                tile.Feature = TileFeature.Water;
            else if (roll < settings.PillarChancePerRoomFloor + settings.WaterChancePerRoomFloor + settings.TrapChancePerRoomFloor)
                tile.Feature = TileFeature.Trap;
            else if (roll < settings.PillarChancePerRoomFloor + settings.WaterChancePerRoomFloor +
                     settings.TrapChancePerRoomFloor + settings.TreasureChancePerRoomFloor)
                tile.Feature = TileFeature.Treasure;
        }
    }

    private static void Shuffle<T>(IList<T> items, Random random)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private static void CreateDirectRoomConnections(
        DungeonMap map,
        MapGenerationSettings settings,
        Random random)
    {
        var rooms = map.Rooms;
        for (var firstIndex = 0; firstIndex < rooms.Count - 1; firstIndex++)
        for (var secondIndex = firstIndex + 1; secondIndex < rooms.Count; secondIndex++)
        {
            var firstRoom = rooms[firstIndex];
            var secondRoom = rooms[secondIndex];

            if (map.Connections.Any(connection => connection.Connects(firstRoom.Id, secondRoom.Id)))
                continue;

            var passage = FindDirectPassage(firstRoom, secondRoom);
            if (passage is null || random.NextDouble() >= settings.DirectRoomConnectionChance)
                continue;

            if (random.NextDouble() < settings.RoomMergeChance)
            {
                // Keep component room IDs for graph references while opening every shared wall.
                foreach (var point in passage.Options.SelectMany(option => option))
                {
                    map.Tiles[point.Y, point.X].Terrain = TerrainType.Floor;
                    map.Tiles[point.Y, point.X].Feature = TileFeature.None;
                }

                map.AddConnection(new RoomConnection(
                    firstRoom.Id,
                    secondRoom.Id,
                    Kind: RoomConnectionKind.MergedRoom));
                continue;
            }

            // Choose one shared wall position, opening the aligned wall and gap tiles
            // around a single closed door. This guarantees a one-tile doorway.
            var crossing = passage.Options[random.Next(passage.Options.Count)];
            foreach (var point in crossing)
            {
                map.Tiles[point.Y, point.X].Terrain = TerrainType.Floor;
                map.Tiles[point.Y, point.X].Feature = TileFeature.None;
            }

            var door = crossing[0];
            TryPlaceDoor(map.Tiles[door.Y, door.X], settings, random);
            map.AddConnection(new RoomConnection(
                firstRoom.Id,
                secondRoom.Id,
                Kind: RoomConnectionKind.Doorway));
        }
    }

    private static DirectPassage? FindDirectPassage(Room first, Room second)
    {
        if (first.X + first.Width == second.X - 1 || second.X + second.Width == first.X - 1)
        {
            var left = first.X < second.X ? first : second;
            var right = first.X < second.X ? second : first;
            var overlapStart = Math.Max(left.Y, right.Y);
            var overlapEnd = Math.Min(left.Y + left.Height, right.Y + right.Height);
            if (overlapStart < overlapEnd)
            {
                var firstX = left.X + left.Width - 1;
                var lastX = right.X;
                var options = Enumerable.Range(overlapStart, overlapEnd - overlapStart)
                    .Select(y => (IReadOnlyList<MapPoint>)Enumerable.Range(firstX, lastX - firstX + 1)
                        .Select(x => new MapPoint(x, y))
                        .ToArray())
                    .ToArray();
                return new DirectPassage(options);
            }
        }

        if (first.Y + first.Height == second.Y - 1 || second.Y + second.Height == first.Y - 1)
        {
            var top = first.Y < second.Y ? first : second;
            var bottom = first.Y < second.Y ? second : first;
            var overlapStart = Math.Max(top.X, bottom.X);
            var overlapEnd = Math.Min(top.X + top.Width, bottom.X + bottom.Width);
            if (overlapStart < overlapEnd)
            {
                var firstY = top.Y + top.Height - 1;
                var lastY = bottom.Y;
                var options = Enumerable.Range(overlapStart, overlapEnd - overlapStart)
                    .Select(x => (IReadOnlyList<MapPoint>)Enumerable.Range(firstY, lastY - firstY + 1)
                        .Select(y => new MapPoint(x, y))
                        .ToArray())
                    .ToArray();
                return new DirectPassage(options);
            }
        }

        return null;
    }

    private static void CarveCorridors(DungeonMap map, MapGenerationSettings settings, Random random)
    {
        var roomsById = map.Rooms.ToDictionary(room => room.Id);

        foreach (var connection in map.Connections)
        {
            if (connection.Kind != RoomConnectionKind.Corridor)
                continue;

            var start = roomsById[connection.FromRoomId].Center;
            var end = roomsById[connection.ToRoomId].Center;

            if (random.Next(2) == 0)
            {
                // Horizontal first, then vertical.
                CarveHorizontal(map, start.X, end.X, start.Y, settings, random);
                CarveVertical(map, start.Y, end.Y, end.X, settings, random);
            }
            else
            {
                // Vertical first, then horizontal.
                CarveVertical(map, start.Y, end.Y, start.X, settings, random);
                CarveHorizontal(map, start.X, end.X, end.Y, settings, random);
            }
        }
    }

    private static void CarveHorizontal(
        DungeonMap map,
        int startX,
        int endX,
        int y,
        MapGenerationSettings settings,
        Random random)
    {
        for (var x = Math.Min(startX, endX); x <= Math.Max(startX, endX); x++)
            CarveCorridorTile(map.Tiles[y, x], settings, random);
    }

    private static void CarveVertical(
        DungeonMap map,
        int startY,
        int endY,
        int x,
        MapGenerationSettings settings,
        Random random)
    {
        for (var y = Math.Min(startY, endY); y <= Math.Max(startY, endY); y++)
            CarveCorridorTile(map.Tiles[y, x], settings, random);
    }

    private static void CarveCorridorTile(DungeonTile tile, MapGenerationSettings settings, Random random)
    {
        if (tile.RoomId is not null && tile.Terrain == TerrainType.Wall)
        {
            if (tile.Feature == TileFeature.None)
                TryPlaceDoor(tile, settings, random);
            return;
        }

        tile.Terrain = TerrainType.Floor;
    }

    private static void TryPlaceDoor(DungeonTile tile, MapGenerationSettings settings, Random random)
    {
        if (random.NextDouble() >= settings.DoorChance)
        {
            tile.Terrain = TerrainType.Floor;
            tile.Feature = TileFeature.None;
            return;
        }

        var stateRoll = random.NextDouble();
        if (stateRoll < settings.LockedDoorChance)
        {
            tile.Terrain = TerrainType.Wall;
            tile.Feature = TileFeature.LockedDoor;
        }
        else if (stateRoll < settings.LockedDoorChance + settings.OpenDoorChance)
        {
            tile.Terrain = TerrainType.Floor;
            tile.Feature = TileFeature.OpenDoor;
        }
        else
        {
            tile.Terrain = TerrainType.Wall;
            tile.Feature = TileFeature.ClosedDoor;
        }
    }

    private static void ConnectRooms(DungeonMap map, MapGenerationSettings settings, Random random)
    {
        var rooms = map.Rooms;
        if (rooms.Count < 2)
            return;

        var edges = new List<RoomEdge>();
        for (var firstIndex = 0; firstIndex < rooms.Count - 1; firstIndex++)
        for (var secondIndex = firstIndex + 1; secondIndex < rooms.Count; secondIndex++)
        {
            var first = rooms[firstIndex];
            var second = rooms[secondIndex];
            var dx = (long)first.Center.X - second.Center.X;
            var dy = (long)first.Center.Y - second.Center.Y;

            edges.Add(new RoomEdge(firstIndex, secondIndex, first.Id, second.Id, dx * dx + dy * dy));
        }

        edges.Sort(static (left, right) =>
        {
            var distanceComparison = left.DistanceSquared.CompareTo(right.DistanceSquared);
            if (distanceComparison != 0)
                return distanceComparison;

            var firstIdComparison = left.FromRoomId.CompareTo(right.FromRoomId);
            return firstIdComparison != 0
                ? firstIdComparison
                : left.ToRoomId.CompareTo(right.ToRoomId);
        });

        var components = new DisjointSet(rooms.Count);
        var minimumSpanningTreeEdges = new HashSet<(int First, int Second)>();

        foreach (var edge in edges)
        {
            if (!components.Union(edge.FirstIndex, edge.SecondIndex))
                continue;

            map.AddConnection(new RoomConnection(edge.FromRoomId, edge.ToRoomId));
            minimumSpanningTreeEdges.Add((edge.FirstIndex, edge.SecondIndex));

            if (minimumSpanningTreeEdges.Count == rooms.Count - 1)
                break;
        }

        var optionalEdges = edges
            .Where(edge => !minimumSpanningTreeEdges.Contains((edge.FirstIndex, edge.SecondIndex)))
            .ToList();

        var requestedExtraConnections = random.Next(
            settings.MinimumExtraConnections,
            settings.MaximumExtraConnections + 1);
        var extraConnectionCount = Math.Min(requestedExtraConnections, optionalEdges.Count);

        // Shuffle the non-MST edges so loops are selected without favoring short routes.
        for (var i = optionalEdges.Count - 1; i > 0; i--)
        {
            var swapIndex = random.Next(i + 1);
            (optionalEdges[i], optionalEdges[swapIndex]) = (optionalEdges[swapIndex], optionalEdges[i]);
        }

        for (var i = 0; i < extraConnectionCount; i++)
        {
            var edge = optionalEdges[i];
            map.AddConnection(new RoomConnection(edge.FromRoomId, edge.ToRoomId, IsOptionalLoop: true));
        }
    }

    private static Room? TryPlaceRoom(
        DungeonMap map,
        RoomSizeDefinition size,
        int roomId,
        int maximumAttempts,
        Random random)
    {
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var width = random.Next(size.MinimumWidth, size.MaximumWidth + 1);
            var height = random.Next(size.MinimumHeight, size.MaximumHeight + 1);
            var x = random.Next(0, map.Width - width + 1);
            var y = random.Next(0, map.Height - height + 1);
            var candidate = new Room(roomId, x, y, width, height, size.Category);

            if (map.Rooms.Any(existing => candidate.Overlaps(existing, RoomBuffer)))
                continue;

            return candidate;
        }

        return null;
    }

    private static void StampRoomFloor(DungeonMap map, Room room)
    {
        for (var y = room.Y; y < room.Y + room.Height; y++)
        for (var x = room.X; x < room.X + room.Width; x++)
        {
            map.Tiles[y, x].Terrain = TerrainType.Floor;
            map.Tiles[y, x].RoomId = room.Id;
        }
    }

    private static void ValidateSettings(MapGenerationSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(settings.RoomPlacementAttempts);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MinimumExtraConnections);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MaximumExtraConnections);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MinimumStairDistance);
        ArgumentNullException.ThrowIfNull(settings.RoomSizes);

        if (settings.MaximumExtraConnections < settings.MinimumExtraConnections)
            throw new ArgumentException("Maximum extra connections must be at least the minimum.", nameof(settings));

        if (!double.IsFinite(settings.DirectRoomConnectionChance) ||
            settings.DirectRoomConnectionChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "Direct room connection chance must be between 0 and 1.");

        if (!double.IsFinite(settings.RoomMergeChance) || settings.RoomMergeChance is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(settings), "Room merge chance must be between 0 and 1.");

        var doorChances = new[]
        {
            settings.DoorChance,
            settings.LockedDoorChance,
            settings.OpenDoorChance,
            settings.ClosedDoorChance
        };
        if (doorChances.Any(chance => !double.IsFinite(chance) || chance is < 0 or > 1))
            throw new ArgumentException("Door chances must be between 0 and 1.", nameof(settings));

        if (Math.Abs(settings.LockedDoorChance + settings.OpenDoorChance + settings.ClosedDoorChance - 1) > 1e-9)
            throw new ArgumentException("Locked, open, and closed door chances must sum to 1.", nameof(settings));

        var decorationChances = new[]
        {
            settings.PillarChancePerRoomFloor,
            settings.WaterChancePerRoomFloor,
            settings.TrapChancePerRoomFloor,
            settings.TreasureChancePerRoomFloor
        };
        if (decorationChances.Any(chance => !double.IsFinite(chance) || chance is < 0 or > 1) ||
            decorationChances.Sum() > 1)
            throw new ArgumentException("Decoration chances must be between 0 and 1 and sum to no more than 1.", nameof(settings));

        if (settings.RoomSizes.Count == 0)
            throw new ArgumentException("At least one room size definition is required.", nameof(settings));

        foreach (var size in settings.RoomSizes)
        {
            if (size.MinimumWidth <= 0 || size.MaximumWidth < size.MinimumWidth || size.MaximumWidth > settings.Width)
                throw new ArgumentException($"Invalid width range for room category {size.Category}.", nameof(settings));

            if (size.MinimumHeight <= 0 || size.MaximumHeight < size.MinimumHeight || size.MaximumHeight > settings.Height)
                throw new ArgumentException($"Invalid height range for room category {size.Category}.", nameof(settings));

            if (size.MinimumCount < 0 || size.MaximumCount < size.MinimumCount)
                throw new ArgumentException($"Invalid room count range for category {size.Category}.", nameof(settings));
        }
    }

    private sealed record RoomEdge(
        int FirstIndex,
        int SecondIndex,
        int FromRoomId,
        int ToRoomId,
        long DistanceSquared);

    private sealed record DirectPassage(IReadOnlyList<IReadOnlyList<MapPoint>> Options);

    private sealed class DisjointSet(int count)
    {
        private readonly int[] _parents = Enumerable.Range(0, count).ToArray();
        private readonly byte[] _ranks = new byte[count];

        public bool Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot == secondRoot)
                return false;

            if (_ranks[firstRoot] < _ranks[secondRoot])
                (firstRoot, secondRoot) = (secondRoot, firstRoot);

            _parents[secondRoot] = firstRoot;
            if (_ranks[firstRoot] == _ranks[secondRoot])
                _ranks[firstRoot]++;

            return true;
        }

        private int Find(int item)
        {
            if (_parents[item] != item)
                _parents[item] = Find(_parents[item]);

            return _parents[item];
        }
    }
}