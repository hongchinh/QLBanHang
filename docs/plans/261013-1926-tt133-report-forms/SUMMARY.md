# TT133 report forms — Excel/PDF exports for funds, inventory and debt (Báo cáo theo mẫu biểu TT133)

> Created: 2026-10-13 19:26:58 · Source design: [brainstorm SUMMARY](../../brainstorms/261012-2237-tt133-report-forms/SUMMARY.md), [section-01 form specs](../../brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md), [section-02 review decisions](../../brainstorms/261012-2237-tt133-report-forms/section-02-review-decisions.md) · Builds on: [Round 2B plan](../archived/261012-1709-cash-debt-round2b/SUMMARY.md) (its "Print/Excel stays in Round 3" item)

## Goal

Export eight forms as Excel and PDF in the layout of Circular 133/2016/TT-BTC (Phụ lục 4), each a presentation of a report the software already shows, so the file always equals the screen:

| Form | Title | Source report | Export route (`api/reports/…`) |
|---|---|---|---|
| S04a-DNN | Sổ quỹ tiền mặt | `cash-book`, `kind=Cash` | `GET cash-book/export` |
| S05-DNN | Sổ tiền gửi ngân hàng | `cash-book`, `kind=Bank` | `GET cash-book/export` |
| S06-DNN | Sổ chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | `stock-card` (with values) | `POST stock-detail/export` |
| S07-DNN | Bảng tổng hợp chi tiết vật liệu, dụng cụ, sản phẩm, hàng hóa | **new** `stock-summary` | `GET stock-summary/export` |
| S08-DNN | Thẻ kho (Sổ kho) | `stock-card` (quantities) | `POST stock-card/export` |
| S12-DNN | Sổ chi tiết thanh toán với người mua (người bán) | `debt-ledger` | `POST debt-ledger/export` |
| — | Bảng tổng hợp chi tiết thanh toán với người mua / người bán | `debt-balance` | `GET debt-balance/export` |
| — | Biên bản đối chiếu công nợ | `debt-reconciliation` | `GET debt-reconciliation/export` |

The plan also delivers what the forms need and the software lacks: the company profile (`CompanyProfile`) with a settings screen, the stock in–out–balance summary (S07) as a query and a screen, the `scope = Branch | Company` parameter of the debt reports and S07, the reason and due-date fields of the cash book and debt ledger rows, and `VndAmountInWords`.

## Scope

**In scope**

- Backend:
  - `CompanyProfile` (one row, migration), `GET/PUT api/settings/company`, permission `settings.company`;
  - `VndAmountInWords`;
  - `ISpreadsheetPdfConverter` (the renamed `IQuotationSpreadsheetPdfConverter`), and the LibreOffice converter kills its process when the conversion times out (T23);
  - `Tt133FormBuilder` and eight form classes (ClosedXML), `ITt133FormRenderer`, `ITt133ExportService`, `Tt133ExportController`;
  - `CashBookRowDto.ReasonName`, `DebtLedgerRowDto.ReasonName`, `DueDate` and `BranchCode` (T21);
  - `ReportScope` on `debt-balance`, `debt-ledger`, `debt-reconciliation`;
  - new report `GET api/reports/stock-summary`;
  - two new exceptions mapped to 422 and 503.
- Frontend:
  - Thông tin đơn vị settings page;
  - the "Xuất theo mẫu TT133" button on Sổ quỹ, Sổ chi tiết công nợ, Số dư công nợ, Biên bản đối chiếu, Thẻ kho and the new Tổng hợp nhập–xuất–tồn page;
  - the target dialog (one / several / all with activity) for S06, S08 and S12;
  - the scope select on the three debt pages and S07;
  - the new columns Lý do (Sổ quỹ, Sổ chi tiết công nợ) and Hạn TT (Sổ chi tiết công nợ), and the branch code in front of Diễn giải when the ledger is read for the whole company;
  - period presets (month / quarter / year).
