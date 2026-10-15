# Phiếu thu/chi và ô đối tượng dùng chung — đặc tả hành vi

Đây là phần chi tiết của [SUMMARY.md](SUMMARY.md). Mã quyết định (D1…D13) xem ở bảng Decisions trong file đó. Bản này đã sửa theo [review](section-02-review.md) ngày 2026-10-15. Nếu khác với mockup hoặc với [BD 4.2](../../bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md) thì **theo file này**.

## 1. Bố cục

### 1.1 Phiếu thu/chi

| Hàng | Nhãn và ô | Ghi chú |
|---|---|---|
| 1 | Số phiếu (chỉ đọc, `tabIndex=-1`) · **Ngày giờ\*** · **Quỹ\*** [nhãn loại quỹ] | Như hiện tại |
| 2 | **Đối tượng** — `PartnerCodeNameField`: ô mã ~250px kèm nút ✕ và **+**, ô tên chiếm phần còn lại | Bỏ hàng "Tên đối tượng / MST". Lỗi của đối tượng hiện ngay dưới hàng |
| 3 | **Lý do\*** · Người nộp / Người nhận | Lỗi của lý do và dòng báo vàng/đỏ (§4) nằm ngay dưới ô Lý do |
| 4 | Địa chỉ | |
| 5 | Lý do nộp / Lý do chi | Tự điền (§3) |
| 6 | Kèm theo (hẹp, placeholder "Số chứng từ gốc") · Ghi chú (rộng) | Ghi chú là ô cuối của phần đầu phiếu |

- Lưới: `#` · Diễn giải · Số tiền · Ghi chú · nút xóa; dòng Tổng; nút "+ Thêm dòng". Cấu trúc lưới không đổi.
- Ô lỗi hiện tại hiện chung `reasonId ?? partnerId` ở một chỗ (`cash-voucher-header.tsx:154`). Tách thành hai: lỗi đối tượng hiện dưới hàng 2, lỗi lý do hiện dưới hàng 3.
- Dưới 1024px, `form-inline-grid` về hai cột (nhãn | ô). Ô mã và ô tên của hàng 2 nằm cùng ô bên phải và xuống dòng thành hai hàng; ô tên không có nhãn riêng và không được rơi vào cột nhãn.

### 1.2 Phiếu kho

- Hàng 1 không đổi: Ngày\* · Lý do\* · Kho\* · HTTT. Dòng báo vàng/đỏ của lý do (§4) nằm dưới hàng 1, ngay dưới ô Lý do. Dòng này chỉ hiện tạm hoặc khi có lỗi; lúc hiện, lưới thấp đi khoảng một dòng chữ.
- Hàng 2: **Đối tượng** [mã][+] [tên] · **Đ.chỉ**. Ba nhãn "Mã đối tượng / Tên đối tượng / Địa chỉ" gộp lại. Lớp lưới mới thay `form-cols-stock-main-2` (nhãn 90px | đối tượng ~2,2fr | nhãn 50px | địa chỉ ~2fr). Lớp này phải có ở **cả hai** mốc media của `form-inline.css`: 1024–1279px và dưới 1024px.
- Lỗi `partnerId` từ server hiện dưới hàng 2 (§4.4).

### 1.3 Báo giá

- "Mã KH\*" + "Tên KH" gộp thành **Khách hàng\*** [mã][+] [tên]. **Giữ id** `customerId` (ô mã) và `customerName` (ô tên) để điều hướng Enter sẵn có (`GENERAL_INFO_FIELD_ORDER`) và các test vẫn chạy.
- Giữ quy tắc hiện có: rời ô tên khi đang trống thì điền lại tên trong danh mục; chọn khách hàng thì điền địa chỉ giao, người nhận, điện thoại nếu các ô đó đang trống; KH ngừng sử dụng thì hiện dòng vàng (chỉ khi sửa).
- Không đổi autofocus của báo giá. Mockup-02 có tự focus Mã KH, nhưng đó chỉ là chi tiết minh họa.

## 2. Điều hướng bàn phím

### 2.1 Phiếu thu/chi — phần đầu phiếu

| # | Ô (id) |
|---|---|
| 1 | Ngày giờ `cash-voucher-at` |
| 2 | Quỹ `cash-fund` |
| **3** | **Mã đối tượng `cash-partner`** — tự focus khi tạo phiếu mới |
| 4 | Tên đối tượng `cash-partner-name` |
| 5 | Lý do `cash-reason` |
| 6 | Người nộp/nhận `cash-person-name` |
| 7 | Địa chỉ `cash-partner-address` |
| 8 | Lý do nộp/chi `cash-description` |
| 9 | Kèm theo `cash-attached-docs` |
| 10 | Ghi chú `cash-voucher-note` |

