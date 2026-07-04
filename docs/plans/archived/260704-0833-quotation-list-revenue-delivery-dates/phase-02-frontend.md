# Phase 02 — Frontend types, API, Grid, Filters

**Status:** [ ] pending
**Complexity:** M

## Objective

Cập nhật types, API layer, hooks; thêm 2 cột "Ngày doanh thu" và "Ngày Giao" vào grid; thêm 2 bộ lọc ngày kiểu pill vào filter area.

## Files

- `frontend/src/features/quotations/types.ts`
- `frontend/src/features/quotations/api.ts`
- `frontend/src/pages/quotations/quotation-list-page.tsx`

## Tasks

### Task 1 — Cập nhật types

1. **Kiểm tra compile hiện tại:**
   ```bash
   cd frontend && npx tsc --noEmit 2>&1 | head -20
   ```
   Expected: 0 errors (baseline sạch).

2. **Implement trong `frontend/src/features/quotations/types.ts`:**

   Trong interface `QuotationListItem` (sau `createdAt`), thêm:
   ```ts
   deliveryDate?: string;
   revenueDate?: string;
   ```

   Trong interface `QuotationListParams` (sau `ownerUserIds`), thêm:
   ```ts
   revenueDateFrom?: string;
   revenueDateTo?: string;
   deliveryDateFrom?: string;
   deliveryDateTo?: string;
   ```

3. **Verify compile:** `cd frontend && npx tsc --noEmit`
   Expected: 0 errors.

4. **Commit:**
   ```
   git add frontend/src/features/quotations/types.ts
   git commit -m "feat: add deliveryDate, revenueDate fields and filter params to quotation types"
   ```

---

### Task 2 — Cập nhật API layer

1. **Kiểm tra:** Mở `frontend/src/features/quotations/api.ts`. Method `list` hiện destructure `{ statuses, ownerUserIds, ...rest }` rồi spread `rest` vào serialized — 4 params mới (`revenueDateFrom`, `revenueDateTo`, `deliveryDateFrom`, `deliveryDateTo`) sẽ tự động được pass qua `...rest` mà không cần sửa thêm.

2. **Verify:** Chạy `cd frontend && npx tsc --noEmit` — Expected: 0 errors. Không cần thay đổi `api.ts`.

   > Nếu backend query param binding cần snake_case hay tên khác, sửa tại đây. Hiện tại backend dùng `[FromQuery]` với camelCase → ASP.NET model binding tự match case-insensitively.

---

### Task 3 — Thêm 2 cột vào grid

1. **Không có automated test** — kiểm tra bằng TypeScript compile + manual review.

2. **Implement trong `quotation-list-page.tsx`:**

   Trong mảng `columns` (bên trong `useMemo`), sau cột `quotationDate` (dòng ~372), thêm 2 cột mới:
   ```tsx
   {
     header: 'N. Doanh thu',
     accessorKey: 'revenueDate',
     cell: ({ row }) => formatDate(row.original.revenueDate),
   },
   {
     header: 'Ngày Giao',
     accessorKey: 'deliveryDate',
     cell: ({ row }) => formatDate(row.original.deliveryDate),
   },
   ```

   > `formatDate` đã có sẵn trong file, nhận `string | undefined` và trả `''` nếu null/undefined — hiển thị ô trống khi chưa có ngày, đúng với requirement.

3. **Verify compile:** `cd frontend && npx tsc --noEmit`
   Expected: 0 errors.

4. **Commit:**
   ```
   git add frontend/src/pages/quotations/quotation-list-page.tsx
   git commit -m "feat: add N.DoanThu and NgayGiao columns to quotation list grid"
   ```

---

### Task 4 — Thêm search params và wire filter vào `useQuotations` call

1. **Implement trong `quotation-list-page.tsx`:**

   Thêm 4 read-only search param state (sau khai báo `[ownerIdsParam, setOwnerIdsParam]`, khoảng dòng 295). Không cần setter riêng — dùng lại `setDateRangeParams` đã có ở dòng 294:
   ```tsx
   const [revDateFrom] = useSearchParamString('revDateFrom');
   const [revDateTo] = useSearchParamString('revDateTo');
   const [dlvDateFrom] = useSearchParamString('dlvDateFrom');
   const [dlvDateTo] = useSearchParamString('dlvDateTo');
   ```

   Trong `useQuotations` call, thêm 4 param mới (sau `to`):
   ```tsx
   revenueDateFrom: revDateFrom || undefined,
   revenueDateTo: revDateTo || undefined,
   deliveryDateFrom: dlvDateFrom || undefined,
   deliveryDateTo: dlvDateTo || undefined,
   ```

