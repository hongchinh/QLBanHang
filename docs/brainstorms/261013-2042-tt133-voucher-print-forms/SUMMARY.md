# Mẫu in chứng từ TT133 — Phiếu thu, Phiếu chi, Phiếu nhập kho, Phiếu xuất kho

> Brainstorm: 2026-10-13 20:42:09 · Tiếp nối [brainstorm sổ TT133](../261012-2237-tt133-report-forms/SUMMARY.md) (mục non-goal "Mẫu in phiếu thu/chi, phiếu nhập/xuất") và [plan sổ TT133](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md) · Trạng thái: thiết kế đã duyệt, chờ plan sổ TT133 chạy xong rồi viết plan

## Problem framing

Phần mềm lập được phiếu thu, phiếu chi, phiếu nhập kho và phiếu xuất kho, nhưng chưa in được phiếu nào. Kế toán và thủ kho cần bản in có chữ ký để lưu chứng từ và giao cho khách/nhà cung cấp.

Yêu cầu: xuất Excel và PDF theo mẫu chứng từ của **Thông tư 133/2016/TT-BTC** (Phụ lục 3), in được từ màn phiếu và từ màn danh sách (tìm kiếm).

| Mẫu | Tên | Chứng từ trong phần mềm |
|---|---|---|
| 01-TT | Phiếu thu | `CashVoucher`, `Type = Receipt` |
| 02-TT | Phiếu chi | `CashVoucher`, `Type = Payment` |
| 01-VT | Phiếu nhập kho | `StockVoucher`, `Type = In` |
| 02-VT | Phiếu xuất kho | `StockVoucher`, `Type = Out` |

Dữ liệu phiếu đã gần đủ cho mẫu. Ba thứ còn thiếu là thông tin đơn vị, số tiền bằng chữ và ô TK Nợ/Có. Hai thứ đầu do plan sổ TT133 cung cấp; thứ ba không làm vì hệ thống tài khoản là non-goal của dự án.

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

- **Làm sau plan sổ TT133.** Nhánh mới tạo từ `qlbanhang/main` đã chứa plan đó. Dùng lại, không làm trùng:
  - `CompanyProfile` và màn Thông tin đơn vị;
  - `VndAmountInWords`;
  - `ISpreadsheetPdfConverter` (LibreOffice);
  - `Tt133FormContext`, `ExportFile`, `ExportFormat`;
  - `UnprocessableException` (422) và `ServiceUnavailableException` (503);
  - `Tt133ExportOptions.MaxSheets`;
  - phần tải blob và gợi ý "chưa có thông tin đơn vị" của frontend.
- Nếu plan sổ đổi tên hoặc hình dạng các contract trên khi thực thi, plan của việc này theo bản đã hiện thực.
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
| Đơn vị, Địa chỉ | `CompanyProfile` và chi nhánh của phiếu: `{Tên công ty} – {Tên chi nhánh}`, địa chỉ chi nhánh, không có thì địa chỉ công ty |
| Mã mẫu | `Mẫu số 01-TT` (hoặc 02-TT, 01-VT, 02-VT) và dòng "Ban hành theo Thông tư số 133/2016/TT-BTC ngày 26/8/2016 của Bộ Tài chính" |
| Ngày … tháng … năm | `VoucherAt` đổi sang ngày Việt Nam |
| Số | `Code` |
| Quyển số, Nợ, Có | Dòng chấm |
| Giám đốc | `LegalRepresentativeName` |
| Kế toán trưởng | `ChiefAccountantName` |
| Người lập phiếu | Họ tên người tạo phiếu (`Owner`) |
| Thủ quỹ, Thủ kho, Người nộp/nhận tiền, Người giao/nhận hàng (khối chữ ký) | Chỉ in chức danh và "(Ký, họ tên)", tên để trống |
| Phiếu đã hủy | Vẫn in được, có chữ **"ĐÃ HỦY"** dưới tiêu đề |
| Chưa có thông tin đơn vị | Vẫn in, các ô đơn vị để trống |

