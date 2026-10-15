---
key: settings-inventory
title: Cấu hình kho
routes: [/settings/inventory]
sources: [frontend/src/pages/settings/inventory-settings-page.tsx, frontend/src/features/inventory-settings/**, backend/src/OrderMgmt.Application/Inventory/Settings/**, backend/src/OrderMgmt.Application/Inventory/Costing/InventoryRecalcService.cs]
related: [settings-recalc-cost, settings-numbering, stock-in.form, stock-out.form]
updated: 2026-10-15
---
## Dùng để
Chọn cách tính giá vốn và các quy tắc khi lập phiếu nhập kho, phiếu xuất kho. Cấu hình áp dụng chung cho mọi chi nhánh.

## Các bước
1. {#costing} Trong khung **Giá vốn**, chọn **Phương pháp tính giá**, **Kỳ tính giá** (**Tháng**, **Quý** hoặc **Năm**) và **Phạm vi tính giá** (**Theo chi nhánh** hoặc **Theo kho**).
2. {#vat} Tích **Giá nhập kho gồm VAT đầu vào** nếu muốn cộng tiền VAT của phiếu nhập vào giá vốn.
3. {#vouchers} Trong khung **Phiếu nhập xuất**, chọn **Chính sách xuất âm** và **Ngày mặc định phiếu mới**, rồi tích hoặc bỏ tích **Số còn lại không cộng VAT**.
4. {#save} Bấm **Lưu**; hệ thống báo Đã lưu cấu hình kho.
5. {#confirm} Nếu bạn đổi kỳ, phạm vi hay cách tính VAT của giá nhập, hộp thoại **Tính lại giá vốn?** hiện ra; bấm **Tiếp tục** để lưu.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- **Phương pháp tính giá** hiện chỉ dùng được **Bình quân cuối kỳ**; **FIFO (chưa hỗ trợ)** bị khóa.
- Đổi **Kỳ tính giá**, **Phạm vi tính giá** hoặc **Giá nhập kho gồm VAT đầu vào** làm hệ thống tính lại giá vốn của mọi chi nhánh từ sau ngày khóa sổ. Khi đổi **Kỳ tính giá** hoặc **Giá nhập kho gồm VAT đầu vào**, chi nhánh nào đã khóa sổ thì ngày khóa phải là ngày cuối kỳ; nếu không hệ thống báo lỗi nêu mã chi nhánh và ngày khóa, và không lưu gì.
- **Chính sách xuất âm** có ba mức: **Cho phép**, **Cảnh báo** hoặc **Chặn** khi phiếu làm tồn kho âm.
- **Ngày mặc định phiếu mới**: **Hôm nay**, hoặc **Theo phiếu trước** (lấy ngày của phiếu cùng loại bạn lập gần nhất ở chi nhánh làm việc).
- Tích **Số còn lại không cộng VAT** thì cột Số còn lại trên dòng phiếu nhập, phiếu xuất không cộng tiền VAT; bỏ tích thì có cộng. Dòng phiếu xuất có giá đã gồm VAT không đổi.
- Nếu máy chủ trả lời chậm, màn hình báo Giá vốn có thể vẫn đang được tính lại; hãy kiểm tra lại sau ít phút.
