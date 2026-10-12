import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from './protected-route';

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (
    selector: (s: {
      isAuthenticated: () => boolean;
      hasPermission: (p: string) => boolean;
      isInRole: () => boolean;
    }) => unknown,
  ) => selector({ isAuthenticated: () => true, hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderFunds() {
  return render(
    <MemoryRouter initialEntries={['/funds']}>
      <Routes>
        <Route
          path="/funds"
          element={
            <ProtectedRoute anyPermission={['cash.catalogs.manage', 'cash.opening_balance']}>
              <div>Trang quỹ</div>
            </ProtectedRoute>
          }
        />
        <Route path="/403" element={<div>Không có quyền</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('ProtectedRoute anyPermission', () => {
  it('opens with any one of the permissions', () => {
    granted = new Set(['cash.opening_balance']);
    renderFunds();
    expect(screen.getByText('Trang quỹ')).toBeInTheDocument();
  });

  it('redirects to 403 without any of them', () => {
    granted = new Set(['cash.settings']);
    renderFunds();
    expect(screen.getByText('Không có quyền')).toBeInTheDocument();
  });
});