- **Enter** sang ô kế tiếp chưa bị khóa. Ở ô 10 thì xuống ô **Diễn giải của dòng cuối** và bôi đen chữ. Lưới đang không có dòng nào thì thêm một dòng trước.
- **Shift+Enter** về ô trước chưa bị khóa. Ở ô 1 thì không làm gì.
- **Chọn đối tượng** (từ danh sách, danh mục hoặc thêm nhanh) thì focus sang **ô 4 Tên đối tượng**, con trỏ ở cuối chữ (D11). Enter tiếp theo đi tới Lý do.
- Ô mã giữ Enter lại trong các trường hợp ở §5.3 (danh sách đang mở, đang chờ kết quả, có chữ mà chưa chọn). Các trường hợp khác thì Enter điều hướng.
- Enter khi đang gõ dấu (`isComposing`) và Enter kèm Ctrl/Alt/Meta không điều hướng.
- Handler bỏ qua sự kiện đến từ hộp thoại (`target.closest(DIALOG_SELECTOR)`, như phiếu kho, §6.6).
- Ô Lý do là `<select>` gốc: Enter/Shift+Enter điều hướng, phím mũi tên đổi giá trị như bình thường.
- **Tab** đi theo thứ tự DOM, nên phải sắp lại DOM theo bố cục mới. Nút ✕ giữ `tabIndex=-1` như hiện nay. Nút **+** Tab tới được (BD 4.2 §4, D13). Nút xóa dòng giữ như hiện nay.

### 2.2 Phiếu thu/chi — lưới

| Từ ô | Enter | Shift+Enter |
|---|---|---|
| Diễn giải dòng i | Số tiền dòng i | Ghi chú dòng i−1; ở dòng 0 thì lên Ghi chú đầu phiếu |
| Số tiền dòng i | Ghi chú dòng i | Diễn giải dòng i |
| Ghi chú dòng i | Diễn giải dòng i+1; ở dòng cuối thì **thêm dòng mới** (đã tự điền) và focus vào Diễn giải của dòng đó | Số tiền dòng i |

- Ô được focus bằng Enter thì bôi đen chữ, gõ vào là thay. `MoneyCell` đã tự bôi đen khi focus.
- `MoneyCell` lưu giá trị theo **từng phím** (`money-cell.tsx:35-38`), nên Enter chuyển ô không làm mất số đã gõ.
- Insert vẫn thêm dòng như cũ, giờ có tự điền diễn giải.

### 2.3 Phiếu kho

- Thứ tự Enter giữ nguyên: Ngày → Lý do → Kho → HTTT → Mã đối tượng → Tên → Địa chỉ → lưới (`MAIN_FIELD_IDS`).
- Chọn đối tượng thì focus sang **Tên** (D11). Hiện nay focus quay về ô mã (`stock-voucher-main-fields.tsx:86-90`); đoạn đó bỏ đi.

### 2.4 Báo giá

Điều hướng Enter sẵn có (`GENERAL_INFO_FIELD_ORDER`, `quotation-form-page.tsx:356-386`) giữ nguyên. Ô dùng chung chỉ giữ Enter lại theo §5.3. Chọn khách hàng thì focus sang `customerName`, như hiện nay.

## 3. Tự điền (phiếu thu/chi)

### 3.1 Quy tắc

- `auto = buildAutoDescription(tênLýDo, tênĐốiTượng)`:
  - có cả hai thì `"<tên lý do> - <tên đối tượng>"`;
  - chỉ có lý do thì `"<tên lý do>"`;
  - chưa có lý do thì `""`.
  - Tên đối tượng lấy từ **ô Tên đối tượng** (đã trim), nên tên gõ tay khi không chọn mã cũng được dùng.
- **Lý do nộp** tính lại khi **người dùng** đổi Lý do, đổi Tên đối tượng, chọn đối tượng hoặc bỏ chọn đối tượng. Nếu ô đang trống hoặc bằng `prevAuto` thì gán `auto`; sau đó `prevAuto = auto`.
- **Diễn giải dòng**: khi Lý do nộp đổi từ `old` sang `new` (do tự điền hoặc do gõ tay), dòng nào đang trống hoặc bằng `old` thì gán `new`.
- **Dòng mới** (nút, Insert, Enter ở ô cuối) có Diễn giải = Lý do nộp hiện tại.
- **Bỏ chọn đối tượng** (✕ hoặc gõ đè): Tên và Địa chỉ bị xóa. Lý do nộp nếu còn là chữ tự điền thì về `"<tên lý do>"`. BD 4.2 §13 ghi là xóa trắng; ở đây D2 thay.
- Phiếu chỉ đọc hoặc phiếu tự động: không tự điền.

### 3.2 Khi mở form

- **Phiếu mới**: khi danh sách lý do đã tải và lý do mặc định có tên, điền Lý do nộp và Diễn giải dòng 1 **một lần**, bằng `setValue` **không** có `shouldDirty`. Form vẫn sạch; chỉ thao tác của người dùng mới làm form dirty.
- **Phiếu đã lưu**: khi mở, và sau mỗi `reset`/`resetTo` (lưu xong, tải lại vì xung đột, tải lại nền), **không ghi gì** vào form. Chỉ đặt `prevAuto = buildAutoDescription(voucher.reasonName, voucher.partnerName)` và `prevDesc = voucher.description`. Từ đó, nếu người dùng đổi lý do hoặc đối tượng mà Lý do nộp còn bằng `prevAuto` thì nó tiếp tục tự cập nhật.
- Lý do: form dirty sẽ khóa nút In ("Lưu thay đổi trước khi in"), ngừng reset khi tải lại nền (D29), và khóa nút QR của brainstorm [261015-0656](../261015-0656-voucher-payment-qr/SUMMARY.md).

