# Stock voucher form redesign (Thiết kế lại form phiếu nhập/xuất kho)

> Created: 2026-10-13 06:22:28 · Source design: [brainstorm SUMMARY](../../brainstorms/261013-0615-stock-voucher-form-tabs/SUMMARY.md) (source of truth for UI and behavior) · Mockups: [mockup-02-t2-detail.html](../../brainstorms/261013-0615-stock-voucher-form-tabs/mockup-02-t2-detail.html)

## Goal

Make the line grid the main part of the stock in/out voucher form, so that a voucher with 15+ lines can be typed without scrolling the page. The form gets:

- a 2-row main area: Ngày, Lý do, Kho, HTTT / Mã đối tượng, Tên, Địa chỉ;
- 4 tabs: Hàng hóa (default), Thông tin khác, Thanh toán, Lịch sử;
- a grid that fills the rest of the viewport, with optional and sticky columns;
- an Enter path that follows the product's pricing mode, configurable per user and per direction;
- a bottom totals bar with a partner debt line (receivable, payable, after-voucher balance).

The backend adds `PayableBalance` to the partner debt summary.

## Scope

**In scope**

- Backend: `PartnerDebtSummaryDto.PayableBalance` and its integration test; the seed label of `debt.partner_summary` (`DbSeeder.cs`, see D1).
- Pure frontend utils with unit tests:
  - `line-navigation.ts`
  - `use-stock-grid-prefs.ts`
  - `debt-after-voucher.ts`
  - `voucher-tabs.ts`
- Form layout T2:
  - `stock-voucher-main-fields.tsx`
  - `stock-voucher-tabs.tsx`
  - `stock-voucher-payment-tab.tsx`
  - `stock-totals-bar.tsx`
  - `partner-debt-strip.tsx`
- Removal of `StockTotalsPanel` and `CustomerDebtPanel` (Phase 03 creates the new components as new files, and deletes the old panels and their tests in Task 3.6 together with the page rewrite). The Thông tin khác tab stays inline in the page (D10).
- Grid:
  - column definition array
  - optional and forced columns
  - sticky columns and header
  - full-height scroll
  - `stock-grid-settings-popover.tsx`
  - per-mode Enter path, ↑/↓, Ctrl+Delete
  - focus after product select; Enter on an empty Mã does nothing
  - `ProductTypeaheadCell` (shared with quotations): the optional `openOnFocus` prop only (D4)
- Docs: `system-architecture.md`, `directory-structure.md`, `conventions.md`, `docs/SUMMARY.md`.

**Out of scope** (brainstorm non-goals)

- Paste from Excel / pull lines from a quotation (next round).
- Cash voucher and quotation forms.
- Mobile-specific layout.
- Server-side (company-wide) grid preferences.

## Decisions made while planning (in addition to the brainstorm)

