using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class StockSchemaTests : InventoryTestBase
{
    private Guid _reasonId;
    private Guid _adminId;

    public StockSchemaTests(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _reasonId = await InDbAsync(db => db.StockReasons.Where(r => r.Code == "NMH").Select(r => r.Id).SingleAsync());
        _adminId = await InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync());
    }

    [Fact]
    public async Task Voucher_code_is_unique_per_type_and_branch_among_active_rows()
    {
        var voucher = NewVoucher(StockDirection.In, MainBranchId, "PN00001");
        await InDbAsync(async db =>
        {
            db.StockVouchers.Add(voucher);
            db.StockVoucherLines.Add(NewLine(voucher.Id, 0));
            db.StockVoucherLines.Add(NewLine(voucher.Id, 1));
            await db.SaveChangesAsync();
        });

        var reloaded = await InDbAsync(db => db.StockVouchers.AsNoTracking().Include(v => v.Lines).SingleAsync(v => v.Id == voucher.Id));
        reloaded.Lines.Select(l => l.SortOrder).Should().BeEquivalentTo(new[] { 0, 1 });
        reloaded.Version.Should().NotBe(0u);

        var duplicate = async () => await InsertAsync(NewVoucher(StockDirection.In, MainBranchId, "PN00001"));
        (await duplicate.Should().ThrowAsync<DbUpdateException>())
            .Which.InnerException.Should().BeOfType<PostgresException>()
            .Which.SqlState.Should().Be("23505");

        var otherBranchId = await CreateBranchAsync("CN02");
        await InsertAsync(NewVoucher(StockDirection.Out, MainBranchId, "PN00001"));
        await InsertAsync(NewVoucher(StockDirection.In, otherBranchId, "PN00001"));

        await InDbAsync(async db =>
        {
            var tracked = await db.StockVouchers.SingleAsync(v => v.Id == voucher.Id);
            tracked.IsDeleted = true;
            await db.SaveChangesAsync();
        });
        await InsertAsync(NewVoucher(StockDirection.In, MainBranchId, "PN00001"));
    }

    [Fact]
    public async Task Stale_version_update_throws_concurrency_exception()
    {
        var voucher = NewVoucher(StockDirection.In, MainBranchId, "PN00002");
        await InsertAsync(voucher);

        await InDbAsync(async dbA =>
        {
            await InDbAsync(async dbB =>
            {
                var a = await dbA.StockVouchers.SingleAsync(v => v.Id == voucher.Id);
                var b = await dbB.StockVouchers.SingleAsync(v => v.Id == voucher.Id);

                a.Note = "A";
                await dbA.SaveChangesAsync();

                b.Note = "B";
                var stale = async () => await dbB.SaveChangesAsync();
                await stale.Should().ThrowAsync<DbUpdateConcurrencyException>();
            });
        });
    }

    private Task InsertAsync(StockVoucher voucher) =>
        InDbAsync(async db =>
        {
            db.StockVouchers.Add(voucher);
            await db.SaveChangesAsync();
        });

    private StockVoucher NewVoucher(StockDirection type, Guid branchId, string code) => new()
    {
        Type = type,
        Code = code,
        VoucherAt = new DateTimeOffset(2026, 10, 2, 1, 0, 0, TimeSpan.Zero),
        BranchId = branchId,
        WarehouseId = DefaultWarehouseId,
        ReasonId = _reasonId,
        OwnerUserId = _adminId,
        Status = StockVoucherStatus.Active,
    };

    private StockVoucherLine NewLine(Guid voucherId, int sortOrder) => new()
    {
        StockVoucherId = voucherId,
        SortOrder = sortOrder,
        ProductId = _productId,
        ProductCode = "HH-TEST-001",
        ProductName = "Test EPS 1000x2000",
        WarehouseId = DefaultWarehouseId,
        TrackInventory = true,
        PricingMode = PricingMode.PerUnit,
        UnitName = "Tấm",
        Quantity = 2m,
        UnitPrice = 50_000m,
        Amount = 100_000m,
        NetAmount = 100_000m,
        InboundValue = 100_000m,
    };
}
