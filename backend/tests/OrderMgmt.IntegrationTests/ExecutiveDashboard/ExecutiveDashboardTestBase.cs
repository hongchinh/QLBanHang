using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OrderMgmt.Application.CashDebt.CreditLimits.Models;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Dashboards.Executive.Models;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Domain.Enums;
using OrderMgmt.IntegrationTests.CashDebt;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.ExecutiveDashboard;

/// Helpers of the executive dashboard tests. A query string never repeats a key: a case that needs another `at`
/// or other dates is written in full, not appended to `October`.
public abstract class ExecutiveDashboardTestBase : CashDebtTestBase
{
    protected const string OctoberRange = "from=2026-10-01&to=2026-10-31";
    protected const string October = OctoberRange + "&at=2026-10-31";

    protected ExecutiveDashboardTestBase(PostgresFixture pg) : base(pg) { }

    /// A day of 2026, the year of the report dataset.
    protected static DateOnly D(int month, int day) => new(2026, month, day);

    /// An admin client whose working branch is the dataset's main branch.
    protected HttpClient MainClient(ReportIds ids)
    {
        var client = CloneAdminClient();
        UseBranch(client, ids.MainBranchId);
        return client;
    }

    protected Task<string> BranchCodeAsync(Guid branchId) =>
        InDbAsync(db => db.Branches.Where(b => b.Id == branchId).Select(b => b.Code).SingleAsync());

    /// An unpaid purchase (NMH) of `amount` from the supplier on 10-20 in the dataset's main branch with the given due
    /// date; one untracked product, quantity 1. `configure` may change the request before it is posted.
    protected async Task<StockVoucherDto> PostPurchaseAsync(ReportIds ids, Guid supplierId, decimal amount,
        DateOnly dueDate, Action<UpsertStockVoucherRequest>? configure = null)
    {
        var product = await CreateInventoryProductAsync("DUE-" + Guid.NewGuid().ToString("N")[..8], track: false);
        var request = VoucherRequest(StockDirection.In, await ReasonIdAsync("NMH"), "2026-10-20 09:00",
            LineRequest(product, 1, amount));
        request.WarehouseId = ids.MainWarehouseId;
        request.PartnerId = supplierId;
        request.DueDate = dueDate;
        configure?.Invoke(request);
        var (status, voucher, error) = await PostVoucherAsync(MainClient(ids), request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// Sets the credit limit of the customer in the working branch of the client.
    protected static async Task SetLimitAsync(HttpClient client, Guid customerId, decimal amount)
    {
        var response = await client.PutAsJsonAsync($"/api/credit-limits/{customerId}",
            new UpsertCreditLimitRequest { Amount = amount });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    /// GET /api/dashboard/executive/{pathAndQuery}, success body read as T.
    protected static async Task<T> GetExecAsync<T>(HttpClient client, string pathAndQuery) =>
        await ReadDataAsync<T>(await GetExecRawAsync(client, pathAndQuery));

    protected static Task<HttpResponseMessage> GetExecRawAsync(HttpClient client, string pathAndQuery) =>
        client.GetAsync($"/api/dashboard/executive/{pathAndQuery}");

    /// A fresh branch with a warehouse, a customer and a supplier; `Client` is an admin client working in it.
    protected sealed record FreshBranch(Guid BranchId, Guid WarehouseId, Guid CustomerId, Guid SupplierId, HttpClient Client);

    protected async Task<FreshBranch> CreateFreshBranchAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var branchId = await CreateBranchAsync("FB" + suffix);
        var warehouseId = await CreateWarehouseAsync(branchId, "FB" + suffix);
        var customerId = await CreatePartnerAsync("FKH" + suffix, isCustomer: true, isSupplier: false);
        var supplierId = await CreatePartnerAsync("FNC" + suffix, isCustomer: false, isSupplier: true);
        var client = CloneAdminClient();
        UseBranch(client, branchId);
        return new FreshBranch(branchId, warehouseId, customerId, supplierId, client);
    }

    /// An unpaid one-line stock voucher of the branch (reason NMH takes a supplier, XBH and NTL a customer); asserts 200.
    /// `configure` may change the request before it is posted (discounts, VAT rate of the line).
    protected async Task<StockVoucherDto> PostLineAsync(FreshBranch branch, StockDirection type, string reasonCode,
        string at, Guid partnerId, Guid productId, decimal quantity, decimal unitPrice,
        Action<UpsertStockVoucherRequest>? configure = null)
    {
        var request = VoucherRequest(type, await ReasonIdAsync(reasonCode), at, LineRequest(productId, quantity, unitPrice));
        request.WarehouseId = branch.WarehouseId;
        request.PartnerId = partnerId;
        configure?.Invoke(request);
        var (status, voucher, error) = await PostVoucherAsync(branch.Client, request);
        status.Should().Be(HttpStatusCode.OK, error?.Message);
        return voucher!;
    }

    /// A fresh branch where a tracked product (VAT 0) is bought 10 × 100 on 10-02 and sold 4 × 150 on 10-04:
    /// revenue 600, cost 400, gross profit 200.
    protected async Task<(FreshBranch Branch, Guid ProductId, StockVoucherDto Sale)> SeedTrackedSaleAsync()
    {
        var branch = await CreateFreshBranchAsync();
        var product = await CreateInventoryProductAsync("COST-01");
        await PostLineAsync(branch, StockDirection.In, "NMH", "2026-10-02 09:00", branch.SupplierId, product, 10, 100);
        var sale = await PostLineAsync(branch, StockDirection.Out, "XBH", "2026-10-04 09:00", branch.CustomerId,
            product, 4, 150);
        return (branch, product, sale);
    }

    protected static Task<SalesSummaryDto> GetSummaryAsync(HttpClient client, string query) =>
        GetExecAsync<SalesSummaryDto>(client, $"sales/summary?{query}");

    /// Asserts a 400 whose validation details name `key` and, when given, carry `message` for it. A 400 without the
    /// app's error envelope (the framework's own model-binding answer, for instance) fails with the response body.
    protected static async Task AssertBadRequestKeyAsync(
        HttpClient client, string pathAndQuery, string key, string? message = null)
    {
        var response = await GetExecRawAsync(client, pathAndQuery);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, pathAndQuery);
        var details = ErrorDetailsOf(body);
        if (details is null)
            Assert.Fail($"{pathAndQuery} answered 400 without error details in the app's envelope. Body: {body}");
        details.Should().ContainKey(key, pathAndQuery);
        if (message is not null)
            details[key].Should().Contain(message, pathAndQuery);
    }

    /// The `error.details` of an app response body; null when the body is not an app envelope or has no details.
    private static IDictionary<string, string[]>? ErrorDetailsOf(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ApiResponse>(body, TestJson.Options)?.Error?.Details;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
