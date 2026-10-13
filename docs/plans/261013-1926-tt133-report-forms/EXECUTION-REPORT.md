# Execution Report — TT133 report forms

> Plan: [SUMMARY.md](SUMMARY.md) · Branch: `feat/tt133-report-forms` (from `qlbanhang/main` `a76ce85`, not pushed) · Executed 2026-10-13 → 2026-10-14 · Baseline: [BASELINE.md](BASELINE.md) · Go-live gate: [GO-LIVE-CHECKLIST.md](GO-LIVE-CHECKLIST.md)

## Outcome

Phases 00–04 are complete. Phase 05 is **left in progress**: its measurements, docs and final suites are done, but some of its steps wait for a user decision (see "Open decisions"), and the browser smoke test was not run.

The branch delivers the eight forms (S04a, S05, S06, S07, S08, S12, debt summary, reconciliation record) as Excel and PDF, the company profile with its settings page, the stock in–out–balance report and page, the branch/company scope of the debt reports and S07, the reason, due-date and branch-code fields, and `VndAmountInWords`.

| Check | Baseline (Phase 00) | Final (`e49eacb`) | Result |
|---|---|---|---|
| `dotnet build OrderMgmt.sln` | 0 errors | 0 errors (9 warnings) | ✅ |
| `dotnet test OrderMgmt.sln` | 19 failed / 841 total | 19 failed / 972 total (953 passed, 19 m 51 s) | ✅ the same 19 tests, compared by name |
| `npm run typecheck` | exit 0 | exit 0 | ✅ |
| `npm run lint` | 3 errors, 14 warnings | 3 errors, 14 warnings | ✅ same as baseline |
| `npm run test` | 3 failed / 948 total | 3 failed / 1035 total (1032 passed) | ✅ the same 3 tests |
| `npm run build` | passes | passes | ✅ |

**Re-run after the later changes (`248ac91`).** Two fixes (decisions 2 and 3) and a merge of `qlbanhang/main` came after the run in the table. The backend suite was run again on the two fixes: 973 tests, 954 passed, 19 failed, the same 19 names (41 m 42 s; the machine was under load). The merge then brought in PR #11 (stock voucher form follow-ups, frontend files only), so the frontend checks were run again on `248ac91`: typecheck clean, lint 3 errors / 14 warnings, 1048 tests with the same 3 failures, build passes. The backend suite was not run again after the merge, which changed no backend file.

