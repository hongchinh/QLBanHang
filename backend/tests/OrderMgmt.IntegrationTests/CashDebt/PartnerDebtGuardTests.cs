using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// A partner with cash or debt data cannot be deleted, and keeps the role of every side it has debt on.
[Collection(nameof(PostgresCollection))]
public class PartnerDebtGuardTests : CashDebtTestBase
{
    private static readonly DateOnly Oct1 = new(2026, 10, 1);

    public PartnerDebtGuardTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Partner_with_cash_or_debt_data_cannot_be_deleted()
    {
        var (client, fundId) = await BranchClientAsync();

        var withOpeningDebt = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        await CreateOpeningDebtAsync(client, withOpeningDebt, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);
        (await client.DeleteAsync($"/api/customers/{withOpeningDebt}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A refund from the supplier (THNCC, payable increase): its only data is that receipt.
        var withReceipt = await CreatePartnerAsync(NewCode("NCC"), isCustomer: false, isSupplier: true);
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("THNCC"), fundId,
            "2026-10-05 09:00", withReceipt, 500m));
        (await client.DeleteAsync($"/api/suppliers/{withReceipt}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // A voucher without debt effect still names the partner, and could not be edited once it is deleted.
        var onVoucherOnly = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        await CreateCashAsync(client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"), fundId,
            "2026-10-05 09:00", onVoucherOnly, 300m));
        (await client.DeleteAsync($"/api/customers/{onVoucherOnly}")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var unused = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        (await client.DeleteAsync($"/api/customers/{unused}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var deleted = await InDbAsync(db => db.Customers.IgnoreQueryFilters()
            .Where(c => new[] { withOpeningDebt, withReceipt, onVoucherOnly, unused }.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.IsDeleted));
        deleted.Should().Equal(new Dictionary<Guid, bool>
        {
            [withOpeningDebt] = false, [withReceipt] = false, [onVoucherOnly] = false, [unused] = true,
        });
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Partner_keeps_the_role_of_a_side_it_has_debt_on()
    {
        var (client, _) = await BranchClientAsync();
        var partner = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: true);
        await CreateOpeningDebtAsync(client, partner, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);

        UpdateCustomerRequest Roles(bool isCustomer, bool isSupplier) => new()
        {
            Name = "Đối tượng có nợ", Status = CustomerStatus.Active, IsCustomer = isCustomer, IsSupplier = isSupplier,
        };

        var dropCustomer = await client.PutAsJsonAsync($"/api/customers/{partner}", Roles(false, true), TestJson.Options);
        dropCustomer.StatusCode.Should().Be(HttpStatusCode.BadRequest, await dropCustomer.Content.ReadAsStringAsync());
        var error = (await dropCustomer.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!;
        error.Details.Should().ContainKey("isCustomer");

        // No payable debt: the supplier role can go.
        var dropSupplier = await client.PutAsJsonAsync($"/api/customers/{partner}", Roles(true, false), TestJson.Options);
        dropSupplier.StatusCode.Should().Be(HttpStatusCode.OK, await dropSupplier.Content.ReadAsStringAsync());

        (await InDbAsync(db => db.Customers.Where(c => c.Id == partner)
                .Select(c => new { c.IsCustomer, c.IsSupplier }).SingleAsync()))
            .Should().Be(new { IsCustomer = true, IsSupplier = false });
        await AssertCashDebtInvariantsAsync();
    }

    /// A fresh branch opened on 10-01 with an admin client working in it and its default cash fund.
    private async Task<(HttpClient Client, Guid FundId)> BranchClientAsync()
    {
        var branchId = await CreateBranchAsync(NewCode("PDG"));
        await SetBranchOpeningDateAsync(branchId, Oct1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (client, await FundIdAsync("QTM", branchId));
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
