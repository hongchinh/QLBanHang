# Phase 03 — Stock voucher forms and price modes, backend

**Status:** [x] complete
**Complexity:** L

## Objective

Print stock-in vouchers (01-VT) and stock-out vouchers (02-VT) through `GET api/stock-vouchers/{id}/print` and `POST api/stock-vouchers/print`, in the three price modes of V11, with the line rules of V10 and the access and error rules of V6. Needs Phase 01 (`Tt133ExportSupport`, `VoucherFormBuilder`, `VoucherPrintService`).

## Files

Backend (`backend/`):

- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintModels.cs` (modify — stock models and request)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintRules.cs` (modify — `LineName`, `VoucherTotals`)
- `src/OrderMgmt.Application/Tt133/Vouchers/IVoucherFormRenderer.cs` (modify — `RenderStockVouchers`)
- `src/OrderMgmt.Application/Tt133/Vouchers/IVoucherPrintService.cs` (modify — `PrintStockVouchersAsync`)
- `src/OrderMgmt.Application/Tt133/Vouchers/VoucherPrintService.cs` (modify)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/StockVoucherForm.cs` (new)
- `src/OrderMgmt.Infrastructure/Excel/Tt133/Vouchers/VoucherFormRenderer.cs` (modify)
- `src/OrderMgmt.WebApi/Controllers/StockVouchersController.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/VoucherPrintRulesTests.cs` (modify)
- `tests/OrderMgmt.IntegrationTests/Tt133/Unit/Vouchers/StockVoucherFormTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/Tt133/Vouchers/StockVoucherPrintTests.cs` (new)
- `tests/OrderMgmt.IntegrationTests/ExecutiveDashboard/DashboardSignalTests.cs` (modify)

## Reference files (read-only)

- `src/OrderMgmt.Application/Inventory/StockVouchers/Services/StockVoucherCalculator.cs` (how `Total`, `VatAmount` and `NetAmount` are computed), `StockVoucherService.cs` L1036-1053 (ledger rows: one per tracked, non-deleted line, `SourceType` StockIn/StockOut, `SourceId` = voucher id, `SourceLineId` = line id; `InValue = InboundValue + VatAmount` when `PurchaseCostIncludesVat`; hard-deleted when cancelled), `StockVoucherPermissions.cs`.
- `src/OrderMgmt.Application/Inventory/Common/CostProvisional.cs` L8 (`IsProvisional(date, period, today)`, the stock card's rule; call it, do not re-implement it), `CostingPeriodCalendar.cs`, `Inventory/Reports/Services/InventoryReportService.cs` L204 (`R4(InValue / QtyIn)` on the stock card; its `R4` at L424 is private, so `VoucherPrintService` gets its own `private static decimal R4(decimal x) => Math.Round(x, 4, MidpointRounding.AwayFromZero)`).
- `src/OrderMgmt.Infrastructure/Persistence/Configurations/InventoryConfiguration.cs` L242-244 (ledger indexes: `(SourceType, SourceId)` exists, `SourceLineId` has none).
- `tests/OrderMgmt.IntegrationTests/Inventory/InventoryTestBase.cs` (`CreateVoucherAsync`, `LineRequest`, `CreateInventoryProductAsync` — names `"Hàng {code}"`, unit `"Tấm"`; `CreatePartnerAsync` — names `"Đối tượng {code}"`, address `"Hà Nội"`; `LedgerOfAsync`, `UpdateInventorySettingsAsync`, `CreateBranchAsync`, `CreateWarehouseAsync`, `CreateClientWithPermissionsAsync`, `UpdateRequestFrom`, `PutVoucherAsync`, `Vn`).
- `tests/OrderMgmt.IntegrationTests/Inventory/InventoryInvariantTests.cs` L41-43 (a line in a second warehouse), L120-126 (`ActionAsync` for cancel); `Inventory/StockVouchers/StockVoucherCreateTests.cs` L24-27 (dimensional line); `Inventory/StockCardReportTests.cs` L189-190 (stock card request).

## Form layout (01-VT / 02-VT)

Columns (`labelColumn: 2`; the `Kho` column only when `ShowWarehouseColumn`):

| Symbol | Group | Header (01-VT / 02-VT) | Width | Kind | Value |
|---|---|---|---|---|---|
| A | — | STT | 5 | Quantity | `Number` |
| B | — | Tên, nhãn hiệu, quy cách, phẩm chất vật tư, dụng cụ, sản phẩm, hàng hóa | 34 | Text | `Name` |
| C | — | Mã số | 12 | Text | `Code` |
| D | — | Đơn vị tính | 8 | Text | `UnitName` |
| E | — | Kho | 8 | Text | `WarehouseCode` |
| 1 | Số lượng | Theo chứng từ / Yêu cầu | 10 | Quantity | `Quantity` |
| 2 | Số lượng | Thực nhập / Thực xuất | 10 | Quantity | `Quantity` |
| 3 | — | Đơn giá | 13 | Money | `UnitPrice` |
| 4 | — | Thành tiền | 15 | Money | `Amount` |

Writing order of `StockVoucherForm` for one model `m` (`i` = stock-in):

1. `WriteHeaderBlock(i ? "01-VT" : "02-VT")`.
2. `WriteTitleBlock(i ? "PHIẾU NHẬP KHO" : "PHIẾU XUẤT KHO", m.Date, m.IsCancelled, [$"Số: {m.Code}"], [$"Nợ: {Dots}", $"Có: {Dots}"])`.
3. Each of `m.Notes` with `Table.WriteNote`.
4. Stock-in: `WriteLine("- Họ và tên người giao:", m.HandlerName)`; `Table.WriteText($"- Theo {Dots} số {Dots} ngày {Dots} của {m.PartnerName ?? Dots}")`; `Table.WriteText($"- Nhập tại kho: {m.WarehouseNames} Địa điểm: {Dots}")`.
   Stock-out: `WriteLine("- Họ và tên người nhận hàng:", m.HandlerName)`; `WriteLine("Địa chỉ (bộ phận):", m.PartnerAddress)`; `WriteLine("- Lý do xuất kho:", m.ReasonName)`; `Table.WriteText($"- Xuất tại kho (ngăn lô): {m.WarehouseNames} Địa điểm: {Dots}")`.
5. `Table.WriteTableHeader()`; then the sub-header row (`Table.TableHeaderFirstRow + 1`, "Theo chứng từ" / "Thực nhập", "Yêu cầu" / "Thực xuất") gets `lines * Sheet.RowHeight`, with `lines` the largest `Tt133FormBuilder.LineCount(column.Header, column.Width, bold: true)` over the grouped columns: `WriteTableHeader` wraps but sets no height, and LibreOffice keeps the default height, so "Theo chứng từ" (2 lines in a 10-wide column) would be cut. `Tt133FormBuilder` stays unchanged. Then one `Table.WriteRow` per line (an empty value is `null`).
6. Each of `m.Totals`: `Table.WriteSummaryRow(t.Label, (amountColumn, t.Amount))` (`amountColumn` = the last column).
7. `m.VatIncludedLine` (when set): `Table.WriteText(line, italic: true)`.
8. `WriteLine("- Tổng số tiền (viết bằng chữ):", m.AmountInWords)`; `WriteLine("- Số chứng từ gốc kèm theo:", null)`.
9. `WriteSignatures(m.Date, …)`, every hint `(Ký, họ tên)`: stock-in Người lập phiếu (`m.PreparerName`), Người giao hàng, Thủ kho, Kế toán trưởng (`context.ChiefAccountantName`); stock-out Người lập phiếu, Người nhận hàng, Thủ kho, Kế toán trưởng, Giám đốc (`context.LegalRepresentativeName`).
10. `Finish()` (A4 portrait, header rows repeated).

## Tasks

### Task 3.1 — Stock rules and models

1. **Write the failing tests** in `VoucherPrintRulesTests.cs`:
   - `LineName` (theory):
     - `("Kính", PerUnit, 1200, 2400, 5, 10)` → `"Kính"`;
     - `("Kính", PerSquareMeter, 1200, 2400, null, 10)` → `"Kính (1200×2400 mm, 10 tấm)"`;
     - `("Xốp", PerCubicMeter, 2000, 1000, 50, 10)` → `"Xốp (2000×1000×50 mm, 10 tấm)"`;
     - `("Thép", PerLinearMeter, 6000, null, null, 4)` → `"Thép (6000 mm, 4 tấm)"`;
     - `("Kính", PerSquareMeter, 1200.5, 0, null, 2)` → `"Kính (1200,5 mm, 2 tấm)"` (zero is left out; vi-VN decimal comma);
     - `("Kính", PerSquareMeter, null, null, null, 3)` → `"Kính (3 tấm)"`; all null → `"Kính"`.
   - `VoucherTotals`:
     - `(1_000_000, 0, 0, 0, 1_000_000)` → `[("Cộng", 1_000_000)]`;
     - `(1_500_000, 50_000, 10_000, 20_000, 1_604_000)` → `[("Cộng tiền hàng", 1_500_000), ("Chiết khấu", 60_000), ("Thuế GTGT", 144_000), ("Cước vận chuyển", 20_000), ("Tổng cộng", 1_604_000)]`;
     - `(2_200_000, 0, 0, 0, 2_200_000)` (VAT inside the price) → `[("Cộng", 2_200_000)]`;
     - `(500_000, 0, 0, 30_000, 530_000)` → `[("Cộng tiền hàng", 500_000), ("Cước vận chuyển", 30_000), ("Tổng cộng", 530_000)]`;
     - (VAT left out of the total by `NetExcludesVat` gives the same arguments as the first case; it is covered by the integration test `Vat_left_out_of_the_total` of Task 3.4, not by a duplicate theory row);
     - for every multi-row case: first − "Chiết khấu" + "Thuế GTGT" + "Cước vận chuyển" = last.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~VoucherPrintRulesTests"`. Expected: FAIL — build error, `LineName` / `StockVoucherPrintTotal` do not exist.
