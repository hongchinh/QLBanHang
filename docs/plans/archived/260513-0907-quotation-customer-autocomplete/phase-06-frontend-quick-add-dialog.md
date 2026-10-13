# Phase 06 — Frontend: Quick-add Customer Dialog

**Status:** [ ] pending
**Complexity:** M

## Objective

Refactor `customer-form-page` để tách phần form (fields + submit) thành component dùng chung, rồi build `CustomerQuickAddDialog` wrap form đó trong `Dialog`. Khi click icon "+" trong `CustomerAutocomplete` (Phase 05) → mở Dialog; lưu xong tự chọn KH vừa tạo (AC-OBJ-026, 028).

## Files

- `frontend/src/pages/customers/customer-form-page.tsx` (refactor — tách component)
- (new) `frontend/src/pages/customers/customer-form-fields.tsx` (component dùng chung)
- (new) `frontend/src/components/customer-autocomplete/customer-quick-add-dialog.tsx`
- `frontend/src/pages/quotations/quotation-form-page.tsx` (render Dialog + wire)

## Tasks

### Refactor `customer-form-page`

1. Extract `CustomerFormInner` ([customer-form-page.tsx:90-191](../../../frontend/src/pages/customers/customer-form-page.tsx#L90)) thành component dùng chung trong file mới `customer-form-fields.tsx`:
   ```tsx
   export interface CustomerFormFieldsProps {
     isEdit: boolean;
     initial?: Customer;  // partial OK for create
     onSubmit: (parsed: CustomerFormParsed) => Promise<void> | void;
     onCancel: () => void;
     submitting: boolean;
     submitError: string;
     hasSubmitError: boolean;
     submitLabel?: string;     // default: 'Tạo mới' / 'Cập nhật'
     cancelLabel?: string;     // default: 'Hủy'
     showStatusField?: boolean; // default: isEdit (giữ logic cũ)
     showHeader?: boolean;     // default: true (page mode) — false khi dùng trong Dialog
   }
   ```

2. Trong `customer-form-page.tsx`, giữ outer `CustomerFormPage` route component, render `<CustomerFormFields {...} showHeader onCancel={() => navigate('/customers')} />`. Toast và navigate vẫn ở outer (page-specific).

3. Verify trang Danh mục KH `/customers/new`, `/customers/:id` không regression.

### Build `CustomerQuickAddDialog`

4. Component mới `customer-quick-add-dialog.tsx`:
   ```tsx
   import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
   import { CustomerFormFields } from '@/pages/customers/customer-form-fields';
   import { useCreateCustomer } from '@/features/customers/hooks';
   import { getErrorMessage } from '@/lib/api-client';
   import { toast } from '@/lib/use-toast';
   import type { Customer } from '@/features/customers/types';

   interface Props {
     open: boolean;
     onOpenChange: (open: boolean) => void;
     onCreated: (customer: Customer) => void;
   }

   export function CustomerQuickAddDialog({ open, onOpenChange, onCreated }: Props) {
     const create = useCreateCustomer();
     return (
       <Dialog open={open} onOpenChange={onOpenChange}>
         <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto">
           <DialogHeader>
             <DialogTitle>Thêm nhanh khách hàng</DialogTitle>
           </DialogHeader>
           <CustomerFormFields
             isEdit={false}
             showHeader={false}
             showStatusField={false}
             submitting={create.isPending}
             submitError={getErrorMessage(create.error)}
             hasSubmitError={create.isError}
             onCancel={() => onOpenChange(false)}
             onSubmit={async (parsed) => {
               try {
                 const created = await create.mutateAsync(parsed);
                 toast({ variant: 'success', title: 'Đã tạo khách hàng', description: created.code });
                 onCreated(created);
                 onOpenChange(false);
               } catch (err) {
                 toast({ variant: 'destructive', title: 'Không thể lưu', description: getErrorMessage(err) });
               }
             }}
           />
         </DialogContent>
       </Dialog>
     );
   }
   ```

### Wire vào `quotation-form-page`

5. Import `CustomerQuickAddDialog`, `<CustomerSearchItem>` shape:
   ```tsx
   import { CustomerQuickAddDialog } from '@/components/customer-autocomplete/customer-quick-add-dialog';
   ```

6. Render Dialog (sau form):
   ```tsx
   <CustomerQuickAddDialog
     open={quickAddOpen}
     onOpenChange={setQuickAddOpen}
     onCreated={(c) => {
       const searchItem: CustomerSearchItem = {
         id: c.id, code: c.code, name: c.name,
         taxCode: c.taxCode, companyAddress: c.companyAddress,
         defaultShippingAddress: c.defaultShippingAddress,
         contactPerson: c.contactPerson, phoneNumber: c.phoneNumber,
         status: c.status,
       };
       handleSelectCustomer(searchItem);  // reuse handler từ Phase 05
     }}
   />
   ```

## Verification

```powershell
cd d:\Projects\QLBanHang\frontend
npm run typecheck
npm run lint
npm run dev
```

**Manual e2e**:
1. Mở `/customers/new` → form render đúng như trước (no regression).
2. Mở `/customers/:id` (edit existing) → form render đúng, có status field.
3. `/quotations/new` → click icon "+" cạnh Khách hàng → Dialog mở, hiển thị full form (không có status field, không có header back-button).
4. Trong Dialog: nhập tên + nhóm + SĐT, Submit → Dialog đóng, autocomplete tự nhận KH mới (input hiển thị tên), Tên KH fill, focus chuyển sang input Tên KH.
5. Submit lỗi (vd: trùng mã KH) → Dialog vẫn mở, hiển thị error banner.
6. Press Esc trong Dialog → đóng Dialog, không tạo KH.

## Exit Criteria

- Typecheck + lint pass.
- 6 manual scenarios pass.
- `customer-form-page` route vẫn hoạt động bình thường.
- Quick-add success → autocomplete value cập nhật, không cần user search lại.
