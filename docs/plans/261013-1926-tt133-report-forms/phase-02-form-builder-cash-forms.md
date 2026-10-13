# Phase 02 — Form builder, export infrastructure, S04a and S05

**Status:** [ ] pending
**Complexity:** L

## Objective

Build the shared pieces every form uses and prove them with the two cash forms:

- `Tt133FormBuilder`: header block, title and period line, two-tier table header with the symbol row, body and summary rows, footer with signatures, page setup, sheet names.
- The export pipeline: `ITt133FormRenderer`, `ITt133ExportService`, `Tt133ExportController`, the 422/503 exceptions, PDF through `ISpreadsheetPdfConverter`.
- `CashBookRowDto.ReasonName` (review B3), shown on the Sổ quỹ screen too.
- **S04a-DNN** (cash funds) and **S05-DNN** (bank funds) from `GET api/reports/cash-book/export`.
- Frontend: the download helper, the "Xuất theo mẫu TT133" button, period presets, and the button on the Sổ quỹ page.

After this phase the builder API, the export service shape and the export button are frozen for Phases 03 and 04.

## Files

Backend:
- `backend/src/OrderMgmt.Domain/Common/DomainException.cs` (modify — two exceptions)
- `backend/src/OrderMgmt.WebApi/Middleware/GlobalExceptionMiddleware.cs` (modify)
- `backend/src/OrderMgmt.Application/Common/ReportScope.cs` (new — the enum only)
- `backend/src/OrderMgmt.Application/Tt133/Models/Tt133ExportModels.cs` (new)
- `backend/src/OrderMgmt.Application/Tt133/Tt133ExportOptions.cs` (new)
- `backend/src/OrderMgmt.Application/Tt133/Interfaces/ITt133FormRenderer.cs`, `ITt133ExportService.cs` (new)
- `backend/src/OrderMgmt.Application/Tt133/Services/Tt133ExportService.cs` (new)
- `backend/src/OrderMgmt.Application/DependencyInjection.cs`, `backend/src/OrderMgmt.Infrastructure/DependencyInjection.cs` (modify)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormBuilder.cs`, `Tt133Column.cs` (new)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/CashBookForm.cs` (new — S04a and S05)
- `backend/src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormRenderer.cs` (new)
- `backend/src/OrderMgmt.WebApi/Controllers/Tt133ExportController.cs` (new)
- `backend/src/OrderMgmt.WebApi/appsettings.json` (modify — `Tt133Export` section)
- `backend/src/OrderMgmt.Application/CashDebt/Guards/FundMovementQuery.cs` (modify — `ReasonName`)
- `backend/src/OrderMgmt.Application/CashDebt/Reports/Models/CashDebtReportDtos.cs`, `Services/CashDebtReportService.cs` (modify)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/Unit/Tt133FormBuilderTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/Tt133Sheet.cs` (new — workbook test helper)
- `backend/tests/OrderMgmt.IntegrationTests/Tt133/CashBookExportTests.cs`, `ExportPdfTests.cs` (new)
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/CashBookReportTests.cs` (modify)

Frontend:
- `frontend/src/lib/blob-error.ts`, `blob-error.test.ts` (new)
- `frontend/src/lib/period-presets.ts`, `period-presets.test.ts` (new)
- `frontend/src/components/period-preset-select/period-preset-select.tsx`, `period-preset-select.test.tsx` (new)
- `frontend/src/features/tt133-export/api.ts`, `types.ts`, `api.test.ts`, `use-tt133-download.ts`, `use-tt133-download.test.tsx` (new)
- `frontend/src/components/tt133-export/tt133-export-button.tsx`, `tt133-export-button.test.tsx` (new)
- `frontend/src/features/cash-reports/types.ts` (modify — `reasonName`)
- `frontend/src/pages/cash-reports/cash-book-page.tsx`, `cash-book-page.test.tsx` (modify)

## Reference files (read-only)

- [section-01 form specs](../../brainstorms/261012-2237-tt133-report-forms/section-01-form-specs.md) — "Khung chung", S04a, S05
- `backend/src/OrderMgmt.Infrastructure/Excel/RevenueReportExcelRenderer.cs` — ClosedXML built in code
- `backend/tests/OrderMgmt.IntegrationTests/Reports/RevenueLineItemsExportTests.cs` — reading a workbook from a response
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationExportTests.cs` (the `WebAppFactoryWithFakePdfConverter` at the end of the file and the test that uses it) — replacing the converter in a factory
- `backend/tests/OrderMgmt.IntegrationTests/CashDebt/Reports/CashBookReportTests.cs` — the dataset numbers
- `frontend/src/features/reports/sales-revenue-detail/api.ts` — blob download

## Builder contract (`Infrastructure/Excel/Tt133/`)

```csharp
public enum Tt133CellKind { Text, Date, Money, Quantity }

/// Symbol: "A", "B", "1", "2"… Group: the upper-tier caption shared by neighbouring columns, null = no group.
public sealed record Tt133Column(string Symbol, string Header, double Width, Tt133CellKind Kind = Tt133CellKind.Text, string? Group = null);

public sealed class Tt133FormBuilder
{
    /// labelColumn (1-based): where summary labels go; null = the first Text column that has no Group.
    public Tt133FormBuilder(IXLWorksheet sheet, Tt133FormContext context, IReadOnlyList<Tt133Column> columns, bool landscape, int? labelColumn = null);

    public IXLWorksheet Sheet { get; }
    public int Row { get; }                 // the next free row (1-based)
    public int TableHeaderFirstRow { get; } // set by the latest WriteTableHeader
    public int TableHeaderLastRow { get; }  // the symbol row

