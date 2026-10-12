using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RogueNetServer.UnitTests.Api;

[Collection(ApiIntegrationCollection.Name)]
public sealed class MapEndpointTests
{
    private readonly HttpClient _client;

    public MapEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMap_withSeed_returnsDeterministicClientSafeMap()
    {
        const int seed = 37007434;
        var firstResponse = await _client.GetAsync($"/api/maps?seed={seed}");
        var secondResponse = await _client.GetAsync($"/api/maps?seed={seed}");
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        var secondJson = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstJson, secondJson);

        using var document = JsonDocument.Parse(firstJson);
        var map = document.RootElement;
        Assert.Equal(seed, map.GetProperty("seed").GetInt32());
        Assert.Equal(80, map.GetProperty("width").GetInt32());
        Assert.Equal(25, map.GetProperty("height").GetInt32());

        var rooms = map.GetProperty("rooms");
        var connections = map.GetProperty("connections");
        var rows = map.GetProperty("tiles");
        Assert.NotEmpty(rooms.EnumerateArray());
        Assert.True(connections.GetArrayLength() >= rooms.GetArrayLength() - 1);
        Assert.Equal(25, rows.GetArrayLength());
        Assert.All(rows.EnumerateArray(), row => Assert.Equal(80, row.GetArrayLength()));

        var tile = rows[0][0];
        Assert.True(tile.TryGetProperty("terrain", out var terrain));
        Assert.True(tile.TryGetProperty("feature", out var feature));
        Assert.True(tile.TryGetProperty("roomId", out _));
        Assert.Equal(JsonValueKind.String, terrain.ValueKind);
        Assert.Equal(JsonValueKind.String, feature.ValueKind);
    }

    [Fact]
    public async Task GetMap_withoutSeed_returnsTheSeedUsedForGeneration()
    {
        using var response = await _client.GetAsync("/api/maps");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(document.RootElement.TryGetProperty("seed", out var seed));
        Assert.Equal(JsonValueKind.Number, seed.ValueKind);
        Assert.True(document.RootElement.GetProperty("width").GetInt32() > 0);
        Assert.True(document.RootElement.GetProperty("height").GetInt32() > 0);
        Assert.NotEmpty(document.RootElement.GetProperty("tiles").EnumerateArray());
    }

    [Fact]
    public async Task GetMap_withMalformedSeed_returnsBadRequest()
    {
        using var response = await _client.GetAsync("/api/maps?seed=not-a-number");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
