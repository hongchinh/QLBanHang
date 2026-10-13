# Thiết kế lại form phiếu nhập/xuất kho — bố cục tab, lưới hàng hóa là chính

> Brainstorm: 2026-10-13 06:15:52 · Mockup: [mockup-01-tab-variants.html](mockup-01-tab-variants.html) (3 biến thể tab) · [mockup-02-t2-detail.html](mockup-02-t2-detail.html) (biến thể T2 đã chọn, tab Thanh toán, menu ⚙)

## Problem framing

Form phiếu nhập/xuất kho ([stock-voucher-form-page.tsx](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx)) được xếp theo thứ tự từ trên xuống: card "Thông tin chung" (8 hàng trường), cột phải "Tổng cộng" và "Công nợ khách hàng", cuối cùng mới đến lưới hàng hóa. Trên màn 1920×1080 chỉ **nhìn thấy khoảng 1 dòng hàng**. Trong thực tế một phiếu thường có **15 dòng trở lên**, nên người dùng phải cuộn cả trang liên tục.

Lưới ([stock-line-grid.tsx](../../../frontend/src/pages/stock-vouchers/components/stock-line-grid.tsx)) có 19 cột, rộng tối thiểu 2000px, nên luôn phải cuộn ngang. Phím Enter đi qua **mọi** ô đang bật (Mã → Kho → Tấm → … → %CK → Tiền CK → %VAT → Ghi chú), tức khoảng 10 lần Enter cho mỗi dòng. Trong khi đó người dùng thực tế chỉ gõ Mã, kích thước/SL và Đơn giá.

Enter trong một ô của phần thông tin chung hiện đang **submit form**, tức là lưu phiếu luôn.

## Goals

- Lưới hàng hóa chiếm phần lớn màn hình: khoảng **19 dòng trên 1080p** và **khoảng 11 dòng trên laptop 768p**.
- Các trường quyết định phiếu và các trường đối tượng hay thay đổi (mã, tên, địa chỉ) luôn hiện. Các trường còn lại chuyển vào tab.
- Nhập toàn bằng bàn phím: Enter chỉ dừng ở các ô cần gõ, theo **cách tính của hàng hóa**, và mỗi user cấu hình được.
- Tổng tiền và **dư nợ phải thu/phải trả, kể cả dư nợ sau phiếu** luôn nhìn thấy khi đang nhập hàng.
- Áp dụng chung cho phiếu nhập và phiếu xuất, cả khi tạo mới lẫn khi sửa.

## Non-goals

- Dán nhiều dòng từ Excel và lấy dòng hàng từ báo giá. Hai việc này để **đợt sau**; thanh công cụ lưới chừa sẵn chỗ đặt nút.
- Form phiếu thu/chi và form báo giá. `ProductTypeaheadCell` dùng chung với báo giá chỉ được thêm prop tùy chọn, không đổi hành vi bên báo giá.
- Tối ưu riêng cho mobile. Tab và lưới vẫn dùng được nhưng không thiết kế lại cho màn nhỏ.
- Cấu hình cấp công ty (lưu trên server) cho cột và phím Enter.

## Constraints & assumptions

- Người dùng dùng cả laptop (1366–1536px) lẫn desktop Full HD.
- Người dùng gõ bàn phím liên tục, kiểu Excel.
- Các cột hay gõ: Mã, Tấm/Dài/Rộng/Dày hoặc SL, Đơn giá. Kho, CK, VAT, Ghi chú hầu như giữ mặc định.
- Các phần sau được giữ nguyên hành vi: logic nghiệp vụ hiện có (đổi Lý do thì bỏ đối tượng sai vai trò, đổi Kho thì kéo kho theo các dòng, mặc định Quỹ/Hạn TT/Đã thanh toán, nháp localStorage, xác nhận P10, xung đột phiên bản), Ctrl+S, Insert.
- Cấu hình cá nhân lưu bằng localStorage theo user, giống pin sidebar và độ rộng cột báo giá. Không cần backend cho phần này.
- Repo đã có sẵn các primitive `components/ui/tabs.tsx` và `popover.tsx`.

