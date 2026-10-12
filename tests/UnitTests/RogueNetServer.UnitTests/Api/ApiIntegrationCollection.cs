using Microsoft.AspNetCore.Mvc.Testing;

namespace RogueNetServer.UnitTests.Api;

[CollectionDefinition(Name)]
public sealed class ApiIntegrationCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
    public const string Name = "API integration";
}