3. **Write the minimal implementation:** the Phase 03 members of the shared contract in `VoucherPrintModels.cs` (`StockVoucherPrintRequest`, `StockVoucherPrintModel`, `StockVoucherPrintLine`, `StockVoucherPrintTotal`) and in `VoucherPrintRules` (`LineName` with `ToString("0.###", vi-VN)` and the `×` sign U+00D7; `VoucherTotals` per V11).
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <the two source files and the test file> && git commit -m "feat(tt133): stock voucher print rules"`

### Task 3.2 — 01-VT and 02-VT forms

1. **Write the failing tests** in `Tt133/Unit/Vouchers/StockVoucherFormTests.cs`, rendering with `new VoucherFormRenderer().RenderStockVouchers(Context(), models)` (`Context()` as in `VoucherFormBuilderTests`). `StockIn()` = Type In, Code "PN00001", Date 2026-10-05, PriceMode Voucher, HandlerName "Trần Giao", PartnerName "Công ty XYZ", WarehouseNames "Kho chính", lines given as (Number, Name, Code, UnitName, WarehouseCode, Quantity, UnitPrice, Amount): `(1, "Hàng A", "A", "Tấm", null, 10, 100_000, 1_000_000)` and `(2, "Hàng B", "B", "Tấm", null, 5, 100_000, 500_000)`, Totals `[("Cộng", 1_500_000)]`, AmountInWords "Một triệu năm trăm nghìn đồng", PreparerName "Quản trị hệ thống".
   - `Stock_in_is_01_VT`: worksheet `"PN00001"`; texts `"Mẫu số 01-VT"`, `"PHIẾU NHẬP KHO"`, `"Số: PN00001"`, `"Nợ: ……………………"`, `"Có: ……………………"`, `"- Họ và tên người giao: Trần Giao"`, `"- Theo …………………… số …………………… ngày …………………… của Công ty XYZ"`, `"- Nhập tại kho: Kho chính Địa điểm: ……………………"`; header cells `"STT"`, the long name header, `"Mã số"`, `"Đơn vị tính"`, `"Số lượng"`, `"Theo chứng từ"`, `"Thực nhập"`, `"Đơn giá"`, `"Thành tiền"` and the symbol row `A B C D 1 2 3 4`; the row of `"Hàng A"` reads 1, `"A"`, `"Tấm"`, 10, 10, 100,000, 1,000,000; `"Cộng"` in column B with 1,500,000 in the last column; `"- Tổng số tiền (viết bằng chữ): Một triệu năm trăm nghìn đồng"`; `"- Số chứng từ gốc kèm theo: ……………………"`; signature titles by column `"Người lập phiếu"`, `"Người giao hàng"`, `"Thủ kho"`, `"Kế toán trưởng"`, with `"Quản trị hệ thống"` and `"Trần B"` under the first and the last; the row of `"Theo chứng từ"` is at least `2 * ws.RowHeight` high; A4, portrait, `PagesWide == 1`, `PagesTall == 0` (see the reload note of Task 1.4), `FirstRowToRepeatAtTop` = the row of `"STT"`.
   - `Stock_out_is_02_VT`: Type Out, Code "PX00001", PartnerAddress "Hà Nội", ReasonName "Xuất bán hàng", HandlerName "Lê Nhận" → `"Mẫu số 02-VT"`, `"PHIẾU XUẤT KHO"`, `"- Họ và tên người nhận hàng: Lê Nhận"`, `"Địa chỉ (bộ phận): Hà Nội"`, `"- Lý do xuất kho: Xuất bán hàng"`, `"- Xuất tại kho (ngăn lô): Kho chính Địa điểm: ……………………"`, headers `"Yêu cầu"` and `"Thực xuất"`; titles `"Người lập phiếu"`, `"Người nhận hàng"`, `"Thủ kho"`, `"Kế toán trưởng"`, `"Giám đốc"` in columns 1, 3, 4, 6, 8 (blocks by width, Phase 01 "Form layout"; "Người lập phiếu" is not alone in the 5-wide STT column) with `"Nguyễn A"` under `"Giám đốc"`.
   - `Totals_rows_and_vat_line`: Totals `[("Cộng tiền hàng", 1_000_000), ("Chiết khấu", 60_000), ("Thuế GTGT", 94_000), ("Cước vận chuyển", 20_000), ("Tổng cộng", 1_054_000)]`, VatIncludedLine `"Trong đó thuế GTGT đã gồm trong giá: 100.000"` → the five labels on consecutive rows in column B with their amounts in the last column; the VAT line on a row after `"Tổng cộng"`.
   - `No_money`: lines with `UnitPrice` and `Amount` null, Totals `[("Cộng", null)]`, AmountInWords null → those cells empty; `"Cộng"` row has no amount; `"- Tổng số tiền (viết bằng chữ): ……………………"`.
   - `Several_warehouses_add_the_Kho_column`: `ShowWarehouseColumn = true`, `WarehouseNames = "Kho chính, Kho phụ"`, line warehouse codes `"KHO01"`, `"KHO02"` → header `"Kho"` with symbol `"E"` between `"Đơn vị tính"` and `"Số lượng"`; the cells `"KHO01"`, `"KHO02"`; `"Thành tiền"` is still the last column; `"- Nhập tại kho: Kho chính, Kho phụ Địa điểm: ……………………"`.
   - `Notes_and_cancelled`: `IsCancelled = true`, Notes `["Phiếu đã hủy, không có giá vốn."]` → `"ĐÃ HỦY"` and the note (italic) between the title block and the first party line.
   - `One_sheet_per_voucher`: models `PN00001`, `PX00001` → worksheets in that order.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherFormTests"`. Expected: FAIL — build error, `RenderStockVouchers` does not exist.