### 3.3 Ví dụ (phiếu thu, lý do mặc định TKH)

| Bước | Lý do nộp | Diễn giải dòng 1 |
|---|---|---|
| Mở phiếu mới | Thu tiền khách hàng | Thu tiền khách hàng |
| Chọn KH0001 | Thu tiền khách hàng - Công ty TNHH Xây dựng An Phát | (đổi theo) |
| Chọn NCC001 → lý do tự đổi sang THNCC | Thu hoàn tiền NCC - Công ty TNHH Cách nhiệt Đông Á | (đổi theo) |
| Gõ tay Lý do nộp "Thu hoàn tiền hàng lỗi" | Thu hoàn tiền hàng lỗi | Thu hoàn tiền hàng lỗi (đổi theo từng phím) |
| Đổi lý do sang "Thu khác" | Thu hoàn tiền hàng lỗi (giữ, vì đã sửa tay) | giữ |
| Gõ tay Diễn giải dòng 1 "Hàng lỗi lô 05" rồi sửa Lý do nộp | (giá trị mới) | Hàng lỗi lô 05 (giữ, vì đã sửa tay) |

### 3.4 Khi lưu

- **Chữ trong ô mã mà chưa chọn** (BD 4.2 §14, AC-OBJ-023/024; D13): nếu ô mã còn chữ mà chưa chọn đối tượng thì không lưu. Ô mã báo "Vui lòng chọn đối tượng từ danh sách" và nhận focus. Áp dụng cho phiếu thu/chi, phiếu kho và báo giá (báo giá: "khách hàng"). Ô mã trống thì không có lỗi này; lúc đó server vẫn kiểm đối tượng có bắt buộc theo lý do hay không như hiện nay.
- `dropBlankLines` bỏ dòng có `isUntouchedLine(line, lýDoNộp)`: số tiền 0 **và** diễn giải trống hoặc bằng Lý do nộp. Ghi chú dòng không được xét, như hiện nay.
  - Như hiện nay, chỉ bỏ khi còn lại ít nhất một dòng.
  - Nếu mọi dòng đều chưa nhập thì không bỏ dòng nào, và dòng đó báo "Số tiền phải lớn hơn 0".
  - Câu "Phiếu phải có ít nhất 1 dòng có số tiền" trong mockup chỉ là chữ minh họa.
- Payload không có `partnerTaxCode`; server lấy MST trong danh mục (`request.PartnerTaxCode ?? partner?.TaxCode`). `partnerName` và `partnerAddress` vẫn gửi như cũ. Hệ quả được chấp nhận:
  - sửa và lưu lại một phiếu cũ thì MST lưu trên phiếu được thay bằng MST hiện tại trong danh mục;
  - phiếu có đối tượng gõ tay (không có mã) không còn lưu được MST.

## 4. Lý do tự khớp (phiếu thu/chi và phiếu kho)

### 4.1 Vai trò lý do yêu cầu — `requiredRole(reason)`

| `partnerType` | `debtEffect` | Vai trò yêu cầu |
|---|---|---|
| None | — | không có đối tượng |
| Customer | bất kỳ | khách hàng |
| Supplier | bất kỳ | nhà cung cấp |
| Any | None | bất kỳ |
| Any | ReceivableIncrease/Decrease | khách hàng (phía phải thu) |
| Any | PayableIncrease/Decrease | nhà cung cấp (phía phải trả) |

Bảng này khớp với `ValidatePartner` của **cả hai** service (`CashVoucherService.cs:631-660`, `StockVoucherService.cs:862-891`).

Đối tượng **hợp** với lý do khi:
- `requiredRole` là "bất kỳ"; hoặc
- là KH mà đối tượng có `isCustomer`; hoặc
- là NCC mà đối tượng có `isSupplier`.

Lý do loại "không có đối tượng" không bao giờ hợp với một đối tượng. **Lý do trống** tính là không hợp.

### 4.2 Khi chọn đối tượng (từ danh sách, từ danh mục, hoặc vừa thêm nhanh)

1. Lý do hiện tại hợp thì giữ, xóa mọi dòng báo dưới ô Lý do.
2. Không hợp (kể cả khi lý do đang trống): chọn lý do đầu tiên trong ô chọn có `requiredRole` là **KH hoặc NCC** và hợp với đối tượng. Nhóm này gồm cả lý do "Bất kỳ" có ảnh hưởng công nợ. Không có thì lấy lý do đầu tiên có `requiredRole` "bất kỳ". Ô Lý do nháy vàng khoảng 1,6 giây và hiện dòng vàng "Đã đổi lý do cho hợp với `<mã>` (`<vai trò>`)."
3. Không có lý do nào hợp thì giữ lý do, hiện dòng đỏ (§4.3).
4. Sau đó:
   - phiếu thu/chi: tính lại phần tự điền (§3) và đặt lại phân bổ như hiện nay;
   - phiếu kho: đổi lý do kéo theo việc hiện/ẩn ô Hạn thanh toán và điền sẵn số ngày nợ. Đây là hệ quả được chấp nhận, và hiệu ứng nháy vàng báo cho người dùng.

