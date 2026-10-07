using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class WarehouseCrudTests : InventoryTestBase
{
    public WarehouseCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Crud_happy_path()
    {
        var create = await _client.PostAsJsonAsync("/api/warehouses",
            new CreateWarehouseRequest { Code = "KHO-A", Name = "Kho A", IsActive = true });
        var created = await ReadDataAsync<WarehouseDto>(create);
        created.BranchId.Should().Be(MainBranchId);
        created.BranchCode.Should().Be("CN01");
        created.IsActive.Should().BeTrue();

        var got = await ReadDataAsync<WarehouseDto>(await _client.GetAsync($"/api/warehouses/{created.Id}"));
        got.Name.Should().Be("Kho A");

        var updated = await ReadDataAsync<WarehouseDto>(await _client.PutAsJsonAsync($"/api/warehouses/{created.Id}",
            new UpdateWarehouseRequest { Name = "Kho A2", BranchId = MainBranchId, IsActive = false }));
        updated.Name.Should().Be("Kho A2");
        updated.IsActive.Should().BeFalse();

        var inactiveOnly = await ReadDataAsync<List<WarehouseDto>>(await _client.GetAsync("/api/warehouses?isActive=false"));
        inactiveOnly.Select(w => w.Code).Should().Contain("KHO-A");

        (await _client.DeleteAsync($"/api/warehouses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/warehouses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_and_creating_follow_the_working_branch()
    {
        var b = await CreateBranchAsync("CN02");
        await CreateWarehouseAsync(MainBranchId, "W1");
        await CreateWarehouseAsync(b, "W2");

        var plain = await ReadDataAsync<List<WarehouseDto>>(await _client.GetAsync("/api/warehouses"));
        plain.Select(w => w.Code).Should().Contain("W1").And.NotContain("W2");

        var inB = CloneAdminClient();
        UseBranch(inB, b);
        var listB = await ReadDataAsync<List<WarehouseDto>>(await inB.GetAsync("/api/warehouses"));
        listB.Select(w => w.Code).Should().Equal("W2");

        var manager = await CreateClientWithPermissionsAsync("wh_manager", null, Permissions.Inventory.ManageCatalogs);
        var managerList = await ReadDataAsync<List<WarehouseDto>>(await manager.GetAsync($"/api/warehouses?branchId={b}"));
        managerList.Select(w => w.Code).Should().Contain("W1").And.NotContain("W2");
        (await manager.PostAsJsonAsync("/api/warehouses",
                new CreateWarehouseRequest { Code = "W3", Name = "W3", BranchId = b, IsActive = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var sales = await CreateClientForRoleAsync("wh_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest { Code = "W4", Name = "W4", IsActive = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_code_and_branch_delete_conflicts()
    {
        await CreateWarehouseAsync(MainBranchId, "W-DUP");
        (await _client.PostAsJsonAsync("/api/warehouses", new CreateWarehouseRequest { Code = "W-DUP", Name = "Dup", IsActive = true }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var b = await CreateBranchAsync("CN07");
        await CreateWarehouseAsync(b, "W-B");
        (await _client.DeleteAsync($"/api/branches/{b}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
