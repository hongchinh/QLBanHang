import type { Permission, Role } from '@/lib/permissions';

interface RouteRule {
  pattern: RegExp;
  permission?: Permission;
  role?: Role;
}

// /<base>, /<base>/new and /<base>/:id, each with its own permission ('new' before ':id').
function listNewDetail(base: string, list: Permission, create: Permission, detail: Permission): RouteRule[] {
  return [
    { pattern: new RegExp(`^/${base}$`), permission: list },
    { pattern: new RegExp(`^/${base}/new$`), permission: create },
    { pattern: new RegExp(`^/${base}/[^/]+$`), permission: detail },
  ];
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
  { pattern: /^\/reports\/sales-revenue\/[^/]+$/, permission: 'reports.revenue' },
  // Partner, product and quotation forms (/new, /:id) need create / update, as in App.tsx.
  ...listNewDetail('customers', 'customers.view', 'customers.create', 'customers.update'),
  ...listNewDetail('suppliers', 'suppliers.view', 'suppliers.create', 'suppliers.update'),
  ...listNewDetail('products', 'products.view', 'products.create', 'products.update'),
  { pattern: /^\/product-groups$/, permission: 'products.view' },
  ...listNewDetail('quotations', 'quotations.view', 'quotations.create', 'quotations.update'),
  // Voucher detail pages stay readable with *.view (the page itself gates editing).
  ...listNewDetail('stock-in', 'stock_in.view', 'stock_in.create', 'stock_in.view'),
  ...listNewDetail('stock-out', 'stock_out.view', 'stock_out.create', 'stock_out.view'),
  { pattern: /^\/inventory\/opening-stock$/, permission: 'inventory.opening_stock' },
  { pattern: /^\/inventory\/(stock-on-hand|stock-card)$/, permission: 'reports.inventory' },
  { pattern: /^\/(warehouses|stock-reasons|payment-methods)$/, permission: 'inventory.catalogs.manage' },
  { pattern: /^\/settings\/quotation$/, permission: 'system.manage_settings' },
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
