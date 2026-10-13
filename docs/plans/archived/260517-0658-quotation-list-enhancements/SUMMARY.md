# Quotation List Page — 3 Enhancements

## Goal
Cải tiến `frontend/src/pages/quotations/quotation-list-page.tsx` với 3 hạng mục đã chốt qua brainstorm (xem `docs/brainstorms/...` nếu có): hiển thị chi tiết tài chính (Subtotal/Discount/Freight bên cạnh Total), gom action buttons (Clone/In PDF/Hủy) vào dropdown `⋯`, và cho phép multi-select status filter với URL `?status=Draft,Sent`.

## Scope

**In scope:**
1. Backend: mở rộng `QuotationListItemDto` với 3 field `Subtotal/Discount/Freight`; nhận `?status=Draft,Sent` (comma-separated) và filter bằng `Contains`.
2. Frontend: thêm 3 cột tài chính vào bảng; refactor cột actions sang button Sửa + dropdown `⋯`; thay `Select` status bằng component `MultiSelect` mới.
3. Component `frontend/src/components/ui/multi-select.tsx` generic, dùng lại `DropdownMenu + DropdownMenuCheckboxItem` có sẵn (không thêm Radix Popover dep).
4. Bổ sung 1 integration test cho multi-status filter.

**Out of scope:**
- Column visibility toggle (ẩn/hiện cột).
- Sort theo các cột mới (Subtotal/Discount/Freight).
- Footer summary "Tổng Subtotal trang".
- Tương thích với client API khác ngoài FE này (chỉ FE QLBanHang gọi `/quotations`).
- Responsive design cho bảng (chấp nhận horizontal scroll trên màn hình hẹp).
- Sửa semantic của `QuotationListItemDto.CanClone`. Hiện `CanClone = isOwnerDeleted` (chỉ true khi báo giá orphan), nên dropdown `⋯` cho báo giá thường thực chất chỉ chứa 2 mục (In PDF + Hủy). Tradeoff được chấp nhận để giữ scope nhỏ; mở rộng semantic cần brainstorm riêng.

## Assumptions
- `Quotation` entity đã có các field `Subtotal`, `Discount`, `Freight` (decimal) — xác nhận qua test `QuotationCrudTests` line 63: `get.Data.Subtotal.Should().Be(200_000m)`.
- Chỉ FE QLBanHang là client của endpoint `/quotations` — không cần giữ tương thích `?status=<SingleValue>` cho client nào khác. Tuy nhiên vẫn giữ hành vi đó như side effect tự nhiên của parse logic (single value vẫn là comma-separated với 1 phần tử).
- Project không có dep `@radix-ui/react-popover` hoặc `@radix-ui/react-checkbox` (xác nhận qua glob). Tránh thêm dep mới — dùng `dropdown-menu.tsx` có sẵn.
- Axios default serializer (qua `URLSearchParams`) sẽ phát ra `?key=a&key=b` (repeated, không brackets) cho array. Backend đang nhận format `?status=Draft,Sent` (comma-separated). → Cần convert array → comma-separated string ở tầng `api.ts` trước khi gọi `apiGet` để chuẩn hoá về 1 format duy nhất, không phụ thuộc axios version.
- `useSearchParamString` (xem `frontend/src/lib/use-search-param-state.ts`) đã tự xoá key khi value rỗng (`if (!next) out.delete(key)`) → gọi `setStatusParam('')` không để lại `?status=` ugly. Verified.

## Risks
- **API contract change**: `QuotationListRequest.Status` đổi type từ `QuotationStatus?` sang `string?`. Vì là query-string binding, không ảnh hưởng JSON serialization — chỉ ảnh hưởng nội bộ. Risk thấp.
- **Backward bookmark**: Bookmark cũ `?status=Draft` vẫn hoạt động (parse logic split `,` cho ra `['Draft']`). Verified case.
- **Edge case backend binding**: Empty string `?status=` cần được treat as no-filter, không phải invalid enum. Plan xử lý trong service.
- **EF translation**: `request.Statuses.Contains(q.Status)` — kiểm chứng EF Core 8 dịch thành SQL `IN (...)`. Đây là pattern chuẩn, không vấn đề.

## Phases
- [x] Phase 01 — Backend DTO + Service + Validator (M) — `phase-01-backend.md`
- [x] Phase 02 — MultiSelect component (S) — `phase-02-multi-select-component.md`
- [x] Phase 03 — Frontend types + api + page integration (M) — `phase-03-frontend-page.md`
- [x] Phase 04 — Integration test cho multi-status filter (S) — `phase-04-integration-test.md` (compile-only)

## Final Verification

**Backend** (chỉ build project thay đổi, không full rebuild — theo memory rule):
```powershell
dotnet build backend/src/OrderMgmt.Application/OrderMgmt.Application.csproj
dotnet build backend/src/OrderMgmt.WebApi/OrderMgmt.WebApi.csproj
dotnet test backend/tests/OrderMgmt.IntegrationTests --filter "FullyQualifiedName~Quotation"
```

**Frontend**:
```powershell
cd frontend ; npm run typecheck
```

**Manual smoke (browser, sau khi restart WebApi)**:
1. Mở `/quotations` → thấy 4 cột tài chính (Tổng tiền hàng / Chiết khấu / Vận chuyển / Tổng tiền) format `vi-VN`.
2. Cột actions: button Sửa (Pencil) hiện riêng, click `⋯` thấy menu chứa Clone (nếu canClone), In PDF, Hủy (nếu canCancel).
3. Filter trạng thái: click → chọn `Draft` + `Sent` → URL update `?status=Draft,Sent` → reload page → state khôi phục đúng.
4. URL legacy `?status=Draft` → mở thẳng → MultiSelect hiển thị 1 mục đã chọn, list filter đúng.
5. Hủy báo giá từ menu dropdown → ConfirmDialog mở → confirm → toast success.

## Rollback / Recovery
4 phase tạo 4 commit độc lập tương đối:
- Nếu vỡ backend → revert commit Phase 01, 04. FE Phase 03 sẽ vỡ vì DTO thiếu field — revert luôn.
- Nếu chỉ vỡ FE → revert Phase 02, 03. Backend không ảnh hưởng (DTO mới chỉ thêm field, BE-only client cũ vẫn hoạt động).
- Component `multi-select.tsx` (Phase 02) là file mới — xóa file + revert import nếu cần.
