# Phase 03 — Frontend: API client + hook + schema

**Status:** [ ] pending
**Complexity:** S

## Objective

Wiring lớp data cho FE: thêm type `CustomerSearchItem`, `customersApi.search`, hook `useCustomersSearch`, và mở rộng zod schema báo giá với field `customerName`.

## Files

- `frontend/src/features/customers/types.ts`
- `frontend/src/features/customers/api.ts`
- `frontend/src/features/customers/keys.ts`
- `frontend/src/features/customers/hooks.ts`
- `frontend/src/features/quotations/schema.ts`
- `frontend/src/features/quotations/types.ts`

## Tasks

1. Trong `customers/types.ts`, thêm:
   ```ts
   export interface CustomerSearchItem {
     id: string;
     code: string;
     name: string;
     taxCode?: string;
     companyAddress?: string;
     defaultShippingAddress?: string;
     contactPerson?: string;
     phoneNumber?: string;
     status: CustomerStatus;
   }
   export interface CustomerSearchParams {
     keyword: string;
     activeOnly?: boolean;
     limit?: number;
   }
   ```

2. Trong `customers/api.ts`, thêm:
   ```ts
   search: (params: CustomerSearchParams) =>
     apiGet<CustomerSearchItem[]>('/customers/search', {
       keyword: params.keyword,
       activeOnly: params.activeOnly ?? true,
       limit: params.limit ?? 20,
     }),
   ```

3. Trong `customers/keys.ts`, thêm:
   ```ts
   search: (params: CustomerSearchParams) => [...customerKeys.all, 'search', params] as const,
   ```
   (Xác nhận pattern `customerKeys.all` khi đọc file thực tế.)

4. Trong `customers/hooks.ts`, thêm hook:
   ```ts
   export function useCustomersSearch(keyword: string, opts?: { activeOnly?: boolean; limit?: number }) {
     const trimmed = keyword.trim();
     return useQuery({
       queryKey: customerKeys.search({ keyword: trimmed, ...opts }),
       queryFn: () => customersApi.search({ keyword: trimmed, ...opts }),
       enabled: trimmed.length > 0,
       staleTime: 30_000,
       placeholderData: keepPreviousData,
     });
   }
   ```

5. Trong `quotations/schema.ts` ([schema.ts:24-37](../../../frontend/src/features/quotations/schema.ts#L24)), thêm field `customerName` sau `customerId`:
   ```ts
   customerId: z.string().uuid('Chọn khách hàng'),
   customerName: optionalString(255),
   quotationDate: z.string().min(1, 'Chọn ngày báo giá'),
   ...
   ```

6. Trong `quotations/types.ts`, thêm `customerName?: string` vào:
   - `Quotation` (đã có `customerId`, thêm bên cạnh)
   - `UpsertQuotationRequest`

## Verification

```powershell
cd d:\Projects\QLBanHang\frontend
npm run typecheck
```

- `tsc --noEmit` 0 errors.
- Browser DevTools (nếu app dev đang chạy): không có import lỗi.

## Exit Criteria

- Typecheck pass.
- `useCustomersSearch('cong')` callable từ React DevTools (smoke check ở phase 04).
