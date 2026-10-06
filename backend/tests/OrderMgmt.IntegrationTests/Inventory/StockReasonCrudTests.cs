using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderMgmt.Application.Inventory.StockReasons.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Inventory;

[Collection(nameof(PostgresCollection))]
public class StockReasonCrudTests : InventoryTestBase
{
    public StockReasonCrudTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Crud_happy_path_with_direction_filter_and_guard()
    {
        var created = await ReadDataAsync<StockReasonDto>(await _client.PostAsJsonAsync("/api/stock-reasons",
            new CreateStockReasonRequest { Code = "NTR", Name = "Nhập trả", Direction = StockDirection.In, PartnerType = PartnerType.Customer }));
        created.IsSystem.Should().BeFalse();
        created.PartnerType.Should().Be(PartnerType.Customer);

        await ReadDataAsync<StockReasonDto>(await _client.PostAsJsonAsync("/api/stock-reasons",
            new CreateStockReasonRequest { Code = "XHU", Name = "Xuất hủy", Direction = StockDirection.Out, PartnerType = PartnerType.None }));

        var inbound = await ReadDataAsync<List<StockReasonDto>>(await _client.GetAsync("/api/stock-reasons?direction=In"));
        inbound.Select(r => r.Code).Should().Contain("NTR").And.NotContain("XHU");

        var updated = await ReadDataAsync<StockReasonDto>(await _client.PutAsJsonAsync($"/api/stock-reasons/{created.Id}",
            new UpdateStockReasonRequest { Name = "Nhập trả lại", Direction = StockDirection.In, PartnerType = PartnerType.Any }));
        updated.Name.Should().Be("Nhập trả lại");
        updated.PartnerType.Should().Be(PartnerType.Any);

        (await _client.DeleteAsync($"/api/stock-reasons/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/stock-reasons/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var sales = await CreateClientForRoleAsync("sr_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/stock-reasons",
                new CreateStockReasonRequest { Code = "X1", Name = "X1", Direction = StockDirection.Out, PartnerType = PartnerType.Any }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task System_reason_rules()
    {
        var id = await InDbAsync(async db =>
        {
            var reason = new StockReason { Code = "SYS1", Name = "Hệ thống", Direction = StockDirection.In, PartnerType = PartnerType.Supplier, IsSystem = true };
            db.StockReasons.Add(reason);
            await db.SaveChangesAsync();
            return reason.Id;
        });

        (await _client.DeleteAsync($"/api/stock-reasons/{id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync($"/api/stock-reasons/{id}",
                new UpdateStockReasonRequest { Name = "Hệ thống", Direction = StockDirection.Out, PartnerType = PartnerType.Supplier }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await _client.PutAsJsonAsync($"/api/stock-reasons/{id}",
                new UpdateStockReasonRequest { Name = "Hệ thống", Direction = StockDirection.In, PartnerType = PartnerType.Any }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var renamed = await ReadDataAsync<StockReasonDto>(await _client.PutAsJsonAsync($"/api/stock-reasons/{id}",
            new UpdateStockReasonRequest { Name = "Đổi tên", Direction = StockDirection.In, PartnerType = PartnerType.Supplier }));
        renamed.Name.Should().Be("Đổi tên");
    }
}
