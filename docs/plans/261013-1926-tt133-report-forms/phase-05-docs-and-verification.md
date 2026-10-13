# Phase 05 — Measurements, docs, final verification

**Status:** [-] in progress (open decisions, see EXECUTION-REPORT.md)
**Complexity:** M

## Objective

- Measure the two recorded risks on real output: bulk export time of the debt and the stock forms (R1) and concurrent LibreOffice conversions (R2), and set `Tt133Export:MaxSheets` and the conversion timeout from the numbers.
- Produce a printed sample of every form and the go-live checklist for the accountant.
- Update the project docs and run the final verification.

No production code changes in this phase except a configuration value from Task 5.1 and fixes found by the checks (each fix starts with a failing test).

## Files

- `backend/src/OrderMgmt.WebApi/appsettings.json` (modify only if Task 5.1 changes `MaxSheets` or `ConversionTimeoutSeconds`)
- `docs/plans/261013-1926-tt133-report-forms/GO-LIVE-CHECKLIST.md` (new, Vietnamese, for the accountant and the PO)
- `docs/plans/261013-1926-tt133-report-forms/EXECUTION-REPORT.md` (new)
- `docs/plans/261013-1926-tt133-report-forms/SUMMARY.md` and the phase files (status marks)
- `docs/SUMMARY.md`, `docs/code-standard/conventions.md`, `docs/codebase/directory-structure.md`, `docs/architecture/system-architecture.md`, `docs/project-pdr/product-goals.md` (modify)
- `docs/brainstorms/261012-2237-tt133-report-forms/SUMMARY.md` (modify — "Next steps" and the technical open question)

## Tasks

### Task 5.1 — Bulk export timing (R1)

1. Write a **temporary** test `backend/tests/OrderMgmt.IntegrationTests/Tt133/BulkExportTimingTests.cs` (inherits `CashDebtTestBase`; it is deleted at the end of this task and never committed):
   - through `InDbAsync`, insert 300 customers and, for each, 20 `DebtEntry` rows in the main branch (side Receivable, alternating Increase 1,000 / Decrease 400, `PostedAt` spread over October 2026, `SourceType = Opening` for the first row and `StockOut` for the others with a random `SourceId`, `DocCode` `"T{n}"`);
   - time `POST /api/reports/debt-ledger/export` with the body `{ side: Receivable, from: 2026-10-01, to: 2026-10-31, all: true }` with a `Stopwatch` (xlsx), for 100 and for 300 partners (the 100 case with `MaxSheets` unchanged and only 100 customers seeded first);
   - write the response of the 300 case to `%TEMP%/tt133-bulk-300.xlsx` and print the timings with `ITestOutputHelper`;
   - a second test in the same file (inherits nothing more: `CashDebtTestBase` is an `InventoryTestBase`) for the stock forms, which share `MaxSheets` and cost more queries per sheet: through `InDbAsync`, insert 300 tracked products (copy the columns of a product made by `CreateInventoryProductAsync`) and, for each, 20 `InventoryLedgerEntry` rows in the main branch's default warehouse (`SourceType = StockIn` / `StockOut` alternating, `QtyIn` 10 / `QtyOut` 4, `InValue` 100,000 on inbound rows, `UnitCost` 10,000 and `CostAmount` 40,000 on outbound rows, `PostedAt` spread over October 2026, a random `SourceId`, `SourceCode` `"T{n}"`, `RunningQty` kept consistent); time `POST /api/reports/stock-card/export` and `POST /api/reports/stock-detail/export` with `{ from: 2026-10-01, to: 2026-10-31, all: true }` (xlsx) and write the S06 response to `%TEMP%/tt133-bulk-s06-300.xlsx`.
   - The inserted rows point at vouchers that do not exist (`DebtEntry` is unique per source, so real vouchers cannot be shared): the reason lookups run but find nothing. Say so next to the numbers in `EXECUTION-REPORT.md`.
