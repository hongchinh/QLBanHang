# Phase 05 — Stock voucher API

**Status:** [ ] pending
**Complexity:** XL

## Objective

Expose stock-in and stock-out vouchers over HTTP: create, read, list (filters, aggregates, owners), update, cancel, restore and delete, plus helper endpoints (form defaults with the next code, stock at a point in time, partner search by reason) — all following the section-03 §5 save rules in one transaction. Map `NegativeStockException` to 422 and EF concurrency conflicts to 409.

## Files

- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Models/StockVoucherDtos.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Validators/StockVoucherValidators.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Interfaces/IStockVoucherService.cs` (new)
- `backend/src/OrderMgmt.Application/Inventory/StockVouchers/Services/{StockVoucherService,StockVoucherPermissions}.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.WebApi/Controllers/StockVouchersController.cs` (new)
- `backend/src/OrderMgmt.WebApi/Middleware/GlobalExceptionMiddleware.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` (modify — voucher helpers)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/StockVouchers/{StockVoucherCreateTests,StockVoucherValidationTests,StockVoucherOutboundTests,StockVoucherQueryTests,StockVoucherUpdateTests,StockVoucherLifecycleTests,StockVoucherHelpersTests,StockVoucherConcurrencyTests}.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` — list/aggregates/owners/activities/line upsert patterns
- `backend/src/OrderMgmt.Application/Sales/Quotations/Helpers/OwnerIdListParser.cs`
- Phase 03/04 contracts: `StockVoucherCalculator`, `DocumentNumberFormatter`, `IDocumentCounter`, `IInventoryPostingService`, `StockUnit`, `VnTime`

## API contract

All routes are under `api/stock-vouchers` with `[Authorize]`. The service checks type-specific permissions through `StockVoucherPermissions.{View,Create,Edit,Delete,Cancel,EditAll}(StockDirection)` and throws `ForbiddenException` (403).

| Method | Route | Permission (by type) | Request → Response |
|---|---|---|---|
| GET | `/?type=In&...` | view | `StockVoucherListRequest` → `StockVoucherListResult` |
| GET | `/owners?type=` | view | `IReadOnlyList<StockVoucherOwnerDto { Id, FullName }>` |
| GET | `/defaults?type=&voucherAt=` | create | `StockVoucherDefaultsDto` |
| POST | `/stock-at` | view | `StockAtRequest` → `IReadOnlyList<StockAtResult>` |
| GET | `/partners?type=&keyword=&limit=20&reasonId=` (reasonId optional) | view | `List<CustomerSearchItemDto>` |
| GET | `/{id}` | view | `StockVoucherDto` |
| GET | `/{id}/activities` | view | `IReadOnlyList<StockVoucherActivityDto>` |
| POST | `/` | create | `UpsertStockVoucherRequest` → `StockVoucherDto` |
| PUT | `/{id}` | edit (+ edit_all if not owner) | `UpsertStockVoucherRequest` (with `Version`) → `StockVoucherDto` |
| POST | `/{id}/cancel` | cancel (+ edit_all if not owner) | `StockVoucherActionRequest { uint? Version; bool AcknowledgeNegativeStock }` (missing `Version` → 400) → `StockVoucherDto` |
| POST | `/{id}/restore` | cancel (+ edit_all if not owner) | same → `StockVoucherDto` |
| DELETE | `/{id}?version=&acknowledgeNegativeStock=` | delete (+ edit_all if not owner) | `ApiResponse` |

DTO shapes (`StockVoucherDtos.cs`):

