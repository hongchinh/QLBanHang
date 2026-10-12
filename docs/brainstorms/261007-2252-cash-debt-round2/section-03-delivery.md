# Section 03 — Kiểm thử & triển khai

## 1. Kiểm thử

Backend chạy trên `qldonhang_integtest`, dùng fixture template DB của Đợt 1.

| Mức | Nội dung |
|---|---|
| Hàm thuần | Xác định phía/chiều theo `DebtEffect` của lý do thu/chi và lý do nhập/xuất (gồm ràng buộc tổ hợp hướng × DebtEffect × PartnerType); phía/chiều phiếu tự động; kiểm tra phân bổ (tổng, từng dòng, cùng đối tượng/phía/chi nhánh); hạn TT mặc định (gồm DueDate trống → DocDate); `EffectiveAt`; số tiền settlement Auto khi trả dư; chia nhóm tuổi nợ và quá hạn tại ngày; điểm thấp nhất của số dư quỹ |
| Engine | Ghi và UPDATE tại chỗ `DebtEntry`; hàng bán bị trả lại và trả hàng NCC (kèm phiếu tự động hoàn tiền); phiếu kho sinh/cập nhật/xóa phiếu tự động (Số tiền TT về 0, đổi HTTT, đổi quỹ, đổi ngày, trả dư); Số tiền TT > 0 với HTTT không có quỹ bị từ chối; hủy/khôi phục/xóa đi cùng nhau, khôi phục tạo lại settlement Auto; sửa xuống dưới phần đã đối trừ phải xác nhận rồi bỏ settlement; xác nhận bỏ settlement khi hủy; sửa ngày thì EffectiveAt được tính lại; khóa sổ với settlement; hạn mức × 3 chính sách, sửa giảm không cảnh báo; âm quỹ × 3 chính sách; chuyển quỹ (âm quỹ đi, hủy làm âm quỹ đến); xung đột `xmin` |
| Bù trừ / đối trừ | Hai bên lệch nhau thì từ chối; vượt số còn mở; khác đối tượng hoặc khác chi nhánh; bỏ batch; hủy chứng từ bù trừ; sửa phiếu thu chỉ thay settlement AtVoucher, giữ Manual |
| Invariant (chạy sau mỗi kịch bản) | Σ DebtEntry = Σ chứng từ nguồn hợp lệ; mỗi dòng có Σ settlement ≤ Amount; settlement cùng đối tượng/phía/chi nhánh và một Increase với một Decrease; phiếu tự động khớp PaidAmount, quỹ, ngày, đối tượng của phiếu kho; mọi phiếu kho có PaidAmount > 0 đều có phiếu tự động; EffectiveAt = max(PostedAt hai dòng); số dư quỹ = đầu kỳ + Σ thu − Σ chi + Σ chuyển đến − Σ chuyển đi |
| Lock | Đếm advisory lock khi một phiếu kho đụng cả hàng, đối tượng và quỹ; thứ tự khóa đúng |
| Báo cáo | Dữ liệu mẫu có nợ đầu kỳ, trả trước, phiếu tự động, hàng trả lại, bù trừ, chuyển quỹ, phiếu hủy, hai chi nhánh. So với số tính tay; tổng tuổi nợ, tổng theo chứng từ và tổng theo NV đều khớp số dư |
| Frontend (vitest) | Grid phân bổ trên form thu/chi, màn đối trừ (kiểm tổng), dialog hạn mức / âm quỹ / bỏ đối trừ, panel khách trên phiếu xuất |

## 2. Thứ tự triển khai dự kiến

| # | Phase | Phụ thuộc |
|---|---|---|
| 1 | Quyền, danh mục và cấu hình: `CashReason` + seed, `Fund`, `FundKind`, `StockReason.DebtEffect` + seed NTL/XTL, `CreditDays`, `CreditLimit`, chính sách, đánh số Receipt/Payment/FundTransfer/DebtOffset, ngày đầu kỳ chi nhánh (khóa khi đã có chứng từ) | — |
| 2 | Engine công nợ: `DebtEntry`, `DebtSettlement`, lock partner/fund, calculator, nợ đầu kỳ, số dư quỹ đầu kỳ | 1 |
| 3 | API phiếu thu/chi tay (có phân bổ lúc lập phiếu, kiểm tra âm quỹ) và chuyển quỹ | 2 |
| 4 | Tích hợp phiếu kho: ghi công nợ theo DebtEffect, rule Số tiền TT ↔ HTTT có quỹ, phiếu tự động, DueDate/FundId, hạn mức, partner-summary | 3 |
| 5 | API đối trừ chứng từ và bù trừ công nợ | 2 |
| 6 | API báo cáo (8) | 2–5 |
| 7 | Frontend: danh mục, cấu hình, quyền, menu nhóm **"Tiền & Công nợ"** | 1 |
| 8 | Frontend: list và form phiếu thu/chi (pattern Báo giá), chuyển quỹ; thay đổi trên form phiếu kho (quỹ, hạn TT, panel khách, dialog) | 3, 4, 7 |
| 9 | Frontend: đối trừ, bù trừ, nợ đầu kỳ, số dư quỹ đầu, báo cáo | 5, 6, 7 |
| 10 | Docs (PDR, architecture, directory, conventions) và verification | tất cả |