## Approaches considered

| # | Phương án | Ưu | Nhược | Kết quả |
|---|---|---|---|---|
| A | Header nén 2 hàng luôn hiện + thanh tổng ở đáy | Luôn thấy đủ thông tin | ~17 / ~9 dòng | Loại |
| B | Header gập thành 1 dòng chip tóm tắt | ~20 / ~11 dòng, đủ chiều ngang | Phải mở/gập; trường bị ẩn khi gập | Loại |
| C | Lưới bên trái, thông tin + tổng ở cột phải | ~24 dòng | Lưới mất 1/3 chiều ngang, laptop phải cuộn nhiều | Loại |
| T1 | Tab toàn trang (Thông tin chung / Hàng hóa / Thanh toán …) | Lưới chiếm trọn màn hình | Khi nhập hàng không thấy Lý do/Đối tượng | Loại |
| **T2** | **Vùng chính luôn hiện + tab bên dưới, tab Hàng hóa mặc định** | Thấy trường quyết định phiếu, lưới ~19 dòng, quen với người dùng MISA/Fast | Trường trong tab khác ít thấy hơn → cần chấm đỏ báo lỗi | **Chọn** |
| T3 | Tab chỉ trong khung thông tin, lưới luôn hiện | Lưới không bao giờ bị che | ~18 / ~10 dòng | Loại |

Người dùng loại A/B/C và yêu cầu dạng tab. Trong các biến thể tab, người dùng chọn T2.

## Recommended approach — T2

### 1. Bố cục trang (từ trên xuống)

1. **Thanh tiêu đề dính trên**: giữ nguyên nút quay lại, tiêu đề, số phiếu dự kiến, badge nháp, các nút Lưu/Hủy/Khôi phục/Xóa. Ô "Số phiếu" bỏ khỏi form vì số phiếu đã có trên tiêu đề.
2. **Vùng chính**, 2 hàng, luôn hiện:
   - Hàng 1: **Ngày\*** · **Lý do\*** · **Kho\*** · **HTTT**
   - Hàng 2: **Mã đối tượng** (`PartnerAutocomplete`) · **Tên đối tượng** · **Địa chỉ** (ô rộng nhất)
3. **Các tab** (dùng Radix Tabs; mọi panel `forceMount`, ẩn bằng CSS để react-hook-form và focus-on-error vẫn chạy):

   | Tab | Phím | Nội dung |
   |---|---|---|
   | Hàng hóa (n) | Alt+1 | Lưới. Tab mặc định cho phiếu mới và phiếu cũ |
   | Thông tin khác | Alt+2 | MST · Người nhận/giao hàng · Ghi chú |
   | Thanh toán | Alt+3 | Phí vận chuyển · CK cả đơn · Số tiền thanh toán · Quỹ (khi có) · Hạn thanh toán (khi có, kèm số ngày nợ) · Áp VAT cho tất cả (chỉ phiếu nhập) · Link phiếu thu/chi tự động |
   | Lịch sử | Alt+4 | Chỉ có khi sửa phiếu. Thay card "Lịch sử" ở cuối trang |

   - Tab chứa trường lỗi (lỗi client hoặc lỗi server qua `applyServerErrors`) có **chấm đỏ**.
   - Khi lưu bị lỗi, form chuyển sang tab có lỗi đầu tiên theo thứ tự tab và focus vào ô lỗi.
   - Tab "Công nợ KH" **bỏ**, vì nội dung đã nằm trong dòng dư nợ (mục 5).
