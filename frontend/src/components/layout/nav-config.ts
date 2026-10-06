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
