# Plan — Customer Autocomplete cho Form Báo giá

> Bắt nguồn từ brainstorm: [docs/brainstorms/260513-0859-quotation-customer-autocomplete/SUMMARY.md](../../brainstorms/260513-0859-quotation-customer-autocomplete/SUMMARY.md)
>
> Spec hành vi: [docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md](../../bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md)

## Goal

Thay block "Thông tin khách hàng" trong [frontend/src/pages/quotations/quotation-form-page.tsx](../../../frontend/src/pages/quotations/quotation-form-page.tsx) bằng autocomplete keyboard-first theo BD §4.2: tìm KH theo `code/name/taxCode/companyAddress/phoneNumber` (có/không dấu), cho phép sửa tay tên KH (snapshot trên chứng từ PDF), auto-fill block giao hàng khi trống, và thêm nhanh KH qua Dialog inline.

## Scope

### In scope
- Backend: endpoint mới `GET /api/customers/search` (unaccent, 5 cột, `activeOnly` mặc định, `limit` clamp).
- Backend: `UpsertQuotationRequest` thêm `CustomerName?`; `QuotationService.Create/Update` fallback `request.CustomerName ?? customer.Name`. Reuse cột `CustomerName` đã có trong entity.
- Frontend: component `CustomerAutocomplete` (debounce 250ms, keyboard nav theo BD §7, table-style dropdown 5 cột + Loại đối tượng) + component test (vitest + RTL).
- Frontend: thêm trường `customerName` vào form báo giá, auto-fill `delivery*` khi field trống.
- Frontend: `CustomerQuickAddDialog` (refactor `customer-form-page` tách `CustomerFormFields` dùng chung).

### Out of scope
- Không thay đổi state machine/transition báo giá.
- Không thêm "Lý do nộp" (BD §10 — không thuộc nghiệp vụ báo giá).
- Không refactor `LineItemsGrid`, `TotalsPanel`.
- Không disable autocomplete theo status (decision §4 brainstorm — luôn cho đổi).
- Không dùng `customerName` cho list-search báo giá.
- Không backfill `Quotation.CustomerName` cũ (đã có sẵn — chỉ thêm khả năng override).

## Assumptions