2. **Verify compile:** `cd frontend && npx tsc --noEmit`
   Expected: 0 errors.

3. **Commit:**
   ```
   git add frontend/src/pages/quotations/quotation-list-page.tsx
   git commit -m "feat: wire revenue date and delivery date filter params to quotation list query"
   ```

---

### Task 5 — Thêm 2 bộ lọc ngày vào filter area

1. **Implement trong `quotation-list-page.tsx`:**

   Trong filter area (sau `<QuotationDateFilter ... />` hiện tại, khoảng dòng 569), thêm 2 bộ lọc mới với label:

   ```tsx
   <div className="flex flex-col gap-0.5">
     <span className="text-xs text-muted-foreground leading-none">N. Doanh thu</span>
     <QuotationDateFilter
       from={revDateFrom}
       to={revDateTo}
       onChange={(f, t) => {
         setDateRangeParams((prev) => {
           const out = new URLSearchParams(prev);
           if (!f) out.delete('revDateFrom'); else out.set('revDateFrom', f);
           if (!t) out.delete('revDateTo'); else out.set('revDateTo', t);
           out.delete('page');
           return out;
         }, { replace: true });
       }}
     />
   </div>

   <div className="flex flex-col gap-0.5">
     <span className="text-xs text-muted-foreground leading-none">Ngày Giao</span>
     <QuotationDateFilter
       from={dlvDateFrom}
       to={dlvDateTo}
       onChange={(f, t) => {
         setDateRangeParams((prev) => {
           const out = new URLSearchParams(prev);
           if (!f) out.delete('dlvDateFrom'); else out.set('dlvDateFrom', f);
           if (!t) out.delete('dlvDateTo'); else out.set('dlvDateTo', t);
           out.delete('page');
           return out;
         }, { replace: true });
       }}
     />
   </div>
   ```

   Thêm label tương tự cho filter Ngày báo giá hiện tại (wrap trong `<div className="flex flex-col gap-0.5">`):
   ```tsx
   <div className="flex flex-col gap-0.5">
     <span className="text-xs text-muted-foreground leading-none">Ngày báo giá</span>
     <QuotationDateFilter
       from={fromDate}
       to={toDate}
       onChange={(f, t) => { ... }} {/* giữ nguyên logic hiện tại */}
     />
   </div>
   ```

2. **Verify compile:** `cd frontend && npx tsc --noEmit`
   Expected: 0 errors.

3. **Commit:**
   ```
   git add frontend/src/pages/quotations/quotation-list-page.tsx
   git commit -m "feat: add N.DoanThu and NgayGiao date filters to quotation list"
   ```

## Verification

```bash
cd frontend && npx tsc --noEmit
```

Expected: 0 type errors.

Manual verification:
1. Chạy dev server (`npm run dev`).
2. Mở trang `/quotations` — kiểm tra 2 cột mới hiển thị sau cột "Ngày".
3. Kiểm tra cột "N. Doanh thu": với báo giá chưa xác nhận hiển thị ô trống; với báo giá đã xác nhận (hoặc đang ở mode `QuotationDate`) hiển thị giá trị đúng.
4. Kiểm tra cột "Ngày Giao": với báo giá có `DeliveryDate` hiển thị ngày; báo giá không có `DeliveryDate` hiển thị ô trống.
5. Kiểm tra 2 bộ lọc mới xuất hiện, chọn range → grid reload và filter đúng.
6. Kiểm tra reset filter ("Tất cả") xóa filter khỏi URL params.

## Exit Criteria

- TypeScript compile 0 errors.
- Grid hiển thị 2 cột mới "N. Doanh thu" và "Ngày Giao" sau cột "Ngày".
- Filter area có 3 bộ lọc ngày với label rõ ràng.
- Chọn filter N. Doanh thu hoặc Ngày Giao gửi đúng query params lên API và grid reload.
