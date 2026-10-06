# Phase 02 — Inventory catalogs, partner roles & settings

**Status:** [x] complete
**Complexity:** L

## Objective

Add the catalogs Round 1 needs (warehouses, stock reasons, payment methods), partner roles on `Customer` with a `/api/suppliers` API and the D2 permission rule, inventory fields on `Product`, the `InventorySettings` singleton, `DocumentNumbering` with its pure formatter, and the seeds (main warehouse, system reasons, payment methods, default numbering). Also add the shared integration-test base `InventoryTestBase`.

## Files

- `backend/src/OrderMgmt.Domain/Enums/InventoryEnums.cs` (new — grows in this phase)
- `backend/src/OrderMgmt.Domain/Enums/Enums.cs` (modify — `PartnerRole`)
- `backend/src/OrderMgmt.Domain/Common/DomainException.cs` (modify — `ValidationDomainException` message overload)
- `backend/src/OrderMgmt.Domain/Entities/Inventory/{Warehouse,StockReason,PaymentMethod,InventorySettings,DocumentNumbering}.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Catalog/{Customer,Product}.cs` (modify)
- `backend/src/OrderMgmt.Application/Common/Interfaces/IAppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Application/Inventory/Warehouses/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Inventory/StockReasons/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Inventory/PaymentMethods/{Interfaces,Models,Services,Validators}/*` (new)
- `backend/src/OrderMgmt.Application/Inventory/Settings/{Interfaces,Models,Services,Validators}/*` (new — settings + numbering)
- `backend/src/OrderMgmt.Application/Inventory/Numbering/DocumentNumberFormatter.cs` (new)
- `backend/src/OrderMgmt.Application/Catalog/Customers/**` (modify — roles, guard, search role)
- `backend/src/OrderMgmt.Application/Search/Services/SearchService.cs`, `Search/Models/GlobalSearchResultDto.cs` (modify — customers filtered by `IsCustomer`, new supplier group, D38)
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationService.cs` (modify — `EnsureCustomerAsync` rejects non-customers, D38)
- `backend/src/OrderMgmt.Application/Catalog/Products/**` (modify — new fields)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify — delete check, default numbering on create)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/InventoryConfiguration.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CatalogConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_{AddWarehouses,AddStockReasons,AddPaymentMethods,AddPartnerRoles,AddProductInventoryFields,AddInventorySettings,AddDocumentNumbering}.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Controllers/{WarehousesController,StockReasonsController,PaymentMethodsController,SuppliersController,InventorySettingsController}.cs` (new)
- `backend/src/OrderMgmt.WebApi/Controllers/CustomersController.cs` (modify — pass `PartnerRole.Customer`)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/{WarehouseCrudTests,StockReasonCrudTests,PaymentMethodCrudTests,InventorySettingsTests,NumberingSettingsTests,InventorySeedTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Catalog/{PartnerRoleTests,ProductInventoryFieldsTests}.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/Unit/DocumentNumberFormatterTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Catalog/ProductGroups/**`, `ProductGroupsController.cs` — CRUD pattern
- `backend/src/OrderMgmt.Application/Catalog/Customers/Services/CustomerService.cs`, `CustomersController.cs`
- `backend/src/OrderMgmt.Application/Sales/Quotations/Services/QuotationSystemSettingsService.cs` — singleton settings pattern

## Tasks

### Task 2.1 — `InventoryTestBase` and Warehouse CRUD

Entity `Warehouse : BaseEntity` { `Code`, `Name`, `Guid BranchId`, `Branch? Branch`, `bool IsActive = true` } in table `warehouses`. Code is unique (filtered `is_deleted = false`). FK to branches is `Restrict`. Query filter `!IsDeleted`. `IsActive` has **no** database default, or `.HasDefaultValue(true).HasSentinel(true)` if one is added (D28) — otherwise an inactive warehouse could never be inserted.

API `WarehousesController` (route `api/warehouses`):

| Method | Route | Guard | Notes |
|---|---|---|---|
| GET | `/?branchId=&isActive=&search=` | `[Authorize]` | `branchId` is honoured only for `branches.access_all`; otherwise (or when omitted) the working branch is used. Ordered by Code. Returns `IReadOnlyList<WarehouseDto>`. |
| GET | `/{id}` | `[Authorize]` | |
| POST | `/` | `inventory.catalogs.manage` | `CreateWarehouseRequest { Code, Name, Guid? BranchId, bool IsActive = true }`. `BranchId` null → working branch. A branch other than the working branch requires `branches.access_all` (403). |
| PUT | `/{id}` | `inventory.catalogs.manage` | `UpdateWarehouseRequest { Name, Guid BranchId, bool IsActive }`. Same branch rule. |
| DELETE | `/{id}` | `inventory.catalogs.manage` | Soft delete. The ledger check is added in Phase 04 Task 4.8. |

`WarehouseDto { Id, Code, Name, BranchId, BranchCode, BranchName, IsActive }`.

1. **Write the failing tests:**
   - Create `Inventory/InventoryTestBase.cs : QuotationTestBase` with these helpers:
     - `protected static readonly Guid MainBranchId = BranchDefaults.MainBranchId;`
     - `protected Task InDbAsync(Func<AppDbContext, Task> f)` and `protected Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> f)` — new DI scope for each call.
     - `protected Task<HttpClient> CreateClientWithPermissionsAsync(string username, Guid? defaultBranchId, params string[] permissionCodes)` — inserts custom role `T_{username}` (`IsSystem = false`) with those permissions, plus a user with password `Pass@123`; returns a logged-in `_factory.CreateClient()`.
     - `protected HttpClient CloneAdminClient()` — a new `_factory.CreateClient()` carrying the existing admin `Authorization` header (no extra login). Use it whenever a test needs more admin clients (e.g. Phase 05 Task 5.8), because the login endpoint is rate-limited (Phase 00 raises the limit for tests, but extra logins stay unnecessary).
     - `protected Task<HttpClient> CreateClientForRoleAsync(string username, string roleCode, Guid? defaultBranchId = null)`.
     - `protected static void UseBranch(HttpClient client, Guid branchId)` — sets `X-Branch-Id`.
     - `protected Task<Guid> CreateBranchAsync(string code)` (admin, via API) and `protected Task<Guid> CreateWarehouseAsync(Guid branchId, string code)` (admin, via API; sets `X-Branch-Id` on a temporary request).
     - `protected async Task<T> ReadDataAsync<T>(HttpResponseMessage r)` — unwraps `ApiResponse<T>` with `TestJson.Options`.
   - `Inventory/WarehouseCrudTests.cs : InventoryTestBase`:
     - `Crud_happy_path`
     - `Listing_and_creating_follow_the_working_branch`:
       - W1 in main, W2 in branch B; plain GET returns W1 only, header B returns W2 only;
       - a client with only `inventory.catalogs.manage` gets main for `?branchId=B`, and 403 when creating in B;
       - SALES gets 403 on POST.
     - `Duplicate_code_and_branch_delete_conflicts`: duplicate code → 409; deleting a branch that has a warehouse → 409 (extends `BranchService.DeleteAsync`).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~WarehouseCrudTests"`. Expected: FAIL (compile/404).
3. **Write the minimal implementation:** entity, `WarehouseConfiguration`, `DbSet<Warehouse> Warehouses`, migration `AddWarehouses`, service/validators (`Code` NotEmpty, max 50; `Name` NotEmpty, max 255), controller, DI, and the branch delete check (`Warehouses.AnyAsync(w => w.BranchId == id)` → `ConflictException`).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~WarehouseCrudTests|FullyQualifiedName~BranchCrudTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): warehouse catalog scoped to working branch"`

### Task 2.2 — Stock reason CRUD

`InventoryEnums.cs` (namespace `OrderMgmt.Domain.Enums`): `public enum StockDirection { In = 1, Out = 2 }` and `public enum PartnerType { None = 0, Customer = 1, Supplier = 2, Any = 3 }`.

Entity `StockReason : BaseEntity` { `Code`, `Name`, `StockDirection Direction`, `PartnerType PartnerType`, `bool IsSystem` }, table `stock_reasons`. Code is unique (filtered); enums are stored as int.

API `StockReasonsController` (route `api/stock-reasons`): GET `/?direction=` `[Authorize]` (ordered by Direction, Code); GET `/{id}` `[Authorize]`; POST / PUT / DELETE guarded by `inventory.catalogs.manage`. `CreateStockReasonRequest { Code, Name, Direction, PartnerType }` and `UpdateStockReasonRequest { Name, Direction, PartnerType }`. Responses use `StockReasonDto { Id, Code, Name, Direction, PartnerType, IsSystem }` (never the entity); POST/PUT return the saved DTO. Enums serialize as strings (the app registers `JsonStringEnumConverter`). A system reason keeps its `Direction` and `PartnerType` (an attempt to change them → 409) and cannot be deleted (409). The voucher-reference delete check is added in Phase 04 Task 4.8.

1. **Write the failing tests** `Inventory/StockReasonCrudTests.cs`:
   - `Crud_happy_path_with_direction_filter_and_guard` (includes SALES POST → 403)
   - `System_reason_rules`: insert `IsSystem = true` via `InDbAsync`. Delete → 409; changing direction or partner type → 409; renaming → 200.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockReasonCrudTests"`. Expected: FAIL.
3. **Write the minimal implementation:** enums, entity, config, DbSet, migration `AddStockReasons`, service/validators (`Direction` and `PartnerType` `IsInEnum`), controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): stock reason catalog with partner type"`

### Task 2.3 — Payment method CRUD

Entity `PaymentMethod : BaseEntity` { `Code`, `Name`, `bool IsCash` }, table `payment_methods`. API `PaymentMethodsController` (route `api/payment-methods`): GET `[Authorize]`; POST/PUT/DELETE `inventory.catalogs.manage`. `CreatePaymentMethodRequest { Code, Name, IsCash }`, `UpdatePaymentMethodRequest { Name, IsCash }`. Responses use `PaymentMethodDto { Id, Code, Name, IsCash }`; POST/PUT return the saved DTO.

1. **Write the failing test** `Inventory/PaymentMethodCrudTests.cs`: `Crud_happy_path_duplicate_code_and_guard` (CRUD; duplicate code → 409; SALES POST → 403).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~PaymentMethodCrudTests"`. Expected: FAIL.
3. **Write the minimal implementation:** entity, config, DbSet, migration `AddPaymentMethods`, service, validators, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): payment method catalog"`

### Task 2.4 — Partner roles on `Customer` and `/api/suppliers`

Rules (D1, D2, D4):
- `Customer.IsCustomer` (default `true`) and `Customer.IsSupplier` (default `false`). At least one must be true after the change, otherwise 400 `PARTNER_ROLE_REQUIRED`.
- `/api/customers/*` works on `PartnerRole.Customer`; the new `/api/suppliers/*` works on `PartnerRole.Supplier`. List, get and search only return partners with that role; GET of a partner lacking the role → 404.
- Create via role R forces flag R = true. The other flag (from the request, default false) requires that role's `create` permission.
- Update requires `<role>.update` for every role in `rolesBefore ∪ rolesAfter`. Request flags that are null keep their current value.
- Delete requires `<role>.delete` for every role the partner has. The voucher-reference check is added in Phase 04 Task 4.8.
- Search: `SearchAsync(CustomerSearchRequest req, PartnerType role, ct)` — `Customer` → `IsCustomer`, `Supplier` → `IsSupplier`, `Any` → no role filter, `None` → empty list. `/api/customers/search` passes `Customer`, `/api/suppliers/search` passes `Supplier`.
- **Every other `Customers` query (D38).** `SearchService` (global header search) filters its customer group by `IsCustomer` and adds a `Suppliers` group (same shape) gated by `suppliers.view` and filtered by `IsSupplier`. `QuotationService.EnsureCustomerAsync` rejects a partner that is not a customer (`DomainException("PARTNER_NOT_CUSTOMER", "Đối tượng không phải khách hàng.")` → 400). Find the call sites with `grep -rn "\.Customers\b" backend/src/OrderMgmt.Application` (the `ICustomerService` grep misses direct DbSet queries).
- **Bool defaults (D28).** `IsCustomer` is configured `.HasDefaultValue(true).HasSentinel(true)` and `IsSupplier` `.HasDefaultValue(false)` — without the sentinel EF omits `IsCustomer = false` from the INSERT and the database stores `true`.

Implementation shape:

```csharp
// Domain/Enums/Enums.cs
public enum PartnerRole { Customer = 1, Supplier = 2 }

// Application/Catalog/Customers/Services/PartnerPermissionGuard.cs
internal static class PartnerPermissionGuard
{
    public static void EnsureCanCreate(ICurrentUser user, bool isCustomer, bool isSupplier);
    public static void EnsureCanUpdate(ICurrentUser user, bool wasCustomer, bool wasSupplier, bool isCustomer, bool isSupplier);
    public static void EnsureCanDelete(ICurrentUser user, bool isCustomer, bool isSupplier);
    // each throws ForbiddenException("Bạn không có quyền ... khách hàng/nhà cung cấp.")
}

// ICustomerService — every method gains a PartnerRole parameter (Search takes PartnerType):
Task<PagedResult<CustomerListItemDto>> ListAsync(CustomerListRequest request, PartnerRole role, CancellationToken ct = default);
Task<CustomerDto> GetAsync(Guid id, PartnerRole role, CancellationToken ct = default);
Task<List<CustomerSearchItemDto>> SearchAsync(CustomerSearchRequest request, PartnerType role, CancellationToken ct = default);
Task<CustomerDto> CreateAsync(CreateCustomerRequest request, PartnerRole role, CancellationToken ct = default);
Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, PartnerRole role, CancellationToken ct = default);
Task DeleteAsync(Guid id, PartnerRole role, CancellationToken ct = default);
```

DTOs: add `bool IsCustomer, IsSupplier` to `CustomerDto`, `CustomerListItemDto` and `CustomerSearchItemDto`; add `bool? IsCustomer, IsSupplier` to `CreateCustomerRequest` and `UpdateCustomerRequest`. `SuppliersController` (route `api/suppliers`) mirrors `CustomersController` with `[HasPermission(Permissions.Suppliers.*)]` and `PartnerRole.Supplier`. Update every other `ICustomerService` call site (`grep -rn "ICustomerService\|_customers\." backend/src`) to pass `PartnerRole.Customer` / `PartnerType.Customer`.

1. **Write the failing tests** `Catalog/PartnerRoleTests.cs : InventoryTestBase`. First add helper `protected Task<Guid> CreatePartnerAsync(string code, bool isCustomer, bool isSupplier)` to `InventoryTestBase` (inserts via `InDbAsync`).
   - `Role_endpoints_create_list_and_get_their_own_role`: POST `/api/customers` → customer-only; POST `/api/suppliers` → supplier-only. Lists are filtered by role (customer-only, supplier-only and dual partners). GET of a supplier-only partner via `/api/customers/{id}` → 404.
   - `Sales_user_cannot_touch_the_supplier_role`: SALES POST `/api/customers` with `isSupplier: true` → 403; SALES PUT on a dual-role partner → 403.
   - `Updating_needs_update_permission_of_every_role`: a client with `customers.view/update` + `suppliers.view/update` updates a dual-role partner → 200. A client with only `suppliers.*` turning on `isCustomer` → 403.
   - `Deleting_dual_role_partner_requires_both_delete_permissions`: a client with only `customers.delete` → 403; admin → 200.
   - `Role_validation_and_search_scoping`: PUT with both flags false → 400 `PARTNER_ROLE_REQUIRED`. `/api/customers/search` returns customers only; `/api/suppliers/search` returns suppliers only.
   - `Supplier_only_partner_is_persisted_as_not_a_customer` (D28 regression): POST `/api/suppliers` → reload from the DB → `IsCustomer == false`.
   - `Global_search_and_quotations_respect_roles` (D38): a SALES client's `/api/search/global?q=` does not return a supplier-only partner in `customers`; an admin sees it under `suppliers`; creating a quotation for a supplier-only partner → 400 `PARTNER_NOT_CUSTOMER`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~PartnerRoleTests"`. Expected: FAIL (compile/404).
3. **Write the minimal implementation:** entity fields; `CustomerConfiguration` with `.HasDefaultValue(true).HasSentinel(true)` / `.HasDefaultValue(false)`; migration `AddPartnerRoles` (existing rows get `is_customer = true`, `is_supplier = false`); guard; service and controller changes; `SuppliersController`; the `SearchService` and `QuotationService.EnsureCustomerAsync` changes (D38).
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~PartnerRoleTests|FullyQualifiedName~CustomerCrudTests|FullyQualifiedName~CustomerSearchTests|FullyQualifiedName~Quotation"`. Expected: PASS (except the baseline failures listed in SUMMARY.md).
5. **Commit:** `git commit -m "feat(partners): customer/supplier roles with role-based permissions and suppliers API"`

### Task 2.5 — Inventory fields on `Product`

Fields: `bool TrackInventory = true`, `decimal PurchaseDiscountRate` (`numeric(5,2)`, default 0), `decimal SalesDiscountRate` (same), `bool PriceIncludesVat = false`, `DateTimeOffset? CostPriceUpdatedOn` (set only by stock-in vouchers, never by the product API).

DTO changes:
- `ProductDto`, `CreateProductRequest`, `UpdateProductRequest`: add the first four.
- `ProductListItemDto`: add `TrackInventory`, `DefaultTaxRate`, `Length`, `Width`, `Thickness`, `PurchaseDiscountRate`, `SalesDiscountRate`, `PriceIncludesVat` (the frontend catalog dialog builds a product suggestion from a list row, so the row must carry every autofill field — review M14).
- `ProductSuggestionDto`: add `DefaultTaxRate`, `Length`, `Width`, `Thickness`, `TrackInventory`, `PurchaseDiscountRate`, `SalesDiscountRate`, `PriceIncludesVat`.
- Validators: both rates `InclusiveBetween(0, 100)`.
- **Bool defaults (D28):** `TrackInventory` is configured `.HasDefaultValue(true).HasSentinel(true)`; `PriceIncludesVat` `.HasDefaultValue(false)`.

1. **Write the failing tests** `Catalog/ProductInventoryFieldsTests.cs : QuotationTestBase`:
   - `Inventory_fields_default_roundtrip_and_appear_in_search`: a create without the fields → `TrackInventory = true`, rates 0, `PriceIncludesVat = false`; an update roundtrips all four; `/api/products/search` returns the new suggestion fields.
   - `Discount_rate_above_100_returns_400`
   - `Untracked_product_is_persisted_as_untracked` (D28 regression): POST with `trackInventory: false` → reload from the DB → `TrackInventory == false`; the list row and the suggestion carry the new fields.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~ProductInventoryFieldsTests"`. Expected: FAIL (compile).
3. **Write the minimal implementation:** entity, config (`HasDefaultValue`), mappings in `ProductService` (list, get, search, create, update), validators. Generate migration `AddProductInventoryFields`, then append:
   ```csharp
   migrationBuilder.Sql("UPDATE products SET track_inventory = false WHERE product_group_id IN (SELECT id FROM product_groups WHERE code = 'VC');");
   ```
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~ProductInventoryFieldsTests|FullyQualifiedName~ProductCrudTests"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(products): inventory tracking, purchase/sales discount and VAT-inclusive price fields"`

### Task 2.6 — `InventorySettings` singleton

Add to `InventoryEnums.cs`: `CostingMethod { PeriodicAverage = 1, Fifo = 2 }`, `CostingPeriod { Month = 1, Quarter = 2, Year = 3 }`, `CostingScope { Branch = 1, Warehouse = 2 }`, `NegativeStockPolicy { Allow = 0, Warn = 1, Block = 2 }`, `DefaultDateMode { Now = 0, PreviousVoucher = 1 }`.

Entity `InventorySettings` (not `BaseEntity`): `int Id`, `CostingMethod`, `CostingPeriod`, `CostingScope`, `bool PurchaseCostIncludesVat`, `NegativeStockPolicy`, `bool NetExcludesVat`, `DefaultDateMode`, `DateTimeOffset UpdatedAt`, `Guid? UpdatedBy`. Table `inventory_settings`, `Id` never generated. `HasData` Id = 1 with PeriodicAverage, Month, Branch, `true`, **Warn** (D12), `false`, Now, `UpdatedAt = UnixEpoch`.

API `InventorySettingsController` (route `api/inventory`): `GET settings` `[Authorize]` → `InventorySettingsDto { CostingMethod, CostingPeriod, CostingScope, PurchaseCostIncludesVat, NegativeStockPolicy, NetExcludesVat, DefaultDateMode, UpdatedAt }`; `PUT settings` `inventory.settings` with `UpdateInventorySettingsRequest` (the seven settings fields) → the updated `InventorySettingsDto`. The validator rejects `CostingMethod.Fifo` ("Phương pháp FIFO chưa được hỗ trợ."). Service `IInventorySettingsService { GetAsync; UpdateAsync }`. Phase 06 Task 6.3 adds recalculation to `UpdateAsync`.

1. **Write the failing tests** `Inventory/InventorySettingsTests.cs : InventoryTestBase`:
   - `Defaults_are_readable_by_anyone_and_update_persists` (SALES GET → 200 with the D12 defaults; admin PUT persists every field)
   - `Update_rejects_fifo_and_requires_permission` (Fifo → 400; SALES PUT → 403)

   Add helper `protected Task UpdateInventorySettingsAsync(Action<InventorySettings> mutate)` (direct DB update) to `InventoryTestBase`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~InventorySettingsTests"`. Expected: FAIL.
3. **Write the minimal implementation:** enums, entity, config with `HasData`, DbSet, migration `AddInventorySettings`, service, validator, controller, DI.
4. **Run tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): inventory settings singleton"`

### Task 2.7 — Document numbering (pure formatter + entity + API + seed)

Add to `InventoryEnums.cs`: `DocumentType { StockIn = 1, StockOut = 2 }` and `NumberingResetPolicy { None = 0, Monthly = 1, Yearly = 2 }`.

```csharp
// Application/Inventory/Numbering/DocumentNumberFormatter.cs
public static class DocumentNumberFormatter
{
    // Tokens: {KH} prefix, {STT} counter padded to length (never truncated), {THANG} MM, {NAM} yyyy.
    public static string Format(string pattern, string prefix, int length, long counter, DateOnly date);
    // None → "", Monthly → "yyyy-MM", Yearly → "yyyy"
    public static string PeriodKey(NumberingResetPolicy policy, DateOnly date);
    // Errors (Vietnamese): missing {STT}; Monthly without {THANG} or {NAM}; Yearly without {NAM}; length outside 1..10.
    public static IReadOnlyList<string> Validate(string pattern, int length, NumberingResetPolicy policy);
}
```

Entity `DocumentNumbering` (not `BaseEntity`): `Guid Id`, `DocumentType DocType`, `Guid BranchId`, `string Prefix` (max 20), `int Length`, `NumberingResetPolicy ResetPolicy`, `string Pattern` (max 100), `DateTimeOffset UpdatedAt`, `Guid? UpdatedBy`. Unique index on `(DocType, BranchId)`.

API (same `InventorySettingsController`): `GET numbering` (`inventory.settings`) → `IReadOnlyList<DocumentNumberingDto { DocType, Prefix, Length, ResetPolicy, Pattern }>` for the working branch; `PUT numbering/{docType}` (`inventory.settings`) with `UpdateNumberingRequest { Prefix, Length, ResetPolicy, Pattern }` → the updated `DocumentNumberingDto`. Errors from `Validate` → `ValidationDomainException` (400) keyed `pattern`, with the first error as the exception message (see the SUMMARY validation-key convention).

Defaults (D13): StockIn `PN`, StockOut `PX`, length 5, `None`, `{KH}{STT}`. `DbSeeder.SeedInventoryReferenceDataAsync` creates the missing rows for every branch, and `BranchService.CreateAsync` adds them for a new branch.

1. **Write the failing tests:**
   - `Inventory/Unit/DocumentNumberFormatterTests.cs` (pure):
     - `Format_default_pattern` → `("{KH}{STT}", "PN", 5, 12, 2026-10-06)` = `"PN00012"`
     - `Format_with_year_and_month` → `("{KH}{NAM}{THANG}-{STT}", "PX", 4, 7, 2026-03-01)` = `"PX202603-0007"`
     - `Counter_longer_than_length_is_not_truncated` → `("{KH}{STT}", "PN", 3, 12345, …)` = `"PN12345"`
     - `PeriodKey_per_policy` → `""`, `"2026-03"`, `"2026"`
     - `Validate_rejects_missing_tokens_and_bad_length` (one case per rule) and `Validate_accepts_valid_monthly_pattern`
   - `Inventory/NumberingSettingsTests.cs : InventoryTestBase`:
     - `Every_branch_has_default_numbering_scoped_to_working_branch` (main after seed; a new branch after create; header B returns B's rows)
     - `Update_validates_pattern_and_persists` (Monthly without `{THANG}` → 400 with `details.pattern` and an `error.message` equal to the Vietnamese rule text; a valid pattern persists and the PUT returns the updated DTO)
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~DocumentNumberFormatterTests|FullyQualifiedName~NumberingSettingsTests"`. Expected: FAIL.
3. **Write the minimal implementation:** enums, formatter, entity, config, DbSet, migration `AddDocumentNumbering`, service methods (`ListNumberingAsync`, `UpdateNumberingAsync`), controller actions, seeder step, branch-create hook. In `Domain/Common/DomainException.cs`, give `ValidationDomainException` a second constructor `(IDictionary<string, string[]> errors, string? message)` whose message defaults to the first detail message (Vietnamese) instead of "One or more validation errors occurred."; use it here and in every later phase, so the frontend toast (`getErrorMessage`) shows the actual reason.
4. **Run tests to verify they pass:** same filter plus `BranchCrudTests`. Expected: PASS.
5. **Commit:** `git commit -m "feat(inventory): document numbering settings with pattern validation"`

### Task 2.8 — Seed the main warehouse, system reasons and payment methods

Extend `DbSeeder.SeedInventoryReferenceDataAsync`. Each block runs only when its table is empty:
- warehouse `KHO01` "Kho chính" in `BranchDefaults.MainBranchId`;
- reasons `NMH` Nhập mua hàng (In, Supplier), `NKH` Nhập khác (In, Any), `XBH` Xuất bán hàng (Out, Customer), `XKH` Xuất khác (Out, Any) — all `IsSystem = true`;
- payment methods `TM` Tiền mặt (`IsCash = true`) and `CK` Chuyển khoản.

1. **Write the failing test** `Inventory/InventorySeedTests.cs : InventoryTestBase`: `Seed_creates_reference_data_once`. Assert the codes, partner types, `IsSystem` and `IsCash`; then call `DbSeeder.SeedAsync` again and check the counts are unchanged. Add `protected Guid DefaultWarehouseId` to `InventoryTestBase`, resolved in `InitializeAsync` from code `KHO01`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~InventorySeedTests"`. Expected: FAIL.
3. **Write the minimal implementation:** the seeder blocks.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~Inventory|FullyQualifiedName~DbSeeder"`. Expected: PASS. Fix any earlier test that assumed empty catalogs (for example, list counts in `WarehouseCrudTests` must account for `KHO01`).
5. **Commit:** `git commit -m "feat(inventory): seed main warehouse, system stock reasons and payment methods"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Catalog|FullyQualifiedName~Customer|FullyQualifiedName~Product"`
- `dotnet test OrderMgmt.sln`

## Exit Criteria

- Warehouses, stock reasons and payment methods have CRUD with the documented guards, and warehouses are branch-scoped.
- The partner role rules D1, D2, D38 and PartnerType-scoped search are enforced (D4's voucher rules arrive in Phase 05); the quotation customer autocomplete, the global search and quotation create only accept customers; `false` flags persist (D28).
- Products expose the inventory fields; the suggestion DTO carries what the voucher grid needs.
- Inventory settings and numbering are readable and editable, with validation; every branch has default numbering.
- A fresh database contains `KHO01`, the four system reasons and two payment methods.
- The full backend suite is green.
