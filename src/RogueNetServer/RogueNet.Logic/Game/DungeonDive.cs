namespace RogueNet.Logic.Game;

using RogueNet.Logic.MapGeneration;

public enum DungeonActionKind
{
    Move,
    Interact
}

public enum CardinalDirection
{
    North,
    East,
    South,
    West
}

public sealed record DungeonAction(DungeonActionKind Kind, CardinalDirection? Direction = null);

public sealed record DungeonActionResult(
    int Turn,
    MapPoint PlayerPosition,
    bool Succeeded,
    string Message);

public sealed record DungeonDiveSnapshot(
    Guid Id,
    int Seed,
    DungeonMap Map,
    MapPoint PlayerPosition,
    int Turn,
    DungeonActionResult? LastAction);

public sealed class DungeonDive
{
    private readonly object _sync = new();
    private readonly DungeonMap _map;
    private MapPoint _playerPosition;
    private int _turn;
    private DungeonActionResult? _lastAction;

    public Guid Id { get; } = Guid.NewGuid();
    public int Seed { get; }

    public DungeonDive(int seed, DungeonMap map)
    {
        ArgumentNullException.ThrowIfNull(map);

        Seed = seed;
        _map = CloneMap(map);
        _playerPosition = FindEntrance(_map);
    }

    public DungeonDiveSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new DungeonDiveSnapshot(
                Id,
                Seed,
                CloneMap(_map),
                _playerPosition,
                _turn,
                _lastAction);
        }
    }

    public DungeonDiveSnapshot SubmitAction(DungeonAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        lock (_sync)
        {
            bool succeeded;
            string message;
            switch (action.Kind)
            {
                case DungeonActionKind.Move:
                    succeeded = TryMove(action.Direction, out message);
                    break;
                case DungeonActionKind.Interact:
                    succeeded = TryInteract(action.Direction, out message);
                    break;
                default:
                    succeeded = SetFailure(out message, "That action is not available.");
                    break;
            }

            _turn++;
            _lastAction = new DungeonActionResult(_turn, _playerPosition, succeeded, message);

            return new DungeonDiveSnapshot(
                Id,
                Seed,
                CloneMap(_map),
                _playerPosition,
                _turn,
                _lastAction);
        }
    }

    private bool TryMove(CardinalDirection? direction, out string message)
    {
        if (direction is null || !Enum.IsDefined(typeof(CardinalDirection), direction.Value))
            return SetFailure(out message, "Choose a cardinal direction to move.");

        var (offsetX, offsetY) = direction.Value switch
        {
            CardinalDirection.North => (0, -1),
            CardinalDirection.East => (1, 0),
            CardinalDirection.South => (0, 1),
            CardinalDirection.West => (-1, 0),
            _ => (0, 0)
        };
        var destinationX = _playerPosition.X + offsetX;
        var destinationY = _playerPosition.Y + offsetY;

        if (destinationX < 0 || destinationX >= _map.Width || destinationY < 0 || destinationY >= _map.Height)
            return SetFailure(out message, "The dungeon ends there.");

        var destination = _map.Tiles[destinationY, destinationX];
        if (!IsWalkable(destination))
            return SetFailure(out message, destination.Feature switch
            {
                TileFeature.ClosedDoor => "The door is closed. Interact to open it.",
                TileFeature.LockedDoor => "The door is locked. Interact to unlock it.",
                TileFeature.Pillar => "A pillar blocks the way.",
                _ => "The way is blocked."
            });

        _playerPosition = new MapPoint(destinationX, destinationY);
        message = $"Moved {direction.Value.ToString().ToLowerInvariant()}.";
        return true;
    }

    private bool TryInteract(CardinalDirection? direction, out string message)
    {
        if (direction is null || !Enum.IsDefined(typeof(CardinalDirection), direction.Value))
            return SetFailure(out message, "Choose a direction to interact with.");

        var (offsetX, offsetY) = direction.Value switch
        {
            CardinalDirection.North => (0, -1),
            CardinalDirection.East => (1, 0),
            CardinalDirection.South => (0, 1),
            CardinalDirection.West => (-1, 0),
            _ => (0, 0)
        };
        var targetX = _playerPosition.X + offsetX;
        var targetY = _playerPosition.Y + offsetY;

        if (targetX < 0 || targetX >= _map.Width || targetY < 0 || targetY >= _map.Height)
            return SetFailure(out message, "There is nothing to interact with there.");

        var target = _map.Tiles[targetY, targetX];
        if (target.Feature is not (TileFeature.ClosedDoor or TileFeature.LockedDoor))
            return SetFailure(out message, "There is no door in that direction.");

        var wasLocked = target.Feature == TileFeature.LockedDoor;
        target.Feature = TileFeature.OpenDoor;
        target.Terrain = TerrainType.Floor;
        message = wasLocked ? "Unlocked and opened the door." : "Opened the door.";
        return true;
    }

    private static bool IsWalkable(DungeonTile tile) =>
        tile.Terrain == TerrainType.Floor && tile.Feature is not
            (TileFeature.ClosedDoor or TileFeature.LockedDoor or TileFeature.Pillar);

    private static MapPoint FindEntrance(DungeonMap map)
    {
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
        {
            if (map.Tiles[y, x].Feature == TileFeature.EntranceStairs)
                return new MapPoint(x, y);
        }

        throw new ArgumentException("The dungeon map must contain entrance stairs.", nameof(map));
    }

    private static DungeonMap CloneMap(DungeonMap source)
    {
        var clone = new DungeonMap(source.Width, source.Height);
        foreach (var room in source.Rooms)
            clone.AddRoom(room);
        foreach (var connection in source.Connections)
            clone.AddConnection(connection);

        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var sourceTile = source.Tiles[y, x];
            clone.Tiles[y, x].Terrain = sourceTile.Terrain;
            clone.Tiles[y, x].Feature = sourceTile.Feature;
            clone.Tiles[y, x].RoomId = sourceTile.RoomId;
        }

        return clone;
    }

    private static bool SetFailure(out string message, string failureMessage)
    {
        message = failureMessage;
        return false;
    }
}
