# Phase 01 — Shared export support and cash voucher forms, backend

**Status:** [ ] pending
**Complexity:** L

## Objective

- Move the form context, the sheet-count check and the file/PDF step out of `Tt133ExportService` into `Tt133ExportSupport` (V2), without changing the report exports.
- Add the voucher print building blocks: models, pure rules, `VoucherFormBuilder` (V4), `IVoucherFormRenderer`.
- Print receipts (01-TT) and payments (02-TT) through `GET api/cash-vouchers/{id}/print` and `POST api/cash-vouchers/print`, with the access and error rules of V6.

## Files

Backend (`backend/`):

- `src/OrderMgmt.Application/Tt133/Services/Tt133ExportSupport.cs` (new)
- `src/OrderMgmt.Application/Tt133/Services/Tt133ExportService.cs` (modify)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintModels.cs` (new)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintRules.cs` (new)
- `src/OrderMgmt.Application/Tt133/Vouchers/IVoucherFormRenderer.cs` (new)
- `src/OrderMgmt.Application/Tt133/Vouchers/IVoucherPrintService.cs` (new)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintService.cs` (new)
- `src/OrderMgmt.Application/DependencyInjection.cs` (modify)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormRenderer.cs` (modify — `Render` becomes `internal static`)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/VoucherFormBuilder.cs` (new)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/CashVoucherForm.cs` (new)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/VoucherFormRenderer.cs` (new)
- `src/OrderMgmt.Infrastructure/DependencyInjection.cs` (modify)
- `src/OrderMgmt.WebApi/Controllers/CashVouchersController.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Tt133ExportSupportTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/VoucherPrintRulesTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/VoucherFormBuilderTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/CashVoucherFormTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/CashVoucherPrintTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/DashboardSignalTests.cs` (modify)

## Reference files (read-only)

- `src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherService.cs` — `LoadDtoAsync` (the `IgnoreQueryFilters` + manual `!IsDeleted` pattern), `EnsurePermission`, `EnsureWorkingBranchAsync`.
- `src/OrderMgmt.Application/CashDebt/CashVouchers/Services/CashVoucherPermissions.cs`.
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormBuilder.cs`, `DebtReconciliationForm.cs` (a form that writes its own blocks next to the builder).
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Tt133FormBuilderTests.cs` (`Context()`, `Texts`, `CellWith` helpers; page-setup assertions).
- `tests/OrderMgmt.IntegrationTests/Tt133/CashBookExportTests.cs` (`WebAppFactoryWithOneSheet`), `Tt133/ExportPdfTests.cs` (`WebAppFactoryWithConverter`, `RecordingConverter`, `AdminClientAsync`).
- `tests/OrderMgmt.IntegrationTests/CashDebt/CashVouchers/CashVoucherLifecycleTests.cs` (`BranchClientAsync`, `NewCode`), `CashDebt/CashDebtTestBase.cs` (`CashRequest`, `CreateCashAsync`, `CashUpdateFrom`, `PutCashAsync`, `CancelCashAsync`, `DeleteCashAsync`, `InsertCashVoucherAsync`).

## Form layout (01-TT / 02-TT)

Columns: ten columns of width 9.5, all `Text` (symbols `"1"`…`"10"`, empty headers; there is no table). Writing order of `CashVoucherForm` for one model `m` (`r` = receipt):

1. `WriteHeaderBlock(r ? "01-TT" : "02-TT")`.
2. `WriteTitleBlock(r ? "PHIẾU THU" : "PHIẾU CHI", m.Date, m.IsCancelled, [], [$"Quyển số: {Dots}", $"Số: {m.Code}", $"Nợ: {Dots}", $"Có: {Dots}"])`.
3. `WriteLine(r ? "Họ và tên người nộp tiền:" : "Họ và tên người nhận tiền:", m.PersonName)`.
4. `WriteLine("Địa chỉ:", m.Address)`.
5. `WriteLine(r ? "Lý do nộp:" : "Lý do chi:", m.Reason)`.
6. `WriteLine("Số tiền:", $"{VoucherPrintRules.Money(m.Amount)} đồng")`.
7. `WriteLine("(Viết bằng chữ):", m.AmountInWords)`.
8. `Table.WriteText(m.AttachedDocsLine)`.
9. `WriteSignatures(m.Date, signers)` with, for 01-TT: Giám đốc `(Ký, họ tên, đóng dấu)` `context.LegalRepresentativeName`; Kế toán trưởng `(Ký, họ tên)` `context.ChiefAccountantName`; Người nộp tiền `(Ký, họ tên)` null; Người lập phiếu `(Ký, họ tên)` `m.PreparerName`; Thủ quỹ `(Ký, họ tên)` null. For 02-TT: Giám đốc, Kế toán trưởng, Thủ quỹ, Người lập phiếu, Người nhận tiền (same hints and names by title).
10. `WriteLine("Đã nhận đủ số tiền (viết bằng chữ):", null)`.
11. `Finish()` (A5 landscape, V12).

`VoucherFormBuilder` geometry (columns `count`): the right part is the last `Math.Max(2, count * 2 / 5)` columns, the left part the columns before it.

- `WriteHeaderBlock(code)`: three rows. Left: `"Đơn vị: {unit}"`, `"Địa chỉ: {Address}"`, empty, where `unit` is `"{CompanyName} – {BranchName}"`, or the one of the two that is set (a label alone when both are blank, as `Tt133FormBuilder.WriteHeaderBlock`). Right (centred): `"Mẫu số {code}"` (bold), `"(Ban hành theo Thông tư số 133/2016/TT-BTC"`, `"ngày 26/8/2016 của Bộ Tài chính)"`. No MST line. A text longer than its merged columns wraps and its row gets the height of its lines (`Tt133FormBuilder.LineCount`). Then one empty row.
- `WriteTitleBlock`: centre (left part, centred) from the current row: the title (bold, size 14), `"ĐÃ HỦY"` (bold) only when cancelled, `"Ngày {dd} tháng {MM} năm {yyyy}"` (italic), then `centreLines`; right part from the same first row: `rightLines`, one per row, left-aligned. Then skip to the row after the longer of the two lists, plus one empty row.
- `WriteLine(label, value)`: `Table.WriteText($"{label} {value.Trim()}")`, or `$"{label} {Dots}"` when the value is blank.
- `WriteSignatures(date, signers)`: one empty row; `"Ngày {dd} tháng {MM} năm {yyyy}"` (italic, right-aligned, merged over the right part); a row of titles (bold, centred), one block of columns per signer; a row of hints (italic); four empty rows; a row of names (bold; empty when null). Blocks are split **by width**: with `c[j]` the summed width of columns 1…`j` (`c[0] = 0`) and `W = c[count]`, boundary `b[0] = 0`, `b[n] = count`, and for `0 < k < n`, `b[k]` is the smallest `j` with `c[j] >= k * W / n - 0.001`, then kept within `b[k-1] + 1 … count - (n - k)` (every block has a column). Block `k` spans columns `b[k] + 1` … `b[k + 1]`. Ten 9.5-wide columns give two columns per signer; the 02-VT columns (widths 5, 34, 12, 8, 10, 10, 13, 15) give blocks starting at columns 1, 3, 4, 6, 8 (widths 39, 12, 18, 23, 15) instead of a 5-wide STT block. The title, hint and name cells are centred and wrap; each of those three rows gets `lines * Sheet.RowHeight`, with `lines` the largest `Tt133FormBuilder.LineCount(text, blockWidth, bold)` of the row (at least 1).
- `Finish()`: `Table.Finish(repeatTableHeader: Table.TableHeaderFirstRow > 0)`; for `A5Landscape` then `PaperSize = A5Paper`, `PageOrientation = Landscape`, `FitToPages(1, 1)`. `A4Portrait` keeps what `Tt133FormBuilder.Finish` set (A4, portrait, `FitToPages(1, 0)`). The `Tt133FormBuilder` is created with `landscape: paper == A5Landscape`.

## Tasks

### Task 1.1 — `Tt133ExportSupport`

1. **Write the failing tests** in `Tt133/Unit/Tt133ExportSupportTests.cs` (plain class, no collection). Build the support with `new Tt133ExportSupport(null!, null!, null!, null!, converter, Options.Create(new Tt133ExportOptions { MaxSheets = 2 }), NullLogger<Tt133ExportSupport>.Instance)` and a private fake `ISpreadsheetPdfConverter` that returns given bytes or throws a given exception:
   - `Zero_sheets_is_no_targets`: `EnsureSheetCount(0, (n, max) => "x")` throws `UnprocessableException` with `Code == "EXPORT_NO_TARGETS"` and `Message == "Không có đối tượng nào để xuất."`.
   - `Too_many_sheets_uses_the_callers_message`: `EnsureSheetCount(3, (n, max) => $"{n}>{max}")` throws `UnprocessableException` with `Code == "EXPORT_TOO_MANY_SHEETS"`, `Message == "3>2"`; `EnsureSheetCount(2, …)` does not throw.
   - `Xlsx_is_returned_as_is`: `ToFileAsync([1, 2, 3], "A", ExportFormat.Xlsx, default)` → bytes `[1, 2, 3]`, `ContentType == Tt133ExportSupport.XlsxContentType`, `FileName == "A.xlsx"`; the converter was not called.
   - `Pdf_is_converted`: the converter returns `[9]` → bytes `[9]`, `"application/pdf"`, `"A.pdf"`.
   - `Pdf_failure_is_503`: the converter throws `InvalidOperationException` → `ServiceUnavailableException` with `Code == "PDF_EXPORT_FAILED"`, `Message == "Xuất PDF thất bại, hãy dùng Excel."`.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133ExportSupportTests"`. Expected: FAIL — build error, `Tt133ExportSupport` does not exist.
