using Microsoft.AspNetCore.Mvc;
using RogueNet.API.Contracts;
using RogueNet.API.Game;
using RogueNet.Logic.Game;

namespace RogueNet.API.Controllers;

[ApiController]
[Route("api/dungeon-dives")]
public sealed class DungeonDivesController(
    DungeonDiveRegistry diveRegistry,
    ILogger<DungeonDivesController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<DungeonDiveResponse>(StatusCodes.Status201Created)]
    public ActionResult<DungeonDiveResponse> Start(StartDungeonDiveRequest request)
    {
        var snapshot = diveRegistry.Start(request.Seed);
        var response = DungeonDiveResponse.FromSnapshot(snapshot);
        logger.LogInformation("Started dungeon dive {DiveId} with seed {Seed}", response.Id, response.Seed);
        return CreatedAtAction(nameof(Get), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<DungeonDiveResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DungeonDiveResponse> Get(Guid id)
    {
        if (!diveRegistry.TryGet(id, out var snapshot))
            return NotFound();

        return Ok(DungeonDiveResponse.FromSnapshot(snapshot!));
    }

    [HttpPost("{id:guid}/actions")]
    [ProducesResponseType<DungeonDiveResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<DungeonDiveResponse> SubmitAction(Guid id, DungeonActionRequest request)
    {
        var action = new DungeonAction(request.Kind, request.Direction);
        if (!diveRegistry.TrySubmitAction(id, action, out var snapshot))
            return NotFound();

        logger.LogInformation(
            "Processed {ActionKind} action for dungeon dive {DiveId} at turn {Turn}: {Succeeded}",
            request.Kind, id, snapshot!.Turn, snapshot.LastAction!.Succeeded);
        return Ok(DungeonDiveResponse.FromSnapshot(snapshot));
    }
}