import type { Permission, Role } from '@/lib/permissions';

interface RouteRule {
  pattern: RegExp;
  permission?: Permission;
  // Accessible when the user has at least one of these.
  anyPermission?: Permission[];
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
  { pattern: /^\/settings\/inventory$/, permission: 'inventory.settings' },
  // Each numbering card needs its own permission (StockIn/StockOut vs the cash types, P7).
  { pattern: /^\/settings\/numbering$/, anyPermission: ['inventory.settings', 'cash.settings'] },
  { pattern: /^\/settings\/recalc-cost$/, permission: 'inventory.recalc_cost' },
  // Round 2A: cash and debt. Voucher detail pages stay readable with *.view.
  ...listNewDetail('receipts', 'receipts.view', 'receipts.create', 'receipts.view'),
  ...listNewDetail('payments', 'payments.view', 'payments.create', 'payments.view'),
  ...listNewDetail('fund-transfers', 'fund_transfers.view', 'fund_transfers.create', 'fund_transfers.view'),
  { pattern: /^\/debt\/matching$/, permission: 'debt.matching' },
  { pattern: /^\/debt\/opening$/, permission: 'debt.opening_balance' },
  ...listNewDetail('debt/offsets', 'debt_offset.view', 'debt_offset.create', 'debt_offset.view'),
  { pattern: /^\/reports\/(debt-balance|debt-ledger|debt-documents|debt-aging|debt-overdue|debt-by-salesperson|debt-reconciliation)$/, permission: 'reports.debt' },
  { pattern: /^\/reports\/cash-book$/, permission: 'reports.cash' },
  { pattern: /^\/cash-reasons$/, permission: 'cash.catalogs.manage' },
  { pattern: /^\/funds$/, anyPermission: ['cash.catalogs.manage', 'cash.opening_balance'] },
  { pattern: /^\/credit-limits$/, permission: 'credit_limit.manage' },
  { pattern: /^\/settings\/cash$/, permission: 'cash.settings' },
  { pattern: /^\/settings\/company$/, permission: 'settings.company' },
];

export function canAccessRouteWith(
  pathname: string,
  hasPermission: (permission: string) => boolean,
  isInRole: (role: string) => boolean,
): boolean {
  const rule = RULES.find((r) => r.pattern.test(pathname));
  if (!rule) return true;
  if (rule.permission && !hasPermission(rule.permission)) return false;
  if (rule.anyPermission && !rule.anyPermission.some((p) => hasPermission(p))) return false;
  if (rule.role && !isInRole(rule.role)) return false;
  return true;
}

export function canAccessRoute(
  pathname: string,
  perms: readonly string[],
  roles: readonly string[],
): boolean {
  return canAccessRouteWith(
    pathname,
    (p) => perms.includes(p),
    (r) => roles.includes(r),
  );
}