- Docs: `docs/SUMMARY.md`, `conventions.md`, `directory-structure.md`, `system-architecture.md`, `product-goals.md`.

**Out of scope** (brainstorm non-goals and rejected review proposals S1–S8)

- Chart of accounts, "TK đối ứng", journals, financial statements, VAT books, foreign currency (S13, S14).
- TT99 mappers, admin-editable templates, HTML print pages, print forms of vouchers.
- Batch source queries for bulk export, a LibreOffice profile per conversion or a semaphore, per-form signature lists, "Số liệu chưa khóa sổ" notes (review S2, S3, S6, S8). Review S4 (POST for bulk export) is **accepted for the three multi-target forms** by user decision 2026-10-13, see T5. R1 and R2 are measured in Phase 05; a fix is a separate decision.
- Changing `CostingScope`: it is an existing setting that the accountant switches to `Warehouse` at go-live (review B2). No code.

## Decisions made while planning (in addition to the brainstorm and review B1–B3)

| # | Topic | Decision |
|---|---|---|
| T1 | Branch (user decision 2026-10-13) | Work starts **after `feat/stock-voucher-form-tabs` is finished and merged into `qlbanhang/main`**. The branch `feat/tt133-report-forms` is then created from the updated `qlbanhang/main`, which holds Round 2B, the sidebar redesign and the stock voucher form. Phase 00 checks the merge and stops if it has not happened. |
| T2 | Layering | Application gets `Application/Tt133/` (`Models`, `Interfaces/ITt133FormRenderer`, `Interfaces/ITt133ExportService`, `Services/Tt133ExportService`, `Tt133ExportOptions`). Infrastructure gets `Infrastructure/Excel/Tt133/` (`Tt133FormBuilder`, one class per form, `Tt133FormRenderer`). The export service resolves targets, loads the report DTOs through the existing report services, builds the `Tt133FormContext`, calls the renderer and, for PDF, the converter. Review S1 (a neutral `FormDocument` model) stays rejected: the form classes call the builder directly. |
| T3 | Routes | One literal action per report in `Tt133ExportController` (`[Route("api/reports")]`), so each keeps the `[HasPermission]` of its source report. Single-document forms are `GET api/reports/{report}/export?format=xlsx|pdf&…` with the parameters of the screen report (`cash-book`, `debt-balance`, `debt-reconciliation`, `stock-summary`). The three multi-target forms are `POST api/reports/{report}/export?format=xlsx|pdf` with the request as a JSON body (`debt-ledger`, `stock-card`, `stock-detail`; T5). S06 and S08 both read `stock-card`, so their slugs are `stock-detail` (S06) and `stock-card` (S08). |
| T4 | Errors | `UnprocessableException(code, message)` → **422** with codes `EXPORT_NO_TARGETS` ("Không có đối tượng nào để xuất.") and `EXPORT_TOO_MANY_SHEETS` ("Số sổ cần xuất ({n}) vượt giới hạn {max}. Hãy thu hẹp điều kiện lọc."). `ServiceUnavailableException(code, message)` → **503** with code `PDF_EXPORT_FAILED` ("Xuất PDF thất bại, hãy dùng Excel."). Both are new classes in `Domain/Common/DomainException.cs`, mapped in `GlobalExceptionMiddleware` before the generic `DomainException` arm. The frontend treats neither as a confirmable 422. |
| T5 | Limits and bulk requests (user decision 2026-10-13) | `Tt133ExportOptions` (section `Tt133Export`): `MaxSheets = 300` is the only limit; it applies to explicit lists and to "all with activity" alike (422 `EXPORT_TOO_MANY_SHEETS`). The id lists of S12, S06 and S08 travel in a POST body, because a GET URL with a few hundred GUIDs passes Kestrel's 8 KB request line. This reverses review S4 for these three routes only. |
| T6 | One snapshot | `Tt133ExportService` wraps each export in one `ITransactionRunner.RunSnapshotAsync`. The report services' own `RunSnapshotAsync` calls join it (`EfTransactionRunner` runs the work directly when a transaction is open), so the target list and every sheet read the same state. Only the data loading (context, targets, report DTOs) runs inside the snapshot; rendering the workbook and the PDF conversion run after it is closed, so no transaction is held while ClosedXML or LibreOffice works. |
| T7 | Scope | `ReportScope { Branch = 0, Company = 1 }` in `Application/Common/ReportScope.cs` with `ReportScopeGuard.EnsureAllowed(ICurrentUser, ReportScope)`: `Company` without `branches.access_all` throws `ForbiddenException("Cần quyền làm việc ở mọi chi nhánh để xem toàn công ty.")` (403). `Company` drops the `BranchId` filter and nothing else; settlements stay inside a branch. The header prints `{Company}` for Company and `{Company} – {Branch}` for Branch. "Ngày mở sổ" is the working branch's `OpeningDate` for Branch and the earliest non-null `Branch.OpeningDate` for Company; null prints dots. |
| T8 | Reason names | Cash book row: the voucher's `CashReason.Name` (soft-deleted reasons included); a transfer half is `"Chuyển quỹ"`. Debt ledger row: `StockIn`/`StockOut` → the stock voucher's `StockReason.Name`; `Receipt`/`Payment` → the cash voucher's `CashReason.Name`; `Offset` → `"Bù trừ công nợ"`. `DueDate` is `DebtEntry.DueDate` (set on Increase rows only). |
| T9 | "Diễn giải" text | Cash forms: `Description`, then ` – {PartnerOrPerson}` when present. A `Description` equal to the row's `ReasonName` (a voucher without its own description shows its reason there) is left out, because the form has its own "Lý do" column; the screen is unchanged (user decision 2026-10-13). S06 column C: `{PartnerName} – {document type}` with the type "Phiếu nhập kho" / "Phiếu xuất kho" / "Tồn đầu kỳ" (the part that is empty is left out). S08 column E: `{PartnerName} – {ReasonName}`. |
| T10 | File names | `{code}_{from:yyyyMMdd}_{to:yyyyMMdd}.{xlsx|pdf}` with code `S04a-DNN`, `S05-DNN`, `S06-DNN`, `S07-DNN`, `S08-DNN`, `S12-DNN`, `TongHopCongNo`, `DoiChieuCongNo_{PartnerCode}`. The server sends it in `Content-Disposition`; the frontend builds the same name itself (the header is not exposed across origins), like the revenue export does. |
| T11 | Period line | `from` = first day and `to` = last day of the same month → "Tháng MM năm yyyy"; 01/01 to 31/12 of one year → "Năm yyyy"; anything else → "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy". The reconciliation record always uses the last form. |
| T12 | Sheets | One sheet per fund / partner × side / product × warehouse. The name is the code (fund code, partner code, `{productCode}-{warehouseCode}`), with `[ ] : * ? / \` replaced by `_`, cut to 31 characters and made unique with a ` (2)`, ` (3)` suffix inside the 31 characters. PDF pages follow the sheet order. |
| T13 | Company profile access | `GET api/settings/company` needs only a signed-in user (report pages read it to warn about an empty profile). `PUT` needs `settings.company` (`Permissions.CompanySettings.Manage`), seeded for ADMIN and MANAGER through "all permissions" and added to the default list of ACCOUNTANT (user decision 2026-10-13: the accountant fills in the tax code and the signers). The row is seeded empty (`Id = 1`); every text field is nullable. |
| T14 | Header tiers | The table header has two tiers (a group and its sub-columns) plus the symbol row. No form in section-01 needs three. |
| T15 | Frontend download | Both formats download as a file through `features/tt133-export` (`responseType: 'blob'`, explicit `timeout: 120_000`). A blob error body is parsed back to JSON so the toast shows the server message. The button is `components/tt133-export/tt133-export-button.tsx`; it shows a hint with a link to Cài đặt when the company name is empty. |
| T16 | Period presets | `lib/period-presets.ts` and `components/period-preset-select`: Tháng này, Tháng trước, Quý này, Quý trước, Năm nay, Năm trước. Added to the pages that get an export button. |
| T17 | Amount in words | `VndAmountInWords.Read(decimal)` in `Application/Common/`. Rounds to a whole đồng, reads the absolute value, splits it into 9-digit chunks (each chunk level adds one "tỷ") and each chunk into triệu / nghìn / units. Details and test cases in Phase 01. |
| T18 | Reconciliation wording | Only the sentences listed in the "Biên bản đối chiếu" layout of Phase 03. The "căn cứ" clause and the reply-within-N-days clause wait for the accountant's template (open question) and are not written. |
| T19 | Opening stock in S07 | S07 uses the stock card's window rule unchanged: a ledger row counts as "đầu kỳ" when `PostedAt < StartOfDay(from)` and as "trong kỳ" otherwise. Opening stock is posted on the branch's opening date, so in the first period it appears under "Nhập trong kỳ", exactly as the stock card lists it as a row. This keeps the invariant "each product × warehouse equals the stock card". Confirmed by the user on 2026-10-13; the accountant still sees it on the printed sample (Phase 05). |
| T20 | S06 / S08 targets | A sheet is one product × warehouse. `all=true`: every pair of the working branch (optionally one warehouse) with a non-zero opening quantity or a ledger row in the period. An explicit product with `warehouseId`: always one sheet, even when empty (as the screen shows it). An explicit product without `warehouseId`: its warehouses with activity; a product with none gets no sheet. |
| T21 | Branch code in company scope (user decision 2026-10-13) | Document numbers are counted per branch, so a company-scope ledger can hold two rows with the same code (two `PX00001`). `DebtLedgerRowDto.BranchCode` is the code of the entry's branch when the scope is `Company` and null for `Branch`. When it is set, S12 and the reconciliation record print `[{BranchCode}] ` in front of "Diễn giải", and the Sổ chi tiết công nợ screen shows the same prefix. No new column. |
| T22 | Search filter on printed summaries (user decision 2026-10-13) | The debt summary and S07 are exported with the screen's `search`, so the file equals the screen. When `search` is not blank the form prints one more parameter line: `Điều kiện lọc: mã hoặc tên chứa "{search}"`. `debt-balance/export` rejects a `partnerId` (400, key `partnerId`): the page never sends one and a one-partner summary is S12. |
| T23 | LibreOffice on timeout | `LibreOfficeSpreadsheetPdfConverter` only stopped waiting when `ConversionTimeoutSeconds` passed; the `soffice` process kept running and kept the default profile. It now calls `process.Kill(entireProcessTree: true)` when the wait is cancelled (timeout or request abort) and rethrows. This is a bug fix in Phase 01, not review S3 (no profile per conversion, no semaphore). |
## Assumptions

- The brainstorm, section-01 and section-02 (B1–B3, R1, R2) are the approved spec. T1–T23 refine it.
- The accountant/PO questions of the brainstorm are **not answered yet** (confirmed by the user on 2026-10-13). The plan proceeds on the brainstorm's choices; each is a go-live gate in Phase 05, not a blocker for development:
  - the company applies TT133 (not TT99);
  - "TK đối ứng" becomes "Lý do" and "Thời hạn được chiết khấu" becomes "Hạn thanh toán";
  - branches have no tax code of their own;
  - `CostingScope` is switched to `Warehouse` at go-live;
  - no company-specific reconciliation template exists yet.
- The layouts come from secondary sources. The accountant compares a printed sample of each form with the original Phụ lục 4 before go-live (Phase 05).
- Local setup as in Round 2B: PostgreSQL on `localhost:5432` (`postgres`/`1`), `TEST_DB_CONNECTION` → `qldonhang_integtest` (never `qldonhang_test`). LibreOffice is not installed on the dev machine: automated tests use a fake converter, and real PDF output is checked in Docker in Phase 05.
- Volumes are small (tens of thousands of ledger rows a year), as assumed by the existing reports.

## Risks

- **R1 — Bulk export is slow.** "All with activity" calls the source report once per target (N+1) and PDF conversion is synchronous. Mitigation: `MaxSheets`, the 120 s client timeout, and measurements of S12 and S08 on bulk data in Phase 05 Task 5.1 that set the final `MaxSheets` and `QuotationExport:ConversionTimeoutSeconds`.
- **R2 — Concurrent LibreOffice runs** share the default user profile and can fail or hang. Mitigation: the converter kills a timed-out process (T23), so one slow conversion cannot block the next ones, and a 3-request parallel test in Docker in Phase 05 Task 5.2. If it fails, stop and ask the user (review S3 was rejected, so the fix is a new decision).
- **Layout drift from the official forms.** Mitigation: the accountant's sign-off per form (go-live gate).
- **Company scope numbers.** A wrong filter silently mixes or drops branches. Mitigation: tests on the Round 2A report dataset, whose branch B holds a 9,999 sale of the same customer.
- **File equals screen.** Each form reads the same DTO as its screen; tests assert the totals of every form against the source report.
- **ClosedXML page setup details** (`FitToPages(1, 0)`, rows to repeat) are read back in unit tests, but only a real print shows them. Phase 05 prints each form.
- **Baseline failures.** The suite had 19 backend and 3 frontend known failures at the end of Round 2B, two of them PDF tests with a fake converter. Phase 00 records the baseline on the new branch; later phases compare test by test.

## Phases

- [x] Phase 00 — Branch and failure baseline (S) — `phase-00-baseline.md`
- [x] Phase 01 — Company profile, amount in words, PDF converter rename and timeout fix (M) — `phase-01-company-profile.md`
- [x] Phase 02 — Form builder, export infrastructure, S04a and S05 (L) — `phase-02-form-builder-cash-forms.md`
- [x] Phase 03 — Debt scope, ledger fields, S12, debt summary, reconciliation record (L) — `phase-03-debt-forms.md`
- [x] Phase 04 — Stock summary (S07) report and page, S06, S08 (L) — `phase-04-stock-forms.md`
- [ ] Phase 05 — Measurements, docs, final verification (M) — `phase-05-docs-and-verification.md`

Phases run in order. Phase 02 freezes the builder, the export service shape and the frontend export button; Phases 03 and 04 only add forms to them.

## Shared contracts (frozen in Phase 02 unless a phase says otherwise)

```csharp
// Application/Common/ReportScope.cs (enum in Phase 02 Task 2.5, guard in Phase 03 Task 3.3)
public enum ReportScope { Branch = 0, Company = 1 }
public static class ReportScopeGuard { public static void EnsureAllowed(ICurrentUser user, ReportScope scope); }

