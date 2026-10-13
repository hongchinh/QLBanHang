# Quotation-only pivot — pivot scope to "Báo giá là chứng từ duy nhất"

## Problem framing

Goal hiện tại của QLBanHang (theo `docs/bd/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md`) là pipeline đầy đủ:

```
Báo giá → Đơn hàng → Bàn giao → Thanh toán → Báo cáo
```

Trong code thực tế chỉ mới có entity `Quotation` (với enum `QuotationStatus = Draft/Sent/Confirmed/ConvertedToOrder/Cancelled`). Các module Đơn hàng, Bàn giao, Thanh toán mới chỉ tồn tại ở mức enum và tài liệu.

Yêu cầu nghiệp vụ mới: **không tách Đơn hàng/Bàn giao thành chứng từ riêng**. Báo giá là chứng từ duy nhất; theo dõi vòng đời bằng status trên chính báo giá. Khi báo giá đạt trạng thái "Đã xác nhận" thì coi như đã thành đơn và ghi nhận doanh thu cho nhân viên sale sở hữu báo giá.

## Goals

- Báo giá là chứng từ duy nhất xuyên suốt vòng đời bán hàng.
- Vòng đời rõ ràng: `Draft → Sent → Confirmed → Cancelled`.
- Khi flip sang `Confirmed`, snapshot thời điểm + người confirm để dùng làm mốc ghi nhận doanh thu.
- Báo cáo doanh thu theo sale (owner của quotation), filter theo `ConfirmedAt`, hiển thị cả Total (gộp thuế + cước) và Subtotal (thuần).
- Sale tự confirm báo giá của mình; admin/manager có quyền hủy báo giá đã Confirmed.
- Tài liệu phản ánh đúng scope mới; BD cũ archive làm reference.

## Non-goals

- Không tạo entity Order/Delivery riêng.
- Không in BBBG, PXK.
- Không tracking thanh toán nhiều đợt, công nợ, tạm ứng.
- Không có báo cáo công nợ, báo cáo giao hàng.
- Không có duyệt báo giá (manager approval) — sale tự confirm.

## Constraints & assumptions

- Đã có `Quotation` entity với `OwnerUserId` → dùng làm "sale ghi nhận doanh thu", không cần sửa ownership model.
- Đã có `Subtotal`, `Total`, `TotalCost`, `GrossProfit` → đủ cho báo cáo doanh thu (và mở rộng báo cáo lợi nhuận sau).
- Backend Clean Architecture (.NET 9 + EF Core + Npgsql) — thay đổi schema cần migration EF.
- Không restart WebApi khi đang chạy — chỉ build các library project liên quan để verify (theo memory `feedback_build_skip_when_app_running`).

## Approaches considered

### A. Bỏ hoàn toàn module Đơn hàng + Bàn giao (chosen)
- **Pros**: scope rõ ràng, ít entity, ít code, tài liệu đơn giản, đúng bản chất nghiệp vụ user mô tả.
- **Cons**: nếu sau này cần in BBBG/PXK thì phải build lại — nhưng đó là quyết định business sau.
- **Complexity**: thấp.

### B. Giữ in BBBG/PXK nhưng generate trực tiếp từ báo giá
- **Pros**: vẫn có chứng từ giao nhận in được khi cần.
- **Cons**: tăng complexity (template engine, số chứng từ riêng), mâu thuẫn với mục tiêu "báo giá là chứng từ duy nhất".
- **Complexity**: trung bình.

### C. Hoãn quyết định module bàn giao
- **Pros**: không cam kết.
- **Cons**: roadmap không rõ ràng; team không biết nên ưu tiên gì.
- **Complexity**: thấp ngắn hạn, cao dài hạn.

## Recommended approach

**Approach A** — bỏ hoàn toàn module Đơn hàng và Bàn giao khỏi scope sản phẩm. Báo giá là chứng từ duy nhất; vòng đời `Draft → Sent → Confirmed → Cancelled`. Khi báo giá flip sang `Confirmed`, snapshot `ConfirmedAt` và `ConfirmedByUserId` để báo cáo doanh thu. Không tracking payment.

### Quyết định cụ thể đã chốt trong brainstorm

| Vấn đề | Quyết định |
|---|---|
| Phạm vi cắt giảm | Bỏ hoàn toàn module Đơn hàng + Bàn giao |
| Mốc ghi doanh thu | Theo thời điểm chuyển trạng thái Confirmed (snapshot `ConfirmedAt`) |
| Theo dõi thanh toán/công nợ | Bỏ luôn payment/công nợ trong scope hiện tại |
| Trạng thái `ConvertedToOrder` cũ | Bỏ; vòng đời mới chỉ còn 4 trạng thái |
| Cơ sở doanh thu | Hiển thị cả Total (gồm thuế + cước) và Subtotal (thuần) trong báo cáo |
| Hủy báo giá đã Confirmed | Cho hủy, nhưng cần permission `Quotation.CancelConfirmed` (admin/manager) |
| Quyền confirm | Sale tự confirm báo giá của mình; admin confirm được mọi báo giá |
| Xử lý BD doc cũ | Viết mới `docs/project-pdr/product-goals.md`, archive BD cũ |

