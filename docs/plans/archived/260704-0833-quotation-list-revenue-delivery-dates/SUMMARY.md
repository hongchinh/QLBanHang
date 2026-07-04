# Thêm cột Ngày doanh thu & Ngày Giao vào danh sách báo giá

## Goal

Thêm hai cột **"Ngày doanh thu"** và **"Ngày Giao"** vào grid danh sách báo giá, đồng thời bổ sung bộ lọc ngày tương ứng trong khu vực filter. "Ngày doanh thu" là trường động phụ thuộc vào `QuotationSystemSettings.RevenueReportingDateField`; backend tự tính toán và trả về field `RevenueDate` trong list item DTO.

## Scope

- **In scope:**
  - Thêm `DeliveryDate` và `RevenueDate` vào `QuotationListItemDto`
  - Thêm `RevenueDateFrom/To` và `DeliveryDateFrom/To` vào `QuotationListRequest` + filter logic trong service
  - Cập nhật frontend types, API layer, hooks
  - Thêm 2 cột trong grid (sau cột "Ngày")
  - Thêm 2 bộ lọc ngày (dạng pill + custom giống `QuotationDateFilter` hiện tại)
  - Integration tests cho backend filter mới

- **Out of scope:**
  - Thay đổi logic tính toán doanh thu
  - Thay đổi `QuotationSystemSettings` UI
  - Frontend unit tests (project hiện không có test frontend được chạy trong CI)

## Assumptions

- `ConfirmedAt` và `AccountingConfirmedAt` trên entity là `DateTime?` (UTC); khi convert sang `DateOnly` để hiển thị dùng `.Date` phần `DateTime` (không cần timezone conversion — nhất quán với cách `QuotationDashboardService` đang làm).
- Khi filter `RevenueDateFrom/To`, chỉ lọc record có giá trị RevenueDate không null (record chưa qua trạng thái tương ứng sẽ bị loại khỏi kết quả filter — nhất quán với dashboard).
- Filter area có thể xuống dòng tự nhiên do `flex-wrap` đã có sẵn — không cần tái cấu trúc layout.
- Không cần validation đặc biệt cho `RevenueDateFrom/To` và `DeliveryDateFrom/To` (ASP.NET model binding tự parse `DateOnly` từ query string `yyyy-MM-dd`).

## Risks

- `QuotationService.ListAsync` cần đọc thêm `QuotationSystemSettings` — thêm 1 DB query cho mỗi list call. Đây là bảng settings 1 row, impact không đáng kể.
- EF Core projection với `DateOnly.FromDateTime(dateTime.Value)` trong LINQ có thể không dịch sang SQL — cần kiểm tra khi implement. Phương án dự phòng: lấy `DateTime?` rồi convert ở application layer.

## Phases

- [x] Phase 01 — Backend DTO, Service, Tests (M) — `phase-01-backend.md`
- [x] Phase 02 — Frontend types, API, Grid, Filters (M) — `phase-02-frontend.md`

## Final Verification

```bash
# Backend tests
cd backend && dotnet test tests/OrderMgmt.IntegrationTests --filter "ClassName~QuotationListFilter" --no-build

# Frontend type check
cd frontend && npx tsc --noEmit
```

## Rollback / Recovery

Tất cả thay đổi là additive (thêm field mới, thêm filter mới). Rollback bằng cách revert commit của từng phase.
