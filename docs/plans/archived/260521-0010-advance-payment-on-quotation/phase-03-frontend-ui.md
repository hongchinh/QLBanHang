# Phase 03 — Frontend UI: Form Page + TotalsPanel

**Status:** [ ] pending
**Complexity:** S

## Objective
Nối `advancePayment` vào React form và hiển thị 2 dòng mới trong `TotalsPanel`: dòng "Tạm ứng" (input có thể nhập) và dòng "Còn lại" (display, chỉ hiện khi advancePayment > 0, tô đỏ khi âm).

## Files
- `frontend/src/pages/quotations/quotation-form-page.tsx`
- `frontend/src/pages/quotations/components/totals-panel.tsx`

## Tasks

### 1. quotation-form-page.tsx — useWatch cho advancePayment
Tìm khối `useWatch` (~dòng 197–202). Sau dòng `watchedFreight`, thêm:
```ts
const watchedAdvancePayment = useWatch({ control: form.control, name: 'advancePayment' }) as number | undefined;
```

### 2. quotation-form-page.tsx — cập nhật header object
Tìm khối `const header: HeaderLike` (~dòng 348–352). Thêm field mới:
```ts
const header: HeaderLike = {
  taxRate: Number(watchedTaxRate ?? 0) || 0,
  discount: Number(watchedDiscount ?? 0) || 0,
  freight: Number(watchedFreight ?? 0) || 0,
  advancePayment: Number(watchedAdvancePayment ?? 0) || 0,
};
```

### 3. quotation-form-page.tsx — cập nhật onHeaderChange
Tìm function `onHeaderChange` (~dòng 354–358). Thêm handler cho `advancePayment`:
```ts
const onHeaderChange = (patch: Partial<HeaderLike>) => {
  if (patch.discount !== undefined) form.setValue('discount', patch.discount as never, { shouldDirty: true });
  if (patch.freight !== undefined) form.setValue('freight', patch.freight as never, { shouldDirty: true });
  if (patch.taxRate !== undefined) form.setValue('taxRate', patch.taxRate as never, { shouldDirty: true });
  if (patch.advancePayment !== undefined) form.setValue('advancePayment', patch.advancePayment as never, { shouldDirty: true });
};
```

### 4. quotation-form-page.tsx — cập nhật toFormDefaults
Tìm function `toFormDefaults` (~dòng 838–882). Trong return object, sau dòng `freight: (q?.freight ?? 0) as number,`, thêm:
```ts
advancePayment: (q?.advancePayment ?? 0) as number,
```

### 5. quotation-form-page.tsx — cập nhật toPayload
Tìm function `toPayload` (~dòng 904–941). Trong return object, sau dòng `freight: parsed.freight,`, thêm:
```ts
advancePayment: parsed.advancePayment,
```

### 6. totals-panel.tsx — làm `label` optional trong EditableMetric
`EditableMetric` luôn render `<Label>` kể cả khi `label=""`, gây empty element với `mb-1` margin thừa. Cập nhật interface và render có điều kiện:

```tsx
interface EditableMetricProps {
  id: string;
  label?: string;  // optional — trước là bắt buộc
  value: number;
  onChange: (value: number) => void;
}

function EditableMetric({ id, label, value, onChange }: EditableMetricProps) {
  // ...
  return (
    <div className="min-w-0">
      {label && (
        <Label htmlFor={id} className="mb-1 block text-xs text-muted-foreground">
          {label}
        </Label>
      )}
      <Input ... />
    </div>
  );
}
```

Các call site hiện tại (CK, VC, Thuế %) đều truyền `label` có giá trị — không bị ảnh hưởng.

### 7. totals-panel.tsx — thêm prop `negative` vào MetricValue
Để hỗ trợ tô đỏ khi "Còn lại" âm, cập nhật `MetricValueProps` và component:

```tsx
interface MetricValueProps {
  value: string;
  bold?: boolean;
  large?: boolean;
  negative?: boolean;  // mới
}

function MetricValue({ value, bold, large, negative }: MetricValueProps) {
  return (
    <span
      className={[
        'block truncate tabular-nums',
        bold ? 'font-bold' : '',
        large ? 'text-base' : 'text-sm',
        negative ? 'text-destructive' : '',
      ].join(' ')}
    >
      {value}
    </span>
  );
}
```

### 8. totals-panel.tsx — thêm dòng Tạm ứng
Tìm khối `SummaryRow label="Tổng cộng"` (~dòng 61–71). Sau khi đóng `</SummaryRow>` của khối đó, thêm:

```tsx
<SummaryRow label="Tạm ứng">
  <EditableMetric
    id="advancePayment"
    value={header.advancePayment}
    onChange={(value) => onHeaderChange({ advancePayment: value })}
  />
</SummaryRow>
```

`label` bị bỏ qua — `EditableMetric` sẽ không render `<Label>` nhờ fix ở Task 6.

### 9. totals-panel.tsx — thêm dòng Còn lại
Ngay sau dòng "Tạm ứng" vừa thêm, thêm dòng "Còn lại":

```tsx
{header.advancePayment > 0 && (
  <SummaryRow label="Còn lại" emphasized>
    <div className="min-w-0 text-right">
      <MetricValue
        value={fmt.format(totals.remainingBalance)}
        bold
        large
        negative={totals.remainingBalance < 0}
      />
    </div>
  </SummaryRow>
)}
```

Dòng "Còn lại" chỉ hiển thị khi `advancePayment > 0`. Giá trị tô đỏ khi `remainingBalance < 0` (tạm ứng vượt tổng cộng).

**Layout sau khi thêm:**
1. Tiền hàng
2. Điều chỉnh (CK + VC)
3. Thuế
4. Tổng cộng ← emphasized (border-t)
5. Tạm ứng
6. Còn lại ← emphasized (border-t), chỉ hiện khi advancePayment > 0

## Verification
1. Chạy dev server, mở form tạo báo giá mới
2. Kiểm tra dòng "Tạm ứng" hiển thị dưới "Tổng cộng", không có label phụ phía trên input
3. Nhập giá trị → dòng "Còn lại" xuất hiện = Tổng cộng - Tạm ứng
4. Nhập tạm ứng > tổng cộng → "Còn lại" tô đỏ
5. Xóa về 0 → dòng "Còn lại" biến mất
6. Lưu → reload → tạm ứng giữ nguyên

```powershell
cd d:\Projects\QLBanHang\frontend
npm run dev
```

## Exit Criteria
- Dòng "Tạm ứng" hiển thị và có thể nhập, không có empty label phụ
- Dòng "Còn lại" chỉ hiển thị khi advancePayment > 0
- "Còn lại" tô đỏ khi giá trị âm
- Giá trị persist qua save/reload
- TypeScript build không có errors: `npm run build`