## Implementation outline (4 phase)

1. **Backend domain + migration** — sửa enum `QuotationStatus` (bỏ `ConvertedToOrder`); thêm `ConfirmedAt`, `ConfirmedByUserId`, `CancelledAt` vào `Quotation`; bỏ enum `OrderStatus`/`PaymentStatus`/`PaymentMethod`/`DocumentType`; viết EF migration (chuyển row Status=4 → 3, backfill `ConfirmedAt = UpdatedAt` cho các Quotation Confirmed sẵn).
2. **Application + API** — endpoint `POST /api/quotations/{id}/confirm`, `/cancel`, `/mark-sent`; endpoint `GET /api/reports/sales-revenue`; thêm permission `Quotation.CancelConfirmed`, `Reports.SalesRevenue` và seed cho ADMIN/MANAGER.
3. **Frontend** — bỏ `ConvertedToOrder` ở filter/select; thêm 3 nút action trên form chi tiết báo giá (Đánh dấu đã gửi, Xác nhận, Hủy với warning đặc biệt nếu Confirmed); trang mới `/reports/sales-revenue`.
4. **Tài liệu** — tạo `docs/project-pdr/product-goals.md`; di chuyển `docs/bd/phan-tich-yeu-cau-phan-mem-quan-ly-don-hang.md` vào `docs/bd/archived/`; cập nhật `docs/SUMMARY.md`.

## Data model changes

### `QuotationStatus` (mới)

```csharp
public enum QuotationStatus {
    Draft = 1,
    Sent = 2,
    Confirmed = 3,
    Cancelled = 9,
}
```

### `Quotation` (thêm 3 trường)

| Trường | Kiểu | Mục đích |
|---|---|---|
| `ConfirmedAt` | `DateTime?` (UTC) | Snapshot thời điểm sang Confirmed; nguồn cho báo cáo doanh thu |
| `ConfirmedByUserId` | `Guid?` | Sale tự confirm hay admin confirm thay |
| `CancelledAt` | `DateTime?` | Snapshot thời điểm hủy; báo cáo loại trừ |

### Enum bị bỏ

- `OrderStatus`, `PaymentStatus`, `PaymentMethod`, `DocumentType` — gỡ khỏi `Enums.cs`.

## API & Permission

| Method | Route | Permission |
|---|---|---|
| POST | `/api/quotations/{id}/confirm` | `Quotation.Update` (owner) hoặc `Quotation.Admin` |
| POST | `/api/quotations/{id}/cancel` | `Quotation.Update` nếu chưa Confirmed; thêm `Quotation.CancelConfirmed` nếu đã Confirmed |
| POST | `/api/quotations/{id}/mark-sent` | `Quotation.Update` |
| GET | `/api/reports/sales-revenue?from=&to=&saleUserId=` | `Reports.SalesRevenue` |

Permission code mới cần seed: `Quotation.CancelConfirmed`, `Reports.SalesRevenue` cho role `ADMIN` và `MANAGER`.

## Migration dữ liệu

- Quotation `Status = 4 (ConvertedToOrder)` → set `Status = 3 (Confirmed)` và `ConfirmedAt = UpdatedAt`.
- Quotation `Status = 3 (Confirmed)` đã tồn tại nhưng chưa có `ConfirmedAt` → backfill `ConfirmedAt = UpdatedAt`.
- Drop check constraint cũ trên cột `Status`, tạo lại với 4 giá trị mới (1, 2, 3, 9).

## Verification

- Integration test: `Confirm_RecordsConfirmedAtAndOwner`, `Cancel_OnConfirmed_RequiresPermission`, `SalesRevenueReport_ExcludesCancelled_AggregatesByOwner`.
- Manual test frontend: tạo báo giá → Sent → Confirmed → kiểm tra báo cáo có doanh thu cho đúng sale.
- Build chỉ project library liên quan, không restart WebApi.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Sale tự confirm tạo doanh thu ảo | Log đầy đủ `ConfirmedByUserId` + `ConfirmedAt`; admin xem được lịch sử; có thể khóa cuối tháng ở phase sau |
| Mất ngữ cảnh BD cũ | Archive thay vì xóa; SUMMARY.md có pointer tới `product-goals.md` |
| Migration làm mất dữ liệu Quotation `Status = 4` | Map sang Confirmed + backfill `ConfirmedAt`, không drop row |

## Open questions

- Báo cáo lợi nhuận theo sale (`GrossProfit / Revenue`) có cần build cùng phase này không? Hiện đang đề xuất giữ nguyên schema (`TotalCost`, `GrossProfit` đã có), build báo cáo lợi nhuận ở phase sau khi business cần.
- Có cần "khóa cuối tháng" để chống chỉnh sửa báo giá Confirmed sau khi đã chốt sổ không? Để open cho future iteration.
- UI cảnh báo khi sale confirm — có cần modal xác nhận "Bạn chắc chắn?" không, hay bấm là confirm luôn?

## Next steps

- Tùy chọn: trigger skill `write-plan` với artifacts này làm input để xây kế hoạch thực thi chi tiết phase-by-phase.
- Cập nhật memory project: ghi nhận pivot scope để các session sau khỏi giả định pipeline cũ.
