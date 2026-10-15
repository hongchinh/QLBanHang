# Execution Report — Demo branches with sample data

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/branch-sample-data` (from `qlbanhang/main` `8135477`, not pushed) · Executed 2026-10-14 → 2026-10-15 · Baseline: [BASELINE.md](BASELINE.md) · Design: [brainstorm](../../../brainstorms/261014-1601-branch-sample-data/SUMMARY.md)

## Outcome

All seven phases (00, 01, 02a–02d, 03) are complete. The four Phase 02 lanes ran in parallel, each in its own worktree and branch, and were merged into `feat/branch-sample-data` with `--no-ff`. `qlbanhang/main` was merged twice afterwards (PRs #15–#18). Twelve decisions taken during the run are **pending the user's confirmation** (see "Decisions awaiting confirmation"). Nothing is pushed and no PR is open.

The branch delivers:
- the flags `Branch.IsDemo`, `SampleDataStatus`, `SampleDataProgress`, `SampleDataError`, `Product.IsSample` and `Customer.IsSample` (migration `AddBranchSampleData`), and `CreateBranchRequest.WithSampleData`;
- `IActingIdentity`, so `ICurrentUser` and `ICurrentBranch` work in background work;
- the in-memory job queue and the `SampleDataHostedService` worker, which marks interrupted fills `Failed` at start;
- the deterministic `SampleDataGenerator`, and the `SampleDataRunner`, which writes the plan through the business services in date order;
- `IDemoBranchService`: deletion preview and hard delete with cost price restore and sample catalog cleanup. The normal branch delete refuses demo branches;
- TT133 company-scope reports and the company-wide executive dashboard leave demo branches out;
- frontend: the create checkbox, the "Loại" column with its Demo / progress / error badge and polling, the type-the-code delete dialog, "(demo)" in the branch pickers and a Demo badge on the header switcher.

| Check | Baseline (Phase 00, `e844171`) | Final (`0f3194a`) | Result |
|---|---|---|---|
| `dotnet test OrderMgmt.sln` | 19 failed / 1281 total | 19 failed / 1518 total (1499 passed) | ✅ the same 19 tests, compared by name |
| `npm run test` | 3 failed / 1961 total | 3 failed / 1996 total (1993 passed) | ✅ the same 3 tests |
| `npm run lint` | 3 errors, 13 warnings | 3 errors, 13 warnings | ✅ same as baseline |
| `npm run typecheck` | exit 0 | exit 0 | ✅ |
| `npm run build` | passes | passes | ✅ |

The backend built with 0 errors after every lane merge. The 9 warnings it shows are older nullable warnings outside this plan's files.

**After the final run.** `0f3194a` was followed by a merge of `qlbanhang/main` with PRs #15–#17 (`f97469e`), three fixes (`8ec4f65`, `f35c983`, `b3812e1`) and a merge with PR #18 (`423dd0e`). After the merges:
- a focused backend run passed 658 of 658;
- the frontend suite on `423dd0e` ran 2199 tests: 2196 passed, and the 3 failures are the baseline three;
- lint is at the baseline.

The run notes record no full backend suite, typecheck or build after the merges.

Up to `0f3194a` the suites grew by 237 backend tests and 35 frontend tests. Against `qlbanhang/main` (`93ad183`), the branch changes 118 files, +17,579 / −112 lines (before this report).

## Commits per phase and lane

| Phase / lane | Commits | Notes |
|---|---|---|
| 00 Worktree and baseline | `e844171`, `dd7c34f` | `dd7c34f` records BASELINE.md |
| 01 Foundation | `7e76e50` → `41a1b7f` (7 commits), marker `d91de9f` | migration `AddBranchSampleData`; the lanes started from earlier Phase 01 commits (D-1) |
| 02a Lane A — generator (`feat/branch-sample-data-gen`, from `49fe77f`) | `a007260` → `5254b4a` (13) | merged last as `bb7fe9f` |
| 02b Lane B — runner and worker (`feat/branch-sample-data-run`, from `4892a0e`) | `07f1d1f` → `ccac812` (15) | merged as `eacd0e5` |
| 02c Lane C — demo delete and company scope (`feat/branch-sample-data-clean`, from `4892a0e`) | `087f3f1` → `e82850c` (14) | merged first as `6793258` |
| 02d Lane D — frontend (`feat/branch-sample-data-fe`, from `7e76e50`) | `8ff11cf` → `6f86ec3` (11) | merged as `b5c7625` |
| 03 Merge, end-to-end, docs, verification | `797d0bf`, `52bfa0d`, `0ab4279`, `f6b3b89`, `ad4320c`, `0f3194a`; after the final run `8ec4f65`, `f35c983`, `b3812e1`; this report | see below |
| Upstream | `f97469e` (PRs #15–#17), `423dd0e` (PR #18) | conflicts resolved by hand |

**Order on the feature branch:** `6793258` (lane C) → `797d0bf` → `eacd0e5` (lane B) → `b5c7625` (lane D) → `52bfa0d` (docs) → `bb7fe9f` (lane A) → `0ab4279` → `f6b3b89` → `ad4320c` → `0f3194a` → `f97469e` → `8ec4f65` → `f35c983` → `b3812e1` → `423dd0e`.

The plan's merge order was C → A → B → D. Lane A merged last because it was still in its last fix round (D-11). The lanes own disjoint files: no lane merge had a conflict, and the backend built after each one.

**Phase 03 commits:**
- `797d0bf` — the normal branch delete answers 409 `DEMO_BRANCH_USE_DEMO_DELETE` for a demo branch (from the C.5 review).
- `52bfa0d` — docs: system architecture, conventions, directory structure, product goals, `docs/SUMMARY.md`, the brainstorm's plan link.
- `0ab4279` — the runner passes `InventorySettings.NetExcludesVat` to the generator.
- `f6b3b89`, `ad4320c` — the end-to-end tests, then tighter checks in them.
- `0f3194a` — a Demo marker on the closed header switcher (browser check finding).
- `8ec4f65` — the demo delete also refetches the product, partner, product group and warehouse caches.
- `f35c983` — the switcher badge is a `span`: a `div` inside the trigger button broke the HTML content model. `badgeVariants` moved to `badge-variants.ts`.
- `b3812e1` — a generator failure shows a fixed Vietnamese text instead of the raw .NET message. The cause stays in the inner exception, which the runner logs.

**Upstream merges:**
- `f97469e` brought PRs #15 (TT133 voucher print forms), #16 (PWA push on iOS) and #17 (mobile header overflow).
  - Conflicts: `Tt133ExportService.cs`, `docs/SUMMARY.md`, `header-branch-switcher.tsx`.
  - PR #15 moved the company opening-date query into the new `Tt133ExportSupport`. The merged file keeps the demo filter (`Tt133ExportSupport.cs:64`), and `CompanyScopeDemoTests.Tt133_company_opening_date_skips_demo_branch` pins it.
- `423dd0e` brought PR #18 (app icon registry). Conflicts: `docs/SUMMARY.md`, `docs/codebase/directory-structure.md`, `branches-page.tsx`.
- Task 3.1 step 3 asks for a check of new company-wide queries after an upstream merge. A search of the upstream backend diff (`8135477..93ad183`) for `ReportScope.Company`, `IsCompanyWide` and `_db.Branches` finds only the moved opening-date query.

## How it was run

- Phases 00 and 01 ran in the feature worktree. The baseline ran in a detached worktree `sd-baseline` on `qldonhang_integtest_sd_base` while Phase 01 ran (37 minutes under load).
- Lanes A–D ran in parallel, each in its own worktree (`sd-gen`, `sd-run`, `sd-clean`, `sd-fe`). Lanes A–C each had their own test database (`_sd_gen`, `_sd_run`, `_sd_clean`).
- Each task: an implementer wrote the test first and reported the RED line. Then a spec review and a quality review, with fix rounds and re-reviews until approved. Reviewers often removed a rule on purpose (mutation) to check that a test fails.
- Phase 03 ran on the feature branch: merges, end-to-end tests, the browser check on a scratch database, docs, final verification.
- Tooling incidents:
  - python heredocs left `python3 -` processes waiting on stdin (killed twice);
  - one late script re-applied D.3's test edits as an uncommitted duplicate in `sd-fe`. It was reverted with `git checkout --`; the content was already in `9e0622d`;
  - later prompts forbade heredocs, scripts and `git stash`. The C.1 implementer had used `git stash` once to show RED; the stash stack was empty afterwards.

## Review outcomes

Every task had a spec review and a quality review. Most quality reviews came back "with fixes", and the fixes were re-reviewed. The notable defects:

- **Rolling-deploy overlap (B.6 review).**
  - During a deploy overlap, the old instance's "Ready" write could turn a branch that the new instance's recovery had marked `Failed` back into `Ready`.
  - Every runner status write (progress, error, ready, failed) now requires the branch to be demo and `Running`. The runner no longer sets `Running` itself (D-5). A progress write that updates no row stops the run (`c7a240a`, `d94d587`, `36c38a8`).
- **Catalog lock race (C.2/C.4 re-review, `36c38a8` re-review).**
  - The runner's catalog item and the last-demo cleanup could interleave: the cleanup deletes sample products that a new demo branch's runner is reusing.
  - The re-review also found that the re-read after the lock compared EF original values.
  - Fix in `ccac812`: the catalog item first locks the product keys of every existing sample product, then reads. READ COMMITTED is load-bearing; `RunSnapshotAsync` would break it. A theoretical window remains (see "Residual risks").
- **Generator early collection and the end-state tail (A.4 fix and A.5 reviews).**
  - The short-till deposit day forced an early collection that bypassed the end hold, in 13% of runs. Fixed in `c60203f`.
  - The implementer then reported "0 failures", measured on its own input sets. On the reviewer's sets, receivables fell below 400M on about 0.5% of inputs, and a new invariant failed on 3 inputs ("limited customers also buy on credit").
  - The last round (`5bdfae9`) steers receivables to their value at T and pins an early credit sale for the limited customers. After three review cycles the remaining tail (about 0.2%) was accepted (decision 11).
- **Reference checks of the catalog cleanup (`f3b8873` re-review).**
  - No test pinned the cleanup's individual reference checks. Dropping one would have left a 409 `DEMO_BRANCH_BUSY` forever without failing a test.
  - `67bdd0b` pins each of the nine checks with a sample row that holds only that one reference (`Every_reference_check_keeps_its_own_row`; each check mutation-verified). A tripwire test lists the foreign keys (`collate "C"`, with `confdeltype`).
- **TT133 demo filter at the upstream merge.** PR #15 moved the company opening-date query to `Tt133ExportSupport`, so the `Tt133ExportService.cs` conflict could have dropped the S15 filter. The merged file keeps it (see "Upstream merges").

Other defects found by the reviews and fixed:
- A normal delete of a `Running` demo branch could leave sample documents on a soft-deleted branch, which the demo delete then answers with 404 (C.5 review → `797d0bf`).
- Soft-deleted demo branches must stay out of company scope: `IgnoreQueryFilters` at four sites, with tests (`e1f6103`).
- A learner could change a reused sample product's pricing mode, stock tracking or VAT-inclusive price, which would break the generator's margins (`6bd6098`):
  - with no ledger rows, the settings are reset;
  - with ledger rows, the catalog item fails with "Hàng mẫu DM-SP01 đã bị đổi theo dõi tồn kho. Hãy xóa các chi nhánh demo rồi tạo lại."
- The generator ignored `NetExcludesVat` (A.4 review). Without the wiring of `0ab4279`, E2E05 fails with "CQ-0006: NEGATIVE_FUND_BLOCKED … QTM: Âm 4.593.020".
- `SampleRandom.NextDecimal` returned the minimum on fractional ranges (A.1 review).
- `ActingIdentityHolder.Set` now throws when the identity is already set in the scope (1.2 review).
- `Jobs_run_one_after_another` passed one time in three against a concurrency mutation, so unit tests now pin the worker's order (B.6 review).

The end-to-end tests passed on their first run, so no lane defect showed there. The Phase 03 review approved them, and `ad4320c` tightened them.

## Measurements

### Seeding time (R1)

- E2E01 (`Demo_branch_is_seeded_and_every_report_has_data`) over four runs: 54.2 s (loaded machine), 29.7 s, 19.4 s, 57.4 s (51.5 s of it in the steps). The other end-to-end branches took 13–57 s.
- The reviewer's run: E2E01 18.4 s, E2E02 22.6 s, E2E03 67.3 s (heavily loaded machine), E2E04 40.0 s, E2E05 17.4 s.
- Browser: `DEMO` 18.3 s in the UI (the runner: 259 steps in 18.07 s); `DEMO2` 34.8 s under load.
- **R1 not triggered.** E2E01 stayed at or below 60 s in all four runs, so `SampleDataVolumes` is unchanged. The time depends strongly on machine load: one branch took 67.3 s on a busy machine.

### Dataset size (E2E01)

- 170 stock vouchers with 569 lines: XBH 123, NMH 35, NTL 5, XKH 4, XTL 3.
- 146 cash vouchers: 74 automatic (TPX 59, CPN 15) and 72 from plan steps (TKH 30, CK 20, TNCC 16, TK 5, CHKH 1).
- 19 fund transfers, 1 debt offset, 16 opening debts, 36 opening stock rows.
- About 263 plan steps plus 29 setup items.
- Every count meets the plan's minimums (≥ 150 stock vouchers, ≥ 60 cash vouchers, ≥ 12 transfers, exactly 1 offset, 16 opening debts, 36 opening stock rows).
- For reference, the generator's plan for `("DEMO01", 2026-10-14, 3)` at the A.6 checkpoint (`9978d76`) had 255 steps: XBH 123, NMH 35, NTL 8, XTL 3, XKH 3, TKH 26, TNCC 12, CK 18, TK 5, CHKH 1, CQ 19 (16 QTM → VCB, 3 VCB → TCB), BT 1, HUY 1. In the `c60203f` sweeps no plan went above 304 steps (limit 320).

### Delete time

- `DD01` (lane C test): 197 ms.
- E2E03 / E2E04:
  - 1232 / 1060 ms on a loaded machine;
  - 297 / 203 ms and 114 / 119 ms in two other runs;
  - 188 / 120 ms in the reviewer's run;
  - 347 / 477 ms at `ad4320c`.
- Browser: 425 ms on the server for `DEMO` (344 documents in the preview).
- Overall 114–1232 ms, depending on load.

### Generator end-state sweeps (lane A, `5bdfae9`)

- The reviewer's boundary set, 2,400 (branch code, date) inputs: 3 miss one end-state target (2 of them receivables below 400M).
- The wide set, 1,630 inputs: 6 misses:
  - receivables of 364M and 373M;
  - monthly revenue of 788–799M on three inputs;
  - one first month with 7 NMH.
- The mechanism set, 208 inputs: 0 misses.
- The invariants (stock, funds, credit) failed on no input of any set.
- In all, about 0.2% of inputs miss one target. This tail was accepted (decision 11).

### Focused test runs

| Where | Runs |
|---|---|
| Lane A | `SampleData.Unit` 105/105 three times at A.6 (`9978d76`); 130/130 at `c60203f`; 116/116 at `5bdfae9`; 117/117 at the lane tip `5254b4a` |
| Lane B | SampleData filter 54/54 at B.6; 63/63 at `c8779a6`; 69/69 at `c7a240a`; 71/71 at `6bd6098` (first run 70/71: one Npgsql `TimeoutException` under load); 73/73 at `d94d587`; 78/78 at the lane tip `ccac812`. ExecutiveDashboard + CashDebt.Reports + BranchOpeningDateTests 353/353 (26 min) |
| Lane C | ExecutiveDemoExclusionTests 4/4 and ExecutiveDashboard 295/295 at C.6 (`f9b7fb2`); 34/34 after `01c0ff0`; C.7 checkpoint (SampleData, ExecutiveDashboard, CashDebt, Tt133, Inventory) 1077 of 1079, with 2 Npgsql timeouts in the test fixture and 7/7 on a class rerun; 36/36 at `443111c` |
| Lane D | after its merge: `npx vitest run src/features/branches src/pages/settings` 56 tests in 12 files passed |
| Phase 03 | after C, `797d0bf`, B and D: 116/116 (3 min 11 s); Branch filter 152/152 at `797d0bf`; after A: 231/231; end-to-end 4/4 on the first run (`f6b3b89`); 136/136 (4 end-to-end + 132 unit) at `ad4320c` |

## Browser check (Task 3.3)

**Setup.** Scratch database `qldonhang_sd_scratch`, dropped afterwards. Backend on port 5061 and Vite on 5183, so the main checkout's servers on 5050/5173 were left alone. The connection string key was `ConnectionStrings__DefaultConnection`, which the code prefers.

**Seeding:**
- Creating `DEMO` showed the toast "Đã tạo chi nhánh demo / Dữ liệu mẫu đang được tạo…".
- The badge went from 0% through 86% to "Demo".

**Reports on the demo branch.** Every page had data, and the figures agree across reports:
- stock on hand: 37 rows;
- stock card `DM-SP01`: in 3,891, out 2,742;
- cash book: QTM, TCB and VCB;
- debt aging: four non-zero buckets;
- overdue: 135.7M;
- dashboard of `DEMO`: revenue 2.95 tỷ, receivables 446.8M.

**Company scope.** The company dashboard showed all figures at 0 and no DM rows. The company debt balance and stock summary were empty.

**Demo delete:**
- The dialog's preview counted 344 documents.
- The wrong codes `DEMOX`, `demo` and `DEM` kept the button disabled.
- The delete took 425 ms on the server, and the sample catalog was gone afterwards.

**Failed state.** With `DEMO2` set to `Failed` by SQL, the badge read "Lỗi tạo dữ liệu" with the title "XBH-0001: Lỗi thử".

**Found and fixed:**
- The closed header switcher clipped its label, so "(demo)" was visible only in the tooltip and the open list. Fixed in `0f3194a` (test first), then reworked in `f35c983`.
- A static render of the trigger with the built CSS shows the Demo badge beside the chevron for long and short labels: `…\scratchpad\badge\shot.png`.

**Not checked:**
- users moved to the main branch on delete (no user had the demo branch as default);
- owners spread over several salespeople (the scratch database had no SALES users).

**Screenshots.** 51 PNG files in `C:\Users\admin\AppData\Local\Temp\claude\d--Projects-QLBanHang\1e8743de-94c6-47e4-b773-c87de664f673\scratchpad\browser-check\shots\` (`01-login.png` … `23f-switcher-after-DEMO2.png`). The browser scripts are one level up. These are temporary session files and are **not committed**.

## Deviations from the plan

| # | Deviation | Reason |
|---|---|---|
| D-1 | The lanes did not start from the Phase 01 tip `d91de9f`. Lane D started from `7e76e50` (after Task 1.1), lane A from `49fe77f` (after Task 1.3), lanes B and C from `4892a0e` (Tasks 1.1–1.4 and the 1.2 fixes). | Wall-clock time. No lane touches a file of the later Phase 01 commits. |
| D-2 | Task 1.3 started while Task 1.2's quality review ran, and Task 1.4 while Task 1.3's review ran. | The reviewers only read; saves time. |
| D-3 | D.1: the preview query key is `branchKeys.demoDeletion` in `features/branches/keys.ts`. That file is not in the lane list, and the plan wrote the key inline. | `conventions.md` puts query keys in `keys.ts`. The plan-versus-docs conflict was decided for the docs (decision 9). |
| D-4 | Files outside the lane lists:<br>• lane A: `Generation/SampleCatalogModels.cs`, `Unit/SampleDataGeneratorMechanismTests.cs`, and the Phase 01 contract `Models/SampleDataPlan.cs` (generator input `NetExcludesVat`);<br>• lane B: `SampleDataPlanException.cs`, `SampleRunState.cs`, `FakeSampleDataGenerator.cs`, `SampleDataRunnerConflictTests.cs`, `Unit/SampleDataErrorFormatTests.cs`, `Unit/SampleDataHostedServiceUnitTests.cs`, and the doc comment of `ISampleDataRunner.cs` (Phase 01 file);<br>• lane C: the doc comment of `IExecutiveScopeResolver.cs`. | Models, test helpers and doc comments that the reviews asked for. No file was shared between lanes. |
| D-5 | The runner does not set the branch `Running` at start (phase-02b "Runner behaviour" step 1). Only `BranchService.CreateAsync` sets it. Every runner status write requires a demo branch in `Running`. A job for a branch that is not `Running` writes nothing and returns `Succeeded = false` ("Không tìm thấy chi nhánh demo đang tạo dữ liệu mẫu."). | Rolling-deploy overlap (see "Review outcomes"). |
| D-6 | The runner refuses a missing or non-demo branch with "KE-HOACH: Không tìm thấy chi nhánh demo để tạo dữ liệu mẫu." and does not touch the row. | Fail fast instead of filling a real branch (B.1 review; decision 3). |
| D-7 | Error texts differ from plan B.5, which expected "KE-HOACH: boom", the generator's own message. The rules of decision 1 apply, and since `b3812e1` a generator failure reads "KE-HOACH: Không tạo được kế hoạch dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại." | `SampleDataError` is visible to every signed-in user (`GET /api/branches` is `[Authorize]` only). |
| D-8 | Reused sample rows are made usable again, and reused products get their settings reset or raise a conflict. S13 only said "reuse". | Decision 4. |
| D-9 | The catalog item runs in a transaction that locks the sample product keys first. S8 expected the single reader to make a catalog lock unnecessary. | The last-demo cleanup runs outside the queue (catalog lock race). |
| D-10 | The demo delete also recomputes `CostPrice` of the sample products used by the branch's stock-ins; the plan named real products only. | A surviving sample product would otherwise keep a deleted voucher's price. Their keys are already locked, so the cost is negligible. |
| D-11 | Merge order C → B → D → A, with `797d0bf` between C and B and the docs commit `52bfa0d` before A. The plan said C → A → B → D. | Lane A was in its last fix round. The files are disjoint, and the build was clean after each merge. |
| D-12 | `797d0bf`: the normal delete refuses demo branches with 409 `DEMO_BRANCH_USE_DEMO_DELETE` "Chi nhánh demo phải xóa bằng chức năng Xóa chi nhánh demo.". Not in the plan. | C.5 review: a soft delete during `Running` stranded sample documents. |
| D-13 | `0ab4279`: the runner reads `InventorySettings.NetExcludesVat` and passes it to the generator. | A.4 review: with the setting on, the estimated totals were wrong, and fund policy `Block` failed (decision 10). |
| D-14 | End-to-end tests differ from the plan:<br>• a fourth test, `Seeding_succeeds_with_net_excludes_vat_under_block_fund_policy` (E2E05);<br>• E2E01 creates 3 SALES users;<br>• debt aging is checked in its default buckets 0–30 / 31–60 / 61–90 / > 90 (the plan's 1–30… buckets are debt-overdue's);<br>• debt-overdue at VN today must have an amount in each of its 4 buckets. | Covers D-13; owners for the salesperson report; the real end-to-end proof of the generator's overdue promise. |
| D-15 | Generator rules that go beyond section-01 or phase-02a. | See decisions 8 and 11. |
| D-16 | Header switcher:<br>• the trigger's `title` is the full label (D.5);<br>• the closed trigger shows a Demo badge (`0f3194a`, `f35c983`).<br>Plan D.5 had "(demo)" in the items only. | The trigger clips the label (browser check). |
| D-17 | D.4: Enter submits the demo-delete dialog, and the confirm button also waits until the preview is neither refetching nor failed. | Decision 7. |
| D-18 | The browser check used ports 5061/5183 and the key `ConnectionStrings__DefaultConnection` (the plan wrote `ConnectionStrings__Default`). | The main checkout's servers held 5050/5173; the code reads `DefaultConnection` first. |
| D-19 | Closing step:<br>• the lane worktrees are removed after this report, not in Task 3.1;<br>• the plan is archived on this branch before the PR (the TT133 plan was archived after its merge);<br>• the commit is `docs(plan): branch sample data execution report`, not `chore(plan): branch sample data complete`. | The user's instructions for the closing step. |

## Decisions awaiting confirmation

These were decided during the run by the executing session, not by the user. **All are pending the user's confirmation.**

1. **Run error texts.** `FormatError` writes:
   - FluentValidation → its first error;
   - policy exceptions → "{Code}: {Message} ({detailKey}: {detail})";
   - other `DomainException` → its message;
   - `SampleDataPlanException` → its message (the generator failure has the fixed text of D-7);
   - `DbUpdateConcurrencyException` and unique violation 23505 → the Vietnamese texts of `GlobalExceptionMiddleware`;
   - anything else → "Lỗi hệ thống khi tạo dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại.", with the full exception logged.

   The text is cut to 1000 characters, surrogate-safe.
2. **Sample group `DM`.** An existing `DM` group counts as the sample group only when its name is "Dữ liệu mẫu":
   - a live `DM` group with another name fails the job with "Mã nhóm DM đã được dùng cho dữ liệu thật.";
   - a soft-deleted one is ignored, and a new sample group is inserted.

   The cleanup deletes the group only when it is the sample one. Consequence: renaming the sample group makes later runs fail.
3. **Non-demo refusal.** The runner refuses a missing or non-demo branch without touching it (D-6).
4. **Reused sample rows are made usable** (B.2, B.7). Beyond S13's "reuse":
   - products and partners are set active, and partner roles widened (`IsCustomer` / `IsSupplier` |= spec);
   - a missing product group is re-attached, and the `DM` group is set active;
   - a reused product with no ledger rows gets `PricingMode = PerUnit`, `TrackInventory` from the spec and `PriceIncludesVat = false`;
   - a reused product with ledger rows and changed settings fails the catalog item.
5. **Code lookup.** A live row decides: a sample row is reused, a real row is a conflict. Otherwise a soft-deleted sample row is restored. With only soft-deleted real rows, a new row is inserted. This matches the app's own create rules.
6. **Switcher title** is the full selected label, "(demo)" included (D.5).
7. **Demo delete dialog.** Enter submits it, and confirming needs a settled preview (D.4).
8. **Opening-debt ages (A.3 review).**
   - The 61–90 overdue bucket could not be reached with ages relative to the opening date. The bad-debt customer KH20 therefore gets a second never-paid opening item, aged from T (75–87 days overdue at T).
   - One KH06 item was dropped, which still gives 10 items for 8 customers.
   - Ages follow the habits: on-time ≤ CreditDays, late ≤ CreditDays + 30, payables ≤ CreditDays. Habit dates before the opening date move to the first working days.
   - All ages stay inside [Opening − 120, Opening − 10].
9. **Query key in `keys.ts`** (D-3).
10. **`NetExcludesVat` wired** into the generator input (D-13).
11. **Generator trade-offs (A.4, A.5):**
    - **Credit share.** A receivables band overrides "about 30% pay at once": credit is about half of non-retail revenue instead of about 70%.
    - **Receivables band** of 540–580M, steered to the value at T. The executing session had suggested about 500M, but 470–540M gave 2 runs below 400M.
    - **Volumes at the edge of the spec ranges.** XBH 35–36 per month (spec 35–45) keeps four-month windows at or below 320 steps.
    - **Reordering.** `ReorderWeeks` 1.75 for the spec's "about one week" (A.4 used 1.5). The purchase pace is about 11 per month, restocking only the triggering group.
    - **Bad debt.** The bad-debt customer pays new invoices 60–90 days after due (spec "late" = 30–60).
    - **Gathering.** Windows of 28 days for receipts and supplier payments and 30 days for early collection (A.4 had 7 and 14).
    - **Revenue pacing** to 930–1,010M per month, which scales quantities by 50–250%.
    - **End of the period:**
      - a run-down of 5 products that are not reordered in the last 16 days, with a clearance line on each sale;
      - an end hold of 10 days;
      - two planned, held credit sales (due T − 15 and T − 40).
    - **Early limited-customer sales.** The first two XBH on or after opening + 3 go to KH08 and KH10 as single-line credit sales; the second may slip one working day.
    - **Simplifications.** The credit stop and the gather floor were removed (`c60203f`), and the shortfall cap too (`5bdfae9`).
    - **Other A.4 interpretations:**
      - a Saturday deposit rule, so every week moves QTM → VCB;
      - the mistaken TK receipt is cancelled the same day;
      - XKH lines have price 0 and VAT 0;
      - VCB keeps an 80M reserve.
    - **Accepted tail.** About 0.2% of (branch code, date) inputs miss one end-state target (see "Measurements").
12. **S14: users moved to the main branch on delete** (C.2 review #9). A trainee without `branches.access_all` whose default branch was the demo branch then works in the real main branch, with their role's write rights; an open form saves into main. Options:
    - keep as planned;
    - deactivate those users;
    - revoke their refresh tokens.

## Open items, follow-ups and residual risks

### Follow-ups (not done)

- **Catalog caches after a fill (D.1 review #5).** The demo delete half was fixed in `8ec4f65`. Running → Ready still refetches no product, partner or warehouse cache (30 s stale time).
- **Preview retries (D.1 review #6).** The preview query keeps the global `retry: 1`, so a 404/409 preview error shows after one retry.
- **No polling test (D.1 review #3).** No `renderHook` test covers the polling wiring of `useBranches`; the browser check covered it by hand.
- **Opening date during a fill (D.3 review #5).** "Đặt ngày đầu kỳ" stays enabled on a demo row while it is `Running`. A changed opening date can make later steps fail. Consider disabling it or a backend guard.
- **Fill error only in a tooltip (D.3 review #3; accessibility).** The error is only a `title` tooltip on a non-focusable badge, so touch and keyboard users never see it.
- **"(demo)" marker missing** in three places: the user form's default-branch picker, the warehouse page and the period-lock page.
- **Real creation times.** Sample documents carry the real creation time, so the "Vừa phát sinh" feed and the receipt history show today.
- **Unit not checked.** `UnitId` is not checked or reset on reused sample products. The unit name is display-only, and per-unit quantities are unaffected. The settings conflict names only the first changed product.
- **Missing tests:**
  - no test pins the HOAN-TAT key (the case where `SetReady` finds the branch no longer `Running`);
  - optional: a real race test of `DeleteAsync` against the catalog item.
- **Not taken:**
  - a runner-side partner check on skipped offsets (the generator guarantees it, and `DebtOffsetService` enforces it on execution);
  - an owner pass-through regression test.
- **Optional:** one fixed advisory key "sample-catalog" on both sides, to close the remaining catalog window.

### Residual risks

- **Catalog window.** The catalog item takes no keys when the sample products do not exist yet, or were committed after the cleanup read their ids. The result would be a failed run, not corruption; delete and recreate the branch.
- **Quotation-line race in the catalog cleanup.** A quotation line saved with a sample product while the last-demo cleanup deletes that product loses its product link, because the key is `ON DELETE SET NULL`. The window is tiny. Recorded next to S17.
- **Unbounded resource use.** Nothing caps the number of demo branches, and the in-memory queue is unbounded, so database size, queue memory and seeding time have no limit. Only `branches.manage` users can create demo branches.
- **Acting identity.** It holds every permission code (S4). Harmless with the seeded roles.
- **Branch stuck in Running.** If writing `Failed` fails three times (about 1.5 s of retries, e.g. a database outage), the branch stays `Running` until a restart. The demo delete refuses `Running` branches, so the user is stuck until then. Follow-up: a longer backoff, or recovery on demand for branches with no active job.
- **One instance.** The in-memory queue and the status writes assume a single backend instance (documented in `system-architecture.md` and `conventions.md`).
  - Queued jobs are lost when the process stops; their branches fail at the next start.
  - During a rolling-deploy overlap, a delete of a branch that the new instance has failed, while the old instance still writes into it, may answer 409 (retry), or the old job fails on a foreign key. Acceptable.
- **Determinism across runtimes.** The seeded `System.Random` stream is not guaranteed across .NET major versions, so a runtime upgrade may change the plans. Determinism holds within one runtime.
- **Renamed sample group.** Renaming the `DM` group makes later runs fail (decision 2).
- **Ledger scan.** The demo delete scans `inventory_ledger`, which has no index led by `branch_id`. Fine at the expected scale.
- **Cancelling with settlements.** Cancelling a voucher with settlements removes them (`ConfirmRemoveSettlements`). Generated plans only cancel a fixed TK receipt, so this path is unused.
- **Extra dashboard refetches.** Demo-branch writes still signal `dashboard-all`, so company viewers refetch. The figures are filtered and correct; only the refetch is wasted.
- **Demo rows count until the delete:**
  - the "has activity" checks of `ProductService` count demo ledger rows of real products until the demo branch is deleted;
  - the company TT133 header prints no opening date when every branch with an opening date is a demo branch.
- **Test environment:**
  - Npgsql "Timeout during reading attempt" on the fixture's maintenance connection when several Postgres-heavy suites run in parallel (seen in lanes B and C);
  - seeding times above 60 s under load;
  - every test host now runs the worker. The template host logs "Sample data recovery skipped" (expected). `BranchSampleDataFlagsTests` uses a recording queue, so it fills no real branch.

### Older UI issues seen in the browser check (present on `8135477`)

- **Toast viewport.** It lies over the header: while a toast shows, it blocks clicks on the branch switcher and covers "Thêm chi nhánh".
- **Number formats.** Quantities use the browser locale; amounts use `vi`.
- **"+" icon.** The icon on "Thêm chi nhánh" is nearly invisible.
- **Radix warnings.** Radix warns about a missing `Description` in the create dialog.

### Plan items carried over

- **S16 — learners writing during seeding.** Not blocked. A learner's document can make a later step fail; the branch then ends `Failed` and is deleted and recreated. The opening-date follow-up above is one case of it.
- **S17 — cost price limit.** If a learner's demo stock-in was the only stock-in of a real product, the recompute after the delete keeps the demo price.
- **R7 — dashboard hub group after a delete.** An open SignalR connection keeps the deleted branch's group until it reconnects. Harmless.
- **Brainstorm open question 2.** No "Mẫu" badge in catalog lists (default taken: not done).
- **Brainstorm open question 3.** One `DM` product group (default taken), so group reports in a demo branch show one group.

## Not done

- **Push and PR** (Task 3.5 step 4). They wait for the user's yes. `gh` is not installed; the compare URL is `https://github.com/hongchinh/QLBanHang/compare/main...feat/branch-sample-data`.
- **Two browser-check items:** user reassignment on delete, and owners spread over several salespeople (see "Browser check").
- **Checks after the upstream merges.** The run notes record no full backend suite, typecheck or build (see "After the final run").

## Leftovers

- **Lane branches.** `feat/branch-sample-data-gen`, `-run`, `-clean` and `-fe` stay until the PR merges.
- **Closing cleanup.** It runs right after this report is committed:
  - it removes the lane worktrees;
  - it drops this plan's test databases (`qldonhang_integtest_sd*`);
  - it retries deleting the folder of the detached baseline worktree `sd-baseline`. Git deregistered that worktree, but the folder could not be deleted earlier ("Permission denied").
- **Session scratchpad** (temporary, not committed): the run notes, the browser-check screenshots and scripts, and the generator sweep outputs.
- **Main checkout.** The untracked copies of the brainstorm and the plan there are stale. Delete them once the branch is merged into `main` and pulled (Phase 00 Task 0.1).
