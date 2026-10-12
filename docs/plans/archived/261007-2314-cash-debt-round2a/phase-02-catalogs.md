# Phase 02 — Catalogs: cash reasons, funds, fund kind, reason debt effect, credit days & limits

**Status:** [x] complete
**Complexity:** L

## Objective

Add every catalog Round 2A reads:

- `CashReason` with its system seeds;
- `Fund` per branch, with a default cash fund `QTM` for every branch;
- `PaymentMethod.FundKind`, replacing `IsCash`;
- `StockReason.DebtEffect`, with seeds `NTL`/`XTL` and DebtEffect values for the existing reasons;
- `Customer.CreditDays`;
- `CreditLimit` per customer × branch.

The pure `DebtEffectRules` (P19) are built first because the validators use them.

## Files

- `backend/src/OrderMgmt.Domain/Enums/CashDebtEnums.cs` (modify — `DebtEffect`, `DebtSide`, `DebtDirection`, `CashVoucherType`, `FundKind`)
- `backend/src/OrderMgmt.Domain/Entities/CashDebt/{CashReason,Fund,CreditLimit}.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Inventory/{PaymentMethod,StockReason}.cs` (modify)
- `backend/src/OrderMgmt.Domain/Entities/Catalog/Customer.cs` (modify — `CreditDays`)
- `backend/src/OrderMgmt.Application/CashDebt/Common/DebtEffectRules.cs` (new)
- `backend/src/OrderMgmt.Application/CashDebt/{CashReasons,Funds,CreditLimits}/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Inventory/PaymentMethods/**` (modify)
- `backend/src/OrderMgmt.Application/Inventory/StockReasons/**` (modify)
- `backend/src/OrderMgmt.Application/Catalog/Customers/**` (modify — `CreditDays` in DTOs/requests/validators/search)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — default fund on create)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/{CashDebtConfiguration,InventoryConfiguration,CatalogConfiguration}.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_{AddCashReasons,AddFunds,AddPaymentMethodFundKind,AddStockReasonDebtEffect,AddCustomerCreditDaysAndCreditLimits}.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/{CashReasonsController,FundsController,CreditLimitsController}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/DebtEffectRulesTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/{CashReasonCrudTests,FundCrudTests,PaymentMethodFundKindTests,StockReasonDebtEffectTests,CreditLimitCrudTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/PaymentMethodCrudTests.cs`, `Catalog/PartnerRoleTests.cs` (modify where `IsCash` / DTO shapes change)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Inventory/StockReasons/Services/StockReasonService.cs` — system/used locks
- `backend/src/OrderMgmt.Application/Inventory/Warehouses/**`, `WarehousesController.cs` — branch-scoped catalog pattern
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{StockReasonCrudTests,WarehouseCrudTests}.cs`

## Tasks

### Task 2.1 — Enums and `DebtEffectRules`

```csharp
// Domain/Enums/CashDebtEnums.cs (append)
public enum DebtEffect { None = 0, ReceivableDecrease = 1, ReceivableIncrease = 2, PayableDecrease = 3, PayableIncrease = 4 }
public enum DebtSide { Receivable = 1, Payable = 2 }
public enum DebtDirection { Increase = 1, Decrease = 2 }
public enum CashVoucherType { Receipt = 1, Payment = 2 }
public enum FundKind { None = 0, Cash = 1, Bank = 2 }

// Application/CashDebt/Common/DebtEffectRules.cs
public static class DebtEffectRules
{
    public static (DebtSide Side, DebtDirection Direction)? Resolve(DebtEffect effect);          // None → null
    public static DebtEffect Of(DebtSide side, DebtDirection direction);
    public static bool IsAllowedForCash(CashVoucherType type, DebtEffect effect);               // P19
    public static bool IsAllowedForStock(StockDirection direction, DebtEffect effect);          // P19
    public static bool IsAllowedPartnerType(DebtEffect effect, PartnerType partnerType);        // Receivable* → Customer|Any; Payable* → Supplier|Any; None → any
    public static bool PartnerHasSideRole(DebtSide side, bool isCustomer, bool isSupplier);     // Receivable → isCustomer; Payable → isSupplier
    public static CashVoucherType AutoVoucherType(StockDirection direction);                    // Out → Receipt; In → Payment
    public static DebtDirection Opposite(DebtDirection direction);
}
```

1. **Write the failing tests** `CashDebt/Unit/DebtEffectRulesTests.cs`:
   - `Resolve_maps_every_effect` (`[Theory]` with 5 rows)
   - `Cash_combinations` (`[Theory]`: Receipt allows None/RD/PI and rejects RI/PD; Payment allows None/PD/RI and rejects RD/PI)
   - `Stock_combinations` (`[Theory]`: In allows None/PI/RD; Out allows None/RI/PD)
   - `Partner_type_combinations` (RD + Supplier → false; RD + Any → true; PI + Customer → false; None + None → true)
   - `Auto_voucher_type_and_opposite_direction`
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DebtEffectRulesTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation.**
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(debt): debt effect enums and combination rules"`

### Task 2.2 — Cash reason catalog with system seeds

Entity `CashReason : BaseEntity`:
- Fields: `Code`, `Name`, `CashVoucherType Type`, `PartnerType PartnerType`, `DebtEffect DebtEffect`, `bool IsSystem`, `bool IsAuto`.
- Table `cash_reasons`; `Code` is unique (filtered `is_deleted = false`); query filter `!IsDeleted`.

Seed `DbSeeder.SeedInventoryReferenceDataAsync`. Insert a row only when its code is missing; all rows `IsSystem = true`.

| Code | Name | Type | PartnerType | DebtEffect | IsAuto |
|---|---|---|---|---|---|
| TKH | Thu tiền khách hàng | Receipt | Customer | ReceivableDecrease | false |
| THNCC | Thu hoàn tiền NCC | Receipt | Supplier | PayableIncrease | false |
| TK | Thu khác | Receipt | Any | None | false |
| TNCC | Trả tiền nhà cung cấp | Payment | Supplier | PayableDecrease | false |
| CHKH | Chi hoàn tiền khách hàng | Payment | Customer | ReceivableIncrease | false |
| CK | Chi khác | Payment | Any | None | false |
| TPX | Thu tiền theo phiếu xuất | Receipt | Any | None | true |
| CPN | Chi tiền theo phiếu nhập | Payment | Any | None | true |

API `CashReasonsController` (route `api/cash-reasons`):

| Method | Route | Guard | Notes |
|---|---|---|---|
| GET | `/?type=&includeAuto=false` | `[Authorize]` | Ordered by Type, Code. `IsAuto` rows are returned only with `includeAuto=true`. |
| GET | `/{id}` | `[Authorize]` | |
| POST | `/` | `cash.catalogs.manage` | `CreateCashReasonRequest { Code, Name, Type, PartnerType, DebtEffect }`. Always `IsSystem = false`, `IsAuto = false`. |
| PUT | `/{id}` | `cash.catalogs.manage` | `UpdateCashReasonRequest { Name, Type, PartnerType, DebtEffect }` |
| DELETE | `/{id}` | `cash.catalogs.manage` | Soft delete. |

DTO: `CashReasonDto { Id, Code, Name, Type, PartnerType, DebtEffect, IsSystem, IsAuto }`.

Rules (P19). Collect them into one `ValidationDomainException`:
- `!IsAllowedForCash(Type, DebtEffect)` → key `debtEffect`.
- `!IsAllowedPartnerType(DebtEffect, PartnerType)` → key `partnerType`.
- `PartnerType = None` with `DebtEffect ≠ None` → key `partnerType`.
- A system reason: changing `Type` or `PartnerType` → 409; delete → 409; renaming is allowed.
- An `IsAuto` reason cannot be updated at all → 409.
- The "used by a cash voucher" lock is added in Phase 05 Task 5.9.

1. **Write the failing tests** `CashDebt/CashReasonCrudTests.cs`:
   - `Seeded_system_reasons_and_auto_filter` (8 rows with `includeAuto=true`, 6 without; `TKH` has `ReceivableDecrease`)
   - `Crud_happy_path_and_guard` (create a Payment reason "Chi phí vận chuyển" with Any/None → list → update name → delete; SALES POST → 403)
   - `Invalid_combinations_return_400` (Receipt + PayableDecrease → `debtEffect`; ReceivableDecrease + Supplier → `partnerType`)
   - `System_and_auto_reason_rules` (delete TKH → 409; change TKH type → 409; rename TKH → 200; update TPX → 409)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashReasonCrudTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, DbSet, migration `AddCashReasons`, seed, service, validators (`Code` NotEmpty max 20, `Name` NotEmpty max 255, enums `IsInEnum`), controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): cash reason catalog with debt effect and system seeds"`

