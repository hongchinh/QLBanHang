# Phase 04 — Stock summary (S07) report and page, S06, S08

**Status:** [x] complete
**Complexity:** L

## Objective

- New report `GET api/reports/stock-summary`: opening / in / out / closing per product over a period, quantities and values, with `scope`, warehouse, product-group and search filters.
- New page **Tổng hợp nhập–xuất–tồn** (`/inventory/stock-summary`).
- Export **S07-DNN** (the summary), **S06-DNN** (stock detail with values) and **S08-DNN** (stock card, quantities), S06 and S08 for one, several or all product × warehouse pairs with activity.
- The export button and target dialog on the Thẻ kho page.

## Files

Backend:
- `backend/src/OrderMgmt.Application/Inventory/Reports/Models/InventoryReportDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/Reports/Interfaces/IInventoryReportService.cs`, `Services/InventoryReportService.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/Reports/Validators/InventoryReportValidators.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/InventoryReportsController.cs` (modify)
- `backend/src/OrderMgmt.Application/Tt133/Models/Tt133ExportModels.cs` (modify — `StockCardExportRequest`, `StockCardSheet`)
- `backend/src/OrderMgmt.Application/Tt133/Validators/Tt133ExportValidators.cs` (modify)
- `backend/src/OrderMgmt.Application/Tt133/Interfaces/ITt133FormRenderer.cs`, `ITt133ExportService.cs`, `Services/Tt133ExportService.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/StockSummaryForm.cs`, `StockCardForm.cs`, `StockDetailForm.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormRenderer.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/Tt133ExportController.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockSummaryReportTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/StockSummaryExportTests.cs`, `StockCardExportTests.cs`, `StockDetailExportTests.cs` (new)

Frontend:
- `frontend/src/features/inventory-reports/types.ts`, `api.ts`, `keys.ts`, `hooks.ts` (modify)
- `frontend/src/pages/inventory/stock-summary-page.tsx`, `stock-summary-page.test.tsx` (new)
- `frontend/src/pages/inventory/stock-card-page.tsx`, `stock-card-page.test.tsx` (modify)
- `frontend/src/components/layout/nav-config.ts`, `nav-config.test.ts` (modify)
- `frontend/src/App.tsx`, `frontend/src/lib/route-permissions.ts` (modify)

## Reference files (read-only)

- [section-01](../../../brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md) — S06, S07, S08; [section-02 B2](../../../brainstorms/261012-2237-tt133-report-forms/section-02-review-decisions.md)
- Phases 02 and 03 of this plan — builder, export helpers, `ReportScopeGuard`, `Tt133ExportButton`, `ExportTargetsDialog`, `ReportScopeSelect`, `PeriodPresetSelect`
- `backend/src/OrderMgmt.Application/Inventory/Reports/Services/InventoryReportService.cs` — `BuildStockCardAsync` (window, values, cost masking)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockCardReportTests.cs` — data setup (`SaveOpeningAsync`, `GetCardAsync` are private there: copy them), cost-masking and Branch-scope tests
- `frontend/src/pages/inventory/stock-on-hand-page.tsx` and its test — page shape

## Contracts

```csharp
// InventoryReportDtos.cs
public class StockSummaryRequest
{
    public DateOnly? From { get; set; }            // VN dates, both required, From <= To
    public DateOnly? To { get; set; }
    public ReportScope Scope { get; set; } = ReportScope.Branch;
    public Guid? WarehouseId { get; set; }         // Branch scope only
    public Guid? ProductGroupId { get; set; }
    public string? Search { get; set; }
}
public class StockSummaryReportDto
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ReportScope Scope { get; set; }
    public Guid? WarehouseId { get; set; }
    public bool IsProvisional { get; set; }        // the costing period containing To has not ended
    public bool CanViewCost { get; set; }
    public bool ValuesAtScopeOnly { get; set; }    // a warehouse filter under CostingScope.Branch: quantities only
    public List<StockSummaryRowDto> Rows { get; set; } = new();   // ordered by ProductCode (ordinal)
    public decimal? TotalOpeningValue { get; set; }
    public decimal? TotalInValue { get; set; }
    public decimal? TotalOutValue { get; set; }
    public decimal? TotalClosingValue { get; set; }
}
public class StockSummaryRowDto
{
    public Guid ProductId { get; set; }
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string? ProductGroupName { get; set; }
    public decimal OpeningQty { get; set; }
    public decimal? OpeningValue { get; set; }
    public decimal InQty { get; set; }
    public decimal? InValue { get; set; }
    public decimal OutQty { get; set; }
    public decimal? OutValue { get; set; }
    public decimal ClosingQty { get; set; }
    public decimal? ClosingValue { get; set; }
}
public class StockCardTargetsRequest { public DateOnly From; public DateOnly To; public Guid? WarehouseId; public List<Guid>? ProductIds; }   // as auto-properties; ProductIds null = every product
public class StockCardTargetDto { public Guid ProductId; public string ProductCode; public Guid WarehouseId; public string WarehouseCode; public string WarehouseName; }   // as auto-properties

