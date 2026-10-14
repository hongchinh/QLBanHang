using System.Diagnostics;
using System.Globalization;
using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class DemoBranchDeleteTests : DemoBranchFixture
{
    private readonly ITestOutputHelper _output;

    public DemoBranchDeleteTests(PostgresFixture pg, ITestOutputHelper output) : base(pg) => _output = output;

    private static string DeleteUrl(Guid branchId) => $"/api/branches/{branchId}/demo";

    /// Tables without a branch_id column, counted per branch through their parent's (`p.branch_id`).
    private static readonly (string Table, string From)[] ChildTables =
    {
        ("debt_settlements", "debt_settlements c left join debt_entries p on p.id = c.increase_entry_id"),
        ("cash_voucher_lines", "cash_voucher_lines c left join cash_vouchers p on p.id = c.cash_voucher_id"),
        ("stock_voucher_lines", "stock_voucher_lines c left join stock_vouchers p on p.id = c.stock_voucher_id"),
        ("stock_voucher_activities", "stock_voucher_activities c left join stock_vouchers p on p.id = c.stock_voucher_id"),
        ("cash_document_activities", "cash_document_activities c left join (select id, branch_id from cash_vouchers"
            + " union all select id, branch_id from fund_transfers union all select id, branch_id from debt_offset_vouchers)"
            + " p on p.id = c.document_id"),
    };

    [Fact]
    public async Task Delete_removes_every_row_of_the_demo_branch_and_nothing_else()
    {
        await ReportDataset.SeedAsync(this);
        // Read at run time, so a table added later is covered too (R4).
        var branchTables = await QueryAsync("select table_name from information_schema.columns"
            + " where table_schema = 'public' and column_name = 'branch_id' order by table_name");
        var tables = branchTables.Select(r => r[0]).ToList();
        var before = await SnapshotAsync(tables, exceptBranchId: null);

        var branch = await CreateDemoBranchAsync("DD01");
        var fill = await FillAsync(branch);
        await CreateTestUserAsync("dd_user", "Pass@123", RoleCodes.Sales, defaultBranchId: branch.BranchId);
        var children = ChildCountQueries(fill);
        // The fill writes every table, so the checks after the delete are not vacuous.
        foreach (var table in tables)
            (await BranchRowCountAsync(table, branch.BranchId)).Should().BePositive($"the fill writes {table}");
        foreach (var (table, sql) in children)
            (await CountAsync(sql)).Should().BePositive($"the fill writes {table}");
        var preview = await ReadDataAsync<DemoBranchDeletionPreviewDto>(
            await _client.GetAsync($"/api/branches/{branch.BranchId}/demo-deletion"));
        preview.DocumentCount.Should().Be(fill.DocumentCount);

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.DeleteAsync(DeleteUrl(branch.BranchId));
        stopwatch.Stop();
        _output.WriteLine($"DELETE /api/branches/{{id}}/demo took {stopwatch.ElapsedMilliseconds} ms");

        var (status, result, error) = await ReadDocumentResponseAsync<DemoBranchDeletionResultDto>(response);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        result!.ReassignedUserCount.Should().Be(1);
        foreach (var table in tables)
            (await BranchRowCountAsync(table, branch.BranchId)).Should().Be(0, $"{table} keeps no row of the demo branch");
        foreach (var (table, sql) in children)
            (await CountAsync(sql)).Should().Be(0, $"{table} keeps no row of the demo branch's documents");
        (await CountAsync($"select count(*) from branches where id = '{branch.BranchId}'")).Should().Be(0);
        (await SnapshotAsync(tables, branch.BranchId)).Should().BeEquivalentTo(before);
        (await InDbAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.Username == "dd_user")
            .Select(u => u.DefaultBranchId).SingleAsync())).Should().Be(BranchDefaults.MainBranchId);
        await AssertInvariantsAsync();
        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Delete_refuses_normal_running_and_unknown_branches()
    {
        var normal = await CreateBranchAsync("DD03");
        var normalWarehouse = await CreateWarehouseAsync(normal, "WDD03");
        await AssertErrorAsync(await _client.DeleteAsync(DeleteUrl(normal)), HttpStatusCode.Conflict, "NOT_A_DEMO_BRANCH");
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Id == normalWarehouse))).Should().BeTrue();

        var running = await CreateDemoBranchAsync("DD04", SampleDataStatus.Running);
        await AssertErrorAsync(await _client.DeleteAsync(DeleteUrl(running.BranchId)), HttpStatusCode.Conflict,
            "SAMPLE_DATA_RUNNING");
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Id == running.WarehouseId))).Should().BeTrue();

        (await _client.DeleteAsync(DeleteUrl(Guid.NewGuid()))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var softDeleted = await CreateDemoBranchAsync("DD05");
        await InDbAsync(db => db.Branches.Where(b => b.Id == softDeleted.BranchId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsDeleted, true)));
        (await _client.DeleteAsync(DeleteUrl(softDeleted.BranchId))).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var demo = await CreateDemoBranchAsync("DD06");
        var noPermission = await CreateClientWithPermissionsAsync("dd_noperm", null, Permissions.Reports.Debt);
        (await noPermission.DeleteAsync(DeleteUrl(demo.BranchId))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await InDbAsync(db => db.Branches.AnyAsync(b => b.Id == demo.BranchId))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_refuses_when_another_branch_uses_its_warehouse()
    {
        var branch = await CreateDemoBranchAsync("DD02");
        var product = await CreateInventoryProductAsync("DDSP02");
        // A test-only corruption standing in for a warehouse moved between branches.
        await InDbAsync(async db =>
        {
            db.OpeningStocks.Add(new OpeningStock
            {
                BranchId = MainBranchId,
                WarehouseId = branch.WarehouseId,
                ProductId = product,
                OpeningDate = DemoOpeningDate,
                Quantity = 1,
                Amount = 100m,
            });
            await db.SaveChangesAsync();
        });

        var response = await _client.DeleteAsync(DeleteUrl(branch.BranchId));

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "DEMO_WAREHOUSE_IN_USE");
        (await InDbAsync(db => db.Warehouses.Where(w => w.BranchId == branch.BranchId).Select(w => w.Id).ToListAsync()))
            .Should().Equal(branch.WarehouseId);
    }

    /// Count queries of the fill's child rows by the ids of their parents: once the parents are gone, a child left
    /// behind no longer joins to any branch.
    private static List<(string Table, string Sql)> ChildCountQueries(DemoFill fill) => new()
    {
        ("debt_settlements", $"select count(*) from debt_settlements where increase_entry_id in ({In(fill.EntryIds)})"
            + $" or decrease_entry_id in ({In(fill.EntryIds)})"),
        ("cash_voucher_lines", $"select count(*) from cash_voucher_lines where cash_voucher_id in ({In(fill.CashVoucherIds)})"),
        ("stock_voucher_lines", $"select count(*) from stock_voucher_lines where stock_voucher_id in ({In(fill.StockVoucherIds)})"),
        ("stock_voucher_activities",
            $"select count(*) from stock_voucher_activities where stock_voucher_id in ({In(fill.StockVoucherIds)})"),
        ("cash_document_activities",
            $"select count(*) from cash_document_activities where document_id in ({In(fill.CashDocumentIds)})"),
    };

    private static string In(IEnumerable<Guid> ids) => string.Join(", ", ids.Select(id => $"'{id}'"));

    private Task<long> BranchRowCountAsync(string table, Guid branchId) =>
        CountAsync($"select count(*) from \"{table}\" where branch_id = '{branchId}'");

    /// Per table and branch (except `exceptBranchId`): the row count and the sums of its `amount` and `quantity`
    /// columns, for every branch_id table and for the child tables through their parent's branch.
    private async Task<Dictionary<string, string>> SnapshotAsync(IEnumerable<string> branchTables, Guid? exceptBranchId)
    {
        var sources = branchTables.Select(t => (Table: t, From: $"\"{t}\" c", Branch: "c.branch_id"))
            .Concat(ChildTables.Select(c => (c.Table, c.From, Branch: "p.branch_id")));
        var snapshot = new Dictionary<string, string>();
        foreach (var (table, from, branch) in sources)
        {
            var sumColumns = (await QueryAsync("select column_name from information_schema.columns where table_schema"
                + $" = 'public' and table_name = '{table}' and column_name in ('amount', 'quantity') order by column_name"))
                .Select(r => r[0]).ToList();
            var select = string.Join(", ", new[] { $"coalesce({branch}::text, 'none')", "count(*)::text" }
                .Concat(sumColumns.Select(col => $"coalesce(sum(c.{col}), 0)::text")));
            var where = exceptBranchId is { } id ? $" where {branch} is distinct from '{id}'" : "";
            foreach (var row in await QueryAsync($"select {select} from {from}{where} group by {branch}"))
                snapshot[$"{table} / {row[0]}"] = string.Join("; ",
                    new[] { "count=" + row[1] }.Concat(sumColumns.Select((col, i) => $"{col}={row[i + 2]}")));
        }
        return snapshot;
    }

    private async Task<long> CountAsync(string sql) =>
        long.Parse((await QueryAsync(sql)).Single()[0], CultureInfo.InvariantCulture);

    /// Rows of a raw query on the test database, every value as invariant text.
    private Task<List<string[]>> QueryAsync(string sql) => InDbAsync(async db =>
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
            rows.Add(Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "null" : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture)!)
                .ToArray());
        return rows;
    });
}
