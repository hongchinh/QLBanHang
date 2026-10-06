using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence.Seed;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class InventorySeedTests : InventoryTestBase
{
    public InventorySeedTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Seed_creates_reference_data_once()
    {
        var warehouse = await InDbAsync(db => db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == DefaultWarehouseId));
        warehouse.Code.Should().Be("KHO01");
        warehouse.Name.Should().Be("Kho chính");
        warehouse.BranchId.Should().Be(MainBranchId);

        var reasons = await InDbAsync(db => db.StockReasons.AsNoTracking().OrderBy(r => r.Code).ToListAsync());
        reasons.Select(r => (r.Code, r.Direction, r.PartnerType, r.IsSystem)).Should().Equal(
            ("NKH", StockDirection.In, PartnerType.Any, true),
            ("NMH", StockDirection.In, PartnerType.Supplier, true),
            ("XBH", StockDirection.Out, PartnerType.Customer, true),
            ("XKH", StockDirection.Out, PartnerType.Any, true));

        var methods = await InDbAsync(db => db.PaymentMethods.AsNoTracking().OrderBy(m => m.Code).ToListAsync());
        methods.Select(m => (m.Code, m.IsCash)).Should().Equal(("CK", false), ("TM", true));

        await DbSeeder.SeedAsync(_factory.Services);

        (await InDbAsync(db => db.Warehouses.CountAsync())).Should().Be(1);
        (await InDbAsync(db => db.StockReasons.CountAsync())).Should().Be(4);
        (await InDbAsync(db => db.PaymentMethods.CountAsync())).Should().Be(2);
        (await InDbAsync(db => db.DocumentNumberings.CountAsync())).Should().Be(2);
    }
}
