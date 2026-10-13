# Phase 05 — Frontend: Tích hợp `CustomerAutocomplete` vào `quotation-form-page`

**Status:** [ ] pending
**Complexity:** M

## Objective

Thay `<Select>` cũ trong block "Thông tin khách hàng" bằng `CustomerAutocomplete`. Thêm trường "Tên khách hàng" (input cho phép sửa tay). Auto-fill `deliveryAddress / deliveryRecipient / deliveryPhone` khi chọn KH **chỉ khi field đang trống**. Cập nhật `toPayload` truyền `customerName`.

## Files

- `frontend/src/pages/quotations/quotation-form-page.tsx`

## Tasks

1. Import:
   ```tsx
   import { CustomerAutocomplete } from '@/components/customer-autocomplete/customer-autocomplete';
   import type { CustomerSearchItem } from '@/features/customers/types';
   import { useCustomer } from '@/features/customers/hooks';
   ```

2. Trong `QuotationFormPage` (outer component), khi `isEdit`, fetch chi tiết KH đã chọn (để hiển thị nếu KH `Inactive`):
   ```tsx
   const selectedCustomerId = quotation?.customerId;
   const { data: selectedCustomer } = useCustomer(selectedCustomerId);
   ```
   Truyền xuống `QuotationFormInner` qua prop `initialSelectedCustomer`.

3. Trong `QuotationFormInner`:
   - Thêm state local `selectedCustomerView` cho autocomplete (đồng bộ với `customerId` + `customerName`):
     ```tsx
     const [selectedCustomerView, setSelectedCustomerView] = useState<{ id: string; name: string } | null>(() => {
       if (initialSelectedCustomer) return { id: initialSelectedCustomer.id, name: initialSelectedCustomer.name };
       return null;
     });
     ```
   - Đồng bộ khi `initialSelectedCustomer` đổi (edit báo giá khác).

4. Thay block `<Controller name="customerId">` ([quotation-form-page.tsx:217-232](../../../frontend/src/pages/quotations/quotation-form-page.tsx#L217)) bằng:
   ```tsx
   <div className="space-y-2 md:col-span-2">
     <Label htmlFor="customerId">Khách hàng *</Label>
     <CustomerAutocomplete
       inputId="customerId"
       inputAriaLabel="Mã đối tượng"
       placeholder="Nhập mã / tên / MST / địa chỉ / SĐT..."
       value={selectedCustomerView}
       errorMessage={form.formState.errors.customerId ? String(form.formState.errors.customerId.message) : undefined}
       onSelect={(c) => handleSelectCustomer(c)}
       onClear={() => handleClearCustomer()}
       onAddNewClick={() => setQuickAddOpen(true)}  // Phase 06
     />
     {isEdit && initialSelectedCustomer?.status === 'Inactive' && (
       <p className="text-xs text-amber-600">Khách hàng đã ngừng sử dụng</p>
     )}
   </div>
   ```

5. Thêm input "Tên khách hàng" ngay dưới autocomplete (cùng Card):
   ```tsx
   <div className="space-y-2 md:col-span-2">
     <Label htmlFor="customerName">Tên khách hàng (in trên báo giá)</Label>
     <Input id="customerName" {...form.register('customerName', {
       onBlur: (e) => {
         if (!e.target.value.trim() && selectedCustomerView) {
           form.setValue('customerName', selectedCustomerView.name, { shouldDirty: true });
         }
       }
     })} />
   </div>
   ```
   Blur revert về master name nếu user clear hẳn (xử lý "Customer.Name empty" risk trong SUMMARY).

6. Handlers:
   ```tsx
   function handleSelectCustomer(c: CustomerSearchItem) {
     form.setValue('customerId', c.id, { shouldDirty: true, shouldValidate: true });
     form.setValue('customerName', c.name, { shouldDirty: true });
     setSelectedCustomerView({ id: c.id, name: c.name });

     // Auto-fill delivery* CHỈ khi field đang trống
     const cur = form.getValues();
     if (!cur.deliveryAddress?.trim()) {
       form.setValue('deliveryAddress', c.defaultShippingAddress ?? c.companyAddress ?? '', { shouldDirty: true });
     }
     if (!cur.deliveryRecipient?.trim()) {
       form.setValue('deliveryRecipient', c.contactPerson ?? '', { shouldDirty: true });
     }
     if (!cur.deliveryPhone?.trim()) {
       form.setValue('deliveryPhone', c.phoneNumber ?? '', { shouldDirty: true });
     }

     // Focus sang input Tên khách hàng (BD §10, AC-OBJ-021)
     setTimeout(() => document.getElementById('customerName')?.focus(), 0);
   }

   function handleClearCustomer() {
     form.setValue('customerId', '', { shouldDirty: true, shouldValidate: true });
     form.setValue('customerName', '', { shouldDirty: true });
     setSelectedCustomerView(null);
     // KHÔNG clear delivery* — user có thể đã sửa tay (BD §11 spirit)
   }
   ```

7. Cập nhật `toFormDefaults`:
   ```tsx
   customerId: q?.customerId ?? '',
   customerName: q?.customerName ?? '',
   ```

8. Cập nhật `toPayload`:
   ```tsx
   customerId: parsed.customerId,
   customerName: parsed.customerName?.trim() || undefined,
   ```

9. State cho Quick-add Dialog (placeholder cho Phase 06):
   ```tsx
   const [quickAddOpen, setQuickAddOpen] = useState(false);
   ```
   (Render Dialog ở Phase 06.)

## Verification

```powershell
cd d:\Projects\QLBanHang\frontend
npm run typecheck
npm run lint
npm run dev  # local manual check
```

**Manual e2e** (dev server):
1. `/quotations/new`: gõ "k" → dropdown mở; chọn 1 KH bằng chuột → input hiển thị tên KH, "Tên khách hàng" tự fill, delivery* tự fill (vì đang trống).
2. Đổi sang KH khác → tên KH update; delivery* KHÔNG ghi đè nếu user đã sửa tay (xóa text trước, gõ tay, rồi đổi KH).
3. Sửa tay "Tên khách hàng" → "Bên A Customer", submit → toast "Đã tạo báo giá"; reload edit → "Tên khách hàng" hiển thị "Bên A Customer".
4. Edit báo giá cũ với KH `Inactive` → autocomplete hiển thị tên + warning "Khách hàng đã ngừng sử dụng".

## Exit Criteria

- Typecheck + lint pass.
- 4 manual e2e scenarios pass.
- Submit báo giá thành công, payload chứa `customerName` (kiểm tra Network tab).
- Không regression: TotalsPanel, LineItemsGrid, status transitions vẫn hoạt động.