Đối tác, lý do, kho và người dùng đã xóa mềm vẫn in đúng tên: đọc với `IgnoreQueryFilters()`.

### 01-TT Phiếu thu / 02-TT Phiếu chi

| Ô | Nguồn |
|---|---|
| Họ và tên người nộp tiền / người nhận tiền | `PersonName`; trống thì `PartnerName` |
| Địa chỉ | `PartnerAddress` |
| Lý do nộp / Lý do chi | `Description`; trống thì tên Lý do thu/chi (`CashReason.Name`) |
| Số tiền | `Total` |
| Viết bằng chữ | `VndAmountInWords.Read(Total)` |
| Kèm theo … chứng từ gốc | `AttachedDocs`; trống thì dòng chấm |
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
| Lý do xuất kho (02-VT) | `StockReason.Name` |
| Nhập tại kho / Xuất tại kho | Tên kho của phiếu; "Địa điểm" để dòng chấm |
| STT | Thứ tự dòng theo `SortOrder` |
| Tên, nhãn hiệu, quy cách | `ProductName` (snapshot trên dòng) |
| Mã số | `ProductCode` |
| Đơn vị tính | `UnitName` |
| Số lượng theo chứng từ (01-VT) / yêu cầu (02-VT) | `Quantity` |
| Số lượng thực nhập / thực xuất | `Quantity` |
| Đơn giá, Thành tiền | Theo chế độ giá, xem bảng dưới |
| Số chứng từ gốc kèm theo | Dòng chấm |

Khối chữ ký:
- 01-VT: Người lập phiếu, Người giao hàng, Thủ kho, Kế toán trưởng.
- 02-VT: Người lập phiếu, Người nhận hàng, Thủ kho, Kế toán trưởng, Giám đốc.

**Ba chế độ giá** (`StockPrintPriceMode`):

| Chế độ | Dòng được in | Đơn giá / Thành tiền | Dưới bảng |
|---|---|---|---|
| `Voucher` — giá trên phiếu (mặc định) | Mọi dòng | `UnitPrice`, `Amount` | Cộng tiền hàng, Chiết khấu (`LineDiscountTotal` + `OrderDiscount`), Thuế GTGT (`VatTotal`), Cước (`Freight`), **Tổng cộng** = `Total`; bằng chữ theo `Total`. Dòng có giá trị 0 thì bỏ |
| `Cost` — giá vốn | Chỉ dòng `TrackInventory` | Nhập: `InboundValue / Quantity`, `InboundValue`. Xuất: đơn giá vốn và giá vốn xuất của dòng, lấy từ sổ kho | Cộng; bằng chữ theo tổng cộng. Giá vốn tạm tính thì thêm ghi chú dưới tiêu đề |
| `None` — không in tiền | Chỉ dòng `TrackInventory` | Để trống | Dòng Cộng và bằng chữ để dòng chấm |

`Cost` cần quyền `inventory.view_cost`. Tổng in ở chế độ `Voucher` bằng tổng phiếu trên màn hình. Giá vốn in ở chế độ `Cost` bằng giá vốn trên thẻ kho của cùng dòng.

### Backend