2. Run it: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~BulkExportTimingTests" --logger "console;verbosity=detailed"`.
3. Build the backend image (Task 5.2 reuses it) and time the PDF conversion of the two 300-sheet files inside it:
   ```bash
   (cd backend && docker build -t qlbh-backend .)
   docker run --rm -v "$TEMP:/work" --entrypoint sh qlbh-backend -c "cd /work && time soffice --headless --convert-to pdf --outdir /work /work/tt133-bulk-300.xlsx && time soffice --headless --convert-to pdf --outdir /work /work/tt133-bulk-s06-300.xlsx"
   ```
4. Decide from the numbers and record them in `EXECUTION-REPORT.md`. `MaxSheets` is one value for every form, so the slowest of S12, S08 and S06 decides:
   - if xlsx for 300 sheets takes more than 20 s, or xlsx + PDF more than 90 s (the client timeout is 120 s and a reverse proxy usually cuts at 60–120 s), lower `Tt133Export:MaxSheets` in `appsettings.json` to the largest tested size that stays inside those limits;
   - set `QuotationExport:ConversionTimeoutSeconds` to at least twice the measured conversion time of the kept `MaxSheets` (never below the current 60);
   - if even 100 sheets exceed the limits, **stop and ask the user** (batch queries were rejected in review S2, so this is a new decision).
5. Delete `BulkExportTimingTests.cs`. Commit only a configuration change: `git add backend/src/OrderMgmt.WebApi/appsettings.json && git commit -m "chore(tt133): export limits from measurements"`.

### Task 5.2 — Real PDF output and concurrent conversions in Docker (R2)

1. Start the backend image of Task 5.1 step 3 (build it now if that step was skipped: `(cd backend && docker build -t qlbh-backend .)`) against a throwaway database (never `qldonhang_test` or `qldonhang_integtest`):
   ```bash
   docker run -d --name qlbh-tt133 -p 8088:8080 \
     -e ConnectionStrings__Default="Host=host.docker.internal;Port=5432;Database=qldonhang_tt133check;Username=postgres;Password=1" \
     -e Jwt__Secret="tt133-check-secret-0123456789-0123456789" \
     -e Jwt__Issuer="qlbh" -e Jwt__Audience="qlbh" \
     -e Seed__AdminPassword="Admin@123" \
     -e Database__AutoMigrateAndSeed=true \
     qlbh-backend
   ```
   If a required configuration key is missing, the container log names it; add it and restart.
2. Log in and keep the token:
   ```bash
   TOKEN=$(curl -s -X POST http://localhost:8088/api/auth/login -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"Admin@123"}' | python -c "import sys,json; print(json.load(sys.stdin)['data']['accessToken'])")
   ```
3. Enter sample data through the frontend pointed at this backend (`VITE_API_BASE_URL=http://localhost:8088/api npm run dev`) or through the API: the company profile; one customer with an opening debt, a sale and a receipt; one supplier with a purchase and a payment; a partner that is both; a bank fund with bank name and account number and a fund transfer; a product with opening stock, a stock-in and a stock-out; `CostingScope = Warehouse` in Cấu hình kho.
4. Export every form as xlsx and pdf into a folder outside the repository (`$TEMP/tt133-samples/`), for example:
   ```bash
   curl -s -H "Authorization: Bearer $TOKEN" -o "$TEMP/tt133-samples/S04a.pdf" \
     "http://localhost:8088/api/reports/cash-book/export?kind=Cash&from=2026-10-01&to=2026-10-31&format=pdf"
   ```
   and the same for `cash-book` (`kind=Bank`), `debt-balance`, `debt-reconciliation` and `stock-summary`. The three multi-target forms are POST requests with a JSON body:
   ```bash
   curl -s -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -o "$TEMP/tt133-samples/S12.pdf" \
     -d '{"side":"Receivable","from":"2026-10-01","to":"2026-10-31","all":true}' \
     "http://localhost:8088/api/reports/debt-ledger/export?format=pdf"
   ```
   (`debt-ledger` for both sides; `stock-card` and `stock-detail` with the body `{"from":"2026-10-01","to":"2026-10-31","all":true}`).
