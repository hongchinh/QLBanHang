using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

/// A branch with cash or debt data cannot be deleted: its cash book and debt ledger would become unreachable.
[Collection(nameof(PostgresCollection))]
public class BranchDeleteGuardTests : CashDebtTestBase
{
    public BranchDeleteGuardTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Branch_with_cash_or_debt_data_cannot_be_deleted()
    {
        var (withReceipt, receiptClient) = await BranchAsync();
        await CreateCashAsync(receiptClient, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TK"),
            await CashFundIdAsync(withReceipt), "2026-10-05 09:00", null, 100m));

        var (withOpeningDebt, debtClient) = await BranchAsync();
        var customer = await CreatePartnerAsync("KH-" + Guid.NewGuid().ToString("N")[..8], isCustomer: true, isSupplier: false);
        await CreateOpeningDebtAsync(debtClient, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 9, 10), 1_000m);

        var (withFundOpening, _) = await BranchAsync();
        await SetFundOpeningAmountAsync(await CashFundIdAsync(withFundOpening), 500m);

        var (unused, _) = await BranchAsync();

        foreach (var branchId in new[] { withReceipt, withOpeningDebt, withFundOpening })
            (await _client.DeleteAsync($"/api/branches/{branchId}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.DeleteAsync($"/api/branches/{unused}")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await InDbAsync(db => db.Branches.IgnoreQueryFilters()
                .Where(b => new[] { withReceipt, withOpeningDebt, withFundOpening, unused }.Contains(b.Id) && b.IsDeleted)
                .Select(b => b.Id).ToListAsync()))
            .Should().Equal(unused);
    }

    /// A fresh branch opened on 10-01 and an admin client working in it.
    private async Task<(Guid BranchId, HttpClient Client)> BranchAsync()
    {
        var branchId = await CreateBranchAsync("BD" + Guid.NewGuid().ToString("N")[..8]);
        await SetBranchOpeningDateAsync(branchId, new DateOnly(2026, 10, 1));
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }
}