    public void Skip(int rows = 1);                                            // leaves empty rows
    public void WriteHeaderBlock(string? formCode);
    public void WriteTitle(string title, string periodLine, params string[] parameterLines);
    public void WriteText(string text, bool bold = false, bool italic = false, bool centered = false);   // one row merged over all columns
    public void WriteNote(string note);                                        // WriteText(note, italic: true, centered: true)
    public void WriteTableHeader();                                            // may be called more than once (reconciliation)
    public void WriteRow(params object?[] cells);                              // one value per column; null = empty
    public void WriteSummaryRow(string label, params (int Column, object? Value)[] values);   // Column is 1-based
    public void WriteBookFooter();                                             // "Sổ này có…" + date + three signatures
    public void Finish(bool repeatTableHeader = true);                         // widths, page setup

    public static string PeriodLine(DateOnly from, DateOnly to);
    public static string SheetName(string code, ISet<string> used);            // adds the result to `used`
}
```

Layout rules (section-01 "Khung chung"):

- **Font:** Times New Roman 11 on the whole sheet (`sheet.Style.Font`), title 14 bold.
- **Header block** (three rows, then one empty row):
  - left, merged over the first half of the columns: `Đơn vị: {CompanyName}` when `BranchName` is null, else `Đơn vị: {CompanyName} – {BranchName}` (when `CompanyName` is empty the line is `Đơn vị: {BranchName}`, or `Đơn vị:` alone for scope Company); `Địa chỉ: {Address}`; `MST: {TaxCode}`. Missing values leave the label alone.
  - right, merged over the last third of the columns, centered, only when `formCode` is not null: `Mẫu số {formCode}` (bold); `(Ban hành theo Thông tư số 133/2016/TT-BTC`; `ngày 26/8/2016 của Bộ Tài chính)`.
- **Title:** upper case as given, bold, 14, centered over all columns. Next row: the period line, centered. Then one centered row per parameter line. `WriteNote` adds an italic centered row. One empty row follows before the table.
- **Period line** (T11): `PeriodLine(2026-10-01, 2026-10-31)` = "Tháng 10 năm 2026"; `(2026-02-01, 2026-02-28)` = "Tháng 02 năm 2026"; `(2026-01-01, 2026-12-31)` = "Năm 2026"; anything else "Từ ngày dd/MM/yyyy đến ngày dd/MM/yyyy".
- **Table header:** two rows plus the symbol row. A column with a `Group` shares one merged cell in the first row with its neighbours of the same group and has its own `Header` in the second row; a column without a group is merged over both rows. The third row holds `Symbol`, italic. All header cells: bold (except the symbol row), centered, wrapped, thin borders.
- **Body rows:** thin borders. `Money` → number format `#,##0;(#,##0)`, right-aligned; `Quantity` → `#,##0.###;(#,##0.###)`, right-aligned; `Date` → a `DateTime` value with format `dd/MM/yyyy`, centered; `Text` → wrapped. `decimal` values are written as numbers (not strings). A `DateOnly` is written as `DateTime` at midnight; a `DateTimeOffset` is first converted with `VnTime.ToVnDate`.
- **Summary rows:** the label goes in the label column (the first `Text` column without a `Group`, unless the constructor names another) and is bold; the values use the format of their column and are bold. In S04a and S05 that column is "Diễn giải".
- **Book footer** (after one empty row):
  - `- Sổ này có … trang, đánh số từ trang 01 đến trang …`
  - `- Ngày mở sổ: dd/MM/yyyy` (or `- Ngày mở sổ: …` when `OpeningDate` is null)
  - right-aligned over the last third: `Ngày dd tháng MM năm yyyy` from `PrintDate`, italic
  - three signature columns of equal width: titles **Người ghi sổ**, **Kế toán trưởng**, **Người đại diện theo pháp luật** (bold, centered); under them `(Ký, họ tên)`, `(Ký, họ tên)`, `(Ký, họ tên, đóng dấu)` (italic); four empty rows; then `BookkeeperName`, `ChiefAccountantName`, `LegalRepresentativeName` (bold, centered).
  - when `AccountingServiceNote` is not empty, one more row with it, italic, merged over all columns.
