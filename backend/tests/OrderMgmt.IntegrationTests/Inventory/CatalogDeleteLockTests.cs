using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Inventory.Interfaces;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

/// Product and warehouse deletes check inventory activity under the locks postings take (review finding),
/// so a posting in flight finishes before the check runs.
[Collection(nameof(PostgresCollection))]
public class CatalogDeleteLockTests : InventoryTestBase
{
    public CatalogDeleteLockTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Product_delete_waits_for_the_product_key()
    {
        var p = await CreateInventoryProductAsync("DL01");

        var status = await DeleteWhileHeldAsync($"/api/products/{p}",
            inventoryLock => inventoryLock.AcquireProductsAsync(new[] { p }));

        status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Warehouse_delete_waits_for_the_branch_gate()
    {
        var w = await CreateWarehouseAsync(MainBranchId, "KDL01");

        var status = await DeleteWhileHeldAsync($"/api/warehouses/{w}",
            inventoryLock => inventoryLock.AcquireBranchGateAsync(new[] { MainBranchId }, exclusive: false));

        status.Should().Be(HttpStatusCode.OK);
    }

    /// Holds a lock as a posting would, sends the delete, checks that it waits, then releases the lock.
    private async Task<HttpStatusCode> DeleteWhileHeldAsync(string url, Func<IInventoryLock, Task> hold)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await hold(scope.ServiceProvider.GetRequiredService<IInventoryLock>());

        var delete = _client.DeleteAsync(url);
        (await Task.WhenAny(delete, Task.Delay(TimeSpan.FromMilliseconds(500)))).Should()
            .NotBeSameAs(delete, "the delete must wait for the lock");

        await tx.CommitAsync();
        return (await delete).StatusCode;
    }
}
