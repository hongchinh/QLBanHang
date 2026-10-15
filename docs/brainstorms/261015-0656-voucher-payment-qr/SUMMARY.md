# QR chuyển khoản trên phiếu thu và phiếu xuất kho

> Brainstorm: 2026-10-15 06:56:58 · Tiếp nối [mẫu in chứng từ TT133](../261013-2042-tt133-voucher-print-forms/SUMMARY.md) (đã hiện thực, PR #15) · Review đối chiếu code: 2026-10-15, đã sửa theo mục [Review đối chiếu code](#review-đối-chiếu-code-2026-10-15) · Trạng thái: **đã hiện thực** (2026-10-15; [kế hoạch](../../plans/261015-0919-voucher-payment-qr/SUMMARY.md), [kết quả đo và chỗ lệch so với thiết kế](#kết-quả-hiện-thực-2026-10-15), [danh sách kiểm tra trước go-live](../../plans/261015-0919-voucher-payment-qr/GO-LIVE-CHECKLIST.md)) · Mockup: [vị trí QR trên bản in](mockup-01-print-placement.html), [hộp thoại QR trên màn phiếu](mockup-02-screen-dialog.html). Mockup-01 vẽ trước khi chốt quy tắc số tiền và trước review, nên có hai chỗ khác bản hiện thực: chữ "Còn phải thu" đổi thành "Cần chuyển khoản"; QR trong khung của 02-VT chuyển sang bên phải (R3)

## Problem framing

Khách nhận phiếu thu hoặc phiếu xuất kho thường trả bằng chuyển khoản. Hiện nhân viên phải đọc số tài khoản, số tiền và nội dung cho khách gõ tay, nên dễ sai số tiền, sai nội dung, và khó đối chiếu sau này.

Yêu cầu: in mã **VietQR** (chuẩn NAPAS/EMVCo) có sẵn số tiền và nội dung trên phiếu thu (01-TT) và phiếu xuất kho (02-VT), đồng thời hiện QR trên màn phiếu để khách quét ngay tại quầy.

Phần mềm đã có những thứ sau:
- `VietQrPayloadBuilder` (`Application/Payments/Services`), có test;
- bảng `Banks` có mã BIN;
- màn "QR thanh toán" cho báo giá, dùng tài khoản cá nhân của nhân viên;
- thư viện `qrcode.react` ở frontend;
- mẫu in 01-TT/02-VT dựng bằng ClosedXML, xuất Excel và PDF.

Còn thiếu một thứ: quỹ ngân hàng (`Fund`) chỉ có `BankName` dạng chữ tự do và `AccountNumber`. Quỹ chưa có BIN và tên chủ tài khoản nên chưa tạo được payload VietQR.

## Goals

- QR trên **bản in** 01-TT (A5 ngang) và 02-VT (A4 dọc), cả in đơn lẻ và in hàng loạt, cả Excel và PDF.
- QR trên **màn phiếu** thu và phiếu xuất đã lưu, trong một hộp thoại. Trên điện thoại hộp thoại là tấm trượt từ dưới lên.
- Tiền về **quỹ ngân hàng** của chi nhánh. Admin khai ngân hàng và chủ tài khoản ở màn Quỹ.
- Người in **bật hoặc tắt** QR khi in. Hệ thống nhớ lựa chọn theo người dùng, mặc định bật.
- Bản in và màn hình luôn cho **cùng payload** (tài khoản, số tiền, nội dung), vì quy tắc nằm ở backend. Cả hai vẽ QR ở mức sửa lỗi M.

## Non-goals

- Xác nhận thanh toán, khớp giao dịch ngân hàng với phiếu, webhook. Non-goal hiện có của `product-goals.md` giữ nguyên; nguồn biến động SePay (đợt 5 của mobile hub) là phần riêng.
- QR trên phiếu chi (02-TT) và phiếu nhập kho (01-VT).
- QR trên màn danh sách phiếu. Màn danh sách chỉ có ô "Kèm QR" khi in hàng loạt.
- Tự ghép `BankName` cũ của quỹ sang bảng `Banks`.
- Chọn tài khoản nhận tiền khi in, hoặc dùng tài khoản cá nhân của nhân viên.
- Logo VietQR/NAPAS trong khung QR, tùy biến kích thước QR.

## Constraints & assumptions

- Dùng lại `VietQrPayloadBuilder.Build`, không viết lại. Builder tự bỏ dấu và cắt tên chủ tài khoản, nội dung về tối đa 25 ký tự ASCII.
- Dùng lại hạ tầng in của plan mẫu in TT133: `VoucherPrintService` (đọc trong một snapshot), `VoucherFormBuilder`, `ISpreadsheetPdfConverter`, `VoucherPrintButton`, `StockPriceModeDialog`, `useVoucherPrint`, `price-mode-storage`.
- Quỹ ngân hàng mặc định tính theo **chi nhánh và loại quỹ** (`FundService` bỏ mặc định của quỹ cùng chi nhánh, cùng loại).
- Phiếu xuất có hình thức thanh toán gắn quỹ thì `PaidAmount` mặc định bằng tổng phiếu. Đây là quy tắc P9 của backend (`StockVoucherService`, khi request không gửi `PaidAmount`); form phiếu xuất cũng tự điền theo quy tắc này. Phiếu có `PaidAmount > 0` thì `FundId` là quỹ của HTTT (quỹ được chọn, hoặc quỹ mặc định cùng loại của chi nhánh), và `AutoCashVoucherService` tự tạo phiếu thu vào quỹ đó với `Total = PaidAmount`. `PaidAmount = 0` thì `FundId = null`. Vì vậy số tiền QR không thể là `Total − PaidAmount`, xem quy tắc dưới.
- `BankName` của quỹ đang được in ở dòng "Nơi mở tài khoản giao dịch" của sổ S04 (`CashBookForm`), nên giữ nguyên.
- Brainstorm [thiết kế lại form phiếu thu/chi](../261015-0644-cash-voucher-form-redesign/SUMMARY.md), viết cùng ngày, cũng sửa `cash-voucher-form-page.tsx`. Feature này chỉ thêm `PaymentQrButton` vào hàng nút của trang đó. Plan làm sau phải rebase lên plan làm trước.
- Thêm một NuGet cho mã nguồn: **QRCoder** (MIT, thuần .NET, `PngByteQRCode` không cần System.Drawing). Thêm hai NuGet chỉ dùng trong test: **ZXing.Net** và **ZXing.Net.Bindings.SkiaSharp**, để đọc PNG và giải mã ngược, kiểm tra mã quét được. SkiaSharp đã có trong Infrastructure. *(Khi hiện thực chỉ dùng **ZXing.Net**; xem [kết quả hiện thực](#kết-quả-hiện-thực-2026-10-15).)*
- `api/cash-vouchers/{id}/payment-qr` và `api/stock-vouchers/{id}/payment-qr` trả dữ liệu theo chi nhánh và quyền, nên **không** được thêm vào `CACHEABLE_API_PREFIXES` của service worker (quy tắc trong [pwa-mobile.md](../../architecture/pwa-mobile.md)).

## Approaches considered

### Tài khoản nhận tiền

| Phương án | Quyết định |
|---|---|
| **Quỹ ngân hàng: quỹ của phiếu nếu là quỹ NH, ngược lại quỹ NH mặc định của chi nhánh** | **Chọn.** Khớp mô hình quỹ, tiền về đúng tài khoản đã ghi trên phiếu, nối được với biến động SePay sau này. Cần một migration |
| Một tài khoản chung trong Thông tin đơn vị | Không: chi nhánh không có tài khoản riêng |
| Tài khoản mặc định của người lập phiếu (như màn QR báo giá) | Không: tiền có thể vào tài khoản cá nhân |
| Chọn tài khoản khi in | Không: thêm một bước mỗi lần in, in hàng loạt phải dùng chung một tài khoản |

### Số tiền QR của phiếu xuất

| Phương án | Quyết định |
|---|---|
| **Số cần chuyển khoản = `Total` − phần đã trả vào quỹ tiền mặt** | **Chọn.** Đúng cho phiếu chuyển khoản (QR = tổng), bán chịu (QR = tổng), trả một phần tiền mặt (QR = phần còn lại) |
| `Total − PaidAmount` | Không: phiếu HTTT chuyển khoản có `PaidAmount = Total` nên sẽ không có QR, đúng ở trường hợp cần nhất |
| Luôn là `Total` | Không: sai khi khách đã trả một phần tiền mặt |
| Công nợ của khách sau phiếu | Không: số thay đổi theo thời điểm in |

### Vị trí QR trên bản in (xem [mockup-01](mockup-01-print-placement.html))

| Phương án | 01-TT A5 | 02-VT A4 |
|---|---|---|
| **A. Cạnh dòng nội dung** | **Chọn.** Trang cao thêm khoảng 1–2 dòng, chữ gần như giữ cỡ (R7) | Không chọn |
| **B. Khối "Thông tin chuyển khoản" trước chữ ký** | Không: trang A5 phải thu nhỏ, chữ còn khoảng 8 pt | **Chọn.** A4 còn chỗ, khung nằm ngay dưới số tiền, đủ thông tin để chuyển tay. Sau review, QR đặt **bên phải** khung, dưới cột Thành tiền (R3) |
| C. Góc trên bên phải | Không: QR nhỏ (khoảng 2 cm), xa dòng số tiền | Không |

### QR trên màn hình (xem [mockup-02](mockup-02-screen-dialog.html))

| Phương án | Quyết định |
|---|---|
| **A. Hộp thoại**, trên điện thoại là tấm trượt từ dưới lên | **Chọn.** QR 264 px, đủ thông tin, có sao chép và tải ảnh |
| B. Popover dưới nút | Không: QR khoảng 170 px, ít thông tin, cần thêm nút phóng to |

### Bật/tắt QR khi in

| Phương án | Quyết định |
|---|---|
| **Ô "Kèm QR" khi in, nhớ theo người dùng, mặc định bật** | **Chọn.** Phiếu thu thường in sau khi đã nhận tiền; QR mang số tiền có thể khiến khách chuyển trùng |
| Luôn in khi đủ điều kiện | Không: không có cách bỏ QR |
| Cài đặt hệ thống theo loại phiếu | Không: không đổi được theo từng lần in |

### Cách hiện thực

| Phương án | Quyết định |
|---|---|
| **Backend giữ quy tắc và payload; bản in vẽ PNG bằng QRCoder; màn hình lấy payload qua API rồi vẽ bằng `qrcode.react`** | **Chọn.** Một bộ quy tắc, bản in và màn hình cùng payload |
| Backend trả ảnh PNG cho cả màn hình | Không: ảnh co giãn kém nét, response nặng hơn |
| Frontend tự tính quy tắc và payload cho màn hình | Không: hai bộ quy tắc dễ lệch nhau |

## Recommended approach (đã chốt)

### Quy tắc nghiệp vụ

**Phiếu nào có QR**

| Phiếu | Điều kiện | Số tiền QR |
|---|---|---|
| Phiếu thu (01-TT) | `Type = Receipt`, `Status = Active`, chưa xóa, `Total > 0`. Gồm phiếu thu tự động sinh từ phiếu xuất (`SourceStockVoucherId`) **khi quỹ của nó là quỹ ngân hàng**, nội dung khác, xem dưới (R1); phiếu thu tự động ghi quỹ tiền mặt không có QR (R15) | `Total` |
| Phiếu xuất kho (02-VT) | `Type = Out`, `Status = Active`, chưa xóa, lý do xuất có `DebtEffect = ReceivableIncrease` (seed: "Xuất bán hàng"), số tiền > 0 | `Total − (Fund.Kind == Cash ? PaidAmount : 0)` |
| Phiếu chi, phiếu nhập | Không có QR | — |

Số tiền làm tròn về đồng (`MidpointRounding.AwayFromZero`) trước khi đưa vào payload.

**Tài khoản nhận tiền**

1. Quỹ của phiếu là quỹ ngân hàng thì dùng chính quỹ đó. Nếu quỹ chưa đủ thông tin QR thì **không có QR**. Hệ thống không chuyển sang quỹ khác, vì tiền sẽ về một tài khoản khác với tài khoản ghi trên phiếu.
2. Quỹ của phiếu là quỹ tiền mặt, hoặc phiếu xuất không có quỹ: dùng **quỹ ngân hàng mặc định của chi nhánh của phiếu** (`Kind = Bank`, `IsDefault`, `IsActive`).
3. Quỹ **đủ thông tin QR** khi đủ bốn điều kiện: quỹ đang hoạt động, có `BankId` trỏ tới ngân hàng đang hoạt động, có `AccountNumber`, có `AccountHolderName`.

**Nội dung chuyển khoản:** số phiếu (`Code`), ví dụ `PT000125`, `PX000318`. Riêng phiếu thu tự động sinh từ phiếu xuất dùng **số phiếu xuất gốc** (R1), để một khoản tiền chỉ mang một mã khi đối chiếu. Phiếu xuất trả đủ bằng chuyển khoản (`PaidAmount = Total`, quỹ ngân hàng) thì QR của phiếu xuất và QR của phiếu thu tự động trùng hẳn nhau: cùng tài khoản, cùng số tiền, cùng nội dung.

**Bật/tắt khi in:**
- Phiếu thu dùng mục "Kèm QR chuyển khoản" trong menu In.
- Phiếu xuất dùng ô "Kèm QR chuyển khoản" trong hộp thoại chọn giá. Ô chỉ dùng được ở chế độ "Giá trên phiếu"; chế độ Giá vốn và Không in tiền bỏ qua QR.
- Phiếu không đủ điều kiện vẫn in, chỉ không có QR.

### Dữ liệu

| Thay đổi | Chi tiết |
|---|---|
| `Fund.BankId` | `Guid?`, khóa ngoại sang `Banks`, `OnDelete(Restrict)`, có index |
| `Fund.AccountHolderName` | `string?`, tối đa 255 ký tự |
| Migration | Một migration, chỉ thêm cột; không đổi dữ liệu cũ |
| `FundDtos`, validator, `FundService` | Nhận và trả hai trường mới cùng tên ngân hàng (`BankShortName`). `BankId` phải trỏ tới ngân hàng đang hoạt động. Khi có `BankId` thì `AccountNumber` phải khớp `^[0-9A-Za-z]{1,19}$` (giới hạn của VietQR) |

`BankName` giữ nguyên và vẫn là "Nơi mở tài khoản" in trên sổ S04.

### Backend

| Lớp | Thành phần | Trách nhiệm |
|---|---|---|
| Application `CashDebt/PaymentQr/` | `VoucherPaymentQrRules` (static, thuần) | `TransferAmount(total, paidAmount, fundKind)`, `PickFund(voucherFund, branchDefaultBank)`, `IsQrReady(fund)` và lý do không có QR. Unit test không cần DB |
| | `IVoucherPaymentQrService` / `VoucherPaymentQrService` | `GetForCashVoucherAsync(id)`, `GetForStockVoucherAsync(id)`: kiểm quyền và chi nhánh như `GetAsync` của màn phiếu, áp quy tắc, dựng payload bằng `VietQrPayloadBuilder`. `ResolveManyAsync(...)`: cho in hàng loạt, chạy trong snapshot của `VoucherPrintService`, không ném lỗi, trả `null` cho phiếu không đủ điều kiện |
| | `VoucherPaymentQrDto` | `Payload`, `Amount`, `Total`, `CashPaid`, `BankName`, `BankShortName`, `AccountNumber`, `AccountHolderName`, `Content`, `FundCode`, `FundName`, `SourceStockVoucherCode` (phiếu thu tự động; hộp thoại ghi "Phiếu thu tự động theo {PX…}") |
| Application `Tt133/Vouchers/` | `CashVoucherPrintRequest.IncludeQr`, `StockVoucherPrintRequest.IncludeQr` | Mặc định `false`, nên API cũ và các test hiện có không đổi |
| | `VoucherQrPrint` trên `CashVoucherPrintModel.Qr` và `StockVoucherPrintModel.Qr` | `Payload`, các dòng chữ đã định dạng (số tiền, ngân hàng, số TK, chủ TK, nội dung, "Tổng − đã trả tiền mặt"); `null` thì không vẽ QR |
| Infrastructure | `QrPngRenderer` | Payload → PNG bằng QRCoder, mức sửa lỗi M, quiet zone 4 module |
| Infrastructure `Excel/Tt133/Vouchers/` | `VoucherFormBuilder.WriteLinesWithQr` | 01-TT: các dòng từ "Họ và tên người nộp tiền" đến "Kèm theo" chỉ chiếm cột 1–8 và wrap theo bề rộng đó. `Tt133FormBuilder.WriteText` hiện luôn merge hết bề rộng, nên cần thêm một biến thể nhận cột cuối. Ảnh QR khoảng 25 mm khi in nằm ở cột 9–10, dưới có chú thích "Quét để chuyển khoản" và "{Tên ngắn ngân hàng} · {STK}" (ngân hàng không có `ShortName` thì dùng `Code`). QR cùng chú thích cao khoảng 102 pt trong sheet, còn 6 dòng nội dung chỉ 90 pt, nên thêm ít nhất 1 dòng (R7) |
| | `VoucherFormBuilder.WritePaymentBlock` | 02-VT: sau "Số chứng từ gốc kèm theo", một khung có viền. Chữ ở bên trái, merge từ cột đầu đến cột trước "Đơn giá": "THÔNG TIN CHUYỂN KHOẢN", ngân hàng, số TK, chủ TK, số tiền (kèm "Tổng … · Đã trả tiền mặt … · Cần chuyển khoản …" khi có), nội dung. QR ở bên phải, trong hai cột cuối "Đơn giá" và "Thành tiền" (khoảng 164 pt), ngay dưới dòng Tổng cộng (R3). Khung nằm trong vùng `KeepSignaturesOnOnePage`; các dòng của khung được đặt chiều cao đủ phủ ảnh, vì phép tính ngắt trang chỉ đọc chiều cao dòng |
| WebApi | `CashVouchersController`, `StockVouchersController` | Thêm action `payment-qr`; các action in nhận thêm `qr` |

Kích thước QR tính theo tỉ lệ co thật của từng mẫu khi in: A5 ngang co khoảng 0,9 theo chiều rộng, A4 dọc khoảng 0,81. Ảnh đặt trong sheet phải lớn hơn tương ứng để khi in ra vẫn khoảng 25 mm. Payload mẫu dài khoảng 170 ký tự, ra QR phiên bản 7 (45 × 45 module) ở mức M. Ở 25 mm, mỗi module khoảng 0,47 mm (tính cả quiet zone). Plan phải đo lại kích thước và tỉ lệ co bằng PDF thật.

### API

Quyền là quyền xem của loại phiếu (`receipts.view`, `stock_out.view`); phạm vi chi nhánh theo `ICurrentBranch`.

```
GET  api/cash-vouchers/{id}/payment-qr     → 200 VoucherPaymentQrDto | 422 PAYMENT_QR_UNAVAILABLE
GET  api/stock-vouchers/{id}/payment-qr    → 200 VoucherPaymentQrDto | 422 PAYMENT_QR_UNAVAILABLE
GET  api/cash-vouchers/{id}/print?format=…&includeQr=true
POST api/cash-vouchers/print?format=…          body { ids, includeQr }
GET  api/stock-vouchers/{id}/print?format=…&priceMode=…&includeQr=true
POST api/stock-vouchers/print?format=…         body { ids, priceMode, includeQr }
```

Tham số dùng cùng tên `includeQr` ở query và body, như `priceMode` hiện có (R14).

Câu báo của 422 `PAYMENT_QR_UNAVAILABLE`:

| Lý do | Câu báo |
|---|---|
| Phiếu đã hủy | "Phiếu đã hủy." |
| Phiếu chi hoặc phiếu nhập | "Phiếu này không có QR chuyển khoản." |
| Phiếu thu tự động ghi quỹ tiền mặt | "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản." |
| Lý do xuất không tăng phải thu | "Lý do xuất không phải bán hàng." |
| Số tiền ≤ 0 | "Phiếu không còn số cần chuyển khoản." |
| Quỹ NH của phiếu thiếu thông tin | "Quỹ {Code} chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản." |
| Không có quỹ NH mặc định đủ thông tin | "Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR." |

### Frontend

| Chỗ | Thay đổi |
|---|---|
| `pages/funds/fund-form-dialog.tsx` | Loại quỹ Ngân hàng có thêm "Ngân hàng (cho QR)" (chọn từ `useBanks()`, hiện tên viết tắt và BIN) và "Chủ tài khoản". Gợi ý: "Có đủ ngân hàng, số tài khoản và chủ tài khoản thì phiếu thu/xuất in được QR." |
| `features/voucher-print/qr-pref-storage.ts` | Nhớ lựa chọn "Kèm QR" theo `userId` trong localStorage, mặc định bật; đọc ghi trong try/catch như `price-mode-storage` |
| `VoucherPrintButton` | Phiếu thu: `DropdownMenuCheckboxItem` "Kèm QR chuyển khoản" trong menu In. Radix đóng menu khi bấm một item, nên `onSelect` gọi `preventDefault`: menu vẫn mở sau khi tick, người dùng chọn tiếp PDF hoặc Excel (R10) |
| `StockPriceModeDialog` | Phiếu xuất: checkbox "Kèm QR chuyển khoản", khóa khi chế độ khác "Giá trên phiếu" |
| `useVoucherPrint` | Gửi `includeQr` (query cho in đơn lẻ, body cho in hàng loạt) |
| `components/payment-qr/payment-qr-button.tsx` | Nút "QR chuyển khoản" ngay sau nút In trên màn phiếu thu và phiếu xuất. Bật khi phiếu đã lưu, không có thay đổi chưa lưu, không bận, chưa hủy. Không tự đoán phiếu có đủ điều kiện hay không. Màn hẹp chỉ hiện icon |
| `components/payment-qr/payment-qr-dialog.tsx` | Mỗi lần mở gọi `useVoucherPaymentQr(kind, id)`. Nội dung theo mockup A: `QRCodeCanvas` 264 px, `level="M"`, có quiet zone (R5); số tiền lớn; dòng "Tổng phiếu … − đã trả tiền mặt …" khi có; dòng "Phiếu thu tự động theo {PX…}" với phiếu thu tự động; ngân hàng, số TK (sao chép), chủ TK, nội dung (sao chép), quỹ nhận. **Tải ảnh QR** ghép QR, số tiền, nội dung và dòng ngân hàng thành `QR_{Code}.png`. Trên thiết bị có `navigator.canShare({ files })`, nhất là iOS chạy dạng cài đặt nơi `<a download>` không lưu được ảnh, thì mở bảng chia sẻ để lưu ảnh hoặc gửi Zalo; nếu không có thì tải file (R6). **Sao chép thông tin** chép nhiều dòng. Codebase chưa dùng `navigator.clipboard` ở đâu; API này cần HTTPS, sao chép lỗi thì báo toast (R12). Gặp 422 thì hiện câu báo; lỗi do cấu hình quỹ thì kèm link sang `/funds`, chỉ hiện khi người dùng có `cash.catalogs.manage` (R9). Dưới `sm` dùng `Sheet` mở từ dưới lên |

### Trường hợp biên

| Tình huống | Xử lý |
|---|---|
| Tên chủ TK có dấu hoặc dài hơn 25 ký tự | Payload dùng bản bỏ dấu đã cắt; hộp thoại và bản in hiện tên đầy đủ |
| Quỹ ngừng hoạt động, ngân hàng `IsActive = false` | Coi như chưa đủ thông tin QR |
| In lô có cả phiếu đủ và không đủ điều kiện | Mỗi phiếu xét riêng; phiếu không đủ in không có QR |
| In ở chế độ Giá vốn hoặc Không in tiền | Bỏ qua `IncludeQr` |
| Phiếu đã hủy | Bản in không có QR; hộp thoại báo 422 |
| Phiếu sửa sau khi in | QR trên bản in cũ không còn đúng; không xử lý |
| Phiếu thu ghi quỹ tiền mặt, khách chuyển khoản theo QR | Tiền vào quỹ ngân hàng mặc định trong khi phiếu ghi quỹ tiền mặt. Kế toán sửa quỹ của phiếu hoặc lập phiếu chuyển quỹ. Đây là hệ quả của quy tắc fallback đã chọn; ô bật/tắt QR giúp giảm rủi ro |
| Phiếu xuất ghi một phần đã trả qua quỹ ngân hàng (HTTT chuyển khoản, `PaidAmount < Total`) | QR đòi đủ `Total`. Nếu phần đó là tiền cọc **đã nhận** thì QR đòi lại phần cọc. Dữ liệu không phân biệt "đã nhận" với "sẽ chuyển"; trường hợp mặc định (P9, `PaidAmount = Total`, chưa chuyển) thì đúng. Ghi nhận rủi ro, hỏi kế toán (R2) |
| Phiếu thu tự động ghi quỹ tiền mặt (phiếu xuất trả tiền mặt) | Không có QR: khách đã đưa tiền mặt, QR sẽ đòi lại khoản đã thu. Hộp thoại báo 422 "Phiếu thu tự động thu tiền mặt, không có QR chuyển khoản."; bản in không có QR (R15) |
| In cả phiếu xuất lẫn phiếu thu tự động của nó | Hai QR cùng nội dung `PX…`. Trả đủ bằng chuyển khoản thì hai QR giống hệt nhau. Khách vẫn có thể quét hai lần; ô bật/tắt QR giúp giảm rủi ro (R1) |

### Kiểm thử

- **Unit (backend):**
  - `VoucherPaymentQrRules`: quỹ tiền mặt / ngân hàng / không có quỹ; quỹ NH thiếu từng trường; có hoặc không có quỹ NH mặc định; `PaidAmount` bằng 0, một phần, đủ; lý do xuất khác `ReceivableIncrease`; nội dung của phiếu thu tự động là số phiếu xuất gốc.
  - `QrPngRenderer`: giải mã ngược PNG bằng ZXing.Net, so khớp payload.
  - 01-TT có QR: có một ảnh, ảnh nằm ở cột 9–10 ngang các dòng nội dung, dòng nội dung chỉ chiếm cột 1–8, có chú thích; các dòng đủ cao để phủ ảnh.
  - 02-VT có QR: khung có viền, đủ các dòng; chữ ở bên trái, ảnh ở hai cột cuối (có và không có cột Kho); khung nằm trong vùng giữ cùng trang với chữ ký.
  - `IncludeQr = false`: workbook không đổi so với hiện tại (các test hiện có vẫn đúng).
- **Integration** (trên `qldonhang_integtest`):
  - `payment-qr`: 200 với payload đúng; phiếu thu tự động trả nội dung là số phiếu xuất; mỗi lý do 422; 403 thiếu quyền, 403 chi nhánh khác; 404 không tồn tại hoặc đã xóa.
  - In với `includeQr=true`: lô trộn có đúng số ảnh; chế độ Giá vốn không có ảnh.
  - API quỹ: lưu và đọc `BankId`, `AccountHolderName`; validation `BankId` và `AccountNumber`.
  - Migration chạy trên DB test.
- **Frontend (vitest):**
  - `qr-pref-storage`;
  - mục "Kèm QR" chỉ có ở phiếu thu, và menu vẫn mở sau khi tick; checkbox chỉ có ở phiếu xuất và bị khóa khi không phải "Giá trên phiếu";
  - `useVoucherPrint` gửi `includeQr`;
  - `PaymentQrDialog`: hiện dữ liệu, dòng phiếu thu tự động, câu báo 422 kèm link (chỉ khi có quyền), sao chép (cả khi lỗi), tải ảnh qua bảng chia sẻ và qua tải file;
  - `PaymentQrButton`: trạng thái bật/tắt theo form;
  - form quỹ chỉ hiện trường mới khi loại là Ngân hàng.
- **Go-live, làm tay:**
  - Quét QR từ giấy in thật (01-TT A5, 02-VT A4, PDF qua LibreOffice) và từ màn hình bằng ít nhất 3 app ngân hàng. Thử cả "Tải ảnh QR" trên iPhone chạy app dạng cài đặt.
  - Admin khai ngân hàng và chủ tài khoản cho từng quỹ ngân hàng, đặt quỹ ngân hàng mặc định cho từng chi nhánh.

### Chia phase dự kiến

| Phase | Nội dung |
|---|---|
| P1 | `Fund.BankId`, `Fund.AccountHolderName`, migration, DTO, validator, form quỹ |
| P2 | Backend chung: `VoucherPaymentQrRules`, `VoucherPaymentQrService` (cả `ResolveManyAsync`), `VoucherPaymentQrDto`, hai action `payment-qr` trong `CashVouchersController` và `StockVouchersController`, DI |
| P3 | Bản in: `QrPngRenderer` (QRCoder), `IncludeQr` ở request, service và hai action in, bố cục QR của 01-TT và 02-VT; frontend: ô bật/tắt trong menu In và hộp thoại chọn giá, `qr-pref-storage`, `useVoucherPrint` |
| P4 | Màn hình: `useVoucherPaymentQr`, `PaymentQrButton`, `PaymentQrDialog` (kể cả `Sheet` và bảng chia sẻ), gắn vào màn phiếu thu và phiếu xuất |
| P5 | Docs (`conventions.md`, `system-architecture.md`, `product-goals.md`, `directory-structure.md`), in thật qua LibreOffice, quét thử, kiểm tra cuối |

P3 và P4 chạy song song được sau P2. Hai phase sửa các file khác nhau: P3 sửa phần in ở backend và `components/voucher-print/`, `features/voucher-print/`; P4 sửa `components/payment-qr/` và hai trang form. Những file cả hai cùng cần sửa (controller, DI) đã gom hết vào P2 (R4).

## Open questions

**Kế toán / PO** (không chặn phát triển; nên hỏi trước go-live):
- Phiếu thu ghi quỹ tiền mặt nhưng khách chuyển khoản theo QR: kế toán muốn sửa quỹ của phiếu hay lập phiếu chuyển quỹ?
- Nội dung chuyển khoản chỉ là số phiếu. Có cần thêm mã khách (ví dụ `PX000318 KH0012`) không? Thêm mã khách thì vẫn phải nằm trong 25 ký tự.
- Phiếu xuất HTTT chuyển khoản ghi một phần đã trả (`PaidAmount < Total`): phần đó thường là tiền **đã nhận** (cọc) hay tiền **sẽ chuyển**? Nếu thường là đã nhận thì QR nên là `Total − PaidAmount` cho trường hợp này (R2).

**Kỹ thuật** (giải quyết khi viết plan):
- Cách đặt ảnh trong ClosedXML (`AddPicture`, `MoveTo` theo ô với offset) để vị trí không lệch sau khi LibreOffice co trang. Kiểm bằng PDF thật.
- Đo lại tỉ lệ co của A5 sau khi thêm dòng cho QR, với "Lý do nộp" dài (R7).

## Review đối chiếu code (2026-10-15)

Đối chiếu tài liệu với code tại `d4f7b51`. Những phần đã khớp:
- `VietQrPayloadBuilder`: bỏ dấu, cắt tối đa 25 ký tự, giới hạn TLV 99 byte;
- bảng `Banks` (seed theo BIN, có `ShortName`, `IsActive`) và `BankLookupService`;
- quỹ mặc định tính theo chi nhánh và loại quỹ;
- `BankName` được in trên sổ S04;
- quyền `receipts.view` / `stock_out.view`;
- `ICurrentBranch`; `UnprocessableException(code, message)`;
- `DropdownMenuCheckboxItem` và `Sheet` có sẵn;
- `qrcode.react`.

Những điểm đã sửa:

| # | Phát hiện | Đã sửa |
|---|---|---|
| R1 | Phiếu xuất HTTT chuyển khoản tự sinh phiếu thu vào cùng quỹ ngân hàng (`AutoCashVoucherService`), nên một khoản tiền có hai QR với hai nội dung `PX…` và `PT…` | Người dùng chọn: phiếu thu tự động vẫn có QR, nội dung là số phiếu xuất gốc; DTO có `SourceStockVoucherCode`; ghi rủi ro quét hai lần |
| R2 | Công thức coi phần đã trả qua quỹ ngân hàng là chưa chuyển; với tiền cọc đã nhận qua chuyển khoản, QR đòi lại phần cọc | Ghi vào Trường hợp biên và Open questions cho kế toán |
| R3 | Khối thanh toán 02-VT để QR bên trái thì giữa QR và chữ trống khoảng 38 mm, vì cột B rộng 34 đơn vị và chữ chỉ bắt đầu được ở biên ô | Người dùng chọn: QR bên phải, ở hai cột cuối dưới Thành tiền; chữ bên trái |
| R4 | P3 cần `ResolveManyAsync` của P2; P2 và P3 cùng sửa controller và DI nhưng tài liệu ghi là "sửa các file khác nhau" | Chia lại 5 phase: phần backend chung vào P2, sau đó P3 (in) và P4 (màn hình) chạy song song |
| R5 | "Cùng một mã" không đúng: QRCoder dùng mức M, `qrcode.react` mặc định mức L | Đổi thành "cùng payload"; màn hình dùng `level="M"` |
| R6 | `<a download>` (như `saveBlob`) không lưu được ảnh trên iOS khi app chạy dạng cài đặt | Dùng bảng chia sẻ khi `canShare({ files })`, không có thì tải file; thêm bước kiểm tra tay trên iPhone |
| R7 | "A5 gần như không cao thêm" lạc quan: QR và chú thích cao khoảng 102 pt so với 90 pt của 6 dòng, dòng nội dung hẹp lại nên xuống dòng nhiều hơn | Ghi "thêm ít nhất 1 dòng"; đo bằng PDF thật trong plan |
| R8 | P9 được dẫn từ frontend, nhưng đây là quy tắc backend; tài liệu chưa nói `FundId = null` khi `PaidAmount = 0` | Sửa Constraints |
| R9 | Câu hỏi mở về đường dẫn và quyền của màn Quỹ trả lời được từ code | Link `/funds`, chỉ hiện khi có `cash.catalogs.manage`; bỏ khỏi Open questions |
| R10 | Radix đóng menu khi bấm `DropdownMenuCheckboxItem` | `onSelect` gọi `preventDefault`; thêm test |
| R11 | ZXing.Net không tự đọc được PNG trên .NET | ~~Thêm `ZXing.Net.Bindings.SkiaSharp` vào gói chỉ dùng trong test~~ Đã làm khác: gói đó phụ thuộc SkiaSharp 4.x trong khi ứng dụng dùng 3.119.x, nên test chỉ thêm `ZXing.Net`, tự đổi PNG sang byte xám bằng SkiaSharp (đã có trong Infrastructure) và đưa cho `RGBLuminanceSource` |
| R12 | Codebase chưa dùng `navigator.clipboard` ở đâu; API này cần HTTPS | Sao chép lỗi thì báo toast; thêm test |
| R13 | `payment-qr` trả dữ liệu theo chi nhánh và quyền | Ghi rõ không thêm vào `CACHEABLE_API_PREFIXES` |
| R14 | Query dùng `qr`, body dùng `includeQr` | Dùng `includeQr` ở cả hai, như `priceMode` |
| R15 | Review plan 2026-10-15: phiếu thu tự động ghi quỹ tiền mặt vẫn có QR (rơi về quỹ ngân hàng mặc định) đòi lại số tiền khách đã trả tiền mặt | Người dùng chọn: phiếu thu tự động chỉ có QR khi quỹ của nó là quỹ ngân hàng; thêm lý do 422 và test `VoucherPaymentQrRules` / `payment-qr` |

## Next steps

- [x] `write-plan` theo 5 phase ở trên ([kế hoạch](../../plans/261015-0919-voucher-payment-qr/SUMMARY.md)).
- [ ] Gửi kế toán các câu hỏi Kế toán/PO ở trên trước go-live. Các câu hỏi, cách thiết lập quỹ, bảng quét thử và rủi ro đã được soạn trong [GO-LIVE-CHECKLIST.md](../../plans/261015-0919-voucher-payment-qr/GO-LIVE-CHECKLIST.md); việc gửi và nhận câu trả lời chưa làm.
- [x] Cập nhật `docs/project-pdr/product-goals.md` khi hiện thực xong (P5), cùng `conventions.md`, `system-architecture.md`, `directory-structure.md` và `pwa-mobile.md`.

## Kết quả hiện thực (2026-10-15)

**Đo trên PDF thật** (LibreOffice, mẫu in thử của plan, Task 3.6):

| Mục | Kết quả |
|---|---|
| Bề rộng QR in trên 01-TT (A5 ngang) | 24,9 đến 25,07 mm, ≈ 25 mm như thiết kế. Riêng phiếu thu có chữ rất dài (họ tên 255, địa chỉ 1000, lý do 500 ký tự) co còn 48 %, QR bị giới hạn trong hai cột nên in 19,2 mm, vẫn giải mã được |
| Bề rộng QR in trên 02-VT (A4 dọc) | 24,9 đến 25,07 mm |
| Khổ trang PDF | 01-TT: A5 ngang 595,3 × 419,6 pt. 02-VT: A4 dọc 595,3 × 841,9 pt |
| Tỉ lệ co của 01-TT có QR | 71 đến 73 %, nhỏ hơn bản không QR (≈ 81 %) và nhỏ hơn dự đoán "khoảng 0,9" ở trên; nguyên nhân: khổ A5 được co theo chiều cao, mà QR cùng chú thích làm tờ cao hơn |
| Ảnh QR nhúng | PNG không nén mất dữ liệu, 295 điểm ảnh mỗi cạnh (≈ 6 điểm ảnh mỗi module, QR phiên bản 6, tức 41 module + vùng yên tĩnh 2 × 4 = 49 module, mỗi module ≈ 0,5 mm) |
| Giải mã | OpenCV giải mã được cả 8 trang mẫu, nội dung trùng payload |
| 02-VT 40 dòng hàng | 2 trang A4, khối thông tin chuyển khoản và chữ ký cùng nằm ở trang 2 |

Chưa đo bằng giấy in thật và app ngân hàng: đó là bảng quét ở phần 3 của danh sách kiểm tra. Phần chênh lệch tỉ lệ co của A5 là một quyết định của PO (chấp nhận, thu QR còn ≈ 22 mm, hoặc rút ngắn các dòng chú thích), ghi ở phần 5 của danh sách kiểm tra.

**Chỗ khác thiết kế**:
- Mỗi module khoảng 0,5 mm (QR phiên bản 6), không phải 0,47 mm (phiên bản 7) như dự tính ở trên.
- Ảnh trong sheet được tính theo tỉ lệ co đo từ chính sheet đã hoàn chỉnh (A5 co theo chiều cao nên chỉ biết sau khi viết xong), không dùng con số 0,9 / 0,81 dự đoán; `VoucherFormBuilder` vẽ lại ảnh ở tỉ lệ thật. Các phương thức là `BeginQrBeside` / `EndQrBeside` (01-TT) và `WritePaymentBlock` (02-VT), không phải `WriteLinesWithQr`.
- Test dùng `ZXing.Net` thuần, không dùng gói bindings SkiaSharp (R11).
