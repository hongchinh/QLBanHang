using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.Catalog.Customers.Models;
using OrderMgmt.Application.CashDebt.CashVouchers.Models;
using OrderMgmt.Application.CashDebt.Ledger;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.CashDebt.Matching;

/// GET /api/debt/open-items and GET /api/debt/partners (Task 8.1).
[Collection(nameof(PostgresCollection))]
public class OpenItemsApiTests : CashDebtTestBase
{
    private static readonly DateOnly Sep1 = new(2026, 9, 1);

    public OpenItemsApiTests(PostgresFixture pg) : base(pg) { }

    [Fact]
    public async Task Partner_search_by_side_and_roles()
    {
        var keyword = NewCode("DPS");
        var customer = await CreatePartnerAsync(keyword + "-KH", isCustomer: true, isSupplier: false);
        var supplier = await CreatePartnerAsync(keyword + "-NCC", isCustomer: false, isSupplier: true);
        var dual = await CreatePartnerAsync(keyword + "-DUAL", isCustomer: true, isSupplier: true);
        var inactive = await CreatePartnerAsync(keyword + "-OFF", isCustomer: true, isSupplier: true);
        await InDbAsync(db => db.Customers.Where(c => c.Id == inactive)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, CustomerStatus.Inactive)));

        var receivable = await SearchAsync(_client, $"side=Receivable&keyword={keyword}");
        receivable.Select(p => p.Id).Should().BeEquivalentTo(new[] { customer, dual });

        var payable = await SearchAsync(_client, $"side=Payable&keyword={keyword}");
        payable.Select(p => p.Id).Should().BeEquivalentTo(new[] { supplier, dual });

        var both = await SearchAsync(_client, $"bothRoles=true&keyword={keyword}");
        both.Select(p => p.Id).Should().BeEquivalentTo(new[] { dual });

        var any = await SearchAsync(_client, $"keyword={keyword}");
        any.Select(p => p.Id).Should().Equal(new[] { dual, customer, supplier }, "any role, ordered by code");
        any[1].Should().BeEquivalentTo(new
        {
            Id = customer,
            Code = keyword + "-KH",
            Name = $"Đối tượng {keyword}-KH",
            TaxCode = "0100000000",
            CompanyAddress = "Hà Nội",
            Status = CustomerStatus.Active,
            IsCustomer = true,
            IsSupplier = false,
        });

        // The keyword also matches the name, and the limit caps the result.
        (await SearchAsync(_client, $"keyword={Uri.EscapeDataString("Đối tượng " + keyword)}&limit=2"))
            .Select(p => p.Id).Should().Equal(dual, customer);

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var accountant = await CreateClientForRoleAsync("dps_acc_" + suffix, RoleCodes.Accountant);
        var accountantResult = await SearchAsync(accountant, $"side=Payable&keyword={keyword}");
        accountantResult.Select(p => p.Id).Should().BeEquivalentTo(new[] { supplier, dual },
            "an accountant needs neither customers.view nor suppliers.view");

        var warehouse = await CreateClientForRoleAsync("dps_wh_" + suffix, RoleCodes.Warehouse);
        (await warehouse.GetAsync($"/api/debt/partners?keyword={keyword}")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Returns_open_items_in_working_branch()
    {
        // "Main" is a fresh branch: an opening debt pins its branch's opening date (invariant 5).
        var (mainId, main) = await BranchClientAsync("OIA");
        var (branchBId, branchB) = await BranchClientAsync("OIB");
        var customer = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var product = await CreateInventoryProductAsync(NewCode("SP"), track: false);

        var opening = await CreateOpeningDebtAsync(main, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 5_000m, dueDate: new DateOnly(2026, 10, 20));
        var mainSale = await PostSaleAsync(main, await CreateWarehouseAsync(mainId, NewCode("K")), customer, product,
            "2026-10-05 09:00", 3_000m, new DateOnly(2026, 10, 10));
        var otherSale = await PostSaleAsync(branchB, await CreateWarehouseAsync(branchBId, NewCode("K")), customer,
            product, "2026-10-06 09:00", 2_000m, new DateOnly(2026, 10, 16));

        var mainItems = await OpenItemsAsync(main, $"partnerId={customer}&side=Receivable");
        mainItems.Should().HaveCount(2);
        mainItems.Select(i => i.SourceId).Should().Equal(new[] { mainSale, opening.OpeningDebtId },
            "ordered by due date");
        mainItems[0].Should().BeEquivalentTo(new
        {
            SourceType = DebtSourceType.StockOut,
            DocDate = new DateOnly(2026, 10, 5),
            DueDate = (DateOnly?)new DateOnly(2026, 10, 10),
            Direction = DebtDirection.Increase,
            Amount = 3_000m,
            Settled = 0m,
            Open = 3_000m,
        });
        mainItems[1].Should().BeEquivalentTo(new
        {
            EntryId = opening.EntryId,
            SourceType = DebtSourceType.Opening,
            DocCode = "HD1",
            Open = 5_000m,
        });

        var branchItems = await OpenItemsAsync(branchB, $"partnerId={customer}&side=Receivable");
        branchItems.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            SourceType = DebtSourceType.StockOut,
            SourceId = otherSale,
            Open = 2_000m,
        });

        // Direction filter and the other side.
        (await OpenItemsAsync(main, $"partnerId={customer}&side=Receivable&direction=Decrease")).Should().BeEmpty();
        (await OpenItemsAsync(main, $"partnerId={customer}&side=Payable")).Should().BeEmpty();

        // PartnerId is required.
        var (missingStatus, _, missingError) = await ReadDocumentResponseAsync<object>(
            await main.GetAsync("/api/debt/open-items?side=Receivable"));
        missingStatus.Should().Be(HttpStatusCode.BadRequest);
        missingError!.Details.Should().ContainKey("partnerId");

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task For_source_adds_back_own_allocations()
    {
        var (branchId, client) = await BranchClientAsync("OIS");
        var customer = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var hd1 = await CreateOpeningDebtAsync(client, customer, DebtSide.Receivable, OpeningDebtKind.Debt, "HD1",
            new DateOnly(2026, 8, 20), 3_000m);

        var request = CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"), await FundIdAsync("QTM", branchId),
            "2026-10-05 09:00", customer, 1_000m);
        request.Allocations = new List<AllocationRequest> { new() { EntryId = hd1.EntryId, Amount = 1_000m } };
        var receipt = await CreateCashAsync(client, request);

        var plain = await OpenItemsAsync(client, $"partnerId={customer}&side=Receivable&direction=Increase");
        plain.Should().ContainSingle().Which.Should().BeEquivalentTo(new { EntryId = hd1.EntryId, Settled = 1_000m, Open = 2_000m });

        var forSource = await OpenItemsAsync(client,
            $"partnerId={customer}&side=Receivable&forSourceType=Receipt&forSourceId={receipt.Id}");
        forSource.Should().ContainSingle("the receipt's own entry is left out").Which.Should().BeEquivalentTo(new
        {
            EntryId = hd1.EntryId,
            DocCode = "HD1",
            Settled = 0m,
            Open = 3_000m,
        });

        await AssertCashDebtInvariantsAsync();
    }

    [Fact]
    public async Task Permission_rules()
    {
        var (branchId, _) = await BranchClientAsync("OIP");
        var customer = await CreatePartnerAsync(NewCode("KH"), isCustomer: true, isSupplier: false);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var url = $"/api/debt/open-items?partnerId={customer}&side=Receivable";

        var warehouse = await CreateClientForRoleAsync("oi_wh_" + suffix, RoleCodes.Warehouse, branchId);
        (await warehouse.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var matching = await CreateClientWithPermissionsAsync("oi_match_" + suffix, branchId, Permissions.Debt.Matching);
        (await matching.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await matching.GetAsync("/api/debt/partners?keyword=x")).StatusCode.Should().Be(HttpStatusCode.OK);

        var payments = await CreateClientWithPermissionsAsync("oi_pay_" + suffix, branchId, Permissions.Payments.Create);
        (await payments.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await payments.GetAsync("/api/debt/partners?keyword=x")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var reports = await CreateClientWithPermissionsAsync("oi_rep_" + suffix, branchId, Permissions.Reports.Debt);
        (await reports.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await reports.GetAsync("/api/debt/partners?keyword=x")).StatusCode.Should().Be(HttpStatusCode.OK);

        await AssertCashDebtInvariantsAsync();
    }

    private async Task<(Guid BranchId, HttpClient Client)> BranchClientAsync(string prefix)
    {
        var branchId = await CreateBranchAsync(NewCode(prefix));
        await SetBranchOpeningDateAsync(branchId, Sep1);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return (branchId, client);
    }

    private async Task<Guid> PostSaleAsync(HttpClient client, Guid warehouseId, Guid customer, Guid product, string at,
        decimal amount, DateOnly dueDate)
    {
        var sale = VoucherRequest(StockDirection.Out, await ReasonIdAsync("XBH"), at, LineRequest(product, 1, amount));
        sale.WarehouseId = warehouseId;
        sale.PartnerId = customer;
        sale.PaidAmount = 0;
        sale.DueDate = dueDate;
        var (status, voucher, error) = await PostVoucherAsync(client, sale);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!.Id;
    }

    private static async Task<List<CustomerSearchItemDto>> SearchAsync(HttpClient client, string query) =>
        await ReadDataAsync<List<CustomerSearchItemDto>>(await client.GetAsync($"/api/debt/partners?{query}"));

    private static async Task<List<OpenItemDto>> OpenItemsAsync(HttpClient client, string query) =>
        await ReadDataAsync<List<OpenItemDto>>(await client.GetAsync($"/api/debt/open-items?{query}"));

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];
}