Ô chọn lý do chỉ gồm lý do của đúng loại phiếu và không phải lý do tự động. Thứ tự là thứ tự server trả về: theo loại rồi theo mã (`CashReasonService`, `StockReasonService`). Header phiếu thu/chi phải nhận đủ `partnerType` và `debtEffect` của từng lý do; hiện nay chỉ nhận `{id, name}`.

### 4.3 Khi đổi lý do

- Lý do loại "Không có" thì xóa đối tượng như hiện nay; ô mã khóa với dòng "Không cần đối tượng".
- Vai trò đối tượng đã biết mà không hợp thì **giữ đối tượng** và hiện dòng đỏ dưới Lý do: "Lý do "`<tên>`" cần `<khách hàng|nhà cung cấp>` — `<mã>` không phải `<…>`". Vai trò được biết khi: đối tượng chọn trong phiên này, hoặc khôi phục từ nháp phiếu kho. Nút lưu vẫn bấm được; server sẽ từ chối với khóa `partnerId`.
- Vai trò chưa biết (đối tượng của phiếu đã lưu): không cảnh báo trước, server báo khi lưu.
- Phiếu thu/chi: hiệu ứng công nợ đổi thì đặt lại phân bổ như hiện nay (`resetAllocations`).

### 4.4 Lỗi từ server

- Phiếu thu/chi: `partnerId` đã nằm trong `HEADER_ERROR_FIELDS`; giờ hiện dưới hàng Đối tượng (§1.1).
- Phiếu kho: thêm `partnerId` vào `HEADER_ERROR_FIELDS` (`stock-vouchers/utils/server-field-errors.ts`), xử lý cả khóa cũ còn sót lại như các khóa đầu phiếu khác. Lỗi hiện dưới hàng 2 qua `PartnerCodeNameField`.

## 5. Ô đối tượng dùng chung

### 5.1 Chỗ dùng và chế độ

Hiện `PartnerAutocomplete` được dùng ở **5** chỗ:
- header phiếu thu chi và main fields phiếu kho, đều ở chế độ tìm theo lý do;
- DS phiếu kho, trang công nợ đầu kỳ, hộp công nợ đầu kỳ, đều truyền `requireReason={false}`.

Bộ lọc của DS phiếu thu chi là component riêng `CashPartnerFilter` và **ngoài phạm vi**. Sau thiết kế này không còn chỗ nào tìm theo lý do, nên **bỏ chế độ `requireReason`**:
- 3 chỗ đang truyền `requireReason={false}` xóa prop đó;
- kiểu `PartnerSearchHook` còn `(keyword) => …`;
- `partnerType = 'None'` vẫn khóa ô ("Không cần đối tượng").

| Cách dùng | Dùng ở | Hook tìm | Thêm nhanh | Danh mục |
|---|---|---|---|---|
| Bộ lọc (như hiện nay) | DS phiếu kho, trang công nợ đầu kỳ | Mọi vai trò, kể cả ngừng dùng | Không | Không |
| Hộp công nợ đầu kỳ (như hiện nay) | `opening-debt-form-dialog` | Hook riêng theo phía công nợ | Không | Không |
| **Form phiếu** | Phiếu thu chi, phiếu kho | KH + NCC, chỉ đang dùng (`activeOnly=true`, không kèm `reasonId`) | Có (theo quyền tạo) | Có (theo quyền xem) |
| **Form báo giá** | Báo giá | Danh mục KH, chỉ đang dùng | Có, luôn tạo KH, chỉ khi có `customers.create` | Có (KH, theo `customers.view`) |

### 5.2 Danh sách tìm

- Mở khi đã gõ ít nhất 1 ký tự, debounce 250ms như hiện nay.
- Dòng đầu:
  - form phiếu: "Tìm thấy N đối tượng (KH + NCC) đang dùng";
  - báo giá: "Tìm thấy N khách hàng đang dùng";
  - bộ lọc: "Tìm thấy N đối tượng" (không có chữ "đang dùng", vì có cả đối tượng ngừng dùng);
  - khi N đạt giới hạn của server (20): "Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp";
  - bên phải là các phím: ↑↓ duyệt · Enter chọn · F4 thêm mới · Esc đóng. F4 chỉ hiện khi có thêm nhanh;
  - đang tải thì "Đang tìm kiếm...", lỗi thì "Không thể tải danh sách. Vui lòng thử lại." Từ khóa không mất.
