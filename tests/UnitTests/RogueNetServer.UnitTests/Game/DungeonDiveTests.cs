using RogueNet.Logic.Game;
using RogueNet.Logic.MapGeneration;

namespace RogueNetServer.UnitTests.Game;

public sealed class DungeonDiveTests
{
    [Fact]
    public void Move_toFloor_advancesOneTurnAndChangesPosition()
    {
        var dive = CreateDive();

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Move, CardinalDirection.East));

        Assert.Equal(1, result.Turn);
        Assert.Equal(new MapPoint(4, 3), result.PlayerPosition);
        Assert.True(result.LastAction!.Succeeded);
    }

    [Theory]
    [InlineData(CardinalDirection.North, TileFeature.ClosedDoor)]
    [InlineData(CardinalDirection.South, TileFeature.LockedDoor)]
    [InlineData(CardinalDirection.West, TileFeature.None)]
    public void Move_intoObstruction_doesNotMoveAndConsumesOneTurn(
        CardinalDirection direction,
        TileFeature expectedFeature)
    {
        var dive = CreateDive();

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Move, direction));

        Assert.Equal(1, result.Turn);
        Assert.Equal(new MapPoint(3, 3), result.PlayerPosition);
        Assert.False(result.LastAction!.Succeeded);
        Assert.NotEmpty(result.LastAction.Message);
        Assert.Equal(expectedFeature, result.Map.Tiles[3 + OffsetY(direction), 3 + OffsetX(direction)].Feature);
    }

    [Fact]
    public void Move_beyondMapBoundaryDoesNotChangePositionAndConsumesTurn()
    {
        var dive = CreateDive(playerX: 0, playerY: 3);

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Move, CardinalDirection.West));

        Assert.Equal(1, result.Turn);
        Assert.Equal(new MapPoint(0, 3), result.PlayerPosition);
        Assert.False(result.LastAction!.Succeeded);
        Assert.Contains("ends", result.LastAction.Message);
    }

    [Fact]
    public void Move_throughOpenDoorSucceeds()
    {
        var dive = CreateDive(openDoorEast: true);

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Move, CardinalDirection.East));

        Assert.Equal(new MapPoint(4, 3), result.PlayerPosition);
        Assert.True(result.LastAction!.Succeeded);
    }

    [Theory]
    [InlineData(TileFeature.ClosedDoor, "Opened the door.")]
    [InlineData(TileFeature.LockedDoor, "Unlocked and opened the door.")]
    public void Interact_opensAdjacentDoorInOneTurn(TileFeature door, string expectedMessage)
    {
        var dive = CreateDive(northDoor: door);

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Interact, CardinalDirection.North));

        Assert.Equal(1, result.Turn);
        Assert.Equal(new MapPoint(3, 3), result.PlayerPosition);
        Assert.True(result.LastAction!.Succeeded);
        Assert.Equal(expectedMessage, result.LastAction.Message);
        Assert.Equal(TileFeature.OpenDoor, result.Map.Tiles[2, 3].Feature);
        Assert.Equal(TerrainType.Floor, result.Map.Tiles[2, 3].Terrain);
    }

    [Fact]
    public void Interact_withoutAdjacentDoorConsumesTurnAndReportsFailure()
    {
        var dive = CreateDive();

        var result = dive.SubmitAction(new DungeonAction(DungeonActionKind.Interact, CardinalDirection.East));

        Assert.Equal(1, result.Turn);
        Assert.Equal(new MapPoint(3, 3), result.PlayerPosition);
        Assert.False(result.LastAction!.Succeeded);
        Assert.Contains("no door", result.LastAction.Message);
    }

    [Fact]
    public void Snapshot_doesNotExposeMutableAuthoritativeMap()
    {
        var dive = CreateDive();
        var snapshot = dive.GetSnapshot();

        snapshot.Map.Tiles[3, 4].Terrain = TerrainType.Wall;
        snapshot.Map.Tiles[3, 4].Feature = TileFeature.LockedDoor;

        var nextSnapshot = dive.GetSnapshot();

        Assert.Equal(TerrainType.Floor, nextSnapshot.Map.Tiles[3, 4].Terrain);
        Assert.Equal(TileFeature.None, nextSnapshot.Map.Tiles[3, 4].Feature);
    }

    private static DungeonDive CreateDive(
        int playerX = 3,
        int playerY = 3,
        TileFeature northDoor = TileFeature.ClosedDoor,
        bool openDoorEast = false)
    {
        var map = new DungeonMap(7, 7);
        for (var y = 0; y < map.Height; y++)
        for (var x = 0; x < map.Width; x++)
            map.Tiles[y, x].Terrain = TerrainType.Wall;

        map.Tiles[playerY, playerX].Terrain = TerrainType.Floor;
        map.Tiles[playerY, playerX].Feature = TileFeature.EntranceStairs;
        map.Tiles[3, 4].Terrain = TerrainType.Floor;
        if (openDoorEast)
            map.Tiles[3, 4].Feature = TileFeature.OpenDoor;
        map.Tiles[2, 3].Feature = northDoor;
        map.Tiles[4, 3].Feature = TileFeature.LockedDoor;

        return new DungeonDive(13579, map);
    }

    private static int OffsetX(CardinalDirection direction) => direction switch
    {
        CardinalDirection.East => 1,
        CardinalDirection.West => -1,
        _ => 0
    };

    private static int OffsetY(CardinalDirection direction) => direction switch
    {
        CardinalDirection.North => -1,
        CardinalDirection.South => 1,
        _ => 0
    };
}
