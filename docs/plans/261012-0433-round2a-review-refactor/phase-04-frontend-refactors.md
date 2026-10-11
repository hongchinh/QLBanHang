# Phase 04 — Frontend refactors

**Status:** [ ] pending
**Complexity:** L

## Objective

Remove the frontend duplication and layering issues the review found, with no visible change (decision B6), plus one convention fix (fund transfer mutations also invalidate `['inventory']`).

## Files

- New: `frontend/src/lib/date-only.ts` (+ test), `frontend/src/lib/to-number.ts` (+ test), `frontend/src/lib/money-input.ts` (moved, + its test), `frontend/src/components/money-cell/money-cell.tsx`, `frontend/src/components/activity-history/activity-history.tsx` (+ test), `frontend/src/components/search-combobox/search-combobox.tsx` (+ test), `frontend/src/hooks/useDocumentActions.ts` (+ test), `frontend/src/features/credit-limits/keys.ts`, `features/opening-debts/keys.ts`, `features/cash-settings/keys.ts`
- Modified (by task): listed in each task.

Commands (from `frontend/`): `npx vitest run <paths>`, `npm run typecheck`, `npx eslint --ext .ts,.tsx <files>`. After any `npm run build`, run `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`.

## Tasks

### Task 4.1 — Date and number helpers

1. Write failing tests `src/lib/date-only.test.ts` and `src/lib/to-number.test.ts`:
   - `formatDateOnly('2026-10-05')` → `'05/10/2026'`; `formatDateOnly('2026-10-05T00:00:00')` → `'05/10/2026'`; `formatDateOnly(null)`, `formatDateOnly(undefined)`, `formatDateOnly('')` → `''`.
   - `toNumberOrZero('12.5')` → `12.5`, `toNumberOrZero('x')` → `0`, `toNumberOrZero(undefined)` → `0`; `toOptionalNumber('')`, `toOptionalNumber(null)`, `toOptionalNumber(undefined)`, `toOptionalNumber('x')` → `undefined`, `toOptionalNumber(3)` → `3`.
2. Run `npx vitest run src/lib/date-only.test.ts src/lib/to-number.test.ts` — expected: FAIL (modules not found).
3. Implement:
   - `lib/date-only.ts`: `export function formatDateOnly(value?: string | null): string { if (!value) return ''; const [y, m, d] = value.slice(0, 10).split('-'); return \`${d}/${m}/${y}\`; }` (yyyy-MM-dd → dd/MM/yyyy without `Date`, so no time-zone shift).
   - `lib/to-number.ts`: `toNumberOrZero(v: unknown): number` (the `number` copies) and `toOptionalNumber(v: unknown): number | undefined` (the `number | undefined` copies), same bodies as today.
   - Replace the private copies and import the helpers:
     - `formatDateOnly`: `pages/cash-vouchers/cash-voucher-form-page.tsx`, `pages/cash-vouchers/components/allocation-grid.tsx`, `pages/debt/components/open-item-grid.tsx`, `pages/debt/debt-offset-form-page.tsx`, `pages/debt/opening-debt-page.tsx`, `pages/inventory/opening-stock-page.tsx`, `pages/settings/branches-page.tsx`;
     - `formatYmd` (same behaviour): `pages/cash-reports/document-link.ts` (keep the export as `export { formatDateOnly as formatYmd }` only if other files import it; otherwise replace the importers) and `pages/debt/components/settlement-list.tsx`;
     - `toNumberOrZero`: `pages/cash-vouchers/cash-voucher-form-page.tsx`, `pages/cash-vouchers/components/allocation-grid.tsx`, `pages/cash-vouchers/components/cash-line-grid.tsx`;
     - `toOptionalNumber`: `pages/stock-vouchers/components/stock-line-grid.tsx`, `pages/stock-vouchers/stock-voucher-form-page.tsx`, `pages/quotations/quotation-form-page.tsx`, `pages/quotations/components/line-items-grid.tsx`.
4. Run the new tests and `npx vitest run src/pages/cash-vouchers src/pages/debt src/pages/cash-reports src/pages/inventory src/pages/settings src/pages/stock-vouchers src/pages/quotations`, `npm run typecheck` — expected: PASS.
5. Commit `refactor(frontend): share the date-only and number helpers`.

### Task 4.2 — Money input and money cell out of `pages/`

