# Đợt 2 — Thu/Chi, Quỹ & Công nợ

> Brainstorm: 2026-10-07 22:52:01 · Tiếp nối [brainstorm clone phiếu nhập/xuất](../261006-2139-stock-voucher-clone/SUMMARY.md) (đóng các câu hỏi ở [section-04](../261006-2139-stock-voucher-clone/section-04-round2-open-questions.md)) · Đợt 1 đã xong: [plan inventory round 1](../../plans/archived/261006-2259-inventory-round1/SUMMARY.md) · Review: 2026-10-07 23:01:34, duyệt có điều kiện, đã sửa theo [section-04](section-04-review-decisions.md)

## Problem framing

Đợt 1 đã ghi sổ hàng hóa: phiếu nhập/xuất có Tổng TT, Số tiền TT và HTTT, nhưng **tiền thì chưa theo dõi**: không có phiếu thu/chi, không có quỹ, không có công nợ. Đợt 2 thay luồng `ChungTu.vb` (THUTM/CHITM/THUKHAC/CHIKHAC) và báo cáo `SoDuCongNoMuaBan.vb` của phần mềm cũ.

Theo yêu cầu, thiết kế làm theo **mô hình MISA nhưng không dùng hệ thống tài khoản**. Ở MISA, phiếu ảnh hưởng công nợ hay không là do TK 131/331; ở đây thay bằng cờ trên Lý do thu/chi và Lý do nhập/xuất. Phần mềm cũ hard-code mã khoản ('10', '09', '04', '08') và bỏ qua THUKHAC/CHIKHAC. Thiết kế mới bỏ cả hai cách đó.

Đã có từ Đợt 1: danh mục đối tượng chung (`Customer.IsCustomer/IsSupplier`), `PaymentMethod.IsCash`, `StockVoucher.PaidAmount`, khóa sổ `Branch.LockedUntil`, `DocumentNumbering`, advisory lock, transaction port. Quyền `suppliers.*` và `reports.debt` cũng đã có.

## Goals

- **Phiếu thu / Phiếu chi**:
  - header gồm lý do, đối tượng, quỹ, ngày giờ, người nộp/nhận;
  - dòng gồm diễn giải và số tiền;
  - list và form theo pattern Báo giá.
- **Lý do thu/chi**: danh mục có lý do hệ thống và lý do người dùng thêm. Mỗi lý do có cờ *Ảnh hưởng công nợ* và *Loại đối tượng*.
- **Quỹ** theo chi nhánh (tiền mặt, ngân hàng):
  - HTTT gắn loại quỹ, thay cho `IsCash`;
  - có số dư quỹ đầu kỳ;
  - có Sổ quỹ;
  - **Chuyển quỹ** (nộp tiền mặt vào ngân hàng, rút tiền, chuyển giữa tài khoản);
  - kiểm tra âm quỹ.
- **Công nợ theo chứng từ** (DebtLedger + đối trừ):
  - phiếu kho ghi công nợ theo cờ *Ảnh hưởng công nợ* trên Lý do nhập/xuất: bán/mua làm tăng, hàng trả lại làm giảm;
  - phiếu thu/chi làm giảm (hoặc tăng) công nợ;
  - đối trừ được lúc lập phiếu, trên màn Đối trừ chứng từ, hoặc qua chứng từ **Bù trừ công nợ** (phải thu ↔ phải trả cùng đối tượng).
- **Phiếu thu/chi tự động** từ phiếu kho có Số tiền TT > 0 (Số tiền TT > 0 thì bắt buộc HTTT có quỹ). Phiếu kho quản lý phiếu này; trên màn thu/chi chỉ xem được.
- **Nợ đầu kỳ theo từng chứng từ cũ**, có loại Trả trước.
- **Hạn mức nợ** theo khách × chi nhánh, chính sách cấu hình Cho phép / Cảnh báo / Chặn.
- **Hạn thanh toán**: *Số ngày được nợ* trên đối tượng, *Hạn TT* trên phiếu kho.
- **8 báo cáo** (7 công nợ + Sổ quỹ):
  - Số dư công nợ (tổng hợp);
  - Sổ chi tiết công nợ;
  - Công nợ theo chứng từ;
  - Tuổi nợ;
  - Nợ quá hạn (kèm cảnh báo trên form phiếu xuất);
  - Công nợ theo nhân viên;
  - Biên bản đối chiếu công nợ (xem trên màn hình);
  - Sổ quỹ.

## Non-goals

- Hệ thống tài khoản, TK Nợ/Có, sổ cái, bút toán kế toán.
- Đối tượng nhân viên, tạm ứng / hoàn ứng (TK 141).
- Trạng thái "Chưa ghi sổ" như MISA. Chứng từ chỉ có Hiệu lực hoặc Đã hủy, giống Đợt 1.
- Mẫu in/Excel phiếu thu/chi và biên bản đối chiếu: để Đợt 3.
- Nhập Excel nợ đầu kỳ: quyết ở Đợt 3 (cut-over).
- Công nợ theo mặt hàng, ngoại tệ, chênh lệch tỷ giá.
- Đồng bộ ngân hàng / VietQR vào quỹ.
- Backfill công nợ cho phiếu kho test đã có (chưa go-live).

