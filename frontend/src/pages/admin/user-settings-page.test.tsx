import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { UserSettingsPage } from './user-settings-page';

const SETTINGS = {
  userId: 'u1',
  userFullName: 'Nguyễn An',
  lockAtStatus: null,
  templateFileName: null,
  templateOriginalName: null,
  templateUploadedAt: null,
};

vi.mock('@/features/admin-user-settings/hooks', () => ({
  useUserSettings: () => ({ data: SETTINGS, isLoading: false }),
  useSetLockAt: () => ({ mutateAsync: vi.fn(), isPending: false }),
}));
vi.mock('@/components/auth/can', () => ({ Can: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function StateProbe() {
  return <div data-testid="state">{JSON.stringify(useLocation().state)}</div>;
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/admin/user-settings/u1']}>
      <Routes>
        <Route path="/admin/user-settings/:userId" element={<UserSettingsPage />} />
        <Route path="/admin/users/:userId/transfer-quotations" element={<StateProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('UserSettingsPage', () => {
  it('links back to the user list', () => {
    renderPage();
    expect(screen.getByRole('link', { name: 'Quay lại Quản lý người dùng' })).toHaveAttribute('href', '/admin/users');
  });

  it('opens the transfer page with its own url as state.from', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(screen.getByRole('button', { name: /Mở trang chuyển nhượng/ }));
    expect(JSON.parse(screen.getByTestId('state').textContent!)).toEqual({ from: '/admin/user-settings/u1' });
  });
});
