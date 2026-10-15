# Phase 01 — Foundation: flags, migration, acting identity, contracts, queue, create flag

**Status:** [x] done
**Complexity:** M

## Objective

Lay down everything the four parallel lanes share, so that after this phase no lane edits a shared file:

- the flags on `Branch`, `Product` and `Customer`, with one migration and the `BranchDto` fields;
- `IActingIdentity`, so application services work from a background scope as a chosen user in a chosen branch;
- the sample data contracts (SUMMARY "Shared contracts"), the job queue, skeleton classes for the generator, the runner and the demo branch service, and all DI registrations;
- `CreateBranchRequest.WithSampleData`: create a demo branch in `Running` state and enqueue its job.

## Files

Backend:
- `backend/src/OrderMgmt.Domain/Enums/Enums.cs` (modify — `SampleDataStatus`)
- `backend/src/OrderMgmt.Domain/Entities/Organization/Branch.cs`, `Catalog/Product.cs`, `Catalog/Customer.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/OrganizationConfiguration.cs`, `CatalogConfiguration.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/*_AddBranchSampleData.cs` (+ `.Designer.cs`) and `backend/src/OrderMgmt.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` (generated)
- `backend/src/OrderMgmt.Application/Organization/Branches/Models/BranchDtos.cs` (modify)
- `backend/src/OrderMgmt.Application/Organization/Branches/Services/BranchService.cs` (modify)
- `backend/src/OrderMgmt.Application/Common/Interfaces/IActingIdentity.cs` (new)
- `backend/src/OrderMgmt.Application/Common/ActingIdentityHolder.cs` (new)
- `backend/src/OrderMgmt.WebApi/Services/CurrentUser.cs`, `CurrentBranch.cs` (modify)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Models/SampleDataPlan.cs`, `SampleDataJob.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Interfaces/ISampleDataQueue.cs`, `ISampleDataGenerator.cs`, `ISampleDataRunner.cs`, `IDemoBranchService.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/SampleDataQueue.cs` (new, complete)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleDataGenerator.cs` (new, skeleton for Phase 02a)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/SampleDataRunner.cs` (new, skeleton for Phase 02b)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/DemoBranchService.cs` (new, skeleton for Phase 02c)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/BranchSampleDataFlagsTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/ActingIdentityTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/Unit/SampleDataQueueTests.cs` (new)

## Reference files (read-only)

- `backend/src/OrderMgmt.Infrastructure/Persistence/Configurations/CatalogConfiguration.cs` lines around `IsSupplier` — bool column with a default.
- `backend/src/OrderMgmt.Infrastructure/Persistence/Migrations/20261006190105_AddPartnerRoles.cs` — `AddColumn<bool>` shape.
- `backend/tests/OrderMgmt.IntegrationTests/Inventory/InventoryEngineTestBase.cs` — calling services from `_factory.Services.CreateAsyncScope()`.
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/BranchOpeningDateTests.cs` — branch API test shape.

## Tasks

### Task 1.1 — Flags, migration and `BranchDto` fields

1. **Write the failing test** `SampleData/BranchSampleDataFlagsTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`, constructor `(PostgresFixture pg) : base(pg)`):
   - `New_branch_has_no_sample_data_flags`:
     - `POST /api/branches` `{ code: "SDF1", name: "CN SDF1" }` as admin → 200. The body's `data` has `isDemo == false`, `sampleDataStatus == "None"`, `sampleDataProgress == 0`, `sampleDataError == null`.
     - `GET /api/branches` lists it with the same values.
   - `Product_and_partner_default_to_not_sample`: create a product (`CreateInventoryProductAsync`) and a partner (`CreatePartnerAsync`). Through `InDbAsync`, both rows have `IsSample == false`.
2. **Run the test to verify it fails:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BranchSampleDataFlagsTests"`. Expected: FAIL — build error, `IsSample` does not exist.
3. **Write the minimal implementation:**
   - `SampleDataStatus` enum (contract) in `Domain/Enums/Enums.cs`.
   - Entity properties (contract).
   - `BranchConfiguration`:
     - `IsDemo` → `HasDefaultValue(false)`;
     - `SampleDataStatus` → `HasConversion<int>().HasDefaultValue(SampleDataStatus.None)`;
     - `SampleDataProgress` → `HasDefaultValue(0)`;
     - `SampleDataError` → `HasMaxLength(1000)`.
   - `CustomerConfiguration` and `ProductConfiguration`: `IsSample` → `HasDefaultValue(false)`.
   - Migration (from `backend/`): `dotnet ef migrations add AddBranchSampleData --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`. Open the generated file and check it only adds the four `branches` columns and `is_sample` on `products` and `customers`, all non-null with defaults (except `sample_data_error`).
   - `BranchDto` gains the four fields; `BranchService.ToDto` fills them.
4. **Run the test to verify it passes:** same filter. Expected: PASS (2 tests). Also run `--filter "FullyQualifiedName~BranchOpeningDateTests|FullyQualifiedName~BranchTests"` (whichever exist): no new failures.
5. **Commit:** `git add <the files above> && git commit -m "feat(branches): sample data flags on branch, product and partner"`