1. PostgreSQL extension `unaccent` có thể bật bằng migration (cần quyền `CREATE EXTENSION` — DB hiện chạy local dev, OK; production cần verify).
2. Cột `Quotation.CustomerName` hiện tại (required, non-null) được giữ nguyên semantic: snapshot tại thời điểm save. Nếu request không truyền, fallback master như cũ ⇒ backward-compatible với client cũ.
3. `EnsureCustomerAsync` filter `!c.IsDeleted` nhưng KHÔNG filter `Status` ⇒ vẫn cho phép tham chiếu KH `Inactive` đã chọn từ trước. Endpoint `/customers/search` mặc định `activeOnly=true` để dropdown chỉ hiển thị Active.
4. Quick-add Dialog tái sử dụng toàn bộ form fields hiện tại ⇒ không có required-field nào khác giữa quick-add vs trang Danh mục KH.
5. Vitest + RTL + jsdom đã cấu hình (xác minh: [frontend/vite.config.ts:45-50](../../../frontend/vite.config.ts#L45), `frontend/src/test/setup.ts`).

## Risks

| Rủi ro | Mức | Mitigation |
|---|---|---|
| Production DB không có quyền `CREATE EXTENSION unaccent` | M | Migration idempotent (`IF NOT EXISTS`); nếu fail, fallback ILIKE không dấu tạm bỏ; ghi rõ trong release notes |
| `unaccent(...)` không sargable → seq scan trên KH lớn | M | Hiện danh mục KH nhỏ; nếu cần tối ưu sau, tạo functional index `CREATE INDEX ON customers (LOWER(unaccent(name)))` |
| `Quotation.CustomerName` đang `NOT NULL` ⇒ client phải luôn có giá trị | L | Backend giữ fallback: `request.CustomerName?.Trim() ?? customer.Name`. FE luôn fill trước khi submit (blur revert về master nếu rỗng) |
| Nested form trong Quick-add Dialog (form trong form) | L | Dialog portal độc lập; form bên trong dùng `<form>` riêng. Đã verified Radix Dialog không tạo `<form>` wrapper |
| KH `Inactive` được dùng trong báo giá cũ (edit) — không thấy trong search | M | Khi edit báo giá, fetch KH theo ID (allow Inactive), hiển thị tag "Ngừng sử dụng"; chỉ search loại Inactive khỏi dropdown |
| Tab cycle trong dropdown làm user mới ngạc nhiên | L | Đã đồng ý giữ đúng BD §7.2. Thêm tooltip/hint mô tả phím tắt |

## Phases

- [x] Phase 01 — Backend: `/customers/search` + unaccent extension (M) — [phase-01-backend-customers-search.md](phase-01-backend-customers-search.md)
- [x] Phase 02 — Backend: cho phép override `CustomerName` trên báo giá (S) — [phase-02-backend-quotation-name-override.md](phase-02-backend-quotation-name-override.md)
- [x] Phase 03 — Frontend: API client + hook + schema (S) — [phase-03-frontend-search-api.md](phase-03-frontend-search-api.md)
- [x] Phase 04 — Frontend: `CustomerAutocomplete` component + tests (L) — [phase-04-frontend-autocomplete-component.md](phase-04-frontend-autocomplete-component.md)
- [x] Phase 05 — Frontend: tích hợp vào `quotation-form-page` (M) — [phase-05-frontend-quotation-form-integration.md](phase-05-frontend-quotation-form-integration.md)
- [x] Phase 06 — Frontend: Quick-add Dialog (M) — [phase-06-frontend-quick-add-dialog.md](phase-06-frontend-quick-add-dialog.md)
- [x] Phase 07 — Verification cuối: PDF + a11y + AC §16 BD (S) — [phase-07-final-verification.md](phase-07-final-verification.md)

## Final Verification

Sau khi tất cả phase pass, chạy đồng thời:

```powershell
# Backend
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.Application\OrderMgmt.Application.csproj
dotnet test d:\Projects\QLBanHang\backend\tests\OrderMgmt.IntegrationTests\OrderMgmt.IntegrationTests.csproj --filter "FullyQualifiedName~Quotation|FullyQualifiedName~Customer"

# Frontend
cd d:\Projects\QLBanHang\frontend; npm run typecheck; npm run lint; npm test
```

**Manual e2e**:
1. Mở `/quotations/new`, gõ "kh" → dropdown mở, có kết quả; gõ "Cong Ty" và "cong ty" (không dấu) → cùng kết quả.
2. Arrow Down + Enter chọn KH → fill Tên KH, `deliveryAddress/Recipient/Phone` (chỉ khi trống), focus sang input Tên KH.
3. Sửa tay Tên KH → "ABC Display Name" → submit → PDF in "ABC Display Name".
4. Click "+" cạnh ô KH → Dialog mở; thêm KH mới với group=Company; sau Save → autocomplete tự chọn KH vừa tạo, focus sang Tên KH.
5. Edit báo giá cũ với KH `Inactive` → autocomplete hiển thị tên KH + badge "Ngừng sử dụng", không cần search lại.

## Rollback / Recovery

| Trường hợp | Rollback |
|---|---|
| Migration `unaccent` lỗi trên prod | `DROP EXTENSION IF EXISTS unaccent;` + revert migration. Search trở lại ILIKE có dấu — không phá vỡ functionality khác |
| Endpoint `/customers/search` lỗi production | Frontend feature flag (env: `VITE_ENABLE_CUSTOMER_AUTOCOMPLETE`) — fallback về `<Select>` cũ. Khuyến nghị thêm flag trong Phase 05 |
| `CustomerName` override gây inconsistency dữ liệu | Migration không thay schema → revert chỉ cần unset request field ở FE; data đã save vẫn dùng được vì cột vẫn `CustomerName` cũ |
| Quick-add Dialog gây nested form bug | Toggle off icon "+" — autocomplete vẫn hoạt động độc lập |

## Acceptance Criteria mapping (BD §16)

Cover toàn bộ 33 AC trừ:
- AC-OBJ-027 (default Customer): N/A (chỉ có Customer).
- AC-OBJ-029 (đổi KH không update bảng hạch toán): tự nhiên đúng (báo giá lines độc lập với KH).
