# Thiết kế lại form phiếu thu/chi và ô đối tượng dùng chung

> Brainstorm: 2026-10-15 06:44:11 · Review: 2026-10-15 07:27:02, đã sửa theo [section-02-review.md](section-02-review.md) · Trạng thái: **thiết kế đã duyệt**, chưa có plan · Chi tiết hành vi: [section-01-behaviour-spec.md](section-01-behaviour-spec.md)
>
> Mockup: [mockup-01-cash-voucher-form.html](mockup-01-cash-voucher-form.html) (form phiếu thu/chi) · [mockup-02-shared-partner-field.html](mockup-02-shared-partner-field.html) (ô đối tượng dùng chung; thay phần ô đối tượng của mockup-01). Mockup được vẽ trước review, nên có bốn chỗ **đã bị thay**:
> 1. Chọn đối tượng xong thì focus sang **Tên đối tượng**, không ở lại ô mã (D11).
> 2. Nút + Tab tới được (D13).
> 3. Màn phiếu kho của mockup-02 vẫn tìm theo lý do và bỏ đối tượng khi đổi lý do. Thực tế phiếu kho tìm cả KH và NCC, lý do tự khớp (D9).
> 4. Lý do đang trống được tính là không hợp (section-01 §4.1).
>
> Liên quan: [BD 4.2 Mã đối tượng](../../bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md) (một số mục được thay bởi thiết kế này, xem D13). Brainstorm [261015-0656 QR chuyển khoản](../261015-0656-voucher-payment-qr/SUMMARY.md) cũng sửa `cash-voucher-form-page.tsx` (thêm nút QR); plan nào làm sau thì rebase lên plan làm trước.

## Problem framing

Form phiếu thu/chi ([cash-voucher-header.tsx](../../../frontend/src/pages/cash-vouchers/components/cash-voucher-header.tsx), [cash-voucher-form-page.tsx](../../../frontend/src/pages/cash-vouchers/cash-voucher-form-page.tsx)) có các vấn đề sau:

- Ô **MST** phải nhập tay, dù server đã tự lấy MST từ danh mục khi không có giá trị gửi lên. Mẫu in 01-TT/02-TT cũng không in MST.
- **Mã** và **tên** đối tượng nằm trên hai hàng. Ô tìm đối tượng bị khóa ("Chọn lý do trước") cho tới khi chọn Lý do.
- **Lý do nộp/chi** và **Diễn giải** từng dòng phải gõ tay cho mọi phiếu, dù nội dung gần như luôn là "lý do + tên đối tượng".
- **Enter không di chuyển** giữa các ô. Form không có nút submit nên Enter không làm gì.
- Không tìm thấy đối tượng thì phải rời form, sang trang Khách hàng/NCC để tạo.

Ô đối tượng cũng **không đồng nhất** giữa các màn hình:

- Phiếu kho và phiếu thu chi dùng `PartnerAutocomplete`: không có thêm nhanh, không có danh mục, không có cột SĐT.
- Báo giá dùng `CustomerAutocomplete`: có nút + rời bên phải, có "Xem danh mục đầy đủ", nhưng không có phím tắt và không có dòng thêm mới.

## Goals

Tám yêu cầu của người dùng cho phiếu thu/chi:

1. Bỏ ô MST. Khi lưu, MST lấy từ danh mục đối tượng.
2. Mã và tên đối tượng nằm cùng một dòng.
3. Lý do nộp/chi và Diễn giải tự điền theo Lý do. Ô trống thì điền; đã có nội dung thì giữ (chi tiết ở D3).
4. Dòng thêm mới cũng được tự điền.
5. Ô mã đối tượng có nút thêm nhanh đối tượng khi tìm không thấy, kiểu MISA.
6. Enter đi tới, Shift+Enter lùi lại giữa các ô nhập trên màn hình.
7. Phiếu mới focus sẵn ô mã đối tượng. Lý do được nhập sau đối tượng.
8. Từ ô Ghi chú, Enter xuống dòng cuối của lưới diễn giải/số tiền.

