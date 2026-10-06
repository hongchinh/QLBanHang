import { Menu } from 'lucide-react';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { useLogout } from '@/features/auth/hooks';
import { BrandBlock } from './brand-block';
import { HeaderSearch } from './header-search';
import { HeaderSearchMobileSheet } from './header-search-mobile-sheet';
import { HeaderBranchSwitcher } from './header-branch-switcher';
import { HeaderNotifications } from './header-notifications';
import { HeaderUserMenu } from './header-user-menu';

export function AppHeader() {
  const user = useAuthStore((s) => s.user);
  const sidebarCollapsed = useUiStore((s) => s.sidebarCollapsed);
  const toggleSidebar = useUiStore((s) => s.toggleSidebar);
  const openMobileDrawer = useUiStore((s) => s.openMobileDrawer);
  const logout = useLogout();

  return (
    <header
      role="banner"
      className="col-span-full flex h-16 items-center bg-header-bg text-header-fg"
    >
      <button
        type="button"
        aria-label="Mở menu"
        onClick={openMobileDrawer}
        className="ml-2 flex h-11 w-11 items-center justify-center rounded-md text-header-fg hover:bg-header-active md:hidden"
      >
        <Menu className="h-5 w-5" />
      </button>

      <BrandBlock collapsed={sidebarCollapsed} onToggleCollapse={toggleSidebar} />

      <div className="flex flex-1 items-center gap-2 px-3 md:px-4">
        <HeaderSearch />
        <HeaderSearchMobileSheet />
        <div className="flex-1" />
        <HeaderBranchSwitcher />
        <HeaderNotifications />
        <HeaderUserMenu user={user} onLogout={() => logout.mutate()} />
      </div>
    </header>
  );
}