3. **Write the minimal implementation:**
   - `Tt133ExportSupport` with the members of the shared contract. Move the bodies of `BuildContextAsync`, `EnsureSheetCount` and `ToFileAsync` and the two content-type constants from `Tt133ExportService` unchanged, except that the too-many message now comes from the caller.
   - `Tt133ExportService`: take `Tt133ExportSupport support` in the constructor instead of `ICurrentBranch`, `IDateTime`, `ISpreadsheetPdfConverter`, `IOptions<Tt133ExportOptions>` and `ILogger<Tt133ExportService>` (keep the others it still uses); call `_support.…` everywhere; pass `private static string SheetsTooMany(int count, int max) => $"Số sổ cần xuất ({count}) vượt giới hạn {max}. Hãy thu hẹp điều kiện lọc.";`. Keep `BaseName` where it is.
   - `Application/DependencyInjection.cs`: `services.AddScoped<Tt133ExportSupport>();` next to `ITt133ExportService`.
4. **Run the tests to verify they pass:** the filter of step 2, then `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133"` (every report export test still passes). Expected: PASS.
5. **Commit:** `git add <the three source files (Tt133ExportSupport.cs, Tt133ExportService.cs, Application/DependencyInjection.cs) and the test file> && git commit -m "refactor(tt133): shared export support for reports and vouchers"`

