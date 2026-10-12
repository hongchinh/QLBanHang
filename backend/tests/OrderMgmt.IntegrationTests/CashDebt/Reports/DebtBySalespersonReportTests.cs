using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Debt.Models;
using OrderMgmt.Application.CashDebt.OpeningDebts.Models;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// Công nợ theo nhân viên (Phase 03).
[Collection(nameof(PostgresCollection))]
public class DebtBySalespersonReportTests : CashDebtTestBase
{
    public DebtBySalespersonReportTests(PostgresFixture pg) : base(pg) { }

    private sealed record Seeded(ReportIds Ids, Guid AdminId, string AdminName, Guid Sp2Id, string Sp2Name,
        Guid Sp2SaleId, HttpClient Admin);

    /// The dataset plus a second sale of 2,000 to KH1 made by sp2 at 10-20 09:00, paid 0.
    private async Task<Seeded> SeedWithSecondSalespersonAsync()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var suffix = ids.Kh1Code[3..];
        var sp2Username = "sp2_" + suffix;
        var sp2 = await CreateClientWithPermissionsAsync(sp2Username, ids.MainBranchId,
            Permissions.StockOut.View, Permissions.StockOut.Create);
        UseBranch(sp2, ids.MainBranchId);
        var sale = await ReportDataset.PostStockAsync(this, sp2, StockDirection.Out, "XBH", "2026-10-20 09:00",
            ids.MainWarehouseId, ids.Kh1Id, 2_000m, paymentMethodId: null, paid: 0m);

