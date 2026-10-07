using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class StockReasonDebtEffectTests : CashDebtTestBase
{
    public StockReasonDebtEffectTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Seeds_have_debt_effect()
    {
        var all = await ReadDataAsync<List<StockReasonDto>>(await _client.GetAsync("/api/stock-reasons"));
        DebtEffect Of(string code) => all.Single(r => r.IsSystem && r.Code == code).DebtEffect;

        Of("XBH").Should().Be(DebtEffect.ReceivableIncrease);
        Of("NMH").Should().Be(DebtEffect.PayableIncrease);
        Of("NKH").Should().Be(DebtEffect.None);
        Of("XKH").Should().Be(DebtEffect.None);
        Of("NTL").Should().Be(DebtEffect.ReceivableDecrease);
        Of("XTL").Should().Be(DebtEffect.PayableDecrease);

        var ntl = all.Single(r => r.Code == "NTL");
        ntl.Direction.Should().Be(StockDirection.In);
        ntl.PartnerType.Should().Be(PartnerType.Customer);
        var xtl = all.Single(r => r.Code == "XTL");
        xtl.Direction.Should().Be(StockDirection.Out);
        xtl.PartnerType.Should().Be(PartnerType.Supplier);
    }

    [Fact]
    public async Task Invalid_combinations_return_400()
    {
        var response = await _client.PostAsJsonAsync("/api/stock-reasons", new CreateStockReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Direction = StockDirection.Out,
            PartnerType = PartnerType.Supplier, DebtEffect = DebtEffect.PayableIncrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("debtEffect");

        response = await _client.PostAsJsonAsync("/api/stock-reasons", new CreateStockReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Direction = StockDirection.In,
            PartnerType = PartnerType.Supplier, DebtEffect = DebtEffect.ReceivableDecrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerType");

        response = await _client.PostAsJsonAsync("/api/stock-reasons", new CreateStockReasonRequest
        {
            Code = "B" + Guid.NewGuid().ToString("N")[..6], Name = "Sai", Direction = StockDirection.In,
            PartnerType = PartnerType.None, DebtEffect = DebtEffect.ReceivableDecrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("partnerType");
    }

    [Fact]
    public async Task Used_reason_keeps_debt_effect()
    {
        var code = "UD" + Guid.NewGuid().ToString("N")[..6];
        var created = await ReadDataAsync<StockReasonDto>(await _client.PostAsJsonAsync("/api/stock-reasons",
            new CreateStockReasonRequest { Code = code, Name = "Xuất dùng", Direction = StockDirection.Out, PartnerType = PartnerType.Any }));
        created.DebtEffect.Should().Be(DebtEffect.None);

        var productId = await CreateInventoryProductAsync("UD" + Guid.NewGuid().ToString("N")[..6]);
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-01-05 08:00", LineRequest(productId, 10, 5));
        await CreateVoucherAsync(_client, StockDirection.Out, code, "2026-01-06 08:00", LineRequest(productId, 1, 5));

        var response = await _client.PutAsJsonAsync($"/api/stock-reasons/{created.Id}", new UpdateStockReasonRequest
        {
            Name = created.Name, Direction = StockDirection.Out, PartnerType = PartnerType.Any, DebtEffect = DebtEffect.ReceivableIncrease,
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("debtEffect");

        // An unused system reason may still change its debt effect.
        var all = await ReadDataAsync<List<StockReasonDto>>(await _client.GetAsync("/api/stock-reasons"));
        var xtl = all.Single(r => r.IsSystem && r.Code == "XTL");
        try
        {
            var ok = await _client.PutAsJsonAsync($"/api/stock-reasons/{xtl.Id}", new UpdateStockReasonRequest
            {
                Name = xtl.Name, Direction = xtl.Direction, PartnerType = xtl.PartnerType, DebtEffect = DebtEffect.None,
            });
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await InDbAsync(async db =>
            {
                var reason = db.StockReasons.Single(r => r.Id == xtl.Id);
                reason.DebtEffect = DebtEffect.PayableDecrease;
                await db.SaveChangesAsync();
            });
        }
    }
}
