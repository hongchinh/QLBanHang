# Cash & Debt Round 2B — Debt analysis reports (Đợt 2B — Báo cáo công nợ)

> Created: 2026-10-12 17:09:46 · Source design: [brainstorm SUMMARY](../../brainstorms/261007-2252-cash-debt-round2/SUMMARY.md), [section-02 §11 reports](../../brainstorms/261007-2252-cash-debt-round2/section-02-design.md#11-báo-cáo), [section-03 §3 split](../../brainstorms/261007-2252-cash-debt-round2/section-03-delivery.md#3-phương-án-tách-2a--2b-review-đề-xuất-tách-chốt-khi-viết-plan) · Builds on: [Round 2A plan](../archived/261007-2314-cash-debt-round2a/SUMMARY.md) (F-10), [Round 2A review refactor](../archived/261012-0433-round2a-review-refactor/SUMMARY.md) · Follow-up list: [stock-voucher-clone SUMMARY](../../brainstorms/261006-2139-stock-voucher-clone/SUMMARY.md#follow-up--chưa-làm-kiểm-tra-2026-10-12)

## Goal

Deliver the five debt reports that Round 2A left out, read-only from the existing ledger (`DebtEntry` + `DebtSettlement`), with no schema change:

- **Công nợ theo chứng từ** (report 3): open documents and unallocated payments per partner at a date.
- **Tuổi nợ** (report 4): open amounts per partner split into age buckets the user sets on the screen.
- **Nợ quá hạn** (report 5): overdue documents per partner at a date, split into overdue-day buckets. This report is also the **overdue warning list**; the stock-out customer panel links to it.
- **Công nợ theo nhân viên** (report 6): per owner of the debt document, opening open, new debt, settled and closing open in a period, with the overdue part and an "unallocated" line.
- **Biên bản đối chiếu công nợ** (report 7): on-screen reconciliation of one partner, both sides when the partner is a customer and a supplier, with the net amount. Print/Excel stays in Round 3.

Every report runs in the working branch behind the existing `reports.debt` permission. The documents, aging and by-salesperson totals reconcile with the Số dư công nợ report (B4); the reconciliation report reuses the ledger; the overdue report is gross by design (B6: the unallocated amount is shown, not subtracted).

## Scope

**In scope**

- Backend:
  - pure calculator `DebtAging` (bucket boundaries, bucket index);
  - shared internal loader `DebtPositionQuery` (entries and settled amounts that count at an instant, P29 window rule);
  - new service `IDebtPositionReportService` with `debt-documents`, `debt-aging`, `debt-overdue`, `debt-by-salesperson`;
  - `debt-reconciliation` added to the existing `ICashDebtReportService` (it reuses the ledger builder);
  - `GET /api/opening-debts/owners`: the active users offered as the owner of an opening debt (B1);
  - validators, controllers, DI; integration tests against the Phase 09 report dataset with hand-computed numbers.
- Frontend:
  - types, API, keys and hooks in `features/cash-reports`;
  - the bucket-boundary helper;
  - a shared report partner picker (extracted from the ledger page);
  - five report pages, their routes, route permissions and sidebar items;
  - an owner picker (Nhân viên) on the opening-debt form and a Nhân viên column on the opening-debt list (B1);
  - the "Xem nợ quá hạn" link on the stock-out customer debt panel.
- Docs: `product-goals.md`, `system-architecture.md`, `conventions.md`, `docs/SUMMARY.md`, the follow-up list in the stock-voucher-clone brainstorm SUMMARY.

**Out of scope**

- `StockVoucher.SalespersonId` (user decision 2026-10-12, see B1).
- A dashboard widget or a save-time overdue warning on the stock-out form (user decision 2026-10-12, see B2).
- Print/Excel of any report, including the reconciliation template: **Round 3**.
- Debt per product, foreign currency (brainstorm non-goals). Cross-branch payment and bank accounts shared by several branches stay open BA questions (B11); every 2B report is branch-scoped.
- Round 2A follow-ups F-1 … F-9 (separate fixes); F-8 (performance) is noted as a risk below.
- Snapshot tables for "as-at" reports (YAGNI, brainstorm section-03 §6).

## Decisions made while planning (in addition to the brainstorm and P1–P29 of Round 2A)

| # | Topic | Decision |
|---|---|---|
| B1 | Employee on debt (user decision 2026-10-12) | Keep P2: the report groups by `DebtEntry.OwnerUserId` (the owner of the source voucher). No `SalespersonId`. For an opening debt the owner is the optional user chosen on the row: **2B adds the owner picker** ("Nhân viên", clearable) to the opening-debt form dialog and a "Nhân viên" column to the opening-debt list (2A deliberately hid it and left the decision to 2B). Options come from the new `GET /api/opening-debts/owners` (permission `debt.opening_balance`, active users, ordered by name), because `/admin/users` needs `users.view`. An entry without an owner shows on a row "Không xác định". Existing opening debts keep their owner (an edit sends the picked value; the 2A rule "an edit keeps the loaded owner" is replaced by the picker's value). |
| B2 | Overdue warning list (user decision 2026-10-12) | The **Nợ quá hạn** report, defaulting to today, is the warning list. No dashboard widget and no save-time warning. The stock-out customer debt panel shows a "Xem nợ quá hạn" link to the report (filtered to that customer) when its overdue amount is > 0 and the user holds `reports.debt`. The link opens in a **new tab** so the unsaved voucher is not lost. The panel subtracts settlements of every date from its overdue amount, the report only those counted at the report date (B3), so the two can differ for a customer with future-dated settlements; accepted. |
| B3 | "As at D" window | An entry **counts at D** when `PostedAt < StartOfNextDay(D)` **or** `SourceType == Opening` (P29, the same rule as the balance report's closing). A settlement **counts at D** when **both** of its entries count at D. For non-Opening entries this equals `EffectiveAt < StartOfNextDay(D)`, because `EffectiveAt = max(PostedAt)`; it also keeps a settlement between two Opening entries in the opening balance. Open of an entry at D = `Amount − Σ settlements that count at D`. |
| B4 | Report identity | For every partner and side at D: `Σ open of Increase entries − Σ open of Decrease entries == Σ Increase − Σ Decrease` (settlements cancel). So documents, aging and by-salesperson totals equal the balance report closing (`closing` = signed balance, P24). The tests assert it. |
| B5 | Days and buckets | `DaysOutstanding = max(0, D − DocDate)` (aging age; an opening debt counted before its own DocDate by P29 would otherwise be negative). `DaysOverdue = D − DueDate` when `DueDate < D`, else 0 (same as the 2A panel: `DueDate < today`). Bucket boundaries are ascending positive integers, 1–6 of them, sent as `buckets=30,60,90` (default). Bucket *i* holds `b[i-1] < days ≤ b[i]`; the first holds `days ≤ b[0]` and the last `days > b[last]`. Aging buckets start at 0 ("0–30"), overdue buckets at 1 ("1–30"). The DTO returns `{ from, to? }` per bucket and the client builds labels. |
| B6 | Unallocated (chưa đối trừ) | Open Decrease entries at D (prepayments, unmatched receipts, unmatched returns). Documents and aging subtract them (balance identity, B4). Overdue shows them per partner **for information only**, not subtracted, because the 2A panel's overdue amount is gross too. |
| B7 | By-salesperson columns | Per owner: `OpeningOpen` (open of the owner's Increase entries at `From − 1`), `Increase` (Σ Amount of the owner's Increase entries that count at `To` but not at `From − 1`), `Settled` (Σ settlements counting at `To` minus those counting at `From − 1`, on the owner's Increase entries; every origin — receipts, returns matched to the sale, offsets — so the UI label is "Đã thu / đối trừ"), `ClosingOpen` (open at `To`), `ClosingOverdue` (part of `ClosingOpen` with `DueDate < To`). Identity per row: `OpeningOpen + Increase − Settled == ClosingOpen`. A separate unallocated line has `Opening` and `Closing`; `OpeningBalance = Σ OpeningOpen − Unallocated.Opening`, `ClosingBalance = Σ ClosingOpen − Unallocated.Closing`. The brainstorm listed no opening column; it is added so each row reconciles. `Side` is a parameter (default Receivable) and `PartnerId` is an optional filter like on every other report (spec §11). Rows are ordered by owner name with the vi-VN comparer used by `StockVoucherService.ListOwnersAsync`, then `OwnerUserId`; the null-owner row is last. The labels depend on the side: Receivable "Đã thu / đối trừ" and "Chưa đối trừ (khách trả trước)", Payable "Đã trả / đối trừ" and "Chưa đối trừ (ứng trước NCC)". |
| B8 | Reconciliation sides | A side is listed when the partner has that role (`IsCustomer` → Receivable, `IsSupplier` → Payable) **or** has any entry of that side in the branch (a former customer keeps its history). Each side is the Sổ chi tiết of that side (`DebtLedgerReportDto`, same window rule). `NetBalance = Receivable.ClosingBalance − Payable.ClosingBalance` when both sides are listed, else null; positive = the partner owes us. The header carries partner code, name, tax code, company address and the branch name for the on-screen document. |
| B9 | Code layout | `DebtAging` in `Application/CashDebt/Common/` (pure, unit-tested in `CashDebt/Unit/`). `DebtPositionQuery` (internal static) and `DebtPositionReportService` in `Application/CashDebt/Reports/Services/`; DTOs in a new `Models/DebtPositionReportDtos.cs`; validators in a new `Validators/DebtPositionReportValidators.cs`; new controller `DebtPositionReportsController` on route `api/reports`. Reconciliation goes into `CashDebtReportService` / `CashDebtReportsController` because it reuses `BuildDebtLedgerAsync` inside the same snapshot. Every new report runs in `ITransactionRunner.RunSnapshotAsync`, like 2A. |
| B10 | Search | New ILike searches are not added (the reports filter by `partnerId`), so F-1 does not grow. |
| B11 | BA gate (cross-branch payment, bank accounts shared by branches) | The follow-up list and the 2A go-live gates ask the BA to settle these before 2B is planned. The answer is not on record, so this plan **proceeds on the assumption that it does not block 2B**: every 2B report reads the branch-scoped ledger and nothing in Phases 01–05 changes with either answer. Phase 06 rewrites the precondition line in the follow-up list as "không chặn 2B: báo cáo theo chi nhánh làm việc". **To confirm with the user before Phase 01.** |
| B12 | Empty results | Every report returns zero-filled arrays when there are no rows: `Totals.Amounts` (aging) and `TotalAmounts` (overdue) always have `Buckets.Count` entries, `Totals` of by-salesperson is an all-zero row. The pages show an empty state instead of an empty table. |

## Assumptions

- The brainstorm section-02 §11 rows 3–7 and the Round 2A plan (P1–P29) are the approved spec; B1–B12 are additions or refinements.
- Branch `feat/cash-debt-round2b` was created from `qlbanhang/main` (`af562f0`, PR #2 merged). Local `main` tracks `origin` and is behind; PRs go to remote `qlbanhang` (memory `git-remotes`).
- Same local setup as 2A: PostgreSQL on `localhost:5432` (`postgres`/`1`), `TEST_DB_CONNECTION` → `qldonhang_integtest` (never `qldonhang_test`). No migration in this plan.
- Volumes are small (tens of thousands of debt rows a year): the reports load the counted entries of one branch and side and aggregate in memory, like the 2A partner summary.
- Known baseline at the end of 2A (`6c0be04`): backend 19 failures, frontend 3 failures, lint 3 errors and 14 warnings. Phase 00 re-records it on this branch.

## Risks

- **"As at" correctness:** a settlement counted at the wrong date silently shifts open amounts. Mitigation: one shared loader (`DebtPositionQuery`) for all four position reports; hand-computed tests at two dates (10-03/10-04 and 10-31); identity tests against the balance report (B4) for every partner and side of the dataset.
- **Performance (F-8):** each report loads every counted entry of the branch and side (the index `(BranchId, PartnerId, Side, PostedAt)` serves it only through its `BranchId` prefix). Acceptable at current volume; revisit with an index or snapshot tables if a report takes more than about 1 s on production data.
- **Owner semantics (B1):** if the BA later says the creator is not the salesperson, the by-salesperson report is wrong until `SalespersonId` is added. Only what is written to `DebtEntry.OwnerUserId` changes then; the report code does not. Opening debts entered before this round have no owner and show as "Không xác định" until someone edits them and picks one.
- **Wrong reading of overdue vs net:** a partner with an unmatched prepayment still shows overdue documents. Mitigation: the unallocated column next to the overdue total (B6), and the documents report with both parts.
- **Baseline failures:** 19 backend / 3 frontend known failures; compare test by test with `BASELINE.md`.

## Phases

- [x] Phase 00 — Baseline commit and failure baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Aging calculator, position query & debt documents report (M) — `phase-01-position-query-and-documents.md`
- [x] Phase 02 — Aging & overdue reports (M) — `phase-02-aging-and-overdue.md`
- [x] Phase 03 — By-salesperson, reconciliation reports & opening-debt owner options (M) — `phase-03-salesperson-and-reconciliation.md`
- [x] Phase 04 — Frontend: documents, aging, overdue pages & panel link (L) — `phase-04-frontend-position-reports.md`
- [x] Phase 05 — Frontend: by-salesperson & reconciliation pages, opening-debt owner picker (M) — `phase-05-frontend-salesperson-reconciliation.md`
- [x] Phase 06 — Documentation & final verification (S) — `phase-06-docs-and-verification.md`

Phases run in order. Phase 01 freezes the shared loader and calculator; Phases 04–05 depend on the API contracts of Phases 01–03 and must not start before Phase 03 is complete.

## Executor conventions

- **Branch and commits:** work on `feat/cash-debt-round2b` (or the worktree the executing skill creates from it). Commit after every task with conventional commits, scope `debt` or `frontend` (e.g. `feat(debt): debt aging report`). End every commit message with the attribution line from the session's system reminder.
- **Staging:** stage files explicitly (`git add <paths>`, taken from the task's own file list — every file a task creates or modifies is staged). Never `git add -A` / `git add .`: `source/` (legacy VB.NET) is untracked on purpose and `.claude/tools/` holds a token.
- **Shell:** commands are written for Git Bash (the Bash tool). Windows PowerShell 5.1 has no `&&`. Keep the cwd on the uppercase `D:\` drive while running workflows or worktree agents (memory `workflow-worktree-drive-case`).
- **Test database** — set before any backend test command:
  - bash: `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
  - PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln` (if a running dev server locks `bin`, add `-o "$TEMP/qlbh-build"`).
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`.
  - Pure unit tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt.Unit"`.
  - Full suite: `dotnet test OrderMgmt.sln`.
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. Every frontend phase ends with `npm run build`; afterwards run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` (the build rewrites these tracked files; skip a path that is not tracked).
- **Test locations:**
  - Integration tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/`, `[Collection(nameof(PostgresCollection))]`, class inherits `CashDebtTestBase`, data from `ReportDataset.SeedAsync(this)` (Phase 09 of 2A; its table and hand-computed numbers are repeated in Phase 01 below).
  - Pure unit tests: `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Unit/` (no collection, no DB).
  - Frontend tests sit next to the file under test; follow `pages/cash-reports/debt-ledger-page.test.tsx`: it mocks the hooks (`vi.mock('@/features/cash-reports/hooks', …)` and `@/features/debt/hooks` for `useDebtPartnerSearch`), sets permissions with `useAuthStore.setState`, and renders with a router. `debt-balance-page.test.tsx` mocks neither the partner search nor permissions, so use it only for `renderAt` / `LocationDisplay`.
- **Rules carried over from 2A:**
  - DTOs use public `{ get; set; }` auto-properties (System.Text.Json ignores fields); lists default to `new()`.
  - Every `DateTimeOffset` sent to EF is UTC (`VnTime.ToUtc`); VN dates use `VnTime.StartOfDay` / `StartOfNextDay`.
  - Soft-deleted partners and users keep their history: read `Customers` / `Users` with `IgnoreQueryFilters()` when resolving names.
  - Validation `details` keys are camelCase (`at`, `buckets`, `partnerId`).
  - Report queries invalidate under the `['cash']` root (P28) — the new keys extend `cashReportKeys.all`.
  - Happy-path tasks must not implement the validation rules of a later task, or that task's RED step cannot fail.
  - **RED can be a build error.** A test that references a DTO or hook that does not exist yet fails to compile; that counts as RED ("FAIL — build error, `<Type>` does not exist"). A 404 RED is only available when the DTOs already exist.
  - **Nav test.** `components/layout/nav-config.test.ts` asserts the exact item list of the Tiền & Công nợ group. Final order: Số dư công nợ, Sổ chi tiết công nợ, Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu, Sổ quỹ. Every task that adds a nav item updates that test in its RED step and runs `npx vitest run src/components/layout`.
  - **Comparing with the baseline.** After a full backend run with `--logger "trx;LogFileName=current.trx"`, list failures with the command in Phase 00 Task 0.2 and `diff` them with the list in `BASELINE.md`; for the frontend compare the `FAIL` lines of `npm run test` and the lint counts.
- **Notation:** `10-05` = 2026-10-05.

## Final Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln

cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

Only the failures recorded in Phase 00 `BASELINE.md` may remain, compared test by test.

Manual smoke test (dev stack: `docker compose up -d`, backend `dotnet run --project src/OrderMgmt.WebApi`, frontend `npm run dev`, login `admin` / `Admin@123`), on data like the 2A smoke test (reset the database, then do the 2A smoke steps 1–5 only: KH1 with opening debt `HD-OLD-1` 5,000,000 due last week and its owner set to a user with the new picker, a sale of 3,000,000 paid 1,000,000, a receipt of 4,000,000 allocated to `HD-OLD-1`):

1. Tiền & Công nợ → **Công nợ theo chứng từ** (phải thu, today): KH1 lists `HD-OLD-1` remaining 1,000,000 with its overdue days and the sale remaining 2,000,000; balance 3,000,000 = Số dư công nợ closing.
2. **Tuổi nợ**: change the boundaries to `15,45` → three columns "0–15", "16–45", "> 45"; the total equals the documents report. An invalid input (`45,15`) shows an error and sends no request. Clicking KH1 opens the documents report for KH1.
3. **Nợ quá hạn** (today): `HD-OLD-1` 1,000,000 in its bucket; the sale is listed only once its due date has passed.
4. Phiếu xuất kho, new, KH1: the customer panel shows "Nợ quá hạn" in red and a link "Xem nợ quá hạn" opening the overdue report for KH1. The seeded SALES role has no stock-out permission, so check the hidden link with a custom role (Cài đặt → Phân quyền) that has `stock_out.view`, `stock_out.create` and `debt.partner_summary` but not `reports.debt`: the panel shows, the link does not.
5. **Công nợ theo nhân viên** (this month): one row per voucher creator; `HD-OLD-1` sits on the owner picked on the opening-debt form (add a second opening debt without owner to see the "Không xác định" row); each row satisfies opening + new − settled = closing; the unallocated line and the closing balance match Số dư công nợ.
6. Make a partner both customer and supplier, sell 1,000,000 and buy 400,000, both with paid amount 0 → **Biên bản đối chiếu**: two sections and "Đối tượng còn nợ 600,000".
7. Log in as WAREHOUSE: none of the five menu items; the URLs redirect to the 403 page.

## Rollback / Recovery

- **Code:** every task is a separate commit on `feat/cash-debt-round2b`; `git revert` the branch or single commits. No migration, no seed and no permission change, so the database needs nothing.
- **Exposure:** the new reports need `reports.debt`, already granted in 2A to ADMIN, MANAGER and ACCOUNTANT. They add no permission, so hiding them means not deploying the branch (or reverting the page commits of Phases 04–05; the nav item of each page is committed with its page).
