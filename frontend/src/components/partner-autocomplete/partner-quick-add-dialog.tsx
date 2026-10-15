import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { CustomerFormFields, type PartnerInitialRoles } from '@/pages/customers/customer-form-fields';
import { useCreateCustomer } from '@/features/customers/hooks';
import { useCreateSupplier } from '@/features/suppliers/hooks';
import { toPartnerSearchItem } from '@/features/customers/partner-search-item';
import { usePartnerPermissions } from '@/features/customers/use-partner-permissions';
import type { CustomerFormParsed } from '@/features/customers/schema';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import { formatApiErrorDetails, getErrorMessage } from '@/lib/api-client';
import { focusById } from '@/lib/enter-focus';
import { toast } from '@/lib/use-toast';

export interface PartnerQuickAddDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialName: string;
  scope: 'partner' | 'customer';
  preferredRole: 'customer' | 'supplier';
  onCreated: (created: PartnerSearchItem) => void;
  onCloseAutoFocus?: (event: Event) => void;
}

// Does not wrap itself in StopFormEvents: the caller wraps the whole <Dialog> so the wrapper sits outside
// DialogContent (Radix's Tab loop runs and the close button is covered).
export function PartnerQuickAddDialog({
  open,
  onOpenChange,
  initialName,
  scope,
  preferredRole,
  onCreated,
  onCloseAutoFocus,
}: PartnerQuickAddDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="max-w-3xl max-h-[90vh] overflow-y-auto"
        onOpenAutoFocus={(e) => {
          e.preventDefault();
          focusById('name', 'end');
        }}
        onCloseAutoFocus={onCloseAutoFocus}
      >
        <DialogHeader>
          <DialogTitle>{scope === 'customer' ? 'Thêm nhanh khách hàng' : 'Thêm nhanh đối tượng'}</DialogTitle>
        </DialogHeader>
        <QuickAddBody
          onOpenChange={onOpenChange}
          initialName={initialName}
          scope={scope}
          preferredRole={preferredRole}
          onCreated={onCreated}
        />
      </DialogContent>
    </Dialog>
  );
}

// Mounted only while the dialog is open: the mutation hooks live here, so a closed dialog needs no query client.
function QuickAddBody({
  onOpenChange,
  initialName,
  scope,
  preferredRole,
  onCreated,
}: Pick<PartnerQuickAddDialogProps, 'onOpenChange' | 'initialName' | 'scope' | 'preferredRole' | 'onCreated'>) {
  const { canCreateCustomer, canCreateSupplier } = usePartnerPermissions();
  const createCustomer = useCreateCustomer();
  const createSupplier = useCreateSupplier();

  const initialRoles = pickInitialRoles(scope, preferredRole, canCreateCustomer, canCreateSupplier);
  const failed = createCustomer.isError ? createCustomer : createSupplier.isError ? createSupplier : null;

  async function handleSubmit(parsed: CustomerFormParsed) {
    // Only the hook that fails now may show its error.
    createCustomer.reset();
    createSupplier.reset();
    try {
      const created = parsed.isCustomer
        ? await createCustomer.mutateAsync(parsed)
        : await createSupplier.mutateAsync(parsed);
      toast({
        variant: 'success',
        title: parsed.isCustomer ? 'Đã tạo khách hàng' : 'Đã tạo nhà cung cấp',
        description: created.code,
      });
      onCreated(toPartnerSearchItem(created));
      onOpenChange(false);
    } catch (err) {
      toast({ variant: 'destructive', title: 'Không thể lưu', description: formatApiErrorDetails(err) });
    }
  }

  return (
    <CustomerFormFields
      isEdit={false}
      showHeader={false}
      showStatusField={false}
      showRoles={scope === 'partner'}
      enterNavigation
      rolesMode="free"
      initialName={initialName}
      initialRoles={initialRoles}
      submitting={createCustomer.isPending || createSupplier.isPending}
      submitError={failed ? getErrorMessage(failed.error) : ''}
      hasSubmitError={failed !== null}
      onCancel={() => onOpenChange(false)}
      onSubmit={handleSubmit}
    />
  );
}

// Quotation quick add: customer only. Elsewhere: the preferred role when the user may create it, else the other one.
function pickInitialRoles(
  scope: 'partner' | 'customer',
  preferredRole: 'customer' | 'supplier',
  canCreateCustomer: boolean,
  canCreateSupplier: boolean,
): PartnerInitialRoles {
  if (scope === 'customer') return { isCustomer: true, isSupplier: false };
  const canCreatePreferred = preferredRole === 'customer' ? canCreateCustomer : canCreateSupplier;
  const role = canCreatePreferred ? preferredRole : preferredRole === 'customer' ? 'supplier' : 'customer';
  return { isCustomer: role === 'customer', isSupplier: role === 'supplier' };
}
