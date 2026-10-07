using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence.Migrations;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt;

[Collection(nameof(PostgresCollection))]
public class BranchOpeningDateTests : CashDebtTestBase
{
    public BranchOpeningDateTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Set_and_read_opening_date()
    {
        var branchId = await CreateBranchAsync(NewCode("OD1"));
        var sales = await CreateClientForRoleAsync("od_sales", RoleCodes.Sales);

        var denied = await PutOpeningDateAsync(sales, branchId, new DateOnly(2026, 10, 1));
        denied.Status.Should().Be(HttpStatusCode.Forbidden);

        var ok = await PutOpeningDateAsync(_client, branchId, new DateOnly(2026, 10, 1));
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Error?.Message);
        ok.Branch!.OpeningDate.Should().Be(new DateOnly(2026, 10, 1));

        var list = await ReadDataAsync<List<BranchDto>>(await _client.GetAsync("/api/branches"));
        list.Single(b => b.Id == branchId).OpeningDate.Should().Be(new DateOnly(2026, 10, 1));

        var outOfRange = await PutOpeningDateAsync(_client, branchId, new DateOnly(1999, 12, 31));
        outOfRange.Status.Should().Be(HttpStatusCode.BadRequest);

        var cleared = await PutOpeningDateAsync(_client, branchId, null);
        cleared.Status.Should().Be(HttpStatusCode.OK, cleared.Error?.Message);
        cleared.Branch!.OpeningDate.Should().BeNull();
    }

    [Fact]
    public async Task Rejected_when_branch_has_documents()
    {
        // The main branch already has stock vouchers from other tests; make sure of it.
        await CreateVoucherAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00",
            LineRequest(await CreateInventoryProductAsync(NewCode("ODP")), 1, 1000));

        var blocked = await PutOpeningDateAsync(_client, MainBranchId, new DateOnly(2026, 10, 1));
        blocked.Status.Should().Be(HttpStatusCode.Conflict);
        blocked.Error!.Code.Should().Be("BRANCH_HAS_DOCUMENTS");

        var branchId = await CreateBranchAsync(NewCode("OD2"));
        var ok = await PutOpeningDateAsync(_client, branchId, new DateOnly(2026, 10, 1));
        ok.Status.Should().Be(HttpStatusCode.OK, ok.Error?.Message);
    }

    [Fact]
    public async Task Rejected_when_locked()
    {
        var withDate = await CreateBranchAsync(NewCode("OD3"));
        (await PutOpeningDateAsync(_client, withDate, new DateOnly(2026, 10, 1))).Status.Should().Be(HttpStatusCode.OK);
        await LockAsync(withDate, new DateOnly(2026, 10, 5));

        // The stored date 10-01 is inside the lock, so it cannot move even to a free date.
        var moved = await PutOpeningDateAsync(_client, withDate, new DateOnly(2026, 10, 10));
        moved.Status.Should().Be(HttpStatusCode.BadRequest);
        moved.Error!.Code.Should().Be("PERIOD_LOCKED");

        var fresh = await CreateBranchAsync(NewCode("OD4"));
        await LockAsync(fresh, new DateOnly(2026, 10, 5));
        var inside = await PutOpeningDateAsync(_client, fresh, new DateOnly(2026, 10, 1));
        inside.Status.Should().Be(HttpStatusCode.BadRequest);
        inside.Error!.Code.Should().Be("PERIOD_LOCKED");
    }

    [Fact]
    public async Task Changing_date_reposts_opening_stock()
    {
        var branchId = await CreateBranchAsync(NewCode("OD5"));
        var warehouseId = await CreateWarehouseAsync(branchId, NewCode("W5"));
        var product = await CreateInventoryProductAsync(NewCode("ODP"));
        (await PutOpeningDateAsync(_client, branchId, new DateOnly(2026, 10, 1))).Status.Should().Be(HttpStatusCode.OK);

        using var branchClient = CloneAdminClient();
        UseBranch(branchClient, branchId);
        var saved = await branchClient.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = warehouseId,
            Lines = { new SaveOpeningStockLine { ProductId = product, Quantity = 5, Amount = 500_000 } },
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());

        var moved = await PutOpeningDateAsync(_client, branchId, new DateOnly(2026, 9, 1));
        moved.Status.Should().Be(HttpStatusCode.OK, moved.Error?.Message);

        var ledger = await InDbAsync(db => db.InventoryLedger.AsNoTracking()
            .Where(e => e.BranchId == branchId && e.SourceType == LedgerSourceType.Opening).ToListAsync());
        ledger.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PostedAt = VnTime.StartOfDay(new DateOnly(2026, 9, 1)),
            QtyIn = 5m,
            InValue = 500_000m,
        });
        var row = await InDbAsync(db => db.OpeningStocks.AsNoTracking().SingleAsync(o => o.BranchId == branchId));
        row.OpeningDate.Should().Be(new DateOnly(2026, 9, 1));
        (await StockOfAsync(product, warehouseId)).Should().Be(5m);

        var clear = await PutOpeningDateAsync(_client, branchId, null);
        clear.Status.Should().Be(HttpStatusCode.Conflict);
        clear.Error!.Code.Should().Be("OPENING_DATA_EXISTS");
    }

    [Fact]
    public async Task Migration_backfill_sql_takes_min_date()
    {
        var branchId = await CreateBranchAsync(NewCode("OD6"));
        var warehouseId = await CreateWarehouseAsync(branchId, NewCode("W6"));
        var p1 = await CreateInventoryProductAsync(NewCode("ODQ"));
        var p2 = await CreateInventoryProductAsync(NewCode("ODR"));

        // The SQL is not scoped to one branch: snapshot every branch's date and restore it afterwards.
        var before = await InDbAsync(db => db.Branches.IgnoreQueryFilters().AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.OpeningDate));
        try
        {
            await InDbAsync(async db =>
            {
                db.OpeningStocks.Add(new OpeningStock { BranchId = branchId, WarehouseId = warehouseId, ProductId = p1, OpeningDate = new DateOnly(2026, 9, 15), Quantity = 1, Amount = 1 });
                db.OpeningStocks.Add(new OpeningStock { BranchId = branchId, WarehouseId = warehouseId, ProductId = p2, OpeningDate = new DateOnly(2026, 9, 1), Quantity = 1, Amount = 1 });
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("UPDATE branches SET opening_date = NULL WHERE id = {0}", branchId);
                await db.Database.ExecuteSqlRawAsync(AddBranchOpeningDate.BackfillSql);
            });

            var date = await InDbAsync(db => db.Branches.AsNoTracking().Where(b => b.Id == branchId).Select(b => b.OpeningDate).SingleAsync());
            date.Should().Be(new DateOnly(2026, 9, 1));
        }
        finally
        {
            await InDbAsync(async db =>
            {
                await db.OpeningStocks.IgnoreQueryFilters().Where(o => o.BranchId == branchId).ExecuteDeleteAsync();
                foreach (var (id, openingDate) in before)
                    await db.Branches.IgnoreQueryFilters().Where(b => b.Id == id)
                        .ExecuteUpdateAsync(s => s.SetProperty(b => b.OpeningDate, openingDate));
            });
        }
    }

    private static string NewCode(string prefix) => $"{prefix}{Guid.NewGuid().ToString("N")[..8]}";

    private async Task LockAsync(Guid branchId, DateOnly lockedUntil)
    {
        var response = await _client.PutAsJsonAsync($"/api/branches/{branchId}/lock", new SetPeriodLockRequest { LockedUntil = lockedUntil });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Status, BranchDto? Branch, ApiError? Error)> PutOpeningDateAsync(
        HttpClient client, Guid branchId, DateOnly? date)
    {
        var response = await client.PutAsJsonAsync($"/api/branches/{branchId}/opening-date", new SetOpeningDateRequest { OpeningDate = date });
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<BranchDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }
}