// IInventoryReportService
Task<StockSummaryReportDto> GetStockSummaryAsync(StockSummaryRequest request, CancellationToken ct = default);
Task<List<StockCardTargetDto>> ListStockCardTargetsAsync(StockCardTargetsRequest request, CancellationToken ct = default);
```

**`stock-summary` rules** (`[HttpGet("stock-summary")]`, `[HasPermission(Permissions.Reports.Inventory)]`, `RunSnapshotAsync`):
- `ReportScopeGuard.EnsureAllowed(_currentUser, request.Scope)`.
- `WarehouseId` with `Scope == Company` → `ValidationDomainException`, key `warehouseId`, "Chỉ lọc kho trong phạm vi chi nhánh." (validator rule). A `WarehouseId` of another branch → the checks of `BuildStockCardAsync` (404 unknown, 403 other branch).
- Ledger = `_db.InventoryLedger.AsNoTracking()`, `.Where(e => e.BranchId == branchId)` for `Branch`, `.Where(e => e.WarehouseId == warehouseId)` when filtered. `start = VnTime.StartOfDay(from)`, `end = VnTime.StartOfNextDay(to)` (T19: no special case for opening stock).
- One grouped query per product: `OpeningQty = Σ(QtyIn − QtyOut)` and `OpeningValue = Σ(InValue − (CostAmount ?? 0))` over `PostedAt < start`; `InQty = Σ QtyIn`, `InValue = Σ InValue`, `OutQty = Σ QtyOut`, `OutValue = Σ (CostAmount ?? 0)` over `start <= PostedAt < end`; rows at or after `end` are left out. `ClosingQty = OpeningQty + InQty − OutQty`, `ClosingValue = OpeningValue + InValue − OutValue`.
- Products: `_db.Products.IgnoreQueryFilters()` with `TrackInventory == true`, the `ProductGroupId` filter, and the search on `Code` / `Name` with `EF.Functions.ILike(…, pattern, LikePattern.EscapeCharacter)` on `LikePattern.Escape(search.Trim())`. `UnitName = StockUnit.NameFor(PricingMode, unit name)`.
- `showValues = canViewCost && !valuesAtScopeOnly`, with `valuesAtScopeOnly = WarehouseId != null && settings.CostingScope == CostingScope.Branch`. When `showValues` is false every value and every total is null.
- Mask first, then drop: a row is dropped when its four quantities and its four (masked) values are all zero.
- Totals = the sums of the four value columns over the kept rows (0 for an empty report when `showValues`).
- Validator `StockSummaryRequestValidator`: `From` / `To` required and in 2000-01-01..2100-12-31, `To ≥ From` (the messages of `StockCardRequestValidator`), `Scope` in enum ("Phạm vi không hợp lệ."), `Search` max 200, the `warehouseId` rule above.

**`ListStockCardTargetsAsync` rules** (T20; working branch only; not exposed as an endpoint): pairs (`ProductId`, `WarehouseId`) of the branch ledger with `Σ(QtyIn − QtyOut)` over `PostedAt < start` ≠ 0, or with any row in `start <= PostedAt < end`; filtered by `WarehouseId` and by `ProductIds` when given; names from `Products` / `Warehouses` with `IgnoreQueryFilters()`; ordered by `ProductCode`, then `WarehouseCode` (ordinal).

**Export routes** (`[HasPermission(Permissions.Reports.Inventory)]`; `format` is an optional query parameter). `stock-summary/export` is GET with `[FromQuery]`; the two multi-target routes are **POST** with `[FromBody] StockCardExportRequest` (T3, T5):

| Route | Request | Service method | Base file name |
|---|---|---|---|
| `GET stock-summary/export` | `StockSummaryRequest` | `ExportStockSummaryAsync` | `S07-DNN_{from}_{to}` |
| `POST stock-card/export` | `StockCardExportRequest` (JSON body) | `ExportStockCardsAsync` | `S08-DNN_{from}_{to}` |
| `POST stock-detail/export` | `StockCardExportRequest` (JSON body) | `ExportStockDetailAsync` | `S06-DNN_{from}_{to}` |

`StockCardExportRequestValidator`: `From` / `To` required, in range, `To ≥ From`; `All == false` and `ProductIds` empty → key `productIds`, "Chọn ít nhất một mặt hàng hoặc xuất tất cả.". There is no cap on the list itself: `EnsureSheetCount` applies `MaxSheets` (T5).

**Notation for the S06 and S08 tests below:** a request written as `from=2026-10-10&to=2026-10-31&warehouseId={KHO01}&productIds={SM01}` on `stock-card/export` or `stock-detail/export` means a `POST` to that route (plus `?format=…` when a format is named) with the JSON body `{ "from": "2026-10-10", "to": "2026-10-31", "warehouseId": "{KHO01}", "productIds": ["{SM01}"] }`. `from` / `to` are 2026-10-10 and 2026-10-31 unless the test gives others, and `all=true` is `"all": true`. Send it with `PostAsJsonAsync(url, body, TestJson.Options)`.

Target resolution shared by S06 and S08 (private `ResolveStockSheetsAsync` in `Tt133ExportService`):
- `All` → `ListStockCardTargetsAsync(from, to, warehouseId, productIds: null)`;
- explicit ids with `WarehouseId` → one target per distinct id with that warehouse (the warehouse's code and name read once; `GetStockCardAsync` answers 404 / 403 for a bad product or warehouse);
- explicit ids without `WarehouseId` → `ListStockCardTargetsAsync(…, productIds)`;
- `EnsureSheetCount(targets.Count)`; one `GetStockCardAsync(new StockCardRequest { ProductId, WarehouseId, From, To })` per target → `StockCardSheet(card, warehouseCode, warehouseName)`, ordered by `ProductCode`, then `WarehouseCode`.

`ExportStockDetailAsync` first checks `_currentUser.HasPermission(Permissions.Inventory.ViewCost)`, otherwise `ForbiddenException("Cần quyền xem giá vốn để xuất sổ chi tiết vật liệu, hàng hóa.")`.

`ExportStockSummaryAsync` resolves `warehouseLabel` (`"Tất cả kho"` or `"{Code} – {Name}"`), `productGroupName` (null when not filtered) and `search` (the request's `Search` trimmed, null when blank; T22), builds the context with the request's scope, and renders one sheet.

## Form layouts

### S07-DNN (`StockSummaryForm`) — landscape, one sheet named `S07`

- Title `BẢNG TỔNG HỢP CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA`. Parameter lines: `Kho: {warehouseLabel}`; `Nhóm hàng: {productGroupName}` only when filtered; `Điều kiện lọc: mã hoặc tên chứa "{search}"` only when `search` is not null (T22).
- Note: `Giá trị tính theo chi nhánh` when `ValuesAtScopeOnly`; else `Giá vốn tạm tính` when values are shown and `IsProvisional`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | STT | Quantity | 1, 2, 3… |
| 2 | B | — | Mã hàng | Text | `ProductCode` |
| 3 | C | — | Tên, quy cách | Text | `ProductName` |
| 4 | D | — | ĐVT | Text | `UnitName` |
| 5, 6 | 1, 2 | Tồn đầu kỳ | Số lượng, Thành tiền | Quantity, Money | `OpeningQty`, `OpeningValue` |
| 7, 8 | 3, 4 | Nhập trong kỳ | Số lượng, Thành tiền | Quantity, Money | `InQty`, `InValue` |
| 9, 10 | 5, 6 | Xuất trong kỳ | Số lượng, Thành tiền | Quantity, Money | `OutQty`, `OutValue` |
| 11, 12 | 7, 8 | Tồn cuối kỳ | Số lượng, Thành tiền | Quantity, Money | `ClosingQty`, `ClosingValue` |

When the values are not shown (`!CanViewCost || ValuesAtScopeOnly`), the four "Thành tiền" columns are left out (eight columns, symbols A–D, 1, 3, 5, 7) and there is no `Cộng` row. Otherwise one summary row `Cộng` with the four totals in columns 6, 8, 10, 12 (quantities are not added up). The form code stays in both cases. Ends with `WriteBookFooter()` and `Finish()`.

### S08-DNN (`StockCardForm`) — portrait, one sheet per `StockCardSheet` named `{ProductCode}-{WarehouseCode}`

- Title `THẺ KHO (SỔ KHO)`. Parameter lines: `Ngày lập thẻ: dd/MM/yyyy` (`PrintDate`); `Tờ số: {n}` (1-based sheet index); `Tên, nhãn hiệu, quy cách vật tư: {ProductName}`; `Đơn vị tính: {UnitName}`; `Mã số: {ProductCode}`; `Kho: {WarehouseCode} – {WarehouseName}`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | STT | Quantity | 1, 2, 3… |
| 2 | B | — | Ngày, tháng | Date | `PostedAt` |
| 3 | C | Số hiệu chứng từ | Nhập | Text | `SourceCode` when `QtyOut == 0` |
| 4 | D | Số hiệu chứng từ | Xuất | Text | `SourceCode` when `QtyOut > 0` |
| 5 | E | — | Diễn giải | Text | `{PartnerName} – {ReasonName}` (T9) |
| 6 | F | — | Ngày nhập, xuất | Date | `PostedAt` |
| 7 | 1 | Số lượng | Nhập | Quantity | `QtyIn` (empty when 0) |
| 8 | 2 | Số lượng | Xuất | Quantity | `QtyOut` (empty when 0) |
| 9 | 3 | Số lượng | Tồn | Quantity | `RunningQty` |
| 10 | G | — | Ký xác nhận của kế toán | Text | empty |

Summary rows: `Tồn đầu kỳ` (9 = `OpeningQty`), `Cộng cuối kỳ` (7 = `InQty`, 8 = `OutQty`), `Tồn cuối kỳ` (9 = `ClosingQty`). No value of the card is read, so the form needs no cost permission. Ends with `WriteBookFooter()` and `Finish()`.

### S06-DNN (`StockDetailForm`) — landscape, one sheet per `StockCardSheet` named `{ProductCode}-{WarehouseCode}`

- Title `SỔ CHI TIẾT VẬT LIỆU, DỤNG CỤ, SẢN PHẨM, HÀNG HÓA`. Parameter lines: `Kho: {WarehouseCode} – {WarehouseName}`; `Tên, quy cách: {ProductCode} – {ProductName}`; `Đơn vị tính: {UnitName}`.
- Note: `Giá trị tính theo chi nhánh` when `card.ValuesAtScopeOnly`; else `Giá vốn tạm tính` when `card.IsProvisional`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | Chứng từ | Số hiệu | Text | `SourceCode` |
| 2 | B | Chứng từ | Ngày, tháng | Date | `PostedAt` |
| 3 | C | — | Diễn giải | Text | `{PartnerName} – {type}`, type = "Tồn đầu kỳ" (`Opening`), "Phiếu nhập kho" (`StockIn`), "Phiếu xuất kho" (`StockOut`) (T9) |
| 4 | D | — | Lý do | Text | `ReasonName` |
| 5 | 1 | — | Đơn giá | Quantity | `UnitCost` (keeps decimals) |
| 6, 7 | 2, 3 | Nhập | Số lượng, Thành tiền | Quantity, Money | `QtyIn`, `InValue` (empty when the quantity is 0) |
| 8, 9 | 4, 5 | Xuất | Số lượng, Thành tiền | Quantity, Money | `QtyOut`, `CostAmount` (empty when the quantity is 0) |
| 10, 11 | 6, 7 | Tồn | Số lượng, Thành tiền | Quantity, Money | `RunningQty`, `RunningValue` |
| 12 | 8 | — | Ghi chú | Text | empty |

Summary rows: `Số dư đầu kỳ` (10 = `OpeningQty`, 11 = `OpeningValue`), `Cộng phát sinh` (6 = `InQty`, 7 = `InValue`, 8 = `OutQty`, 9 = `OutValue`), `Số dư cuối kỳ` (10 = `ClosingQty`, 11 = `ClosingValue`).

When `card.ValuesAtScopeOnly` (review B2), the columns with symbols 1, 3, 5, 7 are left out (eight columns: A, B, C, D, 2, 4, 6, 8) and the summary rows carry quantities only. Ends with `WriteBookFooter()` and `Finish()`.

## Test data (used by the report and the three export test classes)

Each test class has a private `SeedAsync()` doing this in the admin's default branch (`MainBranchId`, default warehouse `KHO01`), after `UpdateInventorySettingsAsync(s => s.CostingScope = CostingScope.Warehouse)`:

| Step | Call | Effect |
|---|---|---|
| product | `CreateInventoryProductAsync("SM01")` | name "Hàng SM01" |
| second warehouse | `CreateWarehouseAsync(MainBranchId, "KHO02")` | |
| opening stock | `SaveOpeningAsync(p, new DateOnly(2026, 10, 1), 10, 1_000_000)` | 10 units, 1,000,000 in KHO01 |
| stock in | `NKH` 10-03 08:00, partner name "NCC A", 10 × 120,000 | +10, +1,200,000 in KHO01 |
| stock out | `XKH` 10-12 08:00, 5 units | −5 at cost `c` in KHO01 (`c` = `CostAmount` of its ledger row, read with `LedgerOfAsync`) |
| stock in | `NKH` 10-15 08:00 into KHO02, 6 × 100,000 | +6, +600,000 in KHO02 |
| stock in | `NKH` 10-20 08:00, 4 × 100,000 | +4, +400,000 in KHO01 |

Hand-computed for `from=2026-10-10&to=2026-10-31`:

| Filter | Opening qty / value | In qty / value | Out qty / value | Closing qty / value |
|---|---|---|---|---|
| no warehouse | 20 / 2,200,000 | 10 / 1,000,000 | 5 / `c` | 25 / 3,200,000 − `c` |
| `warehouseId=KHO01` | 20 / 2,200,000 | 4 / 400,000 | 5 / `c` | 19 / 2,600,000 − `c` |
| `warehouseId=KHO02` | 0 / 0 | 6 / 600,000 | 0 / 0 | 6 / 600,000 |

Stock card rows of SM01 × KHO01 in that window: `XKH` 10-12 (out 5, running 15) and `NKH` 10-20 (in 4, running 19).

## Tasks

### Task 4.1 — Stock summary report

1. **Write the failing tests** `Inventory/StockSummaryReportTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `InventoryTestBase`):
   - `Summary_per_product`: seed; `GET /api/reports/stock-summary?from=2026-10-10&to=2026-10-31` → one row SM01 (`ProductName "Hàng SM01"`, `ProductGroupName` and `UnitName` as the stock-on-hand report shows them) with the "no warehouse" numbers; `Scope == Branch`, `CanViewCost == true`, `ValuesAtScopeOnly == false`; the four totals equal the row's values.
   - `Warehouse_filter`: the KHO01 and KHO02 lines of the table.
   - `Invariants`: for each of the three queries and for `from=2026-10-01&to=2026-10-31`: `OpeningQty + InQty − OutQty == ClosingQty` and the same for values; the unfiltered `ClosingQty` / `ClosingValue` equal the sums of SM01's rows in `GET /api/reports/stock-on-hand?at=2026-10-31T23:59:59%2B07:00`; each warehouse-filtered row equals `OpeningQty`, `InQty`, `OutQty`, `ClosingQty`, `OpeningValue`, `InValue`, `OutValue`, `ClosingValue` of `GET /api/reports/stock-card?productId=…&warehouseId=…` for the same dates.
   - `Rows_with_only_zeros_are_dropped`: a second product `SM02` with a stock-in of 3 on 10-02 and a stock-out of 3 on 10-03 → no SM02 row for `from=2026-10-10`; a row for `from=2026-10-01`.
   - `Only_tracked_products`: set `TrackInventory = false` on SM01 through `InDbAsync` → no rows.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockSummaryReportTests"`. Expected: FAIL — build error, `StockSummaryReportDto` does not exist.
