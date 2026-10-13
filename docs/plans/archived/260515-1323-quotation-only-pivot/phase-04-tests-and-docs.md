# Phase 04 — Integration tests + documentation

**Status:** [x] complete (test execution deferred — Docker not running)
**Complexity:** M

## Objective

Bổ sung integration tests cho hành vi mới (snapshot ConfirmedAt, gating CancelConfirmed, sales revenue report). Cập nhật tài liệu: tạo `product-goals.md`, archive BD doc cũ, refresh `docs/SUMMARY.md`. Cập nhật memory project.

## Files

### Modify
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationStateMachineTests.cs`
- `docs/SUMMARY.md`
- `docs/architecture/system-architecture.md` (chỉ dòng nhắc `ConvertedToOrder` ở phần Lock-at)

### Create
- `backend/tests/OrderMgmt.IntegrationTests/Quotations/QuotationConfirmationTests.cs`
- `backend/tests/OrderMgmt.IntegrationTests/Reports/SalesRevenueReportTests.cs`
- `docs/project-pdr/product-goals.md`
- `docs/bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md` (move from `docs/bd/`)

### Move
- `docs/bd/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md` → `docs/bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md`
- `docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md` → giữ nguyên (không liên quan scope thay đổi).

## Tasks

### A. Integration tests

1. Mở `QuotationStateMachineTests.cs`:
   - Trong `Allowed_transitions_progress_status`: sau khi `afterConfirm.Status.Should().Be(QuotationStatus.Confirmed);`, thêm assert `afterConfirm.ConfirmedAt.Should().NotBeNull();` và `afterConfirm.ConfirmedByUserId.Should().NotBeNull();`.
   - Sau `afterCancel.Status.Should().Be(QuotationStatus.Cancelled);`, thêm `afterCancel.CancelledAt.Should().NotBeNull();`.
2. Tạo `QuotationConfirmationTests.cs`:
   - `[Fact] Confirm_RecordsConfirmedAtAndOwner` — tạo draft → send → confirm → assert `ConfirmedAt` ≈ now (within 5 seconds) và `ConfirmedByUserId == admin user id`.
   - `[Fact] Cancel_FromConfirmed_AsAdmin_SetsCancelledAt` — tạo draft → send → confirm → cancel (admin có quyền) → assert `CancelledAt` không null, status = Cancelled.
   - `[Fact] Cancel_FromConfirmed_WithoutPermission_Returns403` — cần một test client login as user thiếu permission `quotations.cancel_confirmed`. Nếu test infrastructure hiện chưa hỗ trợ multi-user → comment skip + open question. (Xem `QuotationPermissionTests.cs` để check pattern.)
3. Tạo `Reports/SalesRevenueReportTests.cs`:
   - `[Fact] Report_AggregatesByOwner_AndExcludesCancelled` — tạo 3 báo giá: 2 confirmed (cùng owner), 1 cancelled-from-confirmed → call `/api/reports/sales-revenue?from=...&to=...` → assert có 1 item, `quotationCount = 2`, `totalRevenueGross = sum 2 quotation Total`.
   - `[Fact] Report_FiltersByConfirmedAt_NotQuotationDate` — tạo báo giá `quotationDate` ngoài range nhưng `ConfirmedAt` trong range → assert có trong report.
   - `[Fact] Report_FiltersBySaleUserId` — tạo 2 owner khác nhau → filter `saleUserId` → chỉ thấy 1.
4. Build + run tests:
   ```
   dotnet test backend/tests/OrderMgmt.IntegrationTests \
     --filter "FullyQualifiedName~Quotation|FullyQualifiedName~SalesRevenue"
   ```

### B. Documentation

5. Tạo `docs/project-pdr/product-goals.md`:
   ```markdown
   # QLBanHang — Product Goals

   ## Mục tiêu sản phẩm
   QLBanHang là phần mềm quản lý báo giá. Báo giá là chứng từ duy nhất xuyên suốt vòng đời bán hàng. Khi báo giá đạt trạng thái Đã xác nhận, hệ thống ghi nhận doanh thu cho nhân viên kinh doanh sở hữu báo giá đó.

   ## Đối tượng sử dụng
   - **Sale**: tạo báo giá, gửi khách, theo dõi trạng thái, xác nhận khi khách OK.
   - **Quản lý/Admin**: xem báo cáo doanh thu toàn hệ thống, hủy báo giá đã xác nhận khi cần.

   ## Phạm vi
   - Lập, sửa, in báo giá (PDF/Excel).
   - Theo dõi trạng thái báo giá: `Draft → Sent → Confirmed → Cancelled`.
   - Khi flip sang Confirmed: snapshot thời điểm + người xác nhận, ghi nhận doanh thu cho owner sale.
   - Báo cáo doanh thu theo sale, group theo `ConfirmedAt`, hiển thị Total (gồm thuế + cước) và Subtotal (thuần).
   - Phân quyền: Sale tự confirm; chỉ admin/manager hủy báo giá đã Confirmed.
   - Quản lý danh mục khách hàng, hàng hóa.

   ## Non-goals
   - Không quản lý đơn hàng (Order) như chứng từ riêng.
   - Không quản lý bàn giao, phiếu xuất kho, biên bản bàn giao.
   - Không tracking thanh toán nhiều đợt, công nợ, tạm ứng.
   - Không báo cáo công nợ, báo cáo giao hàng.
   - Không gửi báo giá qua email/Zalo trực tiếp từ phần mềm (giai đoạn sau).
   - Không quản lý tồn kho.

   ## Tài liệu liên quan
   - Brainstorm: [../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md](../brainstorms/260515-1249-quotation-only-pivot/SUMMARY.md)
   - BD doc cũ (đã archive): [../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md](../bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md)
   - Architecture: [../architecture/system-architecture.md](../architecture/system-architecture.md)
   ```
6. Tạo folder `docs/bd/archived/` nếu chưa có, di chuyển file BD cũ:
   ```
   git mv docs/bd/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md docs/bd/archived/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md
   ```
7. Mở file vừa di chuyển, prepend ngay sau dòng `# PHÂN TÍCH YÊU CẦU PHẦN MỀM QUẢN LÝ ĐƠN HÀNG`:
   ```markdown
   > **ARCHIVED (2026-05-15).** Tài liệu này phản ánh scope ban đầu (báo giá → đơn hàng → bàn giao → thanh toán → báo cáo). Sản phẩm đã pivot sang scope "báo giá là chứng từ duy nhất". Mục tiêu hiện tại xem [../../project-pdr/product-goals.md](../../project-pdr/product-goals.md). Giữ file này làm reference lịch sử.
   ```
