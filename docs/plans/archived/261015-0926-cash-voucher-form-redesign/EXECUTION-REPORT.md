# Execution report — cash voucher form redesign and one shared partner field

> Executed: 2026-10-15 (one session, subagent-driven) · Plan: [SUMMARY.md](SUMMARY.md) · Branch `feat/cash-voucher-form-redesign` (not pushed) · Base `qlbanhang/main` `1a877638`; `qlbanhang/main` (`db546dda`, PR #21 user help panel) merged in before the push, see "After the report" · Head ``e364c684` (last code commit; the archive commit follows)`
> Supporting files in this folder: [BASELINE.md](BASELINE.md), [BROWSER-CHECK.md](BROWSER-CHECK.md), [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)

## Outcome

All seven phases are done. Cash vouchers, stock vouchers and quotations now use one partner field (`PartnerCodeNameField` over `PartnerAutocomplete`) with the same keys, quick add (F4 and `+`), catalog and permissions. The receipt/payment form is keyboard-first: no MST input, partner code and name on one row, "reason - partner" auto text that never dirties a saved voucher, Enter / Shift+Enter through the header and the line grid, reason auto-match with a shared notice line.

- Frontend, final run: typecheck clean; lint 2 errors, both on the base (one base error went away when the customer catalog moved); build OK; 717 files, 2603 tests, 4 failures = the 3 baseline failures + `header-search` "keyboard Enter follows the same rule" (fails only under machine load, passes 4/4 alone; the branch does not touch `components/layout`).
- Backend, targeted classes (`CashVoucherQueryTests`, `StockVoucherHelpersTests`, `PartnerQuickAddSeedTests`, `CashDebtSeedTests`, `DbSeederUpgradeTests`, `AdminRolesCrudTests`) on `qldonhang_integtest_cvf`: only the 6 baseline `AdminRolesCrudTests` failures. A first run under load also hit 3 Npgsql read timeouts; the two classes rerun alone passed 11/11.
- Real browser (Edge, mocked API and a real API on port 5055 against a scratch database): 10 scenarios, 4 defects found and fixed, re-run passed. No request ever reached the developer API on 5050; the scratch database was dropped.
- Final whole-branch review (opus): ready to merge; its follow-ups were done and re-reviewed (see Phase 06).

## How it was run

- One worktree per lane under `.claude/worktrees/` (`cash-form` main lane; side lanes `cvf-p01-backend`, `cvf-p03`, `cvf-p04`, `cvf-p05`), merged `--no-ff` into the feature branch. Phase 04 and Phase 05 ran in parallel after Phase 03, as the plan allowed.
- Implementers: sonnet; opus for Phase 04 Tasks 4.4/4.6/4.7 (the most intricate task). Each reported the RED line of its first test run.
- Reviews: one opus reviewer per task or task group, spec compliance first, then code quality, each in its own detached checkout (`$TEMP/rev-<sha>`, `node_modules` as a junction). Most reviews ran mutation checks. Fix cycles went back to the same implementer and the same reviewer.
- Test databases: `qldonhang_integtest_cvf_base` (baseline), `_cvf_p01`, `_cvf_rev1`, `_cvf` (final). Never the dev databases.

## Tasks, commits and reviews

### Phase 00 — worktree, baseline
- `fe7338c6` plan and brainstorm docs, `fc88d30c` BASELINE.md. Baseline: frontend lint 3 errors, 3 failing frontend tests, 6 failing backend tests (all pre-existing).

### Phase 01 — backend `activeOnly`, seed grants, pure logic (two lanes)
| Task | Commit | Review |
|---|---|---|
| 1.1 `activeOnly` on `GET /cash-vouchers/partners` | `ea84b7a2` | spec ✅ quality ✅ |
| 1.2 `activeOnly` on `GET /stock-vouchers/partners` | `907da55e` | spec ✅ quality ✅ |
| 1.3 seed grants (Accountant, Warehouse) | `2af74214` | spec ✅ quality ✅ |
| 1.4 `reason-match.ts` | `ad74c6e4` | spec ✅ quality ✅ |
| 1.5 `useReasonSwitchNotice` | `44a65e97` | spec ✅ quality ✅ |
| 1.6 `auto-text.ts` | `62fcd5ea` | spec ✅ quality ✅ |
| 1.7 `enter-focus.ts` | `7fe59cbe` | spec ✅ quality ✅ |
Backend lane merged as `0c9e7883`. Verification: 43 tests, 37 passed, 6 baseline failures.

### Phase 02 — shared partner field
| Task | Commit | Review |
|---|---|---|
| 2.1 `activeOnly` through hooks, keys, api | `1a0fdcce` | ✅ ✅ |
| 2.2 `partnerSearchSummary` | `e06cc4ff` | ✅ ✅ |
| 2.3 `StopFormEvents`, `toPartnerSearchItem`, `usePartnerPermissions` | `75c9c510` | ✅ ✅ |
| 2.4 `useUnpickedPartnerGuard` | `b5a8d01c` | ✅ ✅ |
| 2.5–2.7 `PartnerAutocomplete` list, rows, keys | `d1a27371`, `ece710ef`, `bbf9ddb7` | spec ✅; quality: Changes needed → fixed in `f7a6bb0b`, re-review ✅ |
| 2.8 `PartnerCodeNameField` | `cbeef6a3` | ✅ ✅ |
| Review follow-ups | `8a2034ba` | — |
The Important finding: Tab was swallowed while the list was "open" but not yet shown (latent until the catalog row is enabled). Verification: 1413 tests, only the baseline failure.

### Phase 03 — quick add and catalog dialogs
| Task | Commit | Review |
|---|---|---|
| 3.1 `CustomerFormFields` free roles, Enter navigation | `5d8ae64e` | ✅ ✅ |
| 3.2 `PartnerQuickAddDialog` | `9f634131` | ✅ ✅ |
| 3.3 catalog moved to `partner-catalog-{list,dialog}`, roles, keys | `7b77691b` | ✅ ✅ |
| 3.4 dialogs wired into the field | `2f0b18f8` | ✅ ✅ (9 of 11 mutations killed) |
| Review follow-ups | `0df96a48`, `71191063` | re-review ✅ |
`71191063` replaced a boolean "dialog open" ref with a per-opening token (`useDialogOpening`), so a quick-add or catalog result that arrives after the user cancelled (or cancelled and reopened) is ignored. Side lane merged as `20dc5676`. Verification: 1508 tests, only the baseline failure.

### Phase 04 — cash voucher form (side lane `cvf-p04`)
| Task | Commit | Review |
|---|---|---|
| 4.1 no `partnerTaxCode`, `createEmptyCashLine(description)` | `bfcc7085` | ✅ ✅ |
| 4.2 `cash-navigation.ts` | `bd6dc5ba` | ✅ ✅ |
| 4.3 `useCashAutoText` | `3a167a5e` | ✅ ✅ |
| 4.5 line grid Enter / Shift+Enter | `d01c8a78` | ✅ ✅ |
| Auto text fills the default reason after an early partner pick (spec §3.2 over plan code) | `5e486035` | ✅ |
| 4.4 + 4.6 + 4.7 header, page wiring, page tests | `5a20dc95` | ✅ ✅ |
| Review follow-ups | `a8b8e1e7` | re-review ✅ |
Merged as `0de18e93`.

### Phase 05 — stock voucher and quotation (side lane `cvf-p05`)
| Task | Commit | Review |
|---|---|---|
| 5.1 server `partnerId` error on the stock form | `3bcea6d3` | ✅ ✅ |
| 5.4 `useQuotationCustomerSearch` | `d432c103` | ✅ ✅ |
| 5.2 + 5.3 stock form on the shared field | `140de0af` | spec ✅ (one layout exception); quality: Changes needed |
| 5.5 quotation customer row; old `customer-autocomplete` deleted | `536f3030` | ✅ ✅ |
| Focus fallback when the partner is locked | `c8cdd166` | ✅ |
| Row class test | `36fb51f3` | ✅ |
| Fixes: notice cleared on reset; partner error on grid row 2; one shared reason notice for cash and stock | `cb8a1120`, `f6026fef`, `38b5b045` | re-review ✅ (layout re-measured in Edge at 1440/1300/1100/800) |
Merged as `b4aaeb56`.

### Phase 06 — cleanup, docs, browser checks, final verification
| Task | Commit | Review |
|---|---|---|
| 6.1 remove the reason-based search mode | `fac25400` | ✅ ✅ |
| Review follow-ups (stock row 1 alignment, stock 409 test, unreachable focus resets) | `e42cee66` | ✅ |
| 6.2 docs and go-live checklist | `2b70b924` | covered by the final review |
| 6.3 browser-check fixes D2–D4 + test checks + type fix | `4b468adc`, `328f055c`, `8f64a481`, `f922ae10`, `6e57ad22` | ✅ ✅; browser re-run passed |
| Browser check notes | `388ce62a` | — |
| Final-review follow-ups | `c338aea6`, `ac85494a`, `b35cbe1b`, `a73ebf4e`, `e364c684` | `ac85494a` rejected (red line on false positives) → fixed in `e364c684`, re-review ✅ |

Browser defects: D1 stock row 1 dropped when a reason notice showed (already fixed by `e42cee66`); D2 the quotation Enter walk ignored IME composition (pre-existing, but go-live item 4 relies on it); D3 catalog arrow keys stopped working after a mouse click on a tab; D4 partner-row labels sat between the two lines when code and name wrapped.

## Deviations from the plan (with reasons)

- **Auto text (§3.2 over plan code).** `onInputsChanged` settles the one-time default fill only when its auto text is non-empty, so a partner picked before the reasons load does not block the default reason text.
- **`cashLineCellId`** lives in `pages/cash-vouchers/utils/cash-navigation.ts`, not in the grid component, to avoid a react-refresh lint warning.
- **Reason notice.** Phases 04 and 05 specified different markup. Both forms now use one `reasonNoticeOf` derivation and one `ReasonNoticeLine` (one paragraph, the field error wins, `role="status"`, amber for the switch notice, red for a mismatch). Server resets clear the notice on both forms.
- **Stock partner row.** The plan's JSX put the error between the field and `Đ.chỉ`, which pushed the address to a second line at ≥1024 px; the error is pinned to grid row 2.
- **Row alignment.** `form-align-top` (new CSS class) on the cash reason row, the stock row 1 and the three partner rows, so labels stay level with the first line when a notice shows or when code and name wrap.
- **Late dialog results.** A per-opening token in the field ignores quick-add and catalog results that arrive after a cancel; the catalog also ignores Enter while its typed text is not searched yet, and a second select while a detail loads.
- **New red line** when the reason is empty and no configured reason fits the picked partner (spec §4.2 step 3 has no wording for it): `Không có lý do nào hợp với <mã> (<vai trò>)`, shown only once the reasons have loaded.
- **Cash form** clears a stale server `partnerId` / `reasonId` error on a partner or reason change (the stock form already did).
- **Plan text corrected in the docs:** permissions per scope (either create permission is enough in the partner scope); browser scenario 7 expected the `+` to disappear when `customers.create` is removed, but spec §6.2 keeps it while `suppliers.create` remains.
- **Extra tests** beyond the plan: runtime checks that no host page renders an element with the quick add's global ids (`code`, `name`, `note`, …); a catalog-pick case on the quotation page.

## Open question for the product owner

Spec §1.2 says the reason line under Lý do "chỉ hiện tạm hoặc khi có lỗi". The implementation (plan decision P7) keeps the yellow "Đã đổi lý do…" text until the next partner or reason change or a save; only the 1.6 s flash of the select is temporary. Making the text disappear with the flash is a one-line change in `features/cash-common/use-reason-notice.ts`.

## Residual risks and follow-ups

1. **Merge with the payment QR plan** (`feat/voucher-payment-qr`, not merged): a trial merge had 3 conflicts — the stock page import block, `docs/codebase/directory-structure.md`, `docs/SUMMARY.md` — all "keep both"; the merged tree typechecked and the two form-page test files passed 194/194. After the QR merge, update go-live item 8 and the "once it is merged" clause in `conventions.md`.
2. **Quotation line grid and product typeahead** still act on an Enter during IME composition (`line-items-grid.tsx` ~193 and ~323, `product-typeahead-cell.tsx` ~181). Pre-existing; not covered by the go-live checklist.
3. **Quick add uses global DOM ids** (`focusById('name')`, the Enter order `code`, `name`, `note`, …). No collision on the three host pages today (asserted by tests); scoping the lookups to the dialog content would remove the hazard.
4. **`partner-autocomplete.tsx`** is about 470 lines; extracting the dropdown (`PartnerSearchList`) is a good next refactor.
5. **Bisect:** commits `328f055c`, `8f64a481`, `f922ae10` fail `typecheck` alone (fixed by `6e57ad22`). Not squashed: the history has merge commits.
6. **Seed grants reach new databases only.** An admin must enable them on running databases (go-live checklist item 2). Deploy the backend before the frontend (item 1).
7. **Real Telex/VNI typing** is on the go-live checklist (item 4); the browser check covered composition through CDP only.
8. **Leftover test databases** `qldonhang_integtest_cvf*` remain on the local server (the fixture drops its own copies; the base names stay).
9. The plan's "Shared contracts" show the pre-Task-6.1 signatures (with `reasonId`/`requireReason`); a note at the top of the plan SUMMARY says so.

## After the report: merge of `qlbanhang/main` and help files

- Before the push, `qlbanhang/main` had moved to `db546dda` (PR #21, the user help panel: 42 commits, frontend and docs only). Merged as `9a86ddfe`. Conflicts were docs only: `docs/code-standard/conventions.md` (both new sections kept), `docs/codebase/directory-structure.md` (the new `help/` line kept, the deleted `customer-autocomplete/` line dropped), `docs/SUMMARY.md` (row descriptions joined, both brainstorms' rows kept).
- Main's rule "a UI change updates its help file in the same PR": `c6ea2f93` updates the help of `receipts.form`, `payments.form`, `stock-in.form`, `stock-out.form` and `quotations.form` (partner field, partner before reason, quick add and catalog, auto text, Enter order, F4) and replaces the quotation's dead `customer-autocomplete` source glob. `npm run help:stale` reports nothing; the help tests pass.
- Merged tree (`c6ea2f93`): typecheck clean; lint 2 errors (the base ones); build OK; 766 files, 2776 tests, only the 3 baseline failures. The backend is unchanged by the merge.
