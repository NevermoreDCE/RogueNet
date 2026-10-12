using RogueNet.Logic.Game;
using RogueNet.Logic.MapGeneration;

namespace RogueNet.API.Contracts;

public sealed record StartDungeonDiveRequest(int? Seed);

public sealed record DungeonActionRequest(DungeonActionKind Kind, CardinalDirection? Direction);

public sealed record MapPointResponse(int X, int Y);

public sealed record DungeonActionResultResponse(
    int Turn,
    MapPointResponse PlayerPosition,
    bool Succeeded,
    string Message);

public sealed record DungeonDiveResponse(
    Guid Id,
    int Seed,
    DungeonMapResponse Map,
    MapPointResponse PlayerPosition,
    int Turn,
    DungeonActionResultResponse? LastAction)
{
    public static DungeonDiveResponse FromSnapshot(DungeonDiveSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Seed,
        DungeonMapResponse.FromMap(snapshot.Seed, snapshot.Map),
        new MapPointResponse(snapshot.PlayerPosition.X, snapshot.PlayerPosition.Y),
        snapshot.Turn,
        snapshot.LastAction is null
            ? null
            : new DungeonActionResultResponse(
                snapshot.LastAction.Turn,
                new MapPointResponse(snapshot.LastAction.PlayerPosition.X, snapshot.LastAction.PlayerPosition.Y),
                snapshot.LastAction.Succeeded,
                snapshot.LastAction.Message));
}