4. **Lưới hàng hóa** cao bằng phần còn lại của viewport và cuộn dọc bên trong (chi tiết ở mục 2).
5. **Thanh tổng dính đáy**, chỉ để xem, có 2 dòng:
   - Dòng tổng: số dòng · Tiền hàng · CK · VAT · Phí VC · CK đơn · Đã thanh toán · **Tổng thanh toán**. Bấm vào Phí VC, CK đơn hoặc Đã thanh toán thì chuyển sang tab Thanh toán và focus ô tương ứng.
   - **Dòng dư nợ**: chỉ hiện khi đã chọn đối tượng và user có quyền `debt.partner_summary` hoặc `reports.debt`. Áp dụng cho cả phiếu nhập và phiếu xuất. Nội dung: Mã — Tên đối tượng · **Phải thu** → sau phiếu · **Phải trả** → sau phiếu · Hạn mức · **Còn được nợ sau phiếu** (đỏ khi âm) · Quá hạn (đỏ khi > 0, có link "Xem" nếu có `reports.debt`) · Số ngày nợ.

**Bố cục chiều cao:** trang form là một cột flex cao bằng viewport của vùng nội dung. Khối lưới dùng `flex-1 min-h-[280px] overflow-auto`, thanh tổng nằm cuối cột. Nếu màn hình quá thấp để giữ được `min-h` thì trang quay về cuộn bình thường.

**Phím Enter ở vùng chính:** Enter chuyển sang ô kế tiếp theo thứ tự Ngày → Lý do → Kho → HTTT → Mã ĐT → Tên → Địa chỉ. Enter ở Địa chỉ đưa con trỏ vào ô Mã hàng của **dòng trống đầu tiên** (nếu không có dòng trống thì thêm dòng). Enter **không còn lưu phiếu**; muốn lưu dùng Ctrl+S hoặc nút Lưu. Khi dropdown của `PartnerAutocomplete` đang mở, Enter vẫn dùng để chọn đối tượng (handler kiểm tra `e.defaultPrevented`).

### 2. Lưới hàng hóa

**Cột**

| Loại | Cột |
|---|---|
| Luôn hiện | #, Mã hàng, Tên hàng, ĐVT, Tấm, Dài, Rộng, Dày, SL, Đơn giá, Còn lại, nút xóa |
| Tùy chọn, mặc định **bật** | SL tồn, Số tiền, %CK, %VAT |
| Tùy chọn, mặc định **tắt** | Kho, Tiền CK, Tiền VAT, Ghi chú |

- **Tự hiện cột đang tắt** để không giấu dữ liệu sai:
  - Kho hiện khi có dòng mà `warehouseId` khác kho của phiếu.
  - Mọi cột tùy chọn hiện khi có ít nhất một dòng bị lỗi ở cột đó.
- **Cột dính**: #, Mã, Tên dính trái; Còn lại và nút xóa dính phải (`position: sticky`, có nền và z-index riêng). Tiêu đề cột dính trên trong khung cuộn.
- Bảng tính `minWidth` theo tổng độ rộng các cột đang hiện, thay cho mức cố định 2000px. Ở 1920px không phải cuộn ngang; trên laptop chỉ cuộn nhẹ.
- Ô đang focus tự cuộn vào vùng nhìn thấy mà không bị tiêu đề dính che: dùng `scroll-margin-top` bằng chiều cao `thead`, và `scroll-margin-left` bằng tổng độ rộng các cột dính trái.

**Lộ trình phím Enter theo cách tính** (`pricingMode`)

| Cách tính | Mặc định |
|---|---|
| Cái (`PerUnit`) | Mã → SL → Đơn giá |
| md (`PerLinearMeter`) | Mã → Tấm → Dài → Đơn giá |
| m² (`PerSquareMeter`) | Mã → Tấm → Dài → Rộng → Đơn giá |
| m³ (`PerCubicMeter`) | Mã → Tấm → Dài → Rộng → Dày → Đơn giá |