5. Check each PDF by eye: Vietnamese text in Times New Roman (no fallback font, no missing diacritics); the table fits the page width; the header rows repeat on the second page (use the bulk file of Task 5.1); one sheet starts on a new page; the signature block is not split from its titles on single-page forms; negative numbers in parentheses; dates `dd/MM/yyyy`. A layout defect is fixed in the builder or the form with a failing unit test first (for example an assertion on a width, a merge or a page-setup property), then re-exported.
6. Concurrency: run three PDF exports at the same time, three times in a row:
   ```bash
   for round in 1 2 3; do
     for i in 1 2 3; do
       curl -s -X POST -o "$TEMP/tt133-samples/par-$round-$i.pdf" -w "%{http_code} %{time_total}\n" \
         -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
         -d '{"side":"Receivable","from":"2026-10-01","to":"2026-10-31","all":true}' \
         "http://localhost:8088/api/reports/debt-ledger/export?format=pdf" &
     done; wait
   done
   head -c 5 "$TEMP"/tt133-samples/par-*.pdf
   ```
   Expected: nine lines `200 …` and nine files starting with `%PDF-`.
7. If any request fails, hangs past the timeout or returns 503: record the output in `EXECUTION-REPORT.md`, then **stop and ask the user** which fix to take (a profile directory per conversion via `-env:UserInstallation`, a semaphore, or both — review S3 was rejected, so it needs a decision). Do not implement one unasked.
8. Clean up: `docker rm -f qlbh-tt133`, and `psql -h localhost -U postgres -c "drop database qldonhang_tt133check"`. Keep `$TEMP/tt133-samples/` for the user.

### Task 5.3 — Go-live checklist for the accountant

1. Write `GO-LIVE-CHECKLIST.md` in the plan folder, in Vietnamese, for the accountant and the PO. Sections:
   - **Câu hỏi cần trả lời** — the five accountant/PO questions of the brainstorm (TT133 or TT99; "Lý do" instead of "TK đối ứng" and "Hạn thanh toán" instead of "Thời hạn được chiết khấu"; a company reconciliation template with its "căn cứ" and reply clauses; branches without their own tax code; `CostingScope = Warehouse`), each with what changes in the software for either answer.
   - **Duyệt bản in từng biểu** — one table row per form (S04a, S05, S06, S07, S08, S12, Bảng tổng hợp, Biên bản đối chiếu): the sample file name, "đối chiếu với Phụ lục 4 TT133 bản gốc", the known deviations (columns "TK đối ứng" → "Lý do"; S07 with quantity columns; S07 and the debt summary with the common three-signature footer; opening stock shown under "Nhập trong kỳ" in the first period, T19; A = B dates on S04a; the `[mã chi nhánh]` prefix of "Diễn giải" on company-wide S12 and reconciliation records, T21; the "Điều kiện lọc" line on a summary exported with a search text, T22), and a sign-off cell.
   - **Thiết lập trước khi dùng** — Thông tin đơn vị filled in; `CostingScope = Warehouse` (the recalculation it triggers and its effect on average cost per warehouse); who may edit Thông tin đơn vị (`settings.company`: ADMIN, MANAGER and ACCOUNTANT by default); `branches.access_all` for whoever prints company-wide books; the measured export limits of Task 5.1.
2. Commit: `git add docs/plans/261013-1926-tt133-report-forms/GO-LIVE-CHECKLIST.md && git commit -m "docs(plan): TT133 go-live checklist"`

### Task 5.4 — Project documentation