### Task 1.2 — Print models and the cash rules

1. **Write the failing tests** in `Tt133/Unit/Vouchers/VoucherPrintRulesTests.cs`:
   - `FirstFilled`: `(null, "  ", " B ")` → `"B"`; `(null, " ")` → `null`.
   - `CashReason` (theory):
     - `("Thu tiền hàng", ["Dòng 1"], "Thu khác")` → `"Thu tiền hàng"`;
     - `(null, ["Dòng 1", "Dòng 2"], "Thu khác")` → `"Dòng 1; Dòng 2"`;
     - `(" ", ["", " ", "Dòng 3"], "Thu khác")` → `"Dòng 3"` (blank line texts are skipped);
     - `("", ["", " "], "Thu khác")` → `"Thu khác"`;
     - `(null, [], null)` → `null`.
   - `AttachedDocsLine` (theory): `"2"` → `"Kèm theo: 2 chứng từ gốc"`; `" 02 "` → `"Kèm theo: 2 chứng từ gốc"`; `"HĐ 0001234"` → `"Kèm theo: HĐ 0001234"`; `null` and `"  "` → `"Kèm theo: …………………… chứng từ gốc"`.
   - `Money`: `1_500_000m` → `"1.500.000"`; `0m` → `"0"`; `1_234.6m` → `"1.235"`.
   - `BatchFileBase("PhieuThu", new DateTimeOffset(2026, 10, 14, 9, 5, 0, TimeSpan.Zero))` → `"PhieuThu_20261014-1605"`.
   - `Dots` equals `"……………………"` (ten `…` characters).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~VoucherPrintRulesTests"`. Expected: FAIL — build error, `VoucherPrintRules` does not exist.