3. **Write the minimal implementation:** the DTOs, `GetStockSummaryAsync` (Branch scope, no cost masking yet: values always shown; the `Scope` guard, masking and validation are Task 4.2), an **empty** `StockSummaryRequestValidator`, the controller action.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(reports): stock in-out-balance summary report"`

### Task 4.2 — Stock summary: scope, cost masking, filters, validation

1. **Write the failing tests** in `StockSummaryReportTests.cs`:
   - `Company_scope_adds_every_branch`: seed; create branch B with a warehouse (`CreateBranchAsync`, `CreateWarehouseAsync`) and post a stock-in of 7 × 50,000 of SM01 there on 10-22 with a client switched by `UseBranch`; `scope=Company` → `InQty` 17, `InValue` 1,350,000, `ClosingQty` 32; `scope=Branch` → the Task 4.1 numbers.
   - `Company_scope_needs_access_all`: a client with only `reports.inventory` → 403 for `scope=Company`, 200 for `scope=Branch`.
   - `Values_hidden_without_view_cost`: a client with `reports.inventory` but not `inventory.view_cost` → `CanViewCost == false`, every value and total null, quantities as before.
   - `Warehouse_filter_under_branch_costing_has_quantities_only`: with `CostingScope = Branch` (`UpdateInventorySettingsAsync`; follow the Branch-scope test of `StockCardReportTests` for the recalculation it needs) and `warehouseId=KHO01` → `ValuesAtScopeOnly == true`, values null, quantities 20 / 4 / 5 / 19; without the warehouse filter → values present.
   - `Group_and_search_filters`: `productGroupId` of another group → no rows; `search=sm0` → SM01; `search=%` → no rows (the wildcard is escaped).
   - `Validation_and_permission`: missing `from` → 400 key `from`; `from` after `to` → 400 key `to`; `scope=Company&warehouseId={KHO01}` → 400 key `warehouseId`; a warehouse of another branch → 403; an unknown warehouse → 404; a `reports.debt`-only client → 403.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — `scope=Company` returns the branch numbers, values are returned without `view_cost`, invalid requests return 200 or 500.