```csharp
public class UpsertStockVoucherRequest
{
    public StockDirection Type { get; set; }            // create: chooses type; update: must equal stored type
    public DateTimeOffset VoucherAt { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ReasonId { get; set; }
    public Guid? PartnerId { get; set; }
    public string? PartnerName { get; set; }            // null → partner.Name
    public string? PartnerAddress { get; set; }         // null → partner.CompanyAddress
    public string? PartnerTaxCode { get; set; }         // null → partner.TaxCode
    public string? HandlerName { get; set; }
    public Guid? PaymentMethodId { get; set; }
    public string? Note { get; set; }
    public decimal Freight { get; set; }
    public decimal OrderDiscount { get; set; }
    public decimal? PaidAmount { get; set; }            // null → Total (the frontend always sends it on update, so a partial payment is never reset)
    public uint? Version { get; set; }                  // required on update
    public bool AcknowledgeNegativeStock { get; set; }
    public List<UpsertStockVoucherLineRequest> Lines { get; set; } = new();
}

public class UpsertStockVoucherLineRequest
{
    public Guid? Id { get; set; }
    public int SortOrder { get; set; }
    public Guid ProductId { get; set; }
    public Guid? WarehouseId { get; set; }              // null → header warehouse
    public decimal? SheetCount { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Thickness { get; set; }
    public decimal Quantity { get; set; }               // PerUnit only; others are computed
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal? DiscountAmount { get; set; }
    public bool DiscountManual { get; set; }
    public decimal VatRate { get; set; }
    public string? Note { get; set; }
}
```

- `StockVoucherDto`: every header field, plus `WarehouseCode/Name`, `PartnerCode`, `ReasonName`, `PaymentMethodName`, `DiscountTotal` (= `LineDiscountTotal + OrderDiscount`), `OwnerName`, `CreatedAt`, `Version`, `CanEdit`, `CanCancel`, `CanDelete`, and `Lines` (`StockVoucherLineDto` = every line field except `InboundValue`, plus `WarehouseCode`).
- `StockVoucherListRequest : PageRequest { Type, DateOnly? From, DateOnly? To, WarehouseId?, PartnerId?, ReasonId?, StockVoucherStatus? Status, string? OwnerUserIds }`. Dates are VN dates on `VoucherAt`. Warehouse matches the header warehouse **or** any line warehouse. `Search` is an ILIKE on `Code` and `PartnerName`. Sorting: `code`, `date`, `total`; default `VoucherAt desc`.
- `StockVoucherListItemDto { Id, Type, Code, VoucherAt, WarehouseName, PartnerName, ReasonName, PaymentMethodName, GoodsAmount, DiscountTotal, VatTotal, Freight, Total, PaidAmount, Status, OwnerUserId, OwnerName, CreatedAt }`.
- `StockVoucherListResult : PagedResult<StockVoucherListItemDto>` (inherits `Items`, `Page`, `PageSize`, `TotalItems`, `TotalPages`, `HasNextPage`, `HasPreviousPage`, like `QuotationListResult`) plus `StockVoucherListAggregates Aggregates { GoodsAmount, DiscountTotal, VatTotal, Freight, Total, PaidAmount }`. Aggregates exclude Cancelled unless `Status` is given.
- `StockVoucherActivityDto { Id, Action, ActorUserId, ActorName, OccurredAt, Description }`, newest first.
- Every DTO member is a public auto-property (`{ get; set; }`); System.Text.Json ignores public fields.
- `StockVoucherDefaultsDto { NextCode, VoucherAt, WarehouseId?, ReasonId?, PaymentMethodId? }`.
- `StockAtRequest { Type, DateTimeOffset At, Guid? ExcludeVoucherId, List<StockAtItem { ProductId, WarehouseId }> Items }` and `StockAtResult { ProductId, WarehouseId, Quantity }`.

## Save rules (section-03 §5) — reference for Tasks 5.1–5.6

