import {
  Users,
  Package,
  Tag,
  FileText,
  BarChart3,
  UserCog,
  Users2,
  ShieldCheck,
  Settings,
  QrCode,
  Truck,
  Warehouse,
  ListChecks,
  Wallet,
  ArrowDownToLine,
  ArrowUpFromLine,
  ClipboardList,
  Boxes,
  ScrollText,
  HandCoins,
  Banknote,
  ArrowLeftRight,
  Link2,
  Scale,
  History,
  BookOpen,
  BookText,
  Landmark,
  Gauge,
  Hourglass,
  AlarmClock,
} from 'lucide-react';
import type { Permission, Role } from '@/lib/permissions';
import type { SidebarNavGroup, SidebarNavItem } from './sidebar/sidebar';

interface NavItem extends SidebarNavItem {
  permission?: Permission;
  // Visible when the user has at least one of these.
  anyPermission?: Permission[];
  role?: Role;
}

interface NavGroup {
  label: string;
  items: NavItem[];
}

const navGroups: NavGroup[] = [
  {
    label: 'Chức năng',
    items: [
      { to: '/customers', label: 'Khách hàng', icon: Users, permission: 'customers.view' },
      { to: '/products', label: 'Hàng hóa', icon: Package, permission: 'products.view' },
      { to: '/product-groups', label: 'Nhóm hàng hóa', icon: Tag, permission: 'products.view' },
      { to: '/quotations', label: 'Báo giá', icon: FileText, permission: 'quotations.view' },
      { to: '/qr-thanh-toan', label: 'Tạo mã QR thanh toán', icon: QrCode },
      { to: '/suppliers', label: 'Nhà cung cấp', icon: Truck, permission: 'suppliers.view' },
      { to: '/warehouses', label: 'Kho', icon: Warehouse, permission: 'inventory.catalogs.manage' },
      { to: '/stock-reasons', label: 'Lý do nhập xuất', icon: ListChecks, permission: 'inventory.catalogs.manage' },
      { to: '/payment-methods', label: 'Hình thức thanh toán', icon: Wallet, permission: 'inventory.catalogs.manage' },
      { to: '/cash-reasons', label: 'Lý do thu/chi', icon: ListChecks, permission: 'cash.catalogs.manage' },
      {
        to: '/funds',
        label: 'Quỹ',
        icon: Landmark,
        anyPermission: ['cash.catalogs.manage', 'cash.opening_balance'],
      },
      { to: '/credit-limits', label: 'Hạn mức nợ', icon: Gauge, permission: 'credit_limit.manage' },
    ],
  },
  {
    label: 'Kho',
    items: [
      { to: '/stock-in', label: 'Phiếu nhập kho', icon: ArrowDownToLine, permission: 'stock_in.view' },
      { to: '/stock-out', label: 'Phiếu xuất kho', icon: ArrowUpFromLine, permission: 'stock_out.view' },
      { to: '/inventory/opening-stock', label: 'Tồn đầu kỳ', icon: ClipboardList, permission: 'inventory.opening_stock' },
      { to: '/inventory/stock-on-hand', label: 'Tồn kho', icon: Boxes, permission: 'reports.inventory' },
      { to: '/inventory/stock-card', label: 'Thẻ kho', icon: ScrollText, permission: 'reports.inventory' },
    ],
  },
  {
    label: 'Tiền & Công nợ',
    items: [
      { to: '/receipts', label: 'Phiếu thu', icon: HandCoins, permission: 'receipts.view' },
      { to: '/payments', label: 'Phiếu chi', icon: Banknote, permission: 'payments.view' },
      { to: '/fund-transfers', label: 'Chuyển quỹ', icon: ArrowLeftRight, permission: 'fund_transfers.view' },
      { to: '/debt/matching', label: 'Đối trừ chứng từ', icon: Link2, permission: 'debt.matching' },
      { to: '/debt/offsets', label: 'Bù trừ công nợ', icon: Scale, permission: 'debt_offset.view' },
      { to: '/debt/opening', label: 'Nợ đầu kỳ', icon: History, permission: 'debt.opening_balance' },
      { to: '/reports/debt-balance', label: 'Số dư công nợ', icon: BookOpen, permission: 'reports.debt' },
      { to: '/reports/debt-ledger', label: 'Sổ chi tiết công nợ', icon: BookText, permission: 'reports.debt' },
      { to: '/reports/debt-documents', label: 'Công nợ theo chứng từ', icon: FileText, permission: 'reports.debt' },
      { to: '/reports/debt-aging', label: 'Tuổi nợ', icon: Hourglass, permission: 'reports.debt' },
      { to: '/reports/debt-overdue', label: 'Nợ quá hạn', icon: AlarmClock, permission: 'reports.debt' },
      { to: '/reports/cash-book', label: 'Sổ quỹ', icon: Wallet, permission: 'reports.cash' },
    ],
  },
  {
    label: 'Báo cáo',
    items: [
      { to: '/reports/revenue', label: 'Doanh thu', icon: BarChart3, permission: 'reports.revenue' },
      { to: '/reports/sales-revenue', label: 'Doanh thu sale', icon: BarChart3, permission: 'reports.revenue' },
      { to: '/reports/vehicle-revenue', label: 'Doanh thu xe', icon: BarChart3, permission: 'reports.revenue' },
      { to: '/reports/sales-performance', label: 'Hiệu suất sale', icon: BarChart3, permission: 'quotations.view_all' },
    ],
  },
  {
    label: 'Setting',
    items: [
      { to: '/settings/my-quotation-settings', label: 'Cài đặt của tôi', icon: UserCog },
      {
        to: '/settings',
        label: 'Cấu hình hệ thống',
        icon: Settings,
        anyPermission: [
          'system.manage_settings',
          'branches.manage',
          'period_lock.manage',
          'inventory.settings',
          'inventory.recalc_cost',
          'cash.settings',
        ],
      },
      { to: '/admin/users', label: 'Quản lý người dùng', icon: Users2, permission: 'user_settings.manage' },
      { to: '/admin/roles', label: 'Phân quyền', icon: ShieldCheck, permission: 'roles.view' },
    ],
  },
];

export function visibleNavGroups(
  hasPermission: (p: Permission) => boolean,
  isInRole: (r: Role) => boolean,
): SidebarNavGroup[] {
  return navGroups
    .map((group) => ({
      label: group.label,
      items: group.items.filter((item) => {
        if (item.permission && !hasPermission(item.permission)) return false;
        if (item.anyPermission && !item.anyPermission.some((p) => hasPermission(p))) return false;
        if (item.role && !isInRole(item.role)) return false;
        return true;
      }),
    }))
    .filter((group) => group.items.length > 0);
}