3. **Write the minimal implementation:** the scope guard and filter, `showValues` masking with "mask first, then drop", the warehouse checks, the validator rules.
4. **Run the tests to verify they pass:** `--filter "FullyQualifiedName~StockSummaryReportTests|FullyQualifiedName~StockCardReportTests|FullyQualifiedName~StockOnHandReportTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(reports): scope, cost masking and validation of the stock summary"`

### Task 4.3 — S07 form

1. **Write the failing tests** `Tt133/StockSummaryExportTests.cs` (inherits `InventoryTestBase`, same seed):
   - `S07_with_values`: `GET /api/reports/stock-summary/export?from=2026-10-10&to=2026-10-31` → file name `S07-DNN_20261010_20261031.xlsx`; one worksheet `S07`; cells "Mẫu số S07-DNN", the title, "Từ ngày 10/10/2026 đến ngày 31/10/2026", "Kho: Tất cả kho"; no cell starts with "Nhóm hàng:"; the SM01 row: column 1 = 1, column 2 "SM01", columns 5–12 = (20 / 2,200,000 / 10 / 1,000,000 / 5 / `c` / 25 / 3,200,000 − `c`); `Cộng` with columns 6, 8, 10, 12 equal to the report totals and columns 5, 7, 9, 11 empty; landscape.
   - `S07_parameters`: `warehouseId={KHO01}&productGroupId={EPS group id}` → "Kho: KHO01 – {warehouse name}" and "Nhóm hàng: {group name}". `search=sm0` → the cell `Điều kiện lọc: mã hoặc tên chứa "sm0"` and the SM01 row; without `search` no cell starts with "Điều kiện lọc:".
   - `S07_without_cost_permission_has_quantities_only`: a client without `inventory.view_cost` → the header row has no "Thành tiền" cell, the SM01 row has eight columns with quantities 20 / 10 / 5 / 25, there is no `Cộng` row, and "Mẫu số S07-DNN" is still printed.
   - `S07_note_for_provisional_cost`: with `to` = today (VN) the sheet contains "Giá vốn tạm tính"; with `to=2026-10-31` only when the report's `IsProvisional` is true (assert the cell's presence equals the flag of `GET stock-summary`).
   - `Validation_and_permission`: `from` after `to` → 400; `scope=Company` without `branches.access_all` → 403; a `reports.debt`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockSummaryExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `RenderStockSummary`, `StockSummaryForm`, `ExportStockSummaryAsync`, the action `[HttpGet("stock-summary/export")]` with `StockSummaryRequestValidator`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S07 stock summary form"`

