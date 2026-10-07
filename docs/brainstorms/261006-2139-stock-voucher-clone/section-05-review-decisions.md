# Section 05 — Review kiến trúc/BA và quyết định bổ sung

> Review: 2026-10-06 22:40:45 · Vai trò: Solution Architect / Team lead BA. Kết luận ban đầu: chưa đủ điều kiện `write-plan`. Mọi điểm dưới đây đã được chốt với user; [section-03](section-03-round1-design.md) đã cập nhật theo.

## 1. Điểm chặn

| # | Vấn đề | Quyết định |
|---|---|---|
| B1 | Brainstorm bỏ qua `Product.PricingMode` (cái / m / m² / m³) và kích thước — Báo giá tính SL = số tấm × kích thước | Doanh nghiệp chỉ thương mại, không cắt hàng. **Mỗi hàng 1 đơn vị tồn theo PricingMode.** Dòng phiếu: m dài → số tấm + dài; m² → số tấm + dài + rộng; m³ → số tấm + dài + rộng + dày. Kích thước mặc định từ danh mục, sửa được trên dòng |
| B1' | ĐVT chuyển đổi kiểu MISA | **Bỏ** (không có `ProductUnitConversion`) |
| B2 | Hàng dịch vụ (nhóm "Vận chuyển") bị ghi sổ kho | Thêm cờ **`TrackInventory`** trên hàng hóa |
| B3 | Chưa định nghĩa giá trị nhập kho | Trừ CK dòng; **CK cả đơn và phí VC phân bổ vào giá gốc theo giá trị**; VAT đầu vào tính vào giá gốc hay không là **cấu hình** (`PurchaseCostIncludesVat`, mặc định gồm VAT theo câu trả lời ban đầu) |
| B4 | Chỉ kiểm âm kho khi lưu phiếu xuất | **Kiểm ở mọi thao tác làm đổi tồn**: lưu nhập/xuất, hủy, khôi phục, xóa, sửa tồn đầu |
| B5 | Tính lại giá vốn ghi đè phiếu của người khác | **Giá vốn chỉ nằm trong ledger** (dữ liệu dẫn xuất); dòng phiếu chỉ giữ dữ liệu người dùng nhập. Phiếu có concurrency token (`xmin`) |
| B6 | "Clone đầy đủ" nhưng thiếu Điều chuyển, Kiểm kê, Trả hàng (có trong source cũ) | **Để sau** — ghi vào backlog (mục 5) kèm ràng buộc thiết kế |
| B7 | Doanh thu/thu tiền có hai nguồn (Báo giá vs Phiếu xuất) | **Để sau**: Báo giá sẽ chuyển thành **Phiếu bán hàng**. Trong lúc chờ, doanh thu vẫn lấy từ Báo giá `Confirmed` |

## 2. Điểm quan trọng

| Chủ đề | Quyết định |
|---|---|
| Đa chi nhánh | Làm |
| Phương pháp giá vốn | **Bình quân cuối kỳ**; FIFO vào backlog |
| Kỳ tính giá | Cấu hình Tháng / Quý / Năm (mặc định Tháng) |
| Phạm vi tính giá | Cấu hình: gộp các kho trong chi nhánh (mặc định) hoặc từng kho |
| Làm tròn | VND 0 số lẻ; tồn về 0 thì giá trị tồn về 0 |
| Chi nhánh của user | Mỗi user có **chi nhánh mặc định**; quyền `branches.access_all` cho phép đổi "chi nhánh làm việc" trên header |
| Xem / sửa phiếu | Xem **mọi phiếu trong chi nhánh**; sửa/xóa/hủy **phiếu mình tạo**, phiếu người khác cần `edit_all` |
| Đối tượng | **Danh mục chung kiểu MISA**: mở rộng `Customer` với cờ Là KH / Là NCC; lý do nhập/xuất quy định loại đối tượng; bù trừ công nợ làm ở Đợt 2 |
| CK cả đơn & VAT | CK cả đơn **phân bổ xuống dòng theo giá trị trước khi tính VAT** (cả nhập và xuất); %VAT từng dòng sửa được |
| Đánh số | Thêm **reset theo năm**; kiểm tra mẫu số khi lưu cấu hình |
| Hiệu năng | Bảng số dư theo kỳ làm mốc; ledger lưu SL lũy kế → kiểm âm và Thẻ kho là truy vấn rẻ |
| Kiến trúc | Thêm port transaction; `pg_advisory_xact_lock`; ledger không kế thừa `BaseEntity`/soft-delete |
| Go-live | Chưa xác định — **go-live sau khi xong cả 3 đợt** |
| Ràng buộc danh mục | Không đổi ĐVT/PricingMode, cờ giá gồm VAT, cờ theo dõi tồn; không xóa hàng/kho khi đã có phát sinh |
| Phân quyền | Thêm quyền xem giá vốn, quyền tồn đầu kỳ; tách quyền nhập/xuất; xử lý seed cho role WAREHOUSE |
| SL tồn trên form | Tồn **tại ngày giờ của phiếu** → phiếu lưu **ngày + giờ** |
| Khóa sổ | **Như MISA: khóa ngày bất kỳ.** Khóa chặn chứng từ ≤ ngày khóa; giá vốn của kỳ chưa kết thúc vẫn tính lại tới hết kỳ (kể cả phiếu xuất trong phần đã khóa); giá vốn cố định khi cả kỳ đã bị khóa |