- **Finish:** column widths from `Tt133Column.Width`; `PageSetup.PaperSize = XLPaperSize.A4Paper`; orientation from `landscape`; `PageSetup.FitToPages(1, 0)` (one page wide, any height); `PageSetup.SetRowsToRepeatAtTop(TableHeaderFirstRow, TableHeaderLastRow)`; margins left/right/top/bottom 0.59 inch (1.5 cm).
- **Sheet name** (T12): replace each of `[ ] : * ? / \` with `_`, trim, cut to 31 characters; an empty result becomes `Sheet`; when the name is in `used` (case-insensitive), append ` (2)`, ` (3)`… cutting the base so the whole stays within 31 characters.

## Export pipeline contract

- **Exceptions** (`DomainException.cs`), both `sealed`, constructor `(string code, string message)`: `UnprocessableException` → 422, `ServiceUnavailableException` → 503. The middleware arms return `Code` and `Message` and sit above the generic `DomainException` arm. A 503 is logged as an error by the existing `>= 500` rule.
- **`Tt133ExportOptions`**: `SectionName = "Tt133Export"`, `MaxSheets = 300`. Registered in `Application/DependencyInjection.cs` (or where `FeatureOptions` is bound; follow that). `appsettings.json` gets `"Tt133Export": { "MaxSheets": 300 }`.
- **`Tt133ExportService`** (scoped) depends on `IAppDbContext`, `ICurrentUser`, `ICurrentBranch`, `IDateTime`, `ITransactionRunner`, `ICashDebtReportService`, `ITt133FormRenderer`, `ISpreadsheetPdfConverter`, `IOptions<Tt133ExportOptions>`. Private helpers, reused by every later form:
  - `BuildContextAsync(ReportScope scope, ct)`: reads `CompanyProfiles` (Id 1), the working branch (`Name`, `Address`, `OpeningDate`), `ICurrentUser.FullName`, `PrintDate = VnTime.ToVnDate(clock.UtcNow)`. Scope Branch: `BranchName` = the branch name, `Address` = the branch address or, when blank, the company address, `OpeningDate` = the branch's. Scope Company: `BranchName` null, `Address` = the company address, `OpeningDate` = the earliest non-null `OpeningDate` of all branches. Phase 02 passes `ReportScope.Branch`; **the enum itself is created in this phase** in `Application/Common/ReportScope.cs` (enum only — the guard is Phase 03).
  - `EnsureSheetCount(int count)`: 0 → `UnprocessableException("EXPORT_NO_TARGETS", "Không có đối tượng nào để xuất.")`; more than `MaxSheets` → `UnprocessableException("EXPORT_TOO_MANY_SHEETS", $"Số sổ cần xuất ({count}) vượt giới hạn {max}. Hãy thu hẹp điều kiện lọc.")`.
  - `ToFileAsync(byte[] xlsx, string baseName, ExportFormat format, ct)`: `Xlsx` → `ExportFile(xlsx, xlsxContentType, baseName + ".xlsx")`. `Pdf` → `ConvertAsync`; when it throws and `ct.IsCancellationRequested` is true the exception is rethrown (the client went away); any other exception, the converter's own timeout included, becomes `ServiceUnavailableException("PDF_EXPORT_FAILED", "Xuất PDF thất bại, hãy dùng Excel.")` (log the original with `ILogger`). Do not compare `OperationCanceledException.CancellationToken` with `ct`: the converter throws with its linked token; success → `ExportFile(pdf, "application/pdf", baseName + ".pdf")`.
  - Each `Export…Async` loads its data (the context, the targets, the report DTOs) inside `_transaction.RunSnapshotAsync` (T6), and renders the workbook and calls `ToFileAsync` after the snapshot has returned, so the transaction is not held while ClosedXML builds the sheets.
- **`ExportCashBookAsync(request, format)`**: `request.Kind` must be `Cash` or `Bank` (otherwise `ValidationDomainException` with key `kind`, "Chọn loại quỹ Tiền mặt hoặc Ngân hàng để xuất theo mẫu."); load `GetCashBookAsync(request)`; `EnsureSheetCount(report.Groups.Count)`; `RenderCashBook(context, report, kind)`; base name `S04a-DNN_{from:yyyyMMdd}_{to:yyyyMMdd}` or `S05-DNN_…`.
- **Controller** `Tt133ExportController : ApiControllerBase`, `[Route("api/reports")]`:
  ```csharp
  [HttpGet("cash-book/export")]
  [HasPermission(Permissions.Reports.Cash)]
  public async Task<IActionResult> CashBook([FromQuery] CashBookReportRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
  {
      await _cashBookValidator.ValidateAndThrowAsync(request, ct);
      var file = await _exports.ExportCashBookAsync(request, format, ct);
      return File(file.Bytes, file.ContentType, file.FileName);
  }
  ```
  `format` is optional and defaults to `Xlsx`; `format=pdf` and `format=xlsx` bind case-insensitively; an unknown value is the framework's 400.

## Cash forms (`CashBookForm`)

One sheet per `CashBookGroupDto`, in the report's order; sheet name = `SheetName(group.FundCode, used)`; portrait.

**S04a-DNN** (`kind == FundKind.Cash`) — title `SỔ QUỸ TIỀN MẶT`, parameter line `Loại quỹ: {FundName}`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | Ngày, tháng ghi sổ | Date | `At` |
| 2 | B | — | Ngày, tháng chứng từ | Date | `At` |
| 3 | C | Số hiệu chứng từ | Thu | Text | `ReceiptCode`, or `TransferCode` when `SourceType == TransferIn` |
| 4 | D | Số hiệu chứng từ | Chi | Text | `PaymentCode`, or `TransferCode` when `SourceType == TransferOut` |
| 5 | E | — | Diễn giải | Text | `Description` (left out when it equals `ReasonName`), plus ` – {PartnerOrPerson}` when present; the part that is empty is left out with its dash (T9) |
| 6 | F | — | Lý do | Text | `ReasonName` |
| 7 | 1 | Số tiền | Thu | Money | `In` (empty when 0) |
| 8 | 2 | Số tiền | Chi | Money | `Out` (empty when 0) |
| 9 | 3 | Số tiền | Tồn | Money | `Running` |
| 10 | G | — | Ghi chú | Text | empty |

Summary rows: `Số tồn đầu kỳ` (column 9 = `Opening`) before the rows; after them `Cộng số phát sinh trong kỳ` (column 7 = `TotalIn`, column 8 = `TotalOut`) and `Số tồn cuối kỳ` (column 9 = `Closing`).

**S05-DNN** (`kind == FundKind.Bank`) — title `SỔ TIỀN GỬI NGÂN HÀNG`, parameter lines `Nơi mở tài khoản giao dịch: {BankName}` and `Số hiệu tài khoản tại nơi gửi: {AccountNumber}`.

| # | Symbol | Group | Header | Kind | Value |
|---|---|---|---|---|---|
| 1 | A | — | Ngày, tháng ghi sổ | Date | `At` |
| 2 | B | Chứng từ | Số hiệu | Text | `ReceiptCode ?? PaymentCode ?? TransferCode` |
| 3 | C | Chứng từ | Ngày, tháng | Date | `At` |
| 4 | D | — | Diễn giải | Text | as S04a column E |
| 5 | E | — | Lý do | Text | `ReasonName` |
| 6 | 1 | Số tiền | Thu (gửi vào) | Money | `In` (empty when 0) |
| 7 | 2 | Số tiền | Chi (rút ra) | Money | `Out` (empty when 0) |
| 8 | 3 | Số tiền | Còn lại | Money | `Running` |
| 9 | F | — | Ghi chú | Text | empty |

Summary rows: `Số dư đầu kỳ` (column 8), `Cộng số phát sinh trong kỳ` (columns 6 and 7), `Số dư cuối kỳ` (column 8).

Both forms end with `WriteBookFooter()` and `Finish()`.

## Dataset numbers (Round 2A `ReportDataset`, main branch, 10-01..10-31)

- **QTM** (cash, name "Quỹ tiền mặt"): opening 10,000. Rows: `PT00001` 10-03 in 1,000 → 11,000 (automatic receipt, description "Thu tiền theo phiếu xuất PX00001"); `PT00002` 10-05 in 4,000 → 15,000 (reason `TKH` "Thu tiền khách hàng"); `CQ00001` 10-15 transfer in 5,000 → 20,000; `PC00003` 10-16 out 700 → 19,300 (reason `CK` "Chi khác", person "Nguyễn A"). Total in 10,000, total out 700, closing 19,300. The cancelled `PT00003` is absent.
- **NH01** (bank): opening 20,000. Rows: `PC00001` 10-10 out 2,000 → 18,000; `PC00002` 10-12 out 3,000 → 15,000 (reason `TNCC`); `CQ00001` 10-15 transfer out 5,000 → 10,000. Total in 0, total out 10,000, closing 10,000.

## Tasks

### Task 2.1 — Builder: header block, title, period line

1. **Write the failing tests** `Tt133/Unit/Tt133FormBuilderTests.cs` (no collection). A private `NewBuilder(context, landscape: false)` creates an `XLWorkbook`, one sheet and a builder with four columns: `A "Ngày" Date`, `B "Diễn giải" Text`, `1 "Thu" Money Group "Số tiền"`, `2 "Chi" Money Group "Số tiền"`. A private `Context()` returns `CompanyName "Công ty ABC"`, `BranchName "Chi nhánh HN"`, `Address "12 Lê Lợi"`, `TaxCode "0101234567"`, `BookkeeperName "Lê C"`, `ChiefAccountantName "Trần B"`, `LegalRepresentativeName "Nguyễn A"`, `OpeningDate 2026-10-01`, `PrintDate 2026-10-13`. Tests:
   - `Header_block_prints_unit_address_tax_code_and_form_code`: after `WriteHeaderBlock("S04a-DNN")`, the used cells contain "Đơn vị: Công ty ABC – Chi nhánh HN", "Địa chỉ: 12 Lê Lợi", "MST: 0101234567", "Mẫu số S04a-DNN" (bold) and "(Ban hành theo Thông tư số 133/2016/TT-BTC".
   - `Header_block_for_company_scope_prints_the_company_only`: `BranchName = null` → "Đơn vị: Công ty ABC".
   - `Header_block_without_form_code_has_no_form_lines`: `WriteHeaderBlock(null)` → no cell contains "Mẫu số" or "Thông tư".
   - `Header_block_with_an_empty_profile_keeps_the_labels`: `CompanyName`, `Address`, `TaxCode` null and `BranchName = "Chi nhánh HN"` → cells "Đơn vị: Chi nhánh HN", "Địa chỉ:", "MST:".
   - `Title_block`: `WriteTitle("SỔ QUỸ TIỀN MẶT", "Tháng 10 năm 2026", "Loại quỹ: Quỹ tiền mặt")` → the title cell is bold, size 14, centered, merged over the four columns; the next two rows hold the period line and the parameter line; `WriteNote("Giá vốn tạm tính")` adds an italic centered row.
   - `Text_line_is_merged_over_all_columns`: `WriteText("Hôm nay, chúng tôi gồm:", bold: true)` writes a bold, left-aligned cell merged over the four columns and advances `Row` by one; `Skip(2)` advances `Row` by two.
   - `Period_line` (`[Theory]`): the five cases of the layout rules (include `2026-10-01..2026-11-30` → "Từ ngày 01/10/2026 đến ngày 30/11/2026").
   - `Sheet_uses_times_new_roman_11`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~Tt133FormBuilderTests"`. Expected: FAIL — build error, `Tt133FormBuilder` / `Tt133FormContext` do not exist.
3. **Write the minimal implementation:** `Application/Tt133/Models/Tt133ExportModels.cs` with `Tt133FormContext` only; `Tt133Column.cs`; `Tt133FormBuilder` with the constructor, `Sheet`, `Row`, `Skip`, `WriteHeaderBlock`, `WriteTitle`, `WriteText`, `WriteNote`, `PeriodLine`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): form builder header and title"` (after `git add` of the three source files and the test file).

