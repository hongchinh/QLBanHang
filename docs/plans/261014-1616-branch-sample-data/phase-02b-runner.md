# Phase 02b — Lane B: runner and background worker

**Status:** [ ] pending
**Complexity:** L
**Runs in parallel with:** 02a, 02c, 02d · **Worktree:** `.claude/worktrees/sd-run` on `feat/branch-sample-data-run` from `$P01` · **Test DB:** `qldonhang_integtest_sd_run`

## Objective

Implement `SampleDataRunner`, which turns a `SampleDataPlan` into real data by calling the business services as the acting identity, one fresh DI scope per item (SUMMARY S3–S9, S11, S13, S18). Also add `SampleDataHostedService`, which recovers interrupted jobs at start and runs queued jobs one at a time.

Tests use hand-built tiny plans and a fake generator (S19), so this lane does not wait for Lane A.

## Files (this lane only — touch nothing else)

- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/SampleDataRunner.cs` (replace the skeleton)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/SampleCatalogWriter.cs` (new — the direct catalog inserts of S13)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Services/SampleStepExecutor.cs` (new — one method per step type; split further if long)
- `backend/src/OrderMgmt.WebApi/BackgroundJobs/SampleDataHostedService.cs` (new)
- `backend/src/OrderMgmt.WebApi/Program.cs` (modify — one `AddHostedService` line)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/TinyPlans.cs` (new — test plan builders)
- `backend/tests/OrderMgmt.IntegrationTests/SampleData/SampleDataRunnerSetupTests.cs`, `SampleDataRunnerStepTests.cs`, `SampleDataRunnerStatusTests.cs`, `SampleDataHostedServiceTests.cs` (new)

## Reference files (read-only)

Request shapes:
- `CreateWarehouseRequest`, `CreateFundRequest`, `SetFundOpeningBalanceRequest`, `SetOpeningDateRequest`, `SaveOpeningStockRequest`, `UpsertOpeningDebtRequest`;
- `UpsertStockVoucherRequest` (+ line), `UpsertCashVoucherRequest` (+ line, `AllocationRequest`), `CashDocumentActionRequest`, `UpsertFundTransferRequest`, `CreateDebtOffsetRequest`, `UpsertCreditLimitRequest`.

The services:
- `IWarehouseService.CreateAsync`, `IFundService.CreateAsync` / `SetOpeningBalanceAsync`, `IBranchService.SetOpeningDateAsync`, `IOpeningStockService.SaveAsync`, `IOpeningDebtService.CreateAsync`, `ICreditLimitService.UpsertAsync(customerId, …)`;
- `IStockVoucherService.CreateAsync`, `ICashVoucherService.CreateAsync` / `GetAsync` / `CancelAsync`, `IFundTransferService.CreateAsync`, `IDebtOffsetService.CreateAsync`.

Open amounts: `IDebtQueryService.OpenItemsAsync(new OpenItemsQuery { BranchId, PartnerId, Side, Direction = null })` (no permission check; returns `EntryId`, `Open`).

Debt entry of a document: `db.DebtEntries.Where(e => e.SourceType == X && e.SourceId == id)`, with X:
- `Opening` for an opening debt;
- `StockIn` / `StockOut` by voucher type;
- `Receipt` / `Payment` by cash voucher type.

At most one entry per source and side.

