using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.Reports.Models;
using OrderMgmt.Application.Common;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Reports;

/// scope = Branch | Company on the three debt reports (Phase 03, T7, T21).
[Collection(nameof(PostgresCollection))]
public class DebtReportScopeTests : CashDebtTestBase
{
    private const string October = "from=2026-10-01&to=2026-10-31";

    public DebtReportScopeTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Balance_company_scope_adds_every_branch()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        var company = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", $"side=Receivable&{October}&scope=Company");
        company.Scope.Should().Be(ReportScope.Company);
        var row = company.Rows.Should().ContainSingle().Which;
        row.PartnerId.Should().Be(ids.Kh1Id);
        row.OpeningDebit.Should().Be(4_000m);
        row.Increase.Should().Be(12_999m);
        row.Decrease.Should().Be(5_500m);
        row.ClosingDebit.Should().Be(11_499m);

        var branch = await GetAsync<DebtBalanceReportDto>(client, "debt-balance", $"side=Receivable&{October}");
        branch.Scope.Should().Be(ReportScope.Branch);
        var branchRow = branch.Rows.Should().ContainSingle().Which;
        branchRow.Increase.Should().Be(3_000m);
        branchRow.ClosingDebit.Should().Be(1_500m);
    }

    [Fact]
    public async Task Ledger_company_scope_merges_rows()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"side=Receivable&partnerId={ids.Kh1Id}&{October}";

        var report = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", query + "&scope=Company");

        report.Scope.Should().Be(ReportScope.Company);
        report.Rows.Should().HaveCount(5);
        var sales = report.Rows.Where(r => r.DocCode == "PX00001").ToList();
        sales.Should().HaveCount(2);
        sales.Should().OnlyContain(r => r.PostedAt == Vn("2026-10-03 09:00"));
        report.TotalIncrease.Should().Be(12_999m);
        report.Rows[^1].RunningBalance.Should().Be(11_499m);
        report.ClosingBalance.Should().Be(11_499m);

        var running = report.OpeningBalance;
        foreach (var row in report.Rows)
        {
            running += row.Increase - row.Decrease;
            row.RunningBalance.Should().Be(running);
        }

        var codes = await InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == ids.MainBranchId || b.Id == ids.BranchBId)
            .ToDictionaryAsync(b => b.Id, b => b.Code));
        report.Rows.Should().OnlyContain(r => r.BranchCode != null);
        sales.Select(r => r.BranchCode).Should().BeEquivalentTo(new[] { codes[ids.MainBranchId], codes[ids.BranchBId] });
        codes[ids.MainBranchId].Should().NotBe(codes[ids.BranchBId]);

        var branch = await GetAsync<DebtLedgerReportDto>(client, "debt-ledger", query);
        branch.Scope.Should().Be(ReportScope.Branch);
        branch.Rows.Should().OnlyContain(r => r.BranchCode == null);
    }

    [Fact]
    public async Task Reconciliation_company_scope()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);
        var query = $"partnerId={ids.Kh1Id}&{October}";

        var company = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", query + "&scope=Company");
        company.Scope.Should().Be(ReportScope.Company);
        company.BranchName.Should().BeNull();
        company.Sides.Should().ContainSingle().Which.ClosingBalance.Should().Be(11_499m);

        var mainName = await InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Id == ids.MainBranchId).Select(b => b.Name).SingleAsync());
        foreach (var branchQuery in new[] { query + "&scope=Branch", query })
        {
            var branch = await GetAsync<DebtReconciliationReportDto>(client, "debt-reconciliation", branchQuery);
            branch.Scope.Should().Be(ReportScope.Branch);
            branch.BranchName.Should().Be(mainName);
            branch.Sides.Should().ContainSingle().Which.ClosingBalance.Should().Be(1_500m);
        }
    }

    [Fact]
    public async Task Company_scope_needs_access_all()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var suffix = ids.Kh1Code[3..];
        var debtOnly = await CreateClientWithPermissionsAsync("scope_" + suffix, ids.MainBranchId, Permissions.Reports.Debt);

        var queries = new Dictionary<string, string>
        {
            ["debt-balance"] = $"side=Receivable&{October}",
            ["debt-ledger"] = $"side=Receivable&partnerId={ids.Kh1Id}&{October}",
            ["debt-reconciliation"] = $"partnerId={ids.Kh1Id}&{October}",
        };
        foreach (var (route, query) in queries)
        {
            (await debtOnly.GetAsync($"/api/reports/{route}?{query}&scope=Company"))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden, route);
            (await debtOnly.GetAsync($"/api/reports/{route}?{query}&scope=Branch"))
                .StatusCode.Should().Be(HttpStatusCode.OK, route);
        }
    }

    [Fact]
    public async Task Invalid_scope_is_400()
    {
        var ids = await ReportDataset.SeedAsync(this);
        var client = MainBranchClient(ids);

        (await client.GetAsync($"/api/reports/debt-balance?side=Receivable&{October}&scope=7"))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private HttpClient MainBranchClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string route, string query) =>
        await ReadDataAsync<T>(await client.GetAsync($"/api/reports/{route}?{query}"));
}