## Constraints & assumptions

- Theo Clean Architecture, conventions và các pattern của Đợt 1:
  - ledger không kế thừa `BaseEntity`;
  - `xmin` làm concurrency token;
  - 422 cho vi phạm có thể xác nhận vượt;
  - 409 cho xung đột;
  - advisory lock trong một transaction;
  - `X-Branch-Id` cho chi nhánh làm việc.
- Phân quyền giống phiếu kho: xem mọi phiếu trong chi nhánh; sửa/xóa/hủy phiếu mình tạo; phiếu người khác cần `edit_all`. Quyền Thu và Chi tách riêng.
- Một đối tượng có thể vừa phải thu vừa phải trả. Hai phía theo dõi riêng; lý do quyết định phiếu ghi vào phía nào.
- **Giả định đã xác nhận:**
  - Công nợ theo nhân viên tính theo người lập phiếu xuất (`OwnerUserId`), không theo khách được gán cho nhân viên. *Review đề nghị xác nhận lại, xem Open questions.*
  - Nợ đầu kỳ và số dư quỹ đầu kỳ dùng chung ngày đầu kỳ của chi nhánh với tồn đầu kỳ.
- Ngày đầu kỳ hiện nằm trên từng dòng `OpeningStock`. Plan phải đưa nó thành một nguồn chung cho chi nhánh (`Branch.OpeningDate`), chỉ sửa được khi chi nhánh chưa có chứng từ.
- Hàm SQL cũ (`GetSoDuConNo`, `GetChiTietPhieuCongNo`, `SoTongHopCongNo`, `SoQuyTienMat`) không có trong repo. Công thức được thiết kế lại, nên phải đối chiếu số liệu trước go-live.
- Go-live sau khi xong cả 3 đợt.

## Approaches considered

| Chủ đề | Phương án | Quyết định | Lý do |
|---|---|---|---|
| Ảnh hưởng công nợ | Cờ trên khoản / Theo loại đối tượng / Giữ 4 loại cũ / MISA có TK | **MISA không dùng TK**: 2 loại phiếu + Lý do thu/chi có cờ ảnh hưởng công nợ | Người dùng chọn "tham khảo MISA"; thêm hệ thống tài khoản là làm phần mềm kế toán |
| Cấu trúc phiếu | Header đối tượng + lý do / Đối tượng từng dòng / Cả hai từng dòng | **Header** | Công nợ, hạn mức, in đều đơn giản; thu nhiều KH thì lập nhiều phiếu |
| Đối tượng | KH/NCC theo lý do / Thêm NV / Luôn bắt buộc | **KH/NCC theo lý do**; Thu/Chi khác được để trống | Không phải tạo đối tượng giả cho các khoản lặt vặt |
| Thanh toán | Theo đối tượng / Cả hai kiểu / Để sau | **Cả hai kiểu như MISA** (theo chứng từ và không theo chứng từ) | Có tuổi nợ, nợ quá hạn, công nợ theo NV |
| Khớp khoản chưa đối trừ | Đối trừ thủ công / FIFO tự động / Chỉ lúc lập phiếu | **Đối trừ chứng từ thủ công** | Giống MISA; không có phân bổ dây chuyền khi sửa phiếu lùi ngày |
| Bù trừ công nợ | Theo chứng từ / Theo tổng | **Theo chứng từ** | Nhất quán với đối trừ |
| Nợ đầu kỳ | Từng chứng từ / Một số tổng / Cả hai | **Từng chứng từ cũ** | Đối trừ và tuổi nợ đúng ngay từ đầu |
| Hạn mức | Khách × chi nhánh / Toàn công ty | **Khách × chi nhánh** | Khớp phần mềm cũ và cách chia chi nhánh |
| Vượt hạn mức | Chặn + quyền vượt / Cấu hình / Chặn cứng | **Cấu hình Cho phép / Cảnh báo / Chặn** | Giống chính sách âm kho |
| Quỹ | Danh mục quỹ / Chỉ tiền mặt / Không theo dõi | **Danh mục quỹ** (tiền mặt + ngân hàng) | Chuyển khoản cũng sinh phiếu tự động; có sổ quỹ |
| Phiếu kho ghi nợ | Cờ bool `PostsDebt` / `DebtEffect` trên Lý do nhập xuất / Mọi phiếu có đối tượng | **`StockReason.DebtEffect`** *(sửa sau review)* | Xuất biếu tặng, xuất nội bộ không thành nợ; hàng bán bị trả lại và trả hàng NCC ghi đúng phía, đúng chiều |
| Chuyển quỹ | Chứng từ Chuyển quỹ / Cặp phiếu Chi khác + Thu khác | **Chứng từ `FundTransfer`** *(thêm sau review)* | Một chứng từ, không làm phồng tổng thu/chi, không bị lập thiếu một nửa |
| Phiếu tự động | Phiếu kho quản lý / Sinh một lần rồi độc lập | **Phiếu kho quản lý, chỉ đọc** | Không lệch giữa Số tiền TT và phiếu thu |
| Lưu công nợ | **A** DebtLedger + settlement / B Tính động / C Cột còn nợ trên phiếu | **A** | Cùng kiểu ledger kho; mọi báo cáo đọc một nguồn; bỏ đối trừ dễ |