The suites grew by 132 backend tests and 100 frontend tests (13 of the frontend tests came with PR #11). The change is 142 files, +13,486 / −135 lines against `a76ce85`.

## Commits per phase

| Phase | Commits | Notes |
|---|---|---|
| 00 Branch and baseline | `f441baf`, `e3566f7` | `e3566f7` records BASELINE.md |
| 01 Company profile, amount in words, converter | `3acd2d8` → `609227a` (marker `bf35dd2`) | 6 tasks; migration `AddCompanyProfile` |
| 02 Form builder, export pipeline, S04a, S05 | `d4ec7cb` → `a61c3c8` (marker `8651c1a`) | 9 tasks; builder and export service frozen here |
| 03 Debt scope, S12, debt summary, reconciliation | `c83c4ec` → `9625104` (marker `09493a3`) | 9 tasks |
| 04 Stock summary, S07, S08, S06 | `5644581` → `3b818d2`, fix `80cdde5` (marker `3a83a4f`) | 7 tasks; one review finding fixed |
| 05 Measurements, docs, verification | `c6cfd7b`, `bfc425e`, `d94cf24`, `1499df0`, `e49eacb`, `a70b099`, `c5439c1`, then the docs update | two layout fixes from the PDF check; R2 fix after the user's decision |

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

- The 13 sample workbooks and their PDFs are in `%TEMP%\tt133-samples\` (page images in `png\`; the images predate the locale change of decision 3 and still show commas). The workbooks came from a temporary in-process test and the PDFs from `soffice` run directly in the image, **not** from the container's API (see open decision 1).
- Every PDF was looked at. Fonts are Times New Roman only, with diacritics intact; every form fits the page width; negatives print in parentheses; dates are `dd/MM/yyyy`; each sheet starts a new page; the signature block stays together.
- Two layout defects were fixed, each with a failing unit test first:
  - `bfc425e` — wrapped text printed over the neighbouring rows, and the reconciliation conclusion was cut at the page edge. The builder now sets row heights from an estimate of the line count; some rows get one spare line.
  - `d94cf24` — page 2 had no header rows, because LibreOffice needs absolute print titles (`$9:$11`). The builder rewrites `xl/workbook.xml` after ClosedXML saves.
- Concurrent conversions (substitute for step 6): three parallel `soffice` processes, three rounds, three containers. **7 of 27 failed** (exit code 1, no PDF, no hang). With one profile directory per conversion, 9 of 9 succeeded.

## Open decisions (for the user)

1. **Container checks over HTTP were not run — DECIDED: accepted.** The project settings deny `curl`, `wget` and `Invoke-WebRequest`, so Task 5.2 steps 2, 3, 4 and 6 and the API parts of the smoke test were not carried out against the running container. On 2026-10-14 the user accepted the in-process exports, the direct `soffice` runs in the image and the nine parallel conversions through the converter as the evidence. The HTTP path of the container (login, routes, the converter called by the running application) remains unexercised.
2. **R2: concurrent PDF conversions — DECIDED and fixed.** On the shared default profile about one conversion in three failed (503 `PDF_EXPORT_FAILED` for that user). The user chose a profile directory per conversion on 2026-10-14 (no semaphore). `c5439c1` adds `-env:UserInstallation` under the conversion's temp folder, with the unit test `Each_conversion_gets_its_own_profile_directory` (RED first). Check: nine parallel conversions (3 × 3 rounds) of `S12-DNN_Receivable.xlsx` through `LibreOfficeSpreadsheetPdfConverter` with the local LibreOffice on Windows all returned a PDF (3.6–6.7 s each). That check ran after the final full-suite run, in a temporary test that was not committed; only the three converter tests were re-run after the fix, not the full suite, and the fix was not re-measured inside the Docker image.
3. **Number separators in PDFs — DECIDED and fixed.** Table cells printed `1,250,750,000` (the image's LibreOffice ran with an English locale) while the reconciliation sentence prints `368.775.000 đồng`. The user chose a Vietnamese locale in the image on 2026-10-14. `3dbfd0f` generates `vi_VN.UTF-8` in `backend/Dockerfile` and points `QuotationExport__LibreOfficePath` at a wrapper (`/usr/local/bin/soffice-vi`) that sets the locale for `soffice` only, so the .NET process keeps its culture. Check in the rebuilt image: eight sample forms print `48.600.000`, `(5.000)`, `200,125`, with the same fonts, page counts and `dd/MM/yyyy` dates, and three parallel conversions succeeded. The 13 sample PDFs in `%TEMP%	t133-samples\` were regenerated. Quotation and handover PDFs go through the same converter, so their separators change too; they were not looked at. A dev machine or a deployment that does not use this image keeps its own LibreOffice locale.
4. **Export limits — DECIDED: keep 300 sheets and 130 s.** The conversion timeout (130 s) is longer than the 120 s client timeout, and a 300-sheet S06 PDF takes about 65 s. The user kept both values on 2026-10-14. The deployment must give the reverse proxy a timeout of at least 120 s (added to GO-LIVE-CHECKLIST.md); a proxy that cuts at 60 s would cut a 300-sheet S06 PDF. `appsettings.Development.json` and the test factory still use 60 s.
5. **Archiving — DECIDED: after the PR merges.** The plan folder stays at its current path on this branch, because `docs/SUMMARY.md`, `product-goals.md` and the checklist link to it. Archive it in a separate docs commit after the merge, as was done for the stock voucher form plan (user decision 2026-10-14).

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
| D-8 | Phase 05 is not marked complete and the folder is not archived. | Open decisions 1 and 5. |
| D-9 | A LibreOffice profile per conversion, which review S3 had rejected. | User decision 2026-10-14 after the R2 measurement. |
| D-10 | `backend/Dockerfile` changed (locale and `soffice` wrapper); it is not in the plan's files. | User decision 2026-10-14 on PDF number separators. |

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
- `486ee9a` (the report and checklist edits for decisions 4 and 5) and `248ac91` (merge of `qlbanhang/main` with PR #11, the stock voucher form follow-ups) were committed on this branch from outside this session while the backend suite was running. PR #11 also changed `product-typeahead-cell.tsx`; the `pointerEvents: 'auto'` fix of D-5 is still in the merged file and its test passes.
- During Phase 01 another session's uncommitted change to `docs/SUMMARY.md` and an untracked `docs/brainstorms/261013-2247-executive-dashboard/` folder disappeared from the shared checkout. This run did not touch them.
- Leftovers outside the repository: the `qlbh-backend` Docker image, `%TEMP%\tt133-samples\`, the `tt133-bulk-*` files, `%TEMP%\qlbh-tt133-trx\`, and `qldonhang_integtest_tt133*` databases.
