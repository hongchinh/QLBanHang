# Góp ý cho bộ icon Notch v1.0

> Gửi tác giả canvas "Notch — Bộ icon phần mềm quản lý". Đo trên bản 1791966097-e8e5 của canvas, ngày 2026-10-14.

Cảm ơn bạn đã chia sẻ bộ icon. Ý tưởng một khối nhấn duy nhất rõ ràng và dễ nhận ra. Chúng tôi đã thử đặt Notch vào QLBanHang (trang chủ, form Phiếu xuất kho, sidebar) và đo từng icon. Dưới đây là những điểm cần sửa hoặc bổ sung trước khi chúng tôi có thể dùng bộ icon trong sản phẩm, xếp theo mức ưu tiên.

## 1. Định nghĩa lại khe hở giữa nét và khối

Quy tắc 02 ("nét không chạm khối, luôn chừa khe 1px") hiện được đo từ **đường tâm** nét. Nhưng nét dày 1,75–2 đơn vị, nên riêng nửa độ dày nét đã chiếm 0,875–1 đơn vị. Kết quả là khe nhìn thấy gần như bằng 0. Ở 16px, cỡ mà phần mềm quản lý dùng nhiều nhất, có **18/40 icon** với khe nhìn thấy dưới 0,5px; khi đó khối dính vào nét và "nhịp cắt" của bộ icon biến mất.

**Đề nghị:** đo khe từ **mép** nét, ở mọi cỡ trong bảng. Cụ thể: khoảng cách từ đường tâm tới khối ≥ nửa độ dày nét + 1 đơn vị (tức ≥ 2 ở cỡ 16px, nét 2).

## 2. Các icon có khối chạm hoặc đè lên nét

Khe tính từ mép nét, đơn vị px. Số âm nghĩa là nét đè lên khối.

| Icon | 16px | 20px | 24px | Vấn đề |
|---|---:|---:|---:|---|
| link | −2,00 | −2,44 | −2,88 | Khối nằm trên đoạn nối chéo |
| wallet | −1,33 | −1,60 | −1,88 | Khối nằm trên cạnh phải (x = 20) |
| hr | −0,67 | −0,77 | −0,88 | Hai đoạn ngang kết thúc đúng mép khối |
| clock | −0,67 | −0,77 | −0,87 | Kim giờ và kim phút chạm khối |
| upload (và stockin, stockout) | −0,33 | −0,35 | −0,38 | Mũi tên chạm khối |
| database | −0,20 | −0,19 | −0,18 | Đường elip giữa chạm khối |
| table | −0,17 | −0,15 | −0,12 | Ô được tô sát đường lưới |
| plus | 0,00 | 0,06 | 0,12 | Khối ở tâm; ở 16px nhìn như dấu cộng tô đặc |
| settings | 0,00 | 0,06 | 0,13 | Thanh trượt chạm khối |
| trend | 0,00 | 0,06 | 0,13 | Đầu đường xu hướng chạm khối |
| close | 0,08 | 0,16 | 0,25 | Khối ở tâm, như plus |
| document, contract | 0,16 | 0,26 | 0,36 | Góc gấp sát khối |
| message, mail, trash | 0,33 | 0,48 | 0,62 | |
| help | 0,45 | 0,62 | 0,80 | |
| customer | 0,46 | 0,64 | 0,82 | |

Ảnh render thật ở 16px, phóng to 5 lần (gửi kèm file `notch-16px-zoom.png`). Thứ tự: link, wallet, hr, clock, upload, database, table, plus, settings, trend, close, document, trash; dashboard, reports, cart là ba icon còn tách rõ.

## 3. Vùng an toàn

- Khối của **bell** (y = 1–5) và **trash** (y = 1,5–5,5) nằm ngoài vùng an toàn 2px.
- Nếu muốn giữ vị trí này (giống huy hiệu thông báo, nắp thùng rác), xin ghi đây là ngoại lệ trong style guide.

## 4. Quy tắc góc vuông

**wallet** có góc bo r = 2, trái với quy tắc "khung chữ nhật không bo; chỉ đầu nét bo tròn".

## 5. Khối mang nghĩa gì: chọn một nghĩa

- Artboard "Ứng dụng trong giao diện" nói khối là **trạng thái**: chỉ sáng Signal ở mục đang mở, các mục khác khối xám.
- Hai màn đề xuất (Tổng quan, Thêm phiếu nhập kho) lại tô Signal cho mọi icon: ô KPI, thao tác nhanh, ô tìm kiếm, nút chính. Màn Tổng quan có hơn 10 khối Signal cùng lúc, trái với ý "mắt dừng lại ở khối đó".

Xin chọn một nghĩa và ghi vào style guide. Chúng tôi nghiêng về nghĩa **trạng thái**, khối mặc định để xám.

## 6. Màu

| Cặp màu | Tỷ lệ tương phản |
|---|---:|
| Signal trên Paper `#F3F2EE` | 2,78:1 (dưới mức 3:1 của WCAG 1.4.11) |
| Signal trên trắng | 3,12:1 |
| Mute trên trắng | 2,69:1 |
| Đang chọn (Signal) và mặc định (Mute) | 1,16:1; người mù màu lục: 1,02:1 |
| Đang chọn (Signal) và cảnh báo (`#D92D20`) | 1,55:1; người mù màu đỏ hoặc lục thấy gần như cùng màu |