### Task 2.3 — Fund catalog with a default fund per kind

Entity `Fund : BaseEntity`:
- Fields: `Guid BranchId` (+ `Branch?`), `Code`, `Name`, `FundKind Kind`, `string? BankName`, `string? AccountNumber`, `bool IsDefault`, `decimal OpeningAmount` (`numeric(18,2)`, default 0), `bool IsActive` (`.HasDefaultValue(true).HasSentinel(true)`, D28).
- Table `funds`; unique `(BranchId, Code)` filtered; FK branch `Restrict`.

Seeding (P18):
- `DbSeeder.SeedInventoryReferenceDataAsync` adds fund `QTM` "Quỹ tiền mặt" (Cash, default) to every branch that has no fund.
- `BranchService.CreateAsync` adds the same fund.

API `FundsController` (route `api/funds`):

| Method | Route | Guard | Notes |
|---|---|---|---|
| GET | `/?kind=&isActive=` | `[Authorize]` | Working branch, ordered by Kind, Code. |
| GET | `/{id}` | `[Authorize]` | Another branch → 404. |
| POST | `/` | `cash.catalogs.manage` | `CreateFundRequest { Code, Name, Kind, BankName?, AccountNumber?, IsDefault, IsActive = true }`. Working branch only. `Kind = None` → 400 `kind`. |
| PUT | `/{id}` | `cash.catalogs.manage` | `UpdateFundRequest { Name, Kind, BankName?, AccountNumber?, IsDefault, IsActive }` |
| DELETE | `/{id}` | `cash.catalogs.manage` | Soft delete. |

