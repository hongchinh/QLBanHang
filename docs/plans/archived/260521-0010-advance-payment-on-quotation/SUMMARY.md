# Add AdvancePayment (Tạm ứng) to Quotation

## Goal
Thêm trường `AdvancePayment` (số tiền khách đã đặt cọc/ứng trước) vào Quotation. Trường này được lưu vào DB, hiển thị trong phần Tổng cộng của form báo giá dưới dạng một dòng có thể nhập, kéo theo hiển thị dòng `Còn lại = Tổng cộng - Tạm ứng`. Trường cũng được xuất ra file Excel/PDF.

## Scope
- **In scope:**
  - Backend: domain field, EF configuration, EF migration, DTO, validator, service (create/update/clone/MapToDto)
  - Frontend: types, Zod schema, compute-line utility, TotalsPanel UI (incl. negative balance color), form wiring
  - Excel renderer + template + automated cell-value test
  - Integration tests (persist, update, clone) + frontend unit tests

- **Out of scope:**
  - Hiển thị AdvancePayment trong danh sách báo giá hoặc dashboard
  - Ảnh hưởng đến công thức tính `Total` (Total giữ nguyên)
  - Validation AdvancePayment ≤ Total

## Assumptions
- `Total` không thay đổi công thức — `AdvancePayment` chỉ là trường thông tin/display
- Clone **không** copy `AdvancePayment` (default = 0 — cọc thuộc từng giao dịch cụ thể)
- Dòng "Còn lại" chỉ render khi `advancePayment > 0`
- Excel template `template_baogia.xlsx` cần được mở và sửa tay để thêm 2 dòng

## Risks
- **Migration `defaultValue`:** EF PostgreSQL provider **không tự thêm `defaultValue: 0m`** cho non-nullable decimal. Luôn thêm tay trong migration `Up()` để tránh fail trên DB có data.
- **Template offset shift:** Sau khi thêm 2 dòng vào template Excel, constants `AdvancePaymentRowOffset` / `RemainingBalanceRowOffset` trong renderer phải khớp offset thực. Đo lại sau khi sửa template.
- **Test DB:** Chạy integration test phải dùng DB test riêng (xem memory `feedback_test_db_separation_check.md`).

## Phases
- [x] Phase 01 — backend-domain-migration (M) — `phase-01-backend-domain-migration.md`
- [x] Phase 02 — frontend-core (S) — `phase-02-frontend-core.md`
- [x] Phase 03 — frontend-ui (S) — `phase-03-frontend-ui.md`
- [x] Phase 04 — excel-export (M) — `phase-04-excel-export.md`

## Final Verification
```powershell
# Backend build
cd d:\Projects\QLBanHang\backend
dotnet build src/OrderMgmt.WebApi

# Frontend unit tests
cd d:\Projects\QLBanHang\frontend
npx vitest run src/pages/quotations/utils/compute-line.test.ts

# Integration tests (ensure TEST_DB_CONNECTION points to test DB, not dev DB)
cd d:\Projects\QLBanHang\backend
dotnet test tests/OrderMgmt.IntegrationTests --filter "QuotationCrudTests|QuotationExportTests"
```

## Rollback / Recovery
Migration rollback:
```sql
ALTER TABLE quotations DROP COLUMN advance_payment;
```
Hoặc dùng EF: `dotnet ef migrations remove` (nếu chưa apply lên prod).

Template: giữ bản backup của `template_baogia.xlsx` trước khi sửa.