Every write runs in `ITransactionRunner.RunAsync`. `VoucherAt` from the request is normalized with `VnTime.ToUtc` first (D27). Steps:
1. Permission by type; voucher in the working branch (`ICurrentBranch`; another branch → 403); ownership / `edit_all`; Cancelled is read-only (409); on update / cancel / restore / delete: missing `Version` → 400, then set `Entry(v).Property(x => x.Version).OriginalValue = request.Version.Value` **and** mark the header modified (`v.UpdatedAt = _clock.UtcNow; v.UpdatedBy = _currentUser.UserId;`) so the `xmin` check runs on every write, even when only lines change (D29).
2. Period lock: `VnTime.ToVnDate(VoucherAt) <= Branch.LockedUntil` for the new date and, on update / cancel / restore / delete, the stored date → `DomainException("PERIOD_LOCKED", "Ngày chứng từ đã khóa sổ (đến dd/MM/yyyy).")`.
3. Reference validation, collected into one `ValidationDomainException(errors, message)` (400, message = first error). Keys are camelCase: `reasonId`, `partnerId`, `warehouseId`, `paymentMethodId`, `orderDiscount`, `lines`, `lines[i].productId`, `lines[i].warehouseId`, `lines[i].sheetCount|length|width|thickness|quantity|discountAmount`. Rules:
   - the reason exists and its `Direction == Type`;
   - partner per D4;
   - the header warehouse and every line warehouse are in the branch; "active" is required only for a **new or changed** header warehouse and for lines whose `ProductId` or `WarehouseId` is new or changed — unchanged lines of an older voucher stay editable after a product or warehouse is deactivated (review m15);
   - the payment method exists when given;
   - at least 1 line; every product exists (and is active when the line's product is new or changed);
   - dimensions per `PricingMode` are > 0 (LinearMeter: SheetCount + Length; SquareMeter: + Width; CubicMeter: + Thickness; PerUnit: Quantity > 0), and the computed `Quantity > 0`;
   - `UnitPrice >= 0`; `DiscountRate` and `VatRate` in 0..100; a manual `DiscountAmount` in `0..Amount` (review m24);
   - `0 <= OrderDiscount <= ΣNet`; `Freight >= 0`; `PaidAmount >= 0`.

   FluentValidation covers shape only (non-empty ids, lengths, enum values); every rule above lives in exactly one layer — the service. Register a camelCase property-name resolver once in `Application/DependencyInjection.cs` (`ValidatorOptions.Global.PropertyNameResolver = (_, member, _) => member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);`) so FluentValidation keys are camelCase too (no existing test or frontend code reads the old PascalCase keys).
4. `IInventoryPostingService.AcquireLocksAsync(branchId, old ∪ new product ids)` (shared branch gate, then product keys — D30). Read `InventorySettings` only after this step.
5. Create only: the numbering row for `(DocType, BranchId)` (missing → 400 `NUMBERING_NOT_CONFIGURED`); `IDocumentCounter.NextAsync` with `DocumentNumberFormatter.PeriodKey(policy, VN date)`; `Code = Format(...)`.
6. Write the header and lines from `StockVoucherCalculator.Compute`, with line snapshots: `ProductCode`, `ProductName`, `TrackInventory`, `PricingMode`, `UnitName = StockUnit.NameFor(...)`, `PriceIncludesVat`. Store `GoodsAmount`, `LineDiscountTotal`, `VatTotal`, `Total` and `PaidAmount = request.PaidAmount ?? Total`. On update: update lines by `Id`, add new ones with `_db.StockVoucherLines.Add(...)` (never only through `voucher.Lines`, see SUMMARY "Child rows"), soft-delete removed ones (`QuotationService.PopulateLinesAsync` pattern). `SaveChangesAsync` (the concurrency check happens here).
7. `PostAsync(sourceType = In ? StockIn : StockOut, voucher.Id, drafts, request.AcknowledgeNegativeStock)`. Drafts are built only from tracked lines: `PostedAt = VoucherAt` (UTC), `SourceCode = Code`, `LineSortOrder = SortOrder`, `WarehouseId = line.WarehouseId`, In → `QtyIn = Quantity`, `InValue = InboundValue + (settings.PurchaseCostIncludesVat ? VatAmount : 0)`; Out → `QtyOut = Quantity`. Cancel and delete post an empty list.
8. Any stock-in write (create / update / cancel / restore / delete): D35 cost-price recompute for every product of the old ∪ new lines — `CostPrice` / `CostPriceUpdatedOn` come from the product's latest Active, non-deleted stock-in line (by `VoucherAt`, then `SortOrder`); none left → keep `CostPrice`, clear `CostPriceUpdatedOn`.
9. Activity (`Tạo phiếu`, `Cập nhật phiếu`, `Hủy phiếu`, `Khôi phục phiếu`, `Xóa phiếu`) added with `_db.StockVoucherActivities.Add(...)`, then `SaveChangesAsync`.

Steps per operation (review m15):

| Operation | Steps |
|---|---|
| Create | 1 (create permission, branch), 2, 3, 4, 5, 6, 7, 8 (stock-in), 9 |
| Update | 1–4, 6, 7, 8 (stock-in), 9 |
| Cancel / Delete | 1, 2 (stored date), 4, 7 (empty list), 8 (stock-in), 9 |
| Restore | 1, 2 (stored date), 4, 7 (the stored lines as they are — no re-validation, no re-snapshot), 8 (stock-in), 9 |

The 409 arm for `DbUpdateConcurrencyException` uses a generic message (`"Dữ liệu đã được người khác cập nhật. Vui lòng tải lại."`) because it applies to every module.

## Tasks

### Task 5.1 — Create stock-in voucher (happy path)

1. **Write the failing test** `StockVouchers/StockVoucherCreateTests.cs : InventoryTestBase`. `CreateInventoryProductAsync` and `Vn` already exist (Phase 04 Task 4.5). First add these helpers to `InventoryTestBase`:
   - `ReasonIdAsync(string code)`;
   - `PostVoucherAsync(HttpClient c, UpsertStockVoucherRequest r)` → `(HttpStatusCode, StockVoucherDto?, ApiError?)`.

   Unless a test says otherwise, every line uses `VatRate = 0`, `DiscountRate = 0`, `Freight = 0` and `OrderDiscount = 0`, so expected costs are plain `qty × price`.

   Tests:
   - `Create_stock_in_computes_totals_code_ledger_cost_price_and_activity` — Phase 03 `In_example` data, reason `NMH`, a supplier partner, `KHO01`, `VoucherAt = 2026-10-02 09:00 VN`:
     - response: `Code == "PN00001"`, `GoodsAmount 6,800,000`, `LineDiscountTotal 75,000`, `DiscountTotal 175,000`, `VatTotal 628,513`, `Total 7,453,513`, `PaidAmount 7,453,513`, line values as in Phase 03;
     - DB: 2 ledger rows (tracked lines) with `InValue` 5,573,858 and 1,560,473 (`PurchaseCostIncludesVat = true`); `StockBalance` 100 and 1.0;
     - each product's `CostPrice == UnitPrice` and `CostPriceUpdatedOn == VoucherAt` (the untracked product too);
     - one `Created` activity;
     - the request sends `VoucherAt` as `2026-10-02T09:00:00+07:00` (a non-UTC offset) and the stored value is the same instant with offset 0 (D27).
   - `Codes_are_sequential_per_type_and_reset_monthly_when_configured`:
     - a second stock-in gets `PN00002`; the first stock-out gets `PX00001`;
     - then set the StockIn numbering to `{KH}{NAM}{THANG}{STT}`, Monthly, length 5. Vouchers on 2026-10-05, 2026-10-20 and 2026-11-02 → `PN20261000001`, `PN20261000002`, `PN20261100001`;
     - then set it to `{KH}{NAM}{STT}`, Yearly. Vouchers on 2026-12-30 09:00 VN and 2027-01-01 00:30 VN (= 2026-12-31T17:30Z, still 2026 in UTC) → `PN202600001`, `PN202700001` (review m17; the VN date decides the period, not the UTC date).
   - `Older_voucher_does_not_overwrite_newer_cost_price` — In on 10-05 @ 50,000, then In on 10-01 @ 40,000 → `CostPrice` stays 50,000.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherCreateTests"`. Expected: FAIL (compile / 404).
3. **Write the minimal implementation:** DTOs, the `IStockVoucherService` interface (full surface from the contract table; methods not yet implemented throw `NotImplementedException`), `StockVoucherPermissions`, `CreateAsync` with **only the happy path**: the `create` permission of the type, the working branch, and steps 4–9 (period lock, reference validation and the remaining permission checks are Task 5.2, so its RED step can fail — review m9), `GetAsync` (enough to return the DTO), the controller (`POST /`, `GET /{id}`), DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): create stock-in vouchers with numbering, ledger posting and cost price update"`