DTO: `FundDto { Id, BranchId, Code, Name, Kind, BankName, AccountNumber, IsDefault, OpeningAmount, IsActive }`.

Rules (P18):
- The first fund of a (branch, kind) is stored as default even if `IsDefault = false`.
- Setting `IsDefault = true` clears `IsDefault` on the other funds of that (branch, kind) in the same transaction.
- `IsDefault = false` on the current default → 400 `isDefault` ("Chọn quỹ mặc định khác trước.").
- A default fund cannot be made inactive → 400 `isActive`.
- Delete:
  - the default fund → 409 when another fund of the kind exists;
  - a fund with `OpeningAmount ≠ 0` → 409;
  - reference checks (stock voucher `FundId`, cash vouchers, fund transfers) are added in Phases 05, 06 and 07.
- Changing `Kind` follows the same "referenced or opening ≠ 0" rule → 409 (method `EnsureNotUsedAsync`, extended later).

1. **Write the failing tests** `CashDebt/FundCrudTests.cs`:
   - `Seed_and_new_branch_have_default_cash_fund` (main branch `QTM` default Cash; a new branch gets one)
   - `Crud_happy_path_and_guard` (create `NH01` Bank with bank name/account → first bank fund is default → update → delete; SALES POST → 403; GET from branch B does not list main-branch funds)
   - `Default_fund_rules`:
     - create `NH02` Bank with `IsDefault = true` → `NH01` is no longer default;
     - set `NH02` `IsDefault = false` → 400;
     - deactivate `NH02` → 400;
     - delete `QTM` while `QTM2` (Cash) exists → 409.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~FundCrudTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, DbSet, migration `AddFunds`, seed, service (`EnsureNotUsedAsync` checks only `OpeningAmount` for now), validators (`Code` max 20, `Name` max 255, `BankName` max 255, `AccountNumber` max 50), controller, DI, and the `BranchService.CreateAsync` change.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~FundCrudTests|FullyQualifiedName~BranchCrudTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): funds per branch with one default per kind"`

### Task 2.4 — `PaymentMethod.FundKind` replaces `IsCash`

Changes:
- `PaymentMethod.IsCash` → `FundKind FundKind` (int, default 0).
- Migration `AddPaymentMethodFundKind`:
  - add `fund_kind`;
  - `UPDATE payment_methods SET fund_kind = 1 WHERE is_cash; UPDATE payment_methods SET fund_kind = 2 WHERE code = 'CK' AND NOT is_cash;` (P9);
  - drop `is_cash`.
  - `Down`: add `is_cash`, `UPDATE … SET is_cash = (fund_kind = 1)`, drop `fund_kind`.
- Seed: `TM` = Cash, `CK` = Bank.
- DTOs and requests: `IsCash` → `FundKind`.

