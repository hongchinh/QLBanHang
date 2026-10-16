---
key: admin-dashboard
title: Tổng quan hệ thống
routes: [/admin/dashboard]
sources: [frontend/src/pages/admin/admin-dashboard-page.tsx, frontend/src/features/dashboard/**, frontend/src/hooks/use-dashboard-signal.ts, backend/src/OrderMgmt.Application/Sales/Quotations/Services/DashboardService.cs, backend/src/OrderMgmt.Application/Dashboards/Common/SalesRevenueSource.cs]
related: [dashboard-executive, home]
updated: 2026-10-16
---
## Dùng để
Xem doanh thu bán hàng của chi nhánh đang làm việc, so sánh các nhân viên và theo dõi báo giá trong một kỳ. Số liệu tự cập nhật khi có chứng từ mới.

## Các bước
1. {#branch} Chọn chi nhánh làm việc ở đầu trang; dòng dưới tiêu đề cho biết doanh thu của chi nhánh nào và kỳ nào.
2. {#period} Chọn kỳ: **Hôm nay**, **7N**, **30N**, **Tháng này**, **Tháng trước**, hoặc **Tuỳ chỉnh** rồi nhập **Từ ngày**, **Đến ngày** và bấm **Áp dụng**.
3. {#sale} Chọn một nhân viên ở ô **Tất cả sale** để chỉ xem phiếu do người đó phụ trách.
4. {#kpi} Xem **Doanh thu hôm nay**, **Doanh thu khoảng**, **Tổng báo giá** và **Đã huỷ** ở hàng thẻ trên cùng.
5. {#chart} Xem biểu đồ **Doanh thu**; bấm **Ngày**, **Tuần** hoặc **Tháng** để đổi cách gộp.
6. {#rank} Xem **Bảng xếp hạng sale**, **Top khách hàng** và **Top sản phẩm**; bấm một dòng xếp hạng để mở báo cáo Doanh thu của người đó.

## Lưu ý
- Doanh thu là phiếu xuất bán trừ hàng bán trả lại, trước VAT, không gồm cước, tính theo ngày chứng từ của chi nhánh đang chọn ở đầu trang. Đổi chi nhánh thì số liệu đổi theo.
- Ô **Tất cả sale** lọc theo Nhân viên phụ trách của phiếu. **Bảng xếp hạng sale** hiện số phiếu và gộp theo Nhân viên phụ trách của phiếu, nên có thể gồm cả người không làm sale (ví dụ thủ kho lập phiếu xuất bán); bảng không đổi theo ô **Tất cả sale**.
- **Tổng báo giá**, **Đã huỷ** và **Phễu trạng thái** không theo chi nhánh: chúng tính theo báo giá của mọi người và đổi theo ô **Tất cả sale**.
- Nếu bạn có quyền, mở màn hình **Điều hành** để xem doanh thu toàn công ty.
- Mở trang mà chưa chọn kỳ thì xem từ đầu tháng đến hôm nay. Phần trăm trên thẻ so với kỳ liền trước có cùng số ngày, và hiện — khi kỳ trước bằng 0; **Doanh thu hôm nay** không so sánh nên luôn hiện —. **Tổng báo giá** đếm theo ngày báo giá, **Đã huỷ** theo ngày hủy.
- Báo cáo Doanh thu vẫn tính theo báo giá đã chốt, nên số có thể khác màn hình này.
- **Trực tiếp** ở đầu trang nghĩa là số liệu cập nhật ngay khi có chứng từ hoặc báo giá mới; khi **Mất kết nối**, số liệu chỉ được làm mới mỗi phút, giờ bên cạnh là lần cập nhật cuối.