### Task 4.4 — Stock card targets and S08

1. **Write the failing tests** `Tt133/StockCardExportTests.cs` (inherits `InventoryTestBase`, same seed plus a product `SM03` with no ledger rows):
   - `S08_for_one_product_and_warehouse`: `POST /api/reports/stock-card/export` with `from=2026-10-10&to=2026-10-31&warehouseId={KHO01}&productIds={SM01}` (see the notation above) → file name `S08-DNN_20261010_20261031.xlsx`; one worksheet `SM01-KHO01`; cells "Mẫu số S08-DNN", "THẺ KHO (SỔ KHO)", "Tờ số: 1", "Mã số: SM01", "Tên, nhãn hiệu, quy cách vật tư: Hàng SM01", `Kho: KHO01 – {name}`; `Tồn đầu kỳ` column 9 = 20; two rows: (STT 1, the `XKH` code in column 4, column 3 empty, column 8 = 5, column 9 = 15) and (STT 2, the `NKH` code in column 3, column 7 = 4, column 9 = 19); `Cộng cuối kỳ` 4 and 5; `Tồn cuối kỳ` 19; portrait.
   - `S08_all_pairs_with_activity`: `all=true` (no warehouse) → two worksheets `SM01-KHO01`, `SM01-KHO02` in that order, "Tờ số: 1" and "Tờ số: 2"; no sheet for SM03; each sheet's `Tồn cuối kỳ` equals `ClosingQty` of the matching `GET stock-card`.
   - `S08_explicit_product_without_warehouse`: `productIds={SM01}` → the same two sheets; `productIds={SM03}` → 422 `EXPORT_NO_TARGETS`.
   - `S08_explicit_product_with_warehouse_is_always_a_sheet`: `warehouseId={KHO02}&productIds={SM03}` → one worksheet `SM03-KHO02` with no body rows and `Tồn cuối kỳ` 0.
   - `S08_needs_no_cost_permission`: a client with `reports.inventory` only → 200.
   - `Validation_and_permission`: neither `productIds` nor `all` → 400 key `productIds`; missing `to` → 400 key `to`; `GET` on the route → 405; an unknown product with `warehouseId` → 404; a warehouse of another branch → 403; a `reports.debt`-only client → 403.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockCardExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `StockCardTargetsRequest`, `StockCardTargetDto`, `ListStockCardTargetsAsync`; `StockCardExportRequest`, `StockCardSheet`, `StockCardExportRequestValidator`; `ResolveStockSheetsAsync`; `RenderStockCards`, `StockCardForm`; `ExportStockCardsAsync`; the action `[HttpPost("stock-card/export")]` with `[FromBody] StockCardExportRequest request, [FromQuery] ExportFormat format`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S08 stock card form with bulk targets"`

