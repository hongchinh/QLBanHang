import type { Permission, Role } from '@/lib/permissions';

interface RouteRule {
  pattern: RegExp;
  permission?: Permission;
  role?: Role;
}

// Mirror các <ProtectedRoute permission=...> trong App.tsx.
// Order matters: pattern cụ thể hơn đặt trước (vd /admin/users/:id/transfer-quotations
// phải trước /admin/users/:id).
// Routes không có rule = accessible cho mọi authenticated user.
const RULES: RouteRule[] = [
  { pattern: /^\/admin\/users\/[^/]+\/transfer-quotations$/, permission: 'quotations.transfer_any' },
  { pattern: /^\/admin\/user-settings\/[^/]+$/, permission: 'user_settings.manage' },
  { pattern: /^\/admin\/users(\/[^/]+)?$/, permission: 'user_settings.manage' },
  { pattern: /^\/admin\/roles$/, permission: 'roles.view' },
  { pattern: /^\/admin\/dashboard$/, permission: 'quotations.view_all' },
  { pattern: /^\/reports\/sales-performance$/, permission: 'quotations.view_all' },
  { pattern: /^\/reports\/(revenue|sales-revenue|vehicle-revenue)$/, permission: 'reports.revenue' },
  { pattern: /^\/customers(\/[^/]+)?$/, permission: 'customers.view' },
  { pattern: /^\/products(\/[^/]+)?$/, permission: 'products.view' },
  { pattern: /^\/quotations(\/[^/]+)?$/, permission: 'quotations.view' },
  { pattern: /^\/stock-in(\/[^/]+)?$/, permission: 'stock_in.view' },
  { pattern: /^\/stock-out(\/[^/]+)?$/, permission: 'stock_out.view' },
  { pattern: /^\/inventory\/opening-stock$/, permission: 'inventory.opening_stock' },
  { pattern: /^\/inventory\/(stock-on-hand|stock-card)$/, permission: 'reports.inventory' },
  { pattern: /^\/suppliers(\/[^/]+)?$/, permission: 'suppliers.view' },
  { pattern: /^\/(warehouses|stock-reasons|payment-methods)$/, permission: 'inventory.catalogs.manage' },
  { pattern: /^\/settings\/branches$/, permission: 'branches.manage' },
  { pattern: /^\/settings\/period-lock$/, permission: 'period_lock.manage' },
  { pattern: /^\/settings\/(inventory|numbering)$/, permission: 'inventory.settings' },
  { pattern: /^\/settings\/recalc-cost$/, permission: 'inventory.recalc_cost' },
];

export function canAccessRoute(
  pathname: string,
  perms: readonly string[],
  roles: readonly string[],
): boolean {
  const rule = RULES.find((r) => r.pattern.test(pathname));
  if (!rule) return true;
  if (rule.permission && !perms.includes(rule.permission)) return false;
  if (rule.role && !roles.includes(rule.role)) return false;
  return true;
}