| # | Topic | Decision |
|---|---|---|
| D1 | Permission label | The code `debt.partner_summary` stays. The seed label in `DbSeeder.cs` changes to "Xem công nợ đối tượng trên phiếu nhập/xuất" (brainstorm §4), because the permission now also covers stock-in. `DbSeeder` only inserts missing permission codes, so **only new databases get the new label; existing rows keep the old `Name`, and no data migration is written** (cosmetic). Phase 01 changes the seed (after a grep of `backend/tests` for the old string); Phase 05 updates `system-architecture.md` to the new label. |
| D2 | Where the grid prefs live | `use-stock-grid-prefs.ts` and `line-navigation.ts` go to `frontend/src/pages/stock-vouchers/utils/`, not `features/`. They are UI-only, and their types (`LineFocusField`, `OptionalColumn`) belong to the grid page. This avoids a `features → pages` import. |
| D3 | Prefs hook signature | `useStockGridPrefs(userId: string, type: StockDirection)`. `StockLineGrid` reads `userId` with `useAuthStore((s) => s.user?.id ?? '')` and owns the hook, so the page passes nothing new. |
| D4 | Focus after product select | `ProductTypeaheadCell` only gains the optional prop `openOnFocus` (default `true`, so quotations behave as before; Phase 04 Task 4.5). The stock grid passes `openOnFocus={false}`, because focusing a filled Mã by Enter or ↑/↓ must not re-open the suggestion list. The grid stops passing `nextFocusId` and focuses `nextStop(...)` itself in `onSelect`, after render (`setTimeout 0`). The typeahead already re-positions its portal dropdown on any scroll (`addEventListener('scroll', …, true)`), so brainstorm risk 1 is already handled. |
| D5 | Hidden tab panels | Radix `TabsContent` with `forceMount` stays in the DOM and is **not** `hidden`. Inactive panels are hidden with the class `data-[state=inactive]:hidden`. Element ids stay the same, so existing tests that use `byId(...)` keep working. Tests check the active tab through `data-state`. |
| D6 | Error → tab | A pure `tabOfField(key)` maps a top-level form key or a server key (`lines[0].width`) to `'lines' \| 'other' \| 'payment' \| null`, where `null` means the main area. The order used to pick the first tab with an error is `lines`, `other`, `payment`. |
| D7 | Implicit submit | `handleFormKeyDown` calls `preventDefault()` on an unhandled Enter whose target is an `INPUT` or `SELECT`, so Enter never submits the form. Saving is only through Ctrl+S or the buttons. |
| D8 | Phase split | The suggested "split components first, keep behavior" phase is merged into the layout phase. Extracting the old card layout only to reshape it right after would be double work. The pure logic gets its own phase instead, so the UI phases consume tested functions. |
| D9 | Debt line visibility | The strip shows when a partner is picked and the user has `debt.partner_summary` or `reports.debt`, **for both directions and every reason**. Rows marked "→ sau phiếu" show only for a side that the current or saved reason changes, and never on a cancelled voucher. |
| D10 | Thông tin khác tab | The tab stays inline in the page (a `fieldset` with MST, Người nhận/giao, Ghi chú). There is no `stock-voucher-other-tab.tsx`: it would only move a few registered inputs and add props, so the brainstorm's file list is not followed here. |
| D11 | Grid cells | `stock-line-cells.tsx` (`NumberCell` / `PercentCell`) is not extracted, YAGNI: only the grid uses them. The brainstorm's file list is not followed here. |

## Assumptions

- The brainstorm is approved (the user picked T2 and approved sections 1–3).
- `@radix-ui/react-tabs` and `@radix-ui/react-popover` are already installed (wrapped by `components/ui/tabs.tsx` and `popover.tsx`). No dependency is added.
- The main content area is `main#main-content` with a fixed height (`grid-rows-[4rem_minmax(0,1fr)]`, `overflow-y-auto`, `p-4 md:p-3`), so a page root with `h-full` fills the viewport under the header.
- Suppliers live in `Customers` (`Customer.IsSupplier`), so the summary endpoint needs no lookup change.
- Remotes: work branch `feat/stock-voucher-form-tabs` from `qlbanhang/main`; PR to `qlbanhang`; no `gh` CLI (memory `git-remotes`).

## Risks

- **Test churn** in `stock-voucher-form-page.test.tsx` (973 lines) and `stock-line-grid.test.tsx`. Mitigations: element ids are kept (D5), and the totals/debt testids are renamed in one place per phase.
- **Sticky cells with `border-collapse`.** Sticky cells need their own background, including on hover/focus, and a z-index. Checked manually in Phase 05.
- **jsdom cannot test layout** (height, sticky, scroll). Covered by the manual checklist in Phase 05 at 1366×768 and 1920×1080.
- **Habit change**: Enter no longer saves. The keyboard guide lists Ctrl+S, and the save button gets `title="Ctrl+S"`.
- **Baseline failures**: Phase 01 records `BASELINE.md`, and every later phase compares against it.

## Phases

