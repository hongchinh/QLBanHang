---
key: home
title: Tổng quan của tôi
routes: [/]
sources: [frontend/src/pages/dashboard-page.tsx, frontend/src/features/dashboard/**, frontend/src/hooks/use-dashboard-signal.ts, backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs, backend/src/OrderMgmt.Application/Dashboards/Common/SalesRevenueSource.cs]
related: [dashboard-executive]
updated: 2026-10-16
---
## Dùng để
Xem nhanh doanh thu bán hàng của chi nhánh đang làm việc và tình hình báo giá trong một kỳ. Số liệu tự cập nhật khi có chứng từ mới.

## Các bước
1. {#branch} Chọn chi nhánh làm việc ở đầu trang; dòng dưới tiêu đề cho biết doanh thu của chi nhánh nào và kỳ nào.
2. {#period} Chọn kỳ: **Hôm nay**, **7N**, **30N**, **Tháng này**, **Tháng trước**, hoặc **Tuỳ chỉnh** rồi nhập **Từ ngày**, **Đến ngày** và bấm **Áp dụng**.
3. {#kpi} Xem **Doanh thu hôm nay**, **Doanh thu khoảng**, **Tổng báo giá** và **Đã huỷ** ở hàng thẻ trên cùng.
4. {#chart} Xem biểu đồ **Doanh thu**; bấm **Ngày**, **Tuần** hoặc **Tháng** để đổi cách gộp.
5. {#lists} Xem **Top khách hàng** và **Top sản phẩm** của kỳ đã chọn.
6. {#funnel} Xem **Phễu trạng thái** và **Hoạt động gần đây** để biết các báo giá đang ở bước nào.

## Lưu ý
- Doanh thu là phiếu xuất bán trừ hàng bán trả lại, trước VAT, không gồm cước, tính theo ngày chứng từ của chi nhánh đang chọn ở đầu trang. Đổi chi nhánh thì số liệu đổi theo.
- Không có quyền xem báo giá của mọi người thì bạn chỉ thấy phiếu mình phụ trách, và dòng dưới tiêu đề ghi "phiếu của tôi".
- **Tổng báo giá**, **Đã huỷ**, **Phễu trạng thái** và **Hoạt động gần đây** không theo chi nhánh: chúng tính theo báo giá của bạn, hoặc của mọi người nếu bạn có quyền xem báo giá của mọi người.
- Nếu bạn có quyền, mở màn hình **Điều hành** để xem doanh thu toàn công ty.
- Báo cáo Doanh thu vẫn tính theo báo giá đã chốt, nên số có thể khác màn hình này.
- **Trực tiếp** ở đầu trang nghĩa là số liệu cập nhật ngay khi có chứng từ hoặc báo giá mới; khi **Mất kết nối**, số liệu chỉ được làm mới mỗi phút, giờ bên cạnh là lần cập nhật cuối.

