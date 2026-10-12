# Phase 05 — Frontend: by-salesperson & reconciliation pages

**Status:** [x] done
**Complexity:** M

## Objective

Ship the last two report screens:

- **Công nợ theo nhân viên** (`/reports/debt-by-salesperson`): one row per owner, the "Không xác định" row, the unallocated line and the balances (B7);
- **Biên bản đối chiếu công nợ** (`/reports/debt-reconciliation`): one partner, one ledger section per side, the net amount (B8). On-screen only; the print/Excel template is Round 3;
- the **owner picker** ("Nhân viên") on the opening-debt form and a "Nhân viên" column on the opening-debt list, so the by-salesperson report has owners for opening debts (B1).

## Files

- `frontend/src/features/cash-reports/{types,api,keys,hooks}.ts` (modify)
- `frontend/src/pages/cash-reports/debt-by-salesperson-page.tsx` (+ `.test.tsx`) (new)
- `frontend/src/pages/cash-reports/debt-reconciliation-page.tsx` (+ `.test.tsx`) (new)
- `frontend/src/App.tsx`, `frontend/src/components/layout/nav-config.ts` (+ `nav-config.test.ts`) (modify)
- `frontend/src/pages/cash-reports/debt-ledger-table.tsx` (new), `frontend/src/pages/cash-reports/debt-ledger-page.tsx` (modify — use the table)
- `frontend/src/features/opening-debts/{types,api,keys,hooks}.ts` and `api.test.ts` / `keys.test.ts` (modify — owner options), `frontend/src/features/opening-debts/schema.ts` (modify only if the owner needs a form field)
- `frontend/src/pages/debt/opening-debt-form-dialog.tsx`, `frontend/src/pages/debt/opening-debt-page.tsx`, `frontend/src/pages/debt/opening-debt-page.test.tsx` (modify)

## Reference files (read-only)

- `frontend/src/pages/cash-reports/debt-ledger-page.tsx` — ledger table (opening row, rows, Cộng phát sinh, closing), `formatDebitCredit`, `formatSignedBalance`, `DocumentCodeLink`
- `frontend/src/pages/cash-reports/report-partner-picker.tsx` (Phase 04) — `side` undefined searches every partner
- `frontend/src/pages/cash-reports/debt-ledger-page.tsx` + test — range guard (`rangeError`), mocking of hooks, partner search and permissions
- Phase 03 of this plan — the API contracts

## Contracts used

```ts
// features/cash-reports/types.ts (add; mirror DebtPositionReportDtos.cs and CashDebtReportDtos.cs)

// GET /api/reports/debt-by-salesperson (reports.debt)
export interface DebtBySalespersonParams { side: DebtSide; from: string; to: string; partnerId?: string }
export interface DebtBySalespersonRow { ownerUserId?: string | null; ownerName?: string | null;
  openingOpen: number; increase: number; settled: number; closingOpen: number; closingOverdue: number }
export interface DebtUnallocatedLine { opening: number; closing: number }
export interface DebtBySalespersonReport { side: DebtSide; from: string; to: string; rows: DebtBySalespersonRow[];
  totals: DebtBySalespersonRow; unallocated: DebtUnallocatedLine; openingBalance: number; closingBalance: number }

// GET /api/reports/debt-reconciliation (reports.debt)
export interface DebtReconciliationParams { partnerId: string; from: string; to: string }
export interface DebtReconciliationReport { partnerId: string; partnerCode: string; partnerName: string;
  taxCode?: string | null; address?: string | null; branchName: string; from: string; to: string;
  sides: DebtLedgerReport[];          // Receivable first, then Payable
  netBalance?: number | null }        // > 0: the partner owes us
```

## Tasks

### Task 5.1 — API, keys and hooks

1. **Write the failing test** `features/cash-reports/hooks.test.tsx`, following `features/debt/hooks.test.tsx` (mock `./api`, render hooks inside a `QueryClientProvider`):
   - `useDebtReconciliation` does not call the API while `partnerId` is empty;
   - it calls `cashReportsApi.debtReconciliation` with `{ partnerId, from, to }` once `partnerId` is set;
   - `useDebtBySalesperson` calls `cashReportsApi.debtBySalesperson` with its params and not when `enabled: false`.
2. **Run the test to verify it fails:** `npx vitest run src/features/cash-reports`. Expected: FAIL — `useDebtReconciliation` is not exported.
3. **Write the minimal implementation:** the types above; `cashReportsApi.debtBySalesperson` / `debtReconciliation`; keys under `cashReportKeys.all`; hooks `useDebtBySalesperson(params, { enabled })` and `useDebtReconciliation(params, { enabled })` (enabled only with a `partnerId`, like `useDebtLedger`).
4. **Run tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): salesperson and reconciliation report hooks"`