        var users = await InDbAsync(db => db.Users.AsNoTracking()
            .Where(u => u.Username == "admin" || u.Username == sp2Username)
            .Select(u => new { u.Id, u.Username, u.FullName })
            .ToListAsync());
        var adminUser = users.Single(u => u.Username == "admin");
        var sp2User = users.Single(u => u.Username == sp2Username);
        var admin = CloneAdminClient();
        UseBranch(admin, ids.MainBranchId);
        return new Seeded(ids, adminUser.Id, adminUser.FullName, sp2User.Id, sp2User.FullName, sale.Id, admin);
    }

    private static DebtBySalespersonRowDto Row(Guid? owner, string? name, decimal opening, decimal increase,
        decimal settled, decimal closing, decimal overdue) => new()
    {
        OwnerUserId = owner,
        OwnerName = name,
        OpeningOpen = opening,
        Increase = increase,
        Settled = settled,
        ClosingOpen = closing,
        ClosingOverdue = overdue,
    };

    [Fact]
    public async Task Receivable_by_owner_for_october()
    {
        var s = await SeedWithSecondSalespersonAsync();

        var report = await GetAsync(s.Admin, "side=Receivable&from=2026-10-01&to=2026-10-31");

        report.Side.Should().Be(DebtSide.Receivable);
        report.From.Should().Be(new DateOnly(2026, 10, 1));
        report.To.Should().Be(new DateOnly(2026, 10, 31));
        report.Rows.Should().BeEquivalentTo(new[]
        {
            Row(s.AdminId, s.AdminName, 0m, 3_000m, 1_000m, 2_000m, 2_000m),
            Row(s.Sp2Id, s.Sp2Name, 0m, 2_000m, 0m, 2_000m, 2_000m),
            Row(null, null, 5_000m, 0m, 4_000m, 1_000m, 1_000m),
        }, o => o.WithStrictOrdering());
        report.Totals.Should().BeEquivalentTo(Row(null, null, 5_000m, 5_000m, 5_000m, 5_000m, 5_000m));
        report.Unallocated.Should().BeEquivalentTo(new DebtUnallocatedLineDto { Opening = 1_000m, Closing = 1_500m });
        report.OpeningBalance.Should().Be(4_000m);
        report.ClosingBalance.Should().Be(3_500m);
    }

    [Fact]
    public async Task Receivable_by_owner_from_mid_month()
    {
        var s = await SeedWithSecondSalespersonAsync();

        var report = await GetAsync(s.Admin, "side=Receivable&from=2026-10-06&to=2026-10-31");

        report.Rows.Should().BeEquivalentTo(new[]
        {
            Row(s.AdminId, s.AdminName, 2_000m, 0m, 0m, 2_000m, 2_000m),
            Row(s.Sp2Id, s.Sp2Name, 0m, 2_000m, 0m, 2_000m, 2_000m),
            Row(null, null, 1_000m, 0m, 0m, 1_000m, 1_000m),
        }, o => o.WithStrictOrdering());
        report.Unallocated.Should().BeEquivalentTo(new DebtUnallocatedLineDto { Opening = 1_000m, Closing = 1_500m });
        report.OpeningBalance.Should().Be(2_000m);
        report.ClosingBalance.Should().Be(3_500m);
    }

    [Fact]
    public async Task Payable_by_owner()
    {
        var s = await SeedWithSecondSalespersonAsync();

        var report = await GetAsync(s.Admin, "side=Payable&from=2026-10-01&to=2026-10-31");

        report.Rows.Should().BeEquivalentTo(new[]
        {
            Row(s.AdminId, s.AdminName, 0m, 6_000m, 2_000m, 4_000m, 4_000m),
        }, o => o.WithStrictOrdering());
        report.Unallocated.Should().BeEquivalentTo(new DebtUnallocatedLineDto { Opening = 0m, Closing = 3_000m });
        report.OpeningBalance.Should().Be(0m);
        report.ClosingBalance.Should().Be(1_000m);
    }

    [Fact]
    public async Task Opening_match_straddling_the_period()
    {
        var s = await SeedWithSecondSalespersonAsync();
        var saleEntry = (await EntriesOfAsync(DebtSourceType.StockOut, s.Sp2SaleId)).Single();
        var response = await s.Admin.PostAsJsonAsync("/api/debt/settlements", new CreateSettlementBatchRequest
        {
            PartnerId = s.Ids.Kh1Id,
            Side = DebtSide.Receivable,
            Items = new List<SettlementItemRequest>
            {
                new() { IncreaseEntryId = saleEntry.Id, DecreaseEntryId = s.Ids.OpeningTtOldEntryId, Amount = 1_000m },
            },
        }, TestJson.Options);
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());

        var report = await GetAsync(s.Admin, "side=Receivable&from=2026-10-01&to=2026-10-31");

        report.Rows.Should().BeEquivalentTo(new[]
        {
            Row(s.AdminId, s.AdminName, 0m, 3_000m, 1_000m, 2_000m, 2_000m),
            Row(s.Sp2Id, s.Sp2Name, 0m, 2_000m, 1_000m, 1_000m, 1_000m),
            Row(null, null, 5_000m, 0m, 4_000m, 1_000m, 1_000m),
        }, o => o.WithStrictOrdering());
        report.Unallocated.Should().BeEquivalentTo(new DebtUnallocatedLineDto { Opening = 1_000m, Closing = 500m });
        report.Rows.Sum(r => r.ClosingOpen).Should().Be(4_000m);
        report.OpeningBalance.Should().Be(4_000m);
        report.ClosingBalance.Should().Be(3_500m);
    }

    [Fact]
    public async Task Opening_debt_with_an_owner_is_grouped_under_that_owner()
    {
        var s = await SeedWithSecondSalespersonAsync();
        var baseline = await GetAsync(s.Admin, "side=Receivable&from=2026-10-01&to=2026-10-31");
        var create = await s.Admin.PostAsJsonAsync("/api/opening-debts", new UpsertOpeningDebtRequest
        {
            PartnerId = s.Ids.Kh1Id,
            Side = DebtSide.Receivable,
            Kind = OpeningDebtKind.Debt,
            DocNo = "HD-OWN",
            DocDate = new DateOnly(2026, 9, 10),
            Amount = 700m,
            OwnerUserId = s.Sp2Id,
        }, TestJson.Options);
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());

        var report = await GetAsync(s.Admin, "side=Receivable&from=2026-10-01&to=2026-10-31");

        report.Rows.Single(r => r.OwnerUserId == s.Sp2Id).OpeningOpen.Should().Be(700m);
        report.Rows.Single(r => r.OwnerUserId == null)
            .Should().BeEquivalentTo(baseline.Rows.Single(r => r.OwnerUserId == null));
        report.OpeningBalance.Should().Be(baseline.OpeningBalance + 700m);
    }

    [Fact]
    public async Task Partner_filter()
    {
        var s = await SeedWithSecondSalespersonAsync();
        var all = await GetAsync(s.Admin, "side=Receivable&from=2026-10-01&to=2026-10-31");

        var kh1 = await GetAsync(s.Admin, $"side=Receivable&from=2026-10-01&to=2026-10-31&partnerId={s.Ids.Kh1Id}");
        kh1.Should().BeEquivalentTo(all, o => o.WithStrictOrdering());

        var ncc1 = await GetAsync(s.Admin, $"side=Receivable&from=2026-10-01&to=2026-10-31&partnerId={s.Ids.Ncc1Id}");
        ncc1.Rows.Should().BeEmpty();
        ncc1.Totals.Should().BeEquivalentTo(Row(null, null, 0m, 0m, 0m, 0m, 0m));
        ncc1.OpeningBalance.Should().Be(0m);
        ncc1.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Empty_result_is_zero_filled()
    {
        var s = await SeedWithSecondSalespersonAsync();

        var report = await GetAsync(s.Admin, "side=Payable&from=2026-10-01&to=2026-10-04");

        report.Rows.Should().BeEmpty();
        report.Totals.Should().BeEquivalentTo(Row(null, null, 0m, 0m, 0m, 0m, 0m));
        report.Unallocated.Should().BeEquivalentTo(new DebtUnallocatedLineDto());
        report.OpeningBalance.Should().Be(0m);
        report.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task Rows_reconcile_with_the_balance_report()
    {
        var s = await SeedWithSecondSalespersonAsync();

        foreach (var side in new[] { DebtSide.Receivable, DebtSide.Payable })
        {
            foreach (var from in new[] { "2026-10-01", "2026-10-06" })
            {
                var context = $"{side} from {from}";
                var report = await GetAsync(s.Admin, $"side={side}&from={from}&to=2026-10-31");
                foreach (var row in report.Rows.Append(report.Totals))
                    (row.OpeningOpen + row.Increase - row.Settled).Should().Be(row.ClosingOpen, context);

                var balance = await ReadDataAsync<DebtBalanceReportDto>(await s.Admin.GetAsync(
                    $"/api/reports/debt-balance?side={side}&from={from}&to=2026-10-31"));
                decimal Signed(decimal debit, decimal credit) => side == DebtSide.Receivable ? debit - credit : credit - debit;
                report.OpeningBalance.Should().Be(Signed(balance.Totals.OpeningDebit, balance.Totals.OpeningCredit), context);
                report.ClosingBalance.Should().Be(Signed(balance.Totals.ClosingDebit, balance.Totals.ClosingCredit), context);
            }
        }
    }

    [Fact]
    public async Task Validation_and_permission()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);

        var inverted = await client.GetAsync("/api/reports/debt-by-salesperson?from=2026-10-31&to=2026-10-01");
        inverted.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inverted.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Details.Should().ContainKey("to");

        var cashOnly = await CreateClientWithPermissionsAsync("sp_cash_" + ids.Kh1Code[3..], ids.MainBranchId, Permissions.Reports.Cash);
        (await cashOnly.GetAsync("/api/reports/debt-by-salesperson?from=2026-10-01&to=2026-10-31"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<DebtBySalespersonReportDto> GetAsync(HttpClient client, string query) =>
        await ReadDataAsync<DebtBySalespersonReportDto>(await client.GetAsync($"/api/reports/debt-by-salesperson?{query}"));
}
