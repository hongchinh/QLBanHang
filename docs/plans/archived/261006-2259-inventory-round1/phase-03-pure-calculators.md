# Phase 03 — Pure calculators

**Status:** [x] complete
**Complexity:** M

## Objective

Build the dependency-free calculation core with fast unit tests: pricing quantity (shared with quotations), Vietnam time and costing-period calendar helpers, the stock voucher line/total calculator, and the periodic weighted-average costing calculator. Later phases only orchestrate these; every formula lives here.

## Files

- `backend/src/OrderMgmt.Domain/Entities/Catalog/PricingQuantity.cs` (new)
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` (modify — `EffectiveQuantity` delegates)
- `backend/src/OrderMgmt.Application/Inventory/Common/VnTime.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Common/CostingPeriodCalendar.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherCalculator.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/Costing/PeriodicAverageCalculator.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/{PricingQuantityTests,VnTimeTests,CostingPeriodCalendarTests,StockVoucherCalculatorTests,PeriodicAverageCalculatorTests}.cs` (new)

## Rounding rules (apply everywhere in this phase)

- `R0(x)` = `Math.Round(x, 0, MidpointRounding.AwayFromZero)` for every money value.
- `R4(x)` = `Math.Round(x, 4, MidpointRounding.AwayFromZero)` for `AvgCost`.
- `R6(x)` = `Math.Round(x, 6, MidpointRounding.AwayFromZero)` for stock quantities.

## Tasks

### Task 3.1 — `PricingQuantity` shared with quotations

```csharp
namespace OrderMgmt.Domain.Entities.Catalog;

public static class PricingQuantity
{
    // Dimensions in mm. PerUnit returns `quantity` unchanged; missing dimensions count as 0.
    public static decimal Compute(PricingMode mode, decimal? sheetCount, decimal? length,
        decimal? width, decimal? thickness, decimal quantity);
}
```

1. **Write the failing test** `Inventory/Unit/PricingQuantityTests.cs`:
   - `PerUnit_returns_quantity` → (PerUnit, q = 7) = 7
   - `PerLinearMeter_is_sheets_times_length_over_1000` → (4 sheets, L 2500) = 10
   - `PerSquareMeter_is_sheets_times_length_times_width_over_1e6` → (2, 2000, 1000) = 4
   - `PerCubicMeter_is_sheets_times_dimensions_over_1e9` → (1, 1000, 1000, 500) = 0.5
   - `Missing_dimensions_yield_zero` → (PerSquareMeter, 2, 2000, null) = 0
2. **Run the test to verify it fails:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PricingQuantityTests"`. Expected: FAIL (compile: `PricingQuantity` missing).
3. **Write the minimal implementation:** `PricingQuantity.Compute`. Replace the body of `QuotationService.EffectiveQuantity` with `PricingQuantity.Compute(line.PricingMode, line.SheetCount, line.Length, line.Width, line.Thickness, line.Quantity)`.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~PricingQuantityTests|FullyQualifiedName~QuotationRecomputeTests"`. Expected: PASS.
5. **Commit:** `git commit -m "refactor(quotations): extract PricingQuantity shared with inventory"`

### Task 3.2 — `VnTime` and `CostingPeriodCalendar`

```csharp
namespace OrderMgmt.Application.Inventory.Common;

public static class VnTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(7);
    public static DateOnly ToVnDate(DateTimeOffset at);           // DateOnly.FromDateTime(at.ToOffset(Offset).DateTime)
    public static DateTimeOffset StartOfDay(DateOnly date);       // the UTC instant of 00:00 VN (offset 0, D27)
    public static DateTimeOffset StartOfNextDay(DateOnly date);   // exclusive upper bound of `date`, UTC
    public static DateTimeOffset ToUtc(DateTimeOffset at);        // at.ToUniversalTime() — normalize any input before EF
}

public readonly record struct CostingPeriodRange(DateOnly Start, DateOnly End);

public static class CostingPeriodCalendar
{
    public static CostingPeriodRange PeriodOf(DateOnly date, CostingPeriod period);
    public static CostingPeriodRange Next(CostingPeriodRange current, CostingPeriod period);
    public static IReadOnlyList<CostingPeriodRange> Range(DateOnly from, DateOnly to, CostingPeriod period); // periods covering [from, to]
    public static bool IsPeriodEnd(DateOnly date, CostingPeriod period);
    // PeriodOf(date), advanced while its End <= lockedUntil (fully locked periods are frozen).
    public static CostingPeriodRange FirstUnlockedFrom(DateOnly date, DateOnly? lockedUntil, CostingPeriod period);
}
```