- Cột: Mã · Tên · MST · Địa chỉ · SĐT · Loại. Loại là nhãn "Khách hàng"/"NCC" theo cờ vai trò; báo giá luôn là "Khách hàng".
- Không có kết quả: hiện dòng "Không tìm thấy `<…>` "`<chữ đã gõ>`"".
- Phần chân:
  - **＋ Thêm mới `<đối tượng|khách hàng>` "`<chữ đã gõ>`"** kèm gợi ý phím (F4; khi đã tìm xong mà 0 dòng thì Enter / F4). Chỉ hiện khi có thêm nhanh và không đang chờ, không đang tải, không lỗi;
  - **☰ Xem danh mục đầy đủ**: chỉ hiện khi có danh mục mà người dùng xem được (§7).
- Hai mục ở chân được chọn bằng ↑↓ / Tab như các dòng kết quả.

### 5.3 Phím ở ô mã

"Đang chờ" là khi từ khóa đang gõ khác từ khóa đã debounce, hoặc khi đang tải kết quả của đúng từ khóa đó. Kết quả cũ hiện trong lúc chờ (`placeholderData`) **không** được dùng cho Enter.

| Phím | Danh sách mở | Danh sách đóng |
|---|---|---|
| ↓ / ↑, Tab / Shift+Tab | Chuyển dòng được chọn (kể cả hai mục ở chân), quay vòng | ↓ mở danh sách khi đã gõ chữ (như hiện nay). Tab/Shift+Tab đi theo DOM |
| Enter, đã có kết quả của đúng từ khóa | Có mã trùng khớp chính xác thì chọn mã đó. Không thì làm theo dòng đang chọn: kết quả → chọn; Thêm mới → mở hộp thêm nhanh; Danh mục → mở danh mục. Tìm xong mà 0 dòng thì dòng Thêm mới được chọn sẵn, nên Enter mở hộp thêm nhanh | Có chữ mà chưa chọn → mở lại danh sách. Đã chọn hoặc ô trống → để form điều hướng |
| Enter, **đang chờ** | Ghi nhận "Enter chờ". Khi kết quả về: có mã trùng khớp thì chọn; có kết quả thì chọn dòng đầu; **0 dòng thì chỉ hiện danh sách với dòng Thêm mới được chọn sẵn, không tự mở hộp**. Gõ thêm chữ thì hủy "Enter chờ" | Như vậy |
| Shift+Enter | Không giữ lại, để form lùi về ô trước | Như vậy |
| **F4** | Mở hộp thêm nhanh với chữ đang gõ | Như vậy. Đã chọn đối tượng thì Tên để trống |
| Esc | Đóng danh sách, giữ chữ | — |

- Gõ khi đang có đối tượng được chọn thì bỏ chọn và bắt đầu tìm mới (như hiện nay).
- Chọn xong thì focus sang ô tên (D11). Việc này do component làm, nên `StockVoucherMainFields` bỏ đoạn tự focus lại ô mã.
- Ô bị khóa (chỉ đọc, lý do "Không có"): F4 và nút + không làm gì; nút + bị disabled.
- Nút **+** Tab tới được, có `aria-label`/tooltip "Thêm nhanh `<đối tượng|khách hàng>` (F4)" và `aria-keyshortcuts="F4"`.

### 5.4 `PartnerCodeNameField`

- Gồm một nhãn (`htmlFor` trỏ vào ô mã), ô mã kèm nút + và ô tên. Ô tên do form bên ngoài quản lý (truyền `register` hoặc giá trị và `onChange`).
- Placeholder: "Tên đối tượng" hoặc "Tên khách hàng".
- Nhận `error` (lỗi đối tượng, kể cả lỗi "Vui lòng chọn đối tượng từ danh sách") và hiện ngay dưới hàng.
- Cho form bên ngoài biết ô mã có "chữ mà chưa chọn" hay không (§3.4).

## 6. Hộp thêm nhanh — `PartnerQuickAddDialog`

### 6.1 Ô nhập

- Giống `CustomerFormFields`: Mã ("Để trống để tự sinh"), Tên\*, MST, SĐT, Email, Nhóm, Số ngày được nợ, Người liên hệ, Địa chỉ công ty, Địa chỉ giao hàng mặc định, Ghi chú; vai trò (khi có). Không có ô trạng thái.
- Nhãn dùng chung ("Mã", "Tên"). Tiêu đề hộp: "Thêm nhanh khách hàng" (báo giá) hoặc "Thêm nhanh đối tượng".
- `CustomerFormFields` hiện khóa vai trò của màn hình (đánh dấu sẵn và disabled), và chọn schema/nhãn theo vai trò. Hộp này cần một chế độ mà **cả hai ô vai trò đều tự do**. Plan chọn cách làm: thêm chế độ cho `CustomerFormFields`, hoặc dùng bộ ô riêng.
- **Điền sẵn**: Tên = chữ đang gõ (đã trim). Mở hộp thì focus vào Tên, con trỏ ở cuối chữ.

### 6.2 Vai trò

