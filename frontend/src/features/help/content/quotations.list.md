---
key: quotations.list
title: Danh sách báo giá
routes: [/quotations]
sources: [frontend/src/pages/quotations/quotation-list-page.tsx, frontend/src/pages/quotations/components/status-pill.tsx, frontend/src/pages/quotations/components/list-footer.tsx, frontend/src/pages/quotations/components/quotation-date-filter.tsx, frontend/src/pages/quotations/utils/owner-ids.ts, frontend/src/features/quotations/**, frontend/src/stores/ui-store.ts, backend/src/OrderMgmt.Application/Sales/Quotations/**]
related: [quotations.form, customers.list]
updated: 2026-10-15
---
## Dùng để
Tìm và lọc báo giá, rồi gửi, xác nhận, hủy, nhân bản hoặc in ngay trên từng dòng. Không có quyền xem báo giá của mọi người thì bạn chỉ thấy báo giá của mình.

## Các bước
1. {#search} Gõ số báo giá hoặc tên khách vào ô tìm kiếm, danh sách tự lọc sau giây lát.
2. {#filters} Lọc theo **Trạng thái** và theo **Ngày báo giá**, **N. Doanh thu**, **Ngày Giao** (chọn nhanh hoặc **Tuỳ chỉnh**).
3. {#add} Bấm **Thêm báo giá** để lập báo giá mới.
4. {#edit} Bấm biểu tượng cây bút ở cuối dòng để mở và sửa báo giá.
5. {#actions} Mở nút ba chấm ở cuối dòng, chọn **Gửi**, **Xác nhận**, **Hủy** rồi xác nhận trong hộp thoại, hoặc **Nhân bản** để mở ngay bản sao.
6. {#export} Cũng trong nút ba chấm, chọn Báo giá hoặc Biên bản bàn giao ở nhóm biểu tượng máy in để mở PDF, ở nhóm biểu tượng Excel để tải file Excel.
7. {#footer} Xem dòng tổng ở cuối bảng (**Tiền hàng**, **CK**, **VC**, **Tổng**, **Tạm ứng**) và dùng **Trước**, **Sau** để đổi trang.

## Lưu ý
- Mặc định danh sách ẩn báo giá Đã hủy; chọn thêm trạng thái đó ở **Trạng thái** để xem. Lựa chọn được nhớ trên máy này cho lần sau.
- Dòng tổng cuối bảng cộng cả các báo giá ở trang khác, theo đúng bộ lọc đang chọn.
- Người xem được báo giá của mọi người có thêm ô lọc **Chủ sở hữu** và cột **Chủ sở hữu**; ô này tự chọn chính bạn khi mở trang, trừ tài khoản quản trị.
- **Gửi** chỉ dùng cho báo giá Nháp, **Xác nhận** chỉ dùng cho báo giá Đã gửi; mục không dùng được bị mờ.
- Hủy báo giá đã xác nhận cần quyền riêng và trừ doanh thu của sale. Nút **KT xác nhận đã nhận tiền** (biểu tượng ở cuối dòng) chỉ hiện cho người có quyền kế toán xác nhận.
- Gửi và xác nhận bị chặn khi cấu hình khóa của bạn không cho sửa báo giá ở trạng thái đó, hệ thống báo Không thể gửi hoặc Không thể xác nhận kèm lý do.
- Các nút thêm, sửa, gửi, xác nhận, hủy, nhân bản, in và xuất Excel chỉ hiện khi tài khoản có quyền tương ứng. Cột giá nhập và lợi nhuận chỉ hiện với người có quyền xem giá vốn.