### Task 2.2 — Builder: table header, rows, summary rows

1. **Write the failing tests** in `Tt133FormBuilderTests.cs`:
   - `Table_header_merges_groups_and_writes_the_symbol_row`: after `WriteTableHeader()`, with `r = TableHeaderFirstRow`: the cell (r, 1) "Ngày" is merged over rows r..r+1; (r, 3) holds "Số tiền" merged over columns 3..4; (r+1, 3) "Thu" and (r+1, 4) "Chi"; row r+2 holds "A", "B", "1", "2" in italics; `TableHeaderLastRow == r + 2`; header cells have thin outside borders and are bold.
   - `Rows_write_typed_values`: `WriteRow(new DateOnly(2026, 10, 3), "Thu tiền", 1_000m, null)` → the date cell is a `DateTime` 2026-10-03 with format `dd/MM/yyyy`; the money cell is the number 1000 (`GetValue<decimal>()`) with format `#,##0;(#,##0)` and right alignment; the null cell is empty; all four cells have borders.
   - `Row_converts_an_instant_to_the_vn_date`: `WriteRow(new DateTimeOffset(2026, 10, 3, 18, 0, 0, TimeSpan.Zero), …)` → the cell date is 2026-10-04.
   - `Quantity_cells_use_the_quantity_format`: a builder with one `Quantity` column writes `1.5m` with format `#,##0.###;(#,##0.###)`.
   - `Summary_row_is_bold_and_uses_the_label_column`: `WriteSummaryRow("Cộng số phát sinh trong kỳ", (3, 10_000m), (4, 700m))` → the label is in column 2 (the first ungrouped `Text` column) and bold; columns 3 and 4 hold bold numbers 10000 and 700. A builder whose first `Text` column has a `Group` and whose second has none puts the label in the second; a builder created with `labelColumn: 1` puts it in column 1.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — build error, `WriteTableHeader` does not exist.
