import { useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import {
  LayoutDashboard,
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
} from 'lucide-react';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { useNotificationHub } from '@/hooks/useNotificationHub';
import { useBranchContext } from '@/features/branches/use-branch-context';
import type { Permission, Role } from '@/lib/permissions';
import { TooltipProvider } from '@/components/ui/tooltip';
import { PageLoaderOverlay } from '@/components/ui/page-loader-overlay';
import { cn } from '@/lib/utils';
import { AppHeader } from './header/app-header';
import { Sidebar, type SidebarNavGroup, type SidebarNavItem } from './sidebar/sidebar';
import { SkipToContent } from './skip-to-content';

interface NavItem extends SidebarNavItem {
  permission?: Permission;
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
      { to: '/settings', label: 'Cấu hình hệ thống', icon: Settings, permission: 'system.manage_settings' },
      { to: '/admin/users', label: 'Quản lý người dùng', icon: Users2, permission: 'user_settings.manage' },
      { to: '/admin/roles', label: 'Phân quyền', icon: ShieldCheck, permission: 'roles.view' },
    ],
  },
];

export function AppLayout() {
  const hasPermission = useAuthStore((s) => s.hasPermission);
  const isInRole = useAuthStore((s) => s.isInRole);
  const location = useLocation();

  const sidebarCollapsed = useUiStore((s) => s.sidebarCollapsed);
  const mobileDrawerOpen = useUiStore((s) => s.mobileDrawerOpen);
  const closeMobileDrawer = useUiStore((s) => s.closeMobileDrawer);

  useNotificationHub();
  // Pages render only once the working branch is in the store, so their first
  // request already carries X-Branch-Id.
  const { ready: branchReady } = useBranchContext();

  useEffect(() => {
    closeMobileDrawer();
  }, [location.pathname, closeMobileDrawer]);

  const dashboardItem: SidebarNavItem = hasPermission('quotations.view_all')
    ? { to: '/admin/dashboard', label: 'Tổng quan', icon: LayoutDashboard }
    : { to: '/', label: 'Tổng quan', icon: LayoutDashboard };

  const visibleGroups: SidebarNavGroup[] = navGroups
    .map((group) => ({
      label: group.label,
      items: group.items.filter((item) => {
        if (item.permission && !hasPermission(item.permission)) return false;
        if (item.role && !isInRole(item.role)) return false;
        return true;
      }),
    }))
    .filter((group) => group.items.length > 0);

  return (
    <TooltipProvider delayDuration={200}>
      <div className="grid h-screen grid-cols-1 grid-rows-[4rem_1fr] overflow-hidden bg-muted/30 md:grid-cols-[auto_1fr]">
        <SkipToContent />
        <AppHeader />

        <aside
          className={cn(
            'hidden flex-col border-r bg-card transition-[width] duration-200 ease-in-out md:flex',
            sidebarCollapsed ? 'w-16' : 'w-60',
          )}
        >
          <Sidebar
            dashboardItem={dashboardItem}
            groups={visibleGroups}
            collapsed={sidebarCollapsed}
          />
        </aside>

        <main
          id="main-content"
          className="overflow-y-auto p-4 md:p-3"
        >
          {branchReady ? <Outlet /> : <PageLoaderOverlay open title="Đang tải chi nhánh..." />}
        </main>

        {mobileDrawerOpen && (
          <div
            className="fixed inset-0 z-40 bg-black/40 md:hidden"
            onClick={closeMobileDrawer}
            aria-hidden
          />
        )}
        <aside
          className={cn(
            'fixed inset-y-0 left-0 z-50 flex w-60 flex-col border-r bg-card transition-transform md:hidden',
            mobileDrawerOpen ? 'translate-x-0' : '-translate-x-full',
          )}
        >
          <Sidebar
            dashboardItem={dashboardItem}
            groups={visibleGroups}
            collapsed={false}
            onClose={closeMobileDrawer}
          />
        </aside>
      </div>
    </TooltipProvider>
  );
}