Đề nghị:
- Chọn màu nhấn đạt ≥ 3:1 trên mọi nền mà khối xuất hiện.
- Ghi vào style guide: trạng thái không được báo chỉ bằng sắc màu của khối. Đang chọn cần thêm nền, vạch hoặc chữ đậm; cảnh báo cần thêm chữ hoặc huy hiệu.
- Lưu ý thêm: khối Signal gần như biến mất trên nền có màu đậm (ô màu, header xanh), chỉ đạt 1,0–2,4:1. Nên quy định khối hiển thị thế nào trên nền màu: ẩn đi, hoặc đổi sang Paper.

## 7. Tên gọi

Đề nghị đặt tên theo **hình vẽ**, để ứng dụng tự map sang nghĩa nghiệp vụ:

| Tên hiện tại | Hình vẽ thực tế | Gợi ý tên |
|---|---|---|
| settings | thanh trượt | `sliders` (và vẽ thêm bánh răng `settings` riêng) |
| warehouse | hình hộp | `package` (và vẽ thêm `warehouse` có mái nhà kho) |
| upload, dùng lại cho "Xuất kho" | mũi tên lên khỏi khay | giữ `upload`; đổi `stockin`/`stockout` thành `tray-in`/`tray-out` |
| hr | cặp tài liệu | `briefcase` |
| folder, nhãn "Dự án" | thư mục | giữ `folder`, bỏ nhãn "Dự án" khỏi tên (app dùng cho "Danh mục") |

## 8. Icon còn thiếu cho QLBanHang

### Ưu tiên 1: thao tác dùng nhiều (8)

Lưu (save), Hủy phiếu (ban/void), Xuất Excel (file-spreadsheet), In (printer), Khôi phục (rotate-ccw), Cảnh báo (alert-triangle), Sao chép/Nhân bản (copy), Gửi (send).

### Ưu tiên 2: menu bên trái (35)

Sidebar cần 48 icon khác nhau; nhóm và mục không được dùng chung icon.

- **Còn thiếu (20):**
  - QR thanh toán, Phiếu chi (tờ tiền), Chuyển quỹ (hai mũi tên ngược chiều), Bù trừ công nợ (gộp), Hạn mức (đồng hồ đo).
  - Doanh thu theo NV (huy hiệu tiền), Doanh thu theo xe, Hiệu suất (bia ngắm).
  - Thẻ kho (cuộn giấy), Nhập–xuất–tồn (Σ), Sổ quỹ (heo đất), Số dư công nợ (sổ mở), Sổ chi tiết công nợ (sổ có chữ), Công nợ theo chứng từ (nhiều tờ), Tuổi nợ (đồng hồ cát), Biên bản đối chiếu (tờ có dấu tick).
  - Nhóm hàng (nhãn), Nhà cung cấp (xe tải), Quỹ (ngân hàng), Lý do thu chi (danh sách đánh số).
- **Đang phải dùng tạm (15):**
  - Công nợ (cái cân), Danh mục (thư viện), Nhập kho (khay vào), Tồn đầu kỳ (bảng kẹp), Phiếu thu (tay cầm tiền), Đối trừ (mắt xích).
  - Nợ đầu kỳ (lịch sử), Điều hành (bảng trình chiếu), Tồn kho (nhiều hộp), Nợ quá hạn (đồng hồ báo thức), Hàng hóa (hộp hàng), Kho hàng (container).
  - Lý do nhập xuất (danh sách tick), Hệ thống (bánh răng), Cài đặt của tôi (người và bánh răng).

### Ưu tiên 3: khác (12)

Danh sách chọn (layout-list), Ghim (sao), Đổi chiều (arrow-right-left), Xóa trắng (tẩy), Tự động khớp (lấp lánh), Máy tính, Chi nhánh (tòa nhà), Bỏ liên kết (link-off), Không tìm thấy (search-x), Hoàn tác (undo), Đánh số (#), Bàn phím.

## 9. Chính sách cho icon tiện ích

Các artboard đề xuất có vẽ rời chevron, mũi tên quay lại, thu gọn menu, nút đóng tab, chi nhánh (hình nhà) và spinner, nhưng những icon này chưa có trong thư viện và chưa có luật. Đề nghị:
- thêm một nhóm "Tiện ích" **không có khối**, dùng cùng độ dày nét;
- ghi rõ khi nào một icon có khối và khi nào không.

## 10. Bảng cỡ

Các màn đề xuất dùng cỡ 18px và 22px, nét 1,85. Hai cỡ này chưa có trong bảng cỡ và độ dày nét (16 · 20 · 24 · 32 · 48). Xin bổ sung vào bảng, hoặc đổi các màn đề xuất về cỡ có trong bảng.

## 11. Giấy phép

Xin cho biết điều khoản dùng bộ icon trong một sản phẩm thương mại: có được dùng không, có cần ghi công, có được sửa hoặc vẽ thêm theo cùng phong cách không.

## Cách chúng tôi kiểm tra lại

Khi có bản mới, chúng tôi chạy lại đúng phép đo ở trên, bằng trình duyệt thật ở độ phân giải 1x. Bộ icon đạt yêu cầu khi:
- phủ ≥ 90% icon nghiệp vụ, gồm đủ 8 thao tác ở ưu tiên 1 và 48 icon sidebar không trùng nhau;
- khe từ mép nét tới khối ≥ 1 đơn vị lưới ở mọi cỡ;
- khối nằm trong vùng an toàn, hoặc có ngoại lệ được ghi rõ;
- màu nhấn ≥ 3:1 trên mọi nền có hiện khối.
