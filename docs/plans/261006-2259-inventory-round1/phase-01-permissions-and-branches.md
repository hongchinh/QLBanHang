# Phase 01 — Permissions & branch foundation

**Status:** [x] complete
**Complexity:** L

## Objective

Introduce every Round 1 permission code (plus a seeder path that grants new codes to existing system roles), the `Branch` entity with a seeded main branch, `User.DefaultBranchId`, branch CRUD and period lock, the per-request working branch (`ICurrentBranch` + `X-Branch-Id`), `GET /api/me/branches`, and a default branch on the admin user screens' API.

## Files

- `backend/src/OrderMgmt.Domain/Constants/Permissions.cs` (modify)
- `backend/src/OrderMgmt.Domain/Constants/BranchDefaults.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Organization/Branch.cs` (new)
- `backend/src/OrderMgmt.Domain/Entities/Identity/User.cs` (modify)
- `backend/src/OrderMgmt.Application/Common/Interfaces/IAppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Application/Common/Interfaces/ICurrentBranch.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/Interfaces/IBranchService.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/Models/BranchDtos.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/Branches/Validators/BranchValidators.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Application/Identity/Admin/Models/{CreateUserRequest,UpdateUserRequest,AdminUserDetailDto,AdminUserListItemDto}.cs` (modify)
- `backend/src/OrderMgmt.Application/Identity/Admin/Services/AdminUserService.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/AppDbContext.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/UserConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Seed/DbSeeder.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/<ts>_AddBranches.cs` (generated)
- `backend/src/OrderMgmt.WebApi/Services/CurrentBranch.cs` (new)
- `backend/src/OrderMgmt.WebApi/Controllers/BranchesController.cs` (new)
- `backend/src/OrderMgmt.WebApi/Controllers/MeBranchesController.cs` (new)
- `backend/src/OrderMgmt.WebApi/Program.cs` (modify — register `ICurrentBranch`)
- `backend/tests/OrderMgmt.IntegrationTests/Admin/DbSeederUpgradeTests.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Admin/AdminUserCrudTests.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationTestBase.cs` (modify — optional `defaultBranchId` on `CreateTestUserAsync`)
- `backend/tests/OrderMgmt.IntegrationTests/Organization/BranchSchemaTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Organization/BranchCrudTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Organization/MeBranchesTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Application/Catalog/ProductGroups/**` — CRUD service/controller/validator pattern
- `backend/src/OrderMgmt.WebApi/Services/CurrentUser.cs` — request-scoped adapter pattern
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/QuotationSystemSettingsConfiguration.cs` — `HasData` pattern

## Tasks

### Task 1.1 — Permission constants and seeder upgrade path

1. **Write the failing tests** in `DbSeederUpgradeTests.cs`:
   - `Fresh_seed_has_round1_permissions_and_warehouse_defaults`: `db.Permissions` contains every code in the list below. `GetRoleByCodeAsync(RoleCodes.Warehouse)` contains `stock_in.view`, `stock_in.create`, `stock_in.edit`, `stock_in.delete`, `stock_in.cancel`, the same five for `stock_out`, `inventory.opening_stock`, `reports.inventory` and `suppliers.view`, and does **not** contain `inventory.view_cost`, `stock_in.edit_all` or `inventory.settings`.
   - `Reseeding_grants_newly_introduced_permission_to_roles_whose_defaults_include_it`: in a DB scope, delete the `role_permissions` rows and the `permissions` row for `stock_out.view`. Run `DbSeeder.SeedAsync(_factory.Services)`. Assert that WAREHOUSE and MANAGER have `stock_out.view` again and SALES does not.
   - `Reseeding_does_not_regrant_inventory_permission_removed_by_admin`: remove `stock_in.view` from WAREHOUSE via `PUT /api/admin/roles/{id}/permissions`, reseed, and assert it is still absent.2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DbSeederUpgradeTests"`. Expected: FAIL — compile errors on missing constants, or assertions fail.