- **Mã hàng** luôn là điểm dừng đầu tiên và không bỏ được.
- Với mỗi cách tính, user tick thêm hoặc bỏ điểm dừng trong các ô **đang bật với cách tính đó** (theo `isFieldDisabled`): Kho, Tấm, Dài, Rộng, Dày, SL, Đơn giá, %CK, Tiền CK, %VAT, Ghi chú.
- Thứ tự dừng luôn theo thứ tự cột, không cho đổi thứ tự. Điểm dừng nằm ở cột đang ẩn thì bỏ qua.
- Nếu user bỏ hết, lộ trình chỉ còn Mã, và Enter ở Mã chuyển sang dòng sau.
- Chọn hàng từ gợi ý xong, con trỏ nhảy tới **điểm dừng thứ 2 theo cách tính của hàng vừa chọn** (focus sau render, khi `pricingMode` đã được cập nhật). Hiện nay `nextFocusId` cố định là ô Kho.
- Enter ở điểm dừng cuối → ô Mã của dòng sau. Nếu đang ở dòng cuối thì thêm dòng mới, như hiện nay.
- **Enter ở ô Mã còn trống thì không làm gì**, để tránh sinh dòng rỗng. Các dòng trống ở cuối vẫn bị bỏ khi lưu như hiện nay.
- Shift+Enter đi ngược lộ trình. Tại điểm dừng đầu của một dòng, Shift+Enter về điểm dừng cuối của dòng trên, tính theo cách tính của dòng trên.

**Các phím khác**

| Phím | Hành vi |
|---|---|
| Tab / Shift+Tab | Mặc định của trình duyệt, đi qua mọi ô đang hiện và đang bật |
| ↑ / ↓ | Sang cùng cột ở dòng trên/dưới. Nếu ô đó bị tắt theo cách tính của dòng đó thì về ô Mã. Không áp dụng cho ô chọn Kho (`select`) và khi dropdown gợi ý mã hàng đang mở |
| Insert | Thêm dòng (giữ nguyên) |
| Ctrl+Delete | Xóa dòng đang focus. Focus chuyển sang cùng cột của dòng kế tiếp; nếu không có thì sang dòng trước |
| Ctrl+S | Lưu (giữ nguyên) |
| Alt+1…4 | Chuyển tab |

Dòng hướng dẫn phím tắt trên thanh công cụ lưới được cập nhật theo bảng trên.

**Menu "⚙ Cột & phím Enter"** (popover trên thanh công cụ lưới) có 3 phần:
- "Hiện cột": checkbox cho các cột tùy chọn. Cột Kho có ghi chú "tự hiện khi có dòng khác kho phiếu".
- "Phím Enter dừng ở": ma trận 4 cách tính × các ô. Ô không áp dụng cho cách tính đó hiện dấu "–".
- Nút "Khôi phục mặc định".

Mọi thay đổi áp dụng ngay, không có nút Lưu.

**Lưu cấu hình**
- Key localStorage: `qldh.stock-grid-prefs.{userId}.{In|Out}`, tức riêng cho mỗi user và riêng cho Nhập/Xuất.
- Giá trị: `{ version: 1, hiddenColumns: string[], enterStops: Record<PricingMode, LineFocusField[]> }`.
- Mọi lần đọc/ghi đều bọc try/catch. JSON hỏng, sai version, hoặc chứa cột/ô không hợp lệ thì phần đó dùng mặc định.

### 3. Dư nợ sau phiếu

Gọi:
- `effect` = `debtEffect` của lý do đang chọn,
- `net` = Tổng thanh toán − Đã thanh toán (dùng `effectivePaid`, giá trị form đang hiển thị).

| `effect` | Phải thu sau phiếu | Phải trả sau phiếu |
|---|---|---|
| `ReceivableIncrease` | phải thu + net | phải trả |
| `ReceivableDecrease` | phải thu − net | phải trả |
| `PayableIncrease` | phải thu | phải trả + net |
| `PayableDecrease` | phải thu | phải trả − net |
| `None` | không hiện "→ sau phiếu" | không hiện "→ sau phiếu" |

- **Phiếu đang sửa** (trạng thái Active và `voucher.partnerId` trùng đối tượng đang chọn): trước khi áp giá trị mới, trừ phần ảnh hưởng của phiếu đã lưu, tính theo `voucher.reasonDebtEffect`, `voucher.total` và `voucher.paidAmount` với cùng công thức. Lý do: số dư trên server đã gồm phiếu này.
- **Phiếu đã hủy**: chỉ hiện dư nợ hiện tại.
- **Còn được nợ sau phiếu** = Hạn mức − Phải thu sau phiếu. Không có hạn mức thì hiện "Không giới hạn". Giá trị âm hiện màu đỏ, để người dùng thấy cảnh báo vượt hạn mức ngay khi đang nhập. Server vẫn giữ xác nhận P10 khi lưu.
- Logic này là hàm thuần `utils/debt-after-voucher.ts`.