### Task 5.2 — Công nợ theo nhân viên page

Page `DebtBySalespersonPage`, route `/reports/debt-by-salesperson`. Filters in the URL: Phía (default Receivable), Từ ngày (default `firstDayOfMonthYmd()`), Đến ngày (default `todayYmd()`), Đối tượng (optional `partnerId`, `ReportPartnerPicker` with `side` and `onClear`; switching Phía removes `partnerId`); `from > to` → "Từ ngày không được sau Đến ngày", no request. Empty state when `rows` is empty: "Không có công nợ trong kỳ này" (the cộng / chưa đối trừ / số dư rows are still shown only when there are rows). Table columns: Nhân viên (`ownerName ?? 'Không xác định'`), Còn nợ đầu kỳ, Phát sinh nợ, Đã thu / đối trừ (Payable: "Đã trả / đối trừ"), Còn nợ cuối kỳ, Trong đó quá hạn (red when > 0). Rows, then a **Cộng** row from `totals`, then a row **Chưa đối trừ** (label by side: Receivable "Chưa đối trừ (khách trả trước)", Payable "Chưa đối trừ (ứng trước NCC)") showing `−unallocated.opening` and `−unallocated.closing` in the opening/closing columns, then **Số dư công nợ** with `formatSignedBalance(side, openingBalance)` / `formatSignedBalance(side, closingBalance)`. A note under the table: "Theo người lập chứng từ."

1. **Write the failing test** `debt-by-salesperson-page.test.tsx`:
   - defaults → the hook receives `{ side: 'Receivable', from: <first of month>, to: <today> }`;
   - rows render, a null owner shows "Không xác định", overdue > 0 has the destructive class;
   - the Cộng, Chưa đối trừ and Số dư công nợ rows show the mocked numbers (`formatSignedBalance` for the balances);
   - `from > to` → the error text and the hook is called with `enabled: false`;
   - `?side=Payable` → the labels "Đã trả / đối trừ" and "Chưa đối trừ (ứng trước NCC)" (and not the receivable wording);
   - `?partnerId=p1` is passed to the hook; its clear entry removes it from the URL;
   - empty `rows` → the empty message;
   - nav: add the item to the `nav-config.test.ts` list (RED step) after "Nợ quá hạn".
