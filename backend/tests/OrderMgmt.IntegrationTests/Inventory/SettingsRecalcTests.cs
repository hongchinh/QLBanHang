using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Settings.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class SettingsRecalcTests : InventoryTestBase
{
    public SettingsRecalcTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Purchase_vat_toggle_reprices_outbound_after_the_lock_only()
    {
        var p = await CreateInventoryProductAsync("SR01");
        var september = await CreateInventoryProductAsync("SR02");
        var septemberIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-09-10 08:00",
            VatLine(september, 10, 100_000, 10));
        var octoberIn = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-05 08:00", VatLine(p, 10, 100_000, 10));
        var octoberOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-06 08:00", LineRequest(p, 5, 0));
        (await LedgerOfAsync(octoberOut.Id)).Single().CostAmount.Should().Be(550_000m);
        await SetPeriodLockAsync(new DateOnly(2026, 9, 30));

        var (status, settings, error) = await PutSettingsAsync(s => s.PurchaseCostIncludesVat = false);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        settings!.PurchaseCostIncludesVat.Should().BeFalse();
        (await LedgerOfAsync(octoberOut.Id)).Single().CostAmount.Should().Be(500_000m);
        (await LedgerOfAsync(octoberIn.Id)).Single().InValue.Should().Be(1_000_000m);
        (await LedgerOfAsync(septemberIn.Id)).Single().InValue.Should().Be(1_100_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Purchase_vat_toggle_requires_lock_on_period_end()
    {
        var p = await CreateInventoryProductAsync("SR03");
        var inbound = await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", VatLine(p, 10, 100_000, 10));
        await SetPeriodLockAsync(new DateOnly(2026, 10, 10));

        var (status, _, error) = await PutSettingsAsync(s => s.PurchaseCostIncludesVat = false);

        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("purchaseCostIncludesVat");
        error.Message.Should().Be("Ngày khóa sổ của chi nhánh CN01 (10/10/2026) phải là ngày cuối kỳ khi đổi cách tính VAT vào giá nhập.");
        (await GetSettingsAsync()).PurchaseCostIncludesVat.Should().BeTrue();
        (await LedgerOfAsync(inbound.Id)).Single().InValue.Should().Be(1_100_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Switching_scope_to_warehouse_recomputes_costs()
    {
        var p = await CreateInventoryProductAsync("SR04");
        var second = await CreateWarehouseAsync(MainBranchId, "KHO02");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 09:00", r => r.WarehouseId = second,
            LineRequest(p, 10, 200_000));
        var out1 = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 5, 0));
        var out2 = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 09:00", r => r.WarehouseId = second,
            LineRequest(p, 5, 0));
        (await LedgerOfAsync(out1.Id)).Single().CostAmount.Should().Be(750_000m);
        (await LedgerOfAsync(out2.Id)).Single().CostAmount.Should().Be(750_000m);

        var (status, _, error) = await PutSettingsAsync(s => s.CostingScope = CostingScope.Warehouse);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await LedgerOfAsync(out1.Id)).Single().CostAmount.Should().Be(500_000m);
        (await LedgerOfAsync(out2.Id)).Single().CostAmount.Should().Be(1_000_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Switching_period_to_quarter_uses_quarter_average()
    {
        var p = await CreateInventoryProductAsync("SR05");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 10, 100_000));
        var octOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-10-05 08:00", LineRequest(p, 5, 0));
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-11-03 08:00", LineRequest(p, 10, 200_000));
        var novOut = await CreateVoucherAsync(_client, StockDirection.Out, "XKH", "2026-11-10 08:00", LineRequest(p, 5, 0));
        (await LedgerOfAsync(octOut.Id)).Single().CostAmount.Should().Be(500_000m);
        (await LedgerOfAsync(novOut.Id)).Single().CostAmount.Should().Be(833_333m);

        var (status, _, error) = await PutSettingsAsync(s => s.CostingPeriod = CostingPeriod.Quarter);

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        (await LedgerOfAsync(octOut.Id)).Single().CostAmount.Should().Be(750_000m);
        (await LedgerOfAsync(novOut.Id)).Single().CostAmount.Should().Be(750_000m);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Changing_period_requires_lock_on_new_period_end()
    {
        var p = await CreateInventoryProductAsync("SR06");
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-08-10 08:00", LineRequest(p, 10, 100_000));
        await SetPeriodLockAsync(new DateOnly(2026, 8, 31));

        var (status, _, error) = await PutSettingsAsync(s => s.CostingPeriod = CostingPeriod.Quarter);
        status.Should().Be(HttpStatusCode.BadRequest);
        error!.Details.Should().ContainKey("costingPeriod");
        error.Message.Should().Be("Ngày khóa sổ của chi nhánh CN01 (31/08/2026) phải là ngày cuối kỳ khi đổi kỳ tính giá.");
        (await GetSettingsAsync()).CostingPeriod.Should().Be(CostingPeriod.Month);

        await SetPeriodLockAsync(new DateOnly(2026, 9, 30));
        var (okStatus, settings, okError) = await PutSettingsAsync(s => s.CostingPeriod = CostingPeriod.Quarter);
        okStatus.Should().Be(HttpStatusCode.OK, okError?.Message);
        settings!.CostingPeriod.Should().Be(CostingPeriod.Quarter);
        await AssertInvariantsAsync();
    }

    [Fact]
    public async Task Settings_change_without_ledger_data_only_saves()
    {
        // A mid-period lock would reject the change if there were ledger rows.
        await SetPeriodLockAsync(new DateOnly(2026, 10, 10));

        var (status, settings, error) = await PutSettingsAsync(s =>
        {
            s.CostingPeriod = CostingPeriod.Quarter;
            s.CostingScope = CostingScope.Warehouse;
            s.PurchaseCostIncludesVat = false;
        });

        status.Should().Be(HttpStatusCode.OK, error?.Message);
        settings!.CostingScope.Should().Be(CostingScope.Warehouse);
        (await GetSettingsAsync()).CostingPeriod.Should().Be(CostingPeriod.Quarter);
        (await InDbAsync(db => db.InventoryCostPeriods.CountAsync())).Should().Be(0);
        await AssertInvariantsAsync();
    }

    private static UpsertStockVoucherLineRequest VatLine(Guid productId, decimal quantity, decimal unitPrice, decimal vatRate)
    {
        var line = LineRequest(productId, quantity, unitPrice);
        line.VatRate = vatRate;
        return line;
    }

    private async Task<InventorySettingsDto> GetSettingsAsync() =>
        await ReadDataAsync<InventorySettingsDto>(await _client.GetAsync("/api/inventory/settings"));

    /// PUT /api/inventory/settings with the current settings changed by `mutate`.
    private async Task<(HttpStatusCode Status, InventorySettingsDto? Settings, ApiError? Error)> PutSettingsAsync(
        Action<UpdateInventorySettingsRequest> mutate)
    {
        var current = await GetSettingsAsync();
        var request = new UpdateInventorySettingsRequest
        {
            CostingMethod = current.CostingMethod,
            CostingPeriod = current.CostingPeriod,
            CostingScope = current.CostingScope,
            PurchaseCostIncludesVat = current.PurchaseCostIncludesVat,
            NegativeStockPolicy = current.NegativeStockPolicy,
            NetExcludesVat = current.NetExcludesVat,
            DefaultDateMode = current.DefaultDateMode,
        };
        mutate(request);

        var response = await _client.PutAsJsonAsync("/api/inventory/settings", request);
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<InventorySettingsDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
