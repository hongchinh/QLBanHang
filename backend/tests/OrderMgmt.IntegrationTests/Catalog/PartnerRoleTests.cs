using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Search.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Inventory;
using Xunit;

namespace OrderMgmt.IntegrationTests.Catalog;

[Collection(nameof(PostgresCollection))]
public class PartnerRoleTests : InventoryTestBase
{
    public PartnerRoleTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Role_endpoints_create_list_and_get_their_own_role()
    {
        var customer = await ReadDataAsync<CustomerDto>(await _client.PostAsJsonAsync("/api/customers",
            new CreateCustomerRequest { Code = "P-KH", Name = "Khách" }));
        customer.IsCustomer.Should().BeTrue();
        customer.IsSupplier.Should().BeFalse();

        var supplier = await ReadDataAsync<CustomerDto>(await _client.PostAsJsonAsync("/api/suppliers",
            new CreateCustomerRequest { Code = "P-NCC", Name = "Nhà cung cấp" }));
        supplier.IsCustomer.Should().BeFalse();
        supplier.IsSupplier.Should().BeTrue();

        await CreatePartnerAsync("P-BOTH", isCustomer: true, isSupplier: true);

        var customers = await ReadDataAsync<PagedResult<CustomerListItemDto>>(await _client.GetAsync("/api/customers?pageSize=100"));
        customers.Items.Select(c => c.Code).Should().Contain(new[] { "P-KH", "P-BOTH" }).And.NotContain("P-NCC");

        var suppliers = await ReadDataAsync<PagedResult<CustomerListItemDto>>(await _client.GetAsync("/api/suppliers?pageSize=100"));
        suppliers.Items.Select(c => c.Code).Should().Contain(new[] { "P-NCC", "P-BOTH" }).And.NotContain("P-KH");

        (await _client.GetAsync($"/api/customers/{supplier.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.GetAsync($"/api/suppliers/{supplier.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sales_user_cannot_touch_the_supplier_role()
    {
        var sales = await CreateClientForRoleAsync("pr_sales", RoleCodes.Sales);
        (await sales.PostAsJsonAsync("/api/customers", new CreateCustomerRequest { Code = "P-S1", Name = "X", IsSupplier = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var both = await CreatePartnerAsync("P-S2", isCustomer: true, isSupplier: true);
        (await sales.PutAsJsonAsync($"/api/customers/{both}", new UpdateCustomerRequest { Name = "Sửa", Status = Domain.Enums.CustomerStatus.Active }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Updating_needs_update_permission_of_every_role()
    {
        var both = await CreatePartnerAsync("P-U1", isCustomer: true, isSupplier: true);
        var editor = await CreateClientWithPermissionsAsync("pr_editor", null,
            Permissions.Customers.View, Permissions.Customers.Update, Permissions.Suppliers.View, Permissions.Suppliers.Update);
        (await editor.PutAsJsonAsync($"/api/customers/{both}", new UpdateCustomerRequest { Name = "Đã sửa", Status = Domain.Enums.CustomerStatus.Active }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var supplierOnly = await CreatePartnerAsync("P-U2", isCustomer: false, isSupplier: true);
        var supplierAdmin = await CreateClientWithPermissionsAsync("pr_supplier_admin", null,
            Permissions.Suppliers.View, Permissions.Suppliers.Create, Permissions.Suppliers.Update, Permissions.Suppliers.Delete);
        (await supplierAdmin.PutAsJsonAsync($"/api/suppliers/{supplierOnly}",
                new UpdateCustomerRequest { Name = "NCC", Status = Domain.Enums.CustomerStatus.Active, IsCustomer = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Deleting_dual_role_partner_requires_both_delete_permissions()
    {
        var both = await CreatePartnerAsync("P-D1", isCustomer: true, isSupplier: true);
        var deleter = await CreateClientWithPermissionsAsync("pr_deleter", null, Permissions.Customers.View, Permissions.Customers.Delete);
        (await deleter.DeleteAsync($"/api/customers/{both}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _client.DeleteAsync($"/api/customers/{both}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Role_validation_and_search_scoping()
    {
        var customerOnly = await CreatePartnerAsync("P-V1", isCustomer: true, isSupplier: false);
        var put = await _client.PutAsJsonAsync($"/api/customers/{customerOnly}",
            new UpdateCustomerRequest { Name = "X", Status = Domain.Enums.CustomerStatus.Active, IsCustomer = false, IsSupplier = false });
        put.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await put.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("PARTNER_ROLE_REQUIRED");

        await CreatePartnerAsync("P-V2", isCustomer: false, isSupplier: true);
        var customerHits = await ReadDataAsync<List<CustomerSearchItemDto>>(await _client.GetAsync("/api/customers/search?keyword=P-V"));
        customerHits.Select(c => c.Code).Should().Equal("P-V1");
        var supplierHits = await ReadDataAsync<List<CustomerSearchItemDto>>(await _client.GetAsync("/api/suppliers/search?keyword=P-V"));
        supplierHits.Select(c => c.Code).Should().Equal("P-V2");
    }

    [Fact]
    public async Task Supplier_only_partner_is_persisted_as_not_a_customer()
    {
        var supplier = await ReadDataAsync<CustomerDto>(await _client.PostAsJsonAsync("/api/suppliers",
            new CreateCustomerRequest { Code = "P-PERSIST", Name = "NCC lưu" }));

        var stored = await InDbAsync(db => db.Customers.AsNoTracking().SingleAsync(c => c.Id == supplier.Id));
        stored.IsCustomer.Should().BeFalse();
        stored.IsSupplier.Should().BeTrue();
    }

    [Fact]
    public async Task Global_search_and_quotations_respect_roles()
    {
        var supplierOnly = await CreatePartnerAsync("NCC-GLOBAL", isCustomer: false, isSupplier: true);

        var sales = await CreateClientForRoleAsync("pr_global_sales", RoleCodes.Sales);
        var salesResult = await ReadDataAsync<GlobalSearchResultDto>(await sales.GetAsync("/api/search/global?q=NCC-GLOBAL"));
        salesResult.Customers.Should().BeEmpty();
        salesResult.Suppliers.Should().BeEmpty();

        var adminResult = await ReadDataAsync<GlobalSearchResultDto>(await _client.GetAsync("/api/search/global?q=NCC-GLOBAL"));
        adminResult.Customers.Should().BeEmpty();
        adminResult.Suppliers.Select(s => s.Id).Should().Equal(supplierOnly);

        var request = BuildRequest();
        request.CustomerId = supplierOnly;
        var quotation = await _client.PostAsJsonAsync("/api/quotations", request);
        quotation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await quotation.Content.ReadFromJsonAsync<ApiResponse>(TestJson.Options))!.Error!.Code.Should().Be("PARTNER_NOT_CUSTOMER");
    }
}