3. **Write the minimal implementation:**
   - `Permissions.cs`: add `public const string InventoryModule = "inventory";` and the nested classes:
     ```csharp
     public static class Branches { public const string Manage = "branches.manage"; public const string AccessAll = "branches.access_all"; }
     public static class PeriodLock { public const string Manage = "period_lock.manage"; }
     public static class Suppliers { View = "suppliers.view", Create = "suppliers.create", Update = "suppliers.update", Delete = "suppliers.delete" }
     public static class StockIn  { View = "stock_in.view",  Create = "stock_in.create",  Edit = "stock_in.edit",  Delete = "stock_in.delete",  Cancel = "stock_in.cancel",  EditAll = "stock_in.edit_all" }
     public static class StockOut { View = "stock_out.view", Create = "stock_out.create", Edit = "stock_out.edit", Delete = "stock_out.delete", Cancel = "stock_out.cancel", EditAll = "stock_out.edit_all" }
     public static class Inventory { OpeningStock = "inventory.opening_stock", ViewCost = "inventory.view_cost", ManageCatalogs = "inventory.catalogs.manage", Settings = "inventory.settings", RecalcCost = "inventory.recalc_cost" }
     ```
     Also add `Reports.Inventory = "reports.inventory"`. (The braces above are shorthand. Write each as `public const string X = "...";`.)
   - `DbSeeder.SeedPermissionsAsync`: add every code, with Vietnamese names. Modules: branches / period lock → `SystemModule`; suppliers → `CatalogModule`; stock_in / stock_out / inventory → `InventoryModule`; reports.inventory → `ReportModule`. Change the return type to `Task<IReadOnlySet<string>>` and return the codes inserted in this run.
   - `SeedAsync`: after `MigrateAsync`, open a transaction that covers both permission steps, so the "new codes" signal cannot be lost if the process dies between them (SeedPermissionsAsync currently commits on its own):
     ```csharp
     await using (var tx = await db.Database.BeginTransactionAsync(ct))
     {
         var newCodes = await SeedPermissionsAsync(db, ct);
         await SeedRolesAsync(db, newCodes, ct);
         await tx.CommitAsync(ct);
     }
     ```
     The connection is already open for the session advisory lock, so the transaction runs on the same connection.
   - `SeedRolesAsync`: add the D23 codes to the WAREHOUSE defaults. Add a final branch for existing non-Admin system roles that already have permissions: `AssignPermissions(role, permCodes.Where(newCodes.Contains).ToArray(), allPermissions);`. Add a comment explaining why this runs once per code (admin removals survive restarts) and that this changes the seeder globally: any future permission code added to a system role's defaults is granted once to the existing role (the ACCOUNTANT comment "existing roles must be granted manually" no longer applies to new codes — update it). No ACCOUNTANT inventory defaults (D39 open question).
4. **Run the tests to verify they pass:** the same filter. Expected: PASS, including the three pre-existing `DbSeederUpgradeTests`.
5. **Commit:** `git commit -m "feat(permissions): add round 1 inventory permissions and grant new codes to system roles"`

### Task 1.2 — Branch entity, main branch seed, `User.DefaultBranchId`

1. **Write the failing test** `Organization/BranchSchemaTests.cs` (inherits `QuotationTestBase`):
   - `Main_branch_is_seeded_and_is_every_users_default`: `db.Branches.Single(b => b.Id == BranchDefaults.MainBranchId)` has `Code == "CN01"` and `Name == "Chi nhánh chính"`. The admin user, and a user created through `CreateTestUserAsync`, both have `DefaultBranchId == BranchDefaults.MainBranchId`.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~BranchSchemaTests"`. Expected: FAIL (compile: `Branch`, `BranchDefaults`, `DefaultBranchId` missing).
