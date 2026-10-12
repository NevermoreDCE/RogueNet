namespace RogueNet.Logic.MapGeneration;

public enum TerrainType
{
    Rock,
    Floor,
    Wall
}

public enum TileFeature
{
    None,
    ClosedDoor,
    OpenDoor,
    LockedDoor,
    EntranceStairs,
    ExitStairs,
    Pillar,
    Water,
    Trap,
    Treasure
}

public enum RoomSizeCategory
{
    Large,
    Medium,
    Small
}

public enum RoomConnectionKind
{
    Corridor,
    Doorway,
    MergedRoom
}

public sealed class DungeonTile
{
    public TerrainType Terrain { get; set; } = TerrainType.Rock;
    public TileFeature Feature { get; set; } = TileFeature.None;
    public int? RoomId { get; set; }
}

public sealed class DungeonMap
{
    private readonly List<Room> _rooms = [];
    private readonly List<RoomConnection> _connections = [];

    public int Width { get; }
    public int Height { get; }
    public DungeonTile[,] Tiles { get; }
    public IReadOnlyList<Room> Rooms => _rooms;
    public IReadOnlyList<RoomConnection> Connections => _connections;

    public DungeonMap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        Width = width;
        Height = height;
        Tiles = new DungeonTile[height, width];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            Tiles[y, x] = new DungeonTile();
    }

    public void AddRoom(Room room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (room.X < 0 || room.Y < 0 || room.X + room.Width > Width || room.Y + room.Height > Height)
            throw new ArgumentOutOfRangeException(nameof(room), "The room must fit within the dungeon map.");

        if (_rooms.Any(existing => existing.Id == room.Id))
            throw new ArgumentException($"A room with ID {room.Id} already exists.", nameof(room));

        _rooms.Add(room);
    }

    public void AddConnection(RoomConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_rooms.Any(room => room.Id == connection.FromRoomId) ||
            !_rooms.Any(room => room.Id == connection.ToRoomId))
            throw new ArgumentException("Both rooms must exist on the map before adding a connection.", nameof(connection));

        if (_connections.Any(existing => existing.Connects(connection.FromRoomId, connection.ToRoomId)))
            throw new ArgumentException("A connection between these rooms already exists.", nameof(connection));

        _connections.Add(connection);
    }
}

public sealed record MapPoint(int X, int Y);

/// <summary>Room bounds use map coordinates; right and bottom edges are exclusive.</summary>
public sealed record Room(int Id, int X, int Y, int Width, int Height, RoomSizeCategory Category)
{
    public MapPoint Center => new(X + Width / 2, Y + Height / 2);

    public bool Overlaps(Room other, int buffer = 0)
    {
        ArgumentNullException.ThrowIfNull(other);
        ArgumentOutOfRangeException.ThrowIfNegative(buffer);

        return X < other.X + other.Width + buffer &&
               X + Width + buffer > other.X &&
               Y < other.Y + other.Height + buffer &&
               Y + Height + buffer > other.Y;
    }
}

/// <summary>An edge in the room connectivity graph, later carved as a corridor.</summary>
public sealed record RoomConnection(
    int FromRoomId,
    int ToRoomId,
    bool IsOptionalLoop = false,
    RoomConnectionKind Kind = RoomConnectionKind.Corridor)
{
    public bool Connects(int firstRoomId, int secondRoomId) =>
        (FromRoomId == firstRoomId && ToRoomId == secondRoomId) ||
        (FromRoomId == secondRoomId && ToRoomId == firstRoomId);
}