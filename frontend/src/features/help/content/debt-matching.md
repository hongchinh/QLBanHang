---
key: debt-matching
title: Đối trừ chứng từ
routes: [/debt/matching]
sources: [frontend/src/pages/debt/debt-matching-page.tsx, frontend/src/pages/debt/components/**, frontend/src/features/debt/**, frontend/src/features/cash-common/labels.ts, backend/src/OrderMgmt.Application/CashDebt/Debt/**]
related: [receipts.form, payments.form]
updated: 2026-10-15
---
## Dùng để
Trừ các chứng từ giảm nợ (như phiếu thu, phiếu chi) với các chứng từ ghi nợ còn nợ của cùng một đối tượng, để biết chứng từ nào đã được thanh toán. Đối trừ không đổi số dư công nợ, chỉ đổi số còn lại của từng chứng từ.

## Các bước
1. {#side} Chọn phía **Phải thu** (khách hàng) hoặc **Phải trả** (nhà cung cấp).
2. {#partner} Chọn **Đối tượng** cần đối trừ; đổi phía thì phải chọn lại đối tượng.
3. {#amounts} Ở tab **Chưa đối trừ**, nhập cột **Đối trừ** của chứng từ giảm nợ và chứng từ ghi nợ, mỗi dòng không quá **Còn lại**.
4. {#suggest} Hoặc bấm **Gợi ý** để điền sẵn theo hạn thanh toán sớm nhất trước, rồi sửa nếu cần; **Xóa** để nhập lại.
5. {#apply} Khi hai tổng bằng nhau, bấm **Đối trừ**.
6. {#undo} Muốn bỏ một lần đối trừ, mở tab **Đã đối trừ** rồi bấm **Bỏ** hoặc **Bỏ cả lô**.

## Lưu ý
- Nút **Đối trừ** chỉ bấm được khi tổng hai bên bằng nhau và lớn hơn 0; nếu lệch, màn hình báo Hai bên chưa bằng nhau.
- Số nhập vào một dòng không được vượt số còn lại: màn hình báo Vượt số còn lại.
- Đổi phía hoặc đổi đối tượng thì các số đã nhập bị xóa; chuyển qua lại giữa hai tab thì không mất.
- Đối tượng đã ngừng sử dụng vẫn chọn được, vì có thể vẫn còn công nợ cần đối trừ.
- Chỉ bỏ được đối trừ có nhãn Thủ công hoặc Lúc lập phiếu. Đối trừ Tự động và Bù trừ ghi Do chứng từ quản lý: chúng thay đổi theo phiếu gốc (phiếu xuất/nhập kho hoặc phiếu bù trừ).
- Bỏ đối trừ thì hệ thống hỏi xác nhận trước khi xóa.
- Kỳ đã khóa sổ thì không đối trừ hoặc bỏ đối trừ được; màn hình báo Ngày chứng từ đã khóa sổ.