## 3. Đối chiếu MISA

- **Đối tượng & bù trừ:** MISA dùng một danh mục đối tượng; một đối tượng có thể vừa là KH vừa là NCC (cùng mã). Phải thu (131) và phải trả (331) theo dõi riêng; bù trừ bằng chứng từ "Bù trừ công nợ" (Nợ 331 / Có 131) cho cùng đối tượng.
- **Khóa sổ:** chọn ngày bất kỳ; sau khóa không thêm/sửa/xóa chứng từ có ngày ≤ ngày khóa; có khóa sổ tự động theo ngày/tuần/tháng/năm. ([helpact.misa.vn](https://helpact.misa.vn/?p=3021))
- **Tính giá BQ cuối kỳ:** phiếu xuất ghi sổ chưa có đơn giá vốn, chỉ cập nhật khi chạy "Tính giá xuất kho" (chọn khoảng thời gian, kỳ Tháng/Quý/Năm, theo kho / không theo kho). ([helpact.misa.vn](https://helpact.misa.vn/kb/html_17060000/))
- **Đa chi nhánh:** tùy chọn tính giá chung cho các chi nhánh phụ thuộc hoặc riêng từng chi nhánh. ([helpact.misa.vn](https://helpact.misa.vn/kb/tinh-gia-xuat-kho-binh-quan-cuoi-ky-theo-tung-chi-nhanh/))
- **Khác MISA có chủ đích:** hệ thống mới **tự tính lại** giá vốn sau mỗi lần ghi (bảng số dư theo kỳ nên rẻ), không cần bước chạy tay; giá vốn kỳ chưa kết thúc hiển thị là "tạm tính".

## 4. Hệ quả thiết kế rút ra

- Tồn kho chỉ biết **tổng m / m² / m³** của một mã hàng, không biết còn bao nhiêu tấm mỗi cỡ.
- Công thức SL theo PricingMode **dùng chung** với Báo giá (`EffectiveQuantity`, kích thước tính bằng mm) — thuận lợi cho việc chuyển Báo giá → Phiếu bán hàng.
- Thứ tự giao dịch trong ngày được giải quyết bằng giờ của phiếu (câu hỏi mở cũ đã đóng).
- Đổi cấu hình kỳ / phạm vi tính giá / `PurchaseCostIncludesVat` khi đã có phát sinh: chỉ áp từ đầu một kỳ chưa khóa và kéo theo tính lại toàn bộ từ kỳ đó.
- Phí VC trên phiếu xuất cộng vào Tổng TT, không chịu VAT, không vào giá vốn; cần VAT vận chuyển thì dùng dòng hàng dịch vụ "Vận chuyển".
- Tổng SL trên phiếu chỉ có nghĩa khi mọi dòng cùng đơn vị tồn → không lưu `TotalQuantity`.

## 5. Backlog (ghi vào plan)

| Hạng mục | Ràng buộc cần nhớ khi làm |
|---|---|
| FIFO | Cần bảng lô nhập (`FifoLayer`); `CostingMethod` đã để chỗ |
| Điều chuyển kho | Phạm vi tính giá = chi nhánh → điều chuyển trong chi nhánh không đổi giá vốn. Phạm vi = kho → giá kho nhận phụ thuộc kho xuất, phải tính giá nối tiếp giữa các kho (không còn độc lập theo cặp). Liên chi nhánh = xuất/nhập mang theo giá vốn |
| Kiểm kê | Phiếu điều chỉnh chênh lệch; giá trị theo BQ của kỳ |
| Trả hàng (nhập trả lại / xuất trả NCC) | Nhập trả lại lấy giá vốn của phiếu xuất gốc, không lấy đơn giá nhập tay |
| Báo giá → Phiếu bán hàng | Khi làm, nguồn doanh thu chuyển từ Báo giá sang Phiếu bán hàng; dashboard/báo cáo doanh thu phải đổi theo |
| Bù trừ công nợ | Đợt 2 (dựa trên danh mục đối tượng chung) |

## 6. Còn mở

- `PurchaseCostIncludesVat` mặc định "gồm VAT" chỉ đúng khi VAT đầu vào không được khấu trừ — xác nhận với kế toán trước go-live.
- Danh mục hàng thật: những mã nào `TrackInventory = false` (ngoài nhóm Vận chuyển).
- Kế hoạch cut-over từ phần mềm cũ (go-live sau Đợt 3); có cần nhập Excel tồn đầu kỳ / công nợ đầu kỳ không (đề xuất đưa vào Đợt 3).
- Quản lý NCC dùng chung quyền `customers.*` hay tách quyền riêng.