3. **Write the minimal implementation:** `IVoucherFormRenderer.RenderStockVouchers`; `StockVoucherForm.Write(XLWorkbook, Tt133FormContext, IReadOnlyList<StockVoucherPrintModel>)` per "Form layout" (columns built per model: with or without `Kho`, headers by direction); `VoucherFormRenderer.RenderStockVouchers` → `Tt133FormRenderer.Render(wb => StockVoucherForm.Write(...))`.
4. **Run the tests to verify they pass:** same filter, then `--filter "FullyQualifiedName~Tt133.Unit"`. Expected: PASS.
5. **Commit:** `git add <the source files and the test file> && git commit -m "feat(tt133): 01-VT and 02-VT forms"`

### Task 3.3 — Print one stock voucher at the voucher's prices

Test class `Tt133/Vouchers/StockVoucherPrintTests.cs : InventoryTestBase`, `[Collection(nameof(PostgresCollection))]`. `_client` is the admin working in the main branch, vouchers go to `DefaultWarehouseId` (KHO01). Private helpers: `SetCompanyAsync()` (row 1 of `CompanyProfiles` through `InDbAsync`: Name "Công ty ABC", Address "1 Láng Hạ", ChiefAccountantName "Trần B", LegalRepresentativeName "Nguyễn A"), `PrintAsync(client, id, query = "format=xlsx")` → `GET /api/stock-vouchers/{id}/print?{query}`, `WarehouseNameAsync(id)`, `NewCode(prefix)`, `Texts(ws)`, and `Amount(ws, label)` (`Tt133Sheet.Number(ws, Tt133Sheet.RowOf(ws, label), lastColumn)`).

