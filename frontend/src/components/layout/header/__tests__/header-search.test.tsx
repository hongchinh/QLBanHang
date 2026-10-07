import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { HeaderSearch } from '../header-search';
import { useAuthStore } from '@/stores/auth-store';

vi.mock('@/features/search/hooks', () => ({
  SEARCH_MIN_LENGTH: 3,
  useGlobalSearch: () => ({
    data: {
      customers: [{ id: 'c1', code: 'KH0001', name: 'Khách Thép', status: 'Active' }],
      suppliers: [{ id: 's1', code: 'NCC0001', name: 'Nhà máy Thép', status: 'Active' }],
      quotations: [],
    },
    isFetching: false,
  }),
}));

function LocationDisplay() {
  const { pathname, search } = useLocation();
  return <div data-testid="location">{pathname + search}</div>;
}

function renderSearch() {
  render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route
          path="*"
          element={
            <>
              <HeaderSearch />
              <LocationDisplay />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

function loginWith(permissions: string[]) {
  act(() => {
    useAuthStore.setState({
      accessToken: 'token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 'u1', username: 'u', email: 'u@example.com', fullName: 'U', roles: [], permissions },
    });
  });
}

describe('HeaderSearch', () => {
  beforeEach(() => {
    loginWith(['customers.view', 'customers.update', 'suppliers.view', 'suppliers.update']);
  });

  it('supplier hits link to /suppliers/{id}', async () => {
    const user = userEvent.setup();
    renderSearch();

    await user.type(screen.getByLabelText('Tìm kiếm toàn cục'), 'thép');
    expect(await screen.findByText('Nhà cung cấp')).toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: /Nhà máy Thép/ }));

    expect(screen.getByTestId('location')).toHaveTextContent('/suppliers/s1');
  });

  it('customer hits keep linking to /customers/{id}', async () => {
    const user = userEvent.setup();
    renderSearch();

    await user.type(screen.getByLabelText('Tìm kiếm toàn cục'), 'thép');
    await user.click(await screen.findByRole('option', { name: /Khách Thép/ }));

    expect(screen.getByTestId('location')).toHaveTextContent('/customers/c1');
  });

  it('without suppliers.update a supplier hit opens the supplier list filtered by its code', async () => {
    loginWith(['suppliers.view']); // WAREHOUSE default grants (D23)
    const user = userEvent.setup();
    renderSearch();

    await user.type(screen.getByLabelText('Tìm kiếm toàn cục'), 'thép');
    await user.click(await screen.findByRole('option', { name: /Nhà máy Thép/ }));

    expect(screen.getByTestId('location')).toHaveTextContent('/suppliers?q=NCC0001');
  });

  it('keyboard Enter follows the same rule', async () => {
    loginWith(['customers.view']);
    const user = userEvent.setup();
    renderSearch();

    await user.type(screen.getByLabelText('Tìm kiếm toàn cục'), 'thép');
    await screen.findByRole('option', { name: /Khách Thép/ });
    await user.keyboard('{ArrowDown}{Enter}');

    expect(screen.getByTestId('location')).toHaveTextContent('/customers?q=KH0001');
  });
});