Realtime and test doubles:
- `Application/Notifications/Interfaces/IRealtimeNotifier.cs` — `NotifyDashboardChangedAsync(branchId, DashboardDomains, ct)`; `DashboardDomains.All` is in `Application/Dashboards/Executive/Realtime/DashboardDomains.cs`.
- `backend/tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/RecordingRealtimeNotifier.cs` and `DashboardSignalTests.cs` — the `WithWebHostBuilder` + `ConfigureTestServices` swap.
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/ReportDataset.cs` — the order of a complete dataset through the services.

## Runner behaviour

**`RunPlanAsync(job, plan, ct)`**
1. Set the branch `Running`, progress 0, error null with `ExecuteUpdateAsync` (`IgnoreQueryFilters`), in a scope of its own.
2. **Resolve the people.**
   - The requesting user: `job.RequestedByUserId`.
   - The owners (S5): users with `!IsDeleted && Status == UserStatus.Active` that hold role `RoleCodes.Sales`, ordered by `Username`, take 3. If there are none, the owner list is the requesting user alone.
   - Load `Username` and `FullName` of all of them, and all `Permissions.Code` values once (S4).
3. **Items, in order:**
   1. Catalog, as one item in one scope.
   2. Warehouses, one item each.
   3. Bank funds, one each. `QTM` is not created: it exists.
   4. Opening date: `SetOpeningDateAsync(branchId, new() { OpeningDate = plan.OpeningDate })`.
   5. Fund opening amounts, one per fund, `QTM` included, with `AcknowledgeNegativeFund = true`.
   6. Opening stock, one per warehouse, with `AcknowledgeNegativeStock = true`.
   7. Opening debts, one each, with `OwnerUserId = owner(spec.OwnerIndex)`.
   8. Credit limits.
   9. Steps.

   For every item:
   - create a fresh scope (`IServiceScopeFactory.CreateAsyncScope()`) and set `IActingIdentity` to the item's owner (the requesting user for setup items and for steps with `OwnerIndex == null`), the branch and all permissions;
   - build the request, run its `IValidator<TRequest>` if one is registered (`GetService<IValidator<T>>()?.ValidateAndThrowAsync`), then call the service;
   - record the plan key → entry id, and for cash vouchers the key → voucher id;
   - every 10 items, write `SampleDataProgress = done × 100 / total` (capped at 99 until the end).
4. On success:
   - set `Ready`, progress 100, error null;
   - send one dashboard signal (S18, inside its own try/catch);
   - log at Information: branch code, steps executed, steps skipped, elapsed;
   - return `Succeeded = true`.
5. On any exception other than an `OperationCanceledException` while `ct` is cancelled:
   - set `Failed`, and `SampleDataError = Truncate($"{itemKey}: {Describe(ex)}", 1000)`;
   - log at Error with the exception;
   - return `Succeeded = false`. Data already written stays (S9).

   `Describe(ex)`:
   - `FluentValidation.ValidationException`: the first error's message;
   - `NegativeStockException` / `ConfirmationRequiredException`: `$"{e.Code}: {e.Message}"`;
   - anything else: `ex.Message`.

   Setup items use keys `CATALOG`, `KHO-{Key}`, `QUY-{Code}`, `NGAY-DAU-KY`, `SO-DU-{Code}`, `TON-{Key}`, the `OD-` key, `HAN-MUC-{PartnerCode}`.

**Catalog (S13), `SampleCatalogWriter`**
- Group `DM`: find with `IgnoreQueryFilters`; create `{ Code = "DM", Name = "Dữ liệu mẫu", SortOrder = 98, IsActive = true }` if missing; restore it if soft-deleted.
- Products: unit by code (`db.Units.Single(u => u.Code == spec.UnitCode)`). Each product by code with `IgnoreQueryFilters`:
  - **missing** → insert `IsSample = true`, `Status = Active`, `PricingMode = PerUnit`, `DefaultTaxRate = 10`, `ProductGroupId = DM`, and the spec's fields;
  - **exists with `IsSample`** → reuse; if soft-deleted, set `IsDeleted = false`, `DeletedAt = null`, `DeletedBy = null`;
  - **exists without `IsSample`** → throw `ConflictException($"Mã {code} đã được dùng cho dữ liệu thật.")`.
- Partners: the same rule. Insert with `IsSample = true`, `Status = Active`, `Group`, `IsCustomer`, `IsSupplier`, `CreditDays`, `TaxCode`, `CompanyAddress = Address`, `PhoneNumber`, `ContactPerson`.
- One `SaveChangesAsync`.

**Lookups**
- Warehouse code `{BranchCode}-{Key}` (cut the branch code to 47 characters if the result is longer than 50).
- Funds of the branch by `Code`. Stock reasons, cash reasons (`!IsAuto`) and payment methods by `Code`. Products and partners by `Code`.
- Cache the ids in the runner after the setup items.

**Step mapping**

| Step | Request |
|---|---|
| `SampleStockVoucherStep` | `UpsertStockVoucherRequest`:<br>• `Type`, `VoucherAt = At`, `WarehouseId`, `ReasonId`, `PartnerId`, `PaymentMethodId`, `FundId` (when `FundCode` set), `PaidAmount = null`, `Note`;<br>• `AcknowledgeNegativeStock = AcknowledgeCreditLimit = AcknowledgeNegativeFund = true`;<br>• lines with `SortOrder = i + 1`, `ProductId`, `Quantity`, `UnitPrice`, `DiscountRate`, `VatRate`.<br>Then map the key to the voucher's debt entry, if one exists. |
| `SampleCashVoucherStep` with `FixedAmount` | One line `{ Description, Amount = FixedAmount }`, `Allocations = null`, `PersonName`, `AcknowledgeNegativeFund = true`. |
| `SampleCashVoucherStep` with `Targets` | 1. Load the target entries (side from the entry) and their open amounts with `OpenItemsAsync`.<br>2. Each amount is `min(open, MaxAmount ?? open)`; drop zero amounts.<br>3. If the sum is 0, **skip** (count, log at Information).<br>4. Otherwise one line with the sum, `Allocations` = the non-zero pairs.<br>Then map the key to the voucher's own entry and to its voucher id. |
| `SampleFundTransferStep` | `UpsertFundTransferRequest { VoucherAt, FromFundId, ToFundId, Amount, Description, AcknowledgeNegativeFund = true }`. |
| `SampleDebtOffsetStep` | `amount = min(open(receivable), open(payable))`. 0 → skip. Otherwise `CreateDebtOffsetRequest { VoucherAt, PartnerId, Receivables = [{ entry, amount }], Payables = [{ entry, amount }] }`. |
| `SampleCancelStep` | `GetAsync(voucherId)` for its `Version`, then `CancelAsync(voucherId, new() { Version, ConfirmRemoveSettlements = true, AcknowledgeNegativeFund = true })`. |
| Unknown key | Throw `InvalidOperationException($"Kế hoạch tham chiếu khóa không tồn tại: {key}")`. |

**`RunJobAsync(job, ct)`**
1. Load the branch code with `IgnoreQueryFilters`.
2. Resolve the owners (as above).
3. `plan = _generator.Generate(new(branchCode, VnTime.ToVnDate(_clock.UtcNow), owners.Count))`.
4. `await RunPlanAsync(job, plan, ct)`.

A generator exception is handled like a step failure, with key `KE-HOACH`.

**`RecoverInterruptedAsync`:** `ExecuteUpdateAsync` every `Running` branch (`IgnoreQueryFilters`) to `Failed` with "Bị gián đoạn do ứng dụng khởi động lại. Hãy xóa chi nhánh demo và tạo lại."

**`SampleDataHostedService : BackgroundService`** — `(ISampleDataQueue queue, IServiceScopeFactory scopeFactory, ILogger<SampleDataHostedService> logger)`, `ExecuteAsync`:
1. Recover:
   ```csharp
   try { await using var s = scopeFactory.CreateAsyncScope(); await s.ServiceProvider.GetRequiredService<ISampleDataRunner>().RecoverInterruptedAsync(stoppingToken); }
   catch (Exception ex) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex, "Sample data recovery skipped"); }
   ```
   The test template host has no tables yet.
2. Then `await foreach (var job in queue.ReadAllAsync(stoppingToken))`: a fresh scope, then `RunJobAsync(job, stoppingToken)`.
   - `catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }`
   - `catch (Exception ex)` → log Error and continue with the next job.

Register in `Program.cs` next to the other service registrations: `builder.Services.AddHostedService<SampleDataHostedService>();`.

## Tasks

All DB tests: `[Collection(nameof(PostgresCollection))]`, `: CashDebtTestBase`.

**Helpers**
- `RunPlanAsync(branchId, plan)`: resolve `ISampleDataRunner` from `_factory.Services.CreateAsyncScope()` and call `RunPlanAsync(new SampleDataJob(branchId, adminId), plan)`.
- `TinyPlans` builders:
  - `TinyPlans.Setup(openingDate)`:
    - 2 products: `DM-SP01` tracked, `DM-SP30` not tracked;
    - 3 partners: customer `DM-KH01` (Agent, 30 days, `OwnerIndex = 0`), supplier `DM-NCC01`, dual `DM-DT01`;
    - warehouse `K1`; funds `QTM` 10,000,000 and `VCB` 100,000,000;
    - credit limit `DM-KH01` 50,000,000;
    - opening stock `K1`: `DM-SP01` 100 units, amount 5,000,000;
    - opening debts: `OD-01` receivable `DM-KH01` 3,000,000 (DocDate = opening − 20, due + 30), `OD-02` payable `DM-NCC01` 2,000,000.
  - `TinyPlans.WithSteps(openingDate, params SampleStep[] steps)`: `Setup` plus the steps.

  Dates come from a fixed opening date of `2026-07-01`, with step times built by `VnTime.StartOfDay(date).AddHours(9)` and so on.

### Task B.1 — Setup items

1. **Write the failing test** `SampleDataRunnerSetupTests.Setup_creates_catalog_warehouses_funds_and_opening_balances`:
   - create branch `RS01` via the API; run `TinyPlans.Setup(2026-07-01)`;
   - **result and status:** `Succeeded`, and `StepsExecuted == 0` (no steps). Through `InDbAsync`:
     - branch `SampleDataStatus == Ready`, progress 100, `OpeningDate == 2026-07-01`;
   - **catalog:**
     - group `DM` "Dữ liệu mẫu";
     - `DM-SP01` and `DM-SP30` have `IsSample`, group `DM`, `DefaultTaxRate == 10`;
     - `DM-SP30.TrackInventory` is false;
     - the 3 partners have `IsSample` and the right roles;
   - **warehouse and funds:**
     - warehouse `RS01-K1` in branch `RS01`;
     - funds `QTM` (opening 10,000,000) and `VCB` (Bank, default bank fund, opening 100,000,000);
   - **opening balances:**
     - opening stock row `DM-SP01` 100 / 5,000,000;
     - two opening debts; `OD-01` has `OwnerUserId` equal to the expected first owner, computed in the test with the S5 query (first active non-deleted SALES user by `Username`, else `adminId`). Do not assume the clone has no SALES user: `SeedReferenceDataAsync` may create users;
     - credit limit 50,000,000 for `DM-KH01` in `RS01`;
   - `AssertInvariantsAsync()` and `AssertCashDebtInvariantsAsync()` pass.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~SampleDataRunnerSetupTests"`. Expected: FAIL — `NotImplementedException("Phase 02b")`.