3. **Write the minimal implementation:** `WriteTableHeader`, `TableHeaderFirstRow`, `TableHeaderLastRow`, `WriteRow`, `WriteSummaryRow` and the cell-kind formatting.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): form builder table"`

### Task 2.3 — Builder: book footer, page setup, sheet names

1. **Write the failing tests** in `Tt133FormBuilderTests.cs`:
   - `Book_footer_prints_pages_opening_date_print_date_and_signers`: after `WriteBookFooter()` the used cells contain "- Sổ này có … trang, đánh số từ trang 01 đến trang …", "- Ngày mở sổ: 01/10/2026", "Ngày 13 tháng 10 năm 2026", the three titles, "(Ký, họ tên, đóng dấu)", and the names "Lê C", "Trần B", "Nguyễn A"; each name is in the same column as its title and below it.
   - `Book_footer_without_an_opening_date_prints_dots`: `OpeningDate = null` → "- Ngày mở sổ: …".
   - `Book_footer_adds_the_accounting_service_note`: `AccountingServiceNote = "Dịch vụ kế toán: Công ty XYZ"` → a cell with that text; without the note no such row.
   - `Finish_sets_the_page_setup` (`[Theory]` landscape false/true): `PaperSize == XLPaperSize.A4Paper`; `PageOrientation` matches; `PagesWide == 1` and `PagesTall == 0`; `FirstRowToRepeatAtTop == TableHeaderFirstRow` and `LastRowToRepeatAtTop == TableHeaderLastRow`; `Margins.Left` is 0.59 (± 0.01); column 1 has the width given in its `Tt133Column`.
   - `Finish_without_repeat_leaves_no_rows_to_repeat`: `Finish(repeatTableHeader: false)` → `FirstRowToRepeatAtTop == 0`.
   - `Sheet_name` (`[Theory]`): `"QTM"` → `"QTM"`; `"KH/01:A"` → `"KH_01_A"`; a 40-character code → its first 31 characters; `""` → `"Sheet"`; the same code twice → the second is `"QTM (2)"`; a 31-character code twice → the second ends with `" (2)"` and is 31 characters long; `"qtm"` after `"QTM"` → `"qtm (2)"`.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — build error, `WriteBookFooter` does not exist.
3. **Write the minimal implementation:** `WriteBookFooter`, `Finish`, `SheetName`. If ClosedXML rejects `FitToPages(1, 0)`, set `PageSetup.PagesWide = 1` and leave `PagesTall` at 0 through the property; the test states the outcome, not the call.
4. **Run the tests to verify they pass:** same filter. Expected: PASS (all builder tests).
5. **Commit:** `git commit -m "feat(tt133): form builder footer and page setup"`

### Task 2.4 — `ReasonName` on cash book rows

1. **Write the failing tests:**
   - Backend, in `CashDebt/Reports/CashBookReportTests.cs`, a new test `Rows_carry_the_reason_name`: seed the dataset, `kind=Cash` for October → `qtm.Rows.Select(r => r.ReasonName)` equals `[autoReason, "Thu tiền khách hàng", "Chuyển quỹ", "Chi khác"]`, where `autoReason` is read through `InDbAsync` (`CashVouchers.IgnoreQueryFilters()` of `ids.AutoReceiptId`, its `Reason.Name`). `kind=Bank` → the `CQ00001` row has `"Chuyển quỹ"`.
   - Frontend, in `pages/cash-reports/cash-book-page.test.tsx`: add `reasonName` to the mocked rows ("Thu tiền khách hàng", "Trả tiền NCC", "Chuyển quỹ"); the table has a column header "Lý do" and the first row shows "Thu tiền khách hàng".
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashBookReportTests"` → FAIL — build error, `CashBookRowDto.ReasonName` does not exist. `npx vitest run src/pages/cash-reports/cash-book-page.test.tsx` → FAIL — no "Lý do" header.
3. **Write the minimal implementation:**
   - `FundMovement` gets a `string? ReasonName` member (append it as the last positional parameter with a default of `null`, so the other construction sites compile): the voucher's `ReasonName` (already selected), `"Chuyển quỹ"` for both transfer halves, null for the opening amount. `Description` keeps its fallback to the reason name.
   - `CashBookRowDto.ReasonName` (`string?`, with the comment "Reason name of the voucher; a transfer half is "Chuyển quỹ"."), set in `BuildCashBookAsync`.
   - Frontend: `reasonName?: string | null` on `CashBookRow`; a "Lý do" column after "Diễn giải" in `FundGroupSection` (adjust the three `colSpan` values and `min-w`).
4. **Run the tests to verify they pass:** the two commands above, plus `--filter "FullyQualifiedName~CashDebt"` (the fund guard shares `FundMovementQuery`). Expected: PASS.
5. **Commit:** `git commit -m "feat(reports): reason name on cash book rows"`

### Task 2.5 — Export pipeline and S04a

