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
        await AssertForeignKeysKnownAsync();
        // Read at run time, so a table added later is covered too (R4).
        var branchTables = await QueryAsync("select table_name from information_schema.columns"
            + " where table_schema = 'public' and column_name = 'branch_id' order by table_name");
        var tables = branchTables.Select(r => r[0]).ToList();
        var before = await SnapshotAsync(tables, exceptBranchId: null);

        var branch = await CreateDemoBranchAsync("DD01");
        var fill = await FillAsync(branch);
        await CreateTestUserAsync("dd_user", "Pass@123", RoleCodes.Sales, defaultBranchId: branch.BranchId);
        await CreateTestUserAsync("dd_gone", "Pass@123", RoleCodes.Sales, defaultBranchId: branch.BranchId);
        await InDbAsync(db => db.Users.Where(u => u.Username == "dd_gone")
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsDeleted, true)));
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
        result!.ReassignedUserCount.Should().Be(2, "the soft-deleted user moves too");
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

    [Fact]
    public async Task Delete_refuses_when_another_branchs_voucher_uses_its_warehouse()
    {
        var branch = await CreateDemoBranchAsync("DD07");
        var product = await CreateInventoryProductAsync("DDSP07");
        var voucher = await PostStockLineAsync(branch, StockDirection.In, "NMH", "2026-10-03 09:00",
            await CreatePartnerAsync("DDSUP07", isCustomer: false, isSupplier: true), product, 1, 1_000m);
        // A test-only corruption standing in for a warehouse moved between branches.
        await InDbAsync(db => db.StockVouchers.Where(v => v.Id == voucher.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.BranchId, MainBranchId)));

        await AssertErrorAsync(await _client.DeleteAsync(DeleteUrl(branch.BranchId)), HttpStatusCode.Conflict,
            "DEMO_WAREHOUSE_IN_USE");
        (await InDbAsync(db => db.Branches.AnyAsync(b => b.Id == branch.BranchId))).Should().BeTrue();
    }

    [Fact]
    public async Task Delete_refuses_when_another_branchs_line_uses_its_warehouse()
    {
        var branch = await CreateDemoBranchAsync("DD08");
        var product = await CreateInventoryProductAsync("DDSP08");
        var main = new DemoBranch(MainBranchId, DefaultWarehouseId, await CashFundIdAsync(MainBranchId), _client);
        var voucher = await PostStockLineAsync(main, StockDirection.In, "NMH", "2026-10-03 09:00",
            await CreatePartnerAsync("DDSUP08", isCustomer: false, isSupplier: true), product, 1, 1_000m);
        await InDbAsync(db => db.StockVoucherLines.Where(l => l.StockVoucherId == voucher.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.WarehouseId, branch.WarehouseId)));

        await AssertErrorAsync(await _client.DeleteAsync(DeleteUrl(branch.BranchId)), HttpStatusCode.Conflict,
            "DEMO_WAREHOUSE_IN_USE");
        (await InDbAsync(db => db.Branches.AnyAsync(b => b.Id == branch.BranchId))).Should().BeTrue();
    }

    [Fact]
    public async Task A_foreign_key_hit_at_the_branch_delete_gives_409_and_rolls_everything_back()
    {
        var branch = await CreateDemoBranchAsync("DD09");
        await FillAsync(branch);
        var stockVouchers = await BranchRowCountAsync("stock_vouchers", branch.BranchId);
        var creditLimits = await BranchRowCountAsync("credit_limits", branch.BranchId);
        stockVouchers.Should().BePositive();
        creditLimits.Should().BePositive();
        var numberings = await BranchRowCountAsync("document_numberings", branch.BranchId);
        var warehouses = await BranchRowCountAsync("warehouses", branch.BranchId);
        var funds = await BranchRowCountAsync("funds", branch.BranchId);
        // A write outside the gate, standing in for a fund created for the branch after the funds step: the trigger
        // inserts one when the branch row is about to go, so only the branch delete (step 15) fails on a foreign key.
        await InDbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync(
                "create function demo_race() returns trigger language plpgsql as $$ declare f funds; begin"
                + " select * into f from funds where branch_id <> old.id limit 1;"
                + " f.id := gen_random_uuid(); f.code := 'RACE'; f.branch_id := old.id; insert into funds select f.*;"
                + " return old; end $$");
            await db.Database.ExecuteSqlRawAsync(
                "create trigger demo_race before delete on branches for each row execute function demo_race()");
        });
        try
        {
            await AssertErrorAsync(await _client.DeleteAsync(DeleteUrl(branch.BranchId)), HttpStatusCode.Conflict,
                "DEMO_BRANCH_BUSY");
        }
        finally
        {
            await InDbAsync(async db =>
            {
                await db.Database.ExecuteSqlRawAsync("drop trigger if exists demo_race on branches");
                await db.Database.ExecuteSqlRawAsync("drop function if exists demo_race()");
            });
        }

        (await InDbAsync(db => db.Branches.AnyAsync(b => b.Id == branch.BranchId))).Should().BeTrue();
        (await BranchRowCountAsync("stock_vouchers", branch.BranchId)).Should().Be(stockVouchers, "step 7 is rolled back");
        (await BranchRowCountAsync("credit_limits", branch.BranchId)).Should().Be(creditLimits, "step 10 is rolled back");
        (await BranchRowCountAsync("funds", branch.BranchId)).Should().Be(funds, "step 11 is rolled back");
        (await BranchRowCountAsync("warehouses", branch.BranchId)).Should().Be(warehouses, "step 12 is rolled back");
        (await BranchRowCountAsync("document_numberings", branch.BranchId)).Should().Be(numberings, "step 13 is rolled back");
        (await InDbAsync(db => db.Funds.AnyAsync(f => f.Id == branch.CashFundId))).Should().BeTrue();
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Id == branch.WarehouseId))).Should().BeTrue();
    }

    [Fact]
    public async Task A_permanent_foreign_key_corruption_is_not_reported_as_busy()
    {
        var branch = await CreateDemoBranchAsync("DD10");
        await ReportDataset.SetFundOpeningAsync(branch.Client, branch.CashFundId, 10_000m);
        var expense = CashRequest(CashVoucherType.Payment, await CashReasonIdAsync("CK"), branch.CashFundId,
            "2026-10-09 09:00", partnerId: null, 100m);
        expense.PersonName = "Nguyễn C";
        var cash = await CreateCashAsync(branch.Client, expense);
        // A test-only corruption: a main-branch document on the demo branch's fund. A bug, not a race.
        await InDbAsync(db => db.CashVouchers.Where(v => v.Id == cash.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.BranchId, MainBranchId)));

        var response = await _client.DeleteAsync(DeleteUrl(branch.BranchId));

        var (status, _, error) = await ReadDocumentResponseAsync<object>(response);
        status.Should().Be(HttpStatusCode.InternalServerError);
        error!.Code.Should().Be("INTERNAL_ERROR");
        // Nothing before or at the failing step (11, the funds) was removed. That is all this shows: the rollback of the
        // earlier steps is proved by the 409 test above.
        (await InDbAsync(db => db.Branches.AnyAsync(b => b.Id == branch.BranchId))).Should().BeTrue();
        (await InDbAsync(db => db.Funds.AnyAsync(f => f.Id == branch.CashFundId))).Should().BeTrue();
        (await InDbAsync(db => db.Warehouses.AnyAsync(w => w.Id == branch.WarehouseId))).Should().BeTrue();
    }

    /// The foreign keys into the branch and into every parent the delete removes, as "child.column -> parent [delete
    /// action: r restrict, n set null]". A new one (another column name, another child table, another action) fails the
    /// test until it is covered and the list is updated: a key into branches, stock_vouchers, cash_vouchers,
    /// fund_transfers, debt_offset_vouchers, debt_entries, funds or warehouses needs its rows deleted in DeleteAsync; a
    /// key into products, customers, customer_addresses or product_groups needs a reference check in
    /// CleanSampleCatalogAsync plus a keep test in DemoBranchCatalogCleanupTests. The two [n] keys are why a product or
    /// group delete cannot hit a foreign key (only the partner delete can race): changing their action breaks that.
    private static readonly string[] KnownForeignKeys =
    {
        "cash_voucher_lines.cash_voucher_id -> cash_vouchers [r]",
        "cash_vouchers.branch_id -> branches [r]",
        "cash_vouchers.fund_id -> funds [r]",
        "cash_vouchers.partner_id -> customers [r]",
        "cash_vouchers.source_stock_voucher_id -> stock_vouchers [r]",
        "credit_limits.branch_id -> branches [r]",
        "credit_limits.customer_id -> customers [r]",
        "customer_addresses.customer_id -> customers [r]",
        "debt_entries.branch_id -> branches [r]",
        "debt_entries.partner_id -> customers [r]",
        "debt_offset_vouchers.branch_id -> branches [r]",
        "debt_offset_vouchers.partner_id -> customers [r]",
        "debt_settlements.decrease_entry_id -> debt_entries [r]",
        "debt_settlements.increase_entry_id -> debt_entries [r]",
        "document_numberings.branch_id -> branches [r]",
        "fund_transfers.branch_id -> branches [r]",
        "fund_transfers.from_fund_id -> funds [r]",
        "fund_transfers.to_fund_id -> funds [r]",
        "funds.branch_id -> branches [r]",
        "opening_debts.branch_id -> branches [r]",
        "opening_debts.partner_id -> customers [r]",
        "opening_stocks.branch_id -> branches [r]",
        "opening_stocks.product_id -> products [r]",
        "opening_stocks.warehouse_id -> warehouses [r]",
        "products.product_group_id -> product_groups [n]",
        "quotation_lines.product_id -> products [n]",
        "quotations.customer_id -> customers [r]",
        "stock_voucher_activities.stock_voucher_id -> stock_vouchers [r]",
        "stock_voucher_lines.product_id -> products [r]",
        "stock_voucher_lines.stock_voucher_id -> stock_vouchers [r]",
        "stock_voucher_lines.warehouse_id -> warehouses [r]",
        "stock_vouchers.branch_id -> branches [r]",
        "stock_vouchers.fund_id -> funds [r]",
        "stock_vouchers.partner_id -> customers [r]",
        "stock_vouchers.warehouse_id -> warehouses [r]",
        "users.default_branch_id -> branches [r]",
        "warehouses.branch_id -> branches [r]",
    };

    private async Task AssertForeignKeysKnownAsync()
    {
        var rows = await QueryAsync("select x from (select c.conrelid::regclass::text || '.' || a.attname || ' -> ' || c.confrelid::regclass::text || ' [' || c.confdeltype::text || ']' as x"
            + " from pg_constraint c join pg_attribute a on a.attrelid = c.conrelid and a.attnum = any(c.conkey)"
            + " where c.contype = 'f' and c.confrelid::regclass::text in ('branches', 'stock_vouchers', 'cash_vouchers',"
            + " 'fund_transfers', 'debt_offset_vouchers', 'debt_entries', 'funds', 'warehouses', 'products', 'customers',"
            + " 'customer_addresses', 'product_groups')) t order by x collate \"C\"");
        rows.Select(r => r[0]).Should().Equal(KnownForeignKeys,
            "a new foreign key needs its rows deleted in DemoBranchService.DeleteAsync, or (into products, customers,"
            + " customer_addresses, product_groups) a reference check in CleanSampleCatalogAsync plus a keep test");
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