3. **Write the minimal implementation:**
   - `BranchDefaults`: `public static readonly Guid MainBranchId = new("6f1f7c1e-6a4b-4b53-9a3e-0c0b5a000001");`, `MainBranchCode = "CN01"`, `MainBranchName = "Chi nhánh chính"`.
   - `Branch : BaseEntity`: `Code`, `Name`, `Address?`, `DateOnly? LockedUntil`.
   - `User`: `public Guid DefaultBranchId { get; set; } = BranchDefaults.MainBranchId; public Branch? DefaultBranch { get; set; }`.
   - `OrganizationConfiguration.BranchConfiguration`: table `branches`, Code max 50 (unique, filtered `is_deleted = false`), Name max 255, Address max 1000, `LockedUntil` type `date`, query filter `!IsDeleted`, `HasData(new Branch { Id = MainBranchId, Code = "CN01", Name = "Chi nhánh chính", CreatedAt = DateTimeOffset.UnixEpoch })`.
   - `UserConfiguration`: `b.Property(x => x.DefaultBranchId).HasDefaultValue(BranchDefaults.MainBranchId); b.HasOne(x => x.DefaultBranch).WithMany().HasForeignKey(x => x.DefaultBranchId).OnDelete(DeleteBehavior.Restrict);`
   - `IAppDbContext` / `AppDbContext`: `DbSet<Branch> Branches`.
   - Migration: `dotnet ef migrations add AddBranches --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`. Open the generated file and check that `InsertData("branches", …)` runs **before** `AddForeignKey` on `users`. Reorder by hand if needed.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~BranchSchemaTests|FullyQualifiedName~AdminUser|FullyQualifiedName~Auth"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): add branch entity, seeded main branch and user default branch"`

### Task 1.3 — Branch CRUD and period lock API

API (`BranchesController`, route `api/branches`):

| Method | Route | Guard | Body / result |
|---|---|---|---|
| GET | `/` | `[Authorize]` | `IReadOnlyList<BranchDto>` ordered by Code |
| GET | `/{id}` | `[Authorize]` | `BranchDto` |
| POST | `/` | `branches.manage` | `CreateBranchRequest { Code, Name, Address? }` → `BranchDto` |
| PUT | `/{id}` | `branches.manage` | `UpdateBranchRequest { Name, Address? }` → `BranchDto` |
| DELETE | `/{id}` | `branches.manage` | 409 if main branch or referenced by any user's `DefaultBranchId` (the warehouse check is added in Phase 02 Task 2.1) |
| PUT | `/{id}/lock` | `period_lock.manage` | `SetPeriodLockRequest { DateOnly? LockedUntil }` → `BranchDto` |

`BranchDto { Id, Code, Name, Address, LockedUntil }`.

1. **Write the failing tests** `Organization/BranchCrudTests.cs`:
   - `Crud_and_period_lock_happy_path` (as admin): create, list, get, update, delete. PUT `/lock` `{ lockedUntil: "2026-09-30" }` → dto has the date; PUT `{ lockedUntil: null }` → null.
   - `Conflicts_return_409`: duplicate code; deleting the main branch; deleting a branch used as a user's default. For the last case, create a user with `defaultBranchId` = that branch: extend `QuotationTestBase.CreateTestUserAsync(username, password, roleCode, Guid? defaultBranchId = null)`.
   - `Mutations_and_lock_require_their_permissions`: a SALES user on a second `HttpClient` gets 403 for POST `/api/branches` and for PUT `/lock`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~BranchCrudTests"`. Expected: FAIL (404 — routes missing).
3. **Write the minimal implementation:** `IBranchService` with `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` and `SetLockAsync`; `BranchService` (pattern: `ProductGroupService` with an explicit code — no generated codes); validators (`Code` NotEmpty, max 50, `Name` NotEmpty, max 255, `Address` max 1000); register in `Application/DependencyInjection.cs`; the controller.
4. **Run the tests to verify they pass:** the same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): branch CRUD and period lock endpoints"`

### Task 1.4 — Working branch (`ICurrentBranch`) and `GET /api/me/branches`

Contract:

```csharp
// Application/Common/Interfaces/ICurrentBranch.cs
public interface ICurrentBranch
{
    /// Working branch of the current request. Users with branches.access_all may select any
    /// existing branch through the X-Branch-Id header; everyone else is forced to DefaultBranchId.
    Task<Guid> GetIdAsync(CancellationToken ct = default);
}

// Application/Organization/Branches/Models/BranchDtos.cs
public class MyBranchesDto
{
    public Guid DefaultBranchId { get; set; }
    public Guid WorkingBranchId { get; set; }
    public bool CanSwitch { get; set; }
    public IReadOnlyList<BranchDto> Branches { get; set; } = Array.Empty<BranchDto>(); // all branches if CanSwitch, else only the default
}
```

