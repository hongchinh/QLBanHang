import {
  AlarmClock,
  ArrowDownToLine,
  ArrowLeftRight,
  ArrowUpFromLine,
  BadgeDollarSign,
  Banknote,
  BarChart3,
  BookOpen,
  BookText,
  Boxes,
  CarFront,
  ClipboardList,
  Combine,
  Container,
  CreditCard,
  FileCheck,
  FileText,
  Files,
  Gauge,
  HandCoins,
  History,
  Hourglass,
  Landmark,
  Library,
  Link2,
  ListChecks,
  ListOrdered,
  Package,
  PiggyBank,
  Presentation,
  QrCode,
  Scale,
  ScrollText,
  Settings,
  ShieldCheck,
  ShoppingCart,
  Sigma,
  SlidersHorizontal,
  Tag,
  Target,
  TrendingUp,
  Truck,
  UserCog,
  UserRound,
  Users,
  Users2,
  Wallet,
  Warehouse,
} from 'lucide-react';
import type { Permission, Role } from '@/lib/permissions';

export type NavGroupId = 'sales' | 'inventory' | 'cash' | 'debt' | 'reports' | 'catalogs' | 'system';

export interface NavItem {
  to: string;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  // Sub-heading inside a group (Báo cáo only).
  section?: string;
}

export interface NavGroup {
  id: NavGroupId;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
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
    icon: ShoppingCart,
    color: 'bg-nav-sales',
    defaultOpen: true,
    items: [
      { to: '/quotations', label: 'Báo giá', icon: FileText, permission: 'quotations.view' },
      { to: '/customers', label: 'Khách hàng', icon: Users, permission: 'customers.view' },
      { to: '/qr-thanh-toan', label: 'Tạo mã QR thanh toán', icon: QrCode },
    ],
  },
  {
    id: 'inventory',
    label: 'Kho',
    icon: Warehouse,
    color: 'bg-nav-inventory',
    defaultOpen: true,
    items: [
      { to: '/stock-in', label: 'Phiếu nhập kho', icon: ArrowDownToLine, permission: 'stock_in.view' },
      { to: '/stock-out', label: 'Phiếu xuất kho', icon: ArrowUpFromLine, permission: 'stock_out.view' },
      { to: '/inventory/opening-stock', label: 'Tồn đầu kỳ', icon: ClipboardList, permission: 'inventory.opening_stock' },
    ],
  },
  {
    id: 'cash',
    label: 'Thu chi',
    icon: Wallet,
    color: 'bg-nav-cash',
    defaultOpen: true,
    items: [
      { to: '/receipts', label: 'Phiếu thu', icon: HandCoins, permission: 'receipts.view' },
      { to: '/payments', label: 'Phiếu chi', icon: Banknote, permission: 'payments.view' },
      { to: '/fund-transfers', label: 'Chuyển quỹ', icon: ArrowLeftRight, permission: 'fund_transfers.view' },
    ],
  },
  {
    id: 'debt',
    label: 'Công nợ',
    icon: Scale,
    color: 'bg-nav-debt',
    defaultOpen: true,
    items: [
      { to: '/debt/matching', label: 'Đối trừ chứng từ', icon: Link2, permission: 'debt.matching' },
      { to: '/debt/offsets', label: 'Bù trừ công nợ', icon: Combine, permission: 'debt_offset.view' },
      { to: '/debt/opening', label: 'Nợ đầu kỳ', icon: History, permission: 'debt.opening_balance' },
      { to: '/credit-limits', label: 'Hạn mức nợ', icon: Gauge, permission: 'credit_limit.manage' },
    ],
  },
  {
    id: 'reports',
    label: 'Báo cáo',
    icon: BarChart3,
    color: 'bg-nav-reports',
    defaultOpen: true,
    items: [
      { to: '/dashboard/executive', label: 'Điều hành', icon: Presentation, section: 'Tổng hợp', permission: 'dashboard.executive' },
      { to: '/reports/revenue', label: 'Doanh thu', icon: TrendingUp, section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/sales-revenue', label: 'Doanh thu sale', icon: BadgeDollarSign, section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/vehicle-revenue', label: 'Doanh thu xe', icon: CarFront, section: 'Doanh thu', permission: 'reports.revenue' },
      { to: '/reports/sales-performance', label: 'Hiệu suất sale', icon: Target, section: 'Doanh thu', permission: 'quotations.view_all' },
      { to: '/inventory/stock-on-hand', label: 'Tồn kho', icon: Boxes, section: 'Kho', permission: 'reports.inventory' },
      { to: '/inventory/stock-card', label: 'Thẻ kho', icon: ScrollText, section: 'Kho', permission: 'reports.inventory' },
      { to: '/inventory/stock-summary', label: 'Tổng hợp nhập xuất tồn', icon: Sigma, section: 'Kho', permission: 'reports.inventory' },
      { to: '/reports/cash-book', label: 'Sổ quỹ', icon: PiggyBank, section: 'Quỹ', permission: 'reports.cash' },
      { to: '/reports/debt-balance', label: 'Số dư công nợ', icon: BookOpen, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-ledger', label: 'Sổ chi tiết công nợ', icon: BookText, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-documents', label: 'Công nợ theo chứng từ', icon: Files, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-aging', label: 'Tuổi nợ', icon: Hourglass, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-overdue', label: 'Nợ quá hạn', icon: AlarmClock, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-by-salesperson', label: 'Công nợ theo nhân viên', icon: UserRound, section: 'Công nợ', permission: 'reports.debt' },
      { to: '/reports/debt-reconciliation', label: 'Biên bản đối chiếu', icon: FileCheck, section: 'Công nợ', permission: 'reports.debt' },
    ],
  },
  {
    id: 'catalogs',
    label: 'Danh mục',
    icon: Library,
    color: 'bg-nav-catalogs',
    defaultOpen: false,
    items: [
      { to: '/products', label: 'Hàng hóa', icon: Package, permission: 'products.view' },
      { to: '/product-groups', label: 'Nhóm hàng hóa', icon: Tag, permission: 'products.view' },
      { to: '/suppliers', label: 'Nhà cung cấp', icon: Truck, permission: 'suppliers.view' },
      { to: '/warehouses', label: 'Kho hàng', icon: Container, permission: 'inventory.catalogs.manage' },
      { to: '/stock-reasons', label: 'Lý do nhập xuất', icon: ListChecks, permission: 'inventory.catalogs.manage' },
      { to: '/cash-reasons', label: 'Lý do thu/chi', icon: ListOrdered, permission: 'cash.catalogs.manage' },
      { to: '/payment-methods', label: 'Hình thức thanh toán', icon: CreditCard, permission: 'inventory.catalogs.manage' },
      {
        to: '/funds',
        label: 'Quỹ',
        icon: Landmark,
        anyPermission: ['cash.catalogs.manage', 'cash.opening_balance'],
      },
    ],
  },
  {
    id: 'system',
    label: 'Hệ thống',
    icon: Settings,
    color: 'bg-nav-system',
    defaultOpen: false,
    items: [
      { to: '/settings/my-quotation-settings', label: 'Cài đặt của tôi', icon: UserCog },
      {
        to: '/settings',
        label: 'Cấu hình hệ thống',
        icon: SlidersHorizontal,
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
