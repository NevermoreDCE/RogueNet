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
        var response = DungeonMapResponse.FromMap(actualSeed, map);

        logger.LogInformation(
            "Generated dungeon map for seed {Seed} with {Width} by {Height} tiles, {RoomCount} rooms, and {ConnectionCount} connections",
            actualSeed, map.Width, map.Height, map.Rooms.Count, map.Connections.Count);

        return Ok(response);
    }
}