2. **Run the test to verify it fails:** `npx vitest run src/pages/cash-reports/debt-by-salesperson-page.test.tsx src/components/layout`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** the page, the route (`ProtectedRoute permission="reports.debt"`), nav item `{ to: '/reports/debt-by-salesperson', label: 'Công nợ theo nhân viên', icon: UserRound, permission: 'reports.debt' }` (the route-permission pattern and its test were extended in Phase 04 Task 4.3).
4. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports src/components/layout`. Expected: PASS.
5. **Commit:** `git add` the page, its test, `types.ts`/`api.ts`/`keys.ts`/`hooks.ts` if touched, `App.tsx`, `nav-config.ts` and its test, then `git commit -m "feat(frontend): debt by salesperson report page"`

### Task 5.3 — Biên bản đối chiếu page

Page `DebtReconciliationPage`, route `/reports/debt-reconciliation`. Filters in the URL: Đối tượng (`ReportPartnerPicker` with `side` undefined, `id="debt-reconciliation-partner"`), Từ ngày (default `firstDayOfMonthYmd()`), Đến ngày (default `todayYmd()`); `from > to` → "Từ ngày không được sau Đến ngày" and the hook is called with `enabled: false` (same `rangeError` handling as the ledger page). Without a partner: "Chọn đối tượng để xem biên bản đối chiếu". Document header (on screen): "BIÊN BẢN ĐỐI CHIẾU CÔNG NỢ", branch name, "Từ ngày … đến ngày …" (`formatDateOnly`), partner code – name, MST, address. One section per side titled "Phải thu" / "Phải trả" (`DEBT_SIDE_LABELS`), each with the ledger table of `debt-ledger-page.tsx` (opening row, rows with `DocumentCodeLink`, Cộng phát sinh, closing). Extract that table into `pages/cash-reports/debt-ledger-table.tsx` (`DebtLedgerTable({ ledger }: { ledger: DebtLedgerReport })`) and use it in both pages. When `netBalance` is not null: "Bù trừ hai bên: {Đối tượng còn nợ | Công ty còn nợ} {formatCurrencyVnd(|net|)}" (net > 0 → "Đối tượng còn nợ", < 0 → "Công ty còn nợ", 0 → "Hai bên không còn nợ"). A muted note: "In biên bản: Đợt 3."

1. **Write the failing test** `debt-reconciliation-page.test.tsx`:
   - no `partnerId` → the prompt, the hook is not enabled;
   - `?partnerId=p1&from=2026-10-01&to=2026-10-31` with one side → header texts, one "Phải thu" section with the ledger rows, no net line;
   - `from=2026-10-31&to=2026-10-01` → the range error and `enabled: false`;
   - two sides with `netBalance: 600000` → both sections and "Đối tượng còn nợ" with the amount; `netBalance: -50000` → "Công ty còn nợ";
   - `debt-ledger-page.test.tsx` still passes after the table extraction;
   - nav: add the item to the `nav-config.test.ts` list (RED step) after "Công nợ theo nhân viên", before "Sổ quỹ".
2. **Run the test to verify it fails:** `npx vitest run src/pages/cash-reports/debt-reconciliation-page.test.tsx src/components/layout`. Expected: FAIL — module not found.
3. **Write the minimal implementation:** `debt-ledger-table.tsx` (move the `<table>` out of the ledger page unchanged), the page, the route, nav item `{ to: '/reports/debt-reconciliation', label: 'Biên bản đối chiếu', icon: FileCheck, permission: 'reports.debt' }`.
4. **Run tests to verify they pass:** `npx vitest run src/pages/cash-reports src/components/layout`. Expected: PASS.
5. **Commit:** `git add` the page, its test, `debt-ledger-table.tsx`, `debt-ledger-page.tsx`, `App.tsx`, `nav-config.ts` and its test, then `git commit -m "feat(frontend): on-screen debt reconciliation page"`

### Task 5.4 — Owner picker on the opening-debt form and list (B1)

Backend: `GET /api/opening-debts/owners` (Phase 03 Task 3.4) returns `{ id, fullName }[]`.

1. **Write the failing tests** in `pages/debt/opening-debt-page.test.tsx` (mock the new `useOpeningDebtOwners` hook next to the existing mocks):
   - the list shows a "Nhân viên" column with `ownerName`, and "—" when there is none;
   - the form dialog shows a "Nhân viên" select with the owners and a first option "Không chọn"; picking one and saving sends `ownerUserId` = the picked id in the create request;
   - editing a row that has an owner pre-selects it; choosing "Không chọn" sends `ownerUserId: null`; leaving it unchanged sends the loaded id (the existing assertion at the line that expects the loaded owner keeps passing);
   - an owner that is no longer in the options (deactivated user) is still shown, as an extra option built from `initial.ownerName`, and kept when unchanged;
   - `features/opening-debts/api.test.ts`: `openingDebtsApi.owners()` GETs `/opening-debts/owners`.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/debt/opening-debt-page.test.tsx src/features/opening-debts`. Expected: FAIL — `useOpeningDebtOwners` is not exported / the column is missing.
3. **Write the minimal implementation:** type `OpeningDebtOwner { id: string; fullName: string }`; `openingDebtsApi.owners`; a key `openingDebtKeys.owners()`; hook `useOpeningDebtOwners()` (`staleTime` of a few minutes); in `opening-debt-form-dialog.tsx` replace the 2A comment ("The owner field is not shown in 2A") by a controlled select bound to `ownerUserId` state (initial = `initial?.ownerUserId ?? null`, reset with the form) and pass it in `toUpsertOpeningDebtRequest(values, { ownerUserId, … })` for create and edit; add the column to `opening-debt-page.tsx`. Reuse the select component the form already uses for Phía/Loại.
4. **Run tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git add` the files listed under Files for this task, then `git commit -m "feat(frontend): owner picker on opening debts"`.

### Task 5.5 — Frontend checkpoint

1. Run `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build` from `frontend/`.
2. Compare test failures and lint counts with `BASELINE.md`; fix any new failure with a failing test first.
3. Run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` after the build, and confirm `git status --short` is clean.
4. Commit only if a fix was needed: `git commit -m "fix(frontend): <what>"`.

## Verification

```bash
cd frontend
npx vitest run src/features/cash-reports src/features/opening-debts src/pages/cash-reports src/pages/debt src/components/layout
npm run typecheck
npm run lint
npm run test
npm run build
```

## Exit Criteria

- Both pages render the Phase 03 contracts, keep their filters in the URL and require `reports.debt`.
- The ledger table is shared by the ledger and reconciliation pages; the ledger page tests still pass.
- An opening debt can be given an owner on the form; the list shows it; the by-salesperson report groups by it.
- No new frontend failures or lint errors against `BASELINE.md`; `npm run build` passes.
