---
key: inventory-opening-stock
title: Tồn đầu kỳ
routes: [/inventory/opening-stock]
sources: [frontend/src/pages/inventory/opening-stock-page.tsx, frontend/src/features/opening-stock/**, backend/src/OrderMgmt.Application/Inventory/OpeningStocks/**, frontend/src/pages/stock-vouchers/components/negative-stock-dialog.tsx, frontend/src/pages/quotations/components/product-typeahead-cell.tsx, frontend/src/lib/use-dialog-opening.ts, frontend/src/features/products/components/product-quick-add-dialog.tsx, frontend/src/features/products/components/product-form-fields.tsx, frontend/src/features/products/split-typed-text.ts]
related: [stock-in.form, stock-out.form]
updated: 2026-10-15
---
## Dùng để
Khai báo hàng đang có trong từng kho tại ngày đầu kỳ: số lượng và giá trị của mỗi mặt hàng. Đây là số dư gốc của tồn kho, sau đó tồn kho thay đổi qua phiếu nhập kho và phiếu xuất kho.

## Các bước
1. {#warehouse} Chọn **Kho** cần khai báo, mỗi kho có danh sách riêng. **Ngày đầu kỳ** hiện bên cạnh.
2. {#add} Bấm **Thêm dòng**, rồi chọn hàng ở cột **Mã hàng**.
3. {#qty} Nhập **Số lượng** và **Giá trị** (tổng giá trị của số hàng đó), cột **Đơn giá** tự tính.
4. {#remove} Bớt hàng thì bấm nút xóa ở cuối dòng.
5. {#save} Bấm **Lưu** để ghi danh sách của kho này.

## Lưu ý
- **Lưu** thay toàn bộ danh sách tồn đầu kỳ của kho: hàng đã xóa khỏi danh sách sẽ mất tồn đầu kỳ.
- Đổi **Kho** khi chưa bấm **Lưu** thì các thay đổi chưa lưu bị bỏ, không hỏi lại.
- Ngày đầu kỳ đặt theo chi nhánh. Chưa có thì trang báo Chi nhánh chưa có ngày đầu kỳ và chưa lưu được, cần người có quyền đặt ở Cấu hình hệ thống, mục Chi nhánh.
- Mỗi hàng một dòng, chỉ chọn hàng có theo dõi tồn kho; nếu không, trang báo Hàng đã có trong danh sách hoặc Hàng không theo dõi tồn kho.
- Mỗi dòng phải có **Số lượng** lớn hơn 0 và **Giá trị** không âm.
- Ngày đầu kỳ nằm trong kỳ đã khóa sổ thì không sửa được, hệ thống báo Ngày tồn đầu kỳ đã khóa sổ.
- Giảm tồn đầu kỳ làm tồn kho âm: tùy cấu hình, hệ thống cho qua, hiện Cảnh báo âm kho để bạn xác nhận, hoặc chặn (Không đủ tồn kho).
- `F4` trong ô **Mã hàng**, hoặc dòng **Thêm mới hàng hóa** ở cuối danh sách gợi ý (gõ mã chưa có rồi Enter là chọn dòng này), mở **Thêm nhanh hàng hóa**: chữ đã gõ điền sẵn vào Mã nếu không có dấu cách và không dấu, nếu không thì vào Tên; Enter đi qua từng ô, `Ctrl Enter` hoặc **Lưu và chọn** lưu hàng mới và điền vào dòng, `Esc` hủy và về ô mã. Cần quyền tạo hàng hóa.
- Lưu hàng mới trong **Thêm nhanh hàng hóa** không được (ví dụ Mã hàng '…' đã tồn tại.) thì lỗi hiện ở đầu hộp thoại và hộp thoại vẫn mở để bạn sửa.