1. **Write the failing tests** `Organization/MeBranchesTests.cs`:
   - `Admin_works_in_default_or_requested_branch`: create branch B. `GET /api/me/branches` without the header → `WorkingBranchId == MainBranchId`, `CanSwitch == true`, `Branches` contains main and B. With `X-Branch-Id: B` → `WorkingBranchId == B`.
   - `User_without_access_all_is_forced_to_default_branch`: SALES user (default = main); header = B → `WorkingBranchId == MainBranchId`, `CanSwitch == false`, `Branches` contains only main.
   - `Unknown_or_malformed_header_falls_back_to_default`: header `"not-a-guid"` and header = `Guid.NewGuid()` → main.
   - `Soft_deleted_user_with_valid_token_gets_401`: log in a SALES user, soft-delete it through `InDbAsync`-style direct DB access (`QuotationTestBase` scope), then `GET /api/me/branches` with the old token → 401 (not 500).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~MeBranchesTests"`. Expected: FAIL (404).
3. **Write the minimal implementation:**
   - `WebApi/Services/CurrentBranch.cs` (scoped). Dependencies: `IHttpContextAccessor`, `ICurrentUser`, `IAppDbContext`. Cache the resolved id in a field. Logic:
     1. no `UserId` → throw `UnauthorizedAccessException`;
     2. if `HasPermission(Branches.AccessAll)`, the header parses as a `Guid` and `Branches.AnyAsync(id)` → return it;
     3. else return `Users.Where(u => u.Id == uid).Select(u => (Guid?)u.DefaultBranchId).SingleOrDefaultAsync()`; when it is null (the user was soft-deleted but still holds a valid token), throw `UnauthorizedAccessException` (401) instead of letting `SingleAsync` produce a 500.
   - Register in `Program.cs`: `builder.Services.AddScoped<ICurrentBranch, CurrentBranch>();`
   - `BranchService.GetMyBranchesAsync` (inject `ICurrentBranch`, `ICurrentUser`); `MeBranchesController` (`[Authorize]`, route `api/me/branches`).
4. **Run the tests to verify they pass:** the same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(branches): resolve working branch from X-Branch-Id and expose /api/me/branches"`

### Task 1.5 — Default branch on admin user API

1. **Write the failing tests** (append to `Admin/AdminUserCrudTests.cs`):
   - `Create_assigns_main_branch_by_default_or_the_given_branch`:
     - POST without `defaultBranchId` → detail `DefaultBranchId == MainBranchId`, `DefaultBranchName == "Chi nhánh chính"`;
     - POST with `DefaultBranchId = B` (a created branch) → B;
     - POST with `Guid.NewGuid()` → 400, code `BRANCH_NOT_FOUND`.
   - `Update_changes_default_branch_only_when_given`: PUT with `DefaultBranchId = B` → B; PUT without the field keeps B.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~AdminUserCrudTests"`. Expected: FAIL (compile — properties missing).
3. **Write the minimal implementation:** add `Guid? DefaultBranchId` to `CreateUserRequest` / `UpdateUserRequest`. Add `Guid DefaultBranchId` and `string? DefaultBranchName` to `AdminUserDetailDto` and `AdminUserListItemDto`. In `AdminUserService`, a non-null value must exist (`throw new DomainException("BRANCH_NOT_FOUND", "Chi nhánh không tồn tại.")`); null on create → main branch; null on update → unchanged. Project `DefaultBranchName` in Get and List.
4. **Run tests to verify they pass:** `--filter "FullyQualifiedName~AdminUser"`. Expected: PASS.
5. **Commit:** `git commit -m "feat(admin-users): manage user default branch"`

## Verification

- `cd backend && dotnet build OrderMgmt.sln`
- `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DbSeederUpgradeTests|FullyQualifiedName~Organization|FullyQualifiedName~AdminUser"`
- `dotnet test OrderMgmt.sln` (no regression in quotation/customer/product suites)

## Exit Criteria

- All new permission codes exist. A fresh seed gives WAREHOUSE the D23 defaults. A reseed grants only newly introduced codes.
- The main branch exists after migration, and every user has a `DefaultBranchId`.
- `/api/branches` CRUD and `/lock` work with correct guards. `/api/me/branches` reports the working branch per the `X-Branch-Id` rules.
- Admin user API reads and writes the default branch.
- The full backend suite is green.
