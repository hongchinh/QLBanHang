# Clone Phiếu nhập/xuất kho từ phần mềm cũ (PhieuNhapXuat.vb)

> Brainstorm: 2026-10-06 21:39:54 · Review SA/BA: 2026-10-06 22:40:45 ([section-05](section-05-review-decisions.md)) · Nguồn: `source/Forms/PhieuNhapXuat.vb` (5680 dòng), `ChungTu.vb`, `PhieuThuChi.vb`, `TaoPhieuThuChi.vb`, `SoDuCongNoMuaBan.vb`

## Problem framing

Phần mềm cũ (VB.NET WinForms + SQL Server) có form **PhieuNhapXuat** phục vụ cả Phiếu nhập kho và Phiếu xuất kho. Form này gắn với tồn kho, giá vốn (bình quân/FIFO), quy đổi ĐVT, công nợ, hạn mức nợ, phiếu thu/chi tự động, khóa sổ, nhiều chi nhánh. Yêu cầu: clone **nghiệp vụ phiếu nhập/xuất** sang QLDonHang (.NET 9 + React), UI theo pattern màn **Báo giá** (`/quotations`). Điều chuyển kho, kiểm kê, trả hàng (có trong source cũ) để sau.

Doanh nghiệp là **thương mại** (tấm xốp EPS/XPS, bông khoáng…), không sản xuất, không cắt hàng; hàng bán theo cái / m dài / m² / m³ (`Product.PricingMode`).

App mới hiện là quotation-first, `product-goals.md` ghi rõ non-goal: không phiếu kho, không tồn kho, không công nợ. → **Đây là quyết định mở rộng scope sản phẩm**; docs PDR phải cập nhật.

Phân tích form cũ phát hiện 15 lỗi/bất nhất quán; mỗi lỗi được duyệt riêng với user (xem [section-02](section-02-legacy-bug-decisions.md)).

## Goals

- Phiếu nhập kho / Phiếu xuất kho với nghiệp vụ cũ (đã sửa lỗi theo quyết định).
- Tồn kho theo kho, nhiều kho trên một phiếu, nhiều chi nhánh; mỗi hàng một đơn vị tồn theo PricingMode.
- Giá vốn **bình quân cuối kỳ** (kỳ tháng/quý/năm và phạm vi chi nhánh/kho cấu hình được), tự tính lại khi sửa/xóa/hủy phiếu lùi ngày.
- VAT từng dòng, CK cả đơn phân bổ trước VAT, khóa sổ theo chi nhánh (ngày bất kỳ như MISA).
- Phiếu thu/chi + công nợ + hạn mức + bù trừ công nợ (Đợt 2).
- UI list + form giống Báo giá cho cả phiếu kho và phiếu thu/chi.

## Non-goals