// Application/Common/Interfaces/ISpreadsheetPdfConverter.cs (Phase 01)
public interface ISpreadsheetPdfConverter { Task<byte[]> ConvertAsync(byte[] xlsxBytes, CancellationToken ct = default); }

// Application/Tt133/Models/Tt133ExportModels.cs (Phase 02; the two request classes in Phases 03 and 04)
public enum ExportFormat { Xlsx = 0, Pdf = 1 }
public record ExportFile(byte[] Bytes, string ContentType, string FileName);
public class Tt133FormContext
{
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }            // null for scope Company
    public string? Address { get; set; }               // branch address, else company address
    public string? TaxCode { get; set; }
    public string? BookkeeperName { get; set; }        // ICurrentUser.FullName
    public string? ChiefAccountantName { get; set; }
    public string? LegalRepresentativeName { get; set; }
    public string? AccountingServiceNote { get; set; }
    public DateOnly? OpeningDate { get; set; }
    public DateOnly PrintDate { get; set; }            // VN date of the export
}
public class DebtLedgerExportRequest          // Phase 03
{
    public DebtSide Side { get; set; }
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public ReportScope Scope { get; set; }
    public List<Guid> PartnerIds { get; set; } = new();
    public bool All { get; set; }
}
public class StockCardExportRequest           // Phase 04
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? WarehouseId { get; set; }
    public List<Guid> ProductIds { get; set; } = new();
    public bool All { get; set; }
}
public record StockCardSheet(StockCardDto Card, string WarehouseCode, string WarehouseName);   // Phase 04