## 3. Phương án tách 2A / 2B (review đề xuất tách; chốt khi viết plan)

Khối lượng tương đương hoặc lớn hơn Đợt 1, nay có thêm chuyển quỹ và hàng trả lại. Review đề xuất tách:

- **2A**: phase 1–5 cùng phần frontend tương ứng (gồm chuyển quỹ), báo cáo Số dư công nợ + sổ chi tiết, và Sổ quỹ.
- **2B**: Công nợ theo chứng từ, Tuổi nợ, Nợ quá hạn, Công nợ theo nhân viên, Biên bản đối chiếu.

Data model của 2A đã chứa đủ dữ liệu cho 2B (settlement, DueDate, OwnerUserId), nên 2B chỉ thêm truy vấn và màn hình.

## 4. Dữ liệu hiện có

Chưa go-live nên chưa có dữ liệu thật.
- Migration seed lý do thu/chi hệ thống; gán `DebtEffect` cho lý do Xuất bán (ReceivableIncrease) / Nhập mua (PayableIncrease), các lý do khác = None; seed NTL / XTL; đổi `IsCash` → `FundKind`.
- Phiếu kho test đã có thì **không backfill**: reset dữ liệu test, hoặc mở rồi lưu lại từng phiếu. Phiếu test có Số tiền TT > 0 với HTTT không có quỹ sẽ bị từ chối khi lưu lại, nên reset dữ liệu test là cách gọn hơn.

## 5. Go-live gates (bổ sung)

| Gate | Owner | Khi nào |
|---|---|---|
| Kế toán duyệt lý do thu/chi hệ thống, DebtEffect của lý do thu/chi và lý do nhập/xuất (section-02 §1) | PO + kế toán | Trước khi viết plan phase 1 |
| Kế toán chốt đánh số phiếu: chấp nhận số nhảy khi xóa phiếu tự động; tách counter ngân hàng hay không | PO + kế toán | Trước khi viết plan phase 1 |
| BA chốt `SalespersonId` cho công nợ theo nhân viên | BA | Trước khi viết plan phase 1 |
| BA xác nhận mặc định CreditLimitPolicy / NegativeFundPolicy | BA | Trước go-live |
| Lấy SP `GetSoDuConNo`, `SoTongHopCongNo`, `SoQuyTienMat` từ DB production | Dev | Trước khi đối chiếu |
| Đối chiếu tay số dư công nợ và sổ quỹ 1 tháng với phần mềm cũ | Dev + kế toán | Trước go-live |
| Chốt cách nhập nợ đầu kỳ (tay hay Excel ở Đợt 3) | PO | Trước khi plan Đợt 3 |

## 6. Rủi ro

| Rủi ro | Giảm thiểu |
|---|---|
| Phiếu kho thành transaction lớn (kho + công nợ + quỹ + phiếu tự động) | Thứ tự lock cố định, gom key; test đếm lock; tách service phiếu tự động |
| Settlement bị mất do ghi lại ledger | UPDATE tại chỗ theo khóa duy nhất `(SourceType, SourceId, Side)`; invariant test |
| Báo cáo "tại ngày" chậm khi dữ liệu lớn | Index (BranchId, PartnerId, Side, PostedAt); nếu cần thì thêm bảng số dư theo kỳ sau (YAGNI lúc này) |
| Người dùng nhầm giữa "số dư" và "còn nợ theo chứng từ" | Báo cáo theo chứng từ luôn có phần "chưa đối trừ" để hai tổng khớp nhau |
| Khối lượng lớn | Phương án tách 2A/2B |
| Kế toán không đối trừ thường xuyên, nên báo cáo theo NV và tuổi nợ treo nợ đã trả | Grid phân bổ trên form thu/chi gợi ý sẵn theo chứng từ đến hạn sớm nhất; báo cáo luôn có dòng "Chưa phân bổ" |