1. Write the failing test: move `src/pages/quotations/utils/money-input.test.ts` to `src/lib/money-input.test.ts` with its import changed to `./money-input`; add `src/components/money-cell/money-cell.test.tsx` rendering `MoneyCell` (label, committing `1.234.567` calls `onCommit(1234567)`) and `CellError` (renders the message with the given id).
2. Run `npx vitest run src/lib/money-input.test.ts src/components/money-cell` — expected: FAIL (modules not found).
3. `git mv src/pages/quotations/utils/money-input.ts src/lib/money-input.ts`; update every importer (`grep -rln "utils/money-input" src`, 18 files today, including `features/fund-transfers/*`, `features/opening-debts/schema.ts`, `features/products/components/*`). Move `MoneyCell`, `MoneyCellProps` and `CellError` from `pages/cash-vouchers/components/cash-line-grid.tsx` to `components/money-cell/money-cell.tsx` and update importers (`cash-line-grid.tsx`, `allocation-grid.tsx`, `pages/debt/components/open-item-grid.tsx`, `pages/quotations/components/line-items-grid.tsx`, `pages/stock-vouchers/components/stock-line-grid.tsx` — check each with `grep -rn "MoneyCell\|CellError" src`).
4. Run the new tests, `npx vitest run src/pages src/features`, `npm run typecheck` — expected: PASS.
5. Commit `refactor(frontend): move money input and money cell out of pages`.

### Task 4.3 — One activity history component

1. Write the failing test `src/components/activity-history/activity-history.test.tsx`: renders the loading, error (with "Thử lại" calling `onRetry`), empty ("Chưa có lịch sử.") and list states; a `SettlementRemoved` row shows "Bỏ đối trừ", an `AutoVoucherChanged` row shows "Phiếu thu/chi tự động".
2. Run `npx vitest run src/components/activity-history` — expected: FAIL (module not found).
3. Create `components/activity-history/activity-history.tsx` exporting `ActivityHistory` and the type `DocumentActivity { id: string; action: DocumentActivityAction; occurredAt: string; description: string; actorName?: string | null }` with `DocumentActivityAction = StockVoucherActivityAction` (the stock union is a superset of the cash one). Move the body, labels and icons of `pages/stock-vouchers/components/stock-voucher-activity-history.tsx` into it, delete that file and its test (move the test cases into the new test if they cover more), and use `ActivityHistory` in `stock-voucher-form-page.tsx`, `cash-voucher-form-page.tsx`, `fund-transfer-form-page.tsx` and `debt-offset-form-page.tsx` without casts.
4. Run the new test and `npx vitest run src/pages/stock-vouchers src/pages/cash-vouchers src/pages/fund-transfers src/pages/debt`, `npm run typecheck` — expected: PASS.
5. Commit `refactor(frontend): one activity history component for stock and cash documents`.

### Task 4.4 — Key factories and the fund transfer invalidation

1. Write failing tests:
   - `src/features/fund-transfers/hooks.test.ts(x)` (create it, following `features/debt/hooks.test.tsx`): after a successful create/update/cancel/restore/delete mutation, `invalidateQueries` is called with `{ queryKey: cashKeys.all }` **and** `{ queryKey: inventoryKeys.all }` (conventions: cash mutations invalidate `['cash']` and `['inventory']`).
   - New `src/features/credit-limits/keys.test.ts`, `src/features/opening-debts/keys.test.ts`, `src/features/cash-settings/keys.test.ts`: import the factory from `./keys` and assert the keys it builds today (read them in each `hooks.ts`, e.g. `cashSettingsKeys.all` equals `['cash-settings']`). These features have no hook tests yet.
2. Run `npx vitest run src/features/fund-transfers src/features/credit-limits src/features/opening-debts src/features/cash-settings` — expected: FAIL (missing invalidation; `keys` modules not found).
3. Move each key factory from its `hooks.ts` into a new `keys.ts` in the same feature (re-export nothing from `hooks.ts`; update importers via `grep -rn "creditLimitKeys\|openingDebtKeys\|cashSettingsKeys" src`). In `features/fund-transfers/hooks.ts` (≈line 47) also invalidate `inventoryKeys.all` (`@/features/stock-vouchers/keys`).
4. Run the same tests and `npm run typecheck` — expected: PASS.
5. Commit `refactor(frontend): key factories in keys.ts; fund transfer mutations refresh inventory`.

### Task 4.5 — One search combobox for the partner pickers

1. Write the failing test `src/components/search-combobox/search-combobox.test.tsx` for a generic `SearchCombobox<T>`:
   - props: `id`, `ariaLabel`, `placeholder`, `selectedLabel` (shown while not typing), `useSearch(keyword: string) => { data?: T[]; isLoading: boolean; isError: boolean }` (called with the 250 ms debounced keyword), `getKey(item)`, `renderItem(item)`, `onSelect(item)`, optional `onClear`, optional `invalid`;
   - behaviour (copied from `pages/debt/components/debt-partner-picker.tsx`): opens on focus with the empty-keyword results, ArrowDown/ArrowUp move the highlighted option, Enter selects, Escape closes and restores `selectedLabel`, the input has `role="combobox"`, `aria-expanded`, `aria-controls` and `aria-activedescendant` of the highlighted option, the clear button calls `onClear`.