// Application/Tt133/Interfaces/ITt133FormRenderer.cs — one method per phase task, implemented by Infrastructure
byte[] RenderCashBook(Tt133FormContext context, CashBookReportDto report, FundKind kind);                 // S04a / S05
byte[] RenderDebtLedgers(Tt133FormContext context, IReadOnlyList<DebtLedgerReportDto> ledgers);           // S12
byte[] RenderDebtSummary(Tt133FormContext context, DebtBalanceReportDto report, string? search);         // search: T22
byte[] RenderDebtReconciliation(Tt133FormContext context, DebtReconciliationReportDto report);
byte[] RenderStockSummary(Tt133FormContext context, StockSummaryReportDto report, string warehouseLabel, string? productGroupName, string? search);  // S07; search: T22
byte[] RenderStockDetail(Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets);                 // S06
byte[] RenderStockCards(Tt133FormContext context, IReadOnlyList<StockCardSheet> sheets);                  // S08

// Application/Tt133/Interfaces/ITt133ExportService.cs
Task<ExportFile> ExportCashBookAsync(CashBookReportRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportDebtLedgerAsync(DebtLedgerExportRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportDebtSummaryAsync(DebtBalanceReportRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportDebtReconciliationAsync(DebtReconciliationReportRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportStockSummaryAsync(StockSummaryRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportStockDetailAsync(StockCardExportRequest request, ExportFormat format, CancellationToken ct = default);
Task<ExportFile> ExportStockCardsAsync(StockCardExportRequest request, ExportFormat format, CancellationToken ct = default);
```

Content types: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` and `application/pdf`.

## Executor conventions

- **Branch and commits:** work on `feat/tt133-report-forms` (or the worktree the executing skill creates from it). Commit after every task with conventional commits, scope `tt133`, `reports`, `settings` or `frontend` (e.g. `feat(tt133): S04a cash book form`). End every commit message with the attribution line from the session's system reminder.
- **Staging:** stage files explicitly (`git add <paths>`, from the task's file list). Never `git add -A` / `git add .`: `source/` is untracked on purpose and `.claude/tools/` holds a token.
- **Shell:** commands are written for Git Bash (the Bash tool). Windows PowerShell 5.1 has no `&&`. Keep the cwd on the uppercase `D:\` drive while running workflows or worktree agents.
- **Test database** — set before any backend test command:
  - bash: `export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
  - PowerShell: `$env:TEST_DB_CONNECTION = "Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"`
- **Backend commands** (from `backend/`):
  - Build: `dotnet build OrderMgmt.sln` (if a running dev server locks `bin`, add `-o "$TEMP/qlbh-build"`; if the Git Bash session has no `APPDATA`, set it or NuGet restore fails).
  - Targeted tests: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~<ClassName>"`.
  - Pure unit tests of this plan: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133.Unit"`.
  - Full suite: `dotnet test OrderMgmt.sln`.
  - Migration: `dotnet ef migrations add <Name> --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi -o Persistence/Migrations`.
- **Frontend commands** (from `frontend/`): `npx vitest run <path>`, `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build`. Every frontend phase ends with `npm run build`; afterwards run `git checkout -- frontend/tsconfig.app.tsbuildinfo frontend/tsconfig.node.tsbuildinfo` (skip a path that is not tracked).
- **Test locations:**
  - Pure unit tests: `backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/` (no collection, no database). The builder and the form classes are `public`, and Infrastructure already has `InternalsVisibleTo("OrderMgmt.IntegrationTests")`.
  - Integration tests: `backend/tests/OrderMgmt.IntegrationTests/Tt133/`, `[Collection(nameof(PostgresCollection))]`. Cash and debt forms inherit `CashDebtTestBase` and seed `ReportDataset.SeedAsync(this)` (its rows and numbers are repeated in Phases 02 and 03); stock forms inherit `InventoryTestBase`.
  - Workbook assertions read the response bytes with `new XLWorkbook(new MemoryStream(bytes))`, as `Reports/RevenueLineItemsExportTests.cs` does. Find a row by its label text, not by a fixed row number: add the helper `Tt133/Tt133Sheet.cs` in Phase 02 Task 2.5 (`RowOf(ws, text)`, `ColumnOf(ws, headerRow, text)`).
  - Frontend tests sit next to the file under test and follow `pages/cash-reports/cash-book-page.test.tsx` (mocked hooks, `useAuthStore.setState` for permissions, `MemoryRouter`).
- **Rules carried over from Round 2A/2B:**
  - DTOs use public `{ get; set; }` auto-properties; lists default to `new()`.
  - Every `DateTimeOffset` sent to EF is UTC; VN dates use `VnTime.StartOfDay` / `StartOfNextDay`; a printed date is `VnTime.ToVnDate(instant)`.
  - Soft-deleted partners, products, warehouses, reasons and users keep their history: read them with `IgnoreQueryFilters()` when resolving names.
  - Validation `details` keys are camelCase (`kind`, `partnerIds`, `warehouseId`).
  - New `ILike` searches use `LikePattern.Escape` and pass `LikePattern.EscapeCharacter`.
  - Happy-path tasks must not implement the validation rules of a later task, or that task's RED step cannot fail.
  - **RED can be a build error.** A test that references a type that does not exist yet fails to compile; that counts as RED ("FAIL — build error, `<Type>` does not exist").
  - **Nav test.** `components/layout/nav-config.test.ts` asserts the exact report item list and icon uniqueness; the task that adds the S07 nav item updates it in its RED step.
  - **Comparing with the baseline.** After a full backend run with `--logger "trx;LogFileName=current.trx"`, list the failures with the command in Phase 00 and `diff` them with `BASELINE.md`.
- **Money in text** (reconciliation sentences): `amount.ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"))` → `1.500`.
- **Notation:** `10-05` = 2026-10-05.

## Final Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test OrderMgmt.sln

cd ../frontend
npm run typecheck
npm run lint
npm run test
npm run build
```

Only the failures recorded in Phase 00 `BASELINE.md` may remain, compared test by test. The manual checks (print of each form, Docker PDF run, bulk timing) are Phase 05.

## Rollback / Recovery

- **Code:** every task is a separate commit on `feat/tt133-report-forms`; `git revert` single commits or drop the branch.
- **Database:** one migration, `AddCompanyProfile` (a new table `company_profile` with one seeded row). To undo it before deploy: `dotnet ef migrations remove --project src/OrderMgmt.Infrastructure --startup-project src/OrderMgmt.WebApi`. After deploy: `dotnet ef database update <previous migration> …`, which drops the table; no other table references it.
- **Permission:** `settings.company` is inserted by the seeder (ADMIN, MANAGER, ACCOUNTANT). Reverting the code leaves an unused permission row, which is harmless; delete it by hand from `permissions` and `role_permissions` if wanted.
- **Report behaviour:** `scope` defaults to `Branch` and the new DTO fields are additions, so reverting the frontend alone leaves the existing screens as they were.
