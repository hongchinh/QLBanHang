import { useAuthStore } from '@/stores/auth-store';

// What the signed-in user may do with the partner field's catalogs (quick add and the list picker).
export function usePartnerPermissions() {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  return {
    canCreateCustomer: hasPermission('customers.create'),
    canCreateSupplier: hasPermission('suppliers.create'),
    canViewCustomer: hasPermission('customers.view'),
    canViewSupplier: hasPermission('suppliers.view'),
  };
}
