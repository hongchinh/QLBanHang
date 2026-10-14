# Mẫu in chứng từ TT133 — Phiếu thu, Phiếu chi, Phiếu nhập kho, Phiếu xuất kho

> Brainstorm: 2026-10-13 20:42:09 · Tiếp nối [brainstorm sổ TT133](../261012-2237-tt133-report-forms/SUMMARY.md) (mục non-goal "Mẫu in phiếu thu/chi, phiếu nhập/xuất") và [plan sổ TT133](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md) · Review đối chiếu code: 2026-10-14, đã sửa theo mục [Review đối chiếu code](#review-đối-chiếu-code-2026-10-14) · Trạng thái: **đã hiện thực** theo [plan mẫu in chứng từ TT133](../../plans/261014-1632-tt133-voucher-print-forms/SUMMARY.md); còn chờ kế toán trả lời các câu hỏi và duyệt bản in ([danh sách kiểm tra go-live](../../plans/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md))

## Problem framing

Phần mềm lập được phiếu thu, phiếu chi, phiếu nhập kho và phiếu xuất kho, nhưng chưa in được phiếu nào. Kế toán và thủ kho cần bản in có chữ ký để lưu chứng từ và giao cho khách/nhà cung cấp.

Yêu cầu: xuất Excel và PDF theo mẫu chứng từ của **Thông tư 133/2016/TT-BTC** (Phụ lục 3), in được từ màn phiếu và từ màn danh sách (tìm kiếm).

| Mẫu | Tên | Chứng từ trong phần mềm |
|---|---|---|
| 01-TT | Phiếu thu | `CashVoucher`, `Type = CashVoucherType.Receipt` |
| 02-TT | Phiếu chi | `CashVoucher`, `Type = CashVoucherType.Payment` |
| 01-VT | Phiếu nhập kho | `StockVoucher`, `Type = StockDirection.In` |
| 02-VT | Phiếu xuất kho | `StockVoucher`, `Type = StockDirection.Out` |

Dữ liệu phiếu đã gần đủ cho mẫu. Ba thứ còn thiếu là thông tin đơn vị, số tiền bằng chữ và ô TK Nợ/Có. Hai thứ đầu đã có từ plan sổ TT133; thứ ba không làm vì hệ thống tài khoản là non-goal của dự án.

## Goals

- Xuất **Excel và PDF** cho 4 mẫu, 1 liên: 01-TT và 02-TT khổ **A5 ngang**, 01-VT và 02-VT khổ **A4 dọc**.
- In từ **màn phiếu** (phiếu đã lưu).
- In từ **màn danh sách**: nút in trên từng dòng, và chọn nhiều dòng để in hàng loạt. Mỗi phiếu là một sheet Excel / một trang PDF.
- Phiếu kho có **ba chế độ giá** chọn khi in: giá trên phiếu, giá vốn, không in tiền.

## Non-goals

- Phiếu chuyển quỹ.
- Bảng kê danh sách phiếu đang lọc (xuất chính màn tìm kiếm).
- Template do admin tự sửa bố cục, mẫu riêng theo người dùng.
- Cài đặt khổ giấy hoặc số liên. Cần nhiều liên thì đặt số bản trong hộp thoại máy in.
- Hệ thống tài khoản: các ô Nợ, Có và Quyển số để dòng chấm.
- Ngoại tệ: bỏ dòng "Tỷ giá ngoại tệ" và "Số tiền quy đổi" của 01-TT/02-TT.
- Trang in HTML trên trình duyệt.
- Quyền in riêng.
- TT99.

## Constraints & assumptions

- **Dựa trên plan sổ TT133**, đã merge vào `qlbanhang/main` (PR #12, 2026-10-14). Nhánh mới tạo từ `qlbanhang/main`. Dùng lại, không làm trùng:
  - `CompanyProfile` và màn Thông tin đơn vị;
  - `VndAmountInWords`;
  - `ISpreadsheetPdfConverter` (LibreOffice);
  - `Tt133FormContext`, `ExportFile`, `ExportFormat`;
  - `UnprocessableException` (422) và `ServiceUnavailableException` (503);
  - `Tt133ExportOptions.MaxSheets` (300);
  - phần tải blob và gợi ý "chưa có thông tin đơn vị" của frontend.
- Tên các contract trên đã đối chiếu với code sau khi merge. Một số phần chưa gọi lại được nguyên trạng, plan phải tách ra trước:
  - `EnsureSheetCount` (422), `ToFileAsync` (chuyển PDF, 503) và `BuildContextAsync` (dựng `Tt133FormContext`) đang là `private` trong `Tt133ExportService`. Plan tách thành phần dùng chung trong `Application/Tt133/`, sổ giữ nguyên hành vi. Thông báo vượt giới hạn hiện viết cho sổ ("Số sổ cần xuất… Hãy thu hẹp điều kiện lọc"), nên nơi gọi truyền câu của mình.
  - `downloadTt133` ở frontend gắn cứng `/reports/{report}/export` và luôn tải file về. Plan tách phần tải blob thành hàm nhận URL, dùng chung cho sổ và phiếu.
- Không có migration mới và không đổi công thức số liệu. Bản in là một cách trình bày của phiếu, nên số trên giấy khớp số trên màn hình.
- Bố cục mẫu lấy từ nguồn thứ cấp. Kế toán đối chiếu bản in với bản gốc Phụ lục 3 trước go-live.
- In được cho cả quỹ tiền mặt lẫn quỹ ngân hàng, vì phần mềm gọi chung là phiếu thu/chi.

## Approaches considered

### Cách dựng file

| Tiêu chí | **A. ClosedXML dựng bằng code** | B. Template .xlsx từng mẫu | C. In HTML + Excel backend |
|---|---|---|---|
| Nhất quán với sổ TT133 | Cùng cách, dùng lại hạ tầng | Khác cách | Trái non-goal của plan sổ |
| In nhiều phiếu một lần | Tự nhiên: mỗi phiếu một sheet | Khó: nhân bản sheet mẫu | Dễ trên HTML, vẫn phải làm Excel |
| Số dòng hàng thay đổi | Tự nhiên | Chèn dòng làm lệch merge | Dễ |
| Ba chế độ giá | Một class, rẽ nhánh theo tham số | Tăng số mẫu hoặc ẩn cột bằng code | Hai bố cục phải rẽ nhánh |
| Admin tự sửa bố cục | Không | Có | Không |
| Tốc độ bấm In | Chờ LibreOffice | Chờ LibreOffice | Nhanh nhất |
| Kiểm thử | Dễ: đọc lại workbook | Trung bình | Khó |

**Chọn A.** Mẫu chứng từ do Bộ Tài chính quy định nên nhu cầu sửa bố cục thấp. In hàng loạt và ba chế độ giá là chỗ B yếu nhất. C phải duy trì hai bố cục.

### In từ màn danh sách

| Phương án | Quyết định |
|---|---|
| **In từng phiếu + in nhiều phiếu đã chọn** | **Chọn** |
| Chỉ in từng phiếu | Không: cuối ngày kế toán in cả loạt |
| In phiếu + xuất bảng kê danh sách | Không: bảng kê không phải mẫu TT133, để sau nếu cần |
| Chỉ xuất bảng kê | Không |

### Cột tiền trên phiếu kho

Mẫu 01-VT/02-VT chỉ có Số lượng × Đơn giá = Thành tiền. Phiếu của phần mềm có thêm chiết khấu, VAT và cước.

| Phương án | Quyết định |
|---|---|
| Chỉ giá mua/bán trên phiếu | Không: thủ kho cần bản không tiền |
| Chỉ giá kho (giá vốn) | Không: số khác tổng phiếu, khách không dùng được |
| **Chọn khi in: giá trên phiếu / giá vốn / không in tiền** | **Chọn** |

### Khổ giấy

**Chọn: thu/chi A5 ngang, nhập/xuất A4 dọc, 1 liên.** Không chọn: tất cả A4; 2 liên trên một tờ A4; cài đặt khổ giấy theo loại phiếu.

### Thứ tự triển khai

**Chọn: thiết kế riêng, chạy sau plan sổ TT133.** Không chọn: thêm phase vào plan sổ đã duyệt (plan lớn thêm); làm trước độc lập (phải sửa lại Phase 01 của plan sổ).

## Recommended approach (đã chốt)

### Quy ước chung của 4 mẫu

| Ô trên mẫu | Nguồn |
|---|---|
| Đơn vị, Địa chỉ | `CompanyProfile` và chi nhánh của phiếu: `{Tên công ty} – {Tên chi nhánh}`, địa chỉ chi nhánh, không có thì địa chỉ công ty. Không có dòng MST như khối đơn vị của sổ |
| Mã mẫu | `Mẫu số 01-TT` (hoặc 02-TT, 01-VT, 02-VT) và dòng "Ban hành theo Thông tư số 133/2016/TT-BTC ngày 26/8/2016 của Bộ Tài chính" |
| Ngày … tháng … năm | `VoucherAt` đổi sang ngày Việt Nam |
| Số | `Code` |
| Quyển số, Nợ, Có | Dòng chấm |
| Giám đốc | `LegalRepresentativeName` |
| Kế toán trưởng | `ChiefAccountantName` |
| Người lập phiếu | Họ tên người tạo phiếu (`Owner`) |
| Thủ quỹ, Thủ kho, Người nộp/nhận tiền, Người giao/nhận hàng (khối chữ ký) | Chỉ in chức danh và "(Ký, họ tên)", tên để trống |
| Phiếu đã hủy | Vẫn in được, có chữ **"ĐÃ HỦY"** dưới tiêu đề. Phiếu kho đã hủy ở chế độ `Cost`: xem Ba chế độ giá |
| Chưa có thông tin đơn vị | Vẫn in, các ô đơn vị để trống |

Đọc phiếu theo mẫu của `CashVoucherService.LoadDtoAsync`: `IgnoreQueryFilters()` để đối tác, lý do, kho và người dùng đã xóa mềm vẫn in đúng tên; phiếu và dòng phiếu tự lọc `!IsDeleted`, vì dòng bị bỏ khi sửa phiếu chỉ bị xóa mềm. Phiếu đã xóa trả 404.

### 01-TT Phiếu thu / 02-TT Phiếu chi

| Ô | Nguồn |
|---|---|
| Họ và tên người nộp tiền / người nhận tiền | `PersonName`; trống thì `PartnerName` |
| Địa chỉ | `PartnerAddress` |
| Lý do nộp / Lý do chi | `Description`; trống thì diễn giải các dòng (`CashVoucherLine.Description` theo `SortOrder`, nối bằng "; "); vẫn trống thì tên Lý do thu/chi (`Reason.Name`) |
| Số tiền | `Total` |
| Viết bằng chữ | `VndAmountInWords.Read(Total)` |
| Kèm theo … chứng từ gốc | `AttachedDocs` là số nguyên: "Kèm theo: {n} chứng từ gốc". Là chữ khác: "Kèm theo: {AttachedDocs}". Trống: dòng chấm |
| Đã nhận đủ số tiền (viết bằng chữ) | Dòng chấm |

Khối chữ ký:
- 01-TT: Giám đốc, Kế toán trưởng, Người nộp tiền, Người lập phiếu, Thủ quỹ.
- 02-TT: Giám đốc, Kế toán trưởng, Thủ quỹ, Người lập phiếu, Người nhận tiền.

### 01-VT Phiếu nhập kho / 02-VT Phiếu xuất kho

| Ô | Nguồn |
|---|---|
| Họ và tên người giao (01-VT) / người nhận hàng (02-VT) | `HandlerName`; trống thì `PartnerName` |
| Theo … số … ngày … của … (01-VT) | Dòng chấm; riêng "của" điền `PartnerName` |
| Địa chỉ (bộ phận) (02-VT) | `PartnerAddress` |
| Lý do xuất kho (02-VT) | `Reason.Name` (`StockReason`) |
| Nhập tại kho / Xuất tại kho | Kho của các dòng được in. Mỗi dòng có kho riêng (`StockVoucherLine.WarehouseId`, mặc định là kho của phiếu). Một kho: tên kho đó. Nhiều kho: tên các kho nối bằng ", " theo thứ tự dòng, và bảng thêm cột **Kho** (mã kho) sau Đơn vị tính. "Địa điểm" để dòng chấm |
| STT | Thứ tự dòng theo `SortOrder` |
| Tên, nhãn hiệu, quy cách | `ProductName` (snapshot trên dòng). Dòng tính theo m, m², m³ (`PricingMode` khác `PerUnit`) thêm quy cách trong ngoặc: các kích thước có giá trị (`Length`×`Width`×`Thickness` mm) và `SheetCount` tấm, ví dụ `Kính cường lực (1200×2400×5 mm, 10 tấm)` |
| Mã số | `ProductCode` |
| Đơn vị tính | `UnitName` (snapshot; dòng tính theo m, m², m³ là m, m², m³) |
| Số lượng theo chứng từ (01-VT) / yêu cầu (02-VT) | `Quantity` (dòng tính theo m, m², m³ là số đã quy đổi) |
| Số lượng thực nhập / thực xuất | `Quantity` |
| Đơn giá, Thành tiền | Theo chế độ giá, xem bảng dưới |
| Số chứng từ gốc kèm theo | Dòng chấm |

Khối chữ ký:
- 01-VT: Người lập phiếu, Người giao hàng, Thủ kho, Kế toán trưởng.
- 02-VT: Người lập phiếu, Người nhận hàng, Thủ kho, Kế toán trưởng, Giám đốc.

**Ba chế độ giá** (`StockPrintPriceMode`):

| Chế độ | Dòng được in | Đơn giá / Thành tiền | Dưới bảng |
|---|---|---|---|
| `Voucher` — giá trên phiếu (mặc định) | Mọi dòng | `UnitPrice`, `Amount` | Cộng tiền hàng (`GoodsAmount`), Chiết khấu (`LineDiscountTotal` + `OrderDiscount`), Thuế GTGT (xem dưới), Cước (`Freight`), **Tổng cộng** = `Total`; bằng chữ theo `Total`. Dòng có giá trị 0 thì bỏ |
| `Cost` — giá vốn | Chỉ dòng `TrackInventory` | Dòng sổ kho của dòng phiếu (`InventoryLedgerEntry.SourceLineId`). Nhập: đơn giá `R4(InValue / QtyIn)`, thành tiền `InValue`. Xuất: `UnitCost`, `CostAmount` | Cộng; bằng chữ theo tổng cộng. Giá vốn tạm tính thì thêm ghi chú dưới tiêu đề |
| `None` — không in tiền | Chỉ dòng `TrackInventory` | Để trống | Dòng Cộng và bằng chữ để dòng chấm |

**Thuế GTGT ở chế độ `Voucher`.** `Total = Σ NetAmount + Freight` (`StockVoucherCalculator`), và VAT không phải lúc nào cũng được cộng vào `NetAmount`:
- phiếu xuất, dòng có `PriceIncludesVat`: VAT tách ra từ đơn giá, đã nằm trong Cộng tiền hàng;
- cài đặt `NetExcludesVat = true` lúc lưu phiếu: VAT được tính nhưng không cộng vào tổng.

Vì vậy dòng Thuế GTGT chỉ in phần VAT thực sự cộng vào tổng: `Total − Freight − (GoodsAmount − LineDiscountTotal − OrderDiscount)`. Số này tính từ các tổng đã lưu, không đọc lại cài đặt `NetExcludesVat`, vì cài đặt có thể đổi sau khi lưu phiếu. Phiếu xuất có dòng giá gồm VAT thì thêm một dòng chữ nghiêng dưới Tổng cộng: "Trong đó thuế GTGT đã gồm trong giá: X", với X = Σ `VatAmount` của các dòng đó. Dòng này không tham gia phép cộng. Như vậy luôn có Cộng tiền hàng − Chiết khấu + Thuế GTGT + Cước = Tổng cộng.

**Giá vốn.** `Cost` cần quyền `inventory.view_cost`. Giá vốn đọc từ chính dòng sổ kho mà thẻ kho đọc, nên khớp thẻ kho. Không dùng `InboundValue` của dòng phiếu: khi `PurchaseCostIncludesVat = true` (mặc định), giá trị nhập kho là `InboundValue + VatAmount`. Giá vốn là tạm tính khi kỳ giá vốn chứa ngày Việt Nam của `VoucherAt` chưa kết thúc, cùng quy tắc `IsProvisional` của thẻ kho. Tổng in ở chế độ `Voucher` bằng tổng phiếu trên màn hình.

**Trường hợp biên của phiếu kho:**
- Phiếu đã hủy ở chế độ `Cost`: khi hủy, dòng sổ kho của phiếu bị xóa nên không còn giá vốn. Sheet đó in như `None`, kèm ghi chú "Phiếu đã hủy, không có giá vốn" dưới tiêu đề. Không trả lỗi, để một phiếu hủy không chặn cả lô.
- Ở chế độ `Cost` hoặc `None`, phiếu không có dòng `TrackInventory` nào vẫn in, bảng không có dòng, kèm ghi chú "Phiếu không có hàng theo dõi tồn kho".

### Backend

| Lớp | Thành phần | Trách nhiệm |
|---|---|---|
| Application `Tt133/` | Phần dùng chung tách từ `Tt133ExportService` (tên chốt khi viết plan) | Đếm sheet (422), chuyển PDF (503), dựng `Tt133FormContext`. Sổ giữ nguyên hành vi |
| Application `Tt133/Vouchers/` | `IVoucherPrintService` / `VoucherPrintService` | Kiểm quyền và chi nhánh, nạp phiếu trong một snapshot, đọc dòng sổ kho cho chế độ `Cost`, dựng model in, gọi renderer. Chuyển PDF sau khi đóng snapshot |
| | `CashVoucherPrintModel`, `StockVoucherPrintModel` | Model phẳng đã áp quy tắc fallback, chế độ giá, dòng Thuế GTGT, quy cách và kho. Renderer không đọc entity |
| | `StockPrintPriceMode { Voucher = 0, Cost = 1, None = 2 }` | Tham số chế độ giá |
| | `IVoucherFormRenderer` | `RenderCashVouchers(context, models)`, `RenderStockVouchers(context, models)`; Infrastructure hiện thực |
| Infrastructure `Excel/Tt133/Vouchers/` | `VoucherFormBuilder` | Phần chung: khối đơn vị (Đơn vị, Địa chỉ) và mã mẫu; tiêu đề, ngày, số, Nợ/Có; dòng nhãn–giá trị; khối chữ ký; page setup A5 ngang / A4 dọc |
| | `CashVoucherForm`, `StockVoucherForm` | Mỗi class một cặp mẫu (thu/chi, nhập/xuất) |
| WebApi | Action mới trong `CashVouchersController` và `StockVouchersController` | Xem API |

`VoucherFormBuilder` là builder riêng và không sửa `Tt133FormBuilder`: khung sổ (bảng dài, đầu kỳ/cuối kỳ, "Sổ này có … trang", page setup cứng A4) khác khung chứng từ. Chỉ tách hàm dùng chung khi hai builder trùng mã thật.

### API

Quyền là quyền xem của loại phiếu tương ứng: `receipts.view`, `payments.view`, `stock_in.view`, `stock_out.view`. Phạm vi chi nhánh theo `ICurrentBranch`, như màn phiếu.

```
GET  api/cash-vouchers/{id}/print?format=xlsx|pdf
POST api/cash-vouchers/print?format=xlsx|pdf          body { ids: Guid[] }
GET  api/stock-vouchers/{id}/print?format=xlsx|pdf&priceMode=Voucher|Cost|None
POST api/stock-vouchers/print?format=xlsx|pdf         body { ids: Guid[], priceMode }
```

- In hàng loạt đi bằng POST vì danh sách id dài vượt giới hạn dòng request của Kestrel (plan sổ, quyết định T5).
- Thứ tự sheet: `VoucherAt` rồi `Code`. Tên sheet là số phiếu, làm sạch và làm duy nhất theo quy tắc T12 của plan sổ (`Tt133FormBuilder.SheetName`).
- Giao diện chỉ gửi một loại phiếu mỗi lô, vì màn danh sách tách theo loại. Server không chặn lô trộn phiếu thu với phiếu chi, hoặc phiếu nhập với phiếu xuất: mỗi sheet theo mẫu của loại phiếu đó.
- Tên file: một phiếu là `{Code}.{xlsx|pdf}`. Nhiều phiếu là `PhieuThu_`, `PhieuChi_`, `PhieuNhapKho_` hoặc `PhieuXuatKho_` kèm `{yyyyMMdd-HHmm}` giờ Việt Nam; lô trộn là `PhieuThuChi_…` hoặc `PhieuNhapXuat_…`.
- `priceMode` mặc định là `Voucher`.

### Frontend

- Component chung `VoucherPrintButton`: menu **In (PDF)** và **Excel**. PDF mở trong tab mới để in: mở tab `about:blank` trước mọi `await` rồi gán URL blob, như trang báo giá (`quotation-form-page.tsx`), để trình duyệt không chặn popup. Excel tải về.
- **Màn phiếu thu/chi và phiếu kho:** nút chỉ bật khi phiếu đã lưu và form không có thay đổi chưa lưu.
- **Màn danh sách:** nút in trên từng dòng; thêm cột checkbox và thanh "Đã chọn N phiếu — In ▾". Lựa chọn chỉ trong trang đang hiển thị và bị xóa khi đổi trang hoặc đổi bộ lọc.
- **Phiếu kho:** hộp thoại chọn chế độ giá trước khi in, nhớ lựa chọn lần trước trong `localStorage`. Mục Giá vốn ẩn khi không có quyền `inventory.view_cost`.
- Chưa có tên đơn vị: vẫn in, hiện gợi ý kèm link sang Cài đặt (dùng lại của sổ TT133).

### Lỗi

Kiểm quyền và chi nhánh theo cùng thứ tự với `GetAsync` của màn phiếu.

| Tình huống | Kết quả |
|---|---|
| Phiếu không tồn tại hoặc đã xóa | 404 |
| Thiếu quyền xem loại phiếu | 403 |
| Phiếu thuộc chi nhánh khác | 403 "Phiếu thuộc chi nhánh khác.", như màn phiếu |
| `priceMode=Cost` không có quyền giá vốn | 403 |
| `ids` rỗng | 422 `EXPORT_NO_TARGETS` |
| Số phiếu vượt `MaxSheets` | 422 `EXPORT_TOO_MANY_SHEETS` "Số phiếu cần in ({count}) vượt giới hạn {max}. Hãy chọn ít phiếu hơn." |
| Một id trong lô bị một trong các lỗi trên | Cả lô trả lỗi đó (404 hoặc 403), không in thiếu âm thầm |
| `format` hoặc `priceMode` sai | 400 |
| LibreOffice lỗi hoặc quá thời gian | 503 `PDF_EXPORT_FAILED` "Xuất PDF thất bại, hãy dùng Excel." |

### Kiểm thử

- **Unit, đọc lại workbook:**
  - từng mẫu có đủ ô, mã mẫu, khối chữ ký đúng thứ tự;
  - khổ giấy và hướng giấy (A5 ngang, A4 dọc);
  - fallback người nộp/nhận, lý do (diễn giải, diễn giải các dòng, tên lý do), người giao/nhận; "Kèm theo" dạng số và dạng chữ;
  - ba chế độ giá, kể cả lọc dòng `TrackInventory` và bỏ dòng tổng có giá trị 0;
  - Cộng tiền hàng − Chiết khấu + Thuế GTGT + Cước = Tổng cộng với mọi tổ hợp nhập/xuất × `PriceIncludesVat` × `NetExcludesVat`, và dòng "Trong đó thuế GTGT đã gồm trong giá";
  - quy cách của dòng tính theo m, m², m³; phiếu nhiều kho (tên kho nối và cột Kho);
  - chữ "ĐÃ HỦY"; phiếu kho đã hủy ở chế độ `Cost`; phiếu không có dòng được in;
  - số sheet, thứ tự và tên sheet khi in lô.
- **Integration trên `qldonhang_integtest`:**
  - tổng in bằng tổng phiếu;
  - giá vốn in khớp thẻ kho, cả phiếu nhập khi `PurchaseCostIncludesVat = true`;
  - dòng đã bỏ khi sửa phiếu không được in; đối tác, lý do, kho đã xóa mềm vẫn in tên;
  - quyền và chi nhánh: 403 (thiếu quyền, chi nhánh khác), 404 (không tồn tại, đã xóa);
  - 422 khi rỗng và khi vượt giới hạn;
  - content-type và tên file.
- **Frontend:** nút in tắt khi form có thay đổi chưa lưu; chọn nhiều dòng và thanh in lô; hộp thoại chế độ giá; ẩn Giá vốn khi thiếu quyền; tab PDF được mở trước khi gọi API.
- **Thủ công:** in thật 4 mẫu trên Docker (LibreOffice) để kiểm khổ giấy và ngắt trang.
- **Go-live gate:** kế toán duyệt bản in 4 mẫu so với bản gốc Phụ lục 3 TT133.

### Chia phase dự kiến

| Phase | Nội dung |
|---|---|
| P1 | Tách phần dùng chung khỏi `Tt133ExportService` và `downloadTt133`; `VoucherFormBuilder`, `IVoucherPrintService`, 01-TT và 02-TT, hai endpoint phiếu thu/chi, `VoucherPrintButton`, nút in ở màn phiếu thu/chi và danh sách (kèm chọn nhiều dòng) |
| P2 | 01-VT và 02-VT, ba chế độ giá, hai endpoint phiếu kho, hộp thoại chế độ giá, nút in ở màn phiếu kho và danh sách |
| P3 | Docs (SUMMARY, conventions, directory-structure, product-goals), in thật trên Docker, kiểm tra cuối |

## Open questions

**Kế toán / PO** (không chặn việc phát triển; là go-live gate, đã đưa vào [danh sách kiểm tra go-live](../../plans/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md), phần 1; chưa có câu trả lời):
- Phiếu thu/chi của **quỹ ngân hàng** có in theo 01-TT/02-TT không, hay kế toán chỉ dùng giấy báo Có / ủy nhiệm chi của ngân hàng? Thiết kế hiện cho in cả hai loại quỹ.
- Có chấp nhận để trống ô Nợ, Có và Quyển số cho kế toán ghi tay không?
- Cột "Số lượng theo chứng từ / yêu cầu" in bằng số lượng thực nhập/xuất có đúng thực tế không, hay phải để trống cho thủ kho ghi?
- Chế độ `Voucher` thêm các dòng Chiết khấu, Thuế GTGT, Cước dưới bảng, ngoài mẫu gốc, và với phiếu xuất giá gồm VAT thì thêm dòng "Trong đó thuế GTGT đã gồm trong giá". Kế toán có chấp nhận không?
- Hàng tính theo m, m², m³ in quy cách (kích thước, số tấm) trong cột Tên. Cách ghi này có đúng nhu cầu của thủ kho không?
- Phiếu kho có dòng ở nhiều kho: in tên các kho ở dòng "Nhập/Xuất tại kho" và thêm cột Kho có được không, hay phải tách mỗi kho một phiếu in?

**Kỹ thuật** (đã chốt):
- Tên thành phần dùng chung: `Tt133ExportSupport` (`Application/Tt133/Services`); hàm tải blob của frontend: `requestBlob` / `saveBlob` (`lib/blob-download.ts`).
- A5 ngang qua LibreOffice, nhiều sheet trong một file: Kết quả kiểm tra A5 (Task 5.1): <<TO FILL>>

## Review đối chiếu code (2026-10-14)

Đối chiếu tài liệu với code sau khi plan sổ TT133 merge. Các trường dữ liệu, `CompanyProfile`, `MaxSheets`, mã lỗi 422/503, quyết định T5/T12 và route `api/cash-vouchers`, `api/stock-vouchers` đều khớp. Những điểm đã sửa:

| # | Phát hiện | Đã sửa |
|---|---|---|
| R1 | Giá vốn phiếu nhập lấy `InboundValue / Quantity`, nhưng sổ kho ghi `InboundValue + VatAmount` khi `PurchaseCostIncludesVat = true` (mặc định), nên lệch thẻ kho | Chế độ `Cost` đọc dòng sổ kho theo `SourceLineId` cho cả nhập lẫn xuất |
| R2 | Các dòng dưới bảng của chế độ `Voucher` không cộng ra `Total` khi phiếu xuất có giá gồm VAT hoặc khi `NetExcludesVat` | Dòng Thuế GTGT chỉ in phần VAT cộng vào tổng; thêm dòng chú thích VAT đã gồm trong giá; test phép cộng |
| R3 | Mỗi dòng phiếu kho có kho riêng; "tên kho của phiếu" sai khi các dòng ở nhiều kho | Lấy kho từ các dòng; nhiều kho thì nối tên và thêm cột Kho |
| R4 | Phiếu kho đã hủy không còn dòng sổ kho, chế độ `Cost` không có số | In như `None`, kèm ghi chú |
| R5 | `IgnoreQueryFilters()` áp cho cả câu truy vấn sẽ in cả dòng đã bỏ khi sửa phiếu | Theo mẫu `CashVoucherService.LoadDtoAsync`: tự lọc `!IsDeleted` cho phiếu và dòng |
| R6 | Tài liệu ghi phiếu chi nhánh khác trả 404 "như màn phiếu", nhưng màn phiếu trả 403 | Bảng lỗi theo màn phiếu: 404 không tồn tại hoặc đã xóa, 403 thiếu quyền hoặc chi nhánh khác |
| R7 | Hàng tính theo m, m², m³ không in kích thước, số tấm | Thêm quy cách vào cột Tên; hỏi thủ kho |
| R8 | Phần "dùng lại" của plan sổ đang `private` hoặc gắn cứng đường dẫn | Ghi rõ phần phải tách trong Constraints và P1; thông báo 422 riêng cho phiếu |
| R9 | Mở PDF ở tab mới sau `await` bị chặn popup | Mở tab trước `await`, như trang báo giá |
| R10 | Màn danh sách tách theo loại nên giao diện không gửi lô trộn | Tên file theo loại phiếu; server vẫn nhận lô trộn |
| R11 | Fallback lý do bỏ qua diễn giải các dòng (bắt buộc nhập); "Kèm theo" là chữ tự do | Thêm diễn giải các dòng vào fallback; quy tắc in "Kèm theo" |
| R12 | Trạng thái và bước tiếp theo đã cũ; câu hỏi kỹ thuật về tên controller, tên quyền, nguồn giá vốn đã trả lời được từ code; sai tên `StockDirection`, `Reason`, `GoodsAmount` | Cập nhật trạng thái, Next steps, Open questions và tên trong tài liệu |

## Next steps

1. ~~Gửi kế toán các câu hỏi Kế toán/PO ở trên.~~ Đã đưa vào [danh sách kiểm tra go-live](../../plans/261014-1632-tt133-voucher-print-forms/GO-LIVE-CHECKLIST.md); việc gửi và nhận câu trả lời vẫn mở.
2. ~~`write-plan` cho 3 phase.~~ Xong: [plan](../../plans/261014-1632-tt133-voucher-print-forms/SUMMARY.md) (6 phase, hiện thực xong).
3. ~~Cập nhật `docs/project-pdr/product-goals.md`.~~ Xong.
4. Kế toán trả lời 6 câu hỏi và duyệt bản in từng mẫu so với Phụ lục 3 bản gốc (phần 1 và 2 của danh sách kiểm tra go-live).
