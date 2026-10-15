import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { BulkTransferPage } from './bulk-transfer-page';

vi.mock('@/features/admin-user-settings/hooks', () => ({
  useBulkTransfer: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock('@/features/admin-users/hooks', () => ({
  useAdminUsers: () => ({ data: [], isLoading: false, isError: false, error: null }),
}));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function renderPage(state?: { from: string }) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: '/admin/users/u1/transfer-quotations', state }]}>
      <Routes>
        <Route path="/admin/users/:userId/transfer-quotations" element={<BulkTransferPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('BulkTransferPage', () => {
  it('goes back to the user settings it was opened from', () => {
    renderPage({ from: '/admin/user-settings/u1' });
    expect(screen.getByRole('link', { name: 'Quay lại Cấu hình báo giá của user' })).toHaveAttribute(
      'href',
      '/admin/user-settings/u1',
    );
  });

  it('goes back to the user list with its search', () => {
    renderPage({ from: '/admin/users?search=an' });
    expect(screen.getByRole('link', { name: 'Quay lại Quản lý người dùng' })).toHaveAttribute(
      'href',
      '/admin/users?search=an',
    );
  });

  it('falls back to the user list without state', () => {
    renderPage();
    expect(screen.getByRole('link', { name: 'Quay lại Quản lý người dùng' })).toHaveAttribute('href', '/admin/users');
  });
});