### Task 5.2 — Create validation, permissions, branch and period lock

1. **Write the failing test** `StockVouchers/StockVoucherValidationTests.cs`:
   - `Reason_and_partner_rules`. Each case → 400 with the expected key:
     - a reason of the other direction (`reasonId`);
     - a supplier reason with a customer-only partner, or with no partner (`partnerId`);
     - a customer reason with a supplier-only partner;
     - a `PartnerType.None` reason (created through the API) with a partner.
   - `Line_rules` → 400 per case:
     - a line warehouse in another branch (`lines[0].warehouseId`);
     - a SquareMeter line without width (`lines[0].width`);
     - a PerUnit line with quantity 0;
     - an order discount above ΣNet;
     - a manual `DiscountAmount` above the line amount (`lines[0].discountAmount`);
     - keys come back camelCase from both layers (e.g. an empty `reasonId` from FluentValidation → `reasonId`, not `ReasonId`).
   - `Period_lock_boundary_uses_vn_date`: `LockedUntil = 2026-10-05`; `VoucherAt = 2026-10-05 23:30 VN` → 400 `PERIOD_LOCKED`; `2026-10-06 00:10 VN` → 200.
   - `Type_permission_and_working_branch`:
     - a client with only `stock_in.view/create`: POST Out → 403, POST In → 200;
     - admin with header = branch B and a warehouse in B → stored `BranchId == B` and code `PN00001` (numbering is per branch).
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherValidationTests"`. Expected: FAIL (assertion: the 400s are not produced yet).
3. **Write the minimal implementation:** `UpsertStockVoucherRequestValidator` (FluentValidation shape rules), the global camelCase property-name resolver, the service reference validation (save rule 3), the lock check (rule 2) and the remaining permission checks (rule 1).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockVoucherValidationTests|FullyQualifiedName~StockVoucherCreateTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): validate stock vouchers against reasons, partners, warehouses, dimensions and period lock"`