3. **Write the minimal implementation:**
   - `VoucherPrintModels.cs` with `StockPrintPriceMode`, `CashVoucherPrintRequest` and `CashVoucherPrintModel` (the Phase 01 members of the shared contract; the stock models come in Phase 03).
   - `VoucherPrintRules` with `Dots`, `FirstFilled`, `CashReason`, `AttachedDocsLine` (whole number: `int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out n)`), `Money` (`Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.GetCultureInfo("vi-VN"))`) and `BatchFileBase` (`utcNow.ToOffset(VnTime.Offset)` formatted `yyyyMMdd-HHmm`, invariant culture).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the two source files and the test file> && git commit -m "feat(tt133): voucher print models and cash rules"`

### Task 1.3 — `VoucherFormBuilder`

1. **Write the failing tests** in `Tt133/Unit/Vouchers/VoucherFormBuilderTests.cs`. Helpers: `Context()` (CompanyName "Công ty ABC", BranchName "Chi nhánh HN", Address "12 Láng Hạ", TaxCode "0101234567", ChiefAccountantName "Trần B", LegalRepresentativeName "Nguyễn A", PrintDate 2026-10-14), `NewBuilder(context, paper, columns)` on `new XLWorkbook().Worksheets.Add("V1")`, `Texts(sheet)` and `CellWith(sheet, text)` as in `Tt133FormBuilderTests`. Default columns: ten `new Tt133Column($"{i}", "", 9.5)`.
   - `Header_has_unit_address_and_form_code_but_no_tax_code`: texts contain `"Đơn vị: Công ty ABC – Chi nhánh HN"`, `"Địa chỉ: 12 Láng Hạ"`, `"Mẫu số 01-TT"` (bold), `"(Ban hành theo Thông tư số 133/2016/TT-BTC"`, `"ngày 26/8/2016 của Bộ Tài chính)"`; no text starts with `"MST"`; the form code cell's column is greater than the unit cell's column.
   - `Header_without_company_profile`: an empty context (only PrintDate) → `"Đơn vị:"` and `"Địa chỉ:"`.
   - `Title_block_puts_the_right_lines_next_to_the_title`: `WriteTitleBlock("PHIẾU THU", 2026-10-05, false, [], ["Quyển số: …", "Số: PT00001", "Nợ: …", "Có: …"])` → `"PHIẾU THU"` bold, font size 14; `"Ngày 05 tháng 10 năm 2026"` one row below it, italic; `"Quyển số: …"` on the title's row and `"Số: PT00001"`, `"Nợ: …"`, `"Có: …"` on the three rows after it, in a column right of the title; no `"ĐÃ HỦY"`; `Table.Row` is after the last of those rows plus one empty row.
   - `Cancelled_title_shows_ĐÃ_HỦY_under_the_title`: same call with `cancelled: true` and centre line `"Số: PN00001"` → `"ĐÃ HỦY"` bold on the row under the title, the date on the row after it, `"Số: PN00001"` after the date.
   - `Line_prints_the_value_or_dots`: `WriteLine("Địa chỉ:", " Hà Nội ")` → `"Địa chỉ: Hà Nội"`; `WriteLine("Lý do:", null)` → `"Lý do: ……………………"`.
   - `Signatures_in_order_with_hints_and_names`: five signers (Giám đốc / `(Ký, họ tên, đóng dấu)` / "Nguyễn A", Kế toán trưởng / `(Ký, họ tên)` / "Trần B", Người nộp tiền / `(Ký, họ tên)` / null, Người lập phiếu / `(Ký, họ tên)` / "Lê C", Thủ quỹ / `(Ký, họ tên)` / null) on 2026-10-05 → `"Ngày 05 tháng 10 năm 2026"` italic above the titles; the five titles on one row, bold, in columns 1, 3, 5, 7, 9 (two per signer); each hint on the next row in its signer's column; "Nguyễn A", "Trần B" and "Lê C" on one row below the hints, each in its signer's column.
   - `Signature_blocks_follow_the_column_widths`: columns of widths 5, 34, 12, 8, 10, 10, 13, 15 (the 02-VT table), five signers → the titles start in columns 1, 3, 4, 6, 8 and no block is narrower than 12; with `"Nguyễn Văn Trường Giang"` as the second signer's name (the 12-wide block), the names row is higher than `sheet.RowHeight` and that cell wraps.
   - `A5_landscape_is_one_page`: `Finish()` with `VoucherPaper.A5Landscape` → `PaperSize == XLPaperSize.A5Paper`, `PageOrientation == Landscape`, `PagesWide == 1`, `PagesTall == 1`, column 1 width 9.5, `FirstRowToRepeatAtTop == 0`.
   - `A4_portrait_repeats_the_table_header`: columns `A` / `B` / `1` (group "Số lượng") / `2` (group "Số lượng"), `VoucherPaper.A4Portrait`, `Table.WriteTableHeader()`, `Finish()` → `A4Paper`, `Portrait`, `PagesWide == 1`, `PagesTall == 0`, `FirstRowToRepeatAtTop == Table.TableHeaderFirstRow`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~VoucherFormBuilderTests"`. Expected: FAIL — build error, `VoucherFormBuilder` does not exist.
3. **Write the minimal implementation:** `VoucherPaper`, `VoucherSigner` and `VoucherFormBuilder` as in the shared contract and the geometry above. Write cells through `Sheet` at `Table.Row + i` and move on with `Table.Skip(n)`, as `DebtReconciliationForm.WriteSignatures` does; a private `Merged(row, first, last)` helper like the one in `Tt133FormBuilder`. `Tt133FormBuilder` stays unchanged.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~Tt133.Unit"`. Expected: PASS.
5. **Commit:** `git add <VoucherFormBuilder.cs and the test file> && git commit -m "feat(tt133): voucher form builder"`

### Task 1.4 — 01-TT and 02-TT forms and the voucher renderer

