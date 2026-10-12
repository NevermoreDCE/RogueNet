namespace RogueNet.Logic.MapGeneration;

public sealed record RoomSizeDefinition(
    RoomSizeCategory Category,
    int MinimumWidth,
    int MaximumWidth,
    int MinimumHeight,
    int MaximumHeight,
    int MinimumCount,
    int MaximumCount);

public sealed class MapGenerationSettings
{
    public int Width { get; init; } = 80;
    public int Height { get; init; } = 25;
    public int RoomPlacementAttempts { get; init; } = 100;
    public int MinimumExtraConnections { get; init; } = 1;
    public int MaximumExtraConnections { get; init; } = 3;
    public int MinimumStairDistance { get; init; } = 10;
    public double DirectRoomConnectionChance { get; init; } = 0.25;
    public double RoomMergeChance { get; init; } = 0.20;
    public double DoorChance { get; init; } = 0.25;
    public double LockedDoorChance { get; init; } = 0.10;
    public double OpenDoorChance { get; init; } = 0.30;
    public double ClosedDoorChance { get; init; } = 0.60;
    public double PillarChancePerRoomFloor { get; init; } = 0.01;
    public double WaterChancePerRoomFloor { get; init; } = 0.005;
    public double TrapChancePerRoomFloor { get; init; } = 0.005;
    public double TreasureChancePerRoomFloor { get; init; } = 0.003;

    public IReadOnlyList<RoomSizeDefinition> RoomSizes { get; init; } =
    [
        new(RoomSizeCategory.Large, 12, 18, 8, 12, 2, 3),
        new(RoomSizeCategory.Medium, 8, 12, 6, 8, 4, 6),
        new(RoomSizeCategory.Small, 4, 7, 3, 5, 6, 10)
    ];
}