### Task 5.3 — Stock-out vouchers, outbound cost and 422 mapping

1. **Write the failing test** `StockVouchers/StockVoucherOutboundTests.cs`:
   - `Stock_out_ledger_rows_carry_period_average_cost` (In 100 @ 50,000 on 10-02, Out 30 on 10-05 → Out ledger `CostAmount` 1,500,000, `UnitCost` 50,000)
   - `Warn_policy_returns_422_warning_then_succeeds_with_acknowledgement` (HTTP 422, `error.code == "NEGATIVE_STOCK_WARNING"`, `error.details` has key `"{productCode}@KHO01"`; resend with `acknowledgeNegativeStock: true` → 200)
   - `Block_policy_returns_422_blocked_even_with_acknowledgement` (Allow is already covered by the Phase 04 engine tests)
   - `Untracked_lines_skip_ledger_and_stock_check` (service product under Block with no stock → 200, no ledger row)
   - `Lines_of_the_same_product_are_checked_cumulatively` (stock 10; two lines of 6 under Block → 422)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherOutboundTests"`. Expected: FAIL (500 instead of 422 — no mapping yet).
3. **Write the minimal implementation:** in `GlobalExceptionMiddleware`, add **before** the `DomainException` arm:
   ```csharp
   NegativeStockException nse => (StatusCodes.Status422UnprocessableEntity, new ApiError
       { Code = nse.Code, Message = nse.Message, Details = nse.Shortages }),
   ```
   Add `[ProducesResponseType(typeof(ApiResponse), 422)]` on the controller. Check that the Out paths through the service are complete.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock-out vouchers with negative stock policy responses"`

### Task 5.4 — Get, list, owners and activities