### Task 4.5 — S06 stock detail

1. **Write the failing tests** `Tt133/StockDetailExportTests.cs` (inherits `InventoryTestBase`, same seed):
   - `S06_for_one_product_and_warehouse`: `POST /api/reports/stock-detail/export` with `from=2026-10-10&to=2026-10-31&warehouseId={KHO01}&productIds={SM01}` → file name `S06-DNN_20261010_20261031.xlsx`; worksheet `SM01-KHO01`; cells "Mẫu số S06-DNN", the title, `Kho: KHO01 – {name}`, "Tên, quy cách: SM01 – Hàng SM01"; `Số dư đầu kỳ` columns 10 / 11 = 20 / 2,200,000; the `XKH` row: column 3 ends with "Phiếu xuất kho", column 4 "Xuất khác", column 5 = the row's `UnitCost`, columns 8 / 9 = 5 / `c`, columns 10 / 11 = 15 / 2,200,000 − `c`, columns 6 / 7 empty; the `NKH` row: columns 6 / 7 = 4 / 400,000, columns 10 / 11 = 19 / 2,600,000 − `c`; `Cộng phát sinh` columns 6–9 = 4 / 400,000 / 5 / `c`; `Số dư cuối kỳ` 19 / 2,600,000 − `c`; landscape.
   - `S06_file_equals_the_stock_card`: the summary cells equal the fields of `GET /api/reports/stock-card` for the same parameters.
   - `S06_all_pairs`: `all=true` → two worksheets.
   - `S06_under_branch_costing_has_quantities_only`: with `CostingScope = Branch` (as in Task 4.2) → the sheet contains "Giá trị tính theo chi nhánh", the header has no "Đơn giá" and no "Thành tiền" cell, eight columns, and `Số dư cuối kỳ` holds the quantity 19 only.
   - `S06_needs_the_cost_permission`: a client with `reports.inventory` but not `inventory.view_cost` → 403; the same client gets 200 from `stock-card/export`.
   - `Validation`: neither `productIds` nor `all` → 400 key `productIds`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockDetailExportTests"`. Expected: FAIL — 404.