- [x] Phase 01 — Branch, baseline, backend `PayableBalance`, permission seed label (S) — `phase-01-backend-payable-balance.md`
- [x] Phase 02 — Pure logic: line navigation, grid prefs, debt after voucher, error tabs (M) — `phase-02-pure-logic.md`
- [x] Phase 03 — Form layout T2: main fields, tabs, totals bar, debt strip, full height (L) — `phase-03-form-layout.md`
- [x] Phase 04 — Grid: columns, sticky, settings popover, keyboard navigation (L) — `phase-04-grid.md`
- [ ] Phase 05 — Docs, manual check, final verification (S) — `phase-05-docs-and-verification.md`

Phases run in order. Each phase ends with green targeted tests, `npm run typecheck`, `npm run lint` and `npm run build` (frontend phases), or the targeted `dotnet test` (Phase 01).

## Executor conventions

- **Branch:** `feat/stock-voucher-form-tabs`, created in Phase 01 from `qlbanhang/main`, in place (no worktree). One phase per run, then stop and report (memory `feedback-one-phase-per-run`). Implementers are sonnet agents and must show RED evidence (memory `feedback-subagent-models`).
- **Commits:** conventional commits, scope `backend`, `frontend` or `docs`. End every commit message with the attribution line from the session's system reminder.
- **Phase completion commit:** the last task of every phase sets `**Status:** [x] complete` in the phase file, ticks the phase in this file, and commits both: `git add docs/plans/261013-0622-stock-voucher-form-tabs/phase-0N-*.md docs/plans/261013-0622-stock-voucher-form-tabs/SUMMARY.md` then `chore(plan): stock voucher form phase 0N complete`. This keeps progress on the branch if a session dies.
- **Staging:** stage explicit paths only (`git add <paths>`), and list them in every commit step. Never `git add -A`, `git add .`, `git add -u` or `git commit -a`. Commit `524a7f9` already tracks `.superpowers/*` and five `backend/tests/**/TestResults/*.trx` files (`baseline`, `current`, `final`, `p03`, `p3verify`): never stage or modify them, and do not run `dotnet test --logger "trx;LogFileName=..."` with those names. Whether to remove them from the index is the user's decision, not part of this plan. Only `source/` and `.claude/tools/` stay untracked.
- **Shell:** Git Bash. Keep the cwd on the uppercase `D:\` drive.
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. After `npm run build`, run `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo` (skip any path that is not tracked).
- **Backend tests** (from repo root):
  ```bash
  export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
  dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests"
  ```
  Never point it at `qldonhang_test`. If a running dev WebApi locks `bin`, add `-o "$TEMP/qlbh-build"`. If restore fails with "path1 null", set `APPDATA`/`LOCALAPPDATA` (memory `round2b-execution-notes`).
- **RED can be a compile/module error**: a test importing a function that does not exist yet counts as RED ("FAIL — `<name>` is not exported").
- **Baseline:** `BASELINE.md` records the `FAIL` lines of `npm run test`, the lint error/warning counts and the `npm run typecheck` result. Compare with it after each phase.
- **Archiving:** do not move this plan folder to `docs/plans/archived/` in this branch (Phase 05 adds a `docs/SUMMARY.md` link to it). Archive it in a follow-up docs commit after the PR merges, and record this as a deviation from execute-plan Step 4 in `EXECUTION-REPORT.md`.
- **Team chat:** after each phase, post a short Vietnamese summary with `bash .claude/tools/gchat.sh "…"` from the repo root (memory `feedback-gchat-updates`).

## Final Verification

From the repo root:

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~PartnerSummaryTests|FullyQualifiedName~CashDebtSeedTests"
```

From `frontend/`:

```bash
npx vitest run src/pages/stock-vouchers
npm run test        # FAIL lines == BASELINE.md
npm run typecheck
npm run lint        # error/warning counts <= BASELINE.md
npm run build && git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
```

Manual: the checklist in Phase 05 Task 5.3.

## Rollback / Recovery

- No migration. The backend change only adds a DTO field, which older frontends ignore.
- To roll back, revert the merge commit of `feat/stock-voucher-form-tabs`, or individual phase commits newest first, and redeploy.
- localStorage gains `qldh:stock-grid-prefs:{userId}:{In|Out}` (colon style, like `qldh:col-sizes:`). An older build never reads it, so no cleanup is needed.
