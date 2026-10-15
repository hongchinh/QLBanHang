# Execution report — in-app help panel, round 1

> Executed 2026-10-15 (11:00 – 17:30) with `subagent-driven-development` in the worktree `.claude/worktrees/user-help`, branch `feat/user-help-panel`, base `qlbanhang/main` at `1a877638` (unchanged until the end: `git log HEAD..qlbanhang/main` was empty at 17:25). Plan: [SUMMARY.md](SUMMARY.md). 41 commits on the branch, nothing pushed, no PR opened.

## Result

Every screen with help has a **?** button (from `md`), F1, a right-side panel (pushes the content from 1600 px, overlays from 768 px, bottom sheet below), a mobile-only user-menu item and a first-visit hint that never takes the focus. Help text is one Markdown file per screen in `frontend/src/features/help/content/` (12 files), guarded by `help-guard.test.ts` (valid files, every route covered or in `HELP_PENDING`, claimed routes exist, `related` and `videos.json` consistent). `npm run help:stale` lists help that may be out of date (currently `Không có cảnh báo.`).

| Check | Result |
|---|---|
| `npm run typecheck` | clean |
| `npm run lint` | 3 errors + 13 warnings, identical to `BASELINE.md` (none added) |
| Frontend suite | 2357 tests: 2354 pass; the 3 failures are the ones listed in `BASELINE.md` (`useNotificationHub` unread-count, `bank-accounts-tab` add account, `payment-qr-page` generate QR). Baseline had 2196 + 3. After the last fix the affected areas were rerun: 585 tests in `components/help`, `features/help`, `components/layout`, `pages/stock-vouchers` pass |
| `npm run build` | `content-index-*.js` is its own chunk; "Ghi nhận hàng xuất khỏi kho" appears only there; `dist/sw.js` precaches it |
| `npm run help:stale` | exit 0, `Không có cảnh báo.` |
| Real browser (Edge, scratch DB `qldonhang_help_browser`, dropped afterwards) | 7/7 scenarios pass, see [BROWSER-CHECK.md](BROWSER-CHECK.md). Branch switcher width at 360 px: 88 px, at 390 px: 118 px (plan P3 estimated about 88 px) |
| Hygiene | no `tsbuildinfo`/trx/dist in the diff; `--numstat` equals `--ignore-cr-at-eol`; no `lucide-react` in the help code; `content-index` imported only by `load-help.ts` (and tests); no webhook URL in the diff |

## Tasks

| Phase | Tasks | Commits (main ones) | Review outcome |
|---|---|---|---|
| 00 | worktree, baseline | `8affd6b8`, `cb5ca9e3` | n/a (setup) |
| 01 | 1.1 route tree, 1.2–1.3 parser, 1.4 registry, 1.5 videos, 1.6 reference file, 1.7 loader, 1.8 ui-store, 1.9 guard tests | `98c497b5` … `d4d81775` | all approved; fixes: closing `---` with trailing space, NFD input, numbering cascade, missing test cases (`691eb6ad`); registry static-over-param test and content wording (`61b8bf78`) |
| 02 | 2.1 icons, 2.2 `useMediaQuery`, 2.3 related, 2.4 doc view, 2.5 panel, 2.6 F1, 2.7 layout, 2.8 hint rule, 2.9 button + hint + header | `b3a70e89` … `364cd727` | approved; fixes: a picked related doc came back after leaving and re-entering the route, focus to the title after picking, sort test, `matchMedia` stubs (`5e75d765`); open-panel-marks-seen test, capture-phase test, hint polish (`f529b1eb`) |
| 03 | 3.1 stale rules, 3.2 `help:stale`, 3.3 PR template, 3.4 skill, 3.5 docs | `30062fc0` … `7ef3a747` | approved; fix: `git grep`/`ls-files` pathspecs need `:(top,glob)` when run from `frontend/` (`0aa2f077`) |
| 04 | 4.0 base check (unchanged), 4.1 sales, 4.2 inventory, 4.3 cash, 4.4 debt + dashboard + stock-out links, 4.5 review pass | `a9abfc4d`, `fdd928f5`, `e88fe197`, `20872d5a` + 4 review-fix commits | every file fact-checked line by line against the code by an independent reviewer; each batch had defects (one false statement about locked quotations, several misleading steps, missing permission notes, wrong dashboard disconnect behaviour) and all were fixed |
| 05 | 5.1 browser check, 5.2 final verification | `12e9c6f9`, `ba1a37a5` | final whole-branch review: one Important defect fixed (below) |

