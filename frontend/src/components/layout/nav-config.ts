import type { IconName } from '@/components/icons';
import type { Permission, Role } from '@/lib/permissions';

export type NavGroupId = 'sales' | 'inventory' | 'cash' | 'debt' | 'reports' | 'catalogs' | 'system';

export interface NavItem {
  to: string;
  label: string;
  icon: IconName;
  // Sub-heading inside a group (Báo cáo only).
  section?: string;
}

export interface NavGroup {
  id: NavGroupId;
  label: string;
  icon: IconName;
  // Tailwind background class of the group token (--nav-<id>), see index.css.
  color: string;
  defaultOpen: boolean;
  items: NavItem[];
}

interface NavItemConfig extends NavItem {
  permission?: Permission;
  // Visible when the user has at least one of these.
  anyPermission?: Permission[];
  role?: Role;
}

interface NavGroupConfig extends Omit<NavGroup, 'items'> {
  items: NavItemConfig[];
}

const navGroups: NavGroupConfig[] = [
  {
    id: 'sales',
    label: 'Bán hàng',
    icon: 'sales',
    color: 'bg-nav-sales',
    defaultOpen: true,
    items: [
      { to: '/quotations', label: 'Báo giá', icon: 'quotation', permission: 'quotations.view' },
      { to: '/customers', label: 'Khách hàng', icon: 'customers', permission: 'customers.view' },
      { to: '/qr-thanh-toan', label: 'Tạo mã QR thanh toán', icon: 'paymentQr' },
    ],
  },
  {
    id: 'inventory',
    label: 'Kho',
    icon: 'inventory',
    color: 'bg-nav-inventory',
    defaultOpen: true,
    items: [
      { to: '/stock-in', label: 'Phiếu nhập kho', icon: 'stockIn', permission: 'stock_in.view' },
      { to: '/stock-out', label: 'Phiếu xuất kho', icon: 'stockOut', permission: 'stock_out.view' },
      { to: '/inventory/opening-stock', label: 'Tồn đầu kỳ', icon: 'openingStock', permission: 'inventory.opening_stock' },
    ],
  },
  {
    id: 'cash',
    label: 'Thu chi',
    icon: 'cash',
    color: 'bg-nav-cash',
    defaultOpen: true,
    items: [
      { to: '/receipts', label: 'Phiếu thu', icon: 'receipt', permission: 'receipts.view' },
      { to: '/payments', label: 'Phiếu chi', icon: 'payment', permission: 'payments.view' },
      { to: '/fund-transfers', label: 'Chuyển quỹ', icon: 'fundTransfer', permission: 'fund_transfers.view' },
    ],
  },
  {
    id: 'debt',
    label: 'Công nợ',
    icon: 'debt',
    color: 'bg-nav-debt',
    defaultOpen: true,
    items: [
      { to: '/debt/matching', label: 'Đối trừ chứng từ', icon: 'debtMatching', permission: 'debt.matching' },
      { to: '/debt/offsets', label: 'Bù trừ công nợ', icon: 'debtOffset', permission: 'debt_offset.view' },
      { to: '/debt/opening', label: 'Nợ đầu kỳ', icon: 'openingDebt', permission: 'debt.opening_balance' },
      { to: '/credit-limits', label: 'Hạn mức nợ', icon: 'creditLimit', permission: 'credit_limit.manage' },
    ],
  },
  {
    id: 'reports',
    label: 'Báo cáo',
    icon: 'reports',
    color: 'bg-nav-reports',
    defaultOpen: true,
    items: [
      { to: '/dashboard/executive', label: 'Điều hành', icon: 'executiveDashboard', section: 'Tổng hợp', permission: 'dashboard.executive' },
      { to: '/reports/revenue', label: 'Doanh thu', icon: 'revenueReport', section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/sales-revenue', label: 'Doanh thu sale', icon: 'salesRevenueReport', section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/vehicle-revenue', label: 'Doanh thu xe', icon: 'vehicleRevenueReport', section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/sales-performance', label: 'Hiệu suất sale', icon: 'salesPerformanceReport', section: 'Doanh thu', permission: 'quotations.view_all' },
      { to: '/inventory/stock-on-hand', label: 'Tồn kho', icon: 'stockOnHandReport', section: 'Kho', permission: 'reports.inventory' },
      { to: '/inventory/stock-card', label: 'Thẻ kho', icon: 'stockCardReport', section: 'Kho', permission: 'reports.inventory' },
      { to: '/inventory/stock-summary', label: 'Tổng hợp nhập xuất tồn', icon: 'stockSummaryReport', section: 'Kho', permission: 'reports.inventory' },
      { to: '/reports/cash-book', label: 'Sổ quỹ', icon: 'cashBookReport', section: 'Quỹ', permission: 'reports.cash' },
      { to: '/reports/debt-balance', label: 'Số dư công nợ', icon: 'debtBalanceReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-ledger', label: 'Sổ chi tiết công nợ', icon: 'debtLedgerReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-documents', label: 'Công nợ theo chứng từ', icon: 'debtByDocumentReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-aging', label: 'Tuổi nợ', icon: 'debtAgingReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-overdue', label: 'Nợ quá hạn', icon: 'debtOverdueReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-by-salesperson', label: 'Công nợ theo nhân viên', icon: 'debtBySalespersonReport', section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-reconciliation', label: 'Biên bản đối chiếu', icon: 'reconciliationReport', section: 'Công nợ', permission: 'reports.debt' },
    ],
  },
  {
    id: 'catalogs',
    label: 'Danh mục',
    icon: 'catalogs',
    color: 'bg-nav-catalogs',
    defaultOpen: false,
    items: [
      { to: '/products', label: 'Hàng hóa', icon: 'product', permission: 'products.view' },
      { to: '/product-groups', label: 'Nhóm hàng hóa', icon: 'productGroup', permission: 'products.view' },
      { to: '/suppliers', label: 'Nhà cung cấp', icon: 'supplier', permission: 'suppliers.view' },
      { to: '/warehouses', label: 'Kho hàng', icon: 'warehouse', permission: 'inventory.catalogs.manage' },
      { to: '/stock-reasons', label: 'Lý do nhập xuất', icon: 'stockReason', permission: 'inventory.catalogs.manage' },
      { to: '/cash-reasons', label: 'Lý do thu/chi', icon: 'cashReason', permission: 'cash.catalogs.manage' },
      { to: '/payment-methods', label: 'Hình thức thanh toán', icon: 'paymentMethod', permission: 'inventory.catalogs.manage' },
      {
        to: '/funds',
        label: 'Quỹ',
        icon: 'fund',
        anyPermission: ['cash.catalogs.manage', 'cash.opening_balance'],
      },
    ],
  },
  {
    id: 'system',
    label: 'Hệ thống',
    icon: 'system',
    color: 'bg-nav-system',
    defaultOpen: false,
    items: [
      { to: '/settings/my-quotation-settings', label: 'Cài đặt của tôi', icon: 'mySettings' },
      {
        to: '/settings',
        label: 'Cấu hình hệ thống',
        icon: 'systemConfig',
        anyPermission: [
          'system.manage_settings',
          'branches.manage',
          'period_lock.manage',
          'inventory.settings',
          'inventory.recalc_cost',
          'cash.settings',
        ],
      },
      { to: '/admin/users', label: 'Quản lý người dùng', icon: 'users', permission: 'user_settings.manage' },
      { to: '/admin/roles', label: 'Phân quyền', icon: 'roles', permission: 'roles.view' },
    ],
  },
];

export function visibleNavGroups(
  hasPermission: (p: Permission) => boolean,
  isInRole: (r: Role) => boolean,
): NavGroup[] {
  return navGroups
    .map(({ id, label, icon, color, defaultOpen, items }) => ({
      id,
      label,
      icon,
      color,
      defaultOpen,
      items: items
        .filter((item) => {
          if (item.permission && !hasPermission(item.permission)) return false;
          if (item.anyPermission && !item.anyPermission.some((p) => hasPermission(p))) return false;
          if (item.role && !isInRole(item.role)) return false;
          return true;
        })
        .map(({ to, label: itemLabel, icon: itemIcon, section }) => ({
          to,
          label: itemLabel,
          icon: itemIcon,
          section,
        })),
    }))
    .filter((group) => group.items.length > 0);
}
