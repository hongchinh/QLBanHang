# Phase 01 — Shared sales revenue source

**Status:** [x] done
**Complexity:** M

## Objective

Move the "counted sales vouchers" rule and the revenue expressions out of `SalesMetricsService` into one shared class, `SalesRevenueSource`. Executive figures must not change. The new class also takes an optional owner filter, which Phase 02 needs.

## Files

- Create: `backend/src/OrderMgmt.Application/Dashboards/Common/SalesRevenueSource.cs`
- Modify: `backend/src/OrderMgmt.Application/Dashboards/Executive/Services/SalesMetricsService.cs`
- Modify: `backend/src/OrderMgmt.Application/DependencyInjection.cs` (register `SalesRevenueSource` as scoped)
- Create: `backend/tests/OrderMgmt.IntegrationTests/Dashboard/SalesRevenueSourceTests.cs`

## Target API

```csharp
namespace OrderMgmt.Application.Dashboards.Common;

/// The counted sales vouchers of the dashboards: Active, not deleted, system reason XBH (sale, positive) or
/// NTL (sale return, negative), VoucherAt in [from, to). Revenue is before VAT; freight is not revenue.
public sealed class SalesRevenueSource
{
    public SalesRevenueSource(IAppDbContext db);

    /// `ownerUserId` null = every owner.
    public Task<CountedSales> BuildAsync(IReadOnlyCollection<Guid> branchIds, Guid? ownerUserId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}

/// The query of counted vouchers and the ids of the NTL reason (vouchers that count negative).
public sealed record CountedSales(IQueryable<StockVoucher> Vouchers, IReadOnlyList<Guid> ReturnIds)
{
    /// Signed revenue of each voucher: Id, VoucherAt, BranchId, PartnerId, OwnerUserId, IsReturn, Revenue.
    public IQueryable<SalesVoucherRevenue> VoucherRevenues();

    /// Signed revenue and quantity of each line: VoucherId, VoucherAt, ProductId, Quantity, Revenue.
    public IQueryable<SalesLineRevenue> LineRevenues(IQueryable<StockVoucherLine> lines);
}

public sealed record SalesVoucherRevenue(Guid Id, DateTimeOffset VoucherAt, Guid BranchId, Guid? PartnerId,
    Guid OwnerUserId, bool IsReturn, decimal Revenue);
public sealed record SalesLineRevenue(Guid VoucherId, DateTimeOffset VoucherAt, Guid ProductId, decimal Quantity,
    decimal Revenue);
```

`VoucherRevenues().Revenue` is the **signed** amount. `SalesMetricsService` keeps its own `VoucherRow` with cost; it reads `Revenue` already signed and applies the sign only to cost.

The revenue formula is copied exactly from `SalesMetricsService.LoadVouchersAsync` and `TotalsByProductAsync`: `l.Amount - l.DiscountAmount - l.OrderDiscountAllocated - (v.Type == StockDirection.Out && l.PriceIncludesVat ? l.VatAmount : 0m)`. The NTL reason id list comes from its own query with `IgnoreQueryFilters()` on `StockReasons`, as today.

## Tasks

### Task 1.1 — SalesRevenueSource with owner filter

1. Write the failing test `Dashboard/SalesRevenueSourceTests.cs`. It derives from `ExecutiveDashboard.ExecutiveDashboardTestBase` and uses `[Collection(nameof(PostgresCollection))]`. Cases:
   - `Counts_sales_positive_and_returns_negative`: `CreateFreshBranchAsync`, a tracked product, NMH 10 × 100 on 10-02, XBH 4 × 150 on 10-04, NTL 1 × 150 on 10-05. Resolve `SalesRevenueSource` from `_factory.Services.CreateScope()`. `BuildAsync([branch], null, 10-01 VN, 11-01 VN)`, then `VoucherRevenues().SumAsync(r => r.Revenue)` == 450.
   - `Owner_filter_keeps_only_that_owner`: the same branch plus one XBH owned by a second user (set `OwnerUserId` in `configure`). `BuildAsync(..., ownerUserId: second)` sums only that voucher.
   - `Other_branch_and_cancelled_are_left_out`: an XBH in a second fresh branch, and an XBH cancelled through `POST /api/stock-vouchers/{id}/cancel`. Neither counts.
   - `LineRevenues_sign_quantity_of_returns`: product quantity sum == 3 (4 − 1).
2. Run it and confirm the build fails because `SalesRevenueSource` does not exist:

   ```powershell
   $env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest_dashrev;Username=postgres;Password=1"
   dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SalesRevenueSourceTests"
   ```

   Expected: FAIL, compile error CS0246 `SalesRevenueSource`.
3. Write `SalesRevenueSource.cs` with the API above and register it in `DependencyInjection.cs` with `services.AddScoped<SalesRevenueSource>();`.
4. Run the same command. Expected: PASS (4 tests).
5. Commit: `git commit -m "feat(dashboard): shared sales revenue source with owner filter"`

### Task 1.2 — SalesMetricsService uses the shared source

1. Safety net: no new test, because behavior must not change. First run the executive sales tests and record that they are green:
   `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard.Sales|FullyQualifiedName~BranchComparisonTests|FullyQualifiedName~AttentionTests"`. Expected: PASS.
2. Refactor `SalesMetricsService`:
   - Replace `BuildCountedQueryAsync` with `_source.BuildAsync(scope.BranchIds, null, from, to, ct)`.
   - Replace the private `Counted` record with `CountedSales`.
   - In `LoadVouchersAsync`, read `counted.VoucherRevenues()` and keep the ledger cost query.
   - In `TotalsByProductAsync`, use `counted.LineRevenues(_db.StockVoucherLines)` grouped by `ProductId`.
   - Inject `SalesRevenueSource` through the constructor.
3. Run the same filter. Expected: PASS with the same count as step 1.
4. Run the whole executive folder: `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard"`. Expected: PASS.
5. Commit: `git commit -m "refactor(dashboard): executive sales read the shared revenue source"`

## Verification

- `dotnet build backend/OrderMgmt.sln` with no new warnings.
- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~SalesRevenueSourceTests"` passes.

## Exit Criteria

- `SalesMetricsService` has no XBH/NTL query or revenue formula of its own.
- All executive tests are green, and the 4 new source tests are green.
