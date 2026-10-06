using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderMgmt.Application.Common.Models;
using OrderMgmt.Application.Identity.Interfaces;
using OrderMgmt.Application.Identity.Models;
using OrderMgmt.Application.Sales.Quotations.Models;
using OrderMgmt.Domain.Constants;
using OrderMgmt.Domain.Entities.Catalog;
using OrderMgmt.Domain.Entities.Identity;
using OrderMgmt.Domain.Enums;
using OrderMgmt.Infrastructure.Persistence;
using OrderMgmt.IntegrationTests.Fixtures;
using Xunit;

namespace OrderMgmt.IntegrationTests.Quotations;

public abstract class QuotationTestBase : IAsyncLifetime
{
    protected readonly PostgresFixture _pg;
    protected WebAppFactory _factory = default!;
    protected HttpClient _client = default!;
    protected Guid _customerId;
    protected Guid _productId;
    protected Guid _unitId;

    protected QuotationTestBase(PostgresFixture pg) => _pg = pg;

    public virtual async Task InitializeAsync()
    {
        _factory = new WebAppFactory(_pg);
        await ((IAsyncLifetime)_factory).InitializeAsync();
        _client = _factory.CreateClient();
        await AuthenticateAsync("admin", "Admin@123");
        await SeedReferenceDataAsync();
    }

    public virtual async Task DisposeAsync()
    {
        _client.Dispose();
        await ((IAsyncLifetime)_factory).DisposeAsync();
    }

    protected async Task AuthenticateAsync(string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>(TestJson.Options);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body!.Data!.AccessToken);
    }

    protected async Task SeedReferenceDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var unit = await db.Units.FirstAsync(u => u.Code == "TAM");
        _unitId = unit.Id;

        var customer = new Customer
        {
            Code = "KH-TEST-001",
            Name = "Test Customer",
            TaxCode = "0123456789",
            CompanyAddress = "123 Test Street, Hanoi",
            ContactPerson = "Nguyen Test",
            PhoneNumber = "0900000001",
            DefaultShippingAddress = "Warehouse A, Hanoi",
            Status = CustomerStatus.Active,
        };
        db.Customers.Add(customer);

        var groupId = (await db.ProductGroups.FirstAsync(g => g.Code == "EPS")).Id;
        var product = new Product
        {
            Code = "HH-TEST-001",
            Name = "Test EPS 1000x2000",
            ProductGroupId = groupId,
            UnitId = _unitId,
            DefaultPrice = 100_000,
            CostPrice = 60_000,
            Status = ProductStatus.Active,
            PricingMode = PricingMode.PerUnit,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        _customerId = customer.Id;
        _productId = product.Id;
    }

    protected async Task CreateTestUserAsync(string username, string password, string roleCode, Guid? defaultBranchId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var role = await db.Roles.FirstAsync(r => r.Code == roleCode);
        var user = new User
        {
            Username = username,
            Email = $"{username}@test.local",
            FullName = $"Test {roleCode}",
            PasswordHash = hasher.Hash(password),
            Status = UserStatus.Active,
            DefaultBranchId = defaultBranchId ?? BranchDefaults.MainBranchId,
            UserRoles = new List<UserRole> { new() { RoleId = role.Id } },
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    protected UpsertQuotationRequest BuildRequest(params UpsertQuotationLineRequest[] lines)
    {
        return new UpsertQuotationRequest
        {
            CustomerId = _customerId,
            QuotationDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TaxRate = 8,
            Discount = 0,
            Freight = 0,
            Lines = lines.Length > 0 ? lines : new[]
            {
                new UpsertQuotationLineRequest
                {
                    SortOrder = 0,
                    ProductId = _productId,
                    ProductName = "Test EPS 1000x2000",
                    UnitName = "Tấm",
                    PricingMode = PricingMode.PerUnit,
                    Quantity = 5,
                    UnitPrice = 12_000,
                },
            },
        };
    }
}