1. **Write the failing tests:**
   - `Prints_a_stock_in_with_its_totals`: a supplier `CreatePartnerAsync(NewCode("NCC"), false, true)`; products `a`, `b` (`CreateInventoryProductAsync(NewCode("A"))`, …); `CreateVoucherAsync(_client, StockDirection.In, "NMH", "2026-10-05 09:00", r => { r.PartnerId = supplier; r.HandlerName = "Trần Giao"; r.OrderDiscount = 10_000; r.Freight = 20_000; }, la, lb)` where `la = LineRequest(a, 10, 100_000)` with `la.DiscountRate = 5; la.VatRate = 10;` and `lb = LineRequest(b, 5, 100_000)` with `lb.VatRate = 10;` (`UpsertStockVoucherLineRequest` is a class: set the properties on the returned object; "line(…) with …" elsewhere in this phase means the same). Print → 200, `{v.Code}.xlsx`, one sheet; `"Mẫu số 01-VT"`, `"PHIẾU NHẬP KHO"`, `"- Họ và tên người giao: Trần Giao"`, `$"- Theo …………………… số …………………… ngày …………………… của Đối tượng {supplierCode}"`, `$"- Nhập tại kho: {KHO01 name} Địa điểm: ……………………"`; the row of `"Hàng {a}"`: `"Tấm"`, 10, 10, 100,000, 1,000,000; total rows `"Cộng tiền hàng"` 1,500,000 (= `v.GoodsAmount`), `"Chiết khấu"` 60,000 (= `v.LineDiscountTotal + v.OrderDiscount`), `"Thuế GTGT"` 144,000, `"Cước vận chuyển"` 20,000, `"Tổng cộng"` 1,604,000 (= `v.Total`); `$"- Tổng số tiền (viết bằng chữ): {VndAmountInWords.Read(v.Total)}"`; `v.OwnerName` under "Người lập phiếu".
   - `Prints_a_stock_out_as_02_VT`: stock first (`NKH`, 10-01, `a` × 10 at 100,000); a customer; `XBH` on 10-05 with `r.PartnerId = customer`, `r.HandlerName = null`, line(a, 2, 150,000) → `"PHIẾU XUẤT KHO"`, `$"- Họ và tên người nhận hàng: Đối tượng {customerCode}"` (fallback), `"Địa chỉ (bộ phận): Hà Nội"`, `"- Lý do xuất kho: Xuất bán hàng"`, one `"Cộng"` row 300,000.
   - `Spec_and_several_warehouses`: a second warehouse `CreateWarehouseAsync(MainBranchId, NewCode("KH"))`; an area product `CreateInventoryProductAsync(NewCode("K"), PricingMode.PerSquareMeter)`; `NKH` with line `{ ProductId = area, SheetCount = 10, Length = 1200, Width = 2400, UnitPrice = 50_000 }` (KHO01) and line(a, 3, 100,000) with `WarehouseId = second` → `$"Hàng {areaCode} (1200×2400 mm, 10 tấm)"`, unit `"m²"`, quantity 28.8; a `"Kho"` header; `"- Nhập tại kho: {KHO01 name}, {second name} Địa điểm: ……………………"`.
   - `Removed_lines_and_deleted_names`: a new warehouse `w` (`CreateWarehouseAsync(MainBranchId, NewCode("KW"))`); a stock-in in `w` (`r.WarehouseId = w`) with two lines; `UpdateRequestFrom(v)`, `Lines.RemoveAt(1)`, `PutVoucherAsync` → 200; read the warehouse's name (`"Kho {code}"`) now, then soft-delete the warehouse straight in the database (`db.Warehouses.Where(x => x.Id == w).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true))`; the API refuses a used warehouse with 409). Print → one item row only, and `$"- Nhập tại kho: {w name} Địa điểm: ……………………"` still shows the deleted warehouse's name. After the soft delete, read nothing through query-filtered paths: `WarehouseNameAsync` would miss the row unless it uses `IgnoreQueryFilters()`, and `GET /api/stock-vouchers/{id}` would answer 404 (the voucher's required `Warehouse` is filtered out).
   - `Cancelled_voucher_prints_ĐÃ_HỦY`: cancel through a private `ActionAsync(id, "cancel", version)` copied from `InventoryInvariantTests` L120-126 → print 200 with `"ĐÃ HỦY"` and the voucher prices.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherPrintTests"`. Expected: FAIL — 404 (route does not exist).
3. **Write the minimal implementation:**
   - `IVoucherPrintService.PrintStockVouchersAsync`; in `VoucherPrintService`: de-duplicate `ids`; inside one `RunSnapshotAsync`: `BuildContextAsync(ReportScope.Branch, token)` and one projection of `_db.StockVouchers.IgnoreQueryFilters().AsNoTracking().Where(v => ids.Contains(v.Id) && !v.IsDeleted)` with `Id, Type, Code, VoucherAt, BranchId, Status, HandlerName, PartnerName, PartnerAddress, GoodsAmount, LineDiscountTotal, OrderDiscount, Freight, Total`, `ReasonName = v.Reason!.Name`, `WarehouseName = v.Warehouse!.Name`, `OwnerName`, and `Lines = v.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder).Select(l => new { l.Id, l.ProductName, l.ProductCode, l.UnitName, l.PricingMode, l.Length, l.Width, l.Thickness, l.SheetCount, l.Quantity, l.UnitPrice, l.Amount, l.VatAmount, l.PriceIncludesVat, l.TrackInventory, WarehouseCode = l.Warehouse!.Code, WarehouseName = l.Warehouse.Name })`. Map with V10 (names, units, warehouse names, `ShowWarehouseColumn` when more than one distinct line warehouse), `HandlerName = FirstFilled(HandlerName, PartnerName)`, and `Voucher` mode of V11 (`Totals = VoucherTotals(...)`, `AmountInWords = VndAmountInWords.Read(Total)`). Order and file names as V8 (prefixes `PhieuNhapKho`, `PhieuXuatKho`, `PhieuNhapXuat`). `Cost` and `None` throw `NotSupportedException` until Tasks 3.5 and 3.6; no `VatIncludedLine` yet (Task 3.4); no access checks yet (Task 3.7).
   - `StockVouchersController`: inject `IVoucherPrintService`; action
     ```csharp
     /// 01-VT / 02-VT of one voucher as Excel or PDF, at the voucher's prices, at cost or without money.
     [HttpGet("{id:guid}/print")]
     public async Task<IActionResult> Print(Guid id, [FromQuery] ExportFormat format,
         [FromQuery] StockPrintPriceMode priceMode = StockPrintPriceMode.Voucher, CancellationToken ct = default)
     {
         var file = await _print.PrintStockVouchersAsync(new[] { id }, priceMode, format, ct);
         return File(file.Bytes, file.ContentType, file.FileName);
     }
     ```
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <service, interface, controller, test file> && git commit -m "feat(vouchers): print one stock voucher at its prices"`

### Task 3.4 — VAT rows that add up

1. **Write the failing tests** in `StockVoucherPrintTests`:
   - `Vat_inside_the_price_of_a_sale`: a tracked product `CreateInventoryProductAsync(NewCode("V"), priceIncludesVat: true)`; stock it (`NKH` 10-01, 10 × 500,000); a customer; `XBH` 10-05 with line(p, 2, 1_100_000) and `VatRate = 10` → one row `"Cộng"` 2,200,000 (= `v.Total`), no `"Thuế GTGT"` row, and `"Trong đó thuế GTGT đã gồm trong giá: 200.000"` on a row below `"Cộng"`.
   - `Vat_left_out_of_the_total`: `UpdateInventorySettingsAsync(s => s.NetExcludesVat = true)`; `NKH` 10-05 with line(a, 10, 100,000), `VatRate = 10` → `v.VatTotal` 100,000 and `v.Total` 1,000,000; print → one row `"Cộng"` 1,000,000, no `"Thuế GTGT"`, no VAT-included line. Then `UpdateInventorySettingsAsync(s => s.NetExcludesVat = false)` and print again → the same rows (the print reads the stored totals).
   - `Rows_add_up`: for the voucher of `Prints_a_stock_in_with_its_totals` and a stock-out with an order discount and freight (stock its product first, e.g. `NKH` on 10-01; a stock-out without stock answers 422 under the default `NegativeStockPolicy = Warn`), `"Cộng tiền hàng" − "Chiết khấu" + "Thuế GTGT" + "Cước vận chuyển" == "Tổng cộng" == v.Total` (absent rows count as 0).
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — `Vat_inside_the_price_of_a_sale` finds no VAT-included line (the other two may already pass; they guard the rule).
3. **Write the minimal implementation:** project `VatAmount` / `PriceIncludesVat` (already there) and set `VatIncludedLine = $"Trong đó thuế GTGT đã gồm trong giá: {Money(x)}"` when the voucher is a stock-out and `x = Σ VatAmount` of its `PriceIncludesVat` lines is greater than 0, in `Voucher` mode only.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <service and test file> && git commit -m "feat(vouchers): VAT rows of printed stock vouchers"`

### Task 3.5 — Cost mode from the stock ledger

1. **Write the failing tests** in `StockVoucherPrintTests`:
   - `Cost_of_a_stock_in_is_its_ledger_value`: default settings (`PurchaseCostIncludesVat = true`); an untracked product `u` (`track: false`); `NMH` 10-05 with a supplier, line(a, 10, 100,000) `VatRate = 10` and line(u, 1, 50,000). The ledger row (`LedgerOfAsync(v.Id).Single()`) has `InValue` 1,100,000 while the line's `InboundValue` is 1,000,000 (the DTO has no `InboundValue`: read it with `InDbAsync(db => db.StockVoucherLines.Where(l => l.StockVoucherId == v.Id && l.ProductId == a).Select(l => l.InboundValue).SingleAsync())`). Print `format=xlsx&priceMode=Cost` → only the row of `"Hàng {a}"` (no row for `u`) with unit price 110,000 and amount 1,100,000 (= the ledger `InValue`); one `"Cộng"` row 1,100,000; words `VndAmountInWords.Read(1_100_000)`; no `"Giá vốn tạm tính"` note.
   - `Cost_of_a_stock_out_is_its_ledger_cost`: `NKH` `"2026-09-02 08:00"` a × 10 at 100,000; `XKH` `"2026-09-10 09:00"` a × 4 at 150,000 (a closed period). Print `priceMode=Cost` → unit price = the ledger row's `UnitCost`, amount = its `CostAmount`, the same as the stock card row of that voucher (`GET /api/reports/stock-card?productId={a}&warehouseId={DefaultWarehouseId}&from=2026-09-01&to=2026-09-30`, row with `SourceId == out.Id`); no `"Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc)."`.
   - `Current_period_cost_is_provisional`: today in VN (`VnTime.ToVnDate(DateTimeOffset.UtcNow)`, written as `today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)`, because `Vn()` parses that exact format and a `DateOnly` interpolates with the current culture), an `NKH` at `"{today} 00:01"` and an `XKH` at `"{today} 00:02"`; print the stock-out with `priceMode=Cost` → the note `"Giá vốn tạm tính (kỳ tính giá vốn chưa kết thúc)."`; the stock-in with `priceMode=Cost` → no note.
   - `Cost_needs_the_cost_permission`: `CreateClientWithPermissionsAsync("vpc_" + suffix, MainBranchId, Permissions.StockOut.View)` → print a stock-out with `priceMode=Cost` → 403, `Error.Message == "Cần quyền xem giá vốn để in phiếu theo giá vốn."`; with `priceMode=Voucher` → 200.
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — 500 (`NotSupportedException`) for Cost.
3. **Write the minimal implementation:** before any read, `priceMode == Cost && !_currentUser.HasPermission(Permissions.Inventory.ViewCost)` → `ForbiddenException("Cần quyền xem giá vốn để in phiếu theo giá vốn.")`. Inside the snapshot, for Cost: `_db.InventorySettings.AsNoTracking().SingleAsync(s => s.Id == 1)` and the ledger rows through the `(SourceType, SourceId)` index: `_db.InventoryLedger.AsNoTracking().Where(e => (e.SourceType == LedgerSourceType.StockIn || e.SourceType == LedgerSourceType.StockOut) && voucherIds.Contains(e.SourceId))`, then a dictionary by `SourceLineId` in memory (`SourceLineId` has no index; a query on it scans the whole ledger in production). Map per V11: tracked lines only; stock-in `UnitPrice = QtyIn > 0 ? R4(InValue / QtyIn) : null` (a private `R4`, see Reference files), `Amount = InValue`; stock-out `UnitCost`, `CostAmount`; `Totals = [("Cộng", Σ amounts)]`, `AmountInWords = Read(Σ)`; a stock-out for which `CostProvisional.IsProvisional(Date, settings.CostingPeriod, VnTime.ToVnDate(_clock.UtcNow))` is true gets the provisional note.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <service and test file> && git commit -m "feat(vouchers): print stock vouchers at cost"`

### Task 3.6 — No-money mode, cancelled vouchers at cost, vouchers without tracked lines

1. **Write the failing tests** in `StockVoucherPrintTests`:
   - `None_prints_quantities_only`: a stock-in with a tracked and an untracked line → `priceMode=None`: only the tracked row; its price and amount cells empty; `"Cộng"` without amount; `"- Tổng số tiền (viết bằng chữ): ……………………"`.
   - `Cancelled_voucher_at_cost_prints_without_money`: a cancelled stock-in → `priceMode=Cost` → 200, `"ĐÃ HỦY"`, the note `"Phiếu đã hủy, không có giá vốn."`, empty price and amount cells, words as dots.
   - `Voucher_without_tracked_lines`: an `NKH` with only an untracked line → `priceMode=None` → no item row, `"Cộng"` without amount, the note `"Phiếu không có hàng theo dõi tồn kho."`, `$"- Nhập tại kho: {KHO01 name} Địa điểm: ……………………"` (the voucher's warehouse).
2. **Run the tests to verify they fail:** same filter. Expected: FAIL — 500 (`NotSupportedException`) for None; the cancelled case answers without the note.
3. **Write the minimal implementation:** `None` per V11; effective mode `None` with the cancelled note when `Cost` meets a cancelled voucher; the "no printed line" note and empty totals for Cost and None; warehouse names fall back to the voucher's warehouse when no line is printed.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <service and test file> && git commit -m "feat(vouchers): no-money and cancelled stock voucher prints"`

### Task 3.7 — Batch print, access and errors

1. **Write the failing tests** in `StockVoucherPrintTests`:
   - `Prints_selected_vouchers_in_date_order`: `In1` (`NKH`, `"2026-10-01 08:00"`, product `a` × 10), `Out1` (`XKH`, `"2026-10-05 09:00"`, `a` × 2, so it has stock), `In2` (`NKH`, `"2026-10-06 08:00"`); `POST /api/stock-vouchers/print?format=xlsx` with `new StockVoucherPrintRequest { Ids = [In2.Id, Out1.Id, In1.Id, In2.Id], PriceMode = StockPrintPriceMode.None }` → worksheets `[In1.Code, Out1.Code, In2.Code]`, file name `^PhieuNhapXuat_\d{8}-\d{4}\.xlsx$`; `[In1, In2]` → `^PhieuNhapKho_`; `[Out1]` alone through POST → `{Out1.Code}.xlsx`.
   - `DashboardSignalTests.WithoutHost.Every_document_controller_is_tagged`: the skipped actions are now `stockAt`, the cash `print` POST and the stock `print` POST (`BeEquivalentTo(new[] { stockAt, cashPrint, stockPrint })`, order-free as set in Task 1.6).
   - `Errors`:
     - unknown id → 404; a batch with one unknown id → 404;
     - a voucher of another branch: `CreateBranchAsync(NewCode("VB"))`, `SetBranchOpeningDateAsync(b, 2026-10-01)`, `CreateWarehouseAsync(b, NewCode("KB"))`, a client working in `b` creates an `NKH` there dated on or after 10-01 (`r.WarehouseId = whB`; the opening date refuses earlier dates) → printing it with `_client` → 403 `"Phiếu thuộc chi nhánh khác."`;
     - a user with `Permissions.StockIn.View` only printing a stock-out → 403;
     - `Ids = []` → 422 `EXPORT_NO_TARGETS`; a user without `inventory.view_cost` posting `Ids = []` with `PriceMode = Cost` → 403 (the cost check comes first);
     - `GET …/print?format=xlsx&priceMode=Foo` → 400 (status only: model binding answers with `ValidationProblemDetails`; this one passes at RED already and guards the binding);
     - `POST` with the raw JSON body `{"ids":["<id>"],"priceMode":7}` → 400 with `Error.Details` key `priceMode`.
2. **Run the tests to verify they fail:** `--filter "FullyQualifiedName~StockVoucherPrintTests|FullyQualifiedName~DashboardSignalTests"`. Expected: FAIL — 404/405 on the POST, the reflection test, and the access cases answer 200.
3. **Write the minimal implementation:**
   - `StockVouchersController`:
     ```csharp
     /// Several vouchers, one sheet each. A POST because a list of ids does not fit a URL; it changes nothing.
     [HttpPost("print")]
     [SkipDashboardSignal]
     public async Task<IActionResult> PrintMany(
         [FromBody] StockVoucherPrintRequest request, [FromQuery] ExportFormat format, CancellationToken ct)
     {
         var file = await _print.PrintStockVouchersAsync(request.Ids, request.PriceMode, format, ct);
         return File(file.Bytes, file.ContentType, file.FileName);
     }
     ```
   - `PrintStockVouchersAsync`: first `!Enum.IsDefined(priceMode)` → `ValidationDomainException(new Dictionary<string, string[]> { ["priceMode"] = ["Chế độ giá không hợp lệ."] }, null)`; then the cost permission (Task 3.5); then `EnsureSheetCount(distinct.Count, VouchersTooMany)`; inside the snapshot the per-id checks of V6 with `StockVoucherPermissions.View(row.Type)` and `NotFoundException(nameof(StockVoucher), id)`.
4. **Run the tests to verify they pass:** same filter. Expected: PASS.
5. **Commit:** `git add <controller, service, the two test files> && git commit -m "feat(vouchers): print several stock vouchers, access rules"`

### Task 3.8 — Phase checkpoint

1. `dotnet build OrderMgmt.sln`.
2. `dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133|FullyQualifiedName~DashboardSignal|FullyQualifiedName~StockVoucher"`.
3. Full suite with the trx logger; compare with `BASELINE.md` test by test; fix a new failure (failing test first).
4. Mark Phase 03 `[x]` in this file and in `SUMMARY.md`; commit `git add docs/plans/261014-1632-tt133-voucher-print-forms && git commit -m "chore(plan): phase 03 complete"`.

## Verification

```bash
export TEST_DB_CONNECTION="Host=localhost;Port=5432;Database=qldonhang_integtest_vprint;Username=postgres;Password=1"
cd backend
dotnet build OrderMgmt.sln
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Tt133"
dotnet test tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~DashboardSignal|FullyQualifiedName~StockVoucher"
dotnet test OrderMgmt.sln --logger "trx;LogFileName=phase03.trx" --results-directory "$TEMP/vprint-trx"   # compare with BASELINE.md
```

## Exit Criteria

- `GET api/stock-vouchers/{id}/print` and `POST api/stock-vouchers/print` return 01-VT / 02-VT in the three price modes of V11, with the line rules of V10.
- `Voucher` rows always add up to the voucher total; `Cost` equals the stock ledger and the stock card, also with `PurchaseCostIncludesVat = true`.
- The errors of V6, including 400 for an undefined `priceMode` and 403 for `Cost` without the cost permission.
- No failure beyond `BASELINE.md`.