1. **Write the failing tests:**
   - `VnTimeTests`:
     - `ToVnDate_crosses_midnight_at_17_utc` → 2026-10-06T17:30:00Z gives 2026-10-07
     - `StartOfDay_is_midnight_vn_as_utc_instant` → `StartOfDay(2026-10-07) == 2026-10-06T17:00:00Z` **and** `.Offset == TimeSpan.Zero` (Npgsql rejects non-zero offsets for `timestamptz`, D27)
     - `StartOfNextDay` → `StartOfDay(date + 1)`, offset 0
     - `ToUtc_keeps_the_instant_and_drops_the_offset` → `ToUtc(2026-10-02T08:00:00+07:00) == 2026-10-02T01:00:00Z`, offset 0
   - `CostingPeriodCalendarTests`:
     - `Month_period` → 2026-02-15 gives 2026-02-01..2026-02-28
     - `Month_period_in_leap_year` → 2024-02-10 gives ..2024-02-29
     - `Quarter_period` → 2026-11-30 gives 2026-10-01..2026-12-31
     - `Year_period` → 2026-01-01..2026-12-31
     - `Next_month_crosses_year` → Dec 2026 gives Jan 2027
     - `Range_lists_covering_periods` → (2026-01-15, 2026-03-02, Month) gives 3 periods
     - `IsPeriodEnd` → (2026-03-31, Quarter) true; (2026-03-30, Quarter) false
     - `FirstUnlockedFrom_skips_fully_locked_periods` → (2026-03-10, lock 2026-03-31, Month) gives April 2026
     - `FirstUnlockedFrom_keeps_partially_locked_period` → (2026-03-10, lock 2026-03-15, Month) gives March 2026
     - `FirstUnlockedFrom_without_lock_or_with_older_lock` → (2026-03-10, null) gives March; (2026-05-10, lock 2026-03-31) gives May
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~VnTimeTests|FullyQualifiedName~CostingPeriodCalendarTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** both static classes.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): Vietnam time and costing period calendar helpers"`

### Task 3.3 — `StockVoucherCalculator`

```csharp
namespace OrderMgmt.Application.Inventory.StockVouchers.Services;

public sealed record StockLineInput(
    bool TrackInventory, PricingMode PricingMode, bool PriceIncludesVat,
    decimal? SheetCount, decimal? Length, decimal? Width, decimal? Thickness, decimal Quantity,
    decimal UnitPrice, decimal DiscountRate, decimal? DiscountAmount, bool DiscountManual, decimal VatRate);

public sealed record StockHeaderInput(StockDirection Direction, decimal Freight, decimal OrderDiscount, bool NetExcludesVat);

public sealed record StockLineResult(
    decimal Quantity, decimal Amount, decimal DiscountAmount, decimal OrderDiscountAllocated,
    decimal VatAmount, decimal NetAmount, decimal FreightAllocated, decimal InboundValue);

public sealed record StockVoucherTotals(
    decimal GoodsAmount, decimal LineDiscountTotal, decimal DiscountTotal, decimal VatTotal, decimal Total);

public sealed record StockVoucherComputation(IReadOnlyList<StockLineResult> Lines, StockVoucherTotals Totals);

public static class StockVoucherCalculator
{
    // `lines` must already be in SortOrder.
    public static StockVoucherComputation Compute(StockHeaderInput header, IReadOnlyList<StockLineInput> lines);
}
```