Mở rộng theo yêu cầu khi duyệt thiết kế:

9. **Ô mã đối tượng dùng chung** cho phiếu thu chi, phiếu nhập/xuất kho và báo giá, giao diện và phím giống nhau. Có nút thêm nhanh và phím tắt **F4**.
10. Phiếu kho cũng tìm cả KH và NCC, lý do tự khớp như phiếu thu chi.

## Non-goals

- Mẫu in 01-TT/02-TT, 01-VT/02-VT: không đổi.
- Quy tắc lưu phiếu và DTO ở backend: không đổi. Backend chỉ thêm tham số `activeOnly` cho hai endpoint tìm đối tượng và thêm quyền cho hai role trong seed.
- Điều hướng Enter của báo giá (đã có sẵn) và thứ tự Enter của phiếu kho: giữ nguyên.
- Bộ lọc đối tượng của DS phiếu thu chi (`CashPartnerFilter`, component riêng): không đổi.
- Bộ lọc của DS phiếu kho và màn công nợ đầu kỳ: chỉ nhận phần giao diện chung (dòng đầu, cột SĐT), không có thêm nhanh.
- Thiết kế lại riêng cho mobile.
- Phát hiện trùng đối tượng giữa các vai trò khi thêm nhanh.

## Constraints & assumptions

- Phiếu thu/chi mới có sẵn lý do mặc định (TKH/TNCC) và quỹ mặc định từ `GET /cash-vouchers/defaults`.
- Danh mục đối tượng là **một bảng** với hai cờ `IsCustomer`/`IsSupplier`.
  - Tạo hoặc bật một vai trò cần quyền tạo của vai trò đó (`customers.create` / `suppliers.create`, `PartnerPermissionGuard`). Xem danh mục cần `customers.view` / `suppliers.view`.
  - Role seed: Kế toán chỉ có `customers.view`; Kho có quyền xem cả hai nhưng không có quyền tạo (xem D12).
- Server kiểm đối tượng bằng `ValidatePartner` (phiếu thu chi **và** phiếu kho). Bước 1 theo `PartnerType` của lý do. Bước 2: lý do có ảnh hưởng công nợ thì đối tượng phải có vai trò của phía công nợ, kể cả khi `PartnerType = Any`. Lỗi trả về với khóa `partnerId`.
- `partnerTaxCode` bỏ trống khi lưu (cả lúc tạo và lúc sửa) thì server lấy `partner.TaxCode` (`CashVoucherService.cs:684`).
- Hai endpoint tìm đối tượng: không gửi `reasonId` thì tìm mọi vai trò nhưng **gồm cả đối tượng ngừng dùng** (`ActiveOnly = reasonId.HasValue`). Đây là chế độ bộ lọc.
- `MoneyCell` không xử lý Enter, lưu giá trị theo từng phím và tự bôi đen khi focus.
- Báo giá đã có điều hướng Enter (`GENERAL_INFO_FIELD_ORDER`, gồm `customerId` → `customerName`). Chọn khách hàng thì focus sang Tên KH.
- Sự kiện React (submit, keydown) đi xuyên qua portal theo cây component. Một hộp thoại có `<form>` nằm trong `<form>` của phiếu sẽ submit luôn phiếu. Báo giá tránh được vì đặt hộp ngoài form.
- Lý do được sắp theo loại rồi theo mã. Với lý do hệ thống, mỗi loại phiếu chỉ có một lý do cho mỗi vai trò.

## Decisions

