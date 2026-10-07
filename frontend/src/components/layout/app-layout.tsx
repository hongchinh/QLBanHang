import { useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { LayoutDashboard } from 'lucide-react';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { useUiStore } from '@/stores/ui-store';
import { useNotificationHub } from '@/hooks/useNotificationHub';
import { useBranchContext } from '@/features/branches/use-branch-context';
import { TooltipProvider } from '@/components/ui/tooltip';
import { PageLoaderOverlay } from '@/components/ui/page-loader-overlay';
import { cn } from '@/lib/utils';
import { AppHeader } from './header/app-header';
import { Sidebar, type SidebarNavItem } from './sidebar/sidebar';
import { SkipToContent } from './skip-to-content';
import { visibleNavGroups } from './nav-config';

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
  // Keyed by the working branch: a switch remounts the page, so no state (selected warehouse,
  // unsaved lines) of the previous branch survives.
  const workingBranchId = useBranchStore((s) => s.workingBranchId);

  useEffect(() => {
    closeMobileDrawer();
  }, [location.pathname, closeMobileDrawer]);

  const dashboardItem: SidebarNavItem = hasPermission('quotations.view_all')
    ? { to: '/admin/dashboard', label: 'Tổng quan', icon: LayoutDashboard }
    : { to: '/', label: 'Tổng quan', icon: LayoutDashboard };

  const visibleGroups = visibleNavGroups(hasPermission, isInRole);

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
          {branchReady ? <Outlet key={workingBranchId ?? 'none'} /> : <PageLoaderOverlay open title="Đang tải chi nhánh..." />}
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