3. **Write the minimal implementation:** `RenderStockDetail`, `StockDetailForm`, `ExportStockDetailAsync` (cost permission check, then `ResolveStockSheetsAsync`), the action `[HttpPost("stock-detail/export")]` (body and `format` as for `stock-card/export`).
4. **Run the tests to verify they pass:** `--filter "FullyQualifiedName~Tt133"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S06 stock detail form"`

### Task 4.6 — Frontend: Tổng hợp nhập–xuất–tồn page

1. **Write the failing tests:**
   - `components/layout/nav-config.test.ts`: the report group lists `['Kho', 'Tổng hợp nhập xuất tồn']` after `['Kho', 'Thẻ kho']` (update the two expectations that list the Kho items, for the full list and for the WAREHOUSE role, with path `/inventory/stock-summary`).
   - `lib/route-permissions` test: `/inventory/stock-summary` needs `reports.inventory`.
   - `pages/inventory/stock-summary-page.test.tsx` (mock `@/features/inventory-reports/hooks`, `@/features/warehouses/hooks`, `@/features/products/hooks`, the export button stub and `@/features/company-profile/hooks`):
     - renders a row with code, name, unit and the eight numbers (quantities with `formatStockQuantity`, values with `formatMoneyForDisplay`) and a footer "Cộng" with the four value totals;
     - `canViewCost: false` → no "Thành tiền" headers and no footer;
     - `valuesAtScopeOnly: true` → the note "Giá trị tính theo chi nhánh. Bỏ lọc kho để xem giá trị." and no value columns;
     - `isProvisional: true` with values → the badge "Giá trị tạm tính";
     - the hook receives `{ from, to, scope: 'Branch', warehouseId, productGroupId, search }` from the filters (search debounced); from > to shows "Từ ngày không được sau Đến ngày" and calls the hook with `enabled: false`;
     - with `branches.access_all`, choosing "Toàn công ty" sends `scope: 'Company'`, clears the warehouse filter and disables the warehouse select;
     - the preset "Quý trước" updates the dates;
     - the export button gets one form `S07-DNN`, `report: 'stock-summary'`, `params` equal to the hook params, `fileBase: 'S07-DNN_{from}_{to}'`;
     - an empty report shows "Không có phát sinh trong kỳ";
     - clicking a row navigates to `/inventory/stock-card?productId=…` (plus `&warehouseId=…` when filtered).
