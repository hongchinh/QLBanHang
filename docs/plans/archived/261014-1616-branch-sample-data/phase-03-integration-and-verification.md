# Phase 03 — Merge, end-to-end verification, docs

**Status:** [x] done
**Complexity:** M

## Objective

Merge the four lanes into `feat/branch-sample-data`, prove the whole path end to end with the real generator, runner, worker and delete, measure the seeding time, check it in a browser, update the docs and run the final verification.

## Files

- `backend/tests/OrderMgmt.IntegrationTests/SampleData/SampleDataEndToEndTests.cs` (new)
- `backend/src/OrderMgmt.Application/Organization/SampleData/Generation/SampleDataVolumes.cs` and the generator tests (modify only if R1 triggers)
- `docs/architecture/system-architecture.md`, `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/project-pdr/product-goals.md`, `docs/SUMMARY.md` (modify)
- `docs/brainstorms/261014-1601-branch-sample-data/SUMMARY.md` (modify — "Next steps" status line)
- `docs/plans/261014-1616-branch-sample-data/EXECUTION-REPORT.md` (new)

## Tasks

### Task 3.1 — Merge the lanes

1. In `$WT` (`feat/branch-sample-data`), merge in this order, building after each merge:
   ```bash
   git merge --no-ff feat/branch-sample-data-clean -m "Merge lane C: demo delete and company scope"
   git merge --no-ff feat/branch-sample-data-gen   -m "Merge lane A: sample data generator"
   git merge --no-ff feat/branch-sample-data-run   -m "Merge lane B: runner and worker"
   git merge --no-ff feat/branch-sample-data-fe    -m "Merge lane D: frontend"
   ```
   Build after each backend merge: `cd backend && dotnet build OrderMgmt.sln`.

   The lanes own disjoint files, so no conflict is expected. If one appears, the file is a shared one that a lane should not have touched. Keep both sides' intent, build, and note it in the report.
2. After the four merges:
   - run `--filter "FullyQualifiedName~SampleData|FullyQualifiedName~DemoBranch|FullyQualifiedName~CompanyScopeDemoTests|FullyQualifiedName~ExecutiveDemoExclusionTests"` (DB `qldonhang_integtest_sd`);
   - run `npx vitest run src/features/branches src/pages/settings`.
3. **If `qlbanhang/main` moved since Phase 00** (`git fetch qlbanhang && git log --oneline HEAD..qlbanhang/main`):
   - merge it now (`git merge qlbanhang/main`);
   - re-check the S15 sites and any new company-wide query it adds: search `ReportScope.Company`, `IsCompanyWide` and `_db.Branches` in the new commits. Add the `IsDemo` filter with a test (RED first) for each new site.
4. Remove the lane worktrees:
   ```bash
   cd /D/Projects/QLBanHang
   for w in sd-gen sd-run sd-clean sd-fe; do git worktree remove .claude/worktrees/$w; done
   ```
   `sd-fe` holds a real `node_modules`, not a junction, so this is safe.

   Keep the lane branches until the PR is merged.

### Task 3.2 — End-to-end tests with the real pieces

