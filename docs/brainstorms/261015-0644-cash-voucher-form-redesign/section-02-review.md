# Review tài liệu brainstorm — phát hiện và cách xử lý

> Review: 2026-10-15 07:27:02. Có ba nguồn: một reviewer đọc mới (chưa tham gia brainstorm), một lượt đối chiếu từng khẳng định với code, và một lượt tự rà luồng thao tác. Các phát hiện quan trọng đã được kiểm chứng lại trong code. Ba quyết định của người dùng (D11–D13) nằm trong bảng Decisions của [SUMMARY.md](SUMMARY.md). Mọi sửa đổi đã được đưa vào SUMMARY.md và [section-01](section-01-behaviour-spec.md).

## Mức cao

| # | Phát hiện | Bằng chứng | Cách xử lý |
|---|---|---|---|
| H1 | **Lưu nhầm phiếu qua hộp thêm nhanh.** Nếu ô dùng chung mở hộp từ bên trong `<form>` của phiếu, submit của `CustomerFormFields` (có `<form onSubmit>` riêng) sẽ lan qua portal ra `onSubmit` của phiếu. Enter và Ctrl+S trong hộp cũng lan ra bộ điều hướng của form phiếu | `customer-form-fields.tsx:132`; cash `onSubmit` ở `cash-voucher-form-page.tsx:532`, stock ở `:780`. Báo giá tránh được vì đặt hộp sau `</form>` (`quotation-form-page.tsx:934-936`) | section-01 §6.6: hộp không được để submit và keydown lan ra form phiếu; handler phím của form phiếu bỏ qua sự kiện nằm trong `DIALOG_SELECTOR`. Có test bắt buộc |
| H2 | **Enter khi kết quả tìm chưa về**: gõ nhanh `NCC001⏎` mở hộp thêm nhanh (có thể tạo trùng) hoặc chọn nhầm dòng của từ khóa cũ (`placeholderData`) | debounce 250ms (`partner-autocomplete.tsx`), `placeholderData` của hai hook tìm | section-01 §5.3: Enter "chờ" kết quả của đúng từ khóa. Dòng Thêm mới chỉ được chọn sẵn sau khi đã tìm xong và có 0 dòng. Enter chờ mà ra 0 dòng thì chỉ hiện danh sách, không tự mở hộp |
| H3 | **Mâu thuẫn với [BD 4.2 Mã đối tượng](../../bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md)**: focus sau khi chọn, dòng Thêm mới trong danh sách, nút + Tab tới được, mẫu Lý do nộp, chỉ tìm khách hàng, gõ chữ mà không chọn | BD §4, §5, §8–§10, §14, §15.2, AC-OBJ-021/023/024 | D11: focus sang Tên đối tượng. D13: giữ D2/D4/D9 và dòng Thêm mới + F4; lấy thêm từ BD: nút + Tab tới được, chặn lưu khi có chữ mà chưa chọn. Đã thêm ghi chú vào đầu BD 4.2 |
| H4 | **Danh mục lỗi quyền**: `/customers` và `/suppliers` cần `*.view`. Role Kế toán không có `suppliers.view`, nên phiếu chi mở danh mục NCC bị 403 | `DbSeeder.cs:204-226`; danh mục dùng `useCustomers` | section-01 §7: dòng danh mục và nút chuyển KH/NCC chỉ hiện với vai trò người dùng xem được. D12 cũng cấp thêm `suppliers.view` cho Kế toán |
| H5 | **Thêm nhanh bị ẩn với người dùng chính**: Kế toán và Kho không có `customers.create`/`suppliers.create` | `DbSeeder.cs:204-250` | D12: thêm quyền vào seed. DB đang chạy thì admin bật trong ma trận quyền: seeder chỉ tự cấp các mã quyền mới, không cấp mã đã có (`DbSeeder.cs:268-271`) |

## Mức trung bình

| # | Phát hiện | Cách xử lý |
|---|---|---|
| M1 | Phần tự điền chạy khi mở phiếu đã lưu thì form thành dirty: nút In bị khóa, form ngừng reset khi tải lại nền, và nút QR của brainstorm 261015-0656 cũng bị khóa | section-01 §3.2: phiếu đã lưu không ghi gì khi mở hoặc khi `reset`/`resetTo`, chỉ đặt `prevAuto`/`prevDesc`. Phiếu mới điền lần đầu mà không đánh dấu dirty |
| M2 | Phiếu kho không có chỗ hiện lỗi `partnerId` (`server-field-errors.ts:4` không có khóa này). Từ D9, lưu phiếu kho cũ chỉ ra toast chung | section-01 §4.4: thêm `partnerId` vào bảng lỗi đầu phiếu kho, hiện dưới hàng Đối tượng; dòng vàng/đỏ của lý do nằm dưới hàng 1 |
| M3 | Hộp thêm nhanh hiểu theo hai cách: `CustomerFormFields` khóa vai trò chính; có textarea, Radix Select và `<form>` riêng; chưa có prop điền sẵn tên | section-01 §6: hai ô vai trò tự do, ít nhất một ô; chọn API khi lưu; thứ tự Enter cụ thể; textarea và Select theo quy tắc riêng; điền sẵn tên và focus |
| M4 | Bỏ `requireReason` ở giai đoạn 2 làm hỏng phiếu thu chi và phiếu kho (đang dùng chế độ đó) cho tới giai đoạn 3–4 | SUMMARY Next steps: giai đoạn 2 chuyển luôn hook tìm của hai form sang `activeOnly`, rồi mới bỏ `requireReason` |
| M5 | `PartnerAutocomplete` có **5** chỗ dùng, không phải 6. Bộ lọc DS phiếu thu chi là `CashPartnerFilter` riêng (`<ul>` mã + tên) | Sửa số liệu. `CashPartnerFilter` để ngoài phạm vi (non-goal) |
| M6 | Thêm nhanh xong không làm mới kết quả tìm của phiếu thu chi (`invalidatePartnerQueries` chỉ làm mới phiếu kho, `customers/hooks.ts:13-22`); `sameRoleFilter` chỉ so `type` và `reasonId` | section-01 §8: thêm khóa partners của phiếu thu chi vào `invalidatePartnerQueries`; `sameRoleFilter` so cả `activeOnly` |
| M7 | Tự đổi lý do ở phiếu kho còn đổi việc hiện Hạn thanh toán và điền sẵn số ngày nợ | section-01 §4.2: ghi là hệ quả được chấp nhận; có hiệu ứng nháy vàng báo cho người dùng |