## Recommended approach (đã chốt)

**DebtLedger + DebtSettlement:**

- Mỗi chứng từ có tối đa **một `DebtEntry` cho mỗi phía**, khóa duy nhất `(SourceType, SourceId, Side)`. Khi sửa thì **cập nhật tại chỗ** để settlement trỏ vào vẫn còn nguyên.
- `DebtSettlement` nối dòng giảm với dòng tăng cùng chi nhánh, cùng đối tượng, cùng phía.
- Các số suy ra:
  - Số dư = Σ Tăng − Σ Giảm.
  - Còn nợ của một chứng từ = Amount − Σ settlement.
  - Settlement không làm đổi số dư.
- Quỹ **không có ledger riêng**: Sổ quỹ đọc thẳng từ phiếu thu/chi và chứng từ chuyển quỹ, vì phiếu tự động cũng là phiếu thật.

Chi tiết: [section-02-design.md](section-02-design.md) (data model, quy tắc, quyền, API, định nghĩa báo cáo).

## Supporting files

- [section-01-decisions.md](section-01-decisions.md): trả lời 10 câu hỏi của section-04 cũ, các quyết định thêm, đối chiếu MISA
- [section-02-design.md](section-02-design.md): data model, luồng ghi sổ, quy tắc sửa/hủy/đối trừ, chuyển quỹ, hạn mức, âm quỹ, khóa sổ, quyền, API, 8 báo cáo
- [section-03-delivery.md](section-03-delivery.md): kiểm thử, thứ tự triển khai, phương án tách 2A/2B, go-live gates, rủi ro
- [section-04-review-decisions.md](section-04-review-decisions.md): kết quả review BA lead / SA và các quyết định đã sửa

## Open questions

**Kế toán / PO:**
- Duyệt danh sách lý do thu/chi hệ thống, DebtEffect của từng lý do, và DebtEffect của lý do nhập/xuất, gồm 2 lý do mới NTL / XTL (section-02 §1).
- Khi Số tiền TT về 0, phiếu tự động bị xóa nên số phiếu thu/chi bị nhảy. Có chấp nhận không, hay cần hủy thay vì xóa?
- Phiếu thu/chi qua ngân hàng có cần đánh số riêng với tiền mặt (Giấy báo Có / Ủy nhiệm chi như MISA) không? Nếu có thì đặt counter theo (loại phiếu × FundKind).

**BA:**
- Công nợ theo nhân viên: người lập phiếu xuất có phải là sales không? Nếu phiếu thường do kế toán hoặc thủ kho lập thì thêm `StockVoucher.SalespersonId` (mặc định = người lập) và báo cáo đọc theo trường này.
- Khách trả tiền ở chi nhánh B cho hóa đơn của chi nhánh A: hiện không đối trừ chéo chi nhánh được. Thực tế có xảy ra không?
- Có tài khoản ngân hàng dùng chung cho nhiều chi nhánh không? Thiết kế hiện tại đặt quỹ theo chi nhánh.
- Role SALES có được xem số dư, hạn mức, nợ quá hạn của khách (panel trên phiếu xuất, báo cáo công nợ) không?
- Mặc định chính sách vượt hạn mức và âm quỹ: đề xuất Cảnh báo (reviewer đồng ý), chờ BA chốt.

**Plan:**
- Tách Đợt 2 thành 2A/2B (section-03 §3): reviewer **đề xuất tách**, chốt khi viết plan.

**Từ Đợt 1 vẫn còn:** `PurchaseCostIncludesVat`, danh sách hàng `TrackInventory = false`, kế hoạch cut-over.

**Đã đóng sau review:** nhóm tuổi nợ dùng mặc định 0–30 / 31–60 / 61–90 / >90; người dùng đổi mốc ngay trên tham số báo cáo, không lưu cấu hình.

## Next steps

1. `write-plan` cho Đợt 2 dựa trên section-02 và section-03.
2. Gửi kế toán danh sách lý do thu/chi và lý do nhập/xuất (section-02 §1) cùng các câu hỏi Kế toán/PO ở trên; gửi BA các câu hỏi BA. Câu `SalespersonId` cần trả lời trước khi plan phase 1 vì nó làm đổi data model.
3. Cập nhật `docs/project-pdr/product-goals.md` (Planned Scope Round 2) khi plan được duyệt.
