# Execution report: product quick add from the product code cell

- Plan: [SUMMARY.md](SUMMARY.md) (4 phases), plan review [REVIEW.md](REVIEW.md), brainstorm [261015-2033-product-quick-add](../../../brainstorms/261015-2033-product-quick-add/SUMMARY.md).
- Executed 2026-10-15 on branch `feat/product-quick-add` (worktree `.claude/worktrees/product-quick-add`), all phases back to back (user choice).
- Method: subagent-driven development. Each task had a sonnet implementer (opus for Task 3.4) with TDD and RED evidence, then one opus reviewer doing two stages: spec compliance first, then code quality. Fixes went back to the implementer and were re-reviewed. Phase 01 and the pure Phase 03 tasks (3.1, 3.3) ran in side worktrees in parallel and were merged in. A final whole-branch review closed the run.

## Tasks

| Task | Commit(s) | Spec | Quality | Notes |
|---|---|---|---|---|
| 1.1 Seed grant | 9fcecec3 (merged 331f17ee) | ✅ | ✅ | 6 `AdminRolesCrudTests` fail on `main` too (see follow-ups) |
| 2.1 `splitTypedProductText` | 314aa85a | ✅ | ✅ | |
| 2.2 `useSaveProduct` | d86ced3a | ✅ | ✅ | |
| 2.3 Extract `ProductFormFields` | 41a60d67 | ✅ | ✅ | Page behaviour unchanged (line-by-line parity check) |
| 2.4 Enter navigation | 096ab3cd | ✅ | ✅ | |
| 3.1 `useDialogOpening` to `lib` | b571195a (merged 57ba84fa) | ✅ | ✅ | |
| 3.2 `ProductQuickAddDialog` | 878a20c6 | ✅ | ✅ after fixes | Double save while a typed unit is created: be9f8da1, then 2b4ad133 |
| 3.3 `settledQuery` | b4739737 (merged 57ba84fa) | ✅ | ✅ | |
| 3.4 Quick add in the cell | 3aa4b4f2 | ✅ | ✅ after fixes | 1070da95 (waiting Enter ends on blur, Tab skips the add row, no pick on error), 061e674b (Tab clamp) |
| 4.1 Stock line grid | fb2c5143 | ✅ | ✅ | |
| 4.2 Quotation + opening stock | e9c1a8a8 | ✅ | ✅ | |
| 4.3 Help files and docs | e79b6ec2, 6b0afd3e, 3d45aa4c | ✅ after fixes | ✅ | `help:stale` clean |
| 4.4 Real-browser check | 44df4a8a, 221ab10f (fixes) | ✅ | ✅ | [BROWSER-CHECK.md](BROWSER-CHECK.md): D1–D3 found, fixed and re-checked |
| Final review polish | see `git log` | | | Comments and doc wording |

## Files changed

46 files, +3341/−562 against `main` (before the final polish commit). Main pieces:

- Backend: `DbSeeder.cs` (3 array entries), `Catalog/ProductQuickAddSeedTests.cs`.
- `frontend/src/features/products/`: `split-typed-text.ts`, `use-save-product.ts`, `keys.ts` (`searches()`), `hooks.ts` (`settledQuery`), `components/product-form-fields.tsx`, `components/product-quick-add-dialog.tsx`, with tests.
- `frontend/src/lib/use-dialog-opening.ts` (moved from the partner field).
- `frontend/src/pages/products/product-form-page.tsx` (now only the page shell).
- `frontend/src/pages/quotations/components/product-typeahead-cell.tsx` (`allowQuickAdd`), `line-items-grid.tsx`, `stock-vouchers/components/stock-line-grid.tsx`, `inventory/opening-stock-page.tsx`, with tests.
- Help: `stock-in.form`, `stock-out.form`, `quotations.form`, `inventory-opening-stock`; `sources` only in `payments.form`, `receipts.form`, `quotations.list`.
- Docs: `system-architecture.md`, `conventions.md`, `directory-structure.md`, `docs/SUMMARY.md`, [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md).