### 4. Backend

- `PartnerDebtSummaryDto` thêm `PayableBalance` (Σ Increase − Σ Decrease phía Payable, mọi ngày, chi nhánh đang làm việc). `DebtService.GetPartnerSummaryAsync` gọi thêm `BalanceAsync(branchId, partnerId, DebtSide.Payable, null, ct)`.
- Endpoint chỉ tìm đối tượng trong `Customers`. Như vậy là đủ, vì nhà cung cấp cũng nằm trong bảng này (cờ `Customer.IsSupplier`). Không cần đổi phần tra cứu.
- Seed: đổi mô tả quyền `debt.partner_summary` thành "Xem công nợ đối tượng trên phiếu nhập/xuất".
- Không có migration schema.

### 5. Cấu trúc code (frontend)

[stock-voucher-form-page.tsx](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.tsx) (891 dòng) chỉ giữ phần điều phối: form, các effect mặc định, action và mapping lỗi. Phần hiển thị tách thành các unit sau:

| Unit | Nhiệm vụ | Phụ thuộc |
|---|---|---|
| `components/stock-voucher-main-fields.tsx` | Vùng chính 2 hàng; Enter chuyển ô; Enter ở ô cuối gọi `onEnterGrid()` | `form`, các danh sách lựa chọn, handler đổi Lý do/Kho/Đối tượng |
| `components/stock-voucher-tabs.tsx` | Khung tab, chấm đỏ theo tập field lỗi, Alt+1…4, API `focusTab(tab)` | Radix Tabs |
| `components/stock-voucher-other-tab.tsx` | MST, Người nhận/giao, Ghi chú | `form` |
| `components/stock-voucher-payment-tab.tsx` | Các ô tiền (lấy `EditableRow` từ `StockTotalsPanel`), Quỹ, Hạn TT, Áp VAT cho tất cả, link phiếu tự động | props giống `StockTotalsPanel` hiện tại |
| `components/stock-totals-bar.tsx` | Dòng tổng (chỉ xem), callback `onJump(field)` | `StockTotals` |
| `components/partner-debt-strip.tsx` | Dòng dư nợ | `usePartnerDebtSummary`, `debt-after-voucher` |
| `utils/debt-after-voucher.ts` | Hàm thuần tính dư nợ sau phiếu | — |
| `features/stock-vouchers/use-stock-grid-prefs.ts` | Đọc/ghi/kiểm tra cấu hình lưới | localStorage, `userId`, `type` |
| `pages/stock-vouchers/utils/line-navigation.ts` | Hàm thuần: `enterPath(mode, stops, visible)`, `nextStop`, `prevStop`, `verticalTarget` | `isFieldDisabled` (chuyển từ grid sang đây) |
| `components/stock-grid-settings-popover.tsx` | Menu ⚙ | `use-stock-grid-prefs` |
| `components/stock-line-cells.tsx` | `NumberCell`, `PercentCell` tách khỏi grid | — |

- `stock-line-grid.tsx` render theo **mảng định nghĩa cột** (key, header, width, sticky, optional, render cell). Colgroup, thead, tbody, phần ẩn/hiện và cột dính đều đọc từ một nguồn này.
- **Xóa** `StockTotalsPanel` và `CustomerDebtPanel` cùng test của chúng; nội dung chuyển sang các unit mới.
- `ProductTypeaheadCell` thêm prop tùy chọn để caller tự quyết định focus sau khi chọn (ví dụ bỏ `nextFocusId` và focus trong `onSelect`). Hành vi bên báo giá giữ nguyên.

## Testing