1. **Write the failing tests:**
   - `Tt133/Tt133Sheet.cs` (test helper, `internal static class`): `RowOf(IXLWorksheet ws, string text)` returns the row number of the first used cell whose string **equals** `text` (throws with the text when none); `Contains(ws, text)`; `Number(ws, row, column)` = `ws.Cell(row, column).GetValue<decimal>()`; `OpenAsync(HttpResponseMessage)` returns the `XLWorkbook` of a response body.
   - `Tt133/CashBookExportTests.cs` (inherits `CashDebtTestBase`), test `S04a_for_the_cash_fund`: seed the dataset; set the company profile through `InDbAsync` (`Name "Công ty ABC"`, `TaxCode "0101234567"`, `ChiefAccountantName "Trần B"`, `LegalRepresentativeName "Nguyễn A"`); `GET /api/reports/cash-book/export?kind=Cash&from=2026-10-01&to=2026-10-31` (no `format`) with `MainBranchClient(ids)`:
     - 200, content type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`, `Content-Disposition` file name `S04a-DNN_20261001_20261031.xlsx`;
     - one worksheet named `QTM`;
     - cells "Mẫu số S04a-DNN", "SỔ QUỸ TIỀN MẶT", "Tháng 10 năm 2026", "Loại quỹ: Quỹ tiền mặt", and "Đơn vị: Công ty ABC – Chi nhánh RD…" (the main branch's name, read through `InDbAsync`);
     - the row `RowOf("Số tồn đầu kỳ")` has 10,000 in column 9;
     - the four rows after it: codes `PT00001`, `PT00002`, `CQ00001` in column 3 (the transfer in is a receipt-side code) and `PC00003` in column 4; column 6 of the transfer row is "Chuyển quỹ"; columns 7/8/9 are (1,000 / empty / 11,000), (4,000 / empty / 15,000), (5,000 / empty / 20,000), (empty / 700 / 19,300); column 1 of the first row is the date 2026-10-03; column 5 of the last row is "Nguyễn A" and its column 6 is "Chi khác" (the voucher has no description of its own, so the reason is printed once, T9); column 5 of the transfer row is not empty (a transfer's description differs from "Chuyển quỹ");
     - `RowOf("Cộng số phát sinh trong kỳ")` has 10,000 in column 7 and 700 in column 8; `RowOf("Số tồn cuối kỳ")` has 19,300 in column 9;
     - the footer has "Kế toán trưởng", "Trần B", "Nguyễn A", the admin's full name (from `Users`) and "- Ngày mở sổ: 01/10/2026";
     - page setup: portrait, A4.
   - `File_equals_the_screen_report`: for `kind=Cash`, the closing cell equals `Closing` of `GET /api/reports/cash-book?kind=Cash&…` and the number of body rows equals `Rows.Count`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashBookExportTests"`. Expected: FAIL — 404 (no route) once the helper compiles.
3. **Write the minimal implementation:** the two exceptions and their middleware arms; `ReportScope` (enum only); `ExportFormat`, `ExportFile`; `Tt133ExportOptions` and its binding; `ITt133FormRenderer` with `RenderCashBook`; `ITt133ExportService` with `ExportCashBookAsync`; `Tt133ExportService` with `BuildContextAsync`, `EnsureSheetCount`, `ToFileAsync` and `ExportCashBookAsync` (without the `kind` rule — Task 2.7; until then `Kind == Bank` renders as `FundKind.Bank` and every other value as `FundKind.Cash`); `CashBookForm` (S04a branch only; for `FundKind.Bank` throw `NotSupportedException` until Task 2.6); `Tt133FormRenderer` (creates the workbook, calls `CashBookForm`, returns the bytes through a `MemoryStream`); DI (`ITt133ExportService` scoped in Application, `ITt133FormRenderer` scoped in Infrastructure); the controller action.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): export pipeline and S04a cash book form"`

### Task 2.6 — S05 bank book

1. **Write the failing tests** in `CashBookExportTests.cs`:
   - `S05_for_the_bank_funds`: seed the dataset; set `BankName = "Vietcombank"`, `AccountNumber = "0123456789"` on NH01 through `InDbAsync`; create a second bank fund `NH02` (`CreateBankFundAsync("NH02", ids.MainBranchId, opening: 7_000m)`); `GET …/cash-book/export?kind=Bank&from=2026-10-01&to=2026-10-31&format=xlsx`:
     - file name `S05-DNN_20261001_20261031.xlsx`; two worksheets `NH01`, `NH02` in that order;
     - sheet NH01: "Mẫu số S05-DNN", "SỔ TIỀN GỬI NGÂN HÀNG", "Nơi mở tài khoản giao dịch: Vietcombank", "Số hiệu tài khoản tại nơi gửi: 0123456789"; `Số dư đầu kỳ` 20,000 in column 8; rows `PC00001`, `PC00002`, `CQ00001` in column 2 with (empty / 2,000 / 18,000), (empty / 3,000 / 15,000), (empty / 5,000 / 10,000) in columns 6/7/8; `Cộng số phát sinh trong kỳ` 0 and 10,000; `Số dư cuối kỳ` 10,000;
     - sheet NH02: opening 7,000, no body rows, closing 7,000.
   - `One_fund_only`: `kind=Bank&fundId={NH01}` → one worksheet.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — 500 (`NotSupportedException`).
3. **Write the minimal implementation:** the S05 branch of `CashBookForm`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): S05 bank book form"`

### Task 2.7 — Export validation, permission, limits and PDF