## Verification

- Frontend at 3d45aa4c: typecheck clean. Tests: 2962 of 2965 pass; the 3 failures are the known baseline (bank-accounts-tab, useNotificationHub, payment-qr-page). Lint: 2 errors and 12 warnings, none from this branch (`table.tsx`, `product-catalog-list.tsx` and `line-items-grid.tsx:61` are the same on `main`). `npm run help:stale` prints `Không có cảnh báo.`
- Backend: `dotnet build` gives 0 errors and 9 older nullable warnings in other test files. Filtered tests (`QuickAddSeedTests`, `DbSeederUpgradeTests`, `AdminRolesCrudTests`) give 27 passed and 6 failed. The 6 failures are `AdminRolesCrudTests`, and the reviewer reproduced them on `main` 5ed58abc.
- Real browser (Edge headless, scratch databases, nothing sent to `:5050`): every scenario passes as Admin and as a Warehouse user, with and without `products.create`, after the fixes.

## Deviations from the plan

- **Double-submit guard (not in the plan).** A held Ctrl+Enter, or a second submit while the unit was being created, created the product 2–3 times. The fix has three layers: a `busy` guard in `ProductFormFields` (covers the page and the dialog), `saving` in `useSaveProduct` (the button state covers the unit step), and `savedRef` in the dialog (no save while it closes). Held Enter and Ctrl+Enter repeats are ignored in the dialog. The first design (sharing the in-flight promise) was rejected in review because the page would show 2 toasts and navigate twice.
- **IME.** A composing Enter is ignored in the product cell and in the Enter handlers of the quotation and stock line grids. This also changes the stock card and cost recalculation cells, where a composing Enter no longer picks a row. That breaks the plan's "canAdd false = exactly as today" invariant, but as a bug fix.
- **Tab.** Tab only walks the results and never lands on the add row. With only the add row showing, Tab leaves the cell as before.
- **Dialog focus while the lookups load.** The focus goes to the dialog itself, so keys no longer reach the cell behind it. A failed save scrolls the error box into view.
- **`product-form-page.test.tsx`.** It gained one case (one create, one toast and one navigate on a double submit), and its router mock now shares `navigateMock`. No existing case changed.
- **Help `sources`.** `use-dialog-opening.ts` was added to the 5 form help files and `inventory-opening-stock`, and the `quotations.list` glob was narrowed to the files the list page imports. This is how `help:stale` was cleared.
- **The quotations help has an extra line.** With `products.create`, Enter on an unknown code opens the quick add, so a free-text line takes `Esc`, then Enter, to reach **Tên hàng** (as the brainstorm designed).

## Residual risks and follow-ups

- **Go-live:** on existing databases an admin ticks `products.create` for the three roles ([GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)). With it, these roles can also create products on `/products/new` and create product groups (D5, accepted).
- **User Guide Rule:** `/products/new` and `/products/:id` stay in `HELP_PENDING`. The user decided (2026-10-15) that the page change is a refactor users cannot see plus a seed-only permission change; the PR says so.
- **Habit change on quotations:** free-text lines with an unknown code take `Esc` before Enter for users with `products.create`.
- **Out of scope, found during the run:** `AdminRolesController.Create` never calls `CreateRoleRequestValidator`, so 6 `AdminRolesCrudTests` fail on `main` (code format and reserved codes return 200 or 409 instead of 400). The update validators are probably not called either.
- **Minor, not fixed:**
  - A partly typed unit ("Tấ") followed by Enter keeps the free text, so saving creates a new unit. The product page does the same.
  - Enter in **Ghi chú** saves, so the dialog's note cannot hold a line break.
  - `quotations.list` `sources` lack `column-sizing-defaults.ts`.
  - The redundant `!submitting` check in `ProductFormFields` and the unused `save.reset()` in the dialog.