1. **Write the failing tests** in `Tt133/Unit/Vouchers/CashVoucherFormTests.cs`, rendering with `new VoucherFormRenderer().RenderCashVouchers(Context(), models)` and opening the bytes with `new XLWorkbook(new MemoryStream(bytes))`. `Receipt()` = Type Receipt, Code "PT00001", Date 2026-10-05, PersonName "Nguyễn Văn A", Address "Hà Nội", Reason "Thu tiền hàng", Amount 1,500,000, AmountInWords "Một triệu năm trăm nghìn đồng", AttachedDocsLine "Kèm theo: 2 chứng từ gốc", PreparerName "Quản trị hệ thống".
   - `Receipt_is_01_TT`: one worksheet `"PT00001"`; texts contain `"Mẫu số 01-TT"`, `"PHIẾU THU"`, `"Ngày 05 tháng 10 năm 2026"`, `"Quyển số: ……………………"`, `"Số: PT00001"`, `"Nợ: ……………………"`, `"Có: ……………………"`, `"Họ và tên người nộp tiền: Nguyễn Văn A"`, `"Địa chỉ: Hà Nội"`, `"Lý do nộp: Thu tiền hàng"`, `"Số tiền: 1.500.000 đồng"`, `"(Viết bằng chữ): Một triệu năm trăm nghìn đồng"`, `"Kèm theo: 2 chứng từ gốc"`; the titles row reads, by column, `"Giám đốc"`, `"Kế toán trưởng"`, `"Người nộp tiền"`, `"Người lập phiếu"`, `"Thủ quỹ"`; `"Nguyễn A"`, `"Trần B"`, `"Quản trị hệ thống"` sit in the columns of Giám đốc, Kế toán trưởng and Người lập phiếu; `"Đã nhận đủ số tiền (viết bằng chữ): ……………………"` is on a row below the names; no `"ĐÃ HỦY"`; A5, landscape, 1 × 1 pages (no existing test reads `PagesWide` / `PagesTall` back from a saved workbook; if ClosedXML does not, keep those two in `VoucherFormBuilderTests` only, which checks them in memory, and say so in the phase report).
   - `Payment_is_02_TT`: Type Payment, Code "PC00001" → `"Mẫu số 02-TT"`, `"PHIẾU CHI"`, `"Họ và tên người nhận tiền: Nguyễn Văn A"`, `"Lý do chi: Thu tiền hàng"`; titles by column `"Giám đốc"`, `"Kế toán trưởng"`, `"Thủ quỹ"`, `"Người lập phiếu"`, `"Người nhận tiền"`.
   - `Blank_fields_print_dots`: PersonName, Address and Reason null → `"Họ và tên người nộp tiền: ……………………"`, `"Địa chỉ: ……………………"`, `"Lý do nộp: ……………………"`.
   - `Cancelled_voucher_shows_ĐÃ_HỦY`: IsCancelled true → `"ĐÃ HỦY"` present.
   - `One_sheet_per_voucher_in_the_given_order`: models `PT00002`, `PT00001`, `PT00001` → worksheets `"PT00002"`, `"PT00001"`, `"PT00001 (2)"`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherFormTests"`. Expected: FAIL — build error, `VoucherFormRenderer` does not exist.
3. **Write the minimal implementation:**
   - `IVoucherFormRenderer` with `RenderCashVouchers` only (Phase 03 adds the stock method).
   - `Tt133FormRenderer.Render`: `private static` → `internal static`.
   - `CashVoucherForm.Write(XLWorkbook workbook, Tt133FormContext context, IReadOnlyList<CashVoucherPrintModel> vouchers)`: one sheet per model, `Tt133FormBuilder.SheetName(model.Code, used)`, written as in "Form layout" above.
   - `VoucherFormRenderer : IVoucherFormRenderer` → `Tt133FormRenderer.Render(wb => CashVoucherForm.Write(wb, context, vouchers))`.
   - `Infrastructure/DependencyInjection.cs`: `services.AddScoped<IVoucherFormRenderer, VoucherFormRenderer>();`.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~Tt133.Unit"`. Expected: PASS.
5. **Commit:** `git add <the source files and the test file> && git commit -m "feat(tt133): 01-TT and 02-TT forms"`

### Task 1.5 — Print one cash voucher (`GET …/{id}/print`)