8. Cập nhật `docs/SUMMARY.md`:
   - Sửa dòng `**QLBanHang** — Phần mềm Quản lý Đơn hàng, Báo giá, Bàn giao, Báo cáo.` thành `**QLBanHang** — Phần mềm Quản lý Báo giá.`.
   - Đổi mô tả: `Quản lý vòng đời báo giá: Draft → Sent → Confirmed → Cancelled. Doanh thu ghi nhận khi báo giá Confirmed.`.
   - Section "Project PDR" sửa pointer file thành `product-pdr/product-goals.md` (đã đúng path) và cập nhật description nếu cần.
9. Cập nhật `docs/architecture/system-architecture.md`:
   - Tìm dòng có `Draft<Sent<Confirmed<ConvertedToOrder` → sửa thành `Draft<Sent<Confirmed`.
   - Nếu có section nào nhắc Order/Delivery → thêm note "(out of scope post-pivot 2026-05-15)".

### C. Memory update (auto-memory)

10. Tạo memory file `C:\Users\admin\.claude\projects\d--Projects-QLBanHang\memory\project_quotation_only_pivot.md`:
    ```markdown
    ---
    name: project-quotation-only-pivot
    description: QLBanHang đã pivot scope (2026-05-15) — báo giá là chứng từ duy nhất, không còn module đơn hàng/bàn giao/thanh toán
    metadata:
      type: project
    ---

    QLBanHang pivot scope từ 2026-05-15: bỏ hoàn toàn module Đơn hàng + Bàn giao + Thanh toán. Báo giá là chứng từ duy nhất. Vòng đời `Draft → Sent → Confirmed → Cancelled`. Khi báo giá flip Confirmed → snapshot `ConfirmedAt` + `ConfirmedByUserId`, ghi nhận doanh thu cho owner sale. Hủy báo giá Confirmed cần permission `quotations.cancel_confirmed` (admin/manager).

    **Why:** User explicitly trimmed scope trong brainstorm `docs/brainstorms/260515-1249-quotation-only-pivot/` để tập trung vào MVP báo giá; module đơn hàng/bàn giao chưa hiện thực, không có lý do giữ lại.

    **How to apply:** Đừng giả định có entity Order, Delivery, Payment. BD doc cũ ở `docs/bd/archived/` chỉ là lịch sử. Goal hiện tại: `docs/project-pdr/product-goals.md`. Khi user nhắc "đơn hàng" sau pivot — clarify trước khi build entity mới.
    ```
11. Cập nhật `MEMORY.md` index, thêm dòng mới sau dòng existing:
    ```markdown
    - [Quotation-only pivot](project_quotation_only_pivot.md) — QLBanHang scope pivot (2026-05-15) — báo giá là chứng từ duy nhất, không có Order/Delivery/Payment.
    ```

## Verification

- `dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Quotation|FullyQualifiedName~SalesRevenue"` → all pass (bao gồm test cũ + 4-5 test mới).
- `git status` show file BD cũ moved (rename detected) + 1 file mới `product-goals.md` + memory files.
- `Grep -r ConvertedToOrder docs/architecture` → không khớp (đã sửa system-architecture.md).
- `cat docs/SUMMARY.md` → tagline mới hiển thị đúng.

## Exit Criteria

- 4-5 integration test mới pass.
- `docs/project-pdr/product-goals.md` tồn tại, BD doc cũ đã ở `docs/bd/archived/` với header ARCHIVED.
- `docs/SUMMARY.md` + `docs/architecture/system-architecture.md` không còn nhắc Đơn hàng/Bàn giao/ConvertedToOrder như feature hiện tại.
- Memory project mới đã ghi.
- Tất cả 4 phase đã complete; có thể `git add` toàn bộ và commit theo convention dự án.