| Lớp | Thành phần | Trách nhiệm |
|---|---|---|
| Application `Tt133/Vouchers/` | `IVoucherPrintService` / `VoucherPrintService` | Kiểm quyền và chi nhánh, nạp phiếu trong một snapshot, dựng model in, gọi renderer. Chuyển PDF sau khi đóng snapshot |
| | `CashVoucherPrintModel`, `StockVoucherPrintModel` | Model phẳng đã áp quy tắc fallback và chế độ giá. Renderer không đọc entity |
| | `StockPrintPriceMode { Voucher = 0, Cost = 1, None = 2 }` | Tham số chế độ giá |
| | `IVoucherFormRenderer` | `RenderCashVouchers(context, models)`, `RenderStockVouchers(context, models)`; Infrastructure hiện thực |
| Infrastructure `Excel/Tt133/Vouchers/` | `VoucherFormBuilder` | Phần chung: khối đơn vị và mã mẫu; tiêu đề, ngày, số, Nợ/Có; dòng nhãn–giá trị; khối chữ ký; page setup A5 ngang / A4 dọc |
| | `CashVoucherForm`, `StockVoucherForm` | Mỗi class một cặp mẫu (thu/chi, nhập/xuất) |
| WebApi | Action mới trong controller phiếu thu/chi và phiếu kho | Xem API |

`VoucherFormBuilder` là builder riêng và không sửa `Tt133FormBuilder`: khung sổ (bảng dài, đầu kỳ/cuối kỳ, "Sổ này có … trang") khác khung chứng từ. Chỉ tách hàm dùng chung khi hai builder trùng mã thật.

### API

Quyền là quyền xem của loại phiếu tương ứng. Phạm vi chi nhánh theo `ICurrentBranch`, như màn phiếu.

```
GET  api/cash-vouchers/{id}/print?format=xlsx|pdf
POST api/cash-vouchers/print?format=xlsx|pdf          body { ids: Guid[] }
GET  api/stock-vouchers/{id}/print?format=xlsx|pdf&priceMode=Voucher|Cost|None
POST api/stock-vouchers/print?format=xlsx|pdf         body { ids: Guid[], priceMode }
```

- In hàng loạt đi bằng POST vì danh sách id dài vượt giới hạn dòng request của Kestrel (plan sổ, quyết định T5).
- Thứ tự sheet: `VoucherAt` rồi `Code`. Tên sheet là số phiếu, làm sạch và làm duy nhất theo quy tắc T12 của plan sổ.
- Một lô trộn được phiếu thu với phiếu chi, hoặc phiếu nhập với phiếu xuất. Mỗi sheet theo mẫu của loại phiếu đó.
- Tên file: một phiếu là `{Code}.{xlsx|pdf}`; nhiều phiếu là `PhieuThuChi_{yyyyMMdd-HHmm}` hoặc `PhieuNhapXuat_{yyyyMMdd-HHmm}`.
- `priceMode` mặc định là `Voucher`.

### Frontend

- Component chung `VoucherPrintButton`: menu **In (PDF)** và **Excel**. PDF mở trong tab mới để in; Excel tải về.
- **Màn phiếu thu/chi và phiếu kho:** nút chỉ bật khi phiếu đã lưu và form không có thay đổi chưa lưu.
- **Màn danh sách:** nút in trên từng dòng; thêm cột checkbox và thanh "Đã chọn N phiếu — In ▾". Lựa chọn chỉ trong trang đang hiển thị và bị xóa khi đổi trang hoặc đổi bộ lọc.
- **Phiếu kho:** hộp thoại chọn chế độ giá trước khi in, nhớ lựa chọn lần trước trong `localStorage`. Mục Giá vốn ẩn khi không có quyền `inventory.view_cost`.
- Chưa có tên đơn vị: vẫn in, hiện gợi ý kèm link sang Cài đặt (dùng lại của sổ TT133).

### Lỗi

| Tình huống | Kết quả |
|---|---|
| Phiếu không tồn tại hoặc thuộc chi nhánh khác | 404 |
| `priceMode=Cost` không có quyền giá vốn | 403 |
| `ids` rỗng | 422 `EXPORT_NO_TARGETS` |
| Số phiếu vượt `MaxSheets` | 422 `EXPORT_TOO_MANY_SHEETS` |
| Một id trong lô không hợp lệ | Cả lô trả 404, không in thiếu âm thầm |
| `format` hoặc `priceMode` sai | 400 |
| LibreOffice lỗi hoặc quá thời gian | 503 `PDF_EXPORT_FAILED` "Xuất PDF thất bại, hãy dùng Excel." |

