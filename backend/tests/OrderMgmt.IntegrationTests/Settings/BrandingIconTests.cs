using System.Net;
using FluentAssertions;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Settings;

[Collection(nameof(PostgresCollection))]
public class BrandingIconTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;

    public BrandingIconTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    [Theory]
    [InlineData(192)]
    [InlineData(512)]
    public async Task GetIcon_NoLogoUploaded_Returns200Png(int size)
    {
        var response = await _client.GetAsync($"/api/settings/branding/icon/{size}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    public async Task GetIcon_InvalidSize_Returns400(int size)
    {
        var response = await _client.GetAsync($"/api/settings/branding/icon/{size}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