- **Unit**
  - `line-navigation`: 4 cách tính với mặc định; điểm dừng tùy chỉnh; điểm dừng ở cột ẩn; chỉ còn Mã; Shift+Enter qua ranh giới hai dòng có cách tính khác nhau; ↑/↓ vào ô bị tắt.
  - `use-stock-grid-prefs`: mặc định; JSON hỏng; sai version; field lạ; tách theo user và theo In/Out; localStorage ném lỗi.
  - `debt-after-voucher`: 4 effect và `None`; phiếu đang sửa trừ phần đã lưu; đổi đối tượng khi đang sửa; phiếu đã hủy; không có hạn mức.
- **Component**
  - Lưới: lộ trình Enter theo từng cách tính; focus sau khi chọn hàng; Enter trên Mã trống; ↑/↓; Ctrl+Delete; cột tự hiện khi khác kho hoặc có lỗi; menu ⚙ đổi cột và điểm dừng.
  - Tab: chấm đỏ; tự chuyển tab và focus khi lưu lỗi (cả lỗi server); Alt+n.
  - Vùng chính: Enter không lưu phiếu; Enter ở Địa chỉ vào lưới.
  - Thanh tổng: bấm số thì focus ô trong tab Thanh toán.
  - Dòng dư nợ: ẩn khi không có quyền hoặc không có đối tượng; giá trị âm hiện đỏ.
- **Sửa test cũ**: [stock-voucher-form-page.test.tsx](../../../frontend/src/pages/stock-vouchers/stock-voucher-form-page.test.tsx) (973 dòng) và [stock-line-grid.test.tsx](../../../frontend/src/pages/stock-vouchers/components/stock-line-grid.test.tsx) phải đổi selector và luồng theo bố cục mới. Hành vi nghiệp vụ phải giữ nguyên.
- **Backend**: integration test cho `PayableBalance`, chạy trên DB `qldonhang_integtest`.
- **Chạy app thật**: phiếu nhập và phiếu xuất 15+ dòng với đủ 4 cách tính, ở 1366×768 và 1920×1080.

## Risks

1. **Dropdown gợi ý mã hàng render qua portal.** Khi lưới cuộn bên trong, dropdown sẽ lệch vị trí. Phải cập nhật vị trí khi khung lưới cuộn, hoặc đóng dropdown khi cuộn.
2. **Cột dính + `border-collapse`.** Viền và nền của ô dính dễ bị vỡ. Ô dính cần nền riêng (kể cả khi hover/focus) và z-index cao hơn ô thường; `thead` dính cần z-index cao nhất.
3. **Sửa test nhiều.** Nên tách thành nhiều commit: tái cấu trúc giữ nguyên hành vi trước, đổi bố cục sau.
4. **Chuyển tab khi có lỗi.** Focus vào ô nằm trong tab đang ẩn sẽ không có tác dụng, nên phải chuyển tab trước rồi mới focus.
5. **Bỏ Enter-to-submit** là thay đổi thói quen. Dòng phím tắt và tooltip nút Lưu cần ghi rõ Ctrl+S.

## Open questions

Không còn câu hỏi mở. Các quyết định đã chốt với người dùng:
- Chọn bố cục T2.
- Mã/Tên/Địa chỉ đối tượng nằm ở vùng chính.
- Hiện dư nợ phải thu, phải trả và dư nợ sau phiếu.
- Cột: ẩn bớt + cột dính trái/phải.
- Enter theo cách tính và cấu hình được.
- Cấu hình theo từng user.
- Dán từ Excel và lấy từ báo giá để đợt sau.

## Next steps

1. `write-plan` từ tài liệu này. Gợi ý chia phase:
   - (1) backend `PayableBalance`;
   - (2) tách component, giữ nguyên hành vi;
   - (3) bố cục T2: vùng chính, tab, thanh tổng, dòng dư nợ;
   - (4) lưới: định nghĩa cột, cột dính, chiều cao;
   - (5) điều hướng phím + cấu hình user + menu ⚙.
2. Brainstorm riêng cho "Dán từ Excel / Lấy dòng từ báo giá".
