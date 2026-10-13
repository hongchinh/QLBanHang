# Phase 02 — Frontend Core: Types, Schema, Compute-Line

**Status:** [ ] pending
**Complexity:** S

## Objective
Cập nhật các type definition, Zod schema, và hàm `computeTotals` để bao gồm `advancePayment` và `remainingBalance`. Cập nhật existing tests để khớp interface mới, thêm unit tests cho behavior mới.

## Files
- `frontend/src/features/quotations/types.ts`
- `frontend/src/features/quotations/schema.ts`
- `frontend/src/pages/quotations/utils/compute-line.ts`
- `frontend/src/pages/quotations/utils/compute-line.test.ts`

## Tasks

### 1. types.ts — Quotation interface
Mở `types.ts`. Trong interface `Quotation`, sau dòng `total: number;` (dòng 64), thêm:
```ts
advancePayment: number;
```

### 2. types.ts — UpsertQuotationRequest interface
Trong interface `UpsertQuotationRequest` (dòng 157–172), sau dòng `freight: number;`, thêm:
```ts
advancePayment: number;
```

### 3. schema.ts — quotationSchema
Mở `schema.ts`. Trong `quotationSchema` (~dòng 35–50), sau dòng `freight: z.coerce.number().min(0),`, thêm:
```ts
advancePayment: z.coerce.number().min(0),
```

### 4. compute-line.ts — HeaderLike interface
Mở `compute-line.ts`. Trong interface `HeaderLike` (dòng 15–19), thêm:
```ts
advancePayment: number;
```

### 5. compute-line.ts — Totals interface
Trong interface `Totals` (dòng 55–61), thêm:
```ts
remainingBalance: number;
```

### 6. compute-line.ts — computeTotals function
Trong function `computeTotals` (dòng 63–70), cập nhật thành:
```ts
export function computeTotals(lines: LineLike[], header: HeaderLike): Totals {
  const subtotal = lines.reduce((sum, l) => sum + computeLineTotal(l), 0);
  const totalCost = lines.reduce((sum, l) => sum + (computeLineCost(l) ?? 0), 0);
  const taxAmount = round0((subtotal * header.taxRate) / 100);
  const total = subtotal - header.discount + header.freight + taxAmount;
  const grossProfit = subtotal - totalCost - header.discount;
  const remainingBalance = total - header.advancePayment;
  return { subtotal, totalCost, taxAmount, total, grossProfit, remainingBalance };
}
```

### 7. compute-line.test.ts — cập nhật existing test calls
Mở `compute-line.test.ts`. `HeaderLike` hiện có thêm field `advancePayment` bắt buộc — TypeScript sẽ error nếu không cập nhật. Tìm **tất cả** các lời gọi `computeTotals(lines, { taxRate, discount, freight })` và thêm `advancePayment: 0`:

```ts
// Trước:
computeTotals(lines, { taxRate: 10, discount: 10000, freight: 5000 })
// Sau:
computeTotals(lines, { taxRate: 10, discount: 10000, freight: 5000, advancePayment: 0 })
```

Có 3 lời gọi cần cập nhật trong file (~dòng 75–95).

### 8. compute-line.test.ts — thêm tests cho remainingBalance
Sau test `'tax rounds to integer VND'` (~dòng 97–103), thêm 2 test cases mới:

```ts
it('totals: remainingBalance = total - advancePayment', () => {
  const totals = computeTotals(
    [{ pricingMode: 'PerUnit', quantity: 5, unitPrice: 12000 }],
    { taxRate: 0, discount: 0, freight: 0, advancePayment: 20000 },
  );
  expect(totals.total).toBe(60000);
  expect(totals.remainingBalance).toBe(40000);
});

it('totals: remainingBalance = total when advancePayment = 0', () => {
  const totals = computeTotals(
    [{ pricingMode: 'PerUnit', quantity: 1, unitPrice: 100000 }],
    { taxRate: 0, discount: 0, freight: 0, advancePayment: 0 },
  );
  expect(totals.remainingBalance).toBe(100000);
});
```

## Verification
```powershell
cd d:\Projects\QLBanHang\frontend
npx vitest run src/pages/quotations/utils/compute-line.test.ts
```

## Exit Criteria
- Tất cả unit tests trong `compute-line.test.ts` pass (bao gồm 2 test mới)
- Không có TypeScript errors khi build: `npm run build`