| # | Câu hỏi | Quyết định |
|---|---|---|
| D1 | Ô nào tự điền? | **Lý do nộp/chi** (đầu phiếu) và **Diễn giải** từng dòng. Ghi chú đầu phiếu và Ghi chú dòng không tự điền. |
| D2 | Nội dung tự điền | **"Tên lý do - Tên đối tượng"**. Chưa có tên đối tượng thì chỉ có tên lý do. Tên lấy từ ô Tên đối tượng. Thay mẫu "Thu tiền của [Tên]" của BD 4.2 §10. |
| D3 | Đổi lý do/đối tượng sau khi đã điền | **Cập nhật khi chưa sửa tay**: ô còn trống hoặc còn đúng chữ hệ thống điền lần trước thì thay; đã sửa tay thì giữ. Diễn giải dòng **lấy theo Lý do nộp**. |
| D4 | Tìm đối tượng khi nhập trước lý do | **Tìm cả KH và NCC, lý do tự khớp**: chọn đối tượng không hợp thì tự chuyển sang lý do phù hợp đầu tiên và nháy vàng; đổi lý do sau đó **không xóa** đối tượng mà báo đỏ. |
| D5 | Duyệt UI phiếu thu/chi | mockup-01 được duyệt, gồm cả hai đề xuất thêm: Enter ở ô cuối của dòng cuối thì thêm dòng; Enter điều hướng trong hộp thêm nhanh. |
| D6 | Cách tổ chức code | **Hàm thuần + mở rộng component sẵn có** (phương án A). |
| D7 | Phạm vi ô đối tượng | Dùng chung cho phiếu thu chi, phiếu kho và báo giá. Báo giá sửa cho đồng nhất. |
| D8 | Phím tắt thêm nhanh | **F4** (giống thói quen Fast: F4 = Thêm mới). |
| D9 | Phiếu kho tìm thế nào | **Tìm cả KH và NCC, lý do tự khớp**, như D4. |
| D10 | Thiết kế kỹ thuật | Phần 1 (thành phần) và Phần 2 (backend, lỗi, quyền, kiểm thử) đã duyệt. |
| D11 | Focus sau khi chọn hoặc thêm nhanh (review) | **Sang Tên đối tượng**, cho cả 3 màn hình. Đúng BD 4.2 AC-OBJ-021 và báo giá hiện nay. |
| D12 | Quyền thêm nhanh của Kế toán/Kho (review) | **Thêm vào seed**: Kế toán thêm `customers.create`, `suppliers.view`, `suppliers.create`; Kho thêm `customers.create`, `suppliers.create`. DB đang chạy thì admin bật trong ma trận quyền (checklist triển khai). |
| D13 | Khác biệt với BD 4.2 (review) | **Giữ quyết định mới**: dòng Thêm mới trong danh sách + F4 (thay BD §4, §15.2), D2 (thay §10), D4/D9 (thay §1, §3, AC-OBJ-006). **Lấy từ BD**: nút + Tab tới được (§4); gõ chữ vào ô mã mà không chọn thì không cho lưu, báo "Vui lòng chọn đối tượng từ danh sách" và focus về ô mã (§14, AC-OBJ-023/024). Đầu BD 4.2 đã có ghi chú về các mục bị thay. |

## Approaches considered

| # | Phương án | Ưu | Nhược | Kết quả |
|---|---|---|---|---|
| **A** | **Hàm thuần + mở rộng component sẵn có**: logic tự điền, tự khớp lý do, thứ tự Enter là hàm thuần có unit test; `PartnerAutocomplete` thêm tùy chọn mới và bỏ chế độ tìm theo lý do; `CustomerQuickAddDialog`/`CustomerCatalogDialog` mở rộng cho NCC | Một ô đối tượng duy nhất; logic test được không cần DOM | Đụng component đang dùng ở 5 chỗ → phải test lại các bộ lọc | **Chọn** |
| B | Combobox riêng cho phiếu thu chi | Không ảnh hưởng phiếu kho | Trùng code tìm kiếm và phím; trái với yêu cầu đồng nhất (D7) | Loại |
| C | Bộ điều hướng Enter + thêm nhanh dùng chung cho mọi phiếu, chuyển luôn phiếu kho sang | Nhất quán nhất | Phạm vi lớn; phải viết lại điều hướng Enter của phiếu kho, vốn đã ổn định | Loại |

## Recommended approach

### 1. Giao diện

**Phiếu thu/chi**:

