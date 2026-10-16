using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Platform;

[Collection(nameof(PostgresCollection))]
public class SwaggerDocumentTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;

    public SwaggerDocumentTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    [Fact]
    public async Task Swagger_json_lists_and_requires_the_bearer_scheme()
    {
        var res = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _).Should().BeTrue();
        root.GetProperty("security")[0].TryGetProperty("Bearer", out _).Should().BeTrue();
    }
}
