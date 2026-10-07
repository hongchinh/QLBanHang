using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory.StockVouchers;

[Collection(nameof(PostgresCollection))]
public class StockVoucherQueryTests : InventoryTestBase
{
    private const string AdminName = "Quản trị hệ thống";

    public StockVoucherQueryTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Get_returns_line_snapshots_and_permission_flags()
    {
        var p = await CreateInventoryProductAsync("QG01");
        var adminVoucher = await CreateAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(p, 5, 20_000));
        await InDbAsync(async db =>
        {
            var product = await db.Products.SingleAsync(x => x.Id == p);
            product.Name = "Tên mới";
            await db.SaveChangesAsync();
        });

        var dto = await GetAsync(_client, adminVoucher.Id);
        dto.Should().BeEquivalentTo(new
        {
            Code = "PN00001", WarehouseCode = "KHO01", ReasonName = "Nhập khác", OwnerName = AdminName,
            CanEdit = true, CanCancel = true, CanDelete = true,
        });
        dto.Lines.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            ProductCode = "QG01", ProductName = "Hàng QG01", WarehouseCode = "KHO01", UnitName = "Tấm",
            TrackInventory = true, Quantity = 5m, Amount = 100_000m,
        });

        var warehouseUser = await CreateClientForRoleAsync("qg_kho", RoleCodes.Warehouse);
        (await GetAsync(warehouseUser, adminVoucher.Id)).Should().BeEquivalentTo(new { CanEdit = false, CanCancel = false, CanDelete = false });
        var ownVoucher = await CreateAsync(warehouseUser, StockDirection.In, "NKH", "2026-10-03 08:00", LineRequest(p, 1, 20_000));
        (await GetAsync(warehouseUser, ownVoucher.Id)).Should().BeEquivalentTo(new { CanEdit = true, CanCancel = true, CanDelete = true });

        await SetCancelledAsync(adminVoucher.Id);
        (await GetAsync(_client, adminVoucher.Id)).Should().BeEquivalentTo(new
        {
            Status = StockVoucherStatus.Cancelled, CanEdit = false, CanCancel = true, CanDelete = false,
        });
    }

    [Fact]
    public async Task List_scoping_filters_and_aggregates()
    {
        var service = await CreateInventoryProductAsync("QL-DV", track: false);
        var supplier = await CreatePartnerAsync("NCCQ1", isCustomer: false, isSupplier: true);
        var kho02 = await CreateWarehouseAsync(MainBranchId, "KHO02");
        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var other = await CreateClientWithPermissionsAsync("ql_other", null, Permissions.StockIn.View, Permissions.StockIn.Create);

        var v1 = await CreateAsync(_client, StockDirection.In, "NMH", "2026-10-02 00:10", r => r.PartnerId = supplier, LineRequest(service, 1, 100_000));
        var v2 = await CreateAsync(_client, StockDirection.In, "NKH", "2026-10-06 00:30", r => r.Lines[0].WarehouseId = kho02, LineRequest(service, 1, 200_000));
        var v3 = await CreateAsync(other, StockDirection.In, "NKH", "2026-10-10 08:00", r => r.WarehouseId = kho02, LineRequest(service, 1, 300_000));
        var v4 = await CreateAsync(_client, StockDirection.Out, "XKH", "2026-10-03 08:00", LineRequest(service, 1, 50_000));
        var adminB = CloneAdminClient();
        UseBranch(adminB, branchB);
        await CreateAsync(adminB, StockDirection.In, "NKH", "2026-10-03 08:00", r => r.WarehouseId = warehouseB, LineRequest(service, 1, 70_000));
        var v6 = await CreateAsync(_client, StockDirection.In, "NKH", "2026-10-04 08:00", LineRequest(service, 1, 400_000));
        await SetCancelledAsync(v6.Id);
        var otherId = v3.OwnerUserId;

        // Scoped to the working branch and type; default order VoucherAt desc; aggregates exclude Cancelled.
        var all = await ListAsync(_client, "type=In");
        all.Items.Select(i => i.Code).Should().Equal(v3.Code, v2.Code, v6.Code, v1.Code);
        all.TotalItems.Should().Be(4);
        all.Aggregates.Should().BeEquivalentTo(new { GoodsAmount = 600_000m, Total = 600_000m, PaidAmount = 600_000m, DiscountTotal = 0m });
        all.Items.Single(i => i.Id == v1.Id).Should().BeEquivalentTo(new
        {
            WarehouseName = "Kho chính", PartnerName = "Đối tượng NCCQ1", ReasonName = "Nhập mua hàng",
            OwnerName = AdminName, Total = 100_000m, Status = StockVoucherStatus.Active,
        });
        (await ListAsync(_client, "type=Out")).Items.Select(i => i.Id).Should().Equal(v4.Id);

        // Dates are VN dates: 10-02 00:10 VN (10-01 UTC) is in, 10-06 00:30 VN (10-05 UTC) is out.
        (await ListAsync(_client, "type=In&from=2026-10-02&to=2026-10-05")).Items.Select(i => i.Id).Should().Equal(v6.Id, v1.Id);

        // Warehouse on the header (v3) or on a line (v2).
        (await ListAsync(_client, $"type=In&warehouseId={kho02}")).Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { v2.Id, v3.Id });

        (await ListAsync(_client, $"type=In&partnerId={supplier}")).Items.Select(i => i.Id).Should().Equal(v1.Id);
        (await ListAsync(_client, $"type=In&reasonId={await ReasonIdAsync("NMH")}")).Items.Select(i => i.Id).Should().Equal(v1.Id);

        // An explicit status includes Cancelled in the aggregates.
        var cancelled = await ListAsync(_client, "type=In&status=Cancelled");
        cancelled.Items.Select(i => i.Id).Should().Equal(v6.Id);
        cancelled.Aggregates.Total.Should().Be(400_000m);
        (await ListAsync(_client, "type=In&status=Active")).Items.Select(i => i.Id).Should().BeEquivalentTo(new[] { v1.Id, v2.Id, v3.Id });

        (await ListAsync(_client, $"type=In&ownerUserIds={otherId}")).Items.Select(i => i.Id).Should().Equal(v3.Id);

        // Search: ILIKE on Code and PartnerName.
        (await ListAsync(_client, "type=In&search=nccq")).Items.Select(i => i.Id).Should().Equal(v1.Id);
        (await ListAsync(_client, $"type=In&search={v3.Code[^3..]}")).Items.Select(i => i.Id).Should().Equal(v3.Id);

        (await ListAsync(_client, "type=In&sortBy=total&sortDirection=asc")).Items.Select(i => i.Id).Should().Equal(v1.Id, v2.Id, v3.Id, v6.Id);
        var page2 = await ListAsync(_client, "type=In&page=2&pageSize=3");
        page2.Items.Should().ContainSingle();
        page2.TotalItems.Should().Be(4);
    }

    [Fact]
    public async Task Access_rules()
    {
        var service = await CreateInventoryProductAsync("QA-DV", track: false);
        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var adminB = CloneAdminClient();
        UseBranch(adminB, branchB);
        var foreign = await CreateAsync(adminB, StockDirection.In, "NKH", "2026-10-03 08:00", r => r.WarehouseId = warehouseB, LineRequest(service, 1, 70_000));

        (await _client.GetAsync($"/api/stock-vouchers/{foreign.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.GetAsync($"/api/stock-vouchers/{foreign.Id}/activities")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var outVoucher = await CreateAsync(_client, StockDirection.Out, "XKH", "2026-10-03 08:00", LineRequest(service, 1, 50_000));
        var viewer = await CreateClientWithPermissionsAsync("qa_view", null, Permissions.StockIn.View);
        (await viewer.GetAsync("/api/stock-vouchers?type=In")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync("/api/stock-vouchers?type=Out")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync("/api/stock-vouchers/owners?type=Out")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.GetAsync($"/api/stock-vouchers/{outVoucher.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owners_and_activities()
    {
        var service = await CreateInventoryProductAsync("QO-DV", track: false);
        var branchB = await CreateBranchAsync("CN02");
        var warehouseB = await CreateWarehouseAsync(branchB, "KHO-B");
        var khoA = await CreateClientWithPermissionsAsync("qo_a", null, Permissions.StockIn.View, Permissions.StockIn.Create);
        var khoB = await CreateClientWithPermissionsAsync("qo_b", null,
            Permissions.StockIn.View, Permissions.StockIn.Create, Permissions.StockOut.View, Permissions.StockOut.Create);

        var adminVoucher = await CreateAsync(_client, StockDirection.In, "NKH", "2026-10-02 08:00", LineRequest(service, 1, 10_000));
        var aVoucher = await CreateAsync(khoA, StockDirection.In, "NKH", "2026-10-02 09:00", LineRequest(service, 1, 10_000));
        var bVoucher = await CreateAsync(khoB, StockDirection.Out, "XKH", "2026-10-02 10:00", LineRequest(service, 1, 10_000));
        var adminB = CloneAdminClient();
        UseBranch(adminB, branchB);
        var foreign = await CreateAsync(adminB, StockDirection.In, "NKH", "2026-10-03 08:00", r => r.WarehouseId = warehouseB, LineRequest(service, 1, 10_000));
        // An In voucher owned by qo_b, but in branch B: not an owner of the main branch's stock-in list.
        await InDbAsync(async db =>
        {
            var voucher = await db.StockVouchers.SingleAsync(v => v.Id == foreign.Id);
            voucher.OwnerUserId = bVoucher.OwnerUserId;
            await db.SaveChangesAsync();
        });

        var inOwners = await ReadDataAsync<List<StockVoucherOwnerDto>>(await _client.GetAsync("/api/stock-vouchers/owners?type=In"));
        inOwners.Select(o => (o.Id, o.FullName)).Should().Equal((adminVoucher.OwnerUserId, AdminName), (aVoucher.OwnerUserId, "Test qo_a"));
        var outOwners = await ReadDataAsync<List<StockVoucherOwnerDto>>(await _client.GetAsync("/api/stock-vouchers/owners?type=Out"));
        outOwners.Select(o => o.FullName).Should().Equal("Test qo_b");

        await InDbAsync(async db =>
        {
            db.StockVoucherActivities.Add(new StockVoucherActivity
            {
                StockVoucherId = adminVoucher.Id,
                Action = StockVoucherActivityAction.Updated,
                ActorUserId = aVoucher.OwnerUserId,
                OccurredAt = DateTimeOffset.UtcNow.AddHours(1),
                Description = "Cập nhật phiếu",
            });
            await db.SaveChangesAsync();
        });

        var activities = await ReadDataAsync<List<StockVoucherActivityDto>>(
            await _client.GetAsync($"/api/stock-vouchers/{adminVoucher.Id}/activities"));
        activities.Select(a => (a.Action, a.ActorName, a.Description)).Should().Equal(
            (StockVoucherActivityAction.Updated, "Test qo_a", "Cập nhật phiếu"),
            (StockVoucherActivityAction.Created, AdminName, "Tạo phiếu"));
    }

    private Task<StockVoucherDto> CreateAsync(HttpClient client, StockDirection type, string reasonCode, string at,
        params UpsertStockVoucherLineRequest[] lines) => CreateAsync(client, type, reasonCode, at, null, lines);

    private async Task<StockVoucherDto> CreateAsync(HttpClient client, StockDirection type, string reasonCode, string at,
        Action<UpsertStockVoucherRequest>? configure, params UpsertStockVoucherLineRequest[] lines)
    {
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, lines);
        configure?.Invoke(request);
        var (status, voucher, error) = await PostVoucherAsync(client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    private static async Task<StockVoucherDto> GetAsync(HttpClient client, Guid id) =>
        await ReadDataAsync<StockVoucherDto>(await client.GetAsync($"/api/stock-vouchers/{id}"));

    private static async Task<StockVoucherListResult> ListAsync(HttpClient client, string query) =>
        await ReadDataAsync<StockVoucherListResult>(await client.GetAsync($"/api/stock-vouchers?{query}"));

    /// Cancel/restore arrive in Task 5.6; the flags and list filters only need the stored status.
    private Task SetCancelledAsync(Guid id) =>
        InDbAsync(async db =>
        {
            var voucher = await db.StockVouchers.SingleAsync(v => v.Id == id);
            voucher.Status = StockVoucherStatus.Cancelled;
            voucher.CancelledAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
}
