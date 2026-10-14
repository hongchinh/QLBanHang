using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Inventory.OpeningStocks.Models;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Sales;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.SampleData;

[Collection(nameof(PostgresCollection))]
public class DemoBranchCatalogCleanupTests : DemoBranchFixture
{
    private const string SampleGroupName = "Dữ liệu mẫu";

    public DemoBranchCatalogCleanupTests(PostgresFixture pg) : base(pg) { }

    private sealed record Catalog(Guid SpA, Guid SpB, Guid SpC, Guid KhA, Guid KhB, Guid KhC, Guid KhD, Guid KhE);

    private Task CreateGroupAsync(string name) =>
        InDbAsync(async db =>
        {
            db.ProductGroups.Add(new ProductGroup { Code = "DM", Name = name });
            await db.SaveChangesAsync();
        });

    private Task<Guid> CreateSampleProductAsync(string code) =>
        InDbAsync(async db =>
        {
            var product = new Product
            {
                Code = code,
                Name = $"Hàng {code}",
                IsSample = true,
                ProductGroupId = await db.ProductGroups.Where(g => g.Code == "DM").Select(g => g.Id).SingleAsync(),
                TrackInventory = true,
                Status = ProductStatus.Active,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product.Id;
        });

    private Task<Guid> CreateSamplePartnerAsync(string code, bool withAddress) =>
        InDbAsync(async db =>
        {
            var partner = new Customer
            {
                Code = code, Name = $"Khách {code}", IsSample = true, IsCustomer = true, IsSupplier = false,
            };
            if (withAddress)
                partner.Addresses.Add(new CustomerAddress { Address = "1 Phố Mẫu", IsDefault = true });
            db.Customers.Add(partner);
            await db.SaveChangesAsync();
            return partner.Id;
        });

    /// A sale of `productId` to the customer, after an opening stock of the product in the demo branch.
    private async Task SellInDemoBranchAsync(DemoBranch branch, Guid customerId, Guid productId)
    {
        var opening = await branch.Client.PutAsJsonAsync("/api/inventory/opening-stock", new SaveOpeningStockRequest
        {
            WarehouseId = branch.WarehouseId,
            Lines = { new SaveOpeningStockLine { ProductId = productId, Quantity = 10, Amount = 10_000m } },
        });
        opening.StatusCode.Should().Be(HttpStatusCode.OK, await opening.Content.ReadAsStringAsync());
        await PostCreditSaleAsync(branch, customerId, productId, 2_000m);
    }

    /// The sample catalog with two demo branches DK01 and DK02 selling DM-SPA to DM-KHA, and the main branch using
    /// DM-SPB (a stock-in) and DM-KHB (an opening debt).
    private async Task<(Catalog Catalog, DemoBranch Dk01, DemoBranch Dk02)> SetUpAsync()
    {
        await CreateGroupAsync(SampleGroupName);
        var catalog = new Catalog(await CreateSampleProductAsync("DM-SPA"), await CreateSampleProductAsync("DM-SPB"),
            await CreateSampleProductAsync("DM-SPC"), await CreateSamplePartnerAsync("DM-KHA", withAddress: true),
            await CreateSamplePartnerAsync("DM-KHB", withAddress: false), await CreateSamplePartnerAsync("DM-KHC", withAddress: false),
            await CreateSamplePartnerAsync("DM-KHD", withAddress: false), await CreateSamplePartnerAsync("DM-KHE", withAddress: false));
        var dk01 = await CreateDemoBranchAsync("DK01");
        var dk02 = await CreateDemoBranchAsync("DK02");
        await SellInDemoBranchAsync(dk01, catalog.KhA, catalog.SpA);
        await SellInDemoBranchAsync(dk02, catalog.KhA, catalog.SpA);

        await SetBranchOpeningDateAsync(MainBranchId, DemoOpeningDate);
        var main = new DemoBranch(MainBranchId, DefaultWarehouseId, await CashFundIdAsync(MainBranchId), _client);
        await PostStockLineAsync(main, StockDirection.In, "NMH", "2026-10-03 09:00",
            await CreatePartnerAsync("SUP-REAL", isCustomer: false, isSupplier: true), catalog.SpB, 1, 1_000m);
        await CreateOpeningDebtAsync(_client, catalog.KhB, DebtSide.Receivable, OpeningDebtKind.Debt, "HD-KHB",
            new DateOnly(2026, 9, 5), 5_000m);
        // Main-branch rows that keep sample rows through the other reference lists: a quotation of DM-KHC with a DM-SPC
        // line, a credit limit of DM-KHD and a cash voucher of DM-KHE.
        await InDbAsync(async db =>
        {
            var quotation = new Quotation
            {
                Code = "BG-DM-KEEP", QuotationDate = DemoOpeningDate, CustomerId = catalog.KhC, CustomerName = "Khách DM-KHC",
                OwnerUserId = await db.Users.IgnoreQueryFilters().Select(u => u.Id).FirstAsync(),
            };
            quotation.Lines.Add(new QuotationLine
            {
                ProductId = catalog.SpC, ProductName = "Hàng DM-SPC", UnitName = "Cái", Quantity = 1, UnitPrice = 100m,
            });
            db.Quotations.Add(quotation);
            await db.SaveChangesAsync();
        });
        (await _client.PutAsJsonAsync($"/api/credit-limits/{catalog.KhD}", new UpsertCreditLimitRequest { Amount = 1_000m }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await CreateCashAsync(_client, CashRequest(CashVoucherType.Receipt, await CashReasonIdAsync("TKH"),
            main.CashFundId, "2026-10-04 09:00", catalog.KhE, 500m));
        // The receipt also wrote a debt entry, which alone would keep DM-KHE: remove it so only the voucher names the partner.
        await InDbAsync(db => db.DebtEntries.IgnoreQueryFilters().Where(e => e.PartnerId == catalog.KhE).ExecuteDeleteAsync());
        return (catalog, dk01, dk02);
    }


    private Task<int> ProductCountAsync(params Guid[] ids) =>
        InDbAsync(db => db.Products.IgnoreQueryFilters().CountAsync(p => ids.Contains(p.Id)));

    private Task<int> PartnerCountAsync(params Guid[] ids) =>
        InDbAsync(db => db.Customers.IgnoreQueryFilters().CountAsync(p => ids.Contains(p.Id)));

    private Task<int> AddressCountAsync(Guid partnerId) =>
        InDbAsync(db => db.CustomerAddresses.IgnoreQueryFilters().CountAsync(a => a.CustomerId == partnerId));

    private Task<int> GroupCountAsync(string name) =>
        InDbAsync(db => db.ProductGroups.IgnoreQueryFilters().CountAsync(g => g.Code == "DM" && g.Name == name));

    [Fact]
    public async Task First_delete_keeps_the_catalog_while_a_demo_branch_remains()
    {
        var (c, dk01, _) = await SetUpAsync();

        var result = await DeleteDemoBranchAsync(dk01.BranchId);

        (await ProductCountAsync(c.SpA, c.SpB, c.SpC)).Should().Be(3);
        (await PartnerCountAsync(c.KhA, c.KhB)).Should().Be(2);
        (await AddressCountAsync(c.KhA)).Should().Be(1);
        (await GroupCountAsync(SampleGroupName)).Should().Be(1);
        result.KeptSampleProductCount.Should().Be(0);
        result.KeptSamplePartnerCount.Should().Be(0);
    }

    [Fact]
    public async Task Last_delete_removes_unused_sample_rows()
    {
        var (c, dk01, dk02) = await SetUpAsync();

        await DeleteDemoBranchAsync(dk01.BranchId);
        var result = await DeleteDemoBranchAsync(dk02.BranchId);

        (await ProductCountAsync(c.SpA)).Should().Be(0);
        (await ProductCountAsync(c.SpB)).Should().Be(1);
        (await ProductCountAsync(c.SpC)).Should().Be(1, "a main-branch quotation line uses it");
        (await PartnerCountAsync(c.KhA)).Should().Be(0);
        (await AddressCountAsync(c.KhA)).Should().Be(0);
        (await PartnerCountAsync(c.KhB)).Should().Be(1);
        (await PartnerCountAsync(c.KhC)).Should().Be(1, "a main-branch quotation names it");
        (await PartnerCountAsync(c.KhD)).Should().Be(1, "a main-branch credit limit names it");
        (await PartnerCountAsync(c.KhE)).Should().Be(1, "a main-branch cash voucher names it");
        (await GroupCountAsync(SampleGroupName)).Should().Be(1, "DM-SPB and DM-SPC still hold it");
        result.KeptSampleProductCount.Should().Be(2);
        result.KeptSamplePartnerCount.Should().Be(4);
    }

    [Fact]
    public async Task Group_goes_when_no_product_is_left()
    {
        await CreateGroupAsync(SampleGroupName);
        var spA = await CreateSampleProductAsync("DM-SPA");
        var khA = await CreateSamplePartnerAsync("DM-KHA", withAddress: false);
        var dk01 = await CreateDemoBranchAsync("DK01");
        await SellInDemoBranchAsync(dk01, khA, spA);

        var result = await DeleteDemoBranchAsync(dk01.BranchId);

        (await ProductCountAsync(spA)).Should().Be(0);
        (await GroupCountAsync(SampleGroupName)).Should().Be(0);
        result.KeptSampleProductCount.Should().Be(0);
    }

    [Fact]
    public async Task A_real_DM_group_is_never_deleted()
    {
        await CreateGroupAsync("Nhóm thật");
        var dk01 = await CreateDemoBranchAsync("DK01");

        await DeleteDemoBranchAsync(dk01.BranchId);

        (await GroupCountAsync("Nhóm thật")).Should().Be(1);
    }
}
