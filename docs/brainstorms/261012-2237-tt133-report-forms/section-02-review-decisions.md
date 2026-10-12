# Section 02 — Kết quả review BA / SA

> Thuộc [SUMMARY](SUMMARY.md). Review: 2026-10-12 23:03:28 · Vai trò: BA + Solution Architect · Kết luận: **duyệt có điều kiện**. Ba điểm chặn B1–B3 đã được sửa trong SUMMARY và [section-01](section-01-form-specs.md).

## Điểm chặn (đã sửa)

### B1 — Phạm vi sổ: chi nhánh hay toàn công ty

**Phát hiện:** sổ kế toán lập theo đơn vị kế toán là công ty, nhưng mọi báo cáo công nợ và kho đều lọc theo chi nhánh đang làm việc (`CashDebtReportService` lọc `BranchId` ở cả ba truy vấn). Hệ quả:
- đối tượng có công nợ ở hai chi nhánh sẽ có hai sổ S12 rời nhau;
- Biên bản đối chiếu gửi khách chỉ ra số dư một phần;
- S07 không ra được tồn kho toàn công ty.

**Quyết định:**
- Áp dụng cho S07, S12, Bảng tổng hợp công nợ và Biên bản đối chiếu.
- Thêm tham số `scope = Branch | Company`, mặc định là chi nhánh đang làm việc.
- `Company` cộng mọi chi nhánh và cần quyền `branches.access_all`. Không có quyền thì ẩn lựa chọn này và API trả 403.
- S04a, S05 vẫn theo từng quỹ, vì quỹ gắn với chi nhánh. S06, S08 vẫn theo chi nhánh đang làm việc.

**Hệ quả kỹ thuật:**
- Truy vấn nguồn (`debt-balance`, `debt-ledger`, `debt-reconciliation`, `stock-summary`) nhận thêm `scope`.
- Đối trừ (`DebtSettlement`) chỉ xảy ra trong cùng chi nhánh. Phạm vi Company chỉ cộng và trộn sổ theo thời gian, không đối trừ chéo.
- Dòng "Đơn vị" in `{Tên công ty}` khi scope là Company, và `{Tên công ty} – {Tên chi nhánh}` khi scope là Branch.

### B2 — S06 theo kho không có giá trị khi tính giá theo chi nhánh

**Phát hiện:** `InventorySettings.CostingScope` mặc định là `Branch`. Khi đó thẻ kho lọc theo kho trả giá trị null (`StockCardDto.ValuesAtScopeOnly`, D32). Vì vậy S06 "mỗi mặt hàng × kho" không có cột giá trị.

**Quyết định:**
- **Đặt `CostingScope = Warehouse` lúc go-live.** Đây là thiết lập có sẵn trong Cài đặt kho; đổi thiết lập thì `InventoryRecalcService` tự chạy lại giá vốn, nên không cần sửa code.
- Cần kế toán xác nhận, vì giá vốn sẽ tính bình quân theo từng kho, và chuyển kho nội bộ sẽ mang giá vốn theo.
- S06 mở theo mặt hàng × kho như thiết kế.
- Nếu thiết lập bị đổi lại `Branch`, S06 theo kho chỉ còn cột số lượng, kèm ghi chú "Giá trị tính theo chi nhánh". S07 lọc theo kho dưới phạm vi `Branch` cũng chỉ còn cột số lượng.

### B3 — Báo cáo gốc phải mở rộng DTO

**Phát hiện:** câu "Mỗi biểu là một cách trình bày của báo cáo đang có" không đủ. DTO đang thiếu các trường mà mẫu cần:
- `CashBookRowDto` không có lý do (S04a cột F, S05 cột E);
- `DebtLedgerRowDto` không có lý do (S12 cột E) và hạn thanh toán (S12 cột 1).

**Quyết định:**
- Thêm `ReasonName` vào `CashBookRowDto`.
- Thêm `ReasonName` và `DueDate` vào `DebtLedgerRowDto`.
- Không đổi công thức. Màn Sổ quỹ và Sổ chi tiết công nợ hiện cũng có thêm các cột này.

## Đề xuất không chấp nhận (giữ thiết kế hiện tại)

| # | Đề xuất | Ghi chú |
|---|---|---|
| S1 | Mô hình trung lập `FormDocument` ở Application, ClosedXML chỉ làm renderer | Giữ: mapper gọi `Tt133FormBuilder` ở Infrastructure |
| S2 | Truy vấn batch cho xuất hàng loạt; giới hạn riêng cho PDF | Giữ. Ghi vào Rủi ro R1 |
| S3 | Profile LibreOffice riêng cho mỗi lần chuyển, cộng semaphore | Giữ. Ghi vào Rủi ro R2 |
| S4 | Endpoint riêng cho từng báo cáo; xuất hàng loạt dùng POST | Giữ endpoint `GET api/reports/{report}/export` |
| S5 | Dùng lại `system.manage_settings` thay `settings.company` | Giữ `settings.company` |
| S6 | Danh sách chữ ký theo từng mẫu | Giữ 3 chữ ký chung (riêng Biên bản đối chiếu theo section-01) |
| S7 | Câu `PurchaseCostIncludesVat` là điều kiện duyệt bản in S06/S07 | Giữ |
| S8 | In "Số liệu chưa khóa sổ" khi kỳ chưa khóa | Giữ |
| Nhỏ | `OpeningDate` null; số trang trong Excel; S07 chỉ có số lượng thì bỏ mã mẫu; đổi sang giờ VN; log người xuất sổ; làm P1 sớm | Giữ |

## Rủi ro ghi nhận

- **R1 — Xuất hàng loạt chậm.** "Tất cả có phát sinh" gọi truy vấn nguồn một lần cho mỗi đối tượng/mặt hàng (N+1). Cộng thêm chuyển PDF đồng bộ, yêu cầu có thể vượt timeout của reverse proxy khi gần ngưỡng 300 sheet. Plan phải đo trên dữ liệu thật và chỉnh ngưỡng theo kết quả.
- **R2 — LibreOffice chạy song song.** `LibreOfficeSpreadsheetPdfConverter` gọi `soffice --headless` với user profile mặc định. Hai lần xuất PDF cùng lúc có thể lỗi hoặc treo. Xuất hàng loạt làm rủi ro này tăng. Plan cần một test chạy song song (2–3 yêu cầu) trên môi trường Docker.