| Hàng | Nội dung |
|---|---|
| 1 | Số phiếu (chỉ đọc) · Ngày giờ\* · Quỹ\* [nhãn loại quỹ] |
| 2 | **Đối tượng**: [mã ▾ ✕][+] [tên đối tượng ……] |
| 3 | Lý do\* · Người nộp/Người nhận |
| 4 | Địa chỉ |
| 5 | Lý do nộp/Lý do chi (tự điền) |
| 6 | Kèm theo · Ghi chú |
| Lưới | # · Diễn giải (tự điền) · Số tiền · Ghi chú · xóa |

**Phiếu kho**: hàng 1 giữ nguyên (Ngày · Lý do · Kho · HTTT). Hàng 2 đổi thành **Đối tượng** [mã][+] [tên] · **Đ.chỉ**.

**Báo giá**: "Mã KH" + "Tên KH" gộp thành **Khách hàng**\* [mã][+] [tên]. Vẫn giữ id `customerId`/`customerName`.

### 2. Hành vi (chi tiết ở [section-01](section-01-behaviour-spec.md))

- **Thứ tự Enter của phiếu thu/chi**:
  - 1 Ngày giờ → 2 Quỹ → **3 Mã đối tượng (bắt đầu)** → 4 Tên → 5 Lý do → 6 Người nộp → 7 Địa chỉ → 8 Lý do nộp → 9 Kèm theo → 10 Ghi chú;
  - từ Ghi chú xuống dòng cuối của lưới, ô Diễn giải được bôi đen sẵn;
  - trong lưới: Diễn giải → Số tiền → Ghi chú → dòng sau; Enter ở ô cuối của dòng cuối thì thêm dòng;
  - Shift+Enter đi ngược lại;
  - chọn đối tượng thì focus sang Tên (D11).
- **Tự điền**: Lý do + Tên đối tượng → Lý do nộp → Diễn giải các dòng (D2, D3).
  - Phiếu mới điền lần đầu mà **không làm form dirty**.
  - Phiếu đã lưu **không ghi gì khi mở**, chỉ ghi nhớ chữ tự điền trước đó, nên nút In và nút QR vẫn bật.
- **Khi lưu**:
  - ô mã còn chữ mà chưa chọn thì chặn lưu (D13);
  - bỏ những dòng có số tiền 0 mà diễn giải trống hoặc bằng Lý do nộp;
  - không gửi `partnerTaxCode`. Hệ quả: sửa phiếu cũ thì MST lấy lại theo danh mục; đối tượng gõ tay không còn MST.
- **Lý do tự khớp** (phiếu thu chi và phiếu kho):
  - chọn đối tượng không hợp lý do (kể cả khi lý do đang trống) → chuyển sang lý do KH/NCC đầu tiên hợp, nếu không có thì sang lý do "Bất kỳ"; nháy vàng kèm dòng giải thích;
  - đổi lý do sang loại không hợp → giữ đối tượng, báo đỏ; lý do "Không có" → xóa đối tượng;
  - lỗi `partnerId` từ server hiện dưới hàng Đối tượng ở cả hai form.
- **Ô đối tượng dùng chung**:
  - dòng đầu báo số kết quả theo chế độ; các cột Mã · Tên · MST · Địa chỉ · SĐT · Loại;
  - cuối danh sách có **＋ Thêm mới "…"** và **Xem danh mục đầy đủ** (theo quyền xem);
  - **F4** hoặc nút **+** (Tab tới được) mở hộp thêm nhanh;
  - **Enter khi kết quả chưa về sẽ chờ kết quả của đúng từ khóa**, không bao giờ tự mở hộp thêm nhanh khi người dùng chưa thấy danh sách 0 dòng.
- **Hộp thêm nhanh**:
  - hai ô vai trò tự do, chọn sẵn theo lý do, bị disabled nếu không có quyền tạo;
  - chọn API khi lưu;
  - Enter đi giữa các ô (textarea và Select theo quy tắc riêng), Ctrl+Enter tạo, Esc đóng và đưa focus về ô mã;
  - **không để submit/keydown lan ra form phiếu**.