1. **Write the tests** `SampleData/SampleDataEndToEndTests.cs` (`[Collection(nameof(PostgresCollection))]`, `: ExecutiveDashboardTestBase`, `ITestOutputHelper` in the constructor). A helper `CreateDemoAndWaitAsync(code)`:
   - `POST /api/branches` with `withSampleData: true`;
   - poll `GET /api/branches` every second for up to 300 s until the branch leaves `Running`;
   - fail the test with `sampleDataError` if the branch ends `Failed`;
   - write the elapsed seconds to the test output.

   **`Demo_branch_is_seeded_and_every_report_has_data`** (branch `E2E01`, then `UseBranch(_client, id)`):
   - **status:** `Ready`, progress 100;
   - **invariants:** `AssertInvariantsAsync()` and `AssertCashDebtInvariantsAsync()` pass;
   - **row counts** (`InDbAsync`, branch `E2E01`):
     - stock vouchers ≥ 150; cash vouchers ≥ 60;
     - fund transfers ≥ 12; debt offsets = 1;
     - opening debts = 16; opening stock rows = 36;
     - at least one cancelled cash voucher;
   - **reports** (working branch `E2E01`, period = opening date to yesterday):
     - `stock-on-hand` has rows with a positive total quantity;
     - `stock-summary` (Branch) has in and out quantities;
     - `stock-card` for `DM-SP01` has rows;
     - `cash-book` `kind=Cash` and `kind=Bank` both have rows;
     - `debt-balance` receivable total > 0;
     - `debt-aging` has a non-zero amount in each default bucket (1–30, 31–60, 61–90, > 90);
     - `debt-overdue` has rows;
     - `debt-by-salesperson` has one row per owner;
     - the S12 export of one sample customer returns 200.
   - **executive dashboard:** the sales summary with `branchId = E2E01` has revenue > 0.

   **`Seeding_succeeds_under_block_policies`:**
   - through `InDbAsync`, set `InventorySettings.NegativeStockPolicy = Block`, `CashDebtSettings.CreditLimitPolicy = Block` and `NegativeFundPolicy = Block`;
   - `E2E02` ends `Ready`; invariants pass.

   **`Two_demo_branches_share_the_catalog_and_go_away_cleanly`:**
   - `ReportDataset.SeedAsync(this)` first; read the company `debt-balance` receivable total (`C0`);
   - create `E2E03` and `E2E04` (both `Ready`) → `DM-SP01` exists once; the company total is still `C0`;
   - `DELETE …/E2E03/demo` → the sample catalog still exists;
   - `DELETE …/E2E04/demo` → no `DM-SP*` / `DM-KH*` / `DM-NCC*` / `DM-DT*` rows and no `DM` group (`IgnoreQueryFilters`);
   - the company total is still `C0`; invariants pass.
2. **Run the tests:** `--filter "FullyQualifiedName~SampleDataEndToEndTests"`.
   - These tests check the merged pieces, so they may pass on the first run. Say so in the report.
   - Any failure is a defect in a lane. Reproduce it with a focused failing test in that lane's test class (RED), fix it, re-run both, commit `fix(sample-data): <what>`.
3. **Measure (R1).** Note the seeding time of `E2E01`.
   - If it is above 60 s, shrink `SampleDataVolumes` in section-01's order:
     1. sales to 25 per month;
     2. at most 4 lines per document;
     3. purchases to 8 per month.
   - First change the ranges in `SampleDataGeneratorTests.Monthly_volumes_match_the_spec` and the end-state ranges, as a RED step. Then change the constants, re-run `SampleData.Unit` and the end-to-end tests, and commit `perf(sample-data): smaller dataset`.
   - Record before/after times.
4. **Commit** the end-to-end tests: `git add backend/tests/OrderMgmt.IntegrationTests/SampleData/SampleDataEndToEndTests.cs && git commit -m "test(sample-data): end-to-end demo branch seeding and deletion"`

### Task 3.3 — Browser check on a scratch database

HTTP clients are denied locally, so this goes through the app and a browser:
- use the project's `run` skill or the pattern of earlier plans;
- use the built-in browser or Claude in Chrome (load its skill first).

Never point the app at `qldonhang_test` (the dev DB).

1. Create a scratch database:
   ```bash
   PGPASSWORD=1 "/c/Program Files/PostgreSQL/18/bin/psql" -h localhost -U postgres -c 'create database "qldonhang_sd_scratch"'
   ```
   Start the backend from `$WT/backend` with:
   - `ConnectionStrings__Default="Host=localhost;Port=5432;Database=qldonhang_sd_scratch;Username=postgres;Password=1"`;
   - `Database__AutoMigrateAndSeed=true`;
   - `Seed__AdminPassword=Admin@123`.

   Then start the frontend dev server from `$WT/frontend`.
