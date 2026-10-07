import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { HeaderSearch } from '../header-search';

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
  return <div data-testid="location">{useLocation().pathname}</div>;
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

describe('HeaderSearch', () => {
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
});