### Task 1.2 — Acting identity

1. **Write the failing tests** `SampleData/ActingIdentityTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`):
   - `Background_scope_without_acting_identity_has_no_user`: in `await using var scope = _factory.Services.CreateAsyncScope();`, `ICurrentUser.UserId` is null and `HasPermission(Permissions.Branches.Manage)` is false. (Records today's behaviour; it already passes.)
   - `Acting_identity_drives_current_user_and_current_branch`:
     - create branch `AI01` (`CreateBranchAsync`); read the admin id with `InDbAsync(db => db.Users.Where(u => u.Username == "admin").Select(u => u.Id).SingleAsync())`;
     - in a fresh scope, `GetRequiredService<IActingIdentity>().Set(new ActingIdentity(adminId, "admin", "Quản trị", branchId, new[] { Permissions.Branches.Manage }))`;
     - then `ICurrentUser`: `UserId == adminId`, `Username == "admin"`, `FullName == "Quản trị"`, `IsAuthenticated`, `HasPermission(Permissions.Branches.Manage)` true, `HasPermission(Permissions.Branches.AccessAll)` false, `Roles` empty, `IsInRole(RoleCodes.Admin)` false;
     - `await ICurrentBranch.GetIdAsync() == branchId`. This is not the admin's default branch, and the scope has no header.
   - `Service_called_under_acting_identity_writes_in_that_branch`:
     - create branch `AI02`; in a fresh scope set the acting identity with branch `AI02` and **all** permission codes (`await db.Permissions.Select(p => p.Code).ToListAsync()` from that scope's `IAppDbContext`);
     - `await scope.ServiceProvider.GetRequiredService<IWarehouseService>().CreateAsync(new CreateWarehouseRequest { Code = "AI02-K1", Name = "Kho AI" })`;
     - the result has `BranchId == AI02`. Through `InDbAsync`, the warehouse row has `CreatedBy == adminId`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~ActingIdentityTests"`. Expected: FAIL — build error, `IActingIdentity` / `ActingIdentity` do not exist.
3. **Write the minimal implementation:**
   - `IActingIdentity` and `ActingIdentity` (contract), namespace `OrderMgmt.Application.Common.Interfaces`.
   - `ActingIdentityHolder : IActingIdentity` (`Current { get; private set; }`, `Set` assigns), namespace `OrderMgmt.Application.Common`.
   - `services.AddScoped<IActingIdentity, ActingIdentityHolder>();` in `Application/DependencyInjection.cs`.
   - `CurrentUser(IHttpContextAccessor accessor, IActingIdentity acting)`. When `acting.Current is { } a`:
     - `UserId = a.UserId`, `Username = a.Username`, `Email = null`, `FullName = a.FullName`, `IsAuthenticated = true`;
     - `Roles = Array.Empty<string>()`, `Permissions = a.Permissions`;
     - `HasPermission(p) = a.Permissions.Contains(p)`, `IsInRole(_) = false`.

     Otherwise keep the claim logic unchanged.
   - `CurrentBranch(IHttpContextAccessor accessor, ICurrentUser currentUser, IAppDbContext db, IActingIdentity acting)`: the first statement of `GetIdAsync` is `if (_acting.Current is { } a) return a.BranchId;`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS (3 tests). Then `--filter "FullyQualifiedName~AuthTests|FullyQualifiedName~BranchOpeningDateTests"`: same results as `BASELINE.md`.
5. **Commit:** `git add <the files of this task> && git commit -m "feat(sample-data): acting identity for background work"`

### Task 1.3 — Contracts, queue, skeletons, registrations

1. **Write the failing tests:**
   - `SampleData/Unit/SampleDataQueueTests.cs` (namespace `OrderMgmt.IntegrationTests.SampleData.Unit`, no collection):
     - `Jobs_are_read_in_enqueue_order`: `var q = new SampleDataQueue();` enqueue `job1`, `job2`. Read two items from `q.ReadAllAsync(cts.Token)` (break after two) in order. Use a 5 s `CancellationTokenSource` so a bug cannot hang the run.
     - `Reading_stops_when_cancelled`: with an already-cancelled token, enumerating `ReadAllAsync` throws `OperationCanceledException` (`Assert.ThrowsAnyAsync`).
   - In `BranchSampleDataFlagsTests`, add `Sample_data_services_are_registered`: from `_factory.Services.CreateAsyncScope()`, `GetRequiredService` succeeds for `ISampleDataQueue`, `ISampleDataGenerator`, `ISampleDataRunner` and `IDemoBranchService`, and `ISampleDataQueue` resolves to the same instance from two scopes.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataQueueTests|FullyQualifiedName~BranchSampleDataFlagsTests"`. Expected: FAIL — build error, `SampleDataQueue` does not exist.
3. **Write the minimal implementation:**
   - The models and interfaces of SUMMARY "Shared contracts", **verbatim**, in the files listed above (namespaces `OrderMgmt.Application.Organization.SampleData.Models` / `.Interfaces` / `.Services` / `.Generation`).
   - `DemoBranchDeletionPreviewDto` and `DemoBranchDeletionResultDto` in `BranchDtos.cs`.
   - `SampleDataQueue`: `Channel.CreateUnbounded<SampleDataJob>(new UnboundedChannelOptions { SingleReader = true })`. `Enqueue` uses `TryWrite` and throws `InvalidOperationException` if it returns false. `ReadAllAsync` returns `_channel.Reader.ReadAllAsync(ct)`.
   - Skeletons whose every method body is `throw new NotImplementedException("Phase 02a")` (`SampleDataGenerator`), `"Phase 02b"` (`SampleDataRunner`) and `"Phase 02c"` (`DemoBranchService`). Each class gets the constructor its phase needs:
     - `SampleDataGenerator()`;
     - `SampleDataRunner(IServiceScopeFactory scopeFactory, ISampleDataGenerator generator, IDateTime clock, ILogger<SampleDataRunner> logger)`;
     - `DemoBranchService(IAppDbContext db, ITransactionRunner transaction, IInventoryLock inventoryLock)`.

     Lanes may add constructor parameters.
   - Registrations, together under a `// Sample data (demo branches)` comment in `Application/DependencyInjection.cs`:
     - `AddSingleton<ISampleDataQueue, SampleDataQueue>()`
     - `AddSingleton<ISampleDataGenerator, SampleDataGenerator>()`
     - `AddScoped<ISampleDataRunner, SampleDataRunner>()`
     - `AddScoped<IDemoBranchService, DemoBranchService>()`
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the files of this task> && git commit -m "feat(sample-data): contracts, job queue and service skeletons"`

### Task 1.4 — Create a branch with sample data

1. **Write the failing tests** in `BranchSampleDataFlagsTests`:
   - `Create_with_sample_data_marks_branch_demo_and_running`:
     - `POST /api/branches` `{ code: "SDF2", name: "CN demo", withSampleData: true }` → 200.
     - The **response** has `isDemo == true`, `sampleDataStatus == "Running"`, `sampleDataProgress == 0`. Assert on the response only: after Phase 02b a worker may already have moved the row on.
     - Through `InDbAsync`, `IsDemo == true`.
   - `Create_without_sample_data_is_unchanged`: `{ code: "SDF3", name: "CN thường", withSampleData: false }` → `isDemo == false`, `sampleDataStatus == "None"`. The branch has its numbering rows (`db.DocumentNumberings.Count(n => n.BranchId == id) == DocumentNumberingDefaults.AllTypes.Count`) and the `QTM` fund, as before.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~BranchSampleDataFlagsTests"`. Expected: FAIL — `isDemo` is false (the request property does not exist yet, so the JSON field is ignored). If `CreateBranchRequest` needs the property for the test to compile, the failure is the assertion.
3. **Write the minimal implementation:**
   - `CreateBranchRequest.WithSampleData` (bool).
   - `BranchService` constructor gains `ISampleDataQueue sampleDataQueue`.
   - In `CreateAsync`, when `request.WithSampleData`:
     - `var requestedBy = _currentUser.UserId ?? throw new UnauthorizedAccessException();`
     - set `IsDemo = true` and `SampleDataStatus = SampleDataStatus.Running`;
     - after the existing `SaveChangesAsync`, call `_sampleDataQueue.Enqueue(new SampleDataJob(branch.Id, requestedBy))`.

     Nothing else in `CreateAsync` changes. No validator change.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the files of this task> && git commit -m "feat(branches): create a demo branch with sample data"`

### Task 1.5 — Phase checkpoint and lane start point

1. `dotnet build OrderMgmt.sln`, then run the full backend suite (SUMMARY "Executor conventions"). Compare the failures with `BASELINE.md` by name. If Phase 00 Task 0.2 is still running, wait for it.
2. If a new failure appears, write a focused failing test for it, fix it, re-run, and commit `fix(sample-data): <what>`.
3. Record the lane start point. Run `git rev-parse --short HEAD` and write it into the Phase 01 line of `SUMMARY.md` as `— lanes start at <hash>`. Commit `chore(plan): phase 01 done`. The four Phase 02 worktrees are created from this commit (`$P01`).

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SampleData"
dotnet test OrderMgmt.sln --logger "trx;LogFileName=p01.trx" --results-directory "$TEMP/sd-p01"   # compare with BASELINE.md
```

## Exit Criteria

- The migration adds only the six columns. Existing branches read as `isDemo = false`, `sampleDataStatus = "None"`.
- `ICurrentUser` and `ICurrentBranch` follow `IActingIdentity` when it is set; HTTP behaviour is unchanged.
- Every contract type of SUMMARY exists with the exact names and signatures; the four services resolve from DI; the queue is a singleton.
- `POST /api/branches` with `withSampleData: true` returns a `Running` demo branch and enqueues a job.
- No new failures against `BASELINE.md`; the lane start hash is recorded.
