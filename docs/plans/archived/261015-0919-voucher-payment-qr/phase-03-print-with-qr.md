# Phase 03 — QR on the printed vouchers and the print switch

**Status:** [x] completed
**Complexity:** XL

## Objective

- Backend: `includeQr` on the four print routes; `QrPngRenderer` (QRCoder); the QR on 01-TT (beside the content lines, A5 landscape) and on 02-VT (a payment block with the QR on the right, A4 portrait), in Excel and PDF, single and bulk.
- Frontend: "Kèm QR chuyển khoản" in the receipt print menu and in the stock-out price dialog, remembered per user (default on), sent as `includeQr`.

Needs Phase 02 (`IVoucherPaymentQrService.ResolveManyAsync`). Phase 04 is independent of this phase. Tasks 3.7–3.9 (frontend) mock the API and do not depend on 3.1–3.6, so they may run first or in a second worktree.

## Files

Backend (`backend/`):

- `src/OrderMgmt.Infrastructure/OrderMgmt.Infrastructure.csproj` (modify — `QRCoder`)
- `src/OrderMgmt.Infrastructure/Qr/QrPngRenderer.cs` (new)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintModels.cs` (modify — `Qr`, `IncludeQr`)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintRules.cs` (modify — QR line helpers)
- `src/OrderMgmt.Application/Tt133/Vouchers/IVoucherPrintService.cs`, `VoucherPrintService.cs` (modify)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormBuilder.cs` (modify — `WriteText` gets `lastColumn`)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/VoucherFormBuilder.cs`, `CashVoucherForm.cs`, `StockVoucherForm.cs` (modify)
- `src/OrderMgmt.WebApi/Controllers/CashVouchersController.cs`, `StockVouchersController.cs` (modify — print actions only)
- `tests/OrderMgmt.IntegrationTests/OrderMgmt.IntegrationTests.csproj` (modify — `ZXing.Net`, `ZXing.Net.Bindings.SkiaSharp`)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/QrPngRendererTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/VoucherPrintRulesTests.cs`, `VoucherFormBuilderTests.cs`, `CashVoucherFormTests.cs`, `StockVoucherFormTests.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/VoucherPrintQrTests.cs` (new)
- existing tests that call `PrintCashVouchersAsync` / `PrintStockVouchersAsync` with a positional `ct` (fix the compile errors)

Frontend (`frontend/src/`):

- `features/voucher-print/qr-pref-storage.ts`, `qr-pref-storage.test.ts` (new)
- `features/voucher-print/types.ts`, `api.ts`, `api.test.ts`, `use-voucher-print.test.tsx` (modify)
- `components/voucher-print/voucher-print-button.tsx`, `voucher-print-button.test.tsx` (modify)
- `components/voucher-print/stock-price-mode-dialog.tsx`, `stock-price-mode-dialog.test.tsx` (modify)

## Reference files (read-only)

- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/VoucherFormBuilder.cs` — `WriteLine`, `WriteRow`, `WriteSignatures`, `Finish`, `KeepSignaturesOnOnePage` (the A4 scale formula: `A4WidthPoints`, `MarginPoints`, `SavedWidthPadding`, `PointsPerWidthUnit`), `Width`, `Merged`, `SetRowLines`.
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Tt133FormBuilder.cs` — `WriteText`, `WrapMerged`, `LineCount`, `Row`, `Skip`, `Sheet.RowHeight`.
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/CashVoucherForm.cs` (10 columns of 9.5) and `StockVoucherForm.cs` (columns A…, last two are Đơn giá and Thành tiền).
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/CashVoucherFormTests.cs` (`Context()`, `Receipt()`, `Render`, `Texts`, `CellWith`, `RowOf`, `ColumnOf`), `StockVoucherFormTests.cs`, `VoucherFormBuilderTests.cs`.
- `tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/CashVoucherPrintTests.cs`, `StockVoucherPrintTests.cs` (`SetCompanyAsync`, `PrintAsync`, `Texts`, how a batch POST is made).
- Brainstorm mockups `docs/brainstorms/261015-0656-voucher-payment-qr/mockup-01-print-placement.html` (the A5 and A4 layouts; chữ "Còn phải thu" became "Cần chuyển khoản", the A4 QR is on the right).
- Frontend: `components/ui/dropdown-menu.tsx` (`DropdownMenuCheckboxItem`, `DropdownMenuSeparator` exports), `features/voucher-print/price-mode-storage.ts` + test (the model for `qr-pref-storage`), `stores/auth-store.ts` (`user.id`).

## Layout rules

**Size.** `QrPrintMillimetres = 25`. Sheet points = `25 * 72 / 25.4 / printScale`, pixels = `round(points / 0.75)` (Q7). A4: `printScale` is the value `KeepSignaturesOnOnePage` computes (extract it as `A4PrintScale()`). A5 landscape: `A5LandscapeScale = 0.90` until Task 3.6 measures it. Private to `VoucherFormBuilder`; tests check the picture is square and in a sane range, and Task 3.6 measures it on a real PDF.

**01-TT (A5 landscape, 10 columns), QR beside the lines.**
- `BeginQrBeside(qr)` before the first content line; the lines from "Họ và tên người nộp tiền" to "Kèm theo" are written through `builder.WriteLine` / `builder.WriteText`, which merge only columns 1–8 while the block is open (they wrap by that width and get explicit row heights).
- `EndQrBeside()` after "Kèm theo": columns 9–10 hold, from the first line's row: the picture (`QrPixels` square, horizontally centred in columns 9–10, 2 px below the top of the row), then in the last two rows of the block two merged, centred, 8 pt, shrink-to-fit cells: `QrScanCaption` (bold) and `QrBankCaption(qr)`. If the rows of the block are not tall enough (`sum of heights of rows first … last-2 >= picture points + 2`, at least 3 rows), blank default-height rows are added after the lines until they are (brainstorm R7: at least one extra row is expected). The signatures follow unchanged.
- `Qr == null`: nothing changes (all ten columns, no extra rows).

**02-VT (A4 portrait), payment block.**
- After "- Số chứng từ gốc kèm theo:", when `Qr != null`: a block with a thin outside border over all columns. Left (merged from column 1 to the column before "Đơn giá"), one row each: `THÔNG TIN CHUYỂN KHOẢN` (bold); `Ngân hàng: {BankName}`; `Số tài khoản: {AccountNumber}`; `Chủ tài khoản: {AccountHolderName}`; `QrAmountLine` (bold); `QrBreakdownLine` only when not null; `Nội dung chuyển khoản: {Content}`. Right: the picture in the last two columns ("Đơn giá", "Thành tiền", about 164 pt wide), centred, from the first row; `QrScanCaption` (bold, 8 pt, centred) in the last row of the block.
- Row heights: each left row gets its wrapped height; if the sum of the rows is below `picture points + 2 + one default row`, the missing height is shared equally by all rows (explicit `Row.Height`, because the page-break estimate of `KeepSignaturesOnOnePage` reads only row heights). The block lies between the last body row and the signatures, so it is inside the kept-together area.
- The `Kho` column (9 columns) changes nothing: the picture and caption always use the last two columns.

## Tasks

### Task 3.1 — `QrPngRenderer` and the packages

1. **Write the failing test** in `Tt133/Unit/Vouchers/QrPngRendererTests.cs` (plain class). Add the packages first so it compiles: `dotnet add src/OrderMgmt.Infrastructure package QRCoder`, `dotnet add tests/OrderMgmt.IntegrationTests package ZXing.Net` and `dotnet add tests/OrderMgmt.IntegrationTests package ZXing.Net.Bindings.SkiaSharp` (Q8: if the bindings package conflicts with SkiaSharp 3.x, drop it and decode with `ZXing.BarcodeReaderGeneric` and an `RGBLuminanceSource` built from the `SKBitmap` pixels). Tests, with `payload = VietQrPayloadBuilder.Build("970436", "0123456789", "CONG TY ABC", 1_500_000m, "PT000125")` (about 170 characters):
   - `Png_decodes_back_to_the_payload`: `QrPngRenderer.Render(payload)` → decode with ZXing (`SKBitmap.Decode(bytes)`) → `Text == payload`.
   - `Png_is_square_with_whole_modules`: width equals height and is a multiple of the pixels per module (use `Render(payload, 6)` and check `width % 6 == 0`); the first row of pixels is white (the 4-module quiet zone).
   - `Error_correction_is_M`: decode the result metadata `ZXing.ResultMetadataType.ERROR_CORRECTION_LEVEL` equals `"M"` (skip this assertion if the reader does not expose it, and then assert the module count of the payload at level M, version 7 → `(45 + 8) * ppm` pixels wide, instead).
2. **Run the test to verify it fails:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~QrPngRendererTests"`. Expected: FAIL — build error, `QrPngRenderer` does not exist.
3. **Write the minimal implementation:** `Infrastructure/Qr/QrPngRenderer.cs`:
   ```csharp
   public static class QrPngRenderer
   {
       /// A PNG of the payload: error correction M, the standard 4-module quiet zone.
       public static byte[] Render(string payload, int pixelsPerModule = 8)
       {
           using var generator = new QRCodeGenerator();
           using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
           return new PngByteQRCode(data).GetGraphic(pixelsPerModule, drawQuietZones: true);
       }
   }
   ```
   (use the overload the installed QRCoder version has; `PngByteQRCode` needs no System.Drawing.)
