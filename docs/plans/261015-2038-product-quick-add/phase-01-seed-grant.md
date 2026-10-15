# Phase 01 — Seed grant

**Status:** [ ] pending
**Complexity:** S

## Objective

The seeded Warehouse, Accountant and Sales roles hold `products.create`, so their users see product quick add (brainstorm D2).

## Files

- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (role definitions near lines 192–240)
- `backend/tests/OrderMgmt.IntegrationTests/Catalog/ProductQuickAddSeedTests.cs` (new; the `Catalog/` folder already exists)

## Tasks

### Task 1.1 — Grant `products.create` to three seed roles

1. **Write the failing test.** Create `ProductQuickAddSeedTests.cs`, modelled on `CashDebt/PartnerQuickAddSeedTests.cs`. Use the same base class (`CashDebtTestBase`, which gives `InDbAsync`), the same `[Collection(nameof(PostgresCollection))]` and the same `RoleCodesAsync` helper:

   ```csharp
   using FluentAssertions;
   using Microsoft.EntityFrameworkCore;
   using OrderMgmt.Domain.Constants;
   using OrderMgmt.IntegrationTests.CashDebt;   // CashDebtTestBase
   using OrderMgmt.IntegrationTests.Fixtures;
   using Xunit;

   namespace OrderMgmt.IntegrationTests.Catalog;

   /// Product quick add from the product code cell (brainstorm 261015-2033, D2): which seed roles may create products.
   [Collection(nameof(PostgresCollection))]
   public class ProductQuickAddSeedTests : CashDebtTestBase
   {
       public ProductQuickAddSeedTests(PostgresFixture pg) : base(pg) { }

       private Task<HashSet<string>> RoleCodesAsync(string roleCode) => InDbAsync(async db =>
           (await db.Roles.AsNoTracking()
               .Where(r => r.Code == roleCode)
               .SelectMany(r => r.RolePermissions.Select(rp => rp.Permission.Code))
               .ToListAsync()).ToHashSet());

       [Theory]
       [InlineData(RoleCodes.Warehouse)]
       [InlineData(RoleCodes.Accountant)]
       [InlineData(RoleCodes.Sales)]
       public async Task Role_can_quick_add_products(string roleCode)
       {
           var codes = await RoleCodesAsync(roleCode);
           codes.Should().Contain(new[] { Permissions.Products.View, Permissions.Products.Create });
       }

       [Theory]
       [InlineData(RoleCodes.Warehouse)]
       [InlineData(RoleCodes.Accountant)]
       [InlineData(RoleCodes.Sales)]
       public async Task Role_does_not_get_product_update_or_delete(string roleCode)
       {
           var codes = await RoleCodesAsync(roleCode);
           codes.Should().NotContain(Permissions.Products.Update);
           codes.Should().NotContain(Permissions.Products.Delete);
       }
   }
   ```

   `RoleCodes` members are `const`, so `[InlineData]` compiles. The fixture recreates and seeds the template database on every run, so new seed defaults reach the test.
2. **Run it and check it fails.**
   ```bash
   cd backend && export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
   dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ProductQuickAddSeedTests" --results-directory "$TEMP/pqa-trx"
   ```
   Expected: FAIL. `Role_can_quick_add_products` fails three times with "Expected codes … to contain … products.create". `Role_does_not_get_product_update_or_delete` passes.
3. **Write the minimal implementation.** In `DbSeeder.cs`, add `Permissions.Products.Create` to the arrays of `RoleCodes.Sales`, `RoleCodes.Accountant` and `RoleCodes.Warehouse`, next to `Permissions.Products.View`. Put one comment above the Sales entry, written in the style of the existing D12 comment:
   ```csharp
   // Product quick add from the product code cell (brainstorm 261015-2033, D2). Existing databases: an admin
   // enables products.create in the permission matrix; the seeder grants only new permission codes.
   ```
4. **Run the tests and check they pass.** Run the same command plus the neighbouring seed tests:
   ```bash
   dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ProductQuickAddSeedTests|FullyQualifiedName~PartnerQuickAddSeedTests|FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~AdminRolesCrudTests" --results-directory "$TEMP/pqa-trx"
   ```
   Expected: PASS. (No existing backend test asserts an exact Sales, Accountant or Warehouse permission set; checked 2026-10-15.)
5. **Commit.**
   ```bash
   git add backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs backend/tests/OrderMgmt.IntegrationTests
   git commit -m "feat(seed): grant products.create to warehouse, accountant and sales for product quick add"
   ```
   Do not stage `TestResults/*.trx`.

## Verification

- `cd backend && dotnet build` (no warnings added).
- The filtered `dotnet test` command in step 4 passes.

## Exit Criteria

- The three roles hold `products.create` on a freshly seeded database, and none of them holds `products.update` or `products.delete`.
- No other seed test changed except for the reason stated in step 4.
