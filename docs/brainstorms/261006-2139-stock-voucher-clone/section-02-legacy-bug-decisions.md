# Section 02 — Lỗi phần mềm cũ và quyết định xử lý

Mỗi điểm được duyệt riêng với user trong buổi brainstorm. Các ô ghi *(review)* đã được điều chỉnh sau review — xem [section-05](section-05-review-decisions.md).

| # | Lỗi / bất nhất quán cũ | Quyết định |
|---|---|---|
| 1 | Tổng thanh toán có 3 công thức (lúc nhập / sau lưu / rời ô CK — trừ CKALL 2 lần); NHAP lúc nhập không cộng VAT | **Một công thức cho cả nhập & xuất:** `Tổng TT = ΣSố còn lại + Phí VC`; CK cả đơn phân bổ xuống dòng theo giá trị **trước khi tính VAT** *(review)* |
| 2 | Ô %VAT header phiếu nhập dùng công thức tách VAT, không cập nhật Số còn lại | Chỉ gán %VAT xuống mọi dòng rồi tính lại bằng **công thức dòng chuẩn** |
| 3 | Phiếu thu/chi tự động lấy SUM trước khi lưu dòng (phiếu mới = 0đ), bỏ qua CK đơn/phí VC | Số tiền phiếu thu/chi = **Số tiền TT (đã trả)** của phiếu |
| 4 | Kho dòng khác kho header nhưng tồn/giá vốn/quy đổi tính theo kho header | **Cho nhiều kho trên 1 phiếu; tính theo kho của dòng** |
| 5 | Kiểm xuất âm từng dòng, không cộng dồn; cờ `CHOPHEPXXUAT=1` lại nghĩa là chặn | **Cộng dồn theo (hàng, kho)**, trừ phần của chính phiếu khi sửa; cấu hình 3 mức **Cho phép / Cảnh báo / Chặn**; backend kiểm lại; áp cho **mọi thao tác làm đổi tồn** (lưu nhập/xuất, hủy, khôi phục, xóa, tồn đầu) *(review)* |
| 6 | Số phiếu bị tính lại & ghi đè lúc lưu, không chống trùng, sort kiểu chuỗi | **Backend cấp số trong transaction lúc lưu**, unique theo (loại, chi nhánh); reset theo tháng hoặc năm, kiểm mẫu số khi lưu cấu hình *(review)*; form hiển thị số dự kiến |
| 7 | Hủy không kiểm quyền/khóa sổ; phiếu hủy vẫn sửa được; Xóa bỏ sót thu/chi & quy đổi | Hủy/Khôi phục cần quyền + kiểm khóa sổ; **phiếu hủy không tính tồn/giá vốn/công nợ, chỉ xem**; Xóa = soft-delete kèm phiếu thu/chi tự động |
| 8 | Sao chép không chép header, giá lấy lại theo danh mục, Số tiền bỏ qua CK/VAT | **Chép đủ header (trừ số, ngày = hôm nay) + dòng giữ giá/CK/VAT cũ**, tính lại bằng công thức chuẩn; giá vốn do ledger tính *(review)* |
| 9 | 3 cách chọn hàng (gợi ý / F2-nút / Enter) áp giá-CK-kho khác nhau | **Một quy tắc đầy đủ** cho mọi cách chọn (typeahead như Báo giá) |
| 10 | Nhập Excel nhân đôi dòng khi Sửa; giá vốn = giá nhập kể cả phiếu xuất | **Hỏi Thay thế / Thêm vào**; giữ format 7 cột; tính lại công thức chuẩn; giá vốn theo tồn; báo danh sách mã lỗi |
| 11 | USERNAME không được lưu → quy tắc "chỉ sửa phiếu của mình" hỏng | Lưu owner; **xem mọi phiếu trong chi nhánh làm việc**; sửa/xóa/hủy phiếu mình tạo, phiếu người khác cần `edit_all` *(review)* |
| 12 | Lưu nhiều lệnh rời, không transaction, SQL ghép chuỗi | **Toàn bộ trong 1 transaction** (phiếu, dòng, ledger, giá vốn, giá nhập, phiếu thu/chi); phiếu thu/chi chọn khoản ngay trên form trước khi Lưu |
| 13 | Các tổng không lưu DB | **Backend tính & lưu tổng** trên header |
| 14 | Hạn mức nợ chặn cả phiếu nhập | **Chỉ áp phiếu xuất** |
| 15 | Số còn lại của hàng giá-đã-gồm-VAT = Net − VAT (giá trước thuế) → Tổng TT thấp hơn tiền khách trả | Số còn lại = **số phải trả gồm VAT**: hàng gồm VAT = Net; hàng thường = Net + VAT; cấu hình "không cộng VAT" → Net |

## Tính năng cũ bị bỏ

- Hạn sử dụng / Lô hàng
- Giá bìa sách + cột giá bán lẻ
- Mã vạch (barcode)
- Chuyển/Nhận phiếu qua FTP
- Bảng giá mua/bán theo nhóm, chính sách giá cấp, giá theo lý do (01/XNB)
- Cột STT theo đối tượng/ngày, ảnh QR lưu trên phiếu (thay bằng nút sang màn QR có sẵn — Đợt 3)
- Cấu hình ẩn/hiện cột qua registry máy (thay bằng cấu hình cột của list/grid nếu cần)

## Thay đổi so với cũ (không phải lỗi)

- Quy đổi ĐVT: **bỏ** mã quy đổi; mỗi hàng một đơn vị tồn theo PricingMode (cái / m / m² / m³), dòng phiếu nhập số tấm + kích thước *(review)*.
- Đối tượng: **danh mục đối tượng chung** kiểu MISA — Khách hàng thêm cờ Là KH / Là NCC *(review)*. Người giao/nhận hàng là text tự do.
- %VAT tự điền từ danh mục hàng khi chọn hàng (cũ nhập tay).
- SL > 0 bắt buộc (cũ không kiểm).
- Giá vốn (bình quân cuối kỳ) tự tính lại xuôi chiều khi sửa/xóa/hủy phiếu lùi ngày (cũ không có).
- Phiếu lưu ngày + giờ; SL tồn trên form là tồn tại thời điểm phiếu *(review)*.
