import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { UsersListPage } from './users-list-page';

vi.mock('@/features/admin-users/hooks', () => ({
  useAdminUsers: () => ({ data: [], isLoading: false, isError: false, error: null }),
}));
vi.mock('@/pages/admin/components/user-actions-menu', () => ({ UserActionsMenu: () => null }));
vi.mock('@/pages/admin/components/user-form-dialog', () => ({ UserFormDialog: () => null }));
vi.mock('@/components/auth/can', () => ({ Can: ({ children }: { children: React.ReactNode }) => <>{children}</> }));

describe('UsersListPage', () => {
  it('links back to the settings hub', () => {
    render(
      <MemoryRouter>
        <UsersListPage />
      </MemoryRouter>,
    );
    expect(screen.getByRole('link', { name: 'Quay lại Cấu hình' })).toHaveAttribute('href', '/settings');
  });
});
