# Demo branches with sample data (Chi nhánh demo kèm dữ liệu mẫu để đào tạo)

> Created: 2026-10-14 16:16:42 · Based on `qlbanhang/main` at `8135477` (PR #14 executive dashboard merged) · Source design: [brainstorm SUMMARY](../../brainstorms/261014-1601-branch-sample-data/SUMMARY.md), [section-01 dataset spec](../../brainstorms/261014-1601-branch-sample-data/section-01-dataset-spec.md)

## Goal

A customer who has bought the software trains on the **real database**. In the **Thêm chi nhánh** dialog they tick **"Tạo kèm dữ liệu mẫu để đào tạo"**. They get a branch flagged as demo, filled in the background with:
- a shared sample catalog (`DM-` codes);
- 2 warehouses and 3 funds;
- full opening balances;
- about 3½ months of documents (the opening date is the 1st of the month T − 3; documents run to yesterday).

The data is created **through the existing business services in date order**, so the ledger, costing, debt, automatic vouchers and settlements are exactly what a user would have produced. Every branch-scoped report then has meaningful numbers.

A **"Xóa chi nhánh demo"** button hard-deletes everything of that branch. When no demo branch is left, it also deletes the sample catalog items nothing else uses. Company-scope reports ignore demo branches.

## Scope

**In scope**

- Backend:
  - `Branch.IsDemo`, `SampleDataStatus`, `SampleDataProgress`, `SampleDataError`; `Product.IsSample`, `Customer.IsSample` (one migration `AddBranchSampleData`).
  - `CreateBranchRequest.WithSampleData`; `BranchDto` status fields.
  - `IActingIdentity`, which makes `ICurrentUser` and `ICurrentBranch` work outside an HTTP request.
  - The `ISampleDataQueue` job queue and the `SampleDataHostedService` background worker.
  - The deterministic `SampleDataGenerator` and the `SampleDataRunner`.
  - `IDemoBranchService`: deletion preview and hard delete, including catalog cleanup and cost price restore. Endpoints `GET api/branches/{id}/demo-deletion` and `DELETE api/branches/{id}/demo`.
  - Company-scope report queries and the company-wide executive dashboard skip demo branches.
- Frontend:
  - the checkbox in the create dialog;
  - a "Loại" column with the Demo / progress / error badge, polling while a branch is seeding;
  - the type-the-code delete dialog;
  - a "(demo)" marker in the header branch switcher.
- Docs: `docs/SUMMARY.md`, `system-architecture.md`, `conventions.md`, `directory-structure.md`, `product-goals.md`.

**Out of scope** (brainstorm non-goals)

- Sample quotations, Dashboard and salesperson revenue, a branch on quotations.
- Sample users or roles.
- Hiding sample catalog rows from real branches; a "Mẫu" badge in catalog lists.
- Loading sample data into an existing branch.
- Changing `backend/scripts/seed-sample-catalog.sql`.

## Decisions made while planning

| # | Topic | Decision |
|---|---|---|
| S1 | Where the work happens | The main checkout is shared with other sessions (an executive dashboard plan runs in `.claude/worktrees/executive-dashboard*`).<br>• All work happens in the worktree `.claude/worktrees/branch-sample-data` on `feat/branch-sample-data`, created from `qlbanhang/main` in Phase 00.<br>• Phase 02 lanes each get their own worktree and branch from the Phase 01 tip, merged back with `--no-ff` in Phase 03. |
| S2 | Test databases | Never `qldonhang_test` / `qldonhang`, and not the plain `qldonhang_integtest` (another session may use it).<br>• Main worktree: `qldonhang_integtest_sd`; baseline: `_sd_base`.<br>• Lanes: `_sd_gen`, `_sd_run`, `_sd_clean`. |
| S3 | Background identity | `IActingIdentity` is a scoped holder (`ActingIdentityHolder`).<br>• When set, `CurrentUser` answers every member from it (`IsAuthenticated = true`, `Roles` empty, `Email` null).<br>• `CurrentBranch.GetIdAsync` returns its `BranchId` before any cache or header logic.<br>• HTTP requests never set it, so their behaviour is unchanged. |
| S4 | Seeder permissions | The acting identity carries **every permission code** in `Permissions` (as ADMIN has).<br>• The seeder is a system process started by a `branches.manage` user. The services' in-service checks (`stock_in.create`, `receipts.create`, `customers.create`, `edit_all`…) then pass without listing them.<br>• `UserId` is the document owner (S5); it also fills `CreatedBy`. |
| S5 | Owners | Owners are up to 3 users that are not deleted, have `Status == Active` and hold role `SALES`, ordered by `Username`; otherwise the requesting user.<br>• A plan step's `OwnerIndex` maps to `owners[index % owners.Count]`; `null` means the requesting user.<br>• Opening debts pass `OwnerUserId` the same way. |
| S6 | Validation | Controllers are skipped, so the runner runs every request through its `IValidator<T>` (`ValidateAndThrowAsync`) when one is registered, then calls the service. |
| S7 | Transactions and scopes | Each plan item (one catalog batch, one setup action, one step) runs in a **fresh DI scope** (`IServiceScopeFactory.CreateAsyncScope()`) with the acting identity set. The service's own `ITransactionRunner` gives one transaction per document, as a request would. No outer transaction: locks are never held for long. |
| S8 | Job queue | `ISampleDataQueue` is a singleton unbounded `Channel<SampleDataJob>` with a single reader. `SampleDataHostedService : BackgroundService` (WebApi):<br>1. calls `RecoverInterruptedAsync` once (inside a catch-all: in the test template host the tables may not exist yet);<br>2. then reads jobs one at a time, so two demo branches never seed concurrently and the sample catalog needs no lock;<br>3. calls `RunJobAsync` in a fresh scope.<br>`BranchService.CreateAsync` enqueues after `SaveChangesAsync`. |
| S9 | Status | `None` (normal branch) → `Running` (set at create) → `Ready` or `Failed`.<br>• The runner writes status, progress and error with `ExecuteUpdateAsync` on the branch row, in its own scope.<br>• Progress = items done × 100 / items total, written at most every 10 items.<br>• `SampleDataError` is cut to 1000 characters and starts with the failing step key (`"XBH-0012: <message>"`).<br>• At start the worker turns every `Running` into `Failed` with "Bị gián đoạn do ứng dụng khởi động lại. Hãy xóa chi nhánh demo và tạo lại." |
| S10 | Determinism | `SampleDataGenerator` is pure: no DB, no clock, no `Guid.NewGuid`. The seed is FNV-1a 32-bit of the UTF-8 branch code, fed to `new Random(seed)`. `Today` comes from the runner: `VnTime.ToVnDate(clock.UtcNow)`. |
| S11 | Amounts the runner computes | The generator only estimates totals (with `StockVoucherCalculator.Compute`).<br>• **Allocation and offset amounts are computed by the runner at run time** from the real open amounts: a target's amount is `min(open, MaxAmount ?? open)`, and the voucher total is the sum.<br>• A step whose computed amount is 0 is skipped and counted.<br>• The generator keeps safety margins (S12), so estimates never break a policy. |
| S12 | Policies | The generator never makes stock negative (exact integer simulation per warehouse and product), never lets an estimated fund balance fall below 5,000,000 ₫, and keeps an estimated credit balance at or below 80% of its limit. So seeding succeeds even when `NegativeStockPolicy`, `CreditLimitPolicy` or `NegativeFundPolicy` is `Block`. Requests still send `Acknowledge* = true` for the `Warn` case. |
| S13 | Codes | Products are `DM-SP01..30`, all `PricingMode.PerUnit` with `DefaultTaxRate = 10`, in group `DM` "Dữ liệu mẫu".<br>• Customers `DM-KH01..20`, suppliers `DM-NCC01..08`, dual-role partners `DM-DT01..02`. Names end with `" (mẫu)"` (brainstorm open question 1, default taken).<br>• Warehouses `{BranchCode}-K1` "Kho chính" / `{BranchCode}-K2` "Kho phụ"; if that is longer than 50, the branch code is cut to 47 characters.<br>• Bank funds `VCB`, `TCB`; `QTM` already exists.<br>• Existing sample rows (`IsSample = true`) are reused. A code taken by a row with `IsSample = false` fails the job with "Mã {code} đã được dùng cho dữ liệu thật.". Sample rows are inserted directly through `IAppDbContext`, because the create requests have no `IsSample`. |
| S14 | Hard delete | `DemoBranchService.DeleteAsync` runs one `ITransactionRunner.RunAsync`:<br>1. Take the exclusive branch gate, then product keys (sample products and the real products whose cost price must be recomputed).<br>2. Delete every branch row in the order of Phase 02c, all with `IgnoreQueryFilters()` + `ExecuteDeleteAsync`.<br>3. Move users whose default branch it was to `BranchDefaults.MainBranchId`.<br>4. Recompute `Product.CostPrice` of the real products used by the branch's stock-ins. The logic moves out of `StockVoucherService` into `ProductCostPrices.RecomputeAsync`.<br>5. When no non-deleted demo branch is left, delete the unreferenced sample products, partners (and their addresses) and the `DM` group.<br>Errors: 404 for an unknown branch; 409 `NOT_A_DEMO_BRANCH`; 409 `SAMPLE_DATA_RUNNING`. This is the documented exception to "prefer soft delete". |
| S15 | Company scope | **TT133 reports:** at the four inline company filters and the TT133 company opening-date query, add `!_db.Branches.Any(b => b.Id == e.BranchId && b.IsDemo)` (for the opening date: `!b.IsDemo`):<br>• `CashDebtReportService` (debt balance, debt ledger, reconciliation);<br>• `InventoryReportService.BuildStockSummaryAsync`;<br>• `Tt133ExportService.BuildContextAsync`.<br>`NOT EXISTS` keeps rows of soft-deleted real branches, as today.<br>**Executive dashboard (PR #14):** in the company branch of `ExecutiveScopeResolver.ResolveAsync`, add `.Where(b => !b.IsDemo)` to the branch-id query. Every company-wide dashboard query and the branch comparison table take their ids from it.<br>A demo branch named explicitly (picker, `X-Branch-Id`) still works. The dashboard and the reports exclude demo branches the same way, so their figures keep matching (executive plan P23). |
| S16 | Learners writing during seeding | Not blocked. A learner's document in a `Running` branch can make a later step fail; the branch then ends `Failed` and is deleted and recreated. Recorded as a risk. |
| S18 | Realtime signal | Dashboard signals come only from the `[DashboardSignal]` controller filter, so the seeder's service calls send none (no flood). At the end of a successful `RunPlanAsync`, the runner sends **one** `IRealtimeNotifier.NotifyDashboardChangedAsync(branchId, DashboardDomains.All)`, so an open dashboard on the demo branch refreshes; a failure to send is logged and ignored. Do **not** put `[DashboardSignal]` on `BranchesController`: `DashboardSignalTests.Every_document_controller_is_tagged` pins the tagged controller list. |
| S19 | Test doubles | `WebAppFactory.WithWebHostBuilder(b => b.ConfigureTestServices(s => { s.RemoveAll<T>(); s.AddSingleton<T>(fake); }))` is the existing way to swap a service (`ExecutiveDashboard/DashboardSignalTests.cs`, `RecordingRealtimeNotifier.cs`). Lane B uses it to give the worker a fake `ISampleDataGenerator` that returns a tiny plan, so its tests do not depend on Lane A. |
| S17 | Cost price limit | If a learner's demo stock-in was the only stock-in of a **real** product, the recompute after the delete finds none and keeps the demo price (existing `RecomputeCostPricesAsync` behaviour: only `CostPriceUpdatedOn` is cleared). Recorded as a risk; not solved here. |

## Assumptions

- The brainstorm and section-01 are the approved spec. Open questions take their defaults:
  - name suffix "(mẫu)";
  - no "Mẫu" badge in catalog lists;
  - one `DM` product group;
  - measure the timing and shrink if it exceeds 60 s.
- The document event stream of the mobile manager hub brainstorm does not exist yet; nothing to suppress.
- `InventorySettings.CostingScope` may be `Branch` or `Warehouse`; deleting `InventoryCostPeriods` by `BranchId` covers both (the column is always written).
- One backend instance runs the worker (as today: no multi-instance deployment).
- Local setup: PostgreSQL on `localhost:5432` (`postgres`/`1`), `psql` at `/c/Program Files/PostgreSQL/18/bin/psql`. HTTP clients (`curl`, `wget`, `Invoke-WebRequest`) are denied by local settings, so checks go through tests and the browser.

## Risks

- **R1 — Seeding time.** About 250 documents, each recomputing costing up to the current period.
  - Mitigations: the job runs in the background with progress; Phase 03 measures it.
  - Above 60 s on the dev machine, shrink the dataset in the order of section-01 "Khối lượng và hiệu năng" (a generator constant change plus its tests).
- **R2 — Estimates versus real totals.** Fund and credit margins are built on estimated totals; allocations use real open amounts (S11). If a margin is too thin a step fails with a policy error, and the generator tests plus the Phase 03 end-to-end run under `Block` catch it.
- **R3 — Merge of four lanes.** Shared files are touched in Phase 01 only (DI, DTOs, entities, `BranchService`); lanes own disjoint files (each phase lists them). Phase 03 merges one lane at a time and builds after each.
- **R4 — Hard delete misses a table.** Mitigation: the Phase 02c test counts rows by branch id in **every** table that has `branch_id`, plus children, after the delete, and checks the other branch's counts and sums are unchanged.
- **R5 — Learner actions** (S16, S17).
- **R7 — Dashboard hub group after a delete.** A user whose default branch was the demo branch is moved to the main branch on delete, but an open SignalR connection keeps its old `dashboard-branch-{id}` group until it reconnects (executive plan follow-up `realtime-02`). Harmless: that group receives nothing once the branch is gone. No change here.
- **R6 — Hosted service in tests.** It starts in every `WebAppFactory`, including the template builder. Recovery is wrapped in a catch-all and the worker is idle otherwise. A broken worker shows up in Phase 02b's hosted-service test.
- **Baseline failures.** Phase 00 records them; later phases compare test by test.

## Phases

- [ ] Phase 00 — Worktree, plan commit and failure baseline (S) — `phase-00-baseline.md`
- [ ] Phase 01 — Foundation: flags, migration, acting identity, contracts, queue, create flag (M) — `phase-01-foundation.md`
- [ ] Phase 02a — Lane A: deterministic sample data generator (L) — `phase-02a-generator.md`
- [ ] Phase 02b — Lane B: runner and background worker (L) — `phase-02b-runner.md`
- [ ] Phase 02c — Lane C: demo branch hard delete and company scope (L) — `phase-02c-demo-delete-and-scope.md`
- [ ] Phase 02d — Lane D: frontend (M) — `phase-02d-frontend.md`
- [ ] Phase 03 — Merge, end-to-end verification, docs (M) — `phase-03-integration-and-verification.md`

**Order:**
1. 00, then 01.
2. **02a, 02b, 02c and 02d run in parallel**, one agent each, each in its own worktree from the Phase 01 tip.
3. 03 after all four.

Phase 00 Task 0.2 (baseline run) may overlap Phase 01.

## Shared contracts (frozen in Phase 01)

```csharp
// Domain/Enums/Enums.cs
public enum SampleDataStatus { None = 0, Running = 1, Ready = 2, Failed = 3 }

// Domain/Entities/Organization/Branch.cs (added)
public bool IsDemo { get; set; }
public SampleDataStatus SampleDataStatus { get; set; } = SampleDataStatus.None;
public int SampleDataProgress { get; set; }
public string? SampleDataError { get; set; }            // max 1000
// Domain/Entities/Catalog/Product.cs and Customer.cs (added)
public bool IsSample { get; set; }

// Application/Common/Interfaces/IActingIdentity.cs
public sealed record ActingIdentity(Guid UserId, string Username, string FullName, Guid BranchId, IReadOnlyList<string> Permissions);
public interface IActingIdentity
{
    ActingIdentity? Current { get; }
    void Set(ActingIdentity identity);
}
// Application/Common/ActingIdentityHolder.cs — scoped implementation

// Application/Organization/Branches/Models/BranchDtos.cs (added)
BranchDto: bool IsDemo; SampleDataStatus SampleDataStatus; int SampleDataProgress; string? SampleDataError;
CreateBranchRequest: bool WithSampleData;
public class DemoBranchDeletionPreviewDto { public int DocumentCount { get; set; } public int ReassignedUserCount { get; set; } }
public class DemoBranchDeletionResultDto { public int ReassignedUserCount { get; set; } public int KeptSampleProductCount { get; set; } public int KeptSamplePartnerCount { get; set; } }

// Application/Organization/SampleData/Models/SampleDataJob.cs
public sealed record SampleDataJob(Guid BranchId, Guid RequestedByUserId);
public sealed record SampleDataRunResult(bool Succeeded, int StepsExecuted, int StepsSkipped, TimeSpan Elapsed, string? Error);

// Application/Organization/SampleData/Models/SampleDataPlan.cs
public sealed record SampleDataGeneratorInput(string BranchCode, DateOnly Today, int OwnerCount);
public sealed class SampleDataPlan
{
    public DateOnly OpeningDate { get; init; }
    public DateOnly LastDocumentDate { get; init; }
    public List<SampleProductSpec> Products { get; init; } = new();
    public List<SamplePartnerSpec> Partners { get; init; } = new();
    public List<SampleWarehouseSpec> Warehouses { get; init; } = new();
    public List<SampleFundSpec> Funds { get; init; } = new();
    public List<SampleCreditLimitSpec> CreditLimits { get; init; } = new();
    public List<SampleOpeningStockSpec> OpeningStocks { get; init; } = new();
    public List<SampleOpeningDebtSpec> OpeningDebts { get; init; } = new();
    public List<SampleStep> Steps { get; init; } = new();          // strictly increasing At
}
public sealed record SampleProductSpec(string Code, string Name, string UnitCode, decimal DefaultPrice, decimal CostPrice,
    bool TrackInventory, decimal? Length, decimal? Width, decimal? Thickness, decimal? Density, string? Specification);
public sealed record SamplePartnerSpec(string Code, string Name, CustomerGroup Group, bool IsCustomer, bool IsSupplier,
    int? CreditDays, string? TaxCode, string? Address, string? PhoneNumber, string? ContactPerson, int? OwnerIndex);
public sealed record SampleWarehouseSpec(string Key, string Name);                     // Key "K1" | "K2"
public sealed record SampleFundSpec(string Code, string Name, FundKind Kind, string? BankName, string? AccountNumber, decimal OpeningAmount);
public sealed record SampleCreditLimitSpec(string PartnerCode, decimal Amount);
public sealed record SampleOpeningStockLine(string ProductCode, decimal Quantity, decimal Amount);
public sealed record SampleOpeningStockSpec(string WarehouseKey, IReadOnlyList<SampleOpeningStockLine> Lines);
public sealed record SampleOpeningDebtSpec(string Key, string PartnerCode, DebtSide Side, OpeningDebtKind Kind, string DocNo,
    DateOnly DocDate, DateOnly? DueDate, decimal Amount, int? OwnerIndex);

public abstract record SampleStep(string Key, DateTimeOffset At, int? OwnerIndex);     // At is UTC; OwnerIndex null = requesting user
public sealed record SampleStockLine(string ProductCode, decimal Quantity, decimal UnitPrice, decimal DiscountRate, decimal VatRate);
public sealed record SampleStockVoucherStep(string Key, DateTimeOffset At, int? OwnerIndex, StockDirection Type, string ReasonCode,
    string WarehouseKey, string? PartnerCode, string? PaymentMethodCode, string? FundCode, string? Note,
    IReadOnlyList<SampleStockLine> Lines) : SampleStep(Key, At, OwnerIndex);
public sealed record SampleAllocationTarget(string TargetKey, decimal? MaxAmount);        // null = the whole open amount
public sealed record SampleCashVoucherStep(string Key, DateTimeOffset At, int? OwnerIndex, CashVoucherType Type, string ReasonCode,
    string FundCode, string? PartnerCode, string? PersonName, string Description, decimal? FixedAmount,
    IReadOnlyList<SampleAllocationTarget> Targets) : SampleStep(Key, At, OwnerIndex);
    // FixedAmount for reasons without debt effect (TK, CK); Targets for TKH, TNCC, CHKH. Exactly one of the two is used.
public sealed record SampleFundTransferStep(string Key, DateTimeOffset At, int? OwnerIndex, string FromFundCode, string ToFundCode,
    decimal Amount, string Description) : SampleStep(Key, At, OwnerIndex);
public sealed record SampleDebtOffsetStep(string Key, DateTimeOffset At, int? OwnerIndex, string PartnerCode,
    string ReceivableTargetKey, string PayableTargetKey) : SampleStep(Key, At, OwnerIndex);
public sealed record SampleCancelStep(string Key, DateTimeOffset At, int? OwnerIndex, string TargetKey) : SampleStep(Key, At, OwnerIndex);
    // cancels the cash voucher created by step TargetKey

// Application/Organization/SampleData/Interfaces/
public interface ISampleDataQueue
{
    void Enqueue(SampleDataJob job);
    IAsyncEnumerable<SampleDataJob> ReadAllAsync(CancellationToken ct);
}
public interface ISampleDataGenerator { SampleDataPlan Generate(SampleDataGeneratorInput input); }
public interface ISampleDataRunner
{
    Task RunJobAsync(SampleDataJob job, CancellationToken ct = default);                                      // owners → generate → RunPlanAsync; never throws (except cancellation)
    Task<SampleDataRunResult> RunPlanAsync(SampleDataJob job, SampleDataPlan plan, CancellationToken ct = default);  // status/progress/error; never throws (except cancellation)
    Task RecoverInterruptedAsync(CancellationToken ct = default);
}
public interface IDemoBranchService
{
    Task<DemoBranchDeletionPreviewDto> PreviewDeletionAsync(Guid branchId, CancellationToken ct = default);
    Task<DemoBranchDeletionResultDto> DeleteAsync(Guid branchId, CancellationToken ct = default);
}
```

**Plan keys.**
- Opening debts: `OD-01…`. Stock vouchers: `{ReasonCode}-{nnnn}` (e.g. `XBH-0001`). Cash vouchers: `{ReasonCode}-{nnnn}`. Transfers: `CQ-{nnnn}`. Offsets: `BT-{nnnn}`. Cancels: `HUY-{nnnn}`.
- Keys are unique within a plan. A target key always names an **earlier** item of the same partner.
- The runner keeps `key → DebtEntry id` for opening debts and for documents that post a debt entry, and `key → cash voucher id`.

**Frontend contract** (`features/branches/types.ts`):
```ts
export type SampleDataStatus = 'None' | 'Running' | 'Ready' | 'Failed';
// Branch gains: isDemo?: boolean; sampleDataStatus?: SampleDataStatus; sampleDataProgress?: number; sampleDataError?: string | null;
// CreateBranchRequest gains: withSampleData?: boolean;
export interface DemoBranchDeletionPreview { documentCount: number; reassignedUserCount: number; }
export interface DemoBranchDeletionResult { reassignedUserCount: number; keptSampleProductCount: number; keptSamplePartnerCount: number; }
```
Enums are serialized as strings (`JsonStringEnumConverter`, `Program.cs:184`).

**Endpoints:**

| Method | Route | Auth | Response | Owner |
|---|---|---|---|---|
| POST | `api/branches` (body gains `withSampleData`) | `branches.manage` | `BranchDto` | Phase 01 |
| GET | `api/branches/{id:guid}/demo-deletion` | `branches.manage` | `DemoBranchDeletionPreviewDto` | Phase 02c |
| DELETE | `api/branches/{id:guid}/demo` | `branches.manage` | `DemoBranchDeletionResultDto` | Phase 02c |

## Executor conventions

- **Model and evidence.** Implementers are sonnet (or stronger), never haiku. Each implementer report quotes the RED failure line of every task.
- **Paths.** Every path is relative to the feature worktree `D:\Projects\QLBanHang\.claude\worktrees\branch-sample-data` (Phase 00), or to the lane worktree in Phase 02.
- **Drive case.** Keep the session cwd on uppercase `/D/Projects/QLBanHang` (or below it) while worktree agents or workflows run. A lowercase `d:` cwd breaks worktree agents.
- **Lane worktrees** (created at the start of each Phase 02 lane from the Phase 01 tip `$P01`):
  ```bash
  cd /D/Projects/QLBanHang
  git worktree add .claude/worktrees/sd-gen   -b feat/branch-sample-data-gen   $P01
  git worktree add .claude/worktrees/sd-run   -b feat/branch-sample-data-run   $P01
  git worktree add .claude/worktrees/sd-clean -b feat/branch-sample-data-clean $P01
  git worktree add .claude/worktrees/sd-fe    -b feat/branch-sample-data-fe    $P01   # then: cd .claude/worktrees/sd-fe/frontend && npm ci
  ```
- **Commits.** Commit after every task with conventional commits, scope `sample-data`, `branches`, `reports` or `frontend` (e.g. `feat(sample-data): acting identity`). End every message with the attribution line from the session's system reminder.
- **Staging.** Stage files explicitly (`git add <paths>`). Never `git add -A` / `git add .`: `source/` must stay untracked and `.claude/tools/` holds a token.
- **Shell.** Commands are written for Git Bash (the Bash tool). PowerShell 5.1 has no `&&`.
- **Test database** (bash), set before any backend test command, with the lane's name (S2):
  `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd;Username=postgres;Password=1"`
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln`. If a running dev server locks `bin`, add `-o "$TEMP/qlbh-build"`. If Git Bash has no `APPDATA`, set it, or NuGet restore fails.
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`.
  - Pure tests of this plan: `--filter "FullyQualifiedName~SampleData.Unit"`.
  - Full suite: `dotnet test OrderMgmt.sln --logger "trx;LogFileName=current.trx" --results-directory "$TEMP/sd-run"`. The `TestResults/*.trx` under the test project are tracked: never write new ones there.
  - Migration: `dotnet ef migrations add AddBranchSampleData --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`. Without `-o`, the file lands in the old `Migrations/` folder. The snapshot `src/OrderMgmt.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` is updated in place.
- **Frontend commands** (from `frontend/`):
  - `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`.
  - After a build: `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` (skip a path that is not tracked).
- **Test locations:**
  - Pure tests: `backend/tests/OrderMgmt.IntegrationTests/SampleData/Unit/` (namespace `OrderMgmt.IntegrationTests.SampleData.Unit`, no collection, no database).
  - DB tests: `backend/tests/OrderMgmt.IntegrationTests/SampleData/`, `[Collection(nameof(PostgresCollection))]`, inheriting `CashDebtTestBase` (which gives `InTransactionAsync`, `InDbAsync`, `AssertInvariantsAsync`, `AssertCashDebtInvariantsAsync`, `CreateBranchAsync`, `UseBranch`, `CreateWarehouseAsync`, `CreatePartnerAsync`, `CreateBankFundAsync`, `CreateOpeningDebtAsync`, `Vn(...)`, `CreateClientWithPermissionsAsync`, `CreateTestUserAsync`, `ReportDataset.SeedAsync(this)`).
  - Frontend tests sit next to the file under test and follow `pages/settings/branches-page.test.tsx`: hooks and api modules mocked wholesale, so any new hook or api method must be added to those `vi.mock` factories.
- **Rules carried over:**
  - DTOs are classes with `{ get; set; }`; lists default to `new()`.
  - Every `DateTimeOffset` sent to EF is UTC (D27).
  - `HasDefaultValue(true)` on a `bool` needs `.HasSentinel(true)` (D28).
  - Validation `details` keys are camelCase.
  - Hard deletes and "is it referenced" checks use `IgnoreQueryFilters()`, because soft-deleted rows still hold `Restrict` foreign keys.
  - A happy-path task must not implement the rules of a later task, or that task's RED step cannot fail.
  - **RED can be a build error.** A test that references a type that does not exist fails to compile; that counts as RED.
- **Notation:** `T` = the VN date of the run; amounts in đồng.

## Final Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/branch-sample-data
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln --logger "trx;LogFileName=final.trx" --results-directory "$TEMP/sd-final"

cd ../frontend
npm run typecheck
npm run lint
npm run test -- --testTimeout=30000
npm run build
```

Only the failures recorded in `BASELINE.md` may remain, compared test by test. The manual browser check is in Phase 03.

## Rollback / Recovery

- **Code:** every task is a commit; lanes are separate branches merged with `--no-ff`, so a whole lane is revertable with `git revert -m 1 <merge>`. Or drop `feat/branch-sample-data`.
- **Database:** one migration, `AddBranchSampleData`, which only adds columns with defaults.
  - Before deploy: `dotnet ef migrations remove --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi`.
  - After deploy: `dotnet ef database update <previous migration> …`, which drops the columns.
  - Delete the demo branches with the demo delete **before** rolling back: afterwards the flags are gone and demo data looks like real data.
- **Sample data in production:** each demo branch is removed with "Xóa chi nhánh demo". Sample catalog rows that are still referenced stay, recognisable by the `DM-` codes.
- **Worktrees:** remove lane worktrees after Phase 03 merges (`git worktree remove .claude/worktrees/sd-*`). Their `node_modules` are real installs, not junctions, so removal is safe.
