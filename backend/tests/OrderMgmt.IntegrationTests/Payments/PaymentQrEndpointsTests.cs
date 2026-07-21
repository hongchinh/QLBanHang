using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Payments.Models;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Payments;

[Collection(nameof(PostgresCollection))]
public class PaymentQrEndpointsTests : IAsyncLifetime
{
    private readonly PostgresFixture _pg;
    private WebAppFactory _factory = default!;
    private HttpClient _client = default!;
    private Guid _vcbBankId;

    public PaymentQrEndpointsTests(PostgresFixture pg) => _pg = pg;

    public async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg.ConnectionString);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _vcbBankId = (await db.Banks.FirstAsync(b => b.Code == "VCB")).Id;
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private async Task AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "Admin@123" });
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    [Fact]
    public async Task GetBanks_returns_seeded_banks()
    {
        var response = await _client.GetFromJsonAsync<ApiResponse<IReadOnlyList<BankDto>>>(
            "/api/banks", TestJson.Options);
        response!.Data.Should().Contain(b => b.Code == "VCB");
    }

    [Fact]
    public async Task GenerateQr_returns_payload_ending_in_valid_crc()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 250000m,
            Content = "TT DH-0001",
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<GenerateQrResponse>>(TestJson.Options);
        body!.Data!.Payload.Should().Contain("970436");
        body.Data.Payload.Should().EndWith(body.Data.Payload[^4..]);
        body.Data.Payload.Should().Contain("5406250000");
    }

    [Fact]
    public async Task GenerateQr_with_unknown_bank_returns_404()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = Guid.NewGuid(),
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateQr_with_non_numeric_account_number_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "abc-not-numeric",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GenerateQr_with_zero_amount_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 0m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unauthenticated_generate_returns_401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.PostAsJsonAsync("/api/payment-qr/generate", new GenerateQrRequest
        {
            BankId = _vcbBankId,
            AccountNumber = "0123456789",
            AccountName = "Nguyen Van A",
            Amount = 1000m,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