### Kiểm thử

- **Unit, đọc lại workbook:**
  - từng mẫu có đủ ô, mã mẫu, khối chữ ký đúng thứ tự;
  - khổ giấy và hướng giấy (A5 ngang, A4 dọc);
  - fallback người nộp/nhận, lý do, người giao/nhận;
  - ba chế độ giá, kể cả lọc dòng `TrackInventory` và bỏ dòng tổng có giá trị 0;
  - chữ "ĐÃ HỦY";
  - số sheet, thứ tự và tên sheet khi in lô.
- **Integration trên `qldonhang_integtest`:**
  - tổng in bằng tổng phiếu;
  - giá vốn in khớp thẻ kho;
  - quyền và chi nhánh: 403, 404;
  - 422 khi rỗng và khi vượt giới hạn;
  - content-type và tên file.
- **Frontend:** nút in tắt khi form có thay đổi chưa lưu; chọn nhiều dòng và thanh in lô; hộp thoại chế độ giá; ẩn Giá vốn khi thiếu quyền.
- **Thủ công:** in thật 4 mẫu trên Docker (LibreOffice) để kiểm khổ giấy và ngắt trang.
- **Go-live gate:** kế toán duyệt bản in 4 mẫu so với bản gốc Phụ lục 3 TT133.

### Chia phase dự kiến

| Phase | Nội dung |
|---|---|
| P1 | `VoucherFormBuilder`, `IVoucherPrintService`, 01-TT và 02-TT, hai endpoint phiếu thu/chi, `VoucherPrintButton`, nút in ở màn phiếu thu/chi và danh sách (kèm chọn nhiều dòng) |
| P2 | 01-VT và 02-VT, ba chế độ giá, hai endpoint phiếu kho, hộp thoại chế độ giá, nút in ở màn phiếu kho và danh sách |
| P3 | Docs (SUMMARY, conventions, directory-structure, product-goals), in thật trên Docker, kiểm tra cuối |

## Open questions

**Kế toán / PO** (không chặn việc phát triển; là go-live gate):
- Phiếu thu/chi của **quỹ ngân hàng** có in theo 01-TT/02-TT không, hay kế toán chỉ dùng giấy báo Có / ủy nhiệm chi của ngân hàng? Thiết kế hiện cho in cả hai loại quỹ.
- Có chấp nhận để trống ô Nợ, Có và Quyển số cho kế toán ghi tay không?
- Cột "Số lượng theo chứng từ / yêu cầu" in bằng số lượng thực nhập/xuất có đúng thực tế không, hay phải để trống cho thủ kho ghi?
- Chế độ `Voucher` thêm các dòng Chiết khấu, Thuế GTGT, Cước dưới bảng, ngoài mẫu gốc. Kế toán có chấp nhận không?

**Kỹ thuật** (chốt khi viết plan):
- Nguồn giá vốn xuất của từng dòng phiếu: đọc từ sổ kho theo dòng phiếu. Xác nhận tên trường và cờ "tạm tính" trên code đã có khi viết plan.
- Tên quyền xem phiếu thu/chi và phiếu kho, và tên controller hiện có: lấy theo code tại thời điểm viết plan.
- A5 ngang qua LibreOffice: kiểm trên Docker rằng PDF giữ đúng khổ giấy khi một file có nhiều sheet.

## Next steps

1. Chạy xong [plan sổ TT133](../../plans/archived/261013-1926-tt133-report-forms/SUMMARY.md) và merge vào `qlbanhang/main`.
2. Gửi kế toán các câu hỏi Kế toán/PO ở trên.
3. `write-plan` cho 3 phase, đối chiếu contract với code đã hiện thực của plan sổ.
4. Cập nhật `docs/project-pdr/product-goals.md` khi plan được duyệt.