3. **Write the minimal implementation:** `RunPlanAsync` with status handling, people resolution, the setup items and the catalog writer. Steps are not executed yet.
4. **Run the test to verify it passes:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): runner setup items and sample catalog"` (after `git add` of the touched files).

### Task B.2 — Catalog reuse and conflicts

1. **Write the failing tests** in `SampleDataRunnerSetupTests`:
   - `Second_branch_reuses_the_sample_catalog`:
     - run the setup plan on `RS02` and then on `RS03`;
     - `db.Products.IgnoreQueryFilters().Count(p => p.Code == "DM-SP01") == 1`, and the same for `DM-KH01` and the `DM` group;
     - both runs succeed.
   - `Soft_deleted_sample_row_is_restored`: after a run on `RS04`, soft-delete `DM-KH01` through `InDbAsync`; run on `RS05` → succeeds and `DM-KH01.IsDeleted == false`.
   - `Code_taken_by_real_data_fails_the_job`:
     - insert a real customer `DM-KH01` (`IsSample = false`) in a **fresh test class instance**. Use a separate test class `SampleDataRunnerConflictTests` so the clone has no sample rows yet;
     - run the setup plan on `RS06` → `Succeeded == false`;
     - the branch is `Failed`, and `SampleDataError` starts with `"CATALOG: Mã DM-KH01 đã được dùng cho dữ liệu thật."`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataRunnerSetupTests|FullyQualifiedName~SampleDataRunnerConflictTests"`. Expected: FAIL on the reuse, restore or conflict assertions (whichever B.1 did not already cover). If B.1's code already satisfies one, say so in the report and keep the test.
