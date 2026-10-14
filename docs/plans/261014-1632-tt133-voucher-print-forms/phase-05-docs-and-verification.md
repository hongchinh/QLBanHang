# Phase 05 — Real output, go-live checklist, docs, final verification

**Status:** [ ] pending
**Complexity:** M

## Objective

- Convert every form to a real PDF with LibreOffice and look at it: A5 landscape for 01-TT/02-TT, A4 portrait with a repeated header for 01-VT/02-VT, one voucher per page, Vietnamese text, nothing cut off.
- Write the go-live checklist for the accountant.
- Update the project docs and run the final verification.

Runs after Phases 01–04 are on the branch (Task 5.0 merges the frontend track of a parallel run). No production code changes except fixes found by the checks (each fix starts with a failing test).

## Files

- `docs/plans/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md` (new, Vietnamese)
- `docs/plans/261014-1632-tt133-voucher-print-forms/EXECUTION-REPORT.md` (new)
- `docs/plans/261014-1632-tt133-voucher-print-forms/SUMMARY.md` and the phase files (status marks)
- `docs/SUMMARY.md`, `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/architecture/system-architecture.md`, `docs/project-pdr/product-goals.md` (modify)
- `docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md` (modify — status and next steps)

## Tasks

### Task 5.0 — Merge the frontend track (parallel runs only)

Skip this task when Phases 02 and 04 ran in the first worktree (Phase 00 Task 0.3 recorded the choice).

1. In `.claude/worktrees/tt133-voucher-print-fe`: `git status --short` is clean and Phase 04 is marked complete.
2. In `.claude/worktrees/tt133-voucher-print`: `git merge --no-ff feat/tt133-voucher-print-forms-fe -m "Merge the frontend track of the voucher print forms"`. Only the plan files can conflict (status marks of both tracks): keep every `[x]`.
3. From `frontend/` of the first worktree: `npm run typecheck`, `npm run lint`, `npm run test -- --testTimeout=30000`, `npm run build`, then `git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo`; compare with `BASELINE.md`.
4. Remove the frontend worktree: delete only the junction first (PowerShell `(Get-Item "D:\Projects\QLBanHang\.claude\worktrees\tt133-voucher-print-fe\frontend\node_modules").Delete()`), check that `D:\Projects\QLBanHang\frontend\node_modules` still has its packages, then `git worktree remove .claude/worktrees/tt133-voucher-print-fe` and `git branch -d feat/tt133-voucher-print-forms-fe`.

### Task 5.1 — Real PDF output

1. Write a **temporary** test `backend/tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/VoucherPrintSamplesTests.cs` (inherits `CashDebtTestBase`, `[Collection(nameof(PostgresCollection))]`; deleted at the end of this task, never committed). With the company profile filled in (name, address, the two signers) it creates through the API and saves as xlsx into `Path.Combine(Path.GetTempPath(), "voucher-print-samples")`:
   - `01-TT.xlsx`: a receipt with a partner, a person, two lines and `AttachedDocs = "2"`;
   - `02-TT-long.xlsx`: a payment whose `Description` is about 400 characters (wrapping on A5);
   - `01-TT-cancelled.xlsx`: a cancelled receipt;
   - `01-TT-batch.xlsx`: three receipts through `POST /api/cash-vouchers/print`;
   - `01-VT-voucher.xlsx` and `01-VT-cost.xlsx`: a stock-in with a line discount, an order discount, VAT and freight, in `Voucher` and `Cost` mode;
   - `02-VT-vat-inside.xlsx`: a sale with a `PriceIncludesVat` line;
   - `01-VT-40-lines.xlsx`: a stock-in with 40 lines (page break, repeated header);
   - `01-VT-spec-warehouses.xlsx`: a stock-in with an m² line (sheets and dimensions) and a line in a second warehouse;
   - `02-VT-none.xlsx`: a stock-out in `None` mode.
   Set up the data the guards accept (Executor conventions, "Test data the guards refuse"): money in the fund before the payment, stock before each stock-out. Give one 02-VT sample a long preparer name (e.g. an owner whose full name is "Nguyễn Văn Trường Giang") to check the signer blocks.
   Run it: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherPrintSamplesTests"`.
2. Convert them with the local LibreOffice, on a profile of its own:
   ```bash
   cd "$TEMP/voucher-print-samples"
   "/c/Program Files/LibreOffice/program/soffice.exe" "-env:UserInstallation=file:///$(cygpath -m "$TEMP")/lo-vprint" \
     --headless --convert-to pdf --outdir . *.xlsx
   for f in *.pdf; do echo "$f"; grep -a -o "MediaBox \[[^]]*\]" "$f" | sort | uniq -c; done
   ```
   Expected page sizes: 01-TT / 02-TT `[0 0 595.3 419.5]` (A5 landscape, ±1 pt), 01-VT / 02-VT `[0 0 595.3 841.9]` (A4 portrait); `01-TT-batch.pdf` has three A5 pages; `01-TT.pdf`, `02-TT-long.pdf` and `01-TT-cancelled.pdf` have one page each; `01-VT-40-lines.pdf` has more than one page.
   If Docker Desktop runs, repeat the conversion in the backend image, which prints numbers the vi_VN way: `(cd backend && docker build -t qlbh-backend .)` then `docker run --rm -v "$TEMP/voucher-print-samples:/work" --entrypoint sh qlbh-backend -c "cd /work && soffice-vi --headless --convert-to pdf --outdir /work/docker *.xlsx"`. If Docker is not available, say so in the report.