1. **Write the failing test** `StockVouchers/StockVoucherQueryTests.cs`:
   - `Get_returns_line_snapshots_and_permission_flags` (owner `CanEdit` true; a WAREHOUSE user viewing an admin's voucher → `CanEdit` false)
   - `List_scoping_filters_and_aggregates`:
     - scoped to the working branch and type;
     - one assertion block per filter (dates, warehouse on header or line, partner, reason, status, owner, search);
     - aggregates exclude Cancelled unless `status` is given.
   - `Access_rules`: a voucher of another branch → 403; a client with only `stock_in.view` listing `type=Out` → 403.
   - `Owners_and_activities` (owners lists the creators in the branch; activities newest first)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherQueryTests"`. Expected: FAIL (`NotImplementedException` → 500, or 404).
3. **Write the minimal implementation:** `ListAsync`, `ListOwnersAsync`, `ListActivitiesAsync`, the `CanEdit` / `CanCancel` / `CanDelete` flags in `GetAsync` (permission + ownership/edit_all + not Cancelled; `CanCancel` is true for a Cancelled voucher when restore is allowed), and the controller actions.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock voucher list, detail, owners and activity history"`

### Task 5.5 — Update with ownership, concurrency and period lock

1. **Write the failing test** `StockVouchers/StockVoucherUpdateTests.cs`:
   - `Owner_updates_voucher_and_ledger_is_replaced` (change qty and warehouse → ledger rows match the new lines; `StockBalance` of the old warehouse back to 0)
   - `Edit_rules`:
     - WAREHOUSE user B editing admin A's voucher → 403; a client with `stock_in.edit` + `stock_in.edit_all` → 200;
     - a missing `Version` → 400;
     - a Cancelled voucher → 409.
   - `Stale_version_returns_409_concurrency` (two PUTs with the same `Version` → the second returns 409, `error.code == "CONCURRENCY"`)
   - `Line_only_edit_still_checks_the_version` (D29): the first PUT changes only one line's warehouse (header totals unchanged) → 200 and a new `Version`; a second PUT with the original version that edits only a line note → 409 `CONCURRENCY`
   - `Unchanged_line_with_a_deactivated_product_stays_editable` (review m15): deactivate the product of line 1, then edit only the voucher note → 200; replacing line 1's product with another inactive product → 400 `lines[0].productId`
   - `Moving_an_inbound_after_the_outbound_respects_policy` (review m17): under Block, moving the In that feeds an Out to a later date → 422 `NEGATIVE_STOCK_BLOCKED`; nothing changes
   - `Period_lock_applies_to_old_and_new_dates` (moving into a locked period → `PERIOD_LOCKED`; editing a voucher dated in a locked period → `PERIOD_LOCKED`)
   - `Back_dated_edit_recalculates_and_rejected_edit_changes_nothing`:
     - In 10 @ 100,000 on 10-01, Out 5 on 10-10 → cost 500,000;
     - edit the In price to 120,000 → the Out ledger cost is 600,000;
     - under Block, reduce the In quantity to 3 → 422, and the voucher quantity and ledger are unchanged.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherUpdateTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `UpdateAsync` (the "Update" row of the steps table, line upsert), the controller `PUT`, and the middleware arm before `DomainException`:
   ```csharp
   DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, new ApiError
       { Code = "CONCURRENCY", Message = "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại." }),
   ```
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): update stock vouchers with ownership, optimistic concurrency and back-dated recalculation"`

### Task 5.6 — Cancel, restore and delete

1. **Write the failing test** `StockVouchers/StockVoucherLifecycleTests.cs`:
   - `Cancel_and_restore_move_ledger_rows_and_log_activities`
   - `Lifecycle_permissions_and_period_lock`:
     - a client with `stock_in.view/create/edit` but no `cancel` → 403 on cancel;
     - cancel, restore and delete on a locked date → `PERIOD_LOCKED`.
   - `Policy_applies_to_cancel_and_restore`:
     - cancelling an inbound that feeds an outbound: Block → 422; Warn → 422 warning, then 200 with acknowledgement;
     - restoring an outbound without stock → 422.
   - `Delete_soft_deletes_and_cancelled_cannot_be_deleted` (GET → 404 afterwards; ledger rows removed; balance updated; deleting a Cancelled voucher → 409)
   - `Delete_inbound_feeding_outbound_respects_policy` (review m17): Block → 422 and nothing changes; Warn → 422 warning, then 200 with `acknowledgeNegativeStock=true` in the query string
   - `Cost_price_follows_the_latest_active_stock_in` (D35): In on 10-01 @ 40,000 and In on 10-05 @ 50,000 → `CostPrice` 50,000; cancel the 10-05 voucher → 40,000 and `CostPriceUpdatedOn` = 10-01; restore it → 50,000; correct a mistyped 2027-10-05 voucher to 2026-10-03 → the stamp follows the corrected latest voucher
   - `Restore_reposts_the_stored_lines_unchanged`: deactivate the product after cancelling, restore → 200, the line snapshots are unchanged
   - Every test ends with `AssertInvariantsAsync()`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherLifecycleTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `CancelAsync`, `RestoreAsync`, `DeleteAsync` (the "Cancel / Delete" and "Restore" rows of the steps table) and their controller actions (DELETE binds `[FromQuery] StockVoucherActionRequest`).
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): cancel, restore and delete stock vouchers"`