1. **Write the failing test** `CashDebt/PaymentMethodFundKindTests.cs`: `Seeded_methods_have_fund_kind_and_crud_roundtrips` (TM = Cash, CK = Bank; create `QR` with `fundKind: "Bank"` → GET returns it). Update `Inventory/PaymentMethodCrudTests.cs` to send `fundKind` instead of `isCash`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~PaymentMethodFundKindTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** entity, config, migration with the data SQL, seed, DTOs, service, and the frontend-independent backend tests.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~PaymentMethod"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(cash): payment method fund kind replaces is-cash flag"`

### Task 2.5 — `StockReason.DebtEffect`, seeds `NTL`/`XTL`

Changes:
- `StockReason.DebtEffect` (int, default 0).
- Migration `AddStockReasonDebtEffect`: `UPDATE stock_reasons SET debt_effect = 2 WHERE code = 'XBH'; UPDATE stock_reasons SET debt_effect = 4 WHERE code = 'NMH';`.
- The seeder adds (when missing, `IsSystem = true`):
  - `NTL` "Nhập hàng bán bị trả lại" (In, Customer, ReceivableDecrease);
  - `XTL` "Xuất trả hàng NCC" (Out, Supplier, PayableDecrease).
- DTOs and requests gain `DebtEffect` (request default `None`).

Rules (P19, in `StockReasonService`):
- `!IsAllowedForStock(Direction, DebtEffect)` → 400 `debtEffect`.
- `!IsAllowedPartnerType(DebtEffect, PartnerType)` or (`PartnerType = None` and `DebtEffect ≠ None`) → 400 `partnerType`.
- A reason used by a non-deleted stock voucher cannot change `DebtEffect` → 400 `debtEffect` (same check and message as the existing direction/partner-type lock).
- System reasons may change `DebtEffect` only while unused.

1. **Write the failing tests** `CashDebt/StockReasonDebtEffectTests.cs`:
   - `Seeds_have_debt_effect` (XBH = ReceivableIncrease, NMH = PayableIncrease, NKH/XKH = None, NTL = ReceivableDecrease, XTL = PayableDecrease)
   - `Invalid_combinations_return_400` (Out + PayableIncrease → `debtEffect`; In + ReceivableDecrease + Supplier → `partnerType`)
   - `Used_reason_keeps_debt_effect` (create a custom Out/Any/None reason, use it on a stock-out voucher, PUT `DebtEffect = ReceivableIncrease` → 400 `debtEffect`; an unused system reason `XTL` → change to None → 200)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockReasonDebtEffectTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, migration with data SQL, seed, DTOs, service rules.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~StockReason"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): debt effect on stock reasons with return reasons NTL/XTL"`

### Task 2.6 — `Customer.CreditDays` and the credit limit catalog

Changes:
- `Customer.CreditDays` (`int?`). Validator: `InclusiveBetween(0, 3650)` when set.
- Add it to `CustomerDto`, `CustomerListItemDto`, `CustomerSearchItemDto`, `CreateCustomerRequest` and `UpdateCustomerRequest`, with mappings in `CustomerService`.
  - On update, the value replaces the stored one (null clears it), like `TaxCode` and the other optional fields. Only `IsCustomer`/`IsSupplier` keep the null-means-keep rule.
- Entity `CreditLimit : BaseEntity`: `Guid CustomerId` (+ `Customer?`), `Guid BranchId`, `decimal Amount` (`numeric(18,2)`). Table `credit_limits`; unique `(CustomerId, BranchId)` filtered.
- One migration `AddCustomerCreditDaysAndCreditLimits`.

API `CreditLimitsController` (route `api/credit-limits`, all `credit_limit.manage`, working branch):

| Method | Route | Notes |
|---|---|---|
| GET | `/?search=&page=&pageSize=` | `CreditLimitListResult : PagedResult<CreditLimitDto>`, ordered by customer code |
| PUT | `/{customerId}` | `UpsertCreditLimitRequest { Amount }` (≥ 0). The customer must have `IsCustomer` (400 `customerId`). Creates or updates. |
| DELETE | `/{customerId}` | Soft delete; 404 if none. |

DTO: `CreditLimitDto { CustomerId, CustomerCode, CustomerName, CreditDays, Amount }`.

1. **Write the failing tests** `CashDebt/CreditLimitCrudTests.cs`:
   - `Credit_days_roundtrip_and_search_item` (POST customer with `creditDays: 30` → GET returns 30; `/api/customers/search` item has `creditDays`; `creditDays: -1` → 400)
   - `Upsert_list_delete_per_branch` (PUT 50,000,000 in main → list shows it; branch B list is empty; PUT again 60,000,000 updates; DELETE → list empty; a supplier-only partner → 400; SALES → 403)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CreditLimitCrudTests"`. Expected: FAIL.
3. **Write the minimal implementation:** fields, entity, configs, migration, customer service mappings, credit limit service, validators, controller, DI.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~CreditLimitCrudTests|FullyQualifiedName~Customer|FullyQualifiedName~PartnerRoleTests"`. Expected: PASS (except baseline failures).
5. **Commit:** `git commit -m "feat(debt): customer credit days and credit limits per branch"`

## Verification

```bash
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt|FullyQualifiedName~StockReason|FullyQualifiedName~PaymentMethod|FullyQualifiedName~Customer"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
```

## Exit Criteria

- The cash reason, fund and credit limit APIs work with their rules.
- `PaymentMethod.FundKind` and `StockReason.DebtEffect` are migrated and seeded (incl. `NTL`/`XTL`).
- Every branch has a default `QTM` fund.
- `CreditDays` is on partner DTOs and in search items.
- No new failures against `BASELINE.md`.