| Màn hình | Vai trò yêu cầu của lý do | Vai trò chọn sẵn |
|---|---|---|
| Phiếu thu/chi, phiếu kho | khách hàng / nhà cung cấp | theo lý do |
| Phiếu thu, phiếu xuất | bất kỳ, hoặc lý do trống | khách hàng |
| Phiếu chi, phiếu nhập | bất kỳ, hoặc lý do trống | nhà cung cấp |
| Báo giá | — | khách hàng; ẩn ô vai trò (như hiện nay) |

- Hai ô vai trò đều tự do; người dùng bỏ ô chọn sẵn được. Phải còn ít nhất một ô, nếu không thì báo "Chọn ít nhất một vai trò".
- Ô vai trò nào người dùng không có quyền `customers.create` / `suppliers.create` thì bị disabled. Nếu vai trò chọn sẵn bị disabled thì chọn sẵn vai trò còn lại.
- Không có quyền tạo nào thì không có nút +, F4 và dòng Thêm mới. Báo giá chỉ xét `customers.create`.

### 6.3 Lưu

- Chọn API khi bấm lưu: có tick KH thì `POST /customers` (kèm `isSupplier=true` nếu tick NCC); chỉ tick NCC thì `POST /suppliers`. Server kiểm quyền của từng vai trò được bật (`PartnerPermissionGuard`).
- Tạo xong:
  - toast "Đã tạo khách hàng/nhà cung cấp `<mã>`";
  - gọi `invalidatePartnerQueries`, hàm này giờ gồm cả kết quả tìm của phiếu thu chi (§8);
  - đối tượng mới được chọn như chọn từ danh sách (§4.2 với phiếu thu chi và phiếu kho);
  - focus sang ô tên (D11).
- Lỗi: toast lỗi; hộp vẫn mở và giữ dữ liệu. Lỗi theo ô (ví dụ trùng mã) hiện dưới ô đó như màn danh mục hiện nay.

### 6.4 Phím trong hộp

| Phím | Hành vi |
|---|---|
| Enter | Sang ô kế tiếp chưa bị khóa, theo thứ tự: Mã → Tên → MST → SĐT → Email → Nhóm → Số ngày được nợ → Người liên hệ → Địa chỉ công ty → Địa chỉ giao hàng → Ghi chú → Là khách hàng → Là nhà cung cấp. Ô vai trò bị ẩn (báo giá) hoặc disabled thì bỏ qua. Ở **ô cuối chưa bị khóa** thì **Tạo mới** |
| Shift+Enter | Về ô trước |
| Ctrl+Enter | Tạo mới, ở bất kỳ ô nào |
| Esc | Đóng hộp (§6.5) |

- **Textarea** (hai địa chỉ, Ghi chú): Enter cũng sang ô kế tiếp, không xuống dòng; Shift+Enter về ô trước. Hộp thêm nhanh không cần nhập nhiều dòng; muốn nhiều dòng thì sửa sau ở màn danh mục.
- **Nhóm** (Radix Select) khi đang đóng: Enter sang ô kế tiếp, Space hoặc ↓ mở danh sách. Khi đang mở thì Radix tự xử lý phím.
- Bỏ qua Enter khi đang gõ dấu (`isComposing`).

### 6.5 Đóng hộp

- Esc hoặc Hủy: đóng hộp, **giữ chữ đã gõ** ở ô mã, đưa focus về ô mã một cách tường minh. Dialog mặc định trả focus về phần tử đã mở hộp, ví dụ nút +, nên cần chặn `onCloseAutoFocus` rồi tự focus.

### 6.6 Không để sự kiện lan ra form phiếu

- Hộp thêm nhanh và hộp danh mục **không được để submit và keydown lan ra `<form>` của phiếu**. Sự kiện React đi qua portal theo cây component, nên submit của hộp sẽ chạy `onSubmit` của phiếu và lưu phiếu.
- Cách làm (plan chọn):
  - render hộp ngoài `<form>` của trang, như báo giá hiện nay; hoặc
  - bọc nội dung hộp bằng phần tử dừng lan `onSubmit` và `onKeyDown`.
- Ngoài ra, handler phím của form phiếu (Enter điều hướng, Ctrl+S, Insert) bỏ qua sự kiện có target nằm trong `DIALOG_SELECTOR`.
- Test bắt buộc: tạo đối tượng từ hộp **không** gọi API lưu phiếu; Ctrl+S và Enter trong hộp không lưu phiếu và không chuyển ô của phiếu.

## 7. Hộp danh mục — `PartnerCatalogDialog`

- Phát triển từ `CustomerCatalogDialog`/`CustomerCatalogList`. Danh sách hiện chỉ dùng `useCustomers` và nhãn KH; giờ nhận vai trò để dùng `useCustomers` hoặc `useSuppliers`.
- **Quyền xem**:
  - KH cần `customers.view`, NCC cần `suppliers.view`;
  - dòng "Xem danh mục đầy đủ" chỉ hiện khi người dùng xem được ít nhất một vai trò mà ô cho phép;
  - nút chuyển **Khách hàng | Nhà cung cấp** chỉ có khi xem được cả hai;
  - vai trò mặc định lấy theo vai trò chọn sẵn của hộp thêm nhanh (§6.2); nếu không xem được vai trò đó thì lấy vai trò còn lại.
