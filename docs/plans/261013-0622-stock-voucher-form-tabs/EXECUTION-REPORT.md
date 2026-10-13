# Execution Report: Stock voucher form tabs

Plan: `docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` (phases 01-05). Branch `feat/stock-voucher-form-tabs`, base `524a7f9`.

## Phases and commits (`git log --oneline 524a7f9..HEAD`)

Plan setup
- ddb2e71 docs: apply review fixes to the stock voucher form plan
- 74bce67 chore(plan): record stock voucher form baseline

Phase 01 (backend PayableBalance, permission label)
- bf684cf feat(backend): payable balance in the partner debt summary
- e1154d8 feat(backend): partner debt permission label covers stock-in
- 4255c83 chore(plan): phase 01 complete

Phase 02 (pure logic)
- 2c4fd7f feat(frontend): per-pricing-mode enter path helpers
- 1a5c5d0 feat(frontend): per-user stock grid preferences
- 0858351 feat(frontend): partner debt after the voucher
- fe1de63 feat(frontend): stock voucher field to tab mapping
- e02b997 chore(plan): phase 02 complete

Phase 03 (form layout T2)
- cfdc067 feat(frontend): stock voucher payment tab
- a306a88 feat(frontend): stock voucher totals bar
- c49028c feat(frontend): partner debt strip with after-voucher balances
- c7ade63 feat(frontend): stock voucher tabs shell
- 7237987 feat(frontend): stock voucher main fields with Enter navigation
- fc15aae feat(frontend): stock voucher form T2 layout with tabs, totals bar and debt strip
- edd2e85 test(frontend): the form passes the saved voucher impact to the debt strip (extra, added after verifier mutation checks)
- 9408845 chore(plan): phase 03 complete

Phase 04 (grid)
- f13273a feat(frontend): stock grid column definitions with optional columns
- 77a8e5e feat(frontend): sticky stock grid columns and full-height scroll
- 7c76278 feat(frontend): stock grid column and enter-stop settings
- 7ed3ad3 feat(frontend): stock grid enter path follows the pricing mode
- a83f4f5 feat(frontend): stock grid arrow keys and ctrl+delete
- b21a245 test(frontend): cover ctrl+delete edges and the typeahead open-on-focus default (extra, after verifier mutation checks)
- f11ac8e chore(plan): phase 04 complete

Phase 05 (docs, verification)
- 8f1f22c docs: stock voucher form tabs, grid keyboard and payable balance
- the commit containing this report: chore(plan): stock voucher form redesign complete

## Files changed

`git diff --stat 524a7f9..HEAD` (before this report): 45 files, 3330 insertions, 937 deletions. Mostly `frontend/src/pages/stock-vouchers/` (components, utils, tests), plus `frontend/src/features/debt`, `frontend/src/pages/quotations` (optional `openOnFocus` prop), `frontend/src/styles/form-inline.css`, the backend partner summary and seed, and docs (`docs/SUMMARY.md`, `docs/architecture`, `docs/codebase`, `docs/code-standard`).

## Verification against BASELINE.md

| Command | Result |
| --- | --- |
| dotnet test (PartnerSummaryTests, CashDebtSeedTests; integtest DB) | 6 passed, 0 failed |
| vitest `src/pages/stock-vouchers` | 14 files, 146 tests passed |
| `npm run test` | 3 failed, 936 passed (939). FAIL lines identical to the 3 in BASELINE.md: bank-accounts-tab "adds a new account via the form", useNotificationHub "invalidates unread-count query on NewNotification", payment-qr-page "generates and displays a QR after submitting valid data" |
| `npm run typecheck` | exit 0 |
| `npm run lint` | 17 problems (3 errors, 14 warnings), equal to baseline |
| `npm run build` | exit 0; tsbuildinfo files restored with git checkout |

Earlier in the session the verifier also compared all 20 columns before and after the grid refactor (values, handlers, lock rules, ids unchanged) and ran mutation checks; the survivors were closed by edd2e85 and b21a245.

## Task 5.3 manual checklist

How it ran: a real Chrome (headless, driven by playwright-core) against the Vite dev server, with every `/api/**` request answered by a MOCKED API and `/hubs/**` returning 404. There was NO backend and NO database. Dark mode was forced by adding the `dark` class by hand (the app has no toggle). Viewports 1366x657 and 1920x969, plus a few single checks at other sizes. A mocked API proves nothing about server behaviour (validation, permissions, real partner-summary numbers, payableBalance, numbering, concurrency). **A check against the real backend by a person is still open.** Scripts and 75 screenshots are in the session scratchpad `browser-check/`, not in the repo.

Passed: 1a, 1b, 2a, 2b, 2c, 2d, 2f, 3c, 3d, 4a, 4b, 4c, 4d, 5, 6a, 6b, 6c, 6d, 7a, 7b, 7c, 8a, 8b, 9, 10b, 10f, 10g, 10h, 10i, and "Enter never triggers a POST".

