import { useMemo } from 'react';
import type { Permission } from '@/lib/permissions';
import { useAuthStore } from '@/stores/auth-store';
import { EXECUTIVE_DOMAINS } from './types';
import type { ExecutiveDomain, ExecutiveTab } from './types';

// The report permission each domain needs on top of dashboard.executive (the API checks the same).
const DOMAIN_PERMISSION: Record<ExecutiveDomain, Permission> = {
  sales: 'reports.profit',
  cash: 'reports.cash',
  debt: 'reports.debt',
  inventory: 'reports.inventory',
};

// Which domains the signed-in user may see, and the tabs to show: the overview, then the open domains.
export function useExecutiveAccess(): Record<ExecutiveDomain, boolean> & { tabs: ExecutiveTab[] } {
  const permissions = useAuthStore((s) => s.user?.permissions);

  return useMemo(() => {
    const allowed = (domain: ExecutiveDomain) => permissions?.includes(DOMAIN_PERMISSION[domain]) ?? false;
    const tabs: ExecutiveTab[] = ['overview', ...EXECUTIVE_DOMAINS.filter(allowed)];
    return {
      sales: allowed('sales'),
      cash: allowed('cash'),
      debt: allowed('debt'),
      inventory: allowed('inventory'),
      tabs,
    };
  }, [permissions]);
}