Algorithm (section-03 §4 + D6, D9, D10). Callers guarantee `0 <= DiscountAmount <= Amount` for manual discounts and `0 <= OrderDiscount <= ΣNet` (validated in Phase 05 save rule 3), so `Net` and `Net′` are never negative:
1. `Quantity = R6(PricingQuantity.Compute(...))`; `Amount = R0(Quantity × UnitPrice)`.
2. `Discount = DiscountManual ? R0(DiscountAmount ?? 0) : R0(Amount × DiscountRate / 100)`; `Net = Amount − Discount`.
3. Order discount: if `ΣNet > 0`, `Alloc_i = R0(OrderDiscount × Net_i / ΣNet)` for every line except the last line with `Net > 0`, which gets `OrderDiscount − Σothers`. Otherwise every allocation is 0. `Net′ = Net − Alloc`.
4. VAT: if `Direction == Out && PriceIncludesVat` then `R0(Net′ − Net′ / (1 + VatRate / 100))`, else `R0(Net′ × VatRate / 100)`.
5. `NetAmount` (Số còn lại): `Out && PriceIncludesVat` → `Net′`; otherwise `NetExcludesVat ? Net′ : Net′ + VAT`.
6. Stock-in only: freight is allocated over tracked lines by `Net′`, with the remainder going to the last tracked line with `Net′ > 0` (0 when there are no tracked lines or `ΣNet′ = 0`). `InboundValue = Net′ + FreightAllocated` for tracked inbound lines, otherwise 0. VAT is **not** included (D6).
7. Totals: `GoodsAmount = ΣAmount`, `LineDiscountTotal = ΣDiscount`, `DiscountTotal = ΣDiscount + OrderDiscount`, `VatTotal = ΣVAT`, `Total = ΣNetAmount + Freight`.

1. **Write the failing test** `Inventory/Unit/StockVoucherCalculatorTests.cs` with these exact expectations:
   - `Out_example_allocates_order_discount_before_vat_and_handles_vat_inclusive_price`
     - Header: Out, Freight 20,000, OrderDiscount 30,000, NetExcludesVat false.
     - L1: PerUnit, qty 10, price 100,000, rate 10%, VAT 8%, not VAT-inclusive.
     - L2: PerSquareMeter, sheets 2, L 2000, W 1000, price 50,000, rate 0, VAT 10%, `PriceIncludesVat = true`.
     - Expected L1: Quantity 10, Amount 1,000,000, Discount 100,000, Alloc 24,545, VAT 70,036, NetAmount 945,491.
     - Expected L2: Quantity 4, Amount 200,000, Discount 0, Alloc 5,455, VAT 17,686, NetAmount 194,545.
     - Totals: Goods 1,200,000, LineDiscount 100,000, Discount 130,000, VAT 87,722, Total 1,160,036.
   - `In_example_allocates_freight_to_tracked_lines_only`
     - Header: In, Freight 200,000, OrderDiscount 100,000.
     - L1: tracked PerUnit, qty 100, price 50,000, VAT 10%.
     - L2: tracked PerCubicMeter, sheets 10, L 2000, W 1000, T 50, price 1,500,000, rate 5%, VAT 8%.
     - L3: **untracked** PerUnit, qty 1, price 300,000, VAT 8%.
     - Expected Net: 5,000,000 / 1,425,000 / 300,000. Alloc: 74,349 / 21,190 / 4,461. Net′: 4,925,651 / 1,403,810 / 295,539.
     - Expected VAT: 492,565 / 112,305 / 23,643. NetAmount: 5,418,216 / 1,516,115 / 319,182.
     - Expected FreightAllocated: 155,642 / 44,358 / 0. InboundValue: 5,081,293 / 1,448,168 / 0.
     - Totals: Goods 6,800,000, LineDiscount 75,000, Discount 175,000, VAT 628,513, Total 7,453,513.
   - `Manual_discount_amount_overrides_rate` → qty 3 × 33,333 = 99,999, manual 1,000 at rate 50% → Discount 1,000, Net 98,999
   - `Net_excludes_vat_keeps_vat_out_of_net_amount` → Out, qty 1 × 100,000, VAT 10%, NetExcludesVat → VAT 10,000, NetAmount 100,000
   - `Vat_inclusive_flag_is_ignored_on_stock_in` → In, 1 × 110,000, VAT 10%, flag true → VAT 11,000, NetAmount 121,000
   - `Order_discount_remainder_goes_to_last_line_with_positive_net` → Net 100,000 / 50,000 / 0 with OD 10,000 → Alloc 6,667 / 3,333 / 0
   - `Quantity_is_rounded_to_six_decimals_before_amount` → PerCubicMeter, 1 sheet 333×333×33, price 10,000,000 → Quantity 0.003659, Amount 36,590
   - `Freight_without_tracked_lines_is_not_allocated` → In, only untracked lines → every FreightAllocated 0; Total still includes freight
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherCalculatorTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** `StockVoucherCalculator.Compute` as specified.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock voucher line and total calculator"`

