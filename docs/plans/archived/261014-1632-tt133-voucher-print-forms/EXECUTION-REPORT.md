# Execution Report — TT133 voucher print forms

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/tt133-voucher-print-forms` (from `qlbanhang/main` `8135477`, not pushed) · Executed 2026-10-14 → 2026-10-15 · Baseline: [BASELINE.md](BASELINE.md) · Go-live gate: [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)

## Outcome

All six phases (00–05) are complete. The branch prints receipts (01-TT) and payments (02-TT) on A5 landscape and stock-in (01-VT) and stock-out (02-VT) vouchers on A4 portrait, as Excel and PDF, from the voucher pages and from the four list pages (one voucher, or the selected rows, one sheet / page each). Stock vouchers print in three price modes (Giá trên phiếu, Giá vốn from the stock ledger, Không in tiền). The accountant's questions of the brainstorm are still open; they are the go-live gate in the checklist.

| Check | Baseline (Phase 00, `3a4cc83`) | Final | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors | ✅ |
| `dotnet test OrderMgmt.sln` | 19 failed / 1281 total | 19 failed / 1391 total (1372 passed, 28 m 13 s) on `272f8ca` | ✅ the same 19 tests, compared by name |
| `npm run typecheck` | exit 0 | exit 0 | ✅ |
| `npm run lint` | 3 errors, 13 warnings | 3 errors, 13 warnings | ✅ same as baseline |
| `npm run test` | 3 failed / 1961 total | 3 failed / 2044 total (2041 passed) | ✅ the same 3 tests |
| `npm run build` | passes | passes | ✅ |

- The frontend run is on `6cd1449` (all frontend code except the file-name fix `e9bf2ba`, which was checked with `npx vitest run src/features/voucher-print`, eslint and typecheck, also under `TZ=UTC`).
- The backend run is on `272f8ca` (everything except the two guard tests of `a4d61e0`, which were run on their own: `StockVoucherPrintTests` 28/28). The suite grew by 110 backend tests and 83 frontend tests.
- Phase 01 checkpoint: the full suite on `8c7f4f6` ran 1358 tests with exactly the 19 baseline failures (compared by name). Phase 03's targeted checkpoint (`Tt133|DashboardSignal|StockVoucher|CashVoucher`) ran 400/400.
- The change is 71 files, about +7,100 / −230 lines against `8135477`.

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| 00 Worktree, baseline | `3a4cc83`, `7693795` | parallel tracks chosen by the user |
| 01 Cash vouchers, backend | `a6c8515`, `5a287ae`, `dec0a22`, `096b077`, `522328c`, `e5b320e`, `cd01fdb`, `bb37c35` | `096b077` tightened the builder tests after review |
| 02 Cash voucher pages | `b99633e` → `acaa621`, fix `f872055`, marker `e6a3f22` | `f872055`: list cells remounted on every render |
| 03 Stock vouchers, backend | `35b419a`, `bfaf8a5`, `6f6635e`, `70b241a`, `d0c9f25`, `0776f08`, `09fdaa0`, `03fddc1`, `3eef1d1`, `8d83862` | 3.1–3.2 ran in a third worktree in parallel with 1.5–1.7 |
| 04 Stock voucher pages | `6035ba5` → `5597680`, fixes `43ddf70`, `5485316`, `1e48fe5`, marker `8c36f81` | focus fixes after review |
| 05 Output, docs, verification | `a9984f3`, `c4cb2f2`, `ebdc1ca`, `b5bd24c`, `3ca63d7`, `50175d0`, `7f2e834`, `1d96f9c`, `ae665f4`, `4d6ea1e`, `e9bf2ba`, `a4d61e0`, markers `e01190e` and this report | layout fixes from the PDF check; final review minors |

## How it was run

- Subagent-driven: a fresh implementer per task (sonnet; opus for the form builder, the stock print service and the layout fixes), test first with the RED line reported, then a review per task. To save usage, each task had **one** reviewer that ran the spec-compliance stage and then the quality stage in order (the skill describes two separate reviewers); reviewers ran their tests in a detached checkout with their own database so that the next implementer could work at the same time.
- Tracks: frontend (Phases 02, 04) in `.claude/worktrees/tt133-voucher-print-fe`; backend in `.claude/worktrees/tt133-voucher-print`; Tasks 3.1–3.2 and the Phase 05 layout fixes on short branches in side worktrees, merged back. Each worktree had its own test database (`qldonhang_integtest_vprint`, `_vprint3`, `_vprint_smp`, reviewers `_vprint_rv`, `_vprint_rv2`, checkpoint `_vprint_p01`).
- Review findings that changed code (all fixed with a failing test first):
  - list pages: putting the selection in the columns memo remounted every cell on each render (lost focus, closed menus) → selection passed through TanStack `meta` (`f872055`);
  - price-mode dialog: focus fell to `<body>` on close, then the deferred refocus took focus back from where the user went (`5485316`, `1e48fe5`); unlabelled radio group (`43ddf70`);
  - `{"ids":null}` was answered 400 by MVC's implicit `[Required]` → `Ids` nullable, controllers pass `Ids ?? []` (`bb37c35`, `3eef1d1`);
  - VAT-included line limited to Voucher mode; warehouses of the printed lines only in Cost/None (`09fdaa0`, `03fddc1`); no provisional note when nothing prints (`8d83862`);
  - builder and stock form tests pinned to positions, not presence (`096b077`, `6f6635e`);
  - final review: batch file names on the frontend in VN time like the backend (`e9bf2ba`); two stock guard tests (`a4d61e0`).

## Real PDF output (Task 5.1)

- Samples (xlsx + pdf) in `%TEMP%\voucher-print-samples\`: `01-TT`, `02-TT-long`, `01-TT-cancelled`, `01-TT-batch`, `01-VT-voucher`, `01-VT-cost`, `02-VT-vat-inside`, `01-VT-40-lines`, `01-VT-spec-warehouses`, `02-VT-none`; after the fixes `fixed\` and `fixed2\` (40/70/98 lines, Kho, long and short header), page images in `png\` folders. Converted with the local LibreOffice (`soffice --headless`, own profile); Docker was not used.
- Page sizes: 01-TT / 02-TT `595.30 × 419.56` pt (A5 landscape), one page each, the batch three pages; 01-VT / 02-VT `595.30 × 841.89` pt (A4 portrait); 40 lines → 2 pages with the header rows repeated. Times New Roman with Vietnamese diacritics everywhere.
- Defects found and fixed: the signature block split across the page break on long stock vouchers (manual break estimated from LibreOffice's geometry with a ±3 % margin, moving the last two lines, the totals and the signatures together; checked by converting every length from 30 to 140 lines); the three "Mẫu số…" lines spread when the unit name wrapped (one merged rich-text cell); the `Kho` column broke codes (11 wide, name column 31); A5 not centred.
- Number cells follow the LibreOffice locale (`1.500.000` on this vi-VN machine); text lines are formatted by the backend.

## Deviations from the plan

- One reviewer per task running both stages in order, instead of two reviewer agents (usage).
- The frontend worktree was created from the plan commit before `BASELINE.md` existed (the baseline came in through the merge).
- Phase 03 had no separate full-suite run; its targeted run plus the final full suite cover it.
- `Dots` is the plan's literal (8 `…` characters); the task text's "ten" was a slip.
- `CashVoucherPrintRequest.Ids` / `StockVoucherPrintRequest.Ids` are `List<Guid>?` (see above).
- Task 1.5/1.6 test data: the PDF test voucher code is fixed (`PTPDF1`); each test factory clones its own database.
- Not done: D4 — on 01-VT without the `Kho` column the last signer block (Kế toán trưởng) is the narrowest (15 units); a long name wraps but is not cut. Changing the split rule would move the agreed 02-VT columns.

## Smoke test

Not run: this session has no browser tool and HTTP clients are denied, so the steps of Task 5.4.2 (print from the receipt page and list, price-mode dialog with and without `inventory.view_cost`, overlay click on the stock-out list, empty company name) are left for the user. Behaviour is covered by the page and component tests and by the reviewers' jsdom probes of the focus handling.

## Open items

- Accountant / PO answers and the per-form sign-off: [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md).
- Follow-ups: D4 (signer block widths on 01-VT); `openPdfTab` does not check a tab the user closed before the PDF arrived (same as the quotation page); an all-capital 10-character warehouse code still wraps in the `Kho` column; on a server with an en-US LibreOffice locale numbers print as `1,500,000`.
- The main checkout keeps uncommitted copies of the plan folder, the reviewed brainstorm and its own `docs/SUMMARY.md` edits; after this branch is merged they can be discarded there with `git checkout -- <path>` (or deleted, for the untracked plan folder) when identical.