3. **Write the minimal implementation:** the reuse, restore and conflict rules of `SampleCatalogWriter`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): reuse sample catalog rows and refuse real codes"`

### Task B.3 — Stock voucher steps and owners

1. **Write the failing tests** `SampleDataRunnerStepTests` (one branch per test; codes `RT01`, `RT02`, …):
   - `Stock_steps_post_vouchers_debt_and_automatic_receipts`. Create SALES users `sd_sales_a` and `sd_sales_b` (`CreateTestUserAsync(…, RoleCodes.Sales)`). Steps:
     1. `NMH-0001` `In`, `NMH`, `K1`, `DM-NCC01`, credit: `DM-SP01` 50 × 40,000, VAT 10;
     2. `XBH-0001` `Out`, `XBH`, `DM-KH01`, `TM` + `QTM`, `OwnerIndex = 0`: `DM-SP01` 10 × 60,000, VAT 10, plus `DM-SP30` 1 × 350,000;
     3. `XBH-0002` `Out`, `XBH`, `DM-KH01`, credit, `OwnerIndex = 1`: `DM-SP01` 20 × 60,000;
     4. `XKH-0001` `Out`, `XKH`, no partner: `DM-SP01` 1 × 0.

     Assert:
     - 4 stock vouchers with codes starting `PN`/`PX`;
     - `XBH-0001` has an automatic receipt (`CashVouchers` with `SourceStockVoucherId == id`) in `QTM`;
     - `XBH-0002` has a receivable debt entry;
     - `NMH-0001` has a payable debt entry;
     - owners: `XBH-0001.OwnerUserId` is `owners[0]` and `XBH-0002` is `owners[1]`, where `owners` is computed in the test with the S5 query after creating the users (the clone may hold other SALES users); `NMH-0001` is the admin;
     - stock of `DM-SP01` in `RT01-K1` = 100 + 50 − 10 − 20 − 1 = 119 (`StockBalances`);
     - both invariant checks pass.
   - `Owners_are_active_sales_users_by_username`:
     - create SALES users `sd_o3`, `sd_o1`, `sd_o2`, `sd_o4`; set `sd_o2` to `Status = Disabled` via `InDbAsync`;
     - steps `XBH` with `OwnerIndex` 0, 1, 2, 3 (credit sales of 1 unit each). The owners are `expected[0]`, `[1]`, `[2]`, `[0]` (index 3 wraps), where `expected` = active non-deleted SALES users ordered by `Username`, take 3, computed in the test;
     - `sd_o2` (disabled) is never an owner;
     - if the clone holds no other SALES users, `expected` is `sd_o1`, `sd_o3`, `sd_o4`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataRunnerStepTests"`. Expected: FAIL — no vouchers (steps are not executed).