### Task 3.4 — `PeriodicAverageCalculator`

```csharp
namespace OrderMgmt.Application.Inventory.Costing;

public sealed record CostMovement(Guid EntryId, decimal QtyIn, decimal QtyOut, decimal InValue); // posting order
public sealed record CostPeriodInput(DateOnly Start, DateOnly End, IReadOnlyList<CostMovement> Movements);
public sealed record CostPeriodResult(
    DateOnly Start, DateOnly End,
    decimal OpeningQty, decimal OpeningValue, decimal InQty, decimal InValue,
    decimal OutQty, decimal OutValue, decimal AvgCost, decimal ClosingQty, decimal ClosingValue);
public sealed record OutCost(Guid EntryId, decimal UnitCost, decimal CostAmount);
public sealed record CostRunResult(IReadOnlyList<CostPeriodResult> Periods, IReadOnlyList<OutCost> OutCosts);

public static class PeriodicAverageCalculator
{
    public static CostRunResult Run(decimal openingQty, decimal openingValue, decimal fallbackAvgCost,
        IReadOnlyList<CostPeriodInput> periods);
}
```

Per period (D8, D9, D37):
- `denom = OpeningQty + InQty`; `numer = OpeningValue + InValue`.
- `AvgCost = denom > 0 && numer >= 0 ? R4(numer / denom) : fallback` (a negative numerator is only reachable after negative stock and would give a negative average, D37).
- Every outbound movement gets `UnitCost = AvgCost` and `CostAmount = R0(QtyOut × AvgCost)`; `OutValue = ΣCostAmount`.
- `ClosingQty = OpeningQty + InQty − OutQty`; `ClosingValue = OpeningValue + InValue − OutValue`.
- If `ClosingQty == 0` and the period has at least one outbound movement, add `ClosingValue` to the last outbound movement's `CostAmount` (and to `OutValue`), then set `ClosingValue = 0`.
- The next period opens with this closing, and `fallback = AvgCost`.

1. **Write the failing test** `Inventory/Unit/PeriodicAverageCalculatorTests.cs` (notation: `In q @ v` = quantity q with **line value** v):
   - `Single_month_average_cost`: opening 0/0, one period with In 100 @ 5,000,000, Out 30, In 50 @ 3,000,000, Out 40. Expected AvgCost 53,333.3333; out costs 1,600,000 and 2,133,333; OutValue 3,733,333; Closing 80 / 4,266,667.
   - `Zero_closing_pushes_rounding_residual_to_last_outbound`: In 3 @ 100,000; Out 1, Out 1, Out 1 → costs 33,333 / 33,333 / 33,334; ClosingValue 0.
   - `Non_positive_denominator_uses_previous_period_average`: P1 In 10 @ 1,000,000, Out 10 (cost 1,000,000, closing 0/0); P2 Out 5 → cost 500,000 at fallback 100,000, closing −5 / −500,000; P3 In 10 @ 1,200,000 → AvgCost 140,000, closing 5 / 700,000.
   - `First_period_without_stock_uses_supplied_fallback`: fallback 60,000; Out 2 → cost 120,000; closing −2 / −120,000.
   - `Zero_closing_without_outbound_keeps_value`: opening −5 / −500,000, fallback 90,000, In 5 @ 600,000, no outbound → AvgCost 90,000, ClosingQty 0, ClosingValue 100,000.
   - `Closing_of_period_is_opening_of_next` (two periods, assert equality).
   - `Negative_numerator_uses_fallback` (D37): opening −5 / −500,000, fallback 100,000, In 7 @ 140,000, Out 1 → denom 2 but numerator −360,000 → AvgCost 100,000 (fallback, not −180,000); Out cost 100,000; closing 1 / −460,000.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~PeriodicAverageCalculatorTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** `PeriodicAverageCalculator.Run`.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): periodic weighted-average costing calculator"`

## Verification

- `cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory.Unit|FullyQualifiedName~QuotationRecomputeTests"`
- `dotnet build OrderMgmt.sln`

## Exit Criteria

- Every expectation above passes exactly.
- Quotation recompute tests still pass after the `PricingQuantity` extraction.
- No calculator depends on EF, DI or the clock.