- Ô lọc lấy sẵn chữ đang gõ. Thêm điều khiển bàn phím (hiện chỉ dùng chuột): ↑↓ chọn dòng, Enter chọn, Esc đóng (§6.5, focus về ô mã).
- Chọn một dòng thì tải chi tiết (`customersApi.get` / `suppliersApi.get`) và đưa ra **kèm `isCustomer`, `isSupplier`, `creditDays`**, để tự khớp lý do (§4.2) và phiếu kho dùng được. Sau đó chọn như từ danh sách tìm, focus sang ô tên.
- Cùng quy tắc chống lan sự kiện ở §6.6.

## 8. Backend và lớp dữ liệu frontend

- `CashVouchersController.SearchPartners` và `StockVouchersController.SearchPartners` thêm `[FromQuery] bool? activeOnly = null`. `ICashVoucherService.SearchPartnersAsync` và `IStockVoucherService.SearchPartnersAsync` nhận thêm tham số. Service dùng `ActiveOnly = activeOnly ?? reasonId.HasValue`. Tham số `reasonId` giữ lại vì tương thích, dù form không dùng nữa.
- **Seed quyền** (D12, `DbSeeder.cs`):
  - Kế toán thêm `customers.create`, `suppliers.view`, `suppliers.create`;
  - Kho / giao hàng thêm `customers.create`, `suppliers.create`.
  - Seeder chỉ tự cấp **mã quyền mới** cho role đã tồn tại, nên DB đang chạy phải để admin bật các quyền này trong ma trận quyền. Việc này ghi vào checklist triển khai của plan.
- Frontend:
  - `useCashPartnerSearch` và `usePartnerSearch` (phiếu kho) nhận `activeOnly`. Khóa query (`cash/keys.ts:12`, `stock-vouchers/keys.ts:20`) và `sameRoleFilter` (cash `hooks.ts:69-73`, stock `:92-96`) đều phải có `activeOnly`, để kết quả của bộ lọc và của form không dùng lẫn của nhau;
  - `invalidatePartnerQueries` (`customers/hooks.ts:13-22`) thêm khóa partners của phiếu thu chi (`['cash','cash-vouchers','partners']`).
- Phần lưu phiếu, DTO và mẫu in không đổi.

## 9. Trường hợp biên

- Phiếu mới không có lý do mặc định (thiếu cấu hình): vẫn tìm được đối tượng. Chọn đối tượng thì lý do trống được khớp theo §4.2. Phần tự điền chờ tới khi có lý do.
- Đối tượng ngừng dùng trên phiếu đã lưu vẫn hiện mã như hiện nay; không tìm thấy lại trong danh sách vì chỉ lấy đối tượng đang dùng.
- Phiếu kho có nháp localStorage: khôi phục đối tượng như cũ. Nháp có lưu `isCustomer`/`isSupplier` nên vai trò vẫn biết.
- Phiếu thu/chi tự động, đã hủy hoặc không có quyền sửa: chỉ đọc; không tự điền, không tự focus, không có nút + và F4.
- Đối tượng vừa thêm nhanh không hợp lý do (người dùng đã bỏ vai trò chọn sẵn): đi qua §4.2 nên lý do tự đổi.
- Hai tab cùng tạo nhanh một mã: server báo trùng mã, lỗi hiện trong hộp thêm nhanh.
- Gõ mã rồi Enter thật nhanh: theo quy tắc "Enter chờ" (§5.3). Không bao giờ tự mở hộp thêm nhanh khi người dùng chưa thấy danh sách 0 dòng.
- Gõ chữ vào ô mã rồi rời ô mà không chọn: chữ vẫn còn. Lưu thì bị chặn theo §3.4.

## 10. Danh sách kiểm thử

**Unit**
- `buildAutoDescription`: đủ cả hai, chỉ lý do, không có lý do, tên có khoảng trắng.
- `followAuto`: ô trống, ô còn bằng `prevAuto`, ô đã sửa tay.
- `followHeaderInLines`: dòng trống, dòng bằng giá trị cũ, dòng đã sửa tay.
- `isUntouchedLine`.
- `requiredRole`: đủ bảng §4.1.
- `matchReasonForPartner`: giữ lý do hiện tại; chuyển sang lý do KH/NCC (kể cả "Bất kỳ" có ảnh hưởng công nợ); chuyển sang lý do "bất kỳ"; lý do đang trống; không có lý do nào hợp; đối tượng có cả hai vai trò.
- `focusNextInOrder`: bỏ qua ô bị khóa, đi xuôi và ngược, hết danh sách.