3. **Write the minimal implementation:** the step loop, `SampleStockVoucherStep` mapping, key → entry map, owner identity per step.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): runner stock voucher steps with owners"`

### Task B.4 — Cash, transfer, offset and cancel steps

1. **Write the failing tests** in `SampleDataRunnerStepTests`:
   - `Receipt_allocates_real_open_amounts`:
     - steps: `XBH-0001` credit sale to `DM-KH01` (`DM-SP01` 10 × 100,000, VAT 10 → 1,100,000);
     - then `TKH-0001` (`Receipt`, `TKH`, `VCB`, `DM-KH01`) with targets `OD-01` (null) and `XBH-0001` (`MaxAmount = 500,000`).

     The receipt total is 3,500,000; two `AtVoucher` settlements of 3,000,000 and 500,000; `OD-01` is fully settled.
   - `Payment_and_fixed_amount_vouchers`:
     - `TNCC-0001` (`Payment`, `TNCC`, `VCB`, `DM-NCC01`, target `OD-02`) → total 2,000,000;
     - `CK-0001` (`Payment`, `CK`, `QTM`, `FixedAmount = 1,000,000`, description "Tiền điện") → no allocations;
     - `TK-0001` (`Receipt`, `TK`, `QTM`, `FixedAmount = 2,000,000`).
   - `Transfer_offset_and_cancel`:
     - `CQ-0001` `QTM` → `VCB` 5,000,000;
     - a credit sale `XBH-0001` to `DM-DT01` (1,100,000) and a credit purchase `NMH-0001` from `DM-DT01` (`DM-SP01` 10 × 50,000, VAT 10 → 550,000);
     - `BT-0001` offset → amount 550,000; the payable is fully settled;
     - `TK-0001` fixed 1,000,000 then `HUY-0001` cancels it → that voucher has `Status == Cancelled`.
   - `Zero_amount_steps_are_skipped`: `TKH-0001` targets `OD-01` in full, then `TKH-0002` targets `OD-01` again → `StepsSkipped == 1`, only one `TKH` voucher, and the run is `Ready`.
   - Each test ends with both invariant checks.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — unknown step types are ignored or throw (no cash vouchers).
3. **Write the minimal implementation:** the remaining step mappings and the skip rule.
4. **Run the tests to verify they pass:** same filter. Expected: PASS (all `SampleDataRunnerStepTests`).
5. **Commit:** `git commit -m "feat(sample-data): runner cash, transfer, offset and cancel steps"`

### Task B.5 — Failures, validation, progress, recovery, signal, job