3. Look at every PDF (Read tool with `pages`): Vietnamese diacritics in Times New Roman; the unit and form-code blocks side by side; the title block with "Số", "Nợ", "Có" beside the title; long texts wrap inside the page; the signature titles, hints and names line up and none is cut (on 02-VT "Người lập phiếu" and its name are not squeezed into the STT column); "ĐÃ HỦY" under the title; on 01-VT/02-VT the table fits the page width, the sub-headers "Theo chứng từ" / "Yêu cầu" are not cut, the header rows repeat on page 2, the totals rows and the VAT line sit under the table; the spec text and the `Kho` column are readable.
4. A defect is fixed in `VoucherFormBuilder`, `CashVoucherForm` or `StockVoucherForm` with a failing unit test first (an assertion on a width, a merge, a row height or a page-setup property), then re-converted.
5. Delete `VoucherPrintSamplesTests.cs`; keep `$TEMP/voucher-print-samples/` for the user and list the files in the report. Commit only fixes, one per defect.

### Task 5.2 — Go-live checklist

1. Write `GO-LIVE-CHECKLIST.md` in the plan folder, in Vietnamese, for the accountant and the PO:
   - **Câu hỏi cần trả lời** — the six accountant/PO questions of the brainstorm (bank funds printed as 01-TT/02-TT or not; empty Nợ, Có, Quyển số; "Số lượng theo chứng từ / yêu cầu" equal to the actual quantity or left blank; the extra rows Chiết khấu, Thuế GTGT, Cước and the "Trong đó thuế GTGT đã gồm trong giá" line; the spec text for m, m², m³ goods; several warehouses on one voucher), each with what changes in the software for either answer.
   - **Duyệt bản in từng mẫu** — one row per form and mode (01-TT, 02-TT, 01-VT and 02-VT in Giá trên phiếu / Giá vốn / Không in tiền): the sample file of Task 5.1, "đối chiếu với Phụ lục 3 TT133 bản gốc", the known deviations (Nợ, Có, Quyển số as dotted lines; no foreign-currency lines; extra total rows; spec in the name column; `Kho` column; "(Hoặc bộ phận có nhu cầu nhập)" not printed under Kế toán trưởng; the date above the signatures is the voucher date), and a sign-off cell.
   - **Thiết lập trước khi dùng** — Thông tin đơn vị filled in (name, address, Giám đốc, Kế toán trưởng); who may print at cost (`inventory.view_cost`); copies are set in the printer dialog; the 300-voucher limit per file.
2. Commit: `git add docs/plans/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md && git commit -m "docs(plan): voucher print forms go-live checklist"`

### Task 5.3 — Project documentation

