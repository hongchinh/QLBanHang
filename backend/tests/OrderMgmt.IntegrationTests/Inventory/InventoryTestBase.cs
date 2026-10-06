using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Inventory.Common;
using OrderMgmt.Application.Inventory.Ledger;
using OrderMgmt.Application.Inventory.StockVouchers.Models;
using OrderMgmt.Application.Inventory.Warehouses.Models;
using OrderMgmt.Application.Organization.Branches.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Entities.Inventory;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using OrderMgmt.IntegrationTests.Quotations;

namespace OrderMgmt.IntegrationTests.Inventory;

public abstract class InventoryTestBase : QuotationTestBase
{
    protected static readonly Guid MainBranchId = BranchDefaults.MainBranchId;

    /// The seeded main warehouse KHO01.
    protected Guid DefaultWarehouseId { get; private set; }

    protected InventoryTestBase(PostgresFixture pg) : base(pg) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        DefaultWarehouseId = await InDbAsync(db => db.Warehouses.Where(w => w.Code == "KHO01").Select(w => w.Id).SingleAsync());
    }

    protected async Task InDbAsync(Func<AppDbContext, Task> work)
    {
        using var scope = _factory.Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = _factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// Inserts a custom role T_{username} with exactly these permissions and a user (password Pass@123),
    /// then returns a client logged in as that user.
    protected async Task<HttpClient> CreateClientWithPermissionsAsync(
        string username, Guid? defaultBranchId, params string[] permissionCodes)
    {
        await InDbAsync(async db =>
        {
            var hasher = _factory.Services.GetRequiredService<IPasswordHasher>();
            var permissions = await db.Permissions.Where(p => permissionCodes.Contains(p.Code)).ToListAsync();
            permissions.Should().HaveCount(permissionCodes.Distinct().Count(), "every requested permission code must exist");

            var role = new Role { Code = $"T_{username}".ToUpperInvariant(), Name = $"Test {username}", IsSystem = false };
            foreach (var p in permissions)
                role.RolePermissions.Add(new RolePermission { Role = role, PermissionId = p.Id });
            db.Roles.Add(role);

            db.Users.Add(new User
            {
                Username = username,
                Email = $"{username}@test.local",
                FullName = $"Test {username}",
                PasswordHash = hasher.Hash("Pass@123"),
                Status = UserStatus.Active,
                DefaultBranchId = defaultBranchId ?? MainBranchId,
                UserRoles = new List<UserRole> { new() { Role = role } },
            });
            await db.SaveChangesAsync();
        });

        return await LoginAsync(username, "Pass@123");
    }

    protected async Task<HttpClient> CreateClientForRoleAsync(string username, string roleCode, Guid? defaultBranchId = null)
    {
        await CreateTestUserAsync(username, "Pass@123", roleCode, defaultBranchId);
        return await LoginAsync(username, "Pass@123");
    }

    /// A new client carrying the admin token (no extra login: the login endpoint is rate-limited).
    protected HttpClient CloneAdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;
        return client;
    }

    protected static void UseBranch(HttpClient client, Guid branchId)
    {
        client.DefaultRequestHeaders.Remove("X-Branch-Id");
        client.DefaultRequestHeaders.Add("X-Branch-Id", branchId.ToString());
    }

    protected async Task<Guid> CreateBranchAsync(string code)
    {
        var response = await _client.PostAsJsonAsync("/api/branches", new CreateBranchRequest { Code = code, Name = $"Chi nhánh {code}" });
        return (await ReadDataAsync<BranchDto>(response)).Id;
    }

    protected async Task<Guid> CreateWarehouseAsync(Guid branchId, string code)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/warehouses")
        {
            Content = JsonContent.Create(new CreateWarehouseRequest { Code = code, Name = $"Kho {code}", IsActive = true }),
        };
        request.Headers.Add("X-Branch-Id", branchId.ToString());
        var response = await _client.SendAsync(request);
        return (await ReadDataAsync<WarehouseDto>(response)).Id;
    }

    /// Direct DB update of the singleton (bypasses the API and its recalculation).
    protected Task UpdateInventorySettingsAsync(Action<OrderMgmt.Domain.Entities.Inventory.InventorySettings> mutate) =>
        InDbAsync(async db =>
        {
            var settings = await db.InventorySettings.SingleAsync(s => s.Id == 1);
            mutate(settings);
            await db.SaveChangesAsync();
        });

    /// "2026-10-02 08:00" read as Vietnam time, returned as the UTC instant (D27).
    protected static DateTimeOffset Vn(string local) =>
        new DateTimeOffset(DateTime.ParseExact(local, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), VnTime.Offset)
            .ToUniversalTime();

    protected Task<Guid> CreateInventoryProductAsync(string code, PricingMode mode = PricingMode.PerUnit,
        bool track = true, decimal? costPrice = null, decimal? defaultPrice = null, decimal taxRate = 0,
        bool priceIncludesVat = false) =>
        InDbAsync(async db =>
        {
            var product = new Product
            {
                Code = code,
                Name = $"Hàng {code}",
                ProductGroupId = await db.ProductGroups.Where(g => g.Code == "EPS").Select(g => g.Id).SingleAsync(),
                UnitId = _unitId,
                PricingMode = mode,
                TrackInventory = track,
                CostPrice = costPrice,
                DefaultPrice = defaultPrice,
                DefaultTaxRate = taxRate,
                PriceIncludesVat = priceIncludesVat,
                Status = ProductStatus.Active,
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            return product.Id;
        });

    /// Engine invariants (review m17). Call at the end of every engine, voucher, opening-stock and recalculation scenario.
    protected Task AssertInvariantsAsync() =>
        InDbAsync(async db =>
        {
            // Posting order comes from the database so it matches the engine's ordering.
            var rows = await db.InventoryLedger.AsNoTracking().InPostingOrder().ToListAsync();
            var balances = await db.StockBalances.AsNoTracking().ToListAsync();

            var pairs = rows.GroupBy(r => (r.ProductId, r.WarehouseId)).ToList();
            balances.Select(b => (b.ProductId, b.WarehouseId)).Should().BeEquivalentTo(pairs.Select(p => p.Key),
                "every pair with ledger rows has a balance and no other pair has one");
            foreach (var pair in pairs)
            {
                var running = 0m;
                foreach (var row in pair)
                {
                    running += row.QtyIn - row.QtyOut;
                    row.RunningQty.Should().Be(running, $"RunningQty of {row.SourceCode} at {row.PostedAt:u}");
                }
                balances.Single(b => (b.ProductId, b.WarehouseId) == pair.Key).Quantity.Should().Be(running);
            }

            var branchScope = (await db.InventorySettings.AsNoTracking().SingleAsync()).CostingScope == CostingScope.Branch;
            var periods = (await db.InventoryCostPeriods.AsNoTracking().ToListAsync())
                .Where(p => (p.ScopeKey == p.BranchId) == branchScope)
                .OrderBy(p => p.PeriodStart)
                .GroupBy(p => (p.ProductId, p.ScopeKey));
            foreach (var group in periods)
            {
                var inScope = rows.Where(r => r.ProductId == group.Key.ProductId
                    && (branchScope ? r.BranchId : r.WarehouseId) == group.Key.ScopeKey).ToList();
                InventoryCostPeriod? previous = null;
                foreach (var period in group)
                {
                    if (previous is not null)
                    {
                        period.OpeningQty.Should().Be(previous.ClosingQty, $"opening qty of {period.PeriodStart}");
                        period.OpeningValue.Should().Be(previous.ClosingValue, $"opening value of {period.PeriodStart}");
                    }

                    var end = VnTime.StartOfNextDay(period.PeriodEnd);
                    var upToEnd = inScope.Where(r => r.PostedAt < end).ToList();
                    period.ClosingQty.Should().Be(upToEnd.Sum(r => r.QtyIn - r.QtyOut), $"closing qty of {period.PeriodStart}");
                    period.ClosingValue.Should().Be(upToEnd.Sum(r => r.InValue - (r.CostAmount ?? 0)), $"closing value of {period.PeriodStart}");
                    period.OutValue.Should().Be(upToEnd
                        .Where(r => r.QtyOut > 0 && r.PostedAt >= VnTime.StartOfDay(period.PeriodStart))
                        .Sum(r => r.CostAmount ?? 0), $"out value of {period.PeriodStart}");
                    previous = period;
                }
            }
        });

    protected Task<Guid> ReasonIdAsync(string code) =>
        InDbAsync(db => db.StockReasons.Where(r => r.Code == code).Select(r => r.Id).SingleAsync());

    /// A voucher on KHO01 with the given lines; VatRate, discounts, freight and order discount default to 0.
    protected UpsertStockVoucherRequest VoucherRequest(StockDirection type, Guid reasonId, string at,
        params UpsertStockVoucherLineRequest[] lines) => new()
    {
        Type = type,
        VoucherAt = Vn(at),
        WarehouseId = DefaultWarehouseId,
        ReasonId = reasonId,
        Lines = lines.Select((l, i) => { l.SortOrder = i; return l; }).ToList(),
    };

    protected static UpsertStockVoucherLineRequest LineRequest(Guid productId, decimal quantity, decimal unitPrice) =>
        new() { ProductId = productId, Quantity = quantity, UnitPrice = unitPrice };

    protected static async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, ApiError? Error)> PostVoucherAsync(
        HttpClient c, UpsertStockVoucherRequest r) =>
        await ReadVoucherResponseAsync(await c.PostAsJsonAsync("/api/stock-vouchers", r));

    protected static async Task<(HttpStatusCode Status, StockVoucherDto? Voucher, ApiError? Error)> ReadVoucherResponseAsync(
        HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var parsed = string.IsNullOrEmpty(body)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<ApiResponse<StockVoucherDto>>(body, TestJson.Options);
        return (response.StatusCode, parsed?.Data, parsed?.Error);
    }

    protected Task<Guid> CreatePartnerAsync(string code, bool isCustomer, bool isSupplier) =>
        InDbAsync(async db =>
        {
            var partner = new OrderMgmt.Domain.Entities.Catalog.Customer
            {
                Code = code,
                Name = $"Đối tượng {code}",
                TaxCode = "0100000000",
                CompanyAddress = "Hà Nội",
                IsCustomer = isCustomer,
                IsSupplier = isSupplier,
            };
            db.Customers.Add(partner);
            await db.SaveChangesAsync();
            return partner.Id;
        });

    protected static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"expected success but got {(int)response.StatusCode}: {body}");
        var parsed = System.Text.Json.JsonSerializer.Deserialize<ApiResponse<T>>(body, TestJson.Options);
        return parsed!.Data!;
    }

    protected async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
        return client;
    }
}
