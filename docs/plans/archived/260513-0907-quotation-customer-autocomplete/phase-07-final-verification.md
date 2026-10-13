# Phase 07 — Verification cuối: PDF + a11y + AC §16 BD

**Status:** [ ] pending
**Complexity:** S

## Objective

Đảm bảo toàn bộ tích hợp chạy đúng end-to-end: PDF in dùng `customerName` snapshot, a11y combobox đúng chuẩn WAI-ARIA, và tick checklist 33 AC trong BD §16.

## Files

- Không có file code thay đổi (chỉ verify).
- (optional) `frontend/src/components/customer-autocomplete/customer-autocomplete.test.tsx` (bổ sung test nếu phát hiện gap).

## Tasks

### A. PDF verification

1. Tạo báo giá mới qua UI với:
   - Chọn KH "Công ty TNHH ABC".
   - Sửa "Tên khách hàng (in trên báo giá)" thành "CTY ABC (Display)".
2. Save → mở chi tiết → "In" → download PDF.
3. Mở PDF, verify "Đơn vị: CTY ABC (Display)" (KHÔNG phải "Công ty TNHH ABC").
4. Tạo báo giá khác, KHÔNG sửa "Tên khách hàng", verify PDF hiển thị tên master.

### B. A11y check

1. Mở DevTools → Elements → autocomplete input.
2. Kiểm tra:
   - Input có `role="combobox"`, `aria-expanded`, `aria-controls`, `aria-autocomplete="list"`.
   - Khi dropdown mở: input có `aria-activedescendant` đúng id của row đang highlight.
   - Dropdown có `role="listbox"`, mỗi row có `role="option"`, `aria-selected`.
3. Test screen reader (optional, nếu có NVDA/VoiceOver): mở dropdown, navigate bằng Arrow → reader đọc tên KH đang highlight.
4. Test focus trap: Tab khi dropdown mở giữ focus trong field; Esc đóng → focus quay về input.

### C. AC §16 BD checklist

Đi qua từng AC trong [docs/bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md §16](../../bd/phan-tich-hanh-vi-4-2-ma-doi-tuong.md), tick ✓/✗:

| AC | Tình huống verify |
|---|---|
| AC-OBJ-001 | Submit báo giá không chọn KH → error "Chọn khách hàng" |
| AC-OBJ-002 | Focus input rỗng → KHÔNG mở dropdown |
| AC-OBJ-003 | Focus input rỗng → KHÔNG hiển thị recent/common list |
| AC-OBJ-004 | Gõ "k" → dropdown mở, gọi API |
| AC-OBJ-005 | Tạo 1 KH `Inactive` → search "Inactive Name" → không thấy |
| AC-OBJ-006 | Search trả về chỉ Customer (BD §1 — N/A vì backend chỉ có Customer) |
| AC-OBJ-007 | Dropdown hiển thị đủ 5+1 cột |
| AC-OBJ-008 | Search SĐT → trả KH có SĐT trùng (cover trong Phase 01 test) |
| AC-OBJ-009 | Gõ "cong" (không dấu) → trả KH "Công" (Phase 01 test 4) |
| AC-OBJ-010 | Click row → chọn |
| AC-OBJ-011 | Arrow + Enter chọn |
| AC-OBJ-012 | Arrow Up/Down di chuyển highlight |
| AC-OBJ-013 | Tab khi dropdown mở → highlight tiếp |
| AC-OBJ-014 | Tab ở dòng cuối → vòng dòng đầu |
| AC-OBJ-015 | Enter chọn highlight |
| AC-OBJ-016 | Gõ đúng mã (vd "KH-260101-0001"), 1 kết quả → Enter chọn |
| AC-OBJ-017 | Esc đóng dropdown, giữ keyword |
| AC-OBJ-018, 019 | Sau chọn → fill Tên KH + delivery address (khi trống) |
| AC-OBJ-020 | N/A — không có "Lý do nộp" |
| AC-OBJ-021 | Sau chọn → focus sang Tên KH |
| AC-OBJ-022 | Tên KH cho sửa tay |
| AC-OBJ-023 | Gõ text không chọn từ dropdown → submit fails |
| AC-OBJ-024 | Submit thiếu KH → focus quay về autocomplete + báo lỗi |
| AC-OBJ-025 | Icon "+" cạnh input (không trong dropdown) |
| AC-OBJ-026 | Click "+" → Dialog mở |
| AC-OBJ-027 | N/A — chỉ có Customer |
| AC-OBJ-028 | Tạo KH mới qua Dialog → tự chọn vào báo giá |
| AC-OBJ-029 | N/A — báo giá lines độc lập KH |
| AC-OBJ-030 | N/A — không có "Lý do nộp" |
| AC-OBJ-031 | KH Inactive không hiển thị trong search dropdown |
| AC-OBJ-032 | Stop API server → search → hiển thị lỗi, giữ keyword |
| AC-OBJ-033 | Gõ nhanh 5 ký tự liên tiếp → debounce, chỉ 1 request gửi đi |

### D. Run full verification

```powershell
# Backend
dotnet build d:\Projects\QLBanHang\backend\src\OrderMgmt.Application
dotnet test d:\Projects\QLBanHang\backend\tests\OrderMgmt.IntegrationTests\OrderMgmt.IntegrationTests.csproj

# Frontend
cd d:\Projects\QLBanHang\frontend
npm run typecheck
npm run lint
npm test
npm run build  # ensure production build OK
```

## Exit Criteria

- Backend: tất cả integration test pass (kể cả test mới ở Phase 01 + 02).
- Frontend: typecheck + lint + test + build pass.
- PDF render đúng `customerName` snapshot.
- A11y check pass (4 attrs cơ bản đúng).
- 30/33 AC pass; 3 AC N/A đã ghi rõ lý do (027, 029, 030).
- Đã ghi nhận note nào còn gap (vd: AC-OBJ-033 timing-sensitive) để theo dõi tiếp.
