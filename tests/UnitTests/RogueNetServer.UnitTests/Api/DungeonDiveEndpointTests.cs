using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RogueNetServer.UnitTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class DungeonDiveEndpointTests(WebApplicationFactory<Program> factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task StartDive_returnsSeededMapAndSpawnsOnEntrance()
    {
        using var response = await _client.PostAsJsonAsync("/api/dungeon-dives", new { seed = 37007434 });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var dive = document.RootElement;

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(37007434, dive.GetProperty("seed").GetInt32());
        Assert.Equal(0, dive.GetProperty("turn").GetInt32());
        Assert.Null(dive.GetProperty("lastAction").ValueKind == JsonValueKind.Null ? null : dive.GetProperty("lastAction"));

        var position = dive.GetProperty("playerPosition");
        var spawnTile = dive.GetProperty("map").GetProperty("tiles")
            [position.GetProperty("y").GetInt32()][position.GetProperty("x").GetInt32()];
        Assert.Equal("EntranceStairs", spawnTile.GetProperty("feature").GetString());

        var id = dive.GetProperty("id").GetString();
        using var getResponse = await _client.GetAsync($"/api/dungeon-dives/{id}");
        using var getDocument = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(dive.GetProperty("id").GetString(), getDocument.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Actions_validateMovementAdvanceOneTurnAndIgnoreClientCoordinates()
    {
        using var started = await StartDiveAsync(37007434);
        var dive = started.RootElement;
        var id = dive.GetProperty("id").GetString();
        var start = ReadPosition(dive.GetProperty("playerPosition"));

        Assert.True(TryFindDirection(dive, walkable: true, out var legalDirection, out var legalDestination),
            "The generated entrance must have a walkable cardinal neighbor.");
        using var legalResponse = await SubmitMoveAsync(id!, legalDirection);
        using var legalDocument = JsonDocument.Parse(await legalResponse.Content.ReadAsStringAsync());
        var afterLegalMove = legalDocument.RootElement;

        Assert.Equal(HttpStatusCode.OK, legalResponse.StatusCode);
        Assert.Equal(1, afterLegalMove.GetProperty("turn").GetInt32());
        Assert.Equal(legalDestination, ReadPosition(afterLegalMove.GetProperty("playerPosition")));
        Assert.True(afterLegalMove.GetProperty("lastAction").GetProperty("succeeded").GetBoolean());

        var blockedApproach = FindBlockedApproach(afterLegalMove);
        using var approachDocument = await SubmitPathAsync(id!, afterLegalMove, blockedApproach.Path);
        var approachPosition = ReadPosition(approachDocument.RootElement.GetProperty("playerPosition"));
        var expectedBlockedTurn = approachDocument.RootElement.GetProperty("turn").GetInt32() + 1;
        using var blockedResponse = await SubmitMoveAsync(id!, blockedApproach.Direction);
        using var blockedDocument = JsonDocument.Parse(await blockedResponse.Content.ReadAsStringAsync());
        var afterBlockedMove = blockedDocument.RootElement;

        Assert.Equal(expectedBlockedTurn, afterBlockedMove.GetProperty("turn").GetInt32());
        Assert.Equal(approachPosition, ReadPosition(afterBlockedMove.GetProperty("playerPosition")));
        Assert.False(afterBlockedMove.GetProperty("lastAction").GetProperty("succeeded").GetBoolean());
        Assert.NotEqual(new Position(9999, 9999), ReadPosition(afterBlockedMove.GetProperty("playerPosition")));
        Assert.Equal(start.X + DirectionOffset(legalDirection).X, legalDestination.X);
        Assert.Equal(start.Y + DirectionOffset(legalDirection).Y, legalDestination.Y);
    }

    [Fact]
    public async Task GetDive_withUnknownId_returnsNotFound()
    {
        using var response = await _client.GetAsync($"/api/dungeon-dives/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonDocument> StartDiveAsync(int seed)
    {
        using var response = await _client.PostAsJsonAsync("/api/dungeon-dives", new { seed });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> SubmitMoveAsync(string id, string direction) =>
        _client.PostAsJsonAsync($"/api/dungeon-dives/{id}/actions", new
        {
            kind = "Move",
            direction,
            x = 9999,
            y = 9999,
            playerPosition = new { x = 9999, y = 9999 }
        });

    private async Task<JsonDocument> SubmitPathAsync(string id, JsonElement initialDive, IReadOnlyList<string> path)
    {
        JsonDocument? current = null;
        try
        {
            foreach (var direction in path)
            {
                using var response = await SubmitMoveAsync(id, direction);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var next = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                current?.Dispose();
                current = next;
            }

            return current ?? JsonDocument.Parse(initialDive.GetRawText());
        }
        catch
        {
            current?.Dispose();
            throw;
        }
    }

    private static bool TryFindDirection(
        JsonElement dive,
        bool walkable,
        out string direction,
        out Position destination)
    {
        var position = ReadPosition(dive.GetProperty("playerPosition"));
        var map = dive.GetProperty("map");
        var width = map.GetProperty("width").GetInt32();
        var height = map.GetProperty("height").GetInt32();
        var rows = map.GetProperty("tiles");
        (string Direction, int X, int Y)[] candidates =
        [
            ("North", 0, -1),
            ("East", 1, 0),
            ("South", 0, 1),
            ("West", -1, 0)
        ];

        foreach (var candidate in candidates)
        {
            var x = position.X + candidate.X;
            var y = position.Y + candidate.Y;
            var isWalkable = x >= 0 && x < width && y >= 0 && y < height &&
                IsWalkable(rows[y][x]);
            if (isWalkable != walkable)
                continue;

            direction = candidate.Direction;
            destination = new Position(x, y);
            return true;
        }

        direction = string.Empty;
        destination = default;
        return false;
    }

    private static bool IsWalkable(JsonElement tile) =>
        tile.GetProperty("terrain").GetString() == "Floor" &&
        tile.GetProperty("feature").GetString() is not ("ClosedDoor" or "LockedDoor" or "Pillar");

    private static (IReadOnlyList<string> Path, string Direction) FindBlockedApproach(JsonElement dive)
    {
        var map = dive.GetProperty("map");
        var width = map.GetProperty("width").GetInt32();
        var height = map.GetProperty("height").GetInt32();
        var rows = map.GetProperty("tiles");
        (string Name, int X, int Y)[] directions =
        [
            ("North", 0, -1),
            ("East", 1, 0),
            ("South", 0, 1),
            ("West", -1, 0)
        ];
        var start = ReadPosition(dive.GetProperty("playerPosition"));
        var queue = new Queue<(Position Position, IReadOnlyList<string> Path)>();
        var visited = new HashSet<Position> { start };
        queue.Enqueue((start, []));

        while (queue.TryDequeue(out var current))
        {
            foreach (var direction in directions)
            {
                var next = new Position(current.Position.X + direction.X, current.Position.Y + direction.Y);
                if (next.X < 0 || next.X >= width || next.Y < 0 || next.Y >= height ||
                    !IsWalkable(rows[next.Y][next.X]))
                    return (current.Path, direction.Name);

                if (visited.Add(next))
                    queue.Enqueue((next, [.. current.Path, direction.Name]));
            }
        }

        throw new InvalidOperationException("The generated walkable area must border an obstruction.");
    }

    private static Position ReadPosition(JsonElement position) =>
        new(position.GetProperty("x").GetInt32(), position.GetProperty("y").GetInt32());

    private static Position DirectionOffset(string direction) => direction switch
    {
        "North" => new Position(0, -1),
        "East" => new Position(1, 0),
        "South" => new Position(0, 1),
        "West" => new Position(-1, 0),
        _ => throw new ArgumentOutOfRangeException(nameof(direction))
    };

    private readonly record struct Position(int X, int Y);
}