### 3. Thành phần

- **Ô dùng chung**: phát triển tiếp [partner-autocomplete.tsx](../../../frontend/src/components/partner-autocomplete/partner-autocomplete.tsx).
  - Tùy chọn mới: `onAddNew(keyword)`, `catalog`, `noun`; có quy tắc "Enter chờ".
  - Bỏ chế độ tìm theo lý do (`requireReason`): 2 form chuyển sang tìm mọi vai trò; 3 chỗ dùng làm bộ lọc chỉ cần xóa prop `requireReason={false}`. `partnerType = 'None'` vẫn khóa ô.
  - Component mới `PartnerCodeNameField`: một nhãn, ô mã kèm nút + và ô tên, có chỗ hiện lỗi.
  - `CustomerAutocomplete` bị xóa; báo giá dùng ô chung với nguồn tìm là danh mục khách hàng.
- **`PartnerQuickAddDialog`** (từ `customer-quick-add-dialog.tsx`):
  - vai trò chọn sẵn, vai trò được phép theo quyền, có hiện ô vai trò hay không, tên điền sẵn;
  - `CustomerFormFields` cần chế độ mà hai ô vai trò đều tự do, hoặc hộp dùng bộ ô riêng (plan chọn).
- **`PartnerCatalogDialog`** (từ `CustomerCatalogDialog`): KH hoặc NCC theo quyền xem, điều khiển bằng bàn phím, trả về kèm cờ vai trò.
- **Hàm thuần**:
  - `features/cash-vouchers/auto-text.ts`: `buildAutoDescription`, `followAuto`, `followHeaderInLines`, `isUntouchedLine`;
  - `reason-match.ts` (dùng chung): `requiredRole`, `partnerFitsReason`, `matchReasonForPartner`;
  - `lib/enter-focus.ts`: `focusNextInOrder`.
- **Phiếu thu/chi**:
  - header theo bố cục mới, nhận đủ `partnerType`/`debtEffect` của lý do, tách ô lỗi lý do và đối tượng;
  - hook `useCashAutoText` (theo §3.2 khi mở form);
  - điều hướng Enter trong `CashLineGrid`; focus khi tạo phiếu;
  - `schema.ts`/`payload.ts` bỏ `partnerTaxCode`.
- **Phiếu kho**:
  - `StockVoucherMainFields` dùng `PartnerCodeNameField`, tìm `activeOnly=true` không kèm lý do;
  - `handleReasonChange` không xóa đối tượng, trừ lý do "Không có";
  - thêm `partnerId` vào `HEADER_ERROR_FIELDS`.
- **Báo giá**: `quotation-form-page.tsx` dùng `PartnerCodeNameField`, giữ điều hướng Enter sẵn có.

### 4. Backend

- `GET /cash-vouchers/partners` và `GET /stock-vouchers/partners` thêm `activeOnly` (bool?, mặc định `= reasonId.HasValue`, nên bộ lọc không đổi). Interface và service nhận thêm tham số.
- Seed quyền theo D12.
- Không đổi phần lưu phiếu, không đổi DTO.

### 5. Lỗi, quyền

- Không có quyền tạo nào thì không có nút +, F4 và dòng Thêm mới. Báo giá chỉ xét `customers.create`.
- Ô vai trò không có quyền tạo thì bị disabled.
- Danh mục chỉ hiện với vai trò người dùng xem được.
- Thêm nhanh lỗi thì hiện toast, hộp vẫn mở. Đang chờ, đang tải hoặc lỗi tìm kiếm thì ẩn dòng Thêm mới.
- Thêm nhanh xong làm mới kết quả tìm của cả phiếu kho và phiếu thu chi (`invalidatePartnerQueries`).
- Phiếu chỉ đọc hoặc phiếu tự động: không tự điền, không tự focus, không thêm nhanh.
- Enter khi đang gõ dấu bị bỏ qua. Ctrl+S, Insert, Alt+1..4 giữ nguyên, và đều bỏ qua sự kiện đến từ hộp thoại.

