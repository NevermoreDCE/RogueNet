using Microsoft.AspNetCore.Mvc;
using RogueNet.API.Contracts;
using RogueNet.Logic.MapGeneration;

namespace RogueNet.API.Controllers;

[ApiController]
[Route("api/maps")]
public sealed class MapsController(
    DungeonMapGenerator mapGenerator,
    ILogger<MapsController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DungeonMapResponse>(StatusCodes.Status200OK)]
    public ActionResult<DungeonMapResponse> Get([FromQuery] int? seed)
    {
        var actualSeed = seed ?? Random.Shared.Next();
        var map = mapGenerator.Generate(random: new Random(actualSeed));
        var response = new DungeonMapResponse(
            actualSeed,
            map.Width,
            map.Height,
            map.Rooms.Select(room => new DungeonRoomResponse(
                room.Id, room.X, room.Y, room.Width, room.Height, room.Category)).ToArray(),
            map.Connections.Select(connection => new DungeonRoomConnectionResponse(
                connection.FromRoomId, connection.ToRoomId, connection.IsOptionalLoop, connection.Kind)).ToArray(),
            Enumerable.Range(0, map.Height)
                .Select(y => (IReadOnlyList<DungeonTileResponse>)Enumerable.Range(0, map.Width)
                    .Select(x => map.Tiles[y, x])
                    .Select(tile => new DungeonTileResponse(tile.Terrain, tile.Feature, tile.RoomId))
                    .ToArray())
                .ToArray());

        logger.LogInformation(
            "Generated dungeon map for seed {Seed} with {Width} by {Height} tiles, {RoomCount} rooms, and {ConnectionCount} connections",
            actualSeed, map.Width, map.Height, map.Rooms.Count, map.Connections.Count);

        return Ok(response);
    }
}
