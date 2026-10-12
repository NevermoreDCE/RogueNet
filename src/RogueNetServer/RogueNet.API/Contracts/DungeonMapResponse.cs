using RogueNet.Logic.MapGeneration;

namespace RogueNet.API.Contracts;

/// <summary>A generated dungeon map safe to return to a game client.</summary>
public sealed record DungeonMapResponse(
    int Seed,
    int Width,
    int Height,
    IReadOnlyList<DungeonRoomResponse> Rooms,
    IReadOnlyList<DungeonRoomConnectionResponse> Connections,
    IReadOnlyList<IReadOnlyList<DungeonTileResponse>> Tiles);

/// <summary>Room bounds and category for rendering or client-side navigation.</summary>
public sealed record DungeonRoomResponse(
    int Id,
    int X,
    int Y,
    int Width,
    int Height,
    RoomSizeCategory Category);

/// <summary>A connection between two generated rooms.</summary>
public sealed record DungeonRoomConnectionResponse(
    int FromRoomId,
    int ToRoomId,
    bool IsOptionalLoop,
    RoomConnectionKind Kind);

/// <summary>Terrain, feature, and room ownership for one map cell.</summary>
public sealed record DungeonTileResponse(TerrainType Terrain, TileFeature Feature, int? RoomId);
