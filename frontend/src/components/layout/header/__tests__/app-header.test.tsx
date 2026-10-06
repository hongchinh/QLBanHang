import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppHeader } from '../app-header';
import { TooltipProvider } from '@/components/ui/tooltip';
import { useAuthStore, type CurrentUser } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';

vi.mock('@/features/branches/hooks', () => ({ useMyBranches: () => ({ data: undefined }) }));

const sampleUser: CurrentUser = {
  id: 'u1',
  username: 'nva',
  email: 'nva@example.com',
  fullName: 'Nguyễn Văn A',
  roles: ['SALE'],
  permissions: [],
};

function renderHeader() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <TooltipProvider>
          <AppHeader />
        </TooltipProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('AppHeader', () => {
  beforeEach(() => {
    act(() => {
      useAuthStore.setState({
        accessToken: 'token',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        user: sampleUser,
      });
      useUiStore.setState({ sidebarCollapsed: false, mobileDrawerOpen: false });
    });
  });

  afterEach(() => {
    act(() => {
      useAuthStore.getState().logout();
      useUiStore.setState({ sidebarCollapsed: false, mobileDrawerOpen: false });
    });
  });

  it('renders the user fullName initials as the avatar fallback', () => {
    renderHeader();
    // Avatar fallback shows initials "NA" for "Nguyễn Văn A".
    expect(screen.getByText('NA')).toBeInTheDocument();
  });

  it('opens the mobile drawer when the mobile menu button is clicked', () => {
    renderHeader();
    fireEvent.click(screen.getByLabelText('Mở menu'));
    expect(useUiStore.getState().mobileDrawerOpen).toBe(true);
  });

  it('toggles the sidebar collapsed state when the brand-block toggle is clicked', () => {
    renderHeader();
    expect(useUiStore.getState().sidebarCollapsed).toBe(false);
    fireEvent.click(screen.getByLabelText('Thu gọn menu'));
    expect(useUiStore.getState().sidebarCollapsed).toBe(true);
  });

  it('swaps the brand-block toggle to "Mở rộng menu" when sidebarCollapsed is true', () => {
    act(() => {
      useUiStore.setState({ sidebarCollapsed: true });
    });
    renderHeader();
    // The desktop brand-block now renders only the expand toggle, so the
    // collapse toggle (rendered when expanded) is gone.
    expect(screen.getByLabelText('Mở rộng menu')).toBeInTheDocument();
    expect(screen.queryByLabelText('Thu gọn menu')).not.toBeInTheDocument();
  });
});