## Mức thấp

| # | Phát hiện | Cách xử lý |
|---|---|---|
| L1 | `MoneyCell` lưu theo từng phím (`money-cell.tsx:35-38`), không phải khi mất focus; khi focus thì đã tự bôi đen | Sửa chữ ở §2.2 |
| L2 | "Lý do có vai trò cụ thể" có hai cách hiểu; lý do trống bị mockup coi là hợp | §4.2: định nghĩa qua `requiredRole`; lý do trống tính là không hợp |
| L3 | Báo giá: chỉ `customers.create` mới tính; báo giá đã có điều hướng Enter (`GENERAL_INFO_FIELD_ORDER`, `quotation-form-page.tsx:356-386`); mockup-02 tự focus Mã KH | §1.3/§2.4: giữ id `customerId`/`customerName` và điều hướng sẵn có; không đổi autofocus của báo giá |
| L4 | Màn phiếu kho trong mockup-02 (tìm theo lý do, bỏ đối tượng khi đổi lý do) đã bị D9 thay | Ghi ở đầu SUMMARY |
| L5 | Dòng đầu "… đang dùng" sai ở chế độ bộ lọc; N tối đa là 20 | §5.2: câu chữ theo chế độ; báo khi đạt giới hạn |
| L6 | `request.PartnerTaxCode ?? partner?.TaxCode` cũng chạy khi cập nhật: sửa phiếu cũ thì MST bị ghi đè; phiếu có đối tượng gõ tay không còn MST | §3.4: ghi rõ hệ quả |
| L7 | Esc khỏi hộp thêm nhanh: Dialog mặc định trả focus về phần tử đã mở hộp | §6.5: giữ chữ đã gõ, đưa focus về ô mã một cách tường minh |
| L8 | Header phiếu thu chi gộp lỗi `reasonId ?? partnerId` vào một ô; danh sách lý do truyền vào header chỉ có `{id, name}` | §5.4 / SUMMARY: tách ô lỗi; truyền đủ `partnerType`/`debtEffect` |
| L9 | Spec ghi nút xóa dòng có `tabIndex=-1`, nhưng hiện nay nút này Tab tới được | Bỏ yêu cầu đó, giữ như hiện nay |
| L10 | Danh sách test và tài liệu cần cập nhật còn thiếu | §10: thêm danh sách cụ thể |
| L11 | Lớp CSS mới của phiếu kho phải có ở cả hai mốc media (1024–1279px và dưới 1024px); ở bố cục hai cột, ô tên không được rơi vào cột nhãn | §1.2 |

## Đã xác nhận đúng

- Các id ô của header phiếu thu chi.
- Enter hiện không làm gì trên form phiếu thu chi (không có nút submit).
- `CashVoucherService.cs:684` / `StockVoucherService.cs:925` lấy MST từ danh mục.
- Mẫu in không có dòng MST.
- `ValidatePartner` của **cả hai** service đều kiểm vai trò theo phía công nợ, nên bảng `requiredRole` dùng được cho cả phiếu kho. Riêng `SearchPartnersAsync` của phiếu kho không đổi `Any` theo phía công nợ, nhưng điều này không còn quan trọng vì form tìm mọi vai trò.
- Không có phím F4 nào bị trùng.
- Lý do được sắp theo loại rồi theo mã. Với lý do hệ thống, mỗi loại phiếu chỉ có một lý do cho mỗi vai trò.
- Nháp phiếu kho có lưu cờ vai trò.

## Đề xuất chưa áp dụng

- Enter ở ô cuối của dòng cuối chỉ thêm dòng khi dòng đó đã có số tiền, để tránh dòng rỗng. Đề xuất này đổi hành vi đã duyệt (D5), nên chỉ làm khi người dùng đồng ý. Hiện dòng rỗng được bỏ khi lưu (§3.4).
- Tách giai đoạn 2 (ô dùng chung, đụng 5 chỗ dùng) thành một PR riêng.