Failed:
- 2e Rows visible: 15 at 1080p (met), but only 5 at 1366x657 (normal window) and 8 in a full 768px viewport, against the target of 7.
- 3a Arrow keys: ArrowUp on the first row or ArrowDown on the last row changes the value of type=number cells (%CK 0 to 1, %VAT 8 to 7, Số tấm 10 to 9).
- 3b Ctrl+Delete: removes the row, but on any row except the last the focus drops to body.
- 10a Suggestion list: aligned and stacked correctly, but cut off by the window bottom at 1366x657 (1 of 4 options visible on the bottom row).
- 10c Dark mode: the destructive red is rgb(127,29,29) on near-black (about 1.8:1), so negative "Còn được nợ", "Quá hạn" and the tab red dot are barely legible.
- 10d Read-only Active voucher with the banner at 1366x657: the totals bar covers the bottom 55px of the grid.
- 10e Window about 500px high: at scrollTop 0 the sticky bar covers 139px / 3 rows of the grid until main is scrolled to the end.

Not checked: the real WAREHOUSE role and the server-side 403; real server numbers; a real mouse wheel and IME (headless); a real dark-mode toggle (none exists).

## Deviations from the plan

- The plan was reviewed before execution and fixes were applied (ddb2e71).
- Phase 03: `PartnerAutocomplete` remounts its input after a pick, so `StockVoucherMainFields` refocuses `#stock-partner` after a non-null pick.
- Phase 03: the plan's cancelled-voucher test needed `type: 'Out'` in its fixture.
- Phase 04: the first Task 4.4 commit had an unused import (typecheck failed) and was amended before any push.
- Extra commits not in the plan: the plan-fix commit ddb2e71, and the test commits edd2e85 and b21a245 (from verifier mutation checks).
- Plan folder not archived in this branch (deviation from execute-plan Step 4): `docs/SUMMARY.md` links to it. Move it to `docs/plans/archived/` and fix the row after the PR merges.
- Phase 05: `docs/SUMMARY.md` had no row for the plan, so one was added after the brainstorm row. `docs/SUMMARY.md` and `conventions.md` use CRLF and the edits keep CRLF.
- The permission label change applies to new databases only; an existing database keeps the old stored name (no migration, cosmetic).

## Known limits

- While the product suggestion list is open and has results, up/down arrows move inside the list; press Esc to close it before changing rows.
- Enter is swallowed on an open, empty partner list.
- The permission label change does not reach existing databases.

## Residual risks and follow-ups (browser findings, none fixed yet)

1. Ctrl+Delete loses focus on non-last rows. In `stock-line-grid.tsx` (Ctrl+Delete branch, row key near line 629) the first commit has the shortened `fields` but stale `rows`, so React removes the last `<tr>` and the `setTimeout(0)` refocus targets the row about to be removed. Fix: key rows from the field array (`_uiKey ?? field.id`) and/or refocus in an effect once `rows.length === fields.length`. jsdom tests do not see the two-step commit.
2. Arrow keys on type=number cells at the grid edges: `handleGridKeyDown` returns before `preventDefault()`. Fix: call `preventDefault()` for grid cells before the range check.
3. Too few rows at 1366 in a normal window (5, target 7): the toolbar keyboard guide wraps (71px vs 43px) and the debt strip wraps (91px vs 71px). Fix: one-line guide or hide it below about 1500px, no wrapping in the strip.
4. Read-only banner and 500px-high overlap: the panel `min-h-[280px]` overflows its `min-h-0` parents and the bar is `sticky bottom-0`. Fix: put the minimum on the Tabs root, drop `min-h-0` from the form, remove `sticky bottom-0` from the bar wrapper. Files: `stock-voucher-form-page.tsx`, `stock-voucher-tabs.tsx`.
5. The product suggestion list is cut off at the window bottom (`quotations/components/product-typeahead-cell.tsx`, no flip, `max-h-80`); it also keeps following an input scrolled under the header. Fix: open upward when space is short, cap the max height, close when the input leaves the visible area.
6. A negative Phí vận chuyển or CK cả đơn blocks save with only a red dot and focus; no message is rendered (`stock-voucher-payment-tab.tsx`, `stock-voucher-form-page.tsx`). Fix: pass and render the `errors.freight` and `errors.orderDiscount` messages.
7. Saving with no lines from another tab leaves focus on body (`focusFirstError` / `focusIdOf` for `lines`). Fix: focus the add-row button or the active tab trigger when the target is missing.
8. Low: NumberCell `select()` in `requestAnimationFrame` pulls focus back in Chrome if focus left within the same frame (two Enters 10 ms apart end on Đơn giá). Fix: guard with `document.activeElement === el`.
9. Low, pre-existing: while the push-permission banner shows, the app is 54px taller than the window and the totals bar falls below the fold (`PushPermissionPrompt.tsx`, `app-layout.tsx`).
10. Low: dark-mode destructive contrast (token in `index.css`; use `dark:text-red-400`, `dark:bg-red-500` in `partner-debt-strip.tsx` and `stock-voucher-tabs.tsx`). Only matters once a dark toggle exists.
11. Untracked `backend/tests/OrderMgmt.IntegrationTests/TestResults/*.trx` (368 MB) exist locally, and `.superpowers/*` is already tracked on main by commit 524a7f9; neither was modified or staged here.
12. The 3 baseline vitest failures and 3 lint errors pre-exist and were not touched.
13. Still open: a person must check the form against the real backend. Push, PR and the chat post are done by the main session.