- Bảng giá mua/bán theo nhóm, chính sách giá theo cấp, giá theo lý do (01/XNB) — đơn giá chỉ lấy từ danh mục hàng.
- ĐVT chuyển đổi (mã quy đổi kiểu cũ hoặc kiểu MISA).
- Hạn sử dụng/lô hàng, barcode, giá bìa sách, cột giá bán lẻ tham khảo.
- Chuyển/nhận phiếu qua FTP (bảng ZZZ).
- Các báo cáo kho/công nợ khác ngoài 3 màn tối thiểu.
- Migrate dữ liệu từ DB cũ.
- **Backlog (làm sau, ràng buộc ghi ở [section-05 §5](section-05-review-decisions.md#5-backlog-ghi-vào-plan)):** FIFO, điều chuyển kho, kiểm kê, trả hàng, Báo giá → Phiếu bán hàng.

## Constraints & assumptions

- Theo Clean Architecture + conventions hiện có (EF Core/PostgreSQL, permission guard, soft-delete/audit, TanStack Query/Table).
- Mỗi user có **chi nhánh mặc định**; quyền `branches.access_all` cho phép đổi chi nhánh làm việc. Chi nhánh chỉ áp cho module kho/thu chi — Báo giá, danh mục hàng/đối tượng dùng chung.
- Phân quyền: xem **mọi phiếu trong chi nhánh**; sửa/xóa/hủy phiếu mình tạo, phiếu người khác cần `edit_all`; quyền nhập và xuất tách riêng; xem giá vốn cần quyền riêng.
- Đối tượng là **danh mục chung** (Khách hàng thêm cờ Là KH / Là NCC); lý do nhập/xuất quy định loại đối tượng; người giao/nhận là text tự do.
- Hàng dịch vụ (vd Vận chuyển) đánh dấu không theo dõi tồn kho.
- Phiếu lưu **ngày + giờ**; thứ tự giao dịch và SL tồn trên form theo thời điểm phiếu.
- Giá trị nhập kho gồm CK dòng, phân bổ CK cả đơn và phí VC (theo giá trị); VAT đầu vào tính vào giá gốc hay không là cấu hình.
- Giá vốn chỉ nằm trong ledger; dòng phiếu không lưu giá vốn.
- Phiếu nhập cập nhật `Product.CostPrice` (giá nhập gần nhất) → ảnh hưởng giá vốn mặc định của Báo giá (đã chấp nhận).
- Hàm SQL tính tồn/công nợ của hệ thống cũ (`GetSoLuongTonTenVatTu`, `GetDonGiaTonVatTu`, `GetSoDuConNo`, SP FIFO) **không có trong repo** — công thức mới được thiết kế lại, cần đối chiếu số liệu thực tế.
- Go-live sau khi xong cả 3 đợt.

## Approaches considered

| Chủ đề | Phương án | Quyết định | Lý do |
|---|---|---|---|
| Phạm vi | Kho cơ bản / Clone đầy đủ / Chỉ chứng từ | **Clone nghiệp vụ phiếu nhập/xuất**, chia 3 đợt; điều chuyển/kiểm kê/trả hàng để sau | Thay thế phần mềm cũ; phần còn lại vào backlog |
| Lỗi cũ | Sửa giữ ý đồ / Giữ y hệt / Duyệt từng lỗi | **Duyệt từng lỗi** | — |
| Lưu tồn & công nợ | Tính động từ chứng từ / Ledger + bảng số dư / Ledger + chốt kỳ | **Ledger + StockBalance + bảng số dư theo kỳ giá vốn** | Ledger lưu SL lũy kế → kiểm âm, thẻ kho rẻ; bảng kỳ vừa là mốc chụp số dư vừa phục vụ BQ cuối kỳ |
| Giá vốn | BQ tức thời / BQ cuối kỳ / FIFO | **BQ cuối kỳ**, kỳ và phạm vi cấu hình; FIFO để sau | Giống MISA; tính lại theo kỳ nên rẻ |
| Quy đổi ĐVT | Mã quy đổi kiểu cũ / Kiểu MISA / Bỏ | **Bỏ** — 1 đơn vị tồn theo PricingMode, dòng nhập số tấm + kích thước | DN thương mại, mỗi hàng 1 ĐVT; khớp cách tính của Báo giá |
| Đối tượng | Bảng NCC riêng / Danh mục chung / 2 bảng + liên kết | **Danh mục chung** (Customer + cờ KH/NCC) | Bù trừ công nợ như MISA; không phải refactor Báo giá |
| Nguồn thu/chi | ChungTu.vb / PhieuThuChi.vb / Kết hợp | **Kết hợp**: data model & nghiệp vụ ChungTu, UI nhiều dòng, layout Báo giá | PhieuThuChi.vb là tính năng chết; ChungTu.vb là luồng thật |
| Tồn đầu kỳ | Phiếu nhập lý do đặc biệt / Màn riêng / Migrate | **Màn số dư tồn đầu riêng** | Không migrate DB cũ |
| Khóa sổ | Chỉ cuối kỳ / Ngày bất kỳ | **Ngày bất kỳ như MISA**; giá vốn kỳ chưa kết thúc vẫn tính lại tới hết kỳ | Giống MISA |
| Menu | Nhóm "Kho" / 1 màn chung / Navigator kiểu cũ | **Nhóm menu "Kho"** | — |

## Recommended approach (đã chốt)

**3 đợt triển khai:**

1. **Đợt 1 — Kho:** Chi nhánh, Kho, Đối tượng (cờ KH/NCC), Lý do nhập xuất, HTTT, cờ theo dõi tồn trên hàng, Tồn đầu kỳ, Phiếu nhập/xuất (tồn, giá vốn BQ cuối kỳ, tính lại, kiểm âm mọi thao tác, khóa sổ, đánh số), báo cáo Tồn kho + Thẻ kho.
2. **Đợt 2 — Thu/Chi & Công nợ:** Phiếu thu/chi (UI giống Báo giá, nhiều dòng), Khoản thu/chi, Công nợ đầu kỳ, Hạn mức nợ, phiếu thu/chi tự động từ phiếu kho, DebtLedger, Bù trừ công nợ, báo cáo Số dư công nợ.
3. **Đợt 3:** In/Excel theo template (như Báo giá; mẫu 01-VT/02-VT), Nhập Excel dòng hàng, Sao chép phiếu.

Thiết kế chi tiết Đợt 1: [section-03-round1-design.md](section-03-round1-design.md).

## Supporting files

- [section-01-legacy-phieu-nhap-xuat-spec.md](section-01-legacy-phieu-nhap-xuat-spec.md) — đặc tả nghiệp vụ form cũ (reverse-engineered)
- [section-02-legacy-bug-decisions.md](section-02-legacy-bug-decisions.md) — từng lỗi cũ và quyết định xử lý
- [section-03-round1-design.md](section-03-round1-design.md) — data model, công thức, quy tắc lưu, test, rollout Đợt 1
- [section-04-round2-open-questions.md](section-04-round2-open-questions.md) — phát hiện về thu/chi/công nợ cũ + câu hỏi cần chốt cho Đợt 2
- [section-05-review-decisions.md](section-05-review-decisions.md) — review SA/BA, quyết định bổ sung, đối chiếu MISA, backlog

## Open questions

- ~~Đợt 2: 10 câu hỏi ở [section-04](section-04-round2-open-questions.md)~~ — đã chốt ở [cash-debt-round2/section-01](../261007-2252-cash-debt-round2/section-01-decisions.md).
- Sau review — xem [section-05 §6](section-05-review-decisions.md#6-còn-mở): VAT đầu vào trong giá nhập (xác nhận với kế toán), danh sách hàng không theo dõi tồn, kế hoạch cut-over, quyền quản lý NCC.

## Next steps

1. ~~Cập nhật `docs/project-pdr/product-goals.md` (bỏ non-goal kho/tồn/công nợ)~~ — đã xong 2026-10-06 (mục Planned Scope + Inventory rules).
2. ~~`write-plan` cho **Đợt 1**~~ — đã xong và merge 2026-10-07 ([plan](../../plans/archived/261006-2259-inventory-round1/SUMMARY.md)).
3. ~~Brainstorm riêng cho Đợt 2~~ — đã xong 2026-10-07: [cash-debt-round2](../261007-2252-cash-debt-round2/SUMMARY.md).