1. `docs/code-standard/conventions.md` — add a section **TT133 Report Forms** with these rules:
   - a form is a presentation of an existing report DTO and never queries the database itself; a new number goes into the report service first, so the file always equals the screen;
   - one class per form in `Infrastructure/Excel/Tt133/`, built with `Tt133FormBuilder` (header block, title, two-tier header with the symbol row, summary rows, book footer, page setup); column tables live in the form class;
   - exports go through `ITt133ExportService` (one snapshot for the data of an export, rendering and PDF conversion after it, `EnsureSheetCount`, `ToFileAsync`) and a literal route `api/reports/{report}/export` in `Tt133ExportController` carrying the source report's `[HasPermission]` — GET with the screen report's query for single-document forms, POST with a JSON body for multi-target forms (`debt-ledger`, `stock-card`, `stock-detail`);
   - errors: 422 `EXPORT_NO_TARGETS` / `EXPORT_TOO_MANY_SHEETS` (`UnprocessableException`, not confirmable), 503 `PDF_EXPORT_FAILED` (`ServiceUnavailableException`);
   - limit: `Tt133Export:MaxSheets`, applied to explicit target lists and to "all with activity";
   - `ReportScope` and `ReportScopeGuard`: `Company` drops the branch filter, needs `branches.access_all`, and is offered only on debt reports and the stock summary;
   - a printed form never hides a filter: a search text is printed as a "Điều kiện lọc" line, and company-scope ledger rows carry `BranchCode`, printed as `[code]` in front of "Diễn giải", because document numbers repeat across branches;
   - PDF conversion uses `ISpreadsheetPdfConverter`; tests replace it with a fake; the LibreOffice converter kills its process tree when the wait is cancelled;
   - frontend: `Tt133ExportButton` with `Tt133ExportForm` entries, `useTt133Download`, `ExportTargetsDialog` for multi-target forms, `ReportScopeSelect`, `PeriodPresetSelect`; file names from `tt133FileBase`; downloads pass an explicit timeout and unwrap blob errors with `unwrapBlobError`.
2. `docs/codebase/directory-structure.md`:
   - Backend Modules: rows for "Company profile" (`Domain/Entities/Organization/CompanyProfile.cs`, `Application/Organization/Company`, `CompanyProfileController.cs`) and "TT133 report forms" (`Application/Tt133`, `Infrastructure/Excel/Tt133`, `Tt133ExportController.cs`); mention `ISpreadsheetPdfConverter`, `VndAmountInWords` and `ReportScope` in the cross-feature helpers row; add `stock-summary` to the inventory reports row.
   - API Controllers: `CompanyProfileController` (`/api/settings/company`), `Tt133ExportController` (the seven export routes), and `stock-summary` on `InventoryReportsController`.
   - Frontend Structure: `components/tt133-export/`, `components/report-scope-select/`, `components/period-preset-select/`, `features/company-profile/`, `features/tt133-export/`, the stock summary page under `pages/inventory/`, the company profile page under `pages/settings/`, `lib/report-scope.ts`, `lib/period-presets.ts`, `lib/blob-error.ts`.
   - Required Production Configuration: `Tt133Export__MaxSheets` (optional) and a note that `QuotationExport__LibreOfficePath` and `QuotationExport__ConversionTimeoutSeconds` also serve the TT133 PDF exports.
3. `docs/architecture/system-architecture.md` — in the dashboard/report/export part, add the TT133 export flow (controller → export service → report services inside one snapshot → form renderer → optional LibreOffice conversion), the scope rule, and `settings.company` in the permission matrix.
4. `docs/project-pdr/product-goals.md` — move "Print/Excel of cash, stock and debt reports" from planned to current: the eight forms, Thông tin đơn vị, Tổng hợp nhập–xuất–tồn, branch/company scope; keep the non-goals (chart of accounts, TT99, foreign currency, editable templates).
5. `docs/SUMMARY.md` — update the descriptions of the four files above so they mention the TT133 forms; the plan row was added in Phase 00.
6. `docs/brainstorms/261012-2237-tt133-report-forms/SUMMARY.md` — in "Next steps", mark steps 1, 3 and 4 as done with a link to this plan, and replace the technical open question with the measured limits of Task 5.1.
7. Check every statement against the code (routes, class names, option keys, permission code) with `grep` before committing.
8. Commit: `git add docs/SUMMARY.md docs/code-standard/conventions.md docs/codebase/directory-structure.md docs/architecture/system-architecture.md docs/project-pdr/product-goals.md docs/brainstorms/261012-2237-tt133-report-forms/SUMMARY.md && git commit -m "docs: TT133 report forms"`

