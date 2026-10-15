import { act, render } from '@testing-library/react';
import { MemoryRouter, useLocation, useNavigate, type NavigateFunction } from 'react-router-dom';
import { TooltipProvider } from '@/components/ui/tooltip';
import { useUiStore } from '@/stores/ui-store';
import { visibleNavGroups, type NavGroup, type NavItem } from '../nav-config';
import { Sidebar } from './sidebar';

const DASHBOARD: NavItem = { to: '/admin/dashboard', label: 'Tổng quan', icon: 'overview' };

export function allNavGroups(): NavGroup[] {
  return visibleNavGroups(() => true, () => true);
}

interface Options { groups?: NavGroup[]; onClose?: () => void }

// Renders the sidebar the way AppLayout does: `collapsed` comes from the ui store.
export function renderSidebar(path: string, { groups = allNavGroups(), onClose }: Options = {}) {
  const probe: { navigate?: NavigateFunction; pathname?: string } = {};
  function Probe() {
    probe.navigate = useNavigate();
    probe.pathname = useLocation().pathname;
    return null;
  }
  function Harness() {
    const collapsed = useUiStore((s) => s.sidebarCollapsed);
    return <Sidebar dashboardItem={DASHBOARD} groups={groups} collapsed={collapsed} onClose={onClose} />;
  }
  const utils = render(
    <MemoryRouter initialEntries={[path]}>
      <TooltipProvider>
        <Harness />
        <Probe />
      </TooltipProvider>
    </MemoryRouter>,
  );
  return {
    ...utils,
    navigate: (to: string) => act(() => { probe.navigate!(to); }),
    pathname: () => probe.pathname,
  };
}
