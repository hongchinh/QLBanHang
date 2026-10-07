using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.CashDebt.Settings.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class CashSettingsTests : CashDebtTestBase
{
    public CashSettingsTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Defaults_are_warn_and_update_persists()
    {
        var sales = await CreateClientForRoleAsync("cs_sales", RoleCodes.Sales);
        try
        {
            var defaults = await ReadDataAsync<CashDebtSettingsDto>(await sales.GetAsync("/api/cash/settings"));
            defaults.CreditLimitPolicy.Should().Be(LimitPolicy.Warn);
            defaults.NegativeFundPolicy.Should().Be(LimitPolicy.Warn);

            var updated = await ReadDataAsync<CashDebtSettingsDto>(await _client.PutAsJsonAsync("/api/cash/settings",
                new UpdateCashDebtSettingsRequest { CreditLimitPolicy = LimitPolicy.Block, NegativeFundPolicy = LimitPolicy.Allow }));
            updated.CreditLimitPolicy.Should().Be(LimitPolicy.Block);

            var reread = await ReadDataAsync<CashDebtSettingsDto>(await _client.GetAsync("/api/cash/settings"));
            reread.CreditLimitPolicy.Should().Be(LimitPolicy.Block);
            reread.NegativeFundPolicy.Should().Be(LimitPolicy.Allow);
        }
        finally
        {
            await UpdateCashDebtSettingsAsync(s =>
            {
                s.CreditLimitPolicy = LimitPolicy.Warn;
                s.NegativeFundPolicy = LimitPolicy.Warn;
            });
        }
    }

    [Fact]
    public async Task Update_requires_cash_settings_permission()
    {
        var client = await CreateClientWithPermissionsAsync("cs_inv_only", null, Permissions.Inventory.Settings);
        var response = await client.PutAsJsonAsync("/api/cash/settings",
            new UpdateCashDebtSettingsRequest { CreditLimitPolicy = LimitPolicy.Block, NegativeFundPolicy = LimitPolicy.Block });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