**Component**
- Ô dùng chung:
  - dòng đầu theo chế độ, cột SĐT, câu khi đạt giới hạn 20;
  - dòng Thêm mới và Danh mục, chọn được bằng ↑↓/Tab;
  - tìm xong 0 dòng thì Enter gọi `onAddNew`;
  - **Enter khi đang chờ** chọn đúng mã; Enter chờ mà ra 0 dòng thì không mở hộp;
  - F4; Shift+Enter đi qua; nút + Tab tới được;
  - chế độ bộ lọc không có Thêm mới hay Danh mục và vẫn tìm cả đối tượng ngừng dùng;
  - khóa khi lý do "Không có";
  - chọn xong thì focus sang ô tên.
- Hộp thêm nhanh:
  - vai trò mặc định theo §6.2; vai trò bị disabled theo quyền; ít nhất một vai trò;
  - gọi đúng `POST /customers` hoặc `/suppliers`;
  - thứ tự Enter, textarea, Select, Ctrl+Enter;
  - Esc giữ chữ và đưa focus về ô mã;
  - lỗi thì hộp vẫn mở.
- **Chống lan sự kiện** (§6.6): đặt hộp trong một `<form>` có `onSubmit` giả lập; tạo đối tượng không gọi `onSubmit` đó; Ctrl+S trong hộp không lưu.
- Hộp danh mục: chuyển KH/NCC theo quyền xem; ẩn khi không có quyền xem; ↑↓/Enter; trả về có cờ vai trò.
- `CashLineGrid`: thứ tự Enter và Shift+Enter (§2.2); dòng mới được tự điền; Insert.

**Trang**
- Phiếu thu/chi:
  - phiếu mới focus vào ô mã; thứ tự Enter 1→10 rồi xuống lưới; Shift+Enter đi ngược;
  - chọn đối tượng thì focus sang Tên;
  - các bước tự điền đúng như ví dụ §3.3;
  - **mở phiếu đã lưu thì form không dirty và nút In vẫn bật**; phiếu mới cũng không dirty khi chưa thao tác;
  - bỏ dòng chưa nhập khi lưu; chữ trong ô mã mà chưa chọn thì chặn lưu;
  - payload không có `partnerTaxCode`;
  - tự đổi lý do kèm dòng báo vàng; đổi lý do vẫn giữ đối tượng và báo đỏ;
  - thêm nhanh rồi tự chọn, không lưu phiếu;
  - phiếu chỉ đọc không tự điền.
- Phiếu kho:
  - bố cục hàng 2 ở cả ba mốc độ rộng; tìm cả hai vai trò;
  - tự khớp lý do; đổi lý do không xóa đối tượng;
  - lỗi `partnerId` hiện dưới hàng Đối tượng;
  - thứ tự Enter không đổi; chọn đối tượng thì focus sang Tên;
  - nháp khôi phục được.
- Báo giá:
  - chọn và bỏ chọn khách hàng; rời ô tên trống thì điền lại; F4 thêm nhanh (chỉ khi có `customers.create`); danh mục;
  - cảnh báo KH ngừng sử dụng;
  - Enter sẵn có từ `customerId` sang `customerName`;
  - chữ trong ô mã mà chưa chọn thì chặn lưu.
- Bộ lọc (DS phiếu kho, công nợ đầu kỳ): chạy như cũ.

**Test hiện có phải sửa** (theo lượt đối chiếu code)
- `components/partner-autocomplete/partner-autocomplete.test.tsx` (các ca chế độ theo lý do).
- `components/customer-autocomplete/customer-autocomplete.test.tsx` (xóa cùng component).
- `features/customers/components/customer-catalog-dialog.test.tsx`, `customer-catalog-list.test.tsx`.
- `pages/customers/customer-form-fields.test.tsx`.
- `pages/quotations/quotation-form-page.draft.test.tsx` (mock đường dẫn customer-autocomplete).
- `pages/cash-vouchers/cash-voucher-form-page.test.tsx` (mock hook, kiểm `reasonId`, ô đối tượng bị khóa khi chưa có lý do), `cash-voucher-list-page.test.tsx`.
- `pages/stock-vouchers/stock-voucher-form-page.test.tsx` (mock, đổi lý do xóa đối tượng, các test Enter), `stock-voucher-list-page.test.tsx`.
- `features/stock-vouchers/hooks.test.tsx`.
- `features/cash-vouchers/payload.test.ts`, `schema.test.ts` (`partnerTaxCode`).

**Backend** (integration, DB `qldonhang_integtest`)
- `activeOnly=true` không kèm `reasonId`: trả cả KH và NCC, không có đối tượng ngừng dùng. Mở rộng `CashVoucherQueryTests` và `StockVoucherHelpersTests`.
- Không truyền `activeOnly`: kết quả như hiện nay, cho cả trường hợp có và không có `reasonId`.
- Seed: DB mới thì role Kế toán và Kho có các quyền mới (D12).

**Trình duyệt thật**
- Chạy app dev và làm hết 3 màn hình bằng bàn phím: Enter/Shift+Enter, F4, thêm nhanh, danh mục.
- Gõ mã rồi Enter thật nhanh.
- Thêm một lượt gõ dấu Telex.
- Đăng nhập bằng một tài khoản Kế toán để kiểm tra quyền.