2. As `admin`:
   1. Open Cài đặt → Chi nhánh → Thêm chi nhánh, and tick the checkbox for `DEMO`.
   2. Watch the badge go from "Đang tạo dữ liệu x%" to "Demo".
   3. Switch to the demo branch in the header (the item shows "(demo)").
   4. Open, and take a screenshot of each:
      - Tồn kho, Thẻ kho, Tổng hợp nhập–xuất–tồn;
      - Sổ quỹ (tiền mặt and ngân hàng);
      - Số dư công nợ, Tuổi nợ, Nợ quá hạn, Công nợ theo NV;
      - a stock voucher list and a receipt with allocations;
      - the executive dashboard for the demo branch and for "Toàn công ty" (no demo figures).
   5. Back in Cài đặt → Chi nhánh, delete the demo branch through the dialog: check the counts, and that the button enables only on the exact code.
   6. Confirm the branch is gone and the header switched back.
3. Stop the servers and drop the scratch DB (`drop database "qldonhang_sd_scratch" with (force)`).
4. Put the screenshots' observations in the report. Visual defects are fixed with a focused frontend test first, then committed `fix(frontend): <what>`.

### Task 3.4 — Docs

1. Update the docs:
   - **`system-architecture.md`:** a "Demo branches and sample data" section:
     - the flow from checkbox to queue, worker, runner and services;
     - the acting identity;
     - status values and recovery;
     - the hard delete order and catalog cleanup;
     - company scope (reports and executive dashboard) excluding demo branches;
     - one dashboard signal per seed.
   - **`conventions.md`:**
     - `IActingIdentity` is set only by background work, never in a request;
     - background jobs go through a singleton `Channel` queue and a `BackgroundService` that catches everything and recovers at start;
     - the demo branch hard delete is the documented exception to "prefer soft delete";
     - a new company-wide query must skip `IsDemo` branches;
     - test doubles go through `WithWebHostBuilder` + `ConfigureTestServices`.
   - **`directory-structure.md`:** `Application/Organization/SampleData/{Models,Interfaces,Services,Generation}`, `WebApi/BackgroundJobs/`, the new frontend files.
   - **`product-goals.md`:** the demo branch feature and its limits (no sample quotations, the S17 cost price limit).
   - **`docs/SUMMARY.md`:** refresh the descriptions of those four docs; mark the plan row "(executed)".
   - **Brainstorm SUMMARY:** add `Plan: [261014-1616-branch-sample-data](../../plans/261014-1616-branch-sample-data/SUMMARY.md)` under "Next steps".
2. Commit: `git add docs && git commit -m "docs: demo branches with sample data"` (check `git status` first: only `docs/` files).

### Task 3.5 — Final verification and report

1. Run the SUMMARY "Final Verification" commands. Compare test by test with `BASELINE.md`.
2. Write `EXECUTION-REPORT.md` (format of `docs/plans/archived/261013-1926-tt133-report-forms/EXECUTION-REPORT.md`):
   - baseline vs final table;
   - commits per phase and lane, merge commits;
   - seeding time and dataset size (steps per type);
   - delete time;
   - browser check notes;
   - deviations from the plan;
   - open items: S16, S17, R7; brainstorm open questions 2 and 3.
3. Mark every phase `[x]` in `SUMMARY.md`. Commit `chore(plan): branch sample data complete`.
4. **Ask the user (in Vietnamese) before anything outward-facing:** push `feat/branch-sample-data` to `qlbanhang` and open a PR. `gh` is not installed: give the compare URL `https://github.com/hongchinh/QLBanHang/compare/main...feat/branch-sample-data` and a PR body. Do not push without a yes.

## Verification

```bash
cd /D/Projects/QLBanHang/.claude/worktrees/branch-sample-data
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_sd;Username=postgres;Password=1"
cd backend && dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~SampleDataEndToEndTests"
dotnet test OrderMgmt.sln --logger "trx;LogFileName=final.trx" --results-directory "$TEMP/sd-final"
cd ../frontend && npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 && npm run build
```

## Exit Criteria

- All four lanes are merged and the lane worktrees are removed.
- A demo branch created through the API ends `Ready`, passes both invariant checks, fills every branch report (including all four aging buckets), and is seeded under `Block` policies too.
- Two demo branches share one catalog; company figures never include them; deleting both leaves no sample catalog.
- The seeding time is measured (≤ 60 s, or reduced per R1 with the numbers recorded); the browser check is done on a scratch DB that is dropped afterwards.
- Docs are updated; the final suites show no new failures against `BASELINE.md`; the report is written; nothing is pushed without the user's yes.