### Task 5.7 — Form helpers: defaults, stock-at and partner search

Rules:
- **Defaults.** `VoucherAt` = the query value (normalized to UTC), else (`DefaultDateMode.Now` → `IDateTime.UtcNow`; `PreviousVoucher` → `VoucherAt` of the current user's latest-created voucher of the type in the branch, else `UtcNow`). Never use `IDateTime.Now` (D27). Query-string instants must carry an offset (`…Z` or `+07:00`); the frontend always sends ISO instants. `WarehouseId` / `ReasonId` / `PaymentMethodId` come from that latest voucher. The fallbacks are the first active warehouse of the branch by Code, the first reason of the direction (system first, by Code) and null. `NextCode` = `Format(…, PeekNextAsync(...), VN date of VoucherAt)`.
- **Stock-at.** `Σ(QtyIn − QtyOut)` over ledger rows of the working branch with `PostedAt <= VnTime.ToUtc(At)` and `SourceId != ExcludeVoucherId` (null for a new voucher), grouped by the requested `(ProductId, WarehouseId)`; missing pairs return 0.
- **Partners.** With `reasonId`: load the reason (its `Direction` must equal `type`, else 400) and use `reason.PartnerType`. Without `reasonId` (list-page filter): use `PartnerType.Any`. Then call `ICustomerService.SearchAsync(new CustomerSearchRequest { Keyword, Limit, ActiveOnly = false }, partnerType)`; `ActiveOnly = true` only when `reasonId` is given (the form picks active partners, while filters must also find inactive ones).

1. **Write the failing test** `StockVouchers/StockVoucherHelpersTests.cs`:
   - `Defaults_return_next_code_and_last_used_selections` (including the next code for a requested date under monthly numbering)
   - `Defaults_follow_previous_voucher_date_mode`
   - `Stock_at_respects_time_and_excludes_own_voucher`
   - `Partner_search_scoping`:
     - `NMH` → suppliers only; `XBH` → customers only; `NKH` → both;
     - no `reasonId` → any role;
     - a client with only `stock_out.view` → 200.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherHelpersTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `GetDefaultsAsync`, `GetStockAtAsync`, `SearchPartnersAsync` and their controller actions.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock voucher form defaults, stock-at lookup and partner search"`

### Task 5.8 — Concurrency and rollback guarantees

1. **Write the failing test** `StockVouchers/StockVoucherConcurrencyTests.cs`:
   - `Parallel_creates_get_distinct_sequential_codes` (5 admin clients from `CloneAdminClient()` — no extra logins — POST in parallel → codes `PN00001`..`PN00005`, no 409 / 500)
   - `Parallel_outbounds_cannot_overdraw_under_block_policy` (stock 10; two parallel Out of 6 → exactly one 200 and one 422; balance 4)
   - `Rejected_create_leaves_no_voucher_ledger_or_counter_change` (Block failure → no `stock_vouchers` row, ledger unchanged, `GET defaults` still returns `PX00001`)
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~StockVoucherConcurrencyTests"`. Expected: FAIL if any ordering or locking gap exists. If it already passes, record that in the commit message and keep the tests as regression guards; a deliberate RED is not required for pure regression tests.
3. **Write the minimal implementation:** fix whatever fails. Typical causes: locks taken after the first `SaveChanges`, or the counter used outside the transaction.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockVouchers"`. Expected: PASS.
5. **Commit:** `git commit -m "test(inventory): concurrency and rollback guarantees for stock vouchers"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockVouchers"`
- `dotnet test OrderMgmt.sln`

## Exit Criteria

- Every endpoint in the contract table works with the documented permissions and branch scoping.
- Totals, codes, ledger rows, costs and cost prices match the Phase 03 / 04 expectations through the HTTP API.
- Negative stock gives 422 (warning/blocked) only when an operation worsens a pair (D31), stale versions give 409 `CONCURRENCY` even for line-only edits (D29), and locked dates give 400 `PERIOD_LOCKED`.
- Instants are stored in UTC whatever offset the client sends (D27); `Product.CostPrice` follows the latest Active stock-in (D35).
- Parallel saves never duplicate codes or overdraw stock under Block, and failed saves leave no partial state.