4. **Run the test to verify it passes:** the filter of step 2. Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): QR PNG renderer (QRCoder, level M)"` and put the three package versions in the commit body.

### Task 3.2 — Models, request flag and the QR text rules

1. **Write the failing tests** in `Tt133/Unit/Vouchers/VoucherPrintRulesTests.cs` (add to the class). A helper `Qr(decimal amount, decimal total, decimal cashPaid)` builds a `VoucherPaymentQrDto` with `BankShortName = "Vietcombank"`, `AccountNumber = "0123456789"`:
   - `QrAmountLine`: amount 1_000_000 → `"Số tiền: 1.000.000 đồng"`.
   - `QrBreakdownLine`: `CashPaid = 0` → `null`; `Total 1_500_000, CashPaid 500_000, Amount 1_000_000` → `"Tổng 1.500.000 · Đã trả tiền mặt 500.000 · Cần chuyển khoản 1.000.000"`.
   - `QrBankCaption` → `"Vietcombank · 0123456789"`; `QrScanCaption == "Quét để chuyển khoản"`.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherPrintRulesTests"`. Expected: FAIL — build error.
3. **Write the minimal implementation:**
   - `VoucherPrintModels.cs`: `CashVoucherPrintModel.Qr` and `StockVoucherPrintModel.Qr` of type `VoucherPaymentQrDto?` (`using OrderMgmt.Application.CashDebt.PaymentQr;`); `CashVoucherPrintRequest.IncludeQr` and `StockVoucherPrintRequest.IncludeQr` (`bool`, default `false`).
   - `VoucherPrintRules.cs`: the three helpers and the constant of the SUMMARY contract, using the existing `Money`.