2. Run `npx vitest run src/components/search-combobox` — expected: FAIL (module not found).
3. Implement `components/search-combobox/search-combobox.tsx` from `DebtPartnerPicker`'s code, then rewrite as thin wrappers over it (same exported names and props, so callers do not change): `DebtPartnerPicker` (`pages/debt/components/debt-partner-picker.tsx`, `useSearch = k => useDebtPartnerSearch(side, k, { bothRoles, includeInactive: true })`), `PartnerPicker` in `pages/cash-reports/debt-ledger-page.tsx`, `CashPartnerFilter` (`pages/cash-vouchers/components/cash-partner-filter.tsx`, `useCashPartnerSearch`), `CustomerPicker` in `pages/credit-limits/credit-limit-list-page.tsx`.
4. Run the new test and `npx vitest run src/pages/debt src/pages/cash-reports src/pages/cash-vouchers src/pages/credit-limits`, `npm run typecheck` — expected: PASS (the existing page tests are the regression net for each picker).
5. Commit `refactor(frontend): one search combobox behind the four partner pickers`.

### Task 4.6 — `useDocumentActions` for the four document forms

Run after Phase 03 Task 3.4 (it uses the token-aware `withConfirmed`).

1. Write the failing test `src/hooks/useDocumentActions.test.tsx` (renderHook + QueryClientProvider not needed). API:
   ```ts
   type DocumentAction = { kind: string; flags: ConfirmFlags };
   function useDocumentActions<A extends DocumentAction>(options: {
     perform: (action: A, flags: ConfirmFlags) => Promise<void>;   // mutations, navigation, success toasts
     reload: () => Promise<unknown | undefined>;                      // refetch; undefined when it failed
     onReloaded: (fresh: unknown) => void;                            // resetTo(fresh)
     applyValidationErrors?: (details: Record<string, string[]>, action: A) => boolean;
     errorTitles: Record<A['kind'], string>;
     confirmLabels: Record<A['kind'], string>;
     concurrencyMessage: string;
     concurrencyReloadFailedMessage: string;
   }): {
     busy: boolean;
     run: (action: A) => Promise<void>;                               // starts with action.flags
     confirmDialogProps: { request: ConfirmationRequest | null; confirmLabel?: string; onConfirm: () => void; onClose: () => void };
   };
   ```
   Cases: success calls `perform` once and toggles `busy`; a 422 `NEGATIVE_FUND_WARNING` sets `confirmDialogProps.request`, `onConfirm` calls `perform` again with `{ acknowledgeNegativeFund: true }` added to the earlier flags; a `_BLOCKED` 422 shows the dialog with `blocked` and `onConfirm` does not resend; 409 `CONCURRENCY` calls `reload` then `onReloaded` and toasts `concurrencyMessage` (or `concurrencyReloadFailedMessage` and no `onReloaded` when `reload` resolves undefined); `VALIDATION` with `applyValidationErrors` returning true toasts `errorTitles[kind]` with the server message; any other error toasts `errorTitles[kind]` with `formatApiErrorDetails`.
2. Run `npx vitest run src/hooks/useDocumentActions.test.tsx` — expected: FAIL (module not found).
3. Implement `hooks/useDocumentActions.ts` from the `runAction` / `handleError` / confirmation state now copied in `pages/cash-vouchers/cash-voucher-form-page.tsx` (≈lines 181, 288–353, 654–664). Then migrate, one form per step, keeping each form's `PendingAction` type, `perform` body, `applyValidationErrors` and its own `CONFIRM_LABELS` / `ERROR_TITLES` / concurrency texts:
   - `pages/cash-vouchers/cash-voucher-form-page.tsx`
   - `pages/fund-transfers/fund-transfer-form-page.tsx`
   - `pages/stock-vouchers/stock-voucher-form-page.tsx`
   - `pages/debt/debt-offset-form-page.tsx`
   After each form, run its page tests.
4. Run the hook test and `npx vitest run src/pages/cash-vouchers src/pages/fund-transfers src/pages/stock-vouchers src/pages/debt`, `npm run typecheck`, eslint on the touched files — expected: PASS.
5. Commit `refactor(frontend): share the document action flow of the four document forms`.

## Verification

- `npx vitest run` — only the 3 baseline failures.
- `npm run typecheck` — clean; `npm run lint` — the 3 baseline errors only; `npm run build` — passes (restore the tsbuildinfo files afterwards).

## Exit Criteria

- No copy of `formatDateOnly` / `formatYmd` / `toNum`, `MoneyCell`, the activity history or the partner combobox logic outside its shared module.
- The four document forms keep only their own mutations, field mapping and texts.