1. `docs/code-standard/conventions.md`, section **TT133 Report Forms** — add the voucher rules:
   - voucher prints go through `IVoucherPrintService` (`Application/Tt133/Vouchers`), share `Tt133ExportSupport` with the report exports (context, `EnsureSheetCount` with the caller's message, `ToFileAsync`), load everything in one snapshot and render after it;
   - forms in `Infrastructure/Excel/Tt133/Vouchers` on `VoucherFormBuilder`, which wraps `Tt133FormBuilder` for the table; the pure rules (fallbacks, "Kèm theo", spec text, total rows) live in `VoucherPrintRules`;
   - routes `GET api/{cash|stock}-vouchers/{id}/print` and `POST api/{cash|stock}-vouchers/print` (body with `ids`, `priceMode` for stock); a POST on a `[DashboardSignal]` controller that changes nothing carries `[SkipDashboardSignal]`;
   - `Voucher` mode rows must add up to the voucher total (added VAT from the stored totals); `Cost` reads the stock ledger row of each line, never a value recomputed from the voucher, through the `(SourceType, SourceId)` index (there is none on `SourceLineId`);
   - `VoucherFormBuilder` splits signer blocks by column width and gives wrapped rows an explicit height (LibreOffice keeps the default height otherwise);
   - frontend: `VoucherPrintButton`, `useVoucherPrint` (PDF tab opened before the request), `requestBlob` / `saveBlob` for every file download, `useRowSelection` + `SelectionCheckbox` for list selection, `StockPriceModeDialog`; a clickable list row that holds menus or dialogs navigates only for clicks inside its own DOM (`e.currentTarget.contains(e.target as Node)`), because React events bubble out of portals.
2. `docs/codebase/directory-structure.md`: the "TT133 report forms" module row gets `Application/Tt133/Vouchers`, `Tt133ExportSupport` and `Infrastructure/Excel/Tt133/Vouchers`; the API controller rows of `CashVouchersController` and `StockVouchersController` get the print routes; Frontend Structure gets `components/voucher-print/`, `features/voucher-print/`, `components/ui/selection-checkbox.tsx`, `components/tt133-export/company-profile-hint.tsx`, `lib/blob-download.ts`, `lib/pdf-tab.ts`, `lib/use-row-selection.ts`.
3. `docs/architecture/system-architecture.md`, section **TT133 Report Forms** — a paragraph "Voucher print forms": the four forms and their paper, the flow (controller → `VoucherPrintService` → one snapshot → `VoucherFormRenderer` → optional LibreOffice), the access rules (view permission of the type, working branch, `inventory.view_cost` for Cost), the three price modes, the batch limit.
4. `docs/project-pdr/product-goals.md` — in Round 3, mark the voucher print/Excel as delivered (link this plan); in **Mẫu biểu TT133**, add the voucher rules users see (A5 / A4, one voucher per page, the three price modes and who may use Giá vốn, cancelled vouchers print with "ĐÃ HỦY", printing from the list).
5. `docs/SUMMARY.md` — update the descriptions of the four files above so they mention the voucher prints.
6. `docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md` — status "đã hiện thực" with a link to this plan; mark the next steps done; replace the technical open question on A5 with the result of Task 5.1.
7. Check every statement against the code (`grep` for routes, class names, the storage key, the error messages) before committing.
8. Commit: `git add docs/SUMMARY.md docs/code-standard/conventions.md docs/codebase/directory-structure.md docs/architecture/system-architecture.md docs/project-pdr/product-goals.md docs/brainstorms/261013-2042-tt133-voucher-print-forms/SUMMARY.md && git commit -m "docs: TT133 voucher print forms"`

### Task 5.4 — Final verification and execution report

1. Run the Final Verification block of `SUMMARY.md`; compare the backend failures with `BASELINE.md` test by test, and the frontend failures and lint counts.
2. Manual smoke test on a dev stack started from the worktree (backend `dotnet run --project src/OrderMgmt.WebApi`, frontend `npm run dev`, login `admin` / `Admin@123`; PDF needs `QuotationExport:LibreOfficePath`, set in `appsettings.Development.json`). If the main checkout's dev stack holds the ports, record "not run" with the reason instead.
   1. **Phiếu thu**: open a saved receipt → "In" → "In (PDF)": a new tab shows the A5 voucher; "Excel" downloads `{số phiếu}.xlsx`. Change a field without saving: "In" is disabled with the title "Lưu thay đổi trước khi in".
   2. **Danh sách phiếu thu**: tick three rows → "Đã chọn 3 phiếu" → "In" → one PDF with three pages; go to the next page → the bar is gone.
   3. **Phiếu nhập kho**: "In" → "Excel" → the dialog "Chọn giá in" → "Giá vốn" → the file shows the cost of each tracked line; reopen the dialog → "Giá vốn" is preselected.
   4. Log in as a user without `inventory.view_cost` (e.g. WAREHOUSE): the dialog has no "Giá vốn".
   4a. **Danh sách phiếu xuất kho**: open a row's print menu → "In (PDF)" → the dialog opens; click outside it (on the overlay) → the dialog closes and the page stays on the list (the voucher does not open).
   5. Clear the company name: the print button shows "Chưa có thông tin đơn vị"; printing still works.
3. Write `EXECUTION-REPORT.md` in the format of `docs/plans/archived/261013-1926-tt133-report-forms/EXECUTION-REPORT.md`: outcome against the baseline, commits per phase, the page sizes and findings of Task 5.1, deviations from the plan, whether the smoke test ran, the open accountant questions, follow-ups.
4. Mark every phase `[x] complete` in its file and in `SUMMARY.md`.
5. Commit: `git add docs/plans/261014-1632-tt133-voucher-print-forms && git commit -m "chore(plan): TT133 voucher print forms complete"`

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"
cd backend && dotnet build OrderMgmt.sln && dotnet test OrderMgmt.sln --logger "trx;LogFileName=final.trx" --results-directory "$TEMP/vprint-trx"
cd ../frontend && npm run typecheck && npm run lint && npm run test -- --testTimeout=30000 && npm run build
git checkout -- tsconfig.app.tsbuildinfo tsconfig.node.tsbuildinfo
git status --short   # no VoucherPrintSamplesTests.cs, no sample files in the repository
```

## Exit Criteria

- Every form was converted to PDF with LibreOffice and looked at; the page sizes match V12; defects found were fixed with a test.
- `GO-LIVE-CHECKLIST.md` lists the accountant's questions, the per-form sign-off and the settings.
- The five docs and the brainstorm describe the feature and match the code.
- The full suites show no failure beyond `BASELINE.md`; `EXECUTION-REPORT.md` is written and every phase is marked complete.