### Task 5.5 — Final verification and execution report

1. Run the Final Verification block of `SUMMARY.md`; compare the backend failures with `BASELINE.md` test by test, and the frontend failures and lint counts.
2. Manual smoke test on the dev stack (`docker compose up -d`, backend `dotnet run --project src/OrderMgmt.WebApi`, frontend `npm run dev`, login `admin` / `Admin@123`). PDF needs LibreOffice: without it, step 3 expects the 503 toast.
   1. Cài đặt → **Thông tin đơn vị**: save name, address, tax code and the two signers.
   2. Báo cáo → **Sổ quỹ**: the "Lý do" column shows; "Xuất theo mẫu TT133" offers S04a and S05; the Excel file opens with one sheet per fund and the closing equals the screen.
   3. The same export as PDF: a PDF downloads, or the toast says "Xuất PDF thất bại, hãy dùng Excel."
   4. **Sổ chi tiết công nợ**: columns "Lý do" and "Hạn TT"; export S12 for the current partner, then "Tất cả có phát sinh trong kỳ" → one sheet per partner.
   5. As admin, choose "Toàn công ty" on **Số dư công nợ**: the numbers include the other branch; the exported header prints the company name without a branch. Type a search text and export again: the file has the "Điều kiện lọc" line. On **Sổ chi tiết công nợ** with "Toàn công ty", each row shows its branch code in brackets, on the screen and in S12.
   6. **Biên bản đối chiếu** for a partner on both sides: the file ends with the conclusion in digits and words.
   7. Báo cáo → **Tổng hợp nhập xuất tồn**: opening + in − out = closing on each row; the closing equals **Tồn kho** at the end date; export S07.
   8. **Thẻ kho**: export S08; with the cost permission also S06; a user without `inventory.view_cost` sees only S08.
   9. Clear the company name: every export button shows "Chưa có thông tin đơn vị" and the export still works with an empty header.
   10. Log in as ACCOUNTANT: no scope select (no `branches.access_all`); the Thông tin đơn vị card is there and saving works (on a database seeded before this release the role receives `settings.company` at the first start); the debt and cash exports work.
3. Write `EXECUTION-REPORT.md` in the format of `docs/plans/archived/261012-1709-cash-debt-round2b/EXECUTION-REPORT.md`: outcome table against the baseline, commits per phase, the measurements of Tasks 5.1 and 5.2, deviations, whether the smoke test was run, the open accountant questions, follow-ups.
4. Mark every phase `[x] complete` in its file and in `SUMMARY.md`.
5. Commit: `git add docs/plans/261013-1926-tt133-report-forms && git commit -m "chore(plan): TT133 report forms complete"`

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend && dotnet build OrderMgmt.sln && dotnet test OrderMgmt.sln
cd ../frontend && npm run typecheck && npm run lint && npm run test && npm run build
git status --short   # no untracked BulkExportTimingTests.cs, no sample files inside the repository
```

## Exit Criteria

- Bulk export timings are recorded and `MaxSheets` / the conversion timeout match them.
- Every form was exported as a real PDF from the Docker image and looked at; nine parallel PDF exports succeeded, or the failure was reported to the user and a decision recorded.
- `GO-LIVE-CHECKLIST.md` lists the accountant's questions, the per-form sign-off and the settings to make.
- The five docs describe the feature and match the code.
- The full suites show no failure beyond `BASELINE.md`; `EXECUTION-REPORT.md` is written and every phase is marked complete.
