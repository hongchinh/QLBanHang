# Execution Report — TT133 report forms

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/tt133-report-forms` (from `qlbanhang/main` `a76ce85`, not pushed) · Executed 2026-10-13 → 2026-10-14 · Baseline: [BASELINE.md](BASELINE.md) · Go-live gate: [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)

## Outcome

Phases 00–04 are complete. Phase 05 is **left in progress**: its measurements, docs and final suites are done, but three of its steps wait for a user decision (see "Open decisions"), and the browser smoke test was not run.

The branch delivers the eight forms (S04a, S05, S06, S07, S08, S12, debt summary, reconciliation record) as Excel and PDF, the company profile with its settings page, the stock in–out–balance report and page, the branch/company scope of the debt reports and S07, the reason, due-date and branch-code fields, and `VndAmountInWords`.

| Check | Baseline (Phase 00) | Final (`e49eacb`) | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors (9 warnings) | ✅ |
| `dotnet test OrderMgmt.sln` | 19 failed / 841 total | 19 failed / 972 total (953 passed, 19 m 51 s) | ✅ the same 19 tests, compared by name |
| `npm run typecheck` | exit 0 | exit 0 | ✅ |
| `npm run lint` | 3 errors, 14 warnings | 3 errors, 14 warnings | ✅ same as baseline |
| `npm run test` | 3 failed / 948 total | 3 failed / 1035 total (1032 passed) | ✅ the same 3 tests |
| `npm run build` | passes | passes | ✅ |

The suites grew by 131 backend tests and 87 frontend tests. The change is 142 files, +13,486 / −135 lines against `a76ce85`.

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| 00 Branch and baseline | `f441baf`, `e3566f7` | `e3566f7` records BASELINE.md |
| 01 Company profile, amount in words, converter | `3acd2d8` → `609227a` (marker `bf35dd2`) | 6 tasks; migration `AddCompanyProfile` |
| 02 Form builder, export pipeline, S04a, S05 | `d4ec7cb` → `a61c3c8` (marker `8651c1a`) | 9 tasks; builder and export service frozen here |
| 03 Debt scope, S12, debt summary, reconciliation | `c83c4ec` → `9625104` (marker `09493a3`) | 9 tasks |
| 04 Stock summary, S07, S08, S06 | `5644581` → `3b818d2`, fix `80cdde5` (marker `3a83a4f`) | 7 tasks; one review finding fixed |
| 05 Measurements, docs, verification | `c6cfd7b`, `bfc425e`, `d94cf24`, `1499df0`, `e49eacb`, this report | two layout fixes from the PDF check |

## How it was run

- One workflow per phase, in place on the branch. Each task: a sonnet implementer (test first, RED line reported), then a spec-compliance review and an adversarial correctness review, with a fix loop. Each phase ended with a completeness critic and a checkpoint that ran the full suites and compared the failures with BASELINE.md by name.
- 31 implementation tasks. 30 passed both reviews on the first round. Task 4.7 needed one fix: the product suggestion list could not be clicked with the mouse inside the export target dialog (`80cdde5`, with a regression test in `product-typeahead-cell.test.tsx`).
- Phase 05 docs and the checklist were fact-checked against the code by two reviewers; no false statement was found.

## Measurements (Task 5.1, R1)

Conditions: 20 rows per sheet, October 2026, `all: true`, in-process test server (Debug build), local PostgreSQL. PDF timed with `soffice --headless --convert-to pdf` inside the `qlbh-backend` image (LibreOffice 7.4.7.2, 8 CPUs). The inserted rows point at vouchers that do not exist, so the reason lookups ran but found nothing.

| Form | Sheets | xlsx | PDF conversion | Total |
|---|---|---|---|---|
| S12 | 100 | 2.28 s | 9.75 s | 12.0 s |
| S12 | 300 | 2.57 s | 28.47 s | 31.0 s |
| S08 | 100 | 1.16 s | 6.84 s | 8.0 s |
| S08 | 300 | 3.37 s | 19.48 s | 22.9 s |
| S06 | 100 | 0.96 s | 20.71 s | 21.7 s |
| S06 | 300 | 3.13 s | 61.49 s | 64.6 s |

- All forms stay inside the plan's limits at 300 sheets (20 s for xlsx, 90 s for xlsx + PDF), so `Tt133Export:MaxSheets` stays **300**.
- `QuotationExport:ConversionTimeoutSeconds` went from 60 to **130** (`c6cfd7b`): the S06 conversion alone takes 61 s, longer than the old timeout, and the plan asks for at least twice the measured time.
- S06 prints two pages per sheet at 20 rows (600 pages for 300 sheets), which is why it converts twice as slowly.

## Real PDF output (Task 5.2, R2)

- The 13 sample workbooks and their PDFs are in `%TEMP%\tt133-samples\` (page images in `png\`). The workbooks came from a temporary in-process test and the PDFs from `soffice` run directly in the image, **not** from the container's API (see open decision 1).
- Every PDF was looked at. Fonts are Times New Roman only, with diacritics intact; every form fits the page width; negatives print in parentheses; dates are `dd/MM/yyyy`; each sheet starts a new page; the signature block stays together.
- Two layout defects were fixed, each with a failing unit test first:
  - `bfc425e` — wrapped text printed over the neighbouring rows, and the reconciliation conclusion was cut at the page edge. The builder now sets row heights from an estimate of the line count; some rows get one spare line.
  - `d94cf24` — page 2 had no header rows, because LibreOffice needs absolute print titles (`$9:$11`). The builder rewrites `xl/workbook.xml` after ClosedXML saves.
- Concurrent conversions (substitute for step 6): three parallel `soffice` processes, three rounds, three containers. **7 of 27 failed** (exit code 1, no PDF, no hang). With one profile directory per conversion, 9 of 9 succeeded.

## Open decisions (for the user)

1. **Container checks over HTTP were not run.** The project settings deny `curl`, `wget` and `Invoke-WebRequest`, so Task 5.2 steps 2, 3, 4 and 6 and the API parts of the smoke test were not carried out against the running container. Options: allow `curl` to localhost and re-run the task; accept the in-process exports plus the direct `soffice` runs as the evidence; or run the nine-request loop of step 6 by hand.
2. **R2: concurrent PDF conversions fail about one time in three.** In the application that is a 503 `PDF_EXPORT_FAILED` for that user. The plan's options: a profile directory per conversion (`-env:UserInstallation`), a semaphore, or both. A semaphore alone queues requests behind a 61 s S06 conversion, which can pass the 120 s client timeout. Nothing was implemented, because review S3 rejected both and the plan asks for a decision.
3. **Number separators in PDFs.** Table cells print `1,250,750,000` (the image's LibreOffice runs with an English locale) while the reconciliation sentence prints `368.775.000 đồng`. Excel on a Vietnamese Windows shows dots from the same file. Options: a Vietnamese locale in the image, a locale tag in the number formats, or accept commas in PDFs.
4. **Conversion timeout of 130 s** is longer than the 120 s client timeout and the 60–120 s a reverse proxy usually allows. A 300-sheet S06 PDF takes about 65 s. Lowering `MaxSheets` to 100 would bring it to about 22 s. `appsettings.Development.json` and the test factory still use 60.
5. **Archiving.** The plan folder was not moved to `docs/plans/archived/`: Phase 05 is not closed, and `docs/SUMMARY.md` and `product-goals.md` link to the current path. Archive it in a docs commit after the PR merges, as was done for the stock voucher form plan.

## Deviations from the plan

| # | Deviation | Reason |
|---|---|---|
| D-1 | Backend tests ran on `qldonhang_integtest_tt133`, not `qldonhang_integtest`. | Another session was active in the same repository; the fixture drops and recreates the template database. |
| D-2 | Some builds used `-o "$TEMP/…"`. | Another process held `OrderMgmt.WebApi/bin` (MSB3021). |
| D-3 | `Tt133ExportOptions` is bound in `WebApi/Program.cs`. | `Application/DependencyInjection.cs` has no `IConfiguration`; the plan allows "where FeatureOptions is bound". |
| D-4 | New file `components/report-scope-select/use-report-scope.ts`. | The three debt pages and the stock summary page share the URL scope logic. |
| D-5 | `ProductTypeaheadCell` (shared with quotations and stock vouchers) got `pointerEvents: 'auto'` on its suggestion list. | Fix of the Task 4.7 review finding. |
| D-6 | Task 5.2 used an in-process test and direct `soffice` runs. | HTTP clients are denied (open decision 1). |
| D-7 | LibreOffice is installed on the dev machine, unlike the plan's assumption. | Found while preparing Phase 05; the measurements still use the Docker image. |
| D-8 | Phase 05 is not marked complete and the folder is not archived. | Open decisions 1, 2 and 5. |

## Choices made where the plan was silent

Backend and forms:
- Column widths of every form; the three signature blocks split the columns in thirds by count.
- T9: a description equal to the reason is compared trimmed and case-insensitively.
- The "print 0 in the natural column" rule is also applied to "Cộng số phát sinh" on S12.
- A row with a branch code and no description prints `[HN] ` with a trailing space.
- S05 for a bank fund without bank details prints "Nơi mở tài khoản giao dịch: " with no value.
- The reconciliation record: signer names in bold; zero amounts in body rows are empty.
- Company-scope "Ngày mở sổ" skips soft-deleted branches; the branch path includes them.
- `debt-balance/export` with a `partnerId` answers 400 before the scope check, so a user without `branches.access_all` gets 400, not 403.
- "Xuất tất cả" with a warehouse of another branch answers 422 `EXPORT_NO_TARGETS`, not 403.
- S07 with an unknown product group id prints no "Nhóm hàng" line and no rows.
- S08 prints a period line before its parameter lines.

Frontend:
- The "Thông tin đơn vị" card is first in its list but renders after three hard-coded cards.
- The target dialog has a "Hủy" button and uses native radio inputs.
- A row click on Số dư công nợ carries the company scope into the ledger.
- Stock summary page: dates and scope are in the URL; Kho, Nhóm hàng and Tìm kiếm are local state.
- Filter grids were widened to fit the new "Kỳ" and "Phạm vi" selects.

## Not done

- **Browser smoke test** (Task 5.5 step 2, ten items): not run. The matching behaviour is covered by integration and page tests, which is not the same as a manual check.
- **Task 5.2 steps 2, 3, 4, 6** through the container API: not run (open decision 1).

## Residual risks and follow-ups

- The five accountant/PO questions are still open; each is a go-live gate in GO-LIVE-CHECKLIST.md.
- The layouts come from secondary sources; the accountant compares the samples with the original Phụ lục 4.
- S04a is 161 width units wide on portrait A4, so the print is small (about 58% by calculation). Left for the accountant's sign-off.
- S06 prints the average unit price with three decimals.
- A custom role with only `settings.company` has no sidebar path to the settings hub (`nav-config.ts` is outside the plan's files).
- Company profile: lengths and the tax-code pattern are validated before trimming; the form is not reset after a save.
- The default Kế toán role lacks `reports.inventory`, `inventory.view_cost` and `branches.access_all`; the checklist tells the admin to grant them where needed.
- Two frontend tests (`stock-reason-list-page`, `cash-reason-list-page`) time out at 5000 ms under load. Not caused by this branch.
- The tracked IDE cache `OrderMgmt.Application.csproj.lscache` still names `IQuotationSpreadsheetPdfConverter`; no source file does.
- The five commits of `fix/stock-voucher-form-followups` are not in `qlbanhang/main`, so they are not in this branch.
- During Phase 01 another session's uncommitted change to `docs/SUMMARY.md` and an untracked `docs/brainstorms/261013-2247-executive-dashboard/` folder disappeared from the shared checkout. This run did not touch them.
- Leftovers outside the repository: the `qlbh-backend` Docker image, `%TEMP%\tt133-samples\`, the `tt133-bulk-*` files, `%TEMP%\qlbh-tt133-trx\`, and `qldonhang_integtest_tt133*` databases.