2. **Run the tests to verify they fail:** `npx vitest run src/pages/inventory/stock-summary-page.test.tsx src/components/layout src/lib`. Expected: FAIL — the page cannot be imported; the nav expectations do not match.
3. **Write the minimal implementation:**
   - `features/inventory-reports`: `StockSummaryParams`, `StockSummaryRow`, `StockSummaryReport` (mirroring the DTOs, values `number | null`), `inventoryReportsApi.stockSummary` (`/reports/stock-summary`), `inventoryReportKeys.stockSummary(params)`, `useStockSummary(params, { enabled })`.
   - `pages/inventory/stock-summary-page.tsx` in the shape of `stock-on-hand-page.tsx`: title "Tổng hợp nhập – xuất – tồn"; filters Từ ngày, Đến ngày, `PeriodPresetSelect`, `ReportScopeSelect`, Kho, Nhóm hàng, Tìm kiếm; a two-tier table header (Tồn đầu kỳ / Nhập trong kỳ / Xuất trong kỳ / Tồn cuối kỳ, each "SL" and "Thành tiền"), numeric cells right-aligned with `tabular-nums`.
   - nav item `{ to: '/inventory/stock-summary', label: 'Tổng hợp nhập xuất tồn', icon: Sigma, section: 'Kho', permission: 'reports.inventory' }` after Thẻ kho (`Sigma` is not used by another item; if the icon test objects, pick another unused lucide icon); the route in `App.tsx` under `<ProtectedRoute permission="reports.inventory">`; the route rule `/^\/inventory\/(stock-on-hand|stock-card|stock-summary)$/`.
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): stock in-out-balance summary page"`

### Task 4.7 — Frontend: S06 and S08 on the Thẻ kho page

1. **Write the failing tests** in `pages/inventory/stock-card-page.test.tsx` (export button stub, mocked `useTt133Download`, `useAuthStore.setState` for permissions):
   - with `inventory.view_cost` the button gets two forms: `S08-DNN` (`report: 'stock-card'`) and `S06-DNN` (`report: 'stock-detail'`), both with `params: { from, to, warehouseId }` (`warehouseId` omitted for "Tất cả kho") and file bases `S08-DNN_{from}_{to}` / `S06-DNN_{from}_{to}`; without the permission only `S08-DNN`;
   - selecting a form opens the target dialog titled "Chọn mặt hàng xuất sổ" with the current product (code) as "Đang xem"; confirming it calls `download(form, format, { productIds: [productId] })`; "Tất cả có phát sinh trong kỳ" calls `download(form, format, { all: true })`; several picked products send their ids;
   - no product selected → the dialog starts at "Chọn nhiều";
   - an invalid range disables the button;
   - the preset "Tháng trước" updates the dates.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/inventory/stock-card-page.test.tsx`. Expected: FAIL — no export button on the page.
3. **Write the minimal implementation:** the button with `onSelect`, `ExportTargetsDialog` whose `renderPicker` reuses the product picker the page already has (it calls `add({ id, label: code })`), `useTt133Download`, `PeriodPresetSelect`.
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): S06 and S08 exports on the stock card page"`

### Task 4.8 — Phase checkpoint

1. Run the full backend suite and the frontend `lint`, `test`, `build`; compare with `BASELINE.md`.
2. If a new failure appears, write a focused failing test, fix, re-run.
3. Commit only if a fix was needed: `git commit -m "fix(tt133): <what>"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~StockSummaryReportTests|FullyQualifiedName~StockCardReportTests"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
cd ../frontend
npm run typecheck && npm run lint && npm run test && npm run build
```

## Exit Criteria

- `stock-summary` returns the hand-computed numbers; opening + in − out = closing; closing equals stock-on-hand at the end of `to`; each product × warehouse equals its stock card.
- `scope=Company` adds every branch and needs `branches.access_all`; values are hidden without `inventory.view_cost` and for a warehouse filter under Branch costing.
- S07, S08 and S06 export with the specified columns; S06 needs the cost permission and loses its value columns under Branch costing; S06 and S08 export one, several or all pairs.
- The page Tổng hợp nhập–xuất–tồn is in the sidebar (Báo cáo › Kho) and exports S07; the Thẻ kho page exports S08 and, with the cost permission, S06.
- No new failures against `BASELINE.md`.
