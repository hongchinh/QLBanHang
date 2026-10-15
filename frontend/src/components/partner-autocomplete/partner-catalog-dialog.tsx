import { useEffect, useRef, useState } from 'react';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/dialog';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { customersApi } from '@/features/customers/api';
import { suppliersApi } from '@/features/suppliers/api';
import { toPartnerSearchItem } from '@/features/customers/partner-search-item';
import { usePartnerPermissions } from '@/features/customers/use-partner-permissions';
import type { PartnerSearchItem } from '@/features/stock-vouchers/types';
import { toast } from '@/lib/use-toast';
import { PartnerCatalogList } from './partner-catalog-list';

type PartnerRole = 'customer' | 'supplier';

export interface PartnerCatalogDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  initialQuery: string;
  scope: 'partner' | 'customer';
  preferredRole: PartnerRole;
  onSelect: (partner: PartnerSearchItem) => void;
  onCloseAutoFocus?: (event: Event) => void;
}

const NOUN: Record<PartnerRole, string> = { customer: 'khách hàng', supplier: 'nhà cung cấp' };

// Does not wrap itself in StopFormEvents: the caller wraps the whole <Dialog> so the wrapper sits outside
// DialogContent (Radix's Tab loop runs and the close button is covered).
export function PartnerCatalogDialog({
  open,
  onOpenChange,
  initialQuery,
  scope,
  preferredRole,
  onSelect,
  onCloseAutoFocus,
}: PartnerCatalogDialogProps) {
  const { canViewCustomer, canViewSupplier } = usePartnerPermissions();
  const roles: PartnerRole[] = [];
  if (canViewCustomer) roles.push('customer');
  if (canViewSupplier && scope === 'partner') roles.push('supplier');
  if (roles.length === 0) return null;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="flex max-w-4xl overflow-hidden p-0 h-[80vh]"
        showClose={true}
        onCloseAutoFocus={onCloseAutoFocus}
      >
        <CatalogBody
          roles={roles}
          preferredRole={preferredRole}
          initialQuery={initialQuery}
          onSelect={onSelect}
          onOpenChange={onOpenChange}
        />
      </DialogContent>
    </Dialog>
  );
}

// Mounted only while the dialog is open, so every opening starts from initialQuery and the preferred role.
function CatalogBody({
  roles,
  preferredRole,
  initialQuery,
  onSelect,
  onOpenChange,
}: { roles: PartnerRole[] } & Pick<
  PartnerCatalogDialogProps,
  'preferredRole' | 'initialQuery' | 'onSelect' | 'onOpenChange'
>) {
  const [query, setQuery] = useState(initialQuery);
  const [role, setRole] = useState<PartnerRole>(roles.includes(preferredRole) ? preferredRole : roles[0]);

  // One detail load at a time (a double click, or Enter during the load); a result that arrives after this body
  // unmounted (the dialog was closed meanwhile) is dropped.
  const loadingRef = useRef(false);
  const mountedRef = useRef(true);
  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  async function handleSelectId(id: string) {
    if (loadingRef.current) return;
    loadingRef.current = true;
    try {
      const record = await (role === 'customer' ? customersApi.get(id) : suppliersApi.get(id));
      if (!mountedRef.current) return;
      onSelect(toPartnerSearchItem(record));
      onOpenChange(false);
    } catch {
      if (!mountedRef.current) return;
      toast({ variant: 'destructive', title: `Không thể tải thông tin ${NOUN[role]}. Vui lòng thử lại.` });
    } finally {
      loadingRef.current = false;
    }
  }

  return (
    <div className="flex w-full flex-col min-h-0">
      <div className="flex flex-shrink-0 items-center gap-4 border-b px-4 pr-10 py-3">
        <DialogTitle className="text-base">{`Danh mục ${NOUN[role]}`}</DialogTitle>
        {roles.length > 1 && (
          <Tabs value={role} onValueChange={(value) => setRole(value as PartnerRole)}>
            <TabsList aria-label="Loại đối tượng" className="h-8">
              <TabsTrigger value="customer" className="py-1">Khách hàng</TabsTrigger>
              <TabsTrigger value="supplier" className="py-1">Nhà cung cấp</TabsTrigger>
            </TabsList>
          </Tabs>
        )}
      </div>
      <div className="flex-1 overflow-hidden min-h-0">
        <PartnerCatalogList
          key={role}
          role={role}
          query={query}
          onQueryChange={setQuery}
          onSelect={handleSelectId}
        />
      </div>
    </div>
  );
}
