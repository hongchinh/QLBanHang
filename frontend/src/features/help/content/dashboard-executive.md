---
key: dashboard-executive
title: Điều hành
routes: [/dashboard/executive]
sources: [frontend/src/pages/executive-dashboard/**, frontend/src/features/executive-dashboard/**, backend/src/OrderMgmt.Application/Dashboards/Executive/**]
related: []
updated: 2026-10-15
---
## Dùng để
Xem nhanh doanh thu, tiền, công nợ và tồn kho của cả công ty hoặc một chi nhánh trong một kỳ, so với kỳ trước có cùng độ dài. Số liệu tự cập nhật khi có chứng từ mới.

## Các bước
1. {#scope} Nếu có quyền xem mọi chi nhánh, chọn **Toàn công ty** hoặc một chi nhánh ở ô phạm vi số liệu.
2. {#period} Chọn kỳ: **Hôm nay**, **Tuần này**, **Tháng này**, **Năm nay** hoặc **Tuỳ chỉnh** để nhập **Từ ngày** và **Đến ngày**.
3. {#tiles} Ở tab **Tổng quan**, xem các ô **Trong kỳ** (doanh thu, lãi gộp, thu − chi) và **Hiện tại** (tồn quỹ, phải thu, phải trả, tồn kho).
4. {#attention} Xem cột **Cần chú ý** (nợ quá hạn, vượt hạn mức, tồn âm, quỹ âm...) và **Vừa phát sinh**; bấm dòng có số để mở báo cáo.
5. {#tabs} Mở tab **Kinh doanh**, **Tiền**, **Công nợ** hoặc **Kho** để xem chi tiết.

## Lưu ý
- Mặc định là kỳ Tháng này; dòng So với kỳ trước dưới tiêu đề cho biết kỳ dùng để so sánh. Kỳ chọn dài tối đa 3 năm.
- Tab nào hiện còn tùy quyền xem báo cáo của bạn; giá vốn, lãi gộp và giá trị tồn cần thêm quyền xem giá vốn. Không có quyền xem mọi chi nhánh thì bạn chỉ thấy chi nhánh của mình.
- Chọn một chi nhánh ở đây cũng đổi chi nhánh làm việc của bạn, nên các màn hình khác cũng chuyển theo.
- **Doanh thu xuất bán** tính từ phiếu xuất kho (xuất bán trừ khách trả hàng), trước VAT, không gồm phí vận chuyển; khác báo cáo Doanh thu tính theo báo giá đã chốt.
- **Trực tiếp** ở đầu trang nghĩa là số liệu đang tự cập nhật; **Mất kết nối** thì số liệu dừng ở lần cập nhật cuối.
- Khi không tải được danh sách chi nhánh, màn hình báo chưa xác định được phạm vi số liệu; bấm **Thử lại**.