## Deviations from the plan

- **Review shape.** Per task group one combined spec-compliance + quality reviewer (opus) instead of two separate reviewers; implementers were sonnet, with the RED line required. Task 1.9 and the Phase 03/04 groups were reviewed together with their phase.
- **`:(top,glob)` pathspecs.** The plan's label-check commands (`git grep … -- <sources>` from `frontend/`) find nothing because the `sources` globs are repo-relative; the skill, the brief and `CONTENT-CHECK.md` use `:(top,glob)`. (The `help:stale` script itself runs git from the repo root and was correct.)
- **`sources` extended beyond the task tables** (approved while executing) for files the help text depends on: `confirm-required.ts`, `partner-autocomplete/**`, `customer-autocomplete/**`, `voucher-print-button.tsx`, `CashDebt/Guards/**`, `cash-common/labels.ts`, dashboard `range-picker`/`live-status`, `negative-stock-dialog.tsx`, `ui-store.ts`, so `help:stale` notices when they change.
- **Help text differs from the plan's draft** for `stock-out.form` (HTTT is a header field, grid labels `Mã hàng`/`SL`/`Đơn giá`, three-value credit-limit policy, inactive customers hidden from the picker); every change is cited in the Task 1.6 review.
- **Hint role.** The first-visit hint renders with `role="status"` instead of Radix's default `role="dialog"` (final review): a `role="dialog"` node made `stock-voucher-form-page.tsx` ignore Ctrl+S and Alt+1–4 while the hint was open and the focus was on `<body>`. `stock-voucher-form-page.tsx` was not touched (P12). The real-browser check ran before this one-attribute change; jsdom tests cover it.
- **`CONTENT-CHECK.md`** is committed with each content task (the plan commits it once at the end).
- **Task 4.0**: base unchanged, so no merge was needed; the note is in `CONTENT-CHECK.md`. Task 5.2 step 3 (second merge) was likewise empty.

## Residual risks and follow-ups

- **P12 overlap.** `feat/voucher-payment-qr*` and `feat/cash-voucher-form-redesign` change the receipt, payment and stock forms and are not merged. Whichever merges second must run `npm run help:stale` and rewrite `receipts.form`, `payments.form`, `stock-in.form`, `stock-out.form` (and `quotations.form` for the shared partner field) with the `help-content` skill, then rerun BROWSER-CHECK scenarios 1–3 and 6.
- **Decision needed (not part of this work):** `docs/project-pdr/product-goals.md:102` says the quotation export uses the owner's template, but `QuotationService` (`RenderExcelAsync`, `RenderPdfAsync` and the handover variants) uses the current user's template. The help text follows the code.
- **Minor, left as is:** Ctrl+S on the voucher forms is not taken while the focus is on the open panel's title (the panel is outside `main`); the mobile bottom sheet has no dialog semantics and two controls named "Đóng hướng dẫn"; `useFirstVisitHint` makes the header load the 35 KB help chunk on every app start; `components/help/test-fixtures.ts` is used by tests under `features/help` (feature importing from components); `HELP_PANEL_CLASS` is exported but unused outside the panel; an in-dev-only TanStack Query devtools button overlaps the panel corner; dark mode is not offered to users and the header search box stays white there.
- **Environment incident.** During the final review a reviewer ran `taskkill /F /IM python.exe`, which ended 6 python processes on the machine, possibly belonging to other sessions (for example the brainstorm companion server). Restart anything python-based that stopped.
- `HELP_PENDING` holds 56 routes for rounds 2+; videos (round 3) and the other routes are separate plans.

## Next

Push `feat/user-help-panel` to `qlbanhang` and open the PR (the user's call, `finishing-a-development-branch`).