1. **Write the failing tests** `SampleDataRunnerStatusTests`:
   - `Invalid_step_fails_with_its_key_and_keeps_earlier_data`:
     - steps: `XBH-0001` (valid), then `CQ-0001` with `Amount = 0` (rejected by the transfer validator);
     - `Succeeded == false`; branch `Failed`; `SampleDataError` starts with `"CQ-0001: "`;
     - `XBH-0001` exists.
   - `Unknown_target_key_fails`: `TKH-0001` targeting `XBH-9999` → `Failed`, error contains `XBH-9999`.
   - `Error_is_cut_to_1000_characters`: a step with a 2,000-character description that fails validation (description max length, or any field whose message echoes input). If no validator message reaches 1000 characters, unit-test the private truncation through a `public static string SampleDataRunner.FormatError(string key, Exception ex)`: the result for a 2,000-character message is 1000 characters long.
   - `Recover_marks_running_branches_failed`:
     - set two branches `Running` and one `Ready` through `InDbAsync`;
     - call `RecoverInterruptedAsync` → the two are `Failed` with the restart message; the `Ready` one is unchanged.
   - `Successful_run_sends_one_dashboard_signal`:
     - with a `RecordingRealtimeNotifier` swapped in (S19, `_factory.WithWebHostBuilder(…ConfigureTestServices…)`), run a plan with two steps;
     - the recorder holds exactly one entry `(branchId, DashboardDomains.All)`.
   - `Run_job_uses_the_generator_and_today`:
     - with a fake `ISampleDataGenerator` swapped in that records its input and returns `TinyPlans.Setup(...)`, call `RunJobAsync(new SampleDataJob(branchId, adminId))` for branch `RJ01`;
     - the input is `("RJ01", VnTime.ToVnDate(DateTimeOffset.UtcNow), n)`, where `n` is the S5 owner count computed in the test (1 when there is no SALES user); the branch is `Ready`.
   - `Generator_failure_fails_the_job`: a fake generator that throws `InvalidOperationException("boom")` → branch `Failed`, error `"KE-HOACH: boom"`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~SampleDataRunnerStatusTests"`. Expected: FAIL — no recovery, no signal, `RunJobAsync` not implemented. Some failure tests may already pass from B.1–B.4; report which.
3. **Write the minimal implementation:** error formatting, recovery, the signal, `RunJobAsync`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(sample-data): runner failures, recovery, signal and job entry point"`

### Task B.6 — Background worker

1. **Write the failing test** `SampleDataHostedServiceTests`:
   - Use a factory with a fake `ISampleDataGenerator` returning `TinyPlans.WithSteps(...)` with one credit sale (S19).
   - **`Created_demo_branch_is_seeded_in_the_background`:**
     - `POST /api/branches` `{ code: "HS01", name: "CN demo", withSampleData: true }` through that factory's admin client (log in on the swapped factory's client);
     - poll `GET /api/branches` every 500 ms for up to 60 s until `HS01` leaves `Running` → `sampleDataStatus == "Ready"`, `sampleDataProgress == 100`;
     - the branch has one `XBH` voucher.
   - **`Jobs_run_one_after_another`:**
     - create `HS02` and `HS03` with sample data back to back; both end `Ready`;
     - both have the warehouse `{code}-K1`;
     - `DM-SP01` exists once.
2. **Run the test to verify it fails:** `--filter "FullyQualifiedName~SampleDataHostedServiceTests"`. Expected: FAIL — the branch stays `Running` for 60 s (no worker).
3. **Write the minimal implementation:** `SampleDataHostedService` and its `Program.cs` registration.
4. **Run the test to verify it passes:** same filter. Expected: PASS.
5. **Commit:** `git add backend/src/OrderMgmt.WebApi/BackgroundJobs/SampleDataHostedService.cs backend/src/OrderMgmt.WebApi/Program.cs backend/tests/OrderMgmt.IntegrationTests/SampleData/SampleDataHostedServiceTests.cs && git commit -m "feat(sample-data): background worker for sample data jobs"`

### Task B.7 — Lane checkpoint

1. `dotnet build OrderMgmt.sln`; `--filter "FullyQualifiedName~SampleData"` passes.
2. Run `--filter "FullyQualifiedName~ExecutiveDashboard|FullyQualifiedName~CashDebt.Reports|FullyQualifiedName~BranchOpeningDateTests"`: no new failures against `BASELINE.md`. The worker now starts in every factory; this checks it is harmless.
3. Report: test counts, and any test that passed at RED with the reason.

## Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/sd-run/backend
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd_run;Username=postgres;Password=1"
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SampleData"
```

## Exit Criteria

- `RunPlanAsync` executes every item type through the services with validation, the acting identity and owners. It computes allocation and offset amounts from real open amounts, skips zero steps, and keeps status, progress and error up to date.
- `RunJobAsync` generates with today's VN date and the owner count; `RecoverInterruptedAsync` fails stale `Running` branches.
- The worker runs queued jobs one at a time and survives a failing job.
- Only the files listed above changed.