1. **Write the failing tests:**
   - In `CashBookExportTests.cs`, `Validation_permission_and_limits`:
     - `kind=All` (or no `kind`) → 400 with `details` key `kind`;
     - `from=2026-10-31&to=2026-10-01` → 400 key `to` (the report validator);
     - `kind=Cash&fundId={NH01}` → 400 key `fundId` (the report's own rule);
     - a client with only `reports.debt` → 403;
     - `kind=Bank` on a fresh branch that has no bank fund (`CreateBranchAsync`, `UseBranch`) → 422 with `error.code == "EXPORT_NO_TARGETS"`;
     - with `Tt133Export:MaxSheets` = 1: a `WebAppFactory` subclass in the test file adds the in-memory setting, in the way the test project raises `RateLimiting:LoginPermitLimit`. That factory has its own database clone, so the base class helpers do not reach it: log in as `admin` on it, create a second cash fund through `POST /api/funds` (request shape as in `CashDebt/FundCrudTests.cs`), then `kind=Cash` → 422 `EXPORT_TOO_MANY_SHEETS`.
   - `Tt133/ExportPdfTests.cs` with two file-local factories modelled on `WebAppFactoryWithFakePdfConverter` in `Quotations/QuotationExportTests.cs` (replace `ISpreadsheetPdfConverter`; log in as `admin` the way that file does):
     - `Pdf_returns_the_converted_bytes`: the fake returns `"%PDF-1.0 fake\n"u8`; `GET /api/reports/cash-book/export?kind=Cash&from=2026-10-01&to=2026-10-31&format=pdf` → 200, content type `application/pdf`, file name `S04a-DNN_20261001_20261031.pdf`, body equal to the fake bytes; the fake received a non-empty xlsx (first two bytes `PK`). The admin's default branch has its default cash fund, so no seeding is needed (if the export answers 422 `EXPORT_NO_TARGETS`, create a cash fund through `POST /api/funds` on that factory first).
     - `Pdf_failure_is_503`: the converter throws `InvalidOperationException("LibreOffice executable path is not configured.")` → 503 with `error.code == "PDF_EXPORT_FAILED"` and message "Xuất PDF thất bại, hãy dùng Excel.".
     - If the login or factory code copied from `QuotationExportTests` fails for the reason its own PDF test fails in `BASELINE.md`, find that reason first and report it; do not work around it silently.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashBookExportTests|FullyQualifiedName~ExportPdfTests"`. Expected: FAIL — `kind=All` returns 200 with every fund (no `kind` rule yet). The other cases may already pass through Task 2.5's helpers; the task is complete when all pass.
3. **Write the minimal implementation:** the `kind` rule in `ExportCashBookAsync`; anything of `EnsureSheetCount` / `ToFileAsync` that Task 2.5 left out.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git commit -m "feat(tt133): export validation, limits and PDF conversion"`

### Task 2.8 — Frontend: download helper, period presets, export button

1. **Write the failing tests:**
   - `lib/blob-error.test.ts`: `unwrapBlobError(error)` given an axios error whose `response.data` is a `Blob` of type `application/json` holding `{"success":false,"error":{"code":"EXPORT_NO_TARGETS","message":"Không có đối tượng nào để xuất."}}` resolves to the same error with `response.data` replaced by the parsed object, so `getApiError(result)?.code` is `EXPORT_NO_TARGETS`; a non-blob error and a blob that is not JSON come back unchanged.
   - `lib/period-presets.test.ts`: `periodPreset(key, today)` with `today = '2026-10-13'`: `thisMonth` → `2026-10-01..2026-10-31`; `lastMonth` → `2026-09-01..2026-09-30`; `thisQuarter` → `2026-10-01..2026-12-31`; `lastQuarter` → `2026-07-01..2026-09-30`; `thisYear` → `2026-01-01..2026-12-31`; `lastYear` → `2025-01-01..2025-12-31`; with `today = '2026-01-15'`: `lastMonth` → `2025-12-01..2025-12-31`, `lastQuarter` → `2025-10-01..2025-12-31`; `today = '2024-03-10'`: `lastMonth` → `2024-02-01..2024-02-29`.
   - `components/period-preset-select/period-preset-select.test.tsx`: choosing "Quý trước" calls `onChange({ from: '2026-07-01', to: '2026-09-30' })` (pass `today` as a prop for the test).
   - `features/tt133-export/api.test.ts` (mock `@/lib/api-client` default export): `downloadTt133('cash-book', 'pdf', { kind: 'Cash', from: '2026-10-01', to: '2026-10-31' }, 'S04a-DNN_20261001_20261031')` calls `api.get('/reports/cash-book/export', { params: { kind: 'Cash', from: '2026-10-01', to: '2026-10-31', format: 'pdf' }, responseType: 'blob', timeout: 120000 })` and clicks an anchor with `download = 'S04a-DNN_20261001_20261031.pdf'`; for the multi-target reports (`debt-ledger`, `stock-card`, `stock-detail`) it calls `api.post('/reports/debt-ledger/export', { side: 'Receivable', from, to, partnerIds: ['a', 'b'] }, { params: { format: 'xlsx' }, responseType: 'blob', timeout: 120000 })` instead (the request in the JSON body, `format` in the query, T3); a blob error is rethrown unwrapped.
   - `components/tt133-export/tt133-export-button.test.tsx` (mock `@/features/tt133-export/api` and `@/features/company-profile/hooks`):
     - one form → the menu "Xuất theo mẫu TT133" lists "Excel" and "PDF"; clicking "PDF" calls `downloadTt133(form.report, 'pdf', form.params, form.fileBase)`;
     - two forms → the items are "S04a-DNN · Excel", "S04a-DNN · PDF", "S05-DNN · Excel", "S05-DNN · PDF";
     - `onSelect` given → clicking an item calls `onSelect(form, format)` and does not download;
     - an empty `forms` list or `disabled` → the trigger is disabled;
     - a rejected download shows a destructive toast with the server message ("Xuất PDF thất bại, hãy dùng Excel.");
     - company name empty → the hint "Chưa có thông tin đơn vị" is shown, with a link "Cài đặt" to `/settings/company` only for a user with `settings.company`; name present → no hint.
   - `features/tt133-export/use-tt133-download.test.tsx`: `useTt133Download()` returns `{ download, isPending }`; `download(form, 'xlsx', { all: true })` calls `downloadTt133(form.report, 'xlsx', { ...form.params, all: true }, form.fileBase)`; a rejection shows the destructive toast and does not throw; `isPending` is true while the call runs.
2. **Run the tests to verify they fail:** `npx vitest run src/lib/blob-error.test.ts src/lib/period-presets.test.ts src/components/period-preset-select src/features/tt133-export src/components/tt133-export`. Expected: FAIL — the modules cannot be imported.
3. **Write the minimal implementation:**
   - `lib/blob-error.ts`: `export async function unwrapBlobError(error: unknown): Promise<unknown>`.
   - `lib/period-presets.ts`: `export type PeriodPresetKey = 'thisMonth' | 'lastMonth' | 'thisQuarter' | 'lastQuarter' | 'thisYear' | 'lastYear'`; `PERIOD_PRESET_LABELS` ("Tháng này", "Tháng trước", "Quý này", "Quý trước", "Năm nay", "Năm trước"); `periodPreset(key, today = todayYmd()): { from: string; to: string }` computed on the `yyyy-MM-dd` string parts (no `new Date(string)` parsing, no `toISOString`). `lib/date-range-presets.ts` already has `this-month` / `last-month` for the quotation filter; leave it alone and do not reuse it: it works on the browser's local `Date` and has no quarter or year, and this module must follow the VN date of `todayYmd`.
   - `components/period-preset-select/period-preset-select.tsx`: a `Select` with placeholder "Chọn nhanh kỳ" and label "Kỳ"; props `{ id: string; onChange: (range: { from: string; to: string }) => void; today?: string }`.
   - `features/tt133-export/types.ts`: `export type Tt133Report = 'cash-book' | 'debt-ledger' | 'debt-balance' | 'debt-reconciliation' | 'stock-summary' | 'stock-detail' | 'stock-card'`; `export type ExportFormat = 'xlsx' | 'pdf'`; `export interface Tt133ExportForm { code: string; report: Tt133Report; params: Record<string, unknown>; fileBase: string }`; `export function tt133FileBase(code: string, from: string, to: string)` → `` `${code}_${from.replace(/-/g, '')}_${to.replace(/-/g, '')}` ``.
   - `features/tt133-export/api.ts`: `downloadTt133(report, format, params, fileBase)` as the test describes (anchor download as in `features/reports/sales-revenue-detail/api.ts`); `export const TT133_POST_REPORTS: Tt133Report[] = ['debt-ledger', 'stock-card', 'stock-detail']` decides between GET and POST.
   - `features/tt133-export/use-tt133-download.ts`: the hook of the test above; the error toast has the title "Không thể xuất file" and the description from `formatApiErrorDetails`. Pages that pick targets first (Phases 03 and 04) call it directly.
   - `components/tt133-export/tt133-export-button.tsx`: props `{ forms: Tt133ExportForm[]; onSelect?: (form: Tt133ExportForm, format: ExportFormat) => void; disabled?: boolean }`; `components/ui/dropdown-menu`; trigger button "Xuất theo mẫu TT133" with the emerald export icon colour of the conventions; downloads through `useTt133Download` (pending state on the trigger) unless `onSelect` is given; the company-profile hint.
4. **Run the tests to verify they pass:** the vitest command above, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git add <the files above> && git commit -m "feat(frontend): TT133 export button and period presets"`

### Task 2.9 — Frontend: export on the Sổ quỹ page

1. **Write the failing tests** in `pages/cash-reports/cash-book-page.test.tsx` (mock `@/components/tt133-export/tt133-export-button` with a stub that renders the codes of its `forms` and exposes them, and `@/features/company-profile/hooks`):
   - kind "Tất cả", no fund → forms `S04a-DNN` (`params: { kind: 'Cash', from, to }`) and `S05-DNN` (`params: { kind: 'Bank', from, to }`), file bases `S04a-DNN_{from}_{to}` / `S05-DNN_{from}_{to}` without dashes;
   - kind "Tiền mặt" → only `S04a-DNN`; kind "Ngân hàng" → only `S05-DNN`;
   - a bank fund selected → only `S05-DNN` with `fundId` in its params;
   - an invalid range (from > to) → the button is disabled;
   - choosing the preset "Tháng trước" sets Từ ngày / Đến ngày to the previous month.
2. **Run the tests to verify they fail:** `npx vitest run src/pages/cash-reports/cash-book-page.test.tsx`. Expected: FAIL — no export button on the page.
3. **Write the minimal implementation:** in `cash-book-page.tsx`, a header row with the title and `<Tt133ExportButton forms={forms} disabled={!!rangeError} />`; `forms` computed from `kind`, the selected fund's kind (from `funds`) and the dates; `<PeriodPresetSelect>` in the filter card.
4. **Run the tests to verify they pass:** same command, then `npm run typecheck`. Expected: PASS.
5. **Commit:** `git commit -m "feat(frontend): TT133 export on the cash book page"`

### Task 2.10 — Phase checkpoint

1. Run the full backend suite and the frontend `lint`, `test`, `build`; compare with `BASELINE.md`.
2. If a new failure appears, write a focused failing test, fix, re-run.
3. Commit only if a fix was needed: `git commit -m "fix(tt133): <what>"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133"
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~CashDebt"
dotnet test OrderMgmt.sln   # compare with BASELINE.md
cd ../frontend
npm run typecheck && npm run lint && npm run test && npm run build
```

## Exit Criteria

- The builder tests pass and cover header, title, period line, table, summary rows, footer, page setup and sheet names.
- `GET api/reports/cash-book/export` returns S04a for `kind=Cash` and S05 for `kind=Bank`, one sheet per fund, with the dataset numbers; the file's closing equals the screen report's.
- 400 / 403 / 422 (`EXPORT_NO_TARGETS`, `EXPORT_TOO_MANY_SHEETS`) / 503 (`PDF_EXPORT_FAILED`) behave as specified; PDF goes through `ISpreadsheetPdfConverter`.
- The Sổ quỹ screen shows "Lý do" and the export button with the right forms.
- **The builder API, `Tt133ExportService` helpers, `Tt133ExportButton` props and `downloadTt133` are frozen for Phases 03–04.**
- No new failures against `BASELINE.md`.