### 6. Kiểm thử

Danh sách đầy đủ ở section-01 §10. Các điểm chính:
- unit cho các hàm thuần;
- component cho ô dùng chung (gồm Enter chờ), hộp thêm nhanh, hộp danh mục, **chống lan sự kiện qua portal**;
- trang: ba form, gồm kiểm tra mở phiếu đã lưu thì form không dirty;
- sửa khoảng 13 file test frontend hiện có;
- backend: integration test `activeOnly` trên DB `qldonhang_integtest`, cùng kiểm tra seed;
- chạy thử trên trình duyệt thật, gồm một tài khoản Kế toán.

## Risks

- **Thêm trùng đối tượng ở báo giá.** Báo giá chỉ tìm khách hàng; gõ tên một NCC chưa là KH sẽ không thấy, và người dùng có thể thêm nhanh thành bản ghi mới thay vì bật vai trò KH. Phiếu thu chi và phiếu kho đã giảm rủi ro này nhờ tìm cả hai vai trò và quy tắc "Enter chờ".
- **Lý do tự đổi có thể gây bất ngờ**, nhất là ở phiếu kho: ô Lý do nằm trước ô đối tượng nên lúc đó con trỏ đã ở phía sau, và việc đổi lý do kéo theo ô Hạn thanh toán và số ngày nợ. Giảm bằng hiệu ứng nháy vàng và dòng giải thích.
- **Sửa component dùng ở 5 chỗ** và thay ô khách hàng của báo giá: cần test hồi quy cho chế độ bộ lọc và cho báo giá (kể cả nháp).
- **Quyền trên DB đang chạy**: seed không tự cấp quyền đã có cho role cũ. Nếu admin không bật, Kế toán vẫn không thấy thêm nhanh.
- **F4 trên laptop** có thể phải bấm kèm Fn. Nút + vẫn dùng được bằng chuột và Tab.
- **Phiếu cũ** không biết vai trò đối tượng, nên lỗi lý do không hợp chỉ hiện khi lưu.
- **Chồng lấn với brainstorm QR** (261015-0656): cùng sửa `cash-voucher-form-page.tsx`, cần thống nhất thứ tự merge.

## Open questions

Không còn câu hỏi nào chặn việc lập plan. Câu chữ của các dòng gợi ý và thông báo sẽ chốt trong plan, bám theo mockup và section-01. Đề xuất chưa áp dụng (cần người dùng đồng ý): Enter ở ô cuối của dòng cuối chỉ thêm dòng khi dòng đó đã có số tiền (section-02).

## Next steps

Lập plan bằng `write-plan`, gợi ý chia 5 giai đoạn:

1. **Backend + hàm thuần**: `activeOnly` cho hai endpoint (kèm integration test); seed quyền D12; `auto-text`, `reason-match`, `enter-focus`.
2. **Ô đối tượng dùng chung**:
   - `PartnerAutocomplete` (Enter chờ, Thêm mới, F4, danh mục, dòng đầu, SĐT), `PartnerCodeNameField`, `PartnerQuickAddDialog`, `PartnerCatalogDialog`;
   - chuyển ngay hook tìm của phiếu thu chi và phiếu kho sang `activeOnly` rồi mới **bỏ `requireReason` trong cùng giai đoạn**, để hai form không bị gãy;
   - test hồi quy cho các bộ lọc; nên làm thành PR riêng.
3. **Form phiếu thu/chi**.
4. **Form phiếu kho và báo giá**.
5. **Tài liệu và chạy thử**:
   - cập nhật `docs/code-standard/conventions.md` (quy tắc Enter giờ không chỉ cho phiếu kho, ô đối tượng dùng chung, F4) và `docs/codebase/directory-structure.md`;
   - ghi checklist triển khai (bật quyền D12 trên DB đang chạy);
   - chạy thử trên trình duyệt thật.
