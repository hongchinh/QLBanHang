---
key: settings-recalc-cost
title: Tính lại giá vốn
routes: [/settings/recalc-cost]
sources: [frontend/src/pages/settings/recalc-cost-page.tsx, frontend/src/features/inventory-settings/**, frontend/src/features/warehouses/**, backend/src/OrderMgmt.Application/Inventory/Costing/InventoryRecalcService.cs]
related: [settings-inventory]
updated: 2026-10-16
---
## Dùng để
Tính lại giá vốn bình quân từ một kỳ đã chọn đến nay cho chi nhánh làm việc. Hệ thống vẫn tự tính lại khi chứng từ thay đổi; trang này dùng khi cần chạy lại bằng tay.

## Các bước
1. {#period} Chọn **Từ kỳ**: kỳ đầu tiên cần tính lại. Kỳ hiện tại được chọn sẵn.
2. {#warehouse} Chọn **Kho**, hoặc để **Tất cả kho**.
3. {#product} Gõ mã hoặc tên vào **Hàng hóa** và chọn trong danh sách gợi ý nếu chỉ cần tính một mặt hàng; để trống là **Tất cả hàng hóa**.
4. {#run} Bấm **Tính lại giá vốn**, rồi bấm **Tính lại** trong hộp thoại xác nhận.

## Lưu ý
- Nút ← quay lại Cấu hình nằm ở đầu trang, trước tiêu đề.
- Danh sách **Từ kỳ** có 24 kỳ gần nhất, kỳ mới nhất ở trên, và đơn vị kỳ (**Tháng**, **Quý**, **Năm**) theo **Kỳ tính giá** trong mục Cấu hình kho.
- Ô **Hàng hóa** chỉ hiện với người được xem danh mục hàng hóa. Chọn rồi, bấm nút dấu × bên cạnh tên hàng để quay về tất cả hàng hóa.
- Nút **Tính lại giá vốn** khóa cho đến khi cấu hình kho tải xong.
- Kỳ đã khóa sổ hết (ngày khóa từ ngày cuối kỳ trở đi) thì không tính lại được; hệ thống báo Kỳ tính giá đã khóa sổ, kèm ngày khóa. Kỳ chỉ khóa một phần vẫn được tính lại.
- Khi phạm vi tính giá là Theo chi nhánh, chọn một kho sẽ tính lại các mặt hàng có phát sinh ở kho đó cho cả chi nhánh.
- Xong thì hệ thống báo số phạm vi hàng hóa đã tính lại. Nếu máy chủ trả lời chậm, màn hình báo Giá vốn có thể vẫn đang được tính lại; hãy kiểm tra lại sau ít phút.