Test class `Tt133/Vouchers/CashVoucherPrintTests.cs : CashDebtTestBase`, `[Collection(nameof(PostgresCollection))]`. Private helpers:
- `BranchClientAsync()` — copy of `CashVoucherLifecycleTests.BranchClientAsync` (L316-322) with prefix `"VP"`, together with the private `Oct1` (L20) and `NewCode` (L331) it uses: a new branch opened 10-01, an admin client working in it, its `QTM` fund; then `await SetFundOpeningAmountAsync(fundId, 10_000_000m)` (a new branch's fund holds nothing, and a payment taking it below 0 answers 422 `NEGATIVE_FUND_WARNING`); returns `(BranchId, Client, FundId, BranchName)` with the name from `InDbAsync(db => db.Branches.Where(b => b.Id == id).Select(b => b.Name).SingleAsync())`.
- `SetCompanyAsync()` — row 1 of `CompanyProfiles`: Name "Công ty ABC", Address "1 Láng Hạ", ChiefAccountantName "Trần B", LegalRepresentativeName "Nguyễn A".
- `PrintAsync(client, id, format = "xlsx")` — `GET /api/cash-vouchers/{id}/print?format={format}`.
- `Texts(ws)` — every used cell's string.

1. **Write the failing tests:**
   - `Prints_a_receipt_as_01_TT`: `SetCompanyAsync`; a customer `CreatePartnerAsync(NewCode("KH"), true, false)`; `CashRequest(Receipt, await CashReasonIdAsync("TK"), fundId, "2026-10-05 09:00", partnerId, 1_000_000m, 500_000m)` with `PersonName = "Nguyễn Văn A"`, `AttachedDocs = "2"`; `CreateCashAsync`. Print → 200; `ContentType.MediaType` = the xlsx type; `ContentDisposition.FileName == $"{v.Code}.xlsx"`; one worksheet named `v.Code`; texts contain `"PHIẾU THU"`, `"Mẫu số 01-TT"`, `$"Đơn vị: Công ty ABC – {branchName}"`, `"Địa chỉ: 1 Láng Hạ"`, `$"Số: {v.Code}"`, `"Ngày 05 tháng 10 năm 2026"`, `"Họ và tên người nộp tiền: Nguyễn Văn A"`, `"Địa chỉ: Hà Nội"`, `"Lý do nộp: Dòng 1; Dòng 2"`, `"Số tiền: 1.500.000 đồng"`, `"(Viết bằng chữ): Một triệu năm trăm nghìn đồng"`, `"Kèm theo: 2 chứng từ gốc"`, `"Nguyễn A"`, `"Trần B"` and `v.OwnerName`.
   - `Prints_a_payment_as_02_TT`: `CashRequest(Payment, await CashReasonIdAsync("CK"), fundId, "2026-10-06 10:00", null, 200_000m)` with `PersonName = "Lê C"`, `Description = "Chi tiếp khách"` → `"PHIẾU CHI"`, `"Mẫu số 02-TT"`, `"Họ và tên người nhận tiền: Lê C"`, `"Lý do chi: Chi tiếp khách"`, `"Số tiền: 200.000 đồng"`, `"Kèm theo: …………………… chứng từ gốc"`.
   - `Falls_back_to_the_partner_name_and_text_attachments`: a receipt with a partner, no `PersonName`, `AttachedDocs = "HĐ 0001234"` → `$"Họ và tên người nộp tiền: Đối tượng {partnerCode}"`, `"Kèm theo: HĐ 0001234"`.
   - `Removed_lines_do_not_print`: a receipt with two lines; `CashUpdateFrom(v)`, `Lines.RemoveAt(1)`, `PutCashAsync` → 200; print → `"Lý do nộp: Dòng 1"` and `"Số tiền: 1.000.000 đồng"`.
   - `Soft_deleted_reason_and_owner_keep_their_names`: an owner client `CreateClientWithPermissionsAsync("vpo_" + suffix, branchId, Permissions.Receipts.View, Permissions.Receipts.Create)` with `UseBranch(owner, branchId)` creates the receipt (reason TK); read the owner's `FullName` from `db.Users` by username; through `InDbAsync`: set every line's `Description` of the voucher to `""` (`db.CashVoucherLines`), set `IsDeleted = true` on the TK reason and on the owner user. Print with the admin client → `"Lý do nộp: Thu khác"` and the owner's full name.
   - `Cancelled_receipt_prints_ĐÃ_HỦY`: `CancelCashAsync(client, v.Id, new CashDocumentActionRequest { Version = v.Version })` → print 200 with `"ĐÃ HỦY"`.
   - `Prints_without_a_company_profile`: no `SetCompanyAsync` → 200 and `$"Đơn vị: {branchName}"`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherPrintTests"`. Expected: FAIL — 404 (route does not exist).
3. **Write the minimal implementation:**
   - `IVoucherPrintService` with `PrintCashVouchersAsync` only.
   - `VoucherPrintService(IAppDbContext db, ICurrentUser currentUser, ICurrentBranch currentBranch, IDateTime clock, ITransactionRunner transaction, Tt133ExportSupport support, IVoucherFormRenderer renderer)`. `PrintCashVouchersAsync`: de-duplicate `ids`; inside `_transaction.RunSnapshotAsync`: `_support.BuildContextAsync(ReportScope.Branch, token)` and one projection of `_db.CashVouchers.IgnoreQueryFilters().AsNoTracking().Where(v => ids.Contains(v.Id) && !v.IsDeleted)` selecting `Id, Type, Code, VoucherAt, BranchId, Status, PersonName, PartnerName, PartnerAddress, Description, AttachedDocs, Total`, `ReasonName = v.Reason!.Name`, `OwnerName = v.Owner != null ? v.Owner.FullName : null`, `LineDescriptions = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(l => l.Description).ToList()`. Map each row (V9): `Date = VnTime.ToVnDate(VoucherAt)`, `IsCancelled = Status == DocumentStatus.Cancelled`, `PersonName = FirstFilled(PersonName, PartnerName)`, `Address = FirstFilled(PartnerAddress)`, `Reason = CashReason(Description, LineDescriptions, ReasonName)`, `Amount = Total`, `AmountInWords = VndAmountInWords.Read(Total)`, `AttachedDocsLine = AttachedDocsLine(AttachedDocs)`, `PreparerName = OwnerName`. Order by `VoucherAt`, then `Code` (`StringComparer.Ordinal`). After the snapshot: `_renderer.RenderCashVouchers`, base name `models[0].Code` for one voucher (the batch name comes in Task 1.6), `_support.ToFileAsync`. **No** access checks and no `EnsureSheetCount` yet (Task 1.7).
   - `Application/DependencyInjection.cs`: `services.AddScoped<IVoucherPrintService, VoucherPrintService>();`.
   - `CashVouchersController`: inject `IVoucherPrintService`; action
     ```csharp
     /// 01-TT / 02-TT of one voucher as Excel or PDF.
     [HttpGet("{id:guid}/print")]
     public async Task<IActionResult> Print(Guid id, [FromQuery] ExportFormat format, CancellationToken ct)
     {
         var file = await _print.PrintCashVouchersAsync(new[] { id }, format, ct);
         return File(file.Bytes, file.ContentType, file.FileName);
     }
     ```
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the source files and the test file> && git commit -m "feat(vouchers): print one receipt or payment"`

### Task 1.6 — Batch print, file names, PDF (`POST …/print`)

1. **Write the failing tests** in `CashVoucherPrintTests`:
   - `Prints_selected_vouchers_in_date_order_once_each`: receipts `R1` at `"2026-10-06 08:00"`, `R2` at `"2026-10-05 09:00"`, `R3` at `"2026-10-05 09:00"` (created in that order, so `R2.Code` < `R3.Code`); `POST /api/cash-vouchers/print?format=xlsx` with `new CashVoucherPrintRequest { Ids = [R1.Id, R3.Id, R2.Id, R3.Id] }` → 200; worksheets `[R2.Code, R3.Code, R1.Code]`; file name matches `^PhieuThu_\d{8}-\d{4}\.xlsx$`.
   - `Batch_names_follow_the_types`: two payments → `^PhieuChi_\d{8}-\d{4}\.xlsx$`; a receipt and a payment → `^PhieuThuChi_\d{8}-\d{4}\.xlsx$`, one sheet with `"PHIẾU THU"` and one with `"PHIẾU CHI"`.
   - `Pdf_goes_through_the_converter`: a `file sealed class WebAppFactoryWithConverter` and `RecordingConverter` copied from `Tt133/ExportPdfTests.cs` (fake bytes `"%PDF-1.0 fake\n"u8`), and its `AdminClientAsync`. Insert one receipt straight into that factory's database (copy the body of `CashDebtTestBase.InsertCashVoucherAsync` against `factory.Services.CreateScope()`'s `AppDbContext`: main branch `MainBranchId`, its QTM fund, reason TK, `Code = "PTPDF1"`, one line, the admin user as owner, Total 1,000); `UseBranch(client, MainBranchId)`; `GET /api/cash-vouchers/{id}/print?format=pdf` → 200, `application/pdf`, file name `"PTPDF1.pdf"`, body equals the fake bytes, the converter received bytes starting with `PK`.
   - In `ExecutiveDashboard/DashboardSignalTests.cs`, `WithoutHost.Every_document_controller_is_tagged` (L172-205): the skipped actions must now be `stockAt` and the `CashVouchersController` method whose `HttpPostAttribute.Template == "print"`. Replace the ordered `.Should().Equal(stockAt)` with `.Should().BeEquivalentTo(new[] { stockAt, cashPrint })` (the order of `GetTypes()` / `GetMethods()` is not fixed) and update the comment above it.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherPrintTests|FullyQualifiedName~DashboardSignalTests"`. Expected: FAIL — 405 / 404 on the POST, and the reflection test fails (`Single` finds no `print` action).
3. **Write the minimal implementation:**
   - `CashVouchersController`:
     ```csharp
     /// Several vouchers, one sheet each. A POST because a list of ids does not fit a URL; it changes nothing.
     [HttpPost("print")]
     [SkipDashboardSignal]
     public async Task<IActionResult> PrintMany(
         [FromBody] CashVoucherPrintRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
     {
         var file = await _print.PrintCashVouchersAsync(request.Ids, format, ct);
         return File(file.Bytes, file.ContentType, file.FileName);
     }
     ```
   - `VoucherPrintService`: more than one voucher → `VoucherPrintRules.BatchFileBase(prefix, _clock.UtcNow)` with prefix `PhieuThu` (all receipts), `PhieuChi` (all payments), `PhieuThuChi` (both).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <controller, service, the two test files> && git commit -m "feat(vouchers): print several receipts and payments"`

### Task 1.7 — Access and error rules

1. **Write the failing tests** in `CashVoucherPrintTests`:
   - `Unknown_or_deleted_voucher_is_404`: `GET …/{Guid.NewGuid()}/print` → 404; a receipt deleted with `DeleteCashAsync(client, v.Id, new CashDocumentActionRequest { Version = v.Version })` → 404.
   - `One_unknown_id_fails_the_batch`: `POST …/print` with `[validReceipt, Guid.NewGuid()]` → 404.
   - `Missing_view_permission_is_403`: a receipt; `var payer = await CreateClientWithPermissionsAsync("vpp_" + suffix, branchId, Permissions.Payments.View); UseBranch(payer, branchId);` → `GET` the receipt → 403; `POST` `[payment, receipt]` → 403; a client with `Permissions.Receipts.View` only → `GET` the receipt → 200.
   - `Another_branch_is_403`: a receipt in branch A; a second branch B (`BranchClientAsync` again) → printing A's receipt with B's client → 403 and `Error.Message == "Phiếu thuộc chi nhánh khác."`.
   - `Empty_batch_is_422`: `POST` with `Ids = []` → 422, `Error.Code == "EXPORT_NO_TARGETS"`.
   - `Too_many_is_422`: a `file sealed class WebAppFactoryWithOneSheet` copied from `CashBookExportTests` (`Tt133Export:MaxSheets = 1`) and an admin client on it; `POST` with two random ids → 422, `EXPORT_TOO_MANY_SHEETS`, message `"Số phiếu cần in (2) vượt giới hạn 1. Hãy chọn ít phiếu hơn."` (the count is checked before any read).
   - `Null_ids_is_422`: `POST` with the raw JSON body `{"ids":null}` → 422 `EXPORT_NO_TARGETS` (not a 500).
   - `Bad_format_is_400`: `GET …/print?format=docx` → 400 (status only: model binding answers with `ValidationProblemDetails`).
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~CashVoucherPrintTests"`. Expected: FAIL — the 404/403/422 cases answer 200 or 500 (`bad format` may already pass through model binding; that one is a guard, not a RED).
3. **Write the minimal implementation** in `PrintCashVouchersAsync`:
   - `distinct = (ids ?? []).Distinct().ToList()` (a JSON `null` binds `Ids` to null);
   - before the snapshot: `_support.EnsureSheetCount(distinct.Count, VouchersTooMany)` with `private static string VouchersTooMany(int count, int max) => $"Số phiếu cần in ({count}) vượt giới hạn {max}. Hãy chọn ít phiếu hơn.";`;
   - inside the snapshot, after the projection, for each id in request order: missing → `NotFoundException(nameof(CashVoucher), id)`; `!_currentUser.HasPermission(CashVoucherPermissions.View(row.Type))` → `ForbiddenException()`; `row.BranchId != await _currentBranch.GetIdAsync(token)` → `ForbiddenException("Phiếu thuộc chi nhánh khác.")`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <service and test file> && git commit -m "feat(vouchers): access rules of cash voucher printing"`

### Task 1.8 — Phase checkpoint

1. `dotnet build OrderMgmt.sln`.
2. `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~DashboardSignal|FullyQualifiedName~CashVoucher"`.
3. Full suite with the trx logger (Executor conventions); compare the failures with `BASELINE.md` test by test. A new failure is fixed (failing test first) before the phase ends.
4. Mark Phase 01 `[x]` in this file and in `SUMMARY.md`; commit `git add docs/plans/261014-1632-tt133-voucher-print-forms && git commit -m "chore(plan): phase 01 complete"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133"
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardSignal|FullyQualifiedName~CashVoucher"
dotnet test OrderMgmt.sln --logger "trx;LogFileName=phase01.trx" --results-directory "$TEMP/vprint-trx"   # compare with BASELINE.md
```

## Exit Criteria

- The report exports pass unchanged on top of `Tt133ExportSupport`.
- `GET api/cash-vouchers/{id}/print` and `POST api/cash-vouchers/print` return 01-TT / 02-TT as xlsx or pdf with the content of V9, in the order and with the names of V8, and the errors of V6.
- `DashboardSignalTests` lists the new POST action as a skipped one.
- No failure beyond `BASELINE.md`.