4. **Run the tests to verify they pass:** the filter of step 2. Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): QR fields on the print models and their text rules"`

### Task 3.3 — 01-TT: QR beside the lines

1. **Write the failing tests.**
   - `VoucherFormBuilderTests.cs` (builder level): `WriteText` with a last column — in `Tt133FormBuilderTests` (or the builder tests file you find it fits): `Tt133FormBuilder.WriteText("x", lastColumn: 8)` merges columns 1–8 only (`ws.Cell(row,1).MergedRange().LastColumn().ColumnNumber() == 8`); a text too long for 8 columns wraps and the row gets the taller height; the default (no argument) still merges all columns.
   - `CashVoucherFormTests.cs` (`Receipt()` gains `Qr`; a helper `Qr()` builds a `VoucherPaymentQrDto` with `Payload` from `VietQrPayloadBuilder.Build(...)`, `BankShortName = "Vietcombank"`, `AccountNumber = "0123456789"`):
     - `Receipt_with_qr_has_one_picture_beside_the_lines`: `ws.Pictures.Count == 1`; its `TopLeftCell.Address.ColumnNumber == 9` and its row equals the row of the cell `"Họ và tên người nộp tiền: Nguyễn Văn A"`; the picture is square and between 90 and 130 pixels wide (the exact size is measured on a real PDF in Task 3.6).
     - `Lines_beside_the_qr_stop_at_column_8`: each of the cells for the six lines (person, address, reason, amount, words, attached) has a merged range whose last column is 8; without `Qr` the same cells reach column 10.
     - `Qr_captions_are_in_columns_9_to_10`: the cells `"Quét để chuyển khoản"` and `"Vietcombank · 0123456789"` exist, in column 9, rows greater than the picture's row, and are merged to column 10.
     - `Rows_beside_the_qr_cover_the_picture`: the summed `Row(r).Height` of the rows from the picture's row to the row before the first caption row is at least the picture height in points (`picture.Height * 0.75`).
     - `Long_reason_still_fits`: a 300-character `Reason` (wraps in 8 columns) keeps the rule above and adds no more than the needed blank rows (the sheet's last used row grows by at most the wrapped lines).
     - `Without_qr_nothing_changes`: with `Qr = null`, `Pictures.Count == 0`, the six lines reach column 10, and the sheet has no blank rows between "Kèm theo: 2 chứng từ gốc" and the signature date (the existing tests of the file keep passing unchanged).
     - `Qr_is_on_01_TT_only`: a payment voucher model with a `Qr` set still has no picture (02-TT never prints a QR; the form ignores `Qr` for `CashVoucherType.Payment`).
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherFormBuilderTests|FullyQualifiedName~CashVoucherFormTests|FullyQualifiedName~Tt133FormBuilderTests"`. Expected: FAIL (build errors, then missing pictures).
3. **Write the minimal implementation:**
   - `Tt133FormBuilder.WriteText(string text, bool bold = false, bool italic = false, bool centered = false, int? lastColumn = null)`: `var last = lastColumn ?? _columns.Count;` used by `Merged` and `WrapMerged`. No other caller changes.
   - `VoucherFormBuilder`:
     - `private const double QrPrintMillimetres = 25;`, `private const double A5LandscapeScale = 0.90;`, `A4PrintScale()` extracted from `KeepSignaturesOnOnePage` (same formula, used by both) and a private `QrPixels` property (`round(25 * 72 / 25.4 / printScale / 0.75)`, with `printScale` from the paper).
     - `WriteText(string text)` and `WriteLine(label, value)` pass `_textLastColumn` (`null` outside a QR block) to `Table.WriteText`; `CashVoucherForm` calls `builder.WriteText(m.AttachedDocsLine)` instead of `builder.Table.WriteText(…)`.
     - `BeginQrBeside(VoucherPaymentQrDto qr)` stores `qr`, the first row (`Table.Row`) and sets `_textLastColumn = _columns.Count - 2`; `EndQrBeside()` implements the layout rules above (blank rows, picture via `Sheet.AddPicture(new MemoryStream(QrPngRenderer.Render(qr.Payload)), XLPictureFormat.Png, "payment-qr").MoveTo(Sheet.Cell(firstRow, count - 1), xOffsetPixels, 2).WithSize(px, px)`, the horizontal offset `((columns 9–10 width in pixels) - px) / 2` with a column's pixel width ≈ `width * 7 + 5`, the two caption cells), then clears `_textLastColumn`. `Infrastructure` already references `Application`, so the `VoucherPaymentQrDto` type is visible.
   - `CashVoucherForm.WriteVoucher`: `var qr = receipt ? m.Qr : null;` `if (qr != null) builder.BeginQrBeside(qr);` before the person line, `if (qr != null) builder.EndQrBeside();` after the "Kèm theo" line.
4. **Run the tests to verify they pass:** the filter of step 2 plus `FullyQualifiedName~Tt133` (every existing form test still passes). Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): QR beside the lines on the 01-TT receipt"`

### Task 3.4 — 02-VT: payment block

1. **Write the failing tests** in `StockVoucherFormTests.cs` (`Model()` helpers of that file; `Qr` built as in Task 3.3, with `Total = 1_500_000`, `CashPaid = 500_000`, `Amount = 1_000_000`, `BankName = "Ngân hàng TMCP Ngoại thương Việt Nam"`, `AccountHolderName = "CONG TY ABC"`, `Content = "PX000318"`):
   - `Stock_out_with_qr_has_the_payment_block`: the cells `"THÔNG TIN CHUYỂN KHOẢN"`, `"Ngân hàng: Ngân hàng TMCP Ngoại thương Việt Nam"`, `"Số tài khoản: 0123456789"`, `"Chủ tài khoản: CONG TY ABC"`, `"Số tiền: 1.000.000 đồng"`, `"Tổng 1.500.000 · Đã trả tiền mặt 500.000 · Cần chuyển khoản 1.000.000"`, `"Nội dung chuyển khoản: PX000318"` exist, in that row order, all after the row of `"- Số chứng từ gốc kèm theo:"` and before the signature titles; with `CashPaid = 0` the breakdown row is absent.
   - `Block_has_a_border_and_the_qr_is_on_the_right`: the title cell's top border is not `None`; `Pictures.Count == 1`; its `TopLeftCell` column equals `columns.Count - 1` (7 without and 8 with the `Kho` column... compute from the sheet: the column of the header cell `"Đơn giá"`), its row is the title row of the block, and the `QrScanCaption` cell is merged over `Đơn giá`–`Thành tiền` in the block's last row.
   - `Block_rows_cover_the_picture`: the summed heights of the block rows are at least the picture height in points plus one default row.
   - `Payment_block_and_signatures_are_never_split_by_a_page_break`: for `StockOutWithLines(n)` (a stock-out copy of the file's `StockInWithLines` helper, with a `Qr`) and every `n` from 30 to 110 in steps of 2, no value of `ws.PageSetup.RowBreaks` lies in `[titleRow, namesRow - 1]`, where `titleRow` is the row of `"THÔNG TIN CHUYỂN KHOẢN"` and `namesRow` the row of the preparer's name (a break value is the row after which the page ends; the existing `Signature_block_that_would_cross_a_page…` tests show the form).
   - `Without_qr_nothing_changes`: `Pictures.Count == 0` and no `"THÔNG TIN CHUYỂN KHOẢN"` cell; `Qr` on a stock-in model is ignored.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~StockVoucherFormTests"`. Expected: FAIL.
3. **Write the minimal implementation:** `VoucherFormBuilder.WritePaymentBlock(VoucherPaymentQrDto qr)` per the layout rules (rows via `Table.Row`/`Table.Skip`, left text merged with `Merged(row, 1, count - 2)` and `Wrap`, thin outside border on `Sheet.Range(first, 1, last, count)`, the picture and caption in the last two columns); `StockVoucherForm.WriteVoucher`: `if (!stockIn && m.Qr is { } qr) builder.WritePaymentBlock(qr);` right after `builder.WriteLine("- Số chứng từ gốc kèm theo:", null);`.
4. **Run the tests to verify they pass:** the filter of step 2 plus `FullyQualifiedName~Tt133`. Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): payment block with QR on the 02-VT stock-out"`

### Task 3.5 — Print service, routes and integration tests

1. **Write the failing tests** in `Tt133/Vouchers/VoucherPrintQrTests.cs` (`[Collection(nameof(PostgresCollection))]`, inherits `CashDebtTestBase`; copy the small `PrintAsync` / `PostAsync` helpers of the existing print tests; each test uses a fresh branch with `CreateQrBankFundAsync("NH01", branchId)` as in Phase 02; read workbooks with `Tt133Sheet.OpenAsync`; count `ws.Pictures.Count`):
   - `Receipt_without_include_qr_has_no_picture` (default) and `Receipt_with_include_qr_has_one_picture`: `GET …/print?format=xlsx&includeQr=true` → 200, xlsx, `{code}.xlsx`, one picture.
   - `Receipt_without_a_ready_fund_still_prints_without_qr`: a branch with no bank fund → 200 and `Pictures.Count == 0`.
   - `Batch_counts_pictures_per_voucher`: `POST api/cash-vouchers/print?format=xlsx` with `{ ids: [r1, r2, r3, r4], includeQr: true }` where r1 is on the ready bank fund, r2 on the cash fund (falls back to the default bank fund), r3 is cancelled, r4 is on a second bank fund without a holder name, r5 is the automatic receipt of a stock-out paid in cash (cash fund) → sheets `1,1,0,0,0` pictures in the order of `VoucherAt` then code; the same request without `includeQr` → 0 everywhere.
   - `Payment_vouchers_never_get_a_qr`: `includeQr=true` on a payment → 0 pictures, 200.
   - Stock: `Stock_out_voucher_mode_with_qr` (`priceMode=Voucher&includeQr=true` → one picture and the cell `"THÔNG TIN CHUYỂN KHOẢN"`); `Cost_and_None_modes_ignore_qr` (0 pictures, 200); `Stock_in_ignores_qr`; `Stock_batch_body_flag` (`POST` with `{ ids, priceMode: "Voucher", includeQr: true }` → one picture per eligible voucher).
   - Access still wins: a client without `receipts.view` asking `includeQr=true` → 403; the branch check is unchanged.
2. **Run the tests to verify they fail:** `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherPrintQrTests"`. Expected: FAIL (compile error on the new parameter, then no pictures).
3. **Write the minimal implementation:**
   - `IVoucherPrintService`: `PrintCashVouchersAsync(IReadOnlyList<Guid> ids, ExportFormat format, bool includeQr = false, CancellationToken ct = default)` and `PrintStockVouchersAsync(ids, priceMode, format, bool includeQr = false, ct)`; fix every caller (the two controllers and any test that passed `ct` as the third argument).
   - `VoucherPrintService`: inject `IVoucherPaymentQrService`. Inside each snapshot, **after** `EnsureAccessAsync`: `var qrs = includeQr ? await _paymentQr.ResolveManyAsync(kind, rows.Select(r => r.Id).ToList(), token) : empty` (`kind` is `PaymentQrKind.Cash` in the cash method and `PaymentQrKind.Stock` in the stock method); cash model: `Qr = qrs.GetValueOrDefault(r.Id)` (the cash rows already select `v.Id`); stock: pass `qrs` to `ToStockModel` and set `model.Qr = mode == StockPrintPriceMode.Voucher ? qrs.GetValueOrDefault(r.Id) : null`. Ineligible vouchers (cancelled, payments, stock-in) are simply absent from `qrs`.
   - Controllers: the two single `Print` actions take `[FromQuery] bool includeQr = false`; the `PrintMany` actions pass `request.IncludeQr`. Update the XML summaries.
4. **Run the tests to verify they pass:** the filter of step 2, then `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~DashboardSignal"`. Expected: PASS.
5. **Commit:** `git add backend && git commit -m "feat(payment-qr): includeQr on the voucher print routes"`

### Task 3.6 — Real PDF: measure, tune, look

1. Write a **temporary** test `Tt133/Vouchers/VoucherQrSamplesTests.cs` (inherits `CashDebtTestBase`, never committed) that saves xlsx files to `Path.Combine(Path.GetTempPath(), "voucher-qr-samples")`, the company profile filled in:
   - `01-TT-qr.xlsx`: a receipt of 1 500 000 on a ready bank fund, `includeQr=true`;
   - `01-TT-qr-long.xlsx`: the same with a 400-character `Description`;
   - `01-TT-qr-batch.xlsx`: three receipts in one POST;
   - `02-VT-qr.xlsx`: a stock-out sale with two lines paid by transfer;
   - `02-VT-qr-cash.xlsx`: a stock-out partly paid in cash (shows the breakdown row), with two warehouses (the `Kho` column);
   - `02-VT-qr-40-lines.xlsx`: a stock-out with 40 lines (page break).
   Run it: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~VoucherQrSamplesTests"`.
2. Convert with the local LibreOffice, on a profile of its own:
   ```bash
   cd "$TEMP/voucher-qr-samples"
   "/c/Program Files/LibreOffice/program/soffice.exe" "-env:UserInstallation=file:///$(cygpath -m "$TEMP")/lo-vqr" --headless --convert-to pdf --outdir . *.xlsx
   ```
3. Measure with PyMuPDF (run Python with `-I`, the script lives outside the sample folder, e.g. in the scratchpad):
   ```python
   import pymupdf, sys, glob
   for path in sorted(glob.glob(sys.argv[1] + "/*.pdf")):
       doc = pymupdf.open(path)
       print(path, len(doc), "pages")
       for n, page in enumerate(doc, 1):
           size = [round(v, 1) for v in page.rect[2:]]
           widths_mm = [round((i["bbox"][2] - i["bbox"][0]) * 25.4 / 72, 1) for i in page.get_image_info()]
           print("  page", n, size, "QR width mm:", widths_mm)
   ```
   Expected: A5 pages `[595.3, 419.5]`, A4 pages `[595.3, 841.9]`, **one picture per voucher page, 25 mm ± 1 mm wide**, `01-TT-qr-batch.pdf` three pages, `01-TT-qr*.pdf` one page each (the receipt must still fit one page), `02-VT-qr-40-lines.pdf` more than one page with the payment block and the signatures on the same page.
4. If a width is off by more than 1 mm: set `A5LandscapeScale` to `measured_scale = old_scale * measured_mm / 25` (and, when A4 is off, correct the formula's constants, not the A4 exceptions), re-run steps 1–3. Then put the numbers in the commit message.
5. Inspect the image LibreOffice embedded: for the first image of each QR page, `info = doc.extract_image(xref)` (xref from `page.get_images()`); record `info['ext']` (`jpeg` means lossy compression), `info['width']` × `info['height']` and the pixels per module (`width / (modules + 8)`, 53 modules for the sample payload). The shared converter runs plain `--convert-to pdf` (`LibreOfficeSpreadsheetPdfConverter`); if the image is `jpeg` or resampled below about 4 pixels per module **and** the decode in step 6 fails, **stop and ask the user** whether to pass PDF filter options (lossless compression, no resolution reduction) to the converter, which also affects the quotation exports, or to draw the QR as filled cells.
6. Render each PDF page to PNG with PyMuPDF (`page.get_pixmap(dpi=200).save(...)`) and **look at them** (Read tool on the PNGs): the QR sits to the right of the lines on A5 without touching the text; captions are readable and not clipped; the signature blocks are intact; on A4 the frame is bordered, the QR is under the "Thành tiền" column on the right, text is not cut, and the frame is not split from the signatures. If `import cv2` works, also decode a 300 dpi render with `cv2.QRCodeDetector().detectAndDecode(img)` and compare it with the voucher's payload; otherwise leave the scan to the go-live checklist and say so.
7. Fix defects found (each with a unit test from Tasks 3.3/3.4), delete the temporary test, and commit: `git add backend && git commit -m "fix(payment-qr): print scale measured on real PDFs"` (only if code changed; otherwise record the measured values in the phase report).

### Task 3.7 — Frontend: `qr-pref-storage`, request flag and API

1. **Write the failing tests.**
   - `features/voucher-print/qr-pref-storage.test.ts` (same cases as `price-mode-storage.test.ts`): defaults to `true`; round-trips under `qldh:voucher-print-qr:u1` (`'1'` / `'0'`); a stored value that is not `'0'`/`'1'` → `true`; no user id → `true` and nothing written; survives a throwing `localStorage` (get and set).
   - `api.test.ts`: `fetchVoucherPrint({ kind: 'cash', type: 'Receipt', vouchers: [a], format: 'pdf', includeQr: true })` calls `requestBlob` with `params: { format: 'pdf', includeQr: true }`; with `[a, b]` the body is `{ ids: ['a','b'], includeQr: true }`; stock with `priceMode` and `includeQr` keeps both; `includeQr: false` or absent adds nothing (the existing four cases stay as they are).
   - `use-voucher-print.test.tsx`: `print({ …, includeQr: true })` passes the flag on to `fetchVoucherPrint`.
2. **Run the tests to verify they fail:** from `frontend/`: `npm run test -- voucher-print/qr-pref-storage voucher-print/api voucher-print/use-voucher-print`. Expected: FAIL.
3. **Write the minimal implementation:** `qr-pref-storage.ts` (mirror `price-mode-storage.ts`; key `qldh:voucher-print-qr:{userId}`); `types.ts`: `includeQr?: boolean` on `VoucherPrintRequest`; `api.ts`: add `includeQr: true` to `params` (single) and to `body` (several) only when `request.includeQr === true`.
4. **Run the tests to verify they pass:** the command of step 2. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): includeQr in the voucher print request and a remembered switch"`

### Task 3.8 — Frontend: receipt print menu item

1. **Write the failing tests** in `components/voucher-print/voucher-print-button.test.tsx` (mock `useVoucherPrint` as the file does; set the auth store user id with the file's helper):
   - `shows the QR item for receipts only`: opening the menu of `kind="cash" type="Receipt"` shows a `menuitemcheckbox` named "Kèm QR chuyển khoản"; it is absent for `type="Payment"`, `kind="stock"` `type="In"` and `type="Out"`.
   - `defaults to on and remembers`: checked on first open (no stored value); clicking it unticks it, writes `'0'` under `qldh:voucher-print-qr:{userId}`; a new render reads it back unticked.
   - `menu stays open after the tick`: after the click, the menu (`role="menu"`) and "In (PDF)" are still in the document (R10).
   - `print carries the switch`: with it ticked, choosing "In (PDF)" calls `print` with `includeQr: true`; unticked → `includeQr: false`; for a payment voucher the request has no `includeQr`.
2. **Run the tests to verify they fail:** `npm run test -- voucher-print-button`. Expected: FAIL.
3. **Write the minimal implementation:** in `voucher-print-button.tsx` add `const userId = useAuthStore((s) => s.user?.id)`, `const qrEnabled = kind === 'cash' && type === 'Receipt'`, `const [includeQr, setIncludeQr] = useState(() => readQrPref(userId))`; inside the menu after "Excel": `<DropdownMenuSeparator />` and
   ```tsx
   <DropdownMenuCheckboxItem
     checked={includeQr}
     onCheckedChange={(on) => { setIncludeQr(on); writeQrPref(userId, on); }}
     onSelect={(e) => e.preventDefault()}>
     Kèm QR chuyển khoản
   </DropdownMenuCheckboxItem>
   ```
   rendered only when `qrEnabled`; `choose` passes `includeQr: qrEnabled ? includeQr : undefined` to `print`. No icon import from `lucide-react`.
4. **Run the tests to verify they pass:** the command of step 2 plus `npm run typecheck && npm run lint`. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): Kèm QR chuyển khoản in the receipt print menu"`

### Task 3.9 — Frontend: stock-out price dialog checkbox

1. **Write the failing tests** in `components/voucher-print/stock-price-mode-dialog.test.tsx` and `voucher-print-button.test.tsx`:
   - dialog, `showQr` true: a checkbox "Kèm QR chuyển khoản" is shown, ticked by default; with the mode "Giá trên phiếu" it is enabled; choosing "Giá vốn" (user with `inventory.view_cost`) or "Không in tiền" disables it (`toBeDisabled`) without changing the stored value; pressing "In" calls `onConfirm('Voucher', true)`, and after unticking `onConfirm('Voucher', false)`; with mode `None`/`Cost` `onConfirm(mode, false)`; the choice is stored under the `qr-pref-storage` key.
   - dialog, `showQr` false/absent: no checkbox, `onConfirm(mode, false)` (update the existing assertions of the file to this two-argument form).
   - button: a stock-out (`kind="stock" type="Out"`) opens the dialog with the checkbox; a stock-in does not; confirming calls `print` with `priceMode` and `includeQr`.
2. **Run the tests to verify they fail:** `npm run test -- stock-price-mode-dialog voucher-print-button`. Expected: FAIL.
3. **Write the minimal implementation:** `StockPriceModeDialog` gets `showQr?: boolean`; `onConfirm: (mode: StockPrintPriceMode, includeQr: boolean) => void`; `Body` holds `includeQr` state from `readQrPref(userId)` and renders `<input type="checkbox" id="stock-print-qr" …>` with `<label htmlFor>` "Kèm QR chuyển khoản", `disabled={mode !== 'Voucher'}` and `title="Chỉ dùng với Giá trên phiếu"` while disabled; `confirm` calls `writeQrPref` when `showQr`, then `onConfirm(mode, !!showQr && mode === 'Voucher' && includeQr)`. `VoucherPrintButton` passes `showQr={type === 'Out'}` and `confirmPriceMode(priceMode, includeQr)` forwards `includeQr` to `print`.
4. **Run the tests to verify they pass:** the command of step 2, then `npm run test -- voucher-print`, `npm run typecheck && npm run lint`. Expected: PASS.
5. **Commit:** `git add frontend && git commit -m "feat(payment-qr): Kèm QR chuyển khoản in the stock-out price dialog"`

## Verification

- Backend, from `backend/`: `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~PaymentQr|FullyQualifiedName~DashboardSignal"` passes (existing form and print tests unchanged).
- Frontend, from `frontend/`: `npm run typecheck`, `npm run lint`, `npm run test -- voucher-print` pass; no file outside `components/icons` imports `lucide-react` (`no-direct-lucide-imports.test.ts` is part of the suite).
- Task 3.6 evidence: page sizes, picture count and measured QR width (mm) of each sample are in the phase report.
- `git diff <phase base> --numstat` shows no `tsconfig.app.tsbuildinfo` and no CRLF-only rewrites.

## Exit Criteria

- 01-TT and 02-VT carry a decodable QR of about 25 mm in Excel and PDF, single and bulk, only when `includeQr` is true and the voucher qualifies; the A5 receipt still prints on one page.
- 02-TT, 01-VT, the Cost and None price modes and vouchers without a ready fund print exactly as before.
- The receipt menu and the stock-out dialog remember the switch per user and send it.
