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
          showRoles={false}